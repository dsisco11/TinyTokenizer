using System.Collections.Immutable;
using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;

namespace TinyTokenizer;

/// <summary>
/// Parses simple tokens into semantic tokens.
/// Level 2 of the two-level tokenizer architecture.
/// Handles blocks, strings, comments, operators, tagged identifiers, and emits error tokens for failures.
/// </summary>
public sealed partial class TokenParser
{
    private readonly TokenizerOptions _options;
    private readonly OperatorTrie _operatorTrie;
    private readonly bool _hasCSingleLineComment;
    private readonly bool _hasCMultiLineComment;

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
        
        // Build operator trie for O(k) greedy matching
        _operatorTrie = new OperatorTrie();
        foreach (var op in options.Operators)
        {
            _operatorTrie.Add(op);
        }
        
        // Pre-compute comment style flags to avoid LINQ allocation on each TryParseComment call
        _hasCSingleLineComment = options.CommentStyles.Any(s => s.Start == "//");
        _hasCMultiLineComment = options.CommentStyles.Any(s => s.Start == "/*");
    }

    #region Public API

    /// <summary>
    /// Parses simple tokens into semantic tokens.
    /// </summary>
    public IEnumerable<Token> Parse(IEnumerable<SimpleToken> simpleTokens)
    {
        using var enumerator = simpleTokens.GetEnumerator();
        var reader = new SimpleTokenReader(enumerator);

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

    #region Token Parsing Logic

    private IEnumerable<Token> ParseToken(SimpleTokenReader reader, SimpleTokenType? expectedCloser)
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
            yield return new ErrorToken
            {
                Content = token.Content,
                ErrorMessage = $"Unexpected closing delimiter '{token.FirstChar}'",
                Position = token.Position
            };
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

            // Standalone dot - try operator matching, else emit as symbol
            var dotOpResult = TryParseOperator(reader);
            if (dotOpResult != null)
            {
                yield return dotOpResult;
                yield break;
            }
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
            yield break;
        }

        // Hash - could be tagged identifier or symbol/operator
        if (token.Type == SimpleTokenType.Hash)
        {
            // Check for tagged identifier: # followed by identifier
            if (_options.TagPrefixes.Contains('#'))
            {
                var taggedIdent = TryParseTaggedIdent(reader, '#');
                if (taggedIdent != null)
                {
                    yield return taggedIdent;
                    yield break;
                }
            }

            // Not a tagged identifier - try operator matching, else emit as symbol
            var hashOpResult = TryParseOperator(reader);
            if (hashOpResult != null)
            {
                yield return hashOpResult;
                yield break;
            }
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
            yield break;
        }

        // At - could be tagged identifier or symbol/operator
        if (token.Type == SimpleTokenType.At)
        {
            // Check for tagged identifier: @ followed by identifier
            if (_options.TagPrefixes.Contains('@'))
            {
                var taggedIdent = TryParseTaggedIdent(reader, '@');
                if (taggedIdent != null)
                {
                    yield return taggedIdent;
                    yield break;
                }
            }

            // Not a tagged identifier - try operator matching, else emit as symbol
            var atOpResult = TryParseOperator(reader);
            if (atOpResult != null)
            {
                yield return atOpResult;
                yield break;
            }
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
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

            // Not a comment - try operator matching, else emit as symbol
            var slashOpResult = TryParseOperator(reader);
            if (slashOpResult != null)
            {
                yield return slashOpResult;
                yield break;
            }
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
            yield break;
        }

        // Asterisk - try operator matching, else emit as symbol
        if (token.Type == SimpleTokenType.Asterisk)
        {
            var asteriskOpResult = TryParseOperator(reader);
            if (asteriskOpResult != null)
            {
                yield return asteriskOpResult;
                yield break;
            }
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
            yield break;
        }

        // Backslash outside of string - emit as symbol
        if (token.Type == SimpleTokenType.Backslash)
        {
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
            yield break;
        }

        // Check if this is an operator-capable token type (includes Symbol)
        if (IsOperatorCapableToken(token.Type))
        {
            // First check if it could be a tagged identifier
            // Get the character for this token type
            char? tokenChar = token.Type == SimpleTokenType.Symbol && token.Content.Length == 1
                ? token.Content.Span[0]
                : GetTokenChar(token.Type);
            
            if (tokenChar.HasValue && _options.TagPrefixes.Contains(tokenChar.Value))
            {
                var taggedIdent = TryParseTaggedIdent(reader, tokenChar.Value);
                if (taggedIdent != null)
                {
                    yield return taggedIdent;
                    yield break;
                }
            }

            var opResult = TryParseOperator(reader);
            if (opResult != null)
            {
                yield return opResult;
                yield break;
            }
            // Not an operator - emit as symbol
            reader.Advance();
            yield return new SymbolToken { Content = token.Content, Position = token.Position };
            yield break;
        }

        // Simple pass-through tokens
        reader.Advance();
        yield return token.Type switch
        {
            SimpleTokenType.Ident => new IdentToken { Content = token.Content, Position = token.Position },
            SimpleTokenType.Whitespace => new WhitespaceToken { Content = token.Content, Position = token.Position },
            SimpleTokenType.Newline => new WhitespaceToken { Content = token.Content, Position = token.Position },
            SimpleTokenType.Symbol => new SymbolToken { Content = token.Content, Position = token.Position },
            _ => new IdentToken { Content = token.Content, Position = token.Position }
        };
    }

    /// <summary>
    /// Checks if a SimpleTokenType represents a character that could be part of an operator.
    /// </summary>
    private static bool IsOperatorCapableToken(SimpleTokenType type)
    {
        return type is SimpleTokenType.Symbol
            or SimpleTokenType.Equals
            or SimpleTokenType.Plus
            or SimpleTokenType.Minus
            or SimpleTokenType.LessThan
            or SimpleTokenType.GreaterThan
            or SimpleTokenType.Pipe
            or SimpleTokenType.Ampersand
            or SimpleTokenType.Percent
            or SimpleTokenType.Caret
            or SimpleTokenType.Tilde
            or SimpleTokenType.Question
            or SimpleTokenType.Exclamation
            or SimpleTokenType.Colon
            or SimpleTokenType.At;
    }

    /// <summary>
    /// Gets the character representation of a SimpleTokenType for operator matching.
    /// </summary>
    private static char? GetTokenChar(SimpleTokenType type)
    {
        return type switch
        {
            SimpleTokenType.Equals => '=',
            SimpleTokenType.Plus => '+',
            SimpleTokenType.Minus => '-',
            SimpleTokenType.LessThan => '<',
            SimpleTokenType.GreaterThan => '>',
            SimpleTokenType.Pipe => '|',
            SimpleTokenType.Ampersand => '&',
            SimpleTokenType.Percent => '%',
            SimpleTokenType.Caret => '^',
            SimpleTokenType.Tilde => '~',
            SimpleTokenType.Question => '?',
            SimpleTokenType.Exclamation => '!',
            SimpleTokenType.Colon => ':',
            SimpleTokenType.Hash => '#',
            SimpleTokenType.At => '@',
            SimpleTokenType.Slash => '/',
            SimpleTokenType.Asterisk => '*',
            SimpleTokenType.Dot => '.',
            SimpleTokenType.Comma => ',',
            SimpleTokenType.Semicolon => ';',
            _ => null
        };
    }

    /// <summary>
    /// Tries to parse an operator starting from the current position.
    /// Uses trie-based greedy matching for O(k) lookup where k is operator length.
    /// </summary>
    private OperatorToken? TryParseOperator(SimpleTokenReader reader)
    {
        if (_operatorTrie.IsEmpty)
            return null;

        // Build a span of consecutive operator-capable characters for trie matching
        Span<char> chars = stackalloc char[16]; // Most operators are short
        var tokens = new List<SimpleToken>(8);
        int charCount = 0;

        while (reader.TryPeek(charCount, out var peekToken))
        {
            var tokenChar = GetTokenChar(peekToken.Type);
            if (tokenChar == null)
            {
                // For Symbol type, get the actual character
                if (peekToken.Type == SimpleTokenType.Symbol && peekToken.Content.Length > 0)
                {
                    tokenChar = peekToken.Content.Span[0];
                }
                else
                {
                    break;
                }
            }
            
            // Grow buffer if needed (rare case for very long operators)
            if (charCount >= chars.Length)
            {
                var newChars = new char[chars.Length * 2];
                chars.CopyTo(newChars);
                chars = newChars;
            }
            
            chars[charCount] = tokenChar.Value;
            tokens.Add(peekToken);
            charCount++;
        }

        if (charCount == 0)
            return null;

        // Use trie for O(k) greedy matching
        if (_operatorTrie.TryMatch(chars[..charCount], out var matchedOp) && matchedOp is not null)
        {
            // Found a match! Consume the tokens and build the operator
            var startPosition = tokens[0].Position;
            using var contentBuilder = new ArrayPoolBufferWriter<char>();

            for (int i = 0; i < matchedOp.Length; i++)
            {
                WriteToBuffer(contentBuilder, tokens[i].Content.Span);
                reader.Advance();
            }

            var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
            return new OperatorToken { Content = content, Position = startPosition };
        }

        return null;
    }

    /// <summary>
    /// Tries to parse a tagged identifier (tag + identifier, e.g., #define, @attribute).
    /// </summary>
    private TaggedIdentToken? TryParseTaggedIdent(SimpleTokenReader reader, char tagChar)
    {
        if (!reader.TryPeek(out var tagToken))
            return null;

        // Check for identifier immediately following the tag
        if (!reader.TryPeek(1, out var identToken) || identToken.Type != SimpleTokenType.Ident)
            return null;

        // This is a tagged identifier!
        var startPosition = tagToken.Position;
        using var contentBuilder = new ArrayPoolBufferWriter<char>();

        // Consume tag character
        WriteToBuffer(contentBuilder, tagToken.Content.Span);
        reader.Advance();

        // Consume identifier
        var identName = identToken.Content;
        WriteToBuffer(contentBuilder, identToken.Content.Span);
        reader.Advance();

        var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
        return new TaggedIdentToken { Content = content, Tag = tagChar, Name = identName, Position = startPosition };
    }

    private Token ParseBlock(SimpleTokenReader reader)
    {
        reader.TryPeek(out var openToken);
        reader.Advance();

        var closerType = GetMatchingCloser(openToken.Type);
        var blockType = GetBlockTokenType(openToken.Type);
        var startPosition = openToken.Position;

        var children = ImmutableArray.CreateBuilder<Token>();
        using var contentBuilder = new ArrayPoolBufferWriter<char>();
        
        // Add opener to full content
        WriteToBuffer(contentBuilder, openToken.Content.Span);

        var innerStart = contentBuilder.WrittenCount;

        // Parse children until closing delimiter or end
        while (reader.TryPeek(out var token))
        {
            if (token.Type == closerType)
            {
                // Found closer - extract inner content before adding closer
                var innerContent = contentBuilder.WrittenSpan[innerStart..].ToArray();
                
                // Add closer
                WriteToBuffer(contentBuilder, token.Content.Span);
                
                reader.Advance();

                var fullContent = contentBuilder.WrittenSpan.ToArray().AsMemory();
                var innerMemory = innerContent.AsMemory();

                return new SimpleBlock
                {
                    Content = fullContent,
                    InnerContent = innerMemory,
                    Children = children.ToImmutable(),
                    BlockType = blockType,
                    OpeningDelimiter = openToken,
                    ClosingDelimiter = token,
                    Position = startPosition
                };
            }

            // Parse child token
            foreach (var child in ParseToken(reader, closerType))
            {
                children.Add(child);
                WriteToBuffer(contentBuilder, child.Content.Span);
            }
        }

        // Unclosed block
        return new ErrorToken
        {
            Content = openToken.Content,
            ErrorMessage = $"Unclosed block starting with '{openToken.FirstChar}'",
            Position = startPosition
        };
    }

    private Token ParseString(SimpleTokenReader reader)
    {
        reader.TryPeek(out var quoteToken);
        reader.Advance();

        var quote = quoteToken.FirstChar;
        var startPosition = quoteToken.Position;
        using var contentBuilder = new ArrayPoolBufferWriter<char>();
        
        // Add opening quote
        WriteToBuffer(contentBuilder, quote);

        var quoteType = quoteToken.Type;
        bool escaped = false;

        while (reader.TryPeek(out var token))
        {
            if (escaped)
            {
                // Consume escaped character
                WriteToBuffer(contentBuilder, token.Content.Span);
                reader.Advance();
                escaped = false;
                continue;
            }

            if (token.Type == SimpleTokenType.Backslash)
            {
                WriteToBuffer(contentBuilder, '\\');
                reader.Advance();
                escaped = true;
                continue;
            }

            if (token.Type == quoteType)
            {
                // Closing quote
                WriteToBuffer(contentBuilder, quote);
                reader.Advance();

                var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
                return new StringToken { Content = content, Quote = quote, Position = startPosition };
            }

            // Regular content
            WriteToBuffer(contentBuilder, token.Content.Span);
            reader.Advance();
        }

        // Unterminated string - emit opening quote as symbol
        return new SymbolToken { Content = quoteToken.Content, Position = startPosition };
    }

    private Token? TryParseComment(SimpleTokenReader reader, SimpleToken slashToken, SimpleToken nextToken)
    {
        // Single-line comment: //
        if (_hasCSingleLineComment && nextToken.Type == SimpleTokenType.Slash)
        {
            return ParseSingleLineComment(reader);
        }

        // Multi-line comment: /*
        if (_hasCMultiLineComment && nextToken.Type == SimpleTokenType.Asterisk)
        {
            return ParseMultiLineComment(reader);
        }

        return null;
    }

    private CommentToken ParseSingleLineComment(SimpleTokenReader reader)
    {
        using var contentBuilder = new ArrayPoolBufferWriter<char>();
        int startPosition = 0;

        // Consume first slash
        if (reader.TryPeek(out var slash1))
        {
            startPosition = slash1.Position;
            WriteToBuffer(contentBuilder, '/');
            reader.Advance();
        }

        // Consume second slash
        if (reader.TryPeek(out _))
        {
            WriteToBuffer(contentBuilder, '/');
            reader.Advance();
        }

        // Consume until newline
        while (reader.TryPeek(out var token))
        {
            if (token.Type == SimpleTokenType.Newline)
                break;

            WriteToBuffer(contentBuilder, token.Content.Span);
            reader.Advance();
        }

        var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
        return new CommentToken { Content = content, IsMultiLine = false, Position = startPosition };
    }

    private Token ParseMultiLineComment(SimpleTokenReader reader)
    {
        using var contentBuilder = new ArrayPoolBufferWriter<char>();
        int startPosition = 0;

        // Consume /
        if (reader.TryPeek(out var slash))
        {
            startPosition = slash.Position;
            WriteToBuffer(contentBuilder, '/');
            reader.Advance();
        }

        // Consume *
        if (reader.TryPeek(out _))
        {
            WriteToBuffer(contentBuilder, '*');
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
                    WriteToBuffer(contentBuilder, '*');
                    WriteToBuffer(contentBuilder, '/');
                    reader.Advance(); // *
                    reader.Advance(); // /

                    var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
                    return new CommentToken { Content = content, IsMultiLine = true, Position = startPosition };
                }
            }

            WriteToBuffer(contentBuilder, token.Content.Span);
            reader.Advance();
        }

        // Unterminated comment
        var errorContent = contentBuilder.WrittenSpan.ToArray().AsMemory();
        return new ErrorToken
        {
            Content = errorContent,
            ErrorMessage = "Unterminated multi-line comment",
            Position = startPosition
        };
    }

    /// <summary>
    /// Parses a numeric token starting from a Digits token.
    /// Handles patterns: 123, 123.456
    /// </summary>
    private NumericToken ParseNumericFromDigits(SimpleTokenReader reader)
    {
        reader.TryPeek(out var digitsToken);
        var startPosition = digitsToken.Position;
        using var contentBuilder = new ArrayPoolBufferWriter<char>();
        
        // Add the initial digits
        WriteToBuffer(contentBuilder, digitsToken.Content.Span);
        reader.Advance();

        bool hasDecimal = false;

        // Check for decimal point followed by more digits
        if (reader.TryPeek(out var dotToken) && dotToken.Type == SimpleTokenType.Dot)
        {
            if (reader.TryPeek(1, out var afterDot) && afterDot.Type == SimpleTokenType.Digits)
            {
                // It's a decimal number: add dot and digits
                WriteToBuffer(contentBuilder, '.');
                reader.Advance(); // consume dot

                WriteToBuffer(contentBuilder, afterDot.Content.Span);
                reader.Advance(); // consume digits after dot

                hasDecimal = true;
            }
        }

        var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
        var numericType = hasDecimal ? NumericType.FloatingPoint : NumericType.Integer;
        return new NumericToken { Content = content, NumericType = numericType, Position = startPosition };
    }

    /// <summary>
    /// Parses a numeric token starting from a Dot token.
    /// Handles pattern: .456
    /// </summary>
    private NumericToken ParseNumericFromDot(SimpleTokenReader reader)
    {
        reader.TryPeek(out var dotToken);
        var startPosition = dotToken.Position;
        using var contentBuilder = new ArrayPoolBufferWriter<char>();

        // Add the leading dot
        WriteToBuffer(contentBuilder, '.');
        reader.Advance();

        // Add the digits after the dot
        if (reader.TryPeek(out var digitsToken) && digitsToken.Type == SimpleTokenType.Digits)
        {
            WriteToBuffer(contentBuilder, digitsToken.Content.Span);
            reader.Advance();
        }

        var content = contentBuilder.WrittenSpan.ToArray().AsMemory();
        return new NumericToken { Content = content, NumericType = NumericType.FloatingPoint, Position = startPosition };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Writes a span to the buffer writer.
    /// </summary>
    private static void WriteToBuffer(ArrayPoolBufferWriter<char> buffer, ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return;
        var destination = buffer.GetSpan(span.Length);
        span.CopyTo(destination);
        buffer.Advance(span.Length);
    }

    /// <summary>
    /// Writes a single character to the buffer writer.
    /// </summary>
    private static void WriteToBuffer(ArrayPoolBufferWriter<char> buffer, char c)
    {
        var destination = buffer.GetSpan(1);
        destination[0] = c;
        buffer.Advance(1);
    }

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

    #endregion
}
