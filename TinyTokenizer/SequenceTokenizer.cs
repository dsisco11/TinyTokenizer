using System.Buffers;
using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// A high-performance tokenizer that parses <see cref="ReadOnlySequence{T}"/> into tokens
/// using <see cref="SequenceReader{T}"/> for efficient multi-segment buffer handling.
/// This is a ref struct optimized for streaming scenarios.
/// </summary>
public ref struct SequenceTokenizer
{
    #region Fields

    private SequenceReader<char> _reader;
    private readonly TokenizerOptions _options;
    private readonly TokenizerState _state;
    private readonly bool _isCompleted;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="SequenceTokenizer"/>.
    /// </summary>
    /// <param name="sequence">The character sequence to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="state">Optional state for resumable parsing across buffer boundaries.</param>
    /// <param name="isCompleted">Whether this is the final buffer (no more data coming).</param>
    public SequenceTokenizer(
        ReadOnlySequence<char> sequence,
        TokenizerOptions? options = null,
        TokenizerState? state = null,
        bool isCompleted = true)
    {
        _reader = new SequenceReader<char>(sequence);
        _options = options ?? TokenizerOptions.Default;
        _state = state ?? new TokenizerState();
        _isCompleted = isCompleted;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tokenizes the sequence and returns the parse result.
    /// </summary>
    /// <returns>A <see cref="ParseResult"/> containing tokens and status.</returns>
    public ParseResult Tokenize()
    {
        var tokens = ImmutableArray.CreateBuilder<Token>();
        string? errorMessage = null;

        try
        {
            TokenizeInternal(tokens, null);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        var consumed = _reader.Position;
        var examined = _reader.Sequence.End;

        // Determine status
        ParseStatus status;
        if (errorMessage != null)
        {
            status = ParseStatus.Error;
        }
        else if (_reader.End && _isCompleted)
        {
            status = ParseStatus.Complete;
        }
        else if (!_reader.End)
        {
            status = ParseStatus.Continue;
        }
        else
        {
            // At end but not completed - need more data
            status = _state.Mode == ParseMode.Normal ? ParseStatus.Complete : ParseStatus.NeedMoreData;
        }

        return new ParseResult
        {
            Tokens = tokens.ToImmutable(),
            Status = status,
            Consumed = consumed,
            Examined = examined,
            ErrorMessage = errorMessage
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Internal tokenization method that handles recursive block parsing.
    /// </summary>
    private void TokenizeInternal(ImmutableArray<Token>.Builder tokens, char? expectedCloser)
    {
        while (_reader.TryPeek(out char current))
        {
            long tokenStart = _state.AbsolutePosition + _reader.Consumed;

            // Check for expected closing delimiter (when inside a block)
            if (expectedCloser.HasValue && current == expectedCloser.Value)
            {
                break;
            }

            // Check for unexpected closing delimiter
            if (TokenizerCore.IsClosingDelimiter(current))
            {
                var content = ExtractContent(1);
                tokens.Add(new ErrorToken(content, $"Unexpected closing delimiter '{current}'", tokenStart));
                _reader.Advance(1);
                continue;
            }

            // Check for opening delimiter (start of block)
            if (TokenizerCore.TryGetClosingDelimiter(current, out char closer))
            {
                var blockToken = ParseBlock(current, closer, tokenStart);
                tokens.Add(blockToken);
                continue;
            }

            // Check for comment
            var commentStyle = TryMatchCommentStart();
            if (commentStyle is not null)
            {
                var token = ParseComment(commentStyle, tokenStart);
                if (token != null)
                {
                    tokens.Add(token);
                }
                else if (!_isCompleted)
                {
                    // Need more data for comment
                    _state.BeginPartialToken(ParseMode.InMultiLineComment, tokenStart);
                    _state.CurrentCommentStyle = commentStyle;
                    return;
                }
                continue;
            }

            // Check for string literal
            if (current == '"' || current == '\'')
            {
                var token = ParseString(current, tokenStart);
                if (token != null)
                {
                    tokens.Add(token);
                }
                else if (!_isCompleted)
                {
                    // Need more data for string
                    _state.BeginPartialToken(ParseMode.InString, tokenStart);
                    _state.StringQuote = current;
                    return;
                }
                continue;
            }

            // Check for numeric literal
            if (char.IsDigit(current) || (current == '.' && TryPeekDigitAfter()))
            {
                var token = ParseNumeric(tokenStart);
                tokens.Add(token);
                continue;
            }

            // Check for symbol
            if (_options.Symbols.Contains(current))
            {
                var content = ExtractContent(1);
                tokens.Add(new SymbolToken(content, tokenStart));
                _reader.Advance(1);
                continue;
            }

            // Check for whitespace
            if (char.IsWhiteSpace(current))
            {
                var token = ParseWhitespace(tokenStart);
                tokens.Add(token);
                continue;
            }

            // Otherwise, it's text
            var textToken = ParseText(tokenStart);
            tokens.Add(textToken);
        }
    }

    private bool TryPeekDigitAfter()
    {
        return _reader.TryPeek(1, out char next) && char.IsDigit(next);
    }

    /// <summary>
    /// Parses a block starting at the current position.
    /// </summary>
    private Token ParseBlock(char opener, char closer, long startPosition)
    {
        TokenizerCore.TryGetBlockTokenType(opener, out TokenType blockType);

        long readerStartPos = _reader.Consumed;
        
        // Consume the opening delimiter
        _reader.Advance(1);
        long innerStart = _reader.Consumed;

        // Recursively tokenize the inner content
        var children = ImmutableArray.CreateBuilder<Token>();
        TokenizeInternal(children, closer);

        long innerEnd = _reader.Consumed;

        // Check if we found the closing delimiter
        if (_reader.TryPeek(out char c) && c == closer)
        {
            // Consume the closing delimiter
            _reader.Advance(1);

            long totalLength = _reader.Consumed - readerStartPos;
            var fullContent = ExtractContentFromPosition(readerStartPos, (int)totalLength);
            var innerContent = ExtractContentFromPosition(innerStart, (int)(innerEnd - innerStart));

            return new BlockToken(
                fullContent,
                innerContent,
                children.ToImmutable(),
                blockType,
                opener,
                closer,
                startPosition);
        }
        else
        {
            // Unclosed block - emit error token
            var errorContent = ExtractContentFromPosition(readerStartPos, 1);
            return new ErrorToken(
                errorContent,
                $"Unclosed block starting with '{opener}'",
                startPosition);
        }
    }

    /// <summary>
    /// Parses whitespace starting at the current position.
    /// </summary>
    private WhitespaceToken ParseWhitespace(long startPosition)
    {
        long start = _reader.Consumed;

        while (_reader.TryPeek(out char c) && char.IsWhiteSpace(c))
        {
            _reader.Advance(1);
        }

        var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
        return new WhitespaceToken(content, startPosition);
    }

    /// <summary>
    /// Parses text starting at the current position.
    /// </summary>
    private TextToken ParseText(long startPosition)
    {
        long start = _reader.Consumed;

        while (_reader.TryPeek(out char current))
        {
            // Stop at delimiters
            if (TokenizerCore.IsOpeningDelimiter(current) || TokenizerCore.IsClosingDelimiter(current))
                break;

            // Stop at symbols
            if (_options.Symbols.Contains(current))
                break;

            // Stop at whitespace
            if (char.IsWhiteSpace(current))
                break;

            // Stop at quotes
            if (current == '"' || current == '\'')
                break;

            _reader.Advance(1);
        }

        var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
        return new TextToken(content, startPosition);
    }

    /// <summary>
    /// Parses a string literal starting at the current position.
    /// </summary>
    private Token? ParseString(char quote, long startPosition)
    {
        long start = _reader.Consumed;

        // Consume the opening quote
        _reader.Advance(1);

        while (_reader.TryPeek(out char current))
        {
            // Check for escape sequence
            if (current == '\\' && _reader.TryPeek(1, out _))
            {
                _reader.Advance(2);
                continue;
            }

            // Check for closing quote
            if (current == quote)
            {
                _reader.Advance(1);
                var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
                return new StringToken(content, quote, startPosition);
            }

            _reader.Advance(1);
        }

        // Unterminated string
        if (_isCompleted)
        {
            // Reset and emit as symbol
            _reader.Rewind(_reader.Consumed - start - 1);
            var content = ExtractContentFromPosition(start, 1);
            return new SymbolToken(content, startPosition);
        }

        // Need more data
        _reader.Rewind(_reader.Consumed - start);
        return null;
    }

    /// <summary>
    /// Parses a numeric literal starting at the current position.
    /// </summary>
    private NumericToken ParseNumeric(long startPosition)
    {
        long start = _reader.Consumed;
        bool hasDecimalPoint = false;

        while (_reader.TryPeek(out char current))
        {
            if (char.IsDigit(current))
            {
                _reader.Advance(1);
                continue;
            }

            // Allow one decimal point
            if (current == '.' && !hasDecimalPoint)
            {
                if (_reader.TryPeek(1, out char next) && char.IsDigit(next))
                {
                    hasDecimalPoint = true;
                    _reader.Advance(1);
                    continue;
                }
            }

            break;
        }

        var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
        var numericType = hasDecimalPoint ? NumericType.FloatingPoint : NumericType.Integer;
        return new NumericToken(content, numericType, startPosition);
    }

    /// <summary>
    /// Tries to match a comment start delimiter at the current position.
    /// </summary>
    private CommentStyle? TryMatchCommentStart()
    {
        foreach (var style in _options.CommentStyles)
        {
            if (IsNext(style.Start))
            {
                return style;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the given string matches at the current position.
    /// </summary>
    private bool IsNext(string value)
    {
        var span = value.AsSpan();
        
        for (int i = 0; i < span.Length; i++)
        {
            if (!_reader.TryPeek(i, out char c) || c != span[i])
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Parses a comment starting at the current position.
    /// </summary>
    private CommentToken? ParseComment(CommentStyle style, long startPosition)
    {
        long start = _reader.Consumed;

        // Consume the start delimiter
        _reader.Advance(style.Start.Length);

        if (style.IsMultiLine)
        {
            // Look for end delimiter
            var endSpan = style.End!.AsSpan();
            
            while (!_reader.End)
            {
                // Find the first character of the end delimiter
                if (_reader.TryPeek(out char c) && c == endSpan[0])
                {
                    if (IsNext(style.End!))
                    {
                        _reader.Advance(style.End!.Length);
                        var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
                        return new CommentToken(content, IsMultiLine: true, startPosition);
                    }
                }
                _reader.Advance(1);
            }

            // End delimiter not found
            if (_isCompleted)
            {
                // Consume everything as comment
                var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
                return new CommentToken(content, IsMultiLine: true, startPosition);
            }

            // Need more data
            _reader.Rewind(_reader.Consumed - start);
            return null;
        }
        else
        {
            // Single-line comment: consume until end of line
            while (_reader.TryPeek(out char c))
            {
                if (c == '\n' || c == '\r')
                    break;
                _reader.Advance(1);
            }

            var content = ExtractContentFromPosition(start, (int)(_reader.Consumed - start));
            return new CommentToken(content, IsMultiLine: false, startPosition);
        }
    }

    #endregion

    #region Content Extraction

    /// <summary>
    /// Extracts content of the specified length starting from current position.
    /// </summary>
    private ReadOnlyMemory<char> ExtractContent(int length)
    {
        return ExtractContentFromPosition(_reader.Consumed, length);
    }

    /// <summary>
    /// Extracts content from a specific position in the sequence.
    /// </summary>
    private ReadOnlyMemory<char> ExtractContentFromPosition(long position, int length)
    {
        if (length == 0)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        var slice = _reader.Sequence.Slice(position, length);
        
        // If single segment, return directly
        if (slice.IsSingleSegment)
        {
            return slice.First;
        }

        // Multi-segment: copy to array
        char[] buffer;
        if (length > TokenizerCore.LargeTokenThreshold)
        {
            buffer = new char[length];
        }
        else
        {
            buffer = ArrayPool<char>.Shared.Rent(length);
        }

        slice.CopyTo(buffer);

        // For pooled arrays, we need to create a memory that owns just the right portion
        if (length <= TokenizerCore.LargeTokenThreshold)
        {
            // Create a copy since we can't return pooled array to Memory<char> safely
            var exactBuffer = new char[length];
            Array.Copy(buffer, exactBuffer, length);
            ArrayPool<char>.Shared.Return(buffer);
            return new ReadOnlyMemory<char>(exactBuffer);
        }

        return new ReadOnlyMemory<char>(buffer, 0, length);
    }

    #endregion
}
