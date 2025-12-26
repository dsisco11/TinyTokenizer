using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace TinyTokenizer;

/// <summary>
/// Parses simple tokens into semantic tokens.
/// Level 2 of the two-level tokenizer architecture.
/// Handles blocks, strings, comments and emits error tokens for failures.
/// </summary>
public sealed class TokenParser
{
    private readonly TokenizerOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="TokenParser"/> with default options.
    /// </summary>
    public TokenParser() : this(TokenizerOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TokenParser"/> with the specified options.
    /// </summary>
    public TokenParser(TokenizerOptions options)
    {
        _options = options;
    }

    #region Synchronous Parsing

    /// <summary>
    /// Parses simple tokens into semantic tokens.
    /// </summary>
    public IEnumerable<Token> Parse(IEnumerable<SimpleToken> simpleTokens)
    {
        using var enumerator = simpleTokens.GetEnumerator();
        var reader = new TokenReader(enumerator);

        while (reader.TryPeek(out _))
        {
            foreach (var parsed in ParseToken(reader, expectedCloser: null))
            {
                yield return parsed;
            }
        }
    }

    /// <summary>
    /// Parses simple tokens into an immutable array of semantic tokens.
    /// </summary>
    public ImmutableArray<Token> ParseToArray(IEnumerable<SimpleToken> simpleTokens)
    {
        return [.. Parse(simpleTokens)];
    }

    #endregion

    #region Asynchronous Parsing

    /// <summary>
    /// Parses simple tokens into semantic tokens asynchronously.
    /// </summary>
    public async IAsyncEnumerable<Token> ParseAsync(
        IAsyncEnumerable<SimpleToken> simpleTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = new AsyncTokenReader(simpleTokens, cancellationToken);

        try
        {
            while (await reader.TryPeekAsync())
            {
                await foreach (var token in ParseTokenAsync(reader, expectedCloser: null))
                {
                    yield return token;
                }
            }

            // Flush any unclosed state
            foreach (var error in reader.FlushErrors())
            {
                yield return error;
            }
        }
        finally
        {
            await reader.DisposeAsync();
        }
    }

    #endregion

    #region Token Parsing Logic

    private IEnumerable<Token> ParseToken(TokenReader reader, SimpleTokenType? expectedCloser)
    {
        if (!reader.TryPeek(out var token))
            yield break;

        // Check for expected closing delimiter
        if (expectedCloser.HasValue && token.Type == expectedCloser.Value)
            yield break;

        // Check for unexpected closing delimiter
        if (IsClosingDelimiter(token.Type) && token.Type != expectedCloser)
        {
            reader.Advance();
            yield return new ErrorToken(
                token.Content,
                $"Unexpected closing delimiter '{token.FirstChar}'",
                token.Position);
            yield break;
        }

        // Opening delimiter - parse block
        if (IsOpeningDelimiter(token.Type))
        {
            var block = ParseBlock(reader);
            yield return block;
            yield break;
        }

        // Quote - parse string
        if (token.Type == SimpleTokenType.SingleQuote || token.Type == SimpleTokenType.DoubleQuote)
        {
            var str = ParseString(reader);
            yield return str;
            yield break;
        }

        // Digits - check for potential decimal number (Digits + Dot + Digits)
        if (token.Type == SimpleTokenType.Digits)
        {
            var numericToken = ParseNumericFromDigits(reader);
            yield return numericToken;
            yield break;
        }

        // Dot - could be start of decimal number (.123) or standalone symbol
        if (token.Type == SimpleTokenType.Dot)
        {
            // Check if followed by digits (e.g., .123)
            if (reader.TryPeek(1, out var next) && next.Type == SimpleTokenType.Digits)
            {
                var numericToken = ParseNumericFromDot(reader);
                yield return numericToken;
                yield break;
            }

            // Standalone dot - emit as symbol
            reader.Advance();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        // Comment start detection (// or /*)
        if (token.Type == SimpleTokenType.Slash)
        {
            if (reader.TryPeek(1, out var next))
            {
                var commentResult = TryParseComment(reader, token, next);
                if (commentResult != null)
                {
                    yield return commentResult;
                    yield break;
                }
            }

            // Not a comment - emit as symbol
            reader.Advance();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        // Asterisk as standalone symbol
        if (token.Type == SimpleTokenType.Asterisk)
        {
            reader.Advance();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        // Backslash outside of string - emit as symbol
        if (token.Type == SimpleTokenType.Backslash)
        {
            reader.Advance();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        // Simple pass-through tokens
        reader.Advance();
        yield return token.Type switch
        {
            SimpleTokenType.Text => new TextToken(token.Content, token.Position),
            SimpleTokenType.Whitespace => new WhitespaceToken(token.Content, token.Position),
            SimpleTokenType.Newline => new WhitespaceToken(token.Content, token.Position),
            SimpleTokenType.Symbol => new SymbolToken(token.Content, token.Position),
            _ => new TextToken(token.Content, token.Position)
        };
    }

    private Token ParseBlock(TokenReader reader)
    {
        reader.TryPeek(out var openToken);
        reader.Advance();

        var opener = openToken.FirstChar;
        var closerType = GetMatchingCloser(openToken.Type);
        var blockType = GetBlockTokenType(openToken.Type);
        var startPosition = openToken.Position;

        var children = ImmutableArray.CreateBuilder<Token>();
        var contentBuilder = new List<char>();
        
        // Add opener to full content
        AppendToBuffer(contentBuilder, openToken.Content);

        var innerStart = contentBuilder.Count;

        // Parse children until closing delimiter or end
        while (reader.TryPeek(out var token))
        {
            if (token.Type == closerType)
            {
                // Found closer
                var innerContent = contentBuilder.Skip(innerStart).ToArray();
                
                // Add closer
                AppendToBuffer(contentBuilder, token.Content);
                
                reader.Advance();

                var fullContent = contentBuilder.ToArray().AsMemory();
                var innerMemory = innerContent.AsMemory();
                var closer = token.FirstChar;

                return new BlockToken(
                    fullContent,
                    innerMemory,
                    children.ToImmutable(),
                    blockType,
                    opener,
                    closer,
                    startPosition);
            }

            // Parse child token
            foreach (var child in ParseToken(reader, closerType))
            {
                children.Add(child);
                AppendToBuffer(contentBuilder, child.Content);
            }
        }

        // Unclosed block
        return new ErrorToken(
            openToken.Content,
            $"Unclosed block starting with '{opener}'",
            startPosition);
    }

    private Token ParseString(TokenReader reader)
    {
        reader.TryPeek(out var quoteToken);
        reader.Advance();

        var quote = quoteToken.FirstChar;
        var startPosition = quoteToken.Position;
        var contentBuilder = new List<char>();
        
        // Add opening quote
        contentBuilder.Add(quote);

        var quoteType = quoteToken.Type;
        bool escaped = false;

        while (reader.TryPeek(out var token))
        {
            if (escaped)
            {
                // Consume escaped character
                AppendToBuffer(contentBuilder, token.Content);
                reader.Advance();
                escaped = false;
                continue;
            }

            if (token.Type == SimpleTokenType.Backslash)
            {
                contentBuilder.Add('\\');
                reader.Advance();
                escaped = true;
                continue;
            }

            if (token.Type == quoteType)
            {
                // Closing quote
                contentBuilder.Add(quote);
                reader.Advance();

                var content = contentBuilder.ToArray().AsMemory();
                return new StringToken(content, quote, startPosition);
            }

            // Regular content
            AppendToBuffer(contentBuilder, token.Content);
            reader.Advance();
        }

        // Unterminated string - emit opening quote as symbol
        return new SymbolToken(quoteToken.Content, startPosition);
    }

    private Token? TryParseComment(TokenReader reader, SimpleToken slashToken, SimpleToken nextToken)
    {
        // Check for C-style comments
        var hasCSingleLine = _options.CommentStyles.Any(s => s.Start == "//");
        var hasCMultiLine = _options.CommentStyles.Any(s => s.Start == "/*");

        // Single-line comment: //
        if (hasCSingleLine && nextToken.Type == SimpleTokenType.Slash)
        {
            return ParseSingleLineComment(reader);
        }

        // Multi-line comment: /*
        if (hasCMultiLine && nextToken.Type == SimpleTokenType.Asterisk)
        {
            return ParseMultiLineComment(reader);
        }

        return null;
    }

    private CommentToken ParseSingleLineComment(TokenReader reader)
    {
        var contentBuilder = new List<char>();
        long startPosition = 0;

        // Consume first slash
        if (reader.TryPeek(out var slash1))
        {
            startPosition = slash1.Position;
            contentBuilder.Add('/');
            reader.Advance();
        }

        // Consume second slash
        if (reader.TryPeek(out _))
        {
            contentBuilder.Add('/');
            reader.Advance();
        }

        // Consume until newline
        while (reader.TryPeek(out var token))
        {
            if (token.Type == SimpleTokenType.Newline)
                break;

            AppendToBuffer(contentBuilder, token.Content);
            reader.Advance();
        }

        var content = contentBuilder.ToArray().AsMemory();
        return new CommentToken(content, IsMultiLine: false, startPosition);
    }

    private Token ParseMultiLineComment(TokenReader reader)
    {
        var contentBuilder = new List<char>();
        long startPosition = 0;

        // Consume /
        if (reader.TryPeek(out var slash))
        {
            startPosition = slash.Position;
            contentBuilder.Add('/');
            reader.Advance();
        }

        // Consume *
        if (reader.TryPeek(out _))
        {
            contentBuilder.Add('*');
            reader.Advance();
        }

        // Consume until */
        while (reader.TryPeek(out var token))
        {
            if (token.Type == SimpleTokenType.Asterisk)
            {
                if (reader.TryPeek(1, out var next) && next.Type == SimpleTokenType.Slash)
                {
                    // Found */
                    contentBuilder.Add('*');
                    contentBuilder.Add('/');
                    reader.Advance(); // *
                    reader.Advance(); // /

                    var content = contentBuilder.ToArray().AsMemory();
                    return new CommentToken(content, IsMultiLine: true, startPosition);
                }
            }

            AppendToBuffer(contentBuilder, token.Content);
            reader.Advance();
        }

        // Unterminated comment
        var errorContent = contentBuilder.ToArray().AsMemory();
        return new ErrorToken(
            errorContent,
            "Unterminated multi-line comment",
            startPosition);
    }

    /// <summary>
    /// Parses a numeric token starting from a Digits token.
    /// Handles patterns: 123, 123.456
    /// </summary>
    private NumericToken ParseNumericFromDigits(TokenReader reader)
    {
        reader.TryPeek(out var digitsToken);
        var startPosition = digitsToken.Position;
        var contentBuilder = new List<char>();
        
        // Add the initial digits
        AppendToBuffer(contentBuilder, digitsToken.Content);
        reader.Advance();

        bool hasDecimal = false;

        // Check for decimal point followed by more digits
        if (reader.TryPeek(out var dotToken) && dotToken.Type == SimpleTokenType.Dot)
        {
            if (reader.TryPeek(1, out var afterDot) && afterDot.Type == SimpleTokenType.Digits)
            {
                // It's a decimal number: add dot and digits
                contentBuilder.Add('.');
                reader.Advance(); // consume dot

                AppendToBuffer(contentBuilder, afterDot.Content);
                reader.Advance(); // consume digits after dot

                hasDecimal = true;
            }
        }

        var content = contentBuilder.ToArray().AsMemory();
        var numericType = hasDecimal ? NumericType.FloatingPoint : NumericType.Integer;
        return new NumericToken(content, numericType, startPosition);
    }

    /// <summary>
    /// Parses a numeric token starting from a Dot token.
    /// Handles pattern: .456
    /// </summary>
    private NumericToken ParseNumericFromDot(TokenReader reader)
    {
        reader.TryPeek(out var dotToken);
        var startPosition = dotToken.Position;
        var contentBuilder = new List<char>();

        // Add the leading dot
        contentBuilder.Add('.');
        reader.Advance();

        // Add the digits after the dot
        if (reader.TryPeek(out var digitsToken) && digitsToken.Type == SimpleTokenType.Digits)
        {
            AppendToBuffer(contentBuilder, digitsToken.Content);
            reader.Advance();
        }

        var content = contentBuilder.ToArray().AsMemory();
        return new NumericToken(content, NumericType.FloatingPoint, startPosition);
    }

    #endregion

    #region Async Token Parsing

    private async IAsyncEnumerable<Token> ParseTokenAsync(
        AsyncTokenReader reader,
        SimpleTokenType? expectedCloser)
    {
        if (!await reader.TryPeekAsync())
            yield break;

        var token = reader.Current;

        // Check for expected closing delimiter
        if (expectedCloser.HasValue && token.Type == expectedCloser.Value)
            yield break;

        // Check for unexpected closing delimiter
        if (IsClosingDelimiter(token.Type) && token.Type != expectedCloser)
        {
            await reader.AdvanceAsync();
            yield return new ErrorToken(
                token.Content,
                $"Unexpected closing delimiter '{token.FirstChar}'",
                token.Position);
            yield break;
        }

        // Opening delimiter - parse block
        if (IsOpeningDelimiter(token.Type))
        {
            var block = await ParseBlockAsync(reader);
            yield return block;
            yield break;
        }

        // Quote - parse string
        if (token.Type == SimpleTokenType.SingleQuote || token.Type == SimpleTokenType.DoubleQuote)
        {
            var str = await ParseStringAsync(reader);
            yield return str;
            yield break;
        }

        // Digits - check for potential decimal number (Digits + Dot + Digits)
        if (token.Type == SimpleTokenType.Digits)
        {
            var numericToken = await ParseNumericFromDigitsAsync(reader);
            yield return numericToken;
            yield break;
        }

        // Dot - could be start of decimal number (.123) or standalone symbol
        if (token.Type == SimpleTokenType.Dot)
        {
            // Check if followed by digits (e.g., .123)
            if (await reader.TryPeekAsync(1))
            {
                var next = reader.PeekAhead(1);
                if (next.HasValue && next.Value.Type == SimpleTokenType.Digits)
                {
                    var numericToken = await ParseNumericFromDotAsync(reader);
                    yield return numericToken;
                    yield break;
                }
            }

            // Standalone dot - emit as symbol
            await reader.AdvanceAsync();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        // Comment start detection
        if (token.Type == SimpleTokenType.Slash)
        {
            if (await reader.TryPeekAsync(1))
            {
                var next = reader.PeekAhead(1);
                if (next.HasValue)
                {
                    var commentResult = await TryParseCommentAsync(reader, token, next.Value);
                    if (commentResult != null)
                    {
                        yield return commentResult;
                        yield break;
                    }
                }
            }

            await reader.AdvanceAsync();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        // Other simple tokens
        if (token.Type == SimpleTokenType.Asterisk || token.Type == SimpleTokenType.Backslash)
        {
            await reader.AdvanceAsync();
            yield return new SymbolToken(token.Content, token.Position);
            yield break;
        }

        await reader.AdvanceAsync();
        yield return token.Type switch
        {
            SimpleTokenType.Text => new TextToken(token.Content, token.Position),
            SimpleTokenType.Whitespace => new WhitespaceToken(token.Content, token.Position),
            SimpleTokenType.Newline => new WhitespaceToken(token.Content, token.Position),
            SimpleTokenType.Symbol => new SymbolToken(token.Content, token.Position),
            _ => new TextToken(token.Content, token.Position)
        };
    }

    private async Task<Token> ParseBlockAsync(AsyncTokenReader reader)
    {
        var openToken = reader.Current;
        await reader.AdvanceAsync();

        var opener = openToken.FirstChar;
        var closerType = GetMatchingCloser(openToken.Type);
        var blockType = GetBlockTokenType(openToken.Type);
        var startPosition = openToken.Position;

        var children = ImmutableArray.CreateBuilder<Token>();
        var contentBuilder = new List<char>();

        AppendToBuffer(contentBuilder, openToken.Content);

        var innerStart = contentBuilder.Count;

        while (await reader.TryPeekAsync())
        {
            var token = reader.Current;

            if (token.Type == closerType)
            {
                var innerContent = contentBuilder.Skip(innerStart).ToArray();

                AppendToBuffer(contentBuilder, token.Content);

                await reader.AdvanceAsync();

                return new BlockToken(
                    contentBuilder.ToArray().AsMemory(),
                    innerContent.AsMemory(),
                    children.ToImmutable(),
                    blockType,
                    opener,
                    token.FirstChar,
                    startPosition);
            }

            await foreach (var child in ParseTokenAsync(reader, closerType))
            {
                children.Add(child);
                AppendToBuffer(contentBuilder, child.Content);
            }
        }

        return new ErrorToken(
            openToken.Content,
            $"Unclosed block starting with '{opener}'",
            startPosition);
    }

    private async Task<Token> ParseStringAsync(AsyncTokenReader reader)
    {
        var quoteToken = reader.Current;
        await reader.AdvanceAsync();

        var quote = quoteToken.FirstChar;
        var startPosition = quoteToken.Position;
        var contentBuilder = new List<char> { quote };
        var quoteType = quoteToken.Type;
        bool escaped = false;

        while (await reader.TryPeekAsync())
        {
            var token = reader.Current;

            if (escaped)
            {
                AppendToBuffer(contentBuilder, token.Content);
                await reader.AdvanceAsync();
                escaped = false;
                continue;
            }

            if (token.Type == SimpleTokenType.Backslash)
            {
                contentBuilder.Add('\\');
                await reader.AdvanceAsync();
                escaped = true;
                continue;
            }

            if (token.Type == quoteType)
            {
                contentBuilder.Add(quote);
                await reader.AdvanceAsync();
                return new StringToken(contentBuilder.ToArray().AsMemory(), quote, startPosition);
            }

            AppendToBuffer(contentBuilder, token.Content);
            await reader.AdvanceAsync();
        }

        return new SymbolToken(quoteToken.Content, startPosition);
    }

    private async Task<Token?> TryParseCommentAsync(AsyncTokenReader reader, SimpleToken slashToken, SimpleToken nextToken)
    {
        var hasCSingleLine = _options.CommentStyles.Any(s => s.Start == "//");
        var hasCMultiLine = _options.CommentStyles.Any(s => s.Start == "/*");

        if (hasCSingleLine && nextToken.Type == SimpleTokenType.Slash)
        {
            return await ParseSingleLineCommentAsync(reader);
        }

        if (hasCMultiLine && nextToken.Type == SimpleTokenType.Asterisk)
        {
            return await ParseMultiLineCommentAsync(reader);
        }

        return null;
    }

    private async Task<CommentToken> ParseSingleLineCommentAsync(AsyncTokenReader reader)
    {
        var contentBuilder = new List<char>();
        long startPosition = reader.Current.Position;

        // Consume //
        contentBuilder.Add('/');
        await reader.AdvanceAsync();
        contentBuilder.Add('/');
        await reader.AdvanceAsync();

        while (await reader.TryPeekAsync())
        {
            if (reader.Current.Type == SimpleTokenType.Newline)
                break;

            AppendToBuffer(contentBuilder, reader.Current.Content);
            await reader.AdvanceAsync();
        }

        return new CommentToken(contentBuilder.ToArray().AsMemory(), IsMultiLine: false, startPosition);
    }

    private async Task<Token> ParseMultiLineCommentAsync(AsyncTokenReader reader)
    {
        var contentBuilder = new List<char>();
        long startPosition = reader.Current.Position;

        // Consume /*
        contentBuilder.Add('/');
        await reader.AdvanceAsync();
        contentBuilder.Add('*');
        await reader.AdvanceAsync();

        while (await reader.TryPeekAsync())
        {
            var token = reader.Current;

            if (token.Type == SimpleTokenType.Asterisk)
            {
                if (await reader.TryPeekAsync(1))
                {
                    var next = reader.PeekAhead(1);
                    if (next.HasValue && next.Value.Type == SimpleTokenType.Slash)
                    {
                        contentBuilder.Add('*');
                        contentBuilder.Add('/');
                        await reader.AdvanceAsync();
                        await reader.AdvanceAsync();
                        return new CommentToken(contentBuilder.ToArray().AsMemory(), IsMultiLine: true, startPosition);
                    }
                }
            }

            AppendToBuffer(contentBuilder, token.Content);
            await reader.AdvanceAsync();
        }

        return new ErrorToken(
            contentBuilder.ToArray().AsMemory(),
            "Unterminated multi-line comment",
            startPosition);
    }

    /// <summary>
    /// Parses a numeric token starting from a Digits token asynchronously.
    /// Handles patterns: 123, 123.456
    /// </summary>
    private async Task<NumericToken> ParseNumericFromDigitsAsync(AsyncTokenReader reader)
    {
        var digitsToken = reader.Current;
        var startPosition = digitsToken.Position;
        var contentBuilder = new List<char>();

        // Add the initial digits
        AppendToBuffer(contentBuilder, digitsToken.Content);
        await reader.AdvanceAsync();

        bool hasDecimal = false;

        // Check for decimal point followed by more digits
        if (await reader.TryPeekAsync())
        {
            var dotToken = reader.Current;
            if (dotToken.Type == SimpleTokenType.Dot)
            {
                if (await reader.TryPeekAsync(1))
                {
                    var afterDot = reader.PeekAhead(1);
                    if (afterDot.HasValue && afterDot.Value.Type == SimpleTokenType.Digits)
                    {
                        // It's a decimal number: add dot and digits
                        contentBuilder.Add('.');
                        await reader.AdvanceAsync(); // consume dot

                        AppendToBuffer(contentBuilder, afterDot.Value.Content);
                        await reader.AdvanceAsync(); // consume digits after dot

                        hasDecimal = true;
                    }
                }
            }
        }

        var content = contentBuilder.ToArray().AsMemory();
        var numericType = hasDecimal ? NumericType.FloatingPoint : NumericType.Integer;
        return new NumericToken(content, numericType, startPosition);
    }

    /// <summary>
    /// Parses a numeric token starting from a Dot token asynchronously.
    /// Handles pattern: .456
    /// </summary>
    private async Task<NumericToken> ParseNumericFromDotAsync(AsyncTokenReader reader)
    {
        var dotToken = reader.Current;
        var startPosition = dotToken.Position;
        var contentBuilder = new List<char>();

        // Add the leading dot
        contentBuilder.Add('.');
        await reader.AdvanceAsync();

        // Add the digits after the dot
        if (await reader.TryPeekAsync())
        {
            var digitsToken = reader.Current;
            if (digitsToken.Type == SimpleTokenType.Digits)
            {
                AppendToBuffer(contentBuilder, digitsToken.Content);
                await reader.AdvanceAsync();
            }
        }

        var content = contentBuilder.ToArray().AsMemory();
        return new NumericToken(content, NumericType.FloatingPoint, startPosition);
    }

    #endregion

    #region Helper Methods

    private static bool IsOpeningDelimiter(SimpleTokenType type)
    {
        return type is SimpleTokenType.OpenBrace or SimpleTokenType.OpenBracket or SimpleTokenType.OpenParen;
    }

    private static bool IsClosingDelimiter(SimpleTokenType type)
    {
        return type is SimpleTokenType.CloseBrace or SimpleTokenType.CloseBracket or SimpleTokenType.CloseParen;
    }

    private static SimpleTokenType GetMatchingCloser(SimpleTokenType opener)
    {
        return opener switch
        {
            SimpleTokenType.OpenBrace => SimpleTokenType.CloseBrace,
            SimpleTokenType.OpenBracket => SimpleTokenType.CloseBracket,
            SimpleTokenType.OpenParen => SimpleTokenType.CloseParen,
            _ => throw new ArgumentException($"Not an opening delimiter: {opener}")
        };
    }

    private static TokenType GetBlockTokenType(SimpleTokenType opener)
    {
        return opener switch
        {
            SimpleTokenType.OpenBrace => TokenType.BraceBlock,
            SimpleTokenType.OpenBracket => TokenType.BracketBlock,
            SimpleTokenType.OpenParen => TokenType.ParenthesisBlock,
            _ => throw new ArgumentException($"Not an opening delimiter: {opener}")
        };
    }

    /// <summary>
    /// Appends memory content to a buffer without using Span in async context.
    /// </summary>
    private static void AppendToBuffer(List<char> buffer, ReadOnlyMemory<char> content)
    {
        for (int i = 0; i < content.Length; i++)
        {
            buffer.Add(content.Span[i]);
        }
    }

    /// <summary>
    /// Checks if memory contains a specific character without using Span.Contains in non-sync context.
    /// </summary>
    private static bool ContainsChar(ReadOnlyMemory<char> content, char c)
    {
        for (int i = 0; i < content.Length; i++)
        {
            if (content.Span[i] == c)
                return true;
        }
        return false;
    }

    #endregion

    #region Token Reader Classes

    /// <summary>
    /// Synchronous token reader with lookahead support.
    /// </summary>
    private sealed class TokenReader : IDisposable
    {
        private readonly IEnumerator<SimpleToken> _enumerator;
        private readonly List<SimpleToken> _buffer = new();
        private int _position;
        private bool _exhausted;

        public TokenReader(IEnumerator<SimpleToken> enumerator)
        {
            _enumerator = enumerator;
        }

        public bool TryPeek(out SimpleToken token)
        {
            return TryPeek(0, out token);
        }

        public bool TryPeek(int offset, out SimpleToken token)
        {
            var targetIndex = _position + offset;

            while (_buffer.Count <= targetIndex && !_exhausted)
            {
                if (_enumerator.MoveNext())
                {
                    _buffer.Add(_enumerator.Current);
                }
                else
                {
                    _exhausted = true;
                }
            }

            if (targetIndex < _buffer.Count)
            {
                token = _buffer[targetIndex];
                return true;
            }

            token = default;
            return false;
        }

        public void Advance()
        {
            _position++;
        }

        public void Dispose()
        {
            _enumerator.Dispose();
        }
    }

    /// <summary>
    /// Asynchronous token reader with lookahead support.
    /// </summary>
    private sealed class AsyncTokenReader : IAsyncDisposable
    {
        private readonly IAsyncEnumerator<SimpleToken> _enumerator;
        private readonly List<SimpleToken> _buffer = new();
        private readonly Stack<(char Opener, long Position)> _blockStack = new();
        private int _position;
        private bool _exhausted;

        public AsyncTokenReader(IAsyncEnumerable<SimpleToken> tokens, CancellationToken cancellationToken)
        {
            _enumerator = tokens.GetAsyncEnumerator(cancellationToken);
        }

        public SimpleToken Current => _buffer[_position];

        public async ValueTask<bool> TryPeekAsync(int offset = 0)
        {
            var targetIndex = _position + offset;

            while (_buffer.Count <= targetIndex && !_exhausted)
            {
                if (await _enumerator.MoveNextAsync())
                {
                    _buffer.Add(_enumerator.Current);
                }
                else
                {
                    _exhausted = true;
                }
            }

            return targetIndex < _buffer.Count;
        }

        public SimpleToken? PeekAhead(int offset)
        {
            var targetIndex = _position + offset;
            if (targetIndex < _buffer.Count)
                return _buffer[targetIndex];
            return null;
        }

        public ValueTask AdvanceAsync()
        {
            _position++;
            return ValueTask.CompletedTask;
        }

        public void PushBlock(char opener, long position)
        {
            _blockStack.Push((opener, position));
        }

        public IEnumerable<ErrorToken> FlushErrors()
        {
            while (_blockStack.Count > 0)
            {
                var (opener, pos) = _blockStack.Pop();
                yield return new ErrorToken(
                    ReadOnlyMemory<char>.Empty,
                    $"Unclosed block starting with '{opener}' at position {pos}",
                    pos);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync();
        }
    }

    #endregion
}
