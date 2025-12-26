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
    private long _lastSafePosition; // Position up to which we can safely consume

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
        _lastSafePosition = 0;
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
        bool needMoreData = false;

        try
        {
            // Resume from partial token if any
            if (_state.Mode != ParseMode.Normal)
            {
                needMoreData = !ResumePartialToken(tokens);
                if (needMoreData && !_isCompleted)
                {
                    return CreateNeedMoreDataResult(tokens);
                }
            }

            if (!needMoreData)
            {
                TokenizeInternal(tokens, null, ref needMoreData);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        // Determine consumed position
        var consumed = needMoreData && !_isCompleted
            ? _reader.Sequence.GetPosition(_lastSafePosition)
            : _reader.Position;
        var examined = _reader.Sequence.End;

        // Determine status
        ParseStatus status;
        if (errorMessage != null)
        {
            status = ParseStatus.Error;
        }
        else if (needMoreData && !_isCompleted)
        {
            status = ParseStatus.NeedMoreData;
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
            status = ParseStatus.Complete;
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

    private ParseResult CreateNeedMoreDataResult(ImmutableArray<Token>.Builder tokens)
    {
        return new ParseResult
        {
            Tokens = tokens.ToImmutable(),
            Status = ParseStatus.NeedMoreData,
            Consumed = _reader.Sequence.GetPosition(_lastSafePosition),
            Examined = _reader.Sequence.End,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Resumes parsing a partial token from previous buffer.
    /// </summary>
    /// <returns>True if token was completed, false if still need more data.</returns>
    private bool ResumePartialToken(ImmutableArray<Token>.Builder tokens)
    {
        switch (_state.Mode)
        {
            case ParseMode.InString:
                return ResumeString(tokens);
            case ParseMode.InMultiLineComment:
                return ResumeMultiLineComment(tokens);
            case ParseMode.InSingleLineComment:
                return ResumeSingleLineComment(tokens);
            case ParseMode.InBlock:
                // Blocks are handled differently - they accumulate children
                return true;
            default:
                _state.ClearPartialToken();
                return true;
        }
    }

    private bool ResumeString(ImmutableArray<Token>.Builder tokens)
    {
        char quote = _state.StringQuote ?? '"';
        bool escaped = _state.IsEscaped;

        while (_reader.TryPeek(out char current))
        {
            if (escaped)
            {
                _state.PartialBuffer.Append(current);
                _reader.Advance(1);
                escaped = false;
                _state.IsEscaped = false;
                continue;
            }

            if (current == '\\')
            {
                _state.PartialBuffer.Append(current);
                _reader.Advance(1);
                escaped = true;
                _state.IsEscaped = true;
                continue;
            }

            if (current == quote)
            {
                _state.PartialBuffer.Append(current);
                _reader.Advance(1);
                
                // Complete the string token
                var content = _state.PartialBuffer.ToString().AsMemory();
                tokens.Add(new StringToken(content, quote, _state.TokenStartPosition));
                _state.ClearPartialToken();
                _lastSafePosition = _reader.Consumed;
                return true;
            }

            _state.PartialBuffer.Append(current);
            _reader.Advance(1);
        }

        // Still need more data
        _state.IsEscaped = escaped;
        return false;
    }

    private bool ResumeMultiLineComment(ImmutableArray<Token>.Builder tokens)
    {
        var style = _state.CurrentCommentStyle;
        if (style?.End == null) return true;

        var endSpan = style.End.AsSpan();

        while (_reader.TryPeek(out char current))
        {
            _state.PartialBuffer.Append(current);
            _reader.Advance(1);

            // Check if buffer ends with the end delimiter
            if (_state.PartialBuffer.Length >= style.End.Length)
            {
                bool matches = true;
                int bufferLen = _state.PartialBuffer.Length;
                for (int i = 0; i < style.End.Length; i++)
                {
                    if (_state.PartialBuffer[bufferLen - style.End.Length + i] != endSpan[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    var content = _state.PartialBuffer.ToString().AsMemory();
                    tokens.Add(new CommentToken(content, IsMultiLine: true, _state.TokenStartPosition));
                    _state.ClearPartialToken();
                    _lastSafePosition = _reader.Consumed;
                    return true;
                }
            }
        }

        // Still need more data
        return false;
    }

    private bool ResumeSingleLineComment(ImmutableArray<Token>.Builder tokens)
    {
        while (_reader.TryPeek(out char current))
        {
            if (current == '\n' || current == '\r')
            {
                // End of single-line comment (don't consume newline)
                var content = _state.PartialBuffer.ToString().AsMemory();
                tokens.Add(new CommentToken(content, IsMultiLine: false, _state.TokenStartPosition));
                _state.ClearPartialToken();
                _lastSafePosition = _reader.Consumed;
                return true;
            }

            _state.PartialBuffer.Append(current);
            _reader.Advance(1);
        }

        // At end of buffer
        if (_isCompleted)
        {
            // EOF counts as end of single-line comment
            var content = _state.PartialBuffer.ToString().AsMemory();
            tokens.Add(new CommentToken(content, IsMultiLine: false, _state.TokenStartPosition));
            _state.ClearPartialToken();
            _lastSafePosition = _reader.Consumed;
            return true;
        }

        return false;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Internal tokenization method that handles recursive block parsing.
    /// </summary>
    private void TokenizeInternal(ImmutableArray<Token>.Builder tokens, char? expectedCloser, ref bool needMoreData)
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
                _lastSafePosition = _reader.Consumed;
                continue;
            }

            // Check for opening delimiter (start of block)
            if (TokenizerCore.TryGetClosingDelimiter(current, out char closer))
            {
                var blockToken = ParseBlock(current, closer, tokenStart, ref needMoreData);
                if (blockToken != null)
                {
                    tokens.Add(blockToken);
                    _lastSafePosition = _reader.Consumed;
                }
                else if (needMoreData)
                {
                    return;
                }
                continue;
            }

            // Check for comment
            var commentStyle = TryMatchCommentStart();
            if (commentStyle is not null)
            {
                var token = ParseComment(commentStyle, tokenStart, ref needMoreData);
                if (token != null)
                {
                    tokens.Add(token);
                    _lastSafePosition = _reader.Consumed;
                }
                else if (needMoreData)
                {
                    return;
                }
                continue;
            }

            // Check for string literal
            if (current == '"' || current == '\'')
            {
                var token = ParseString(current, tokenStart, ref needMoreData);
                if (token != null)
                {
                    tokens.Add(token);
                    _lastSafePosition = _reader.Consumed;
                }
                else if (needMoreData)
                {
                    return;
                }
                continue;
            }

            // Check for numeric literal
            if (char.IsDigit(current) || (current == '.' && TryPeekDigitAfter()))
            {
                var token = ParseNumeric(tokenStart);
                tokens.Add(token);
                _lastSafePosition = _reader.Consumed;
                continue;
            }

            // Check for symbol
            if (_options.Symbols.Contains(current))
            {
                var content = ExtractContent(1);
                tokens.Add(new SymbolToken(content, tokenStart));
                _reader.Advance(1);
                _lastSafePosition = _reader.Consumed;
                continue;
            }

            // Check for whitespace
            if (char.IsWhiteSpace(current))
            {
                var token = ParseWhitespace(tokenStart);
                tokens.Add(token);
                _lastSafePosition = _reader.Consumed;
                continue;
            }

            // Otherwise, it's text
            var textToken = ParseText(tokenStart);
            tokens.Add(textToken);
            _lastSafePosition = _reader.Consumed;
        }
    }

    private bool TryPeekDigitAfter()
    {
        return _reader.TryPeek(1, out char next) && char.IsDigit(next);
    }

    /// <summary>
    /// Parses a block starting at the current position.
    /// </summary>
    private Token? ParseBlock(char opener, char closer, long startPosition, ref bool needMoreData)
    {
        TokenizerCore.TryGetBlockTokenType(opener, out TokenType blockType);

        long readerStartPos = _reader.Consumed;
        
        // Consume the opening delimiter
        _reader.Advance(1);
        long innerStart = _reader.Consumed;

        // Recursively tokenize the inner content
        var children = ImmutableArray.CreateBuilder<Token>();
        TokenizeInternal(children, closer, ref needMoreData);

        if (needMoreData)
        {
            // Rewind to before the block
            _reader.Rewind(_reader.Consumed - readerStartPos);
            return null;
        }

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
        else if (!_isCompleted)
        {
            // Need more data - rewind and signal
            _reader.Rewind(_reader.Consumed - readerStartPos);
            needMoreData = true;
            return null;
        }
        else
        {
            // Unclosed block at EOF - emit error token
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
    private Token? ParseString(char quote, long startPosition, ref bool needMoreData)
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

        // Need more data - save state for resumption
        _state.BeginPartialToken(ParseMode.InString, startPosition);
        _state.StringQuote = quote;
        
        // Copy what we have so far to the partial buffer
        var partialSlice = _reader.Sequence.Slice(start, _reader.Consumed - start);
        foreach (var segment in partialSlice)
        {
            _state.PartialBuffer.Append(segment.Span);
        }
        
        _reader.Rewind(_reader.Consumed - start);
        needMoreData = true;
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
    private CommentToken? ParseComment(CommentStyle style, long startPosition, ref bool needMoreData)
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

            // Need more data - save state for resumption
            _state.BeginPartialToken(ParseMode.InMultiLineComment, startPosition);
            _state.CurrentCommentStyle = style;
            
            // Copy what we have so far to the partial buffer
            var partialSlice = _reader.Sequence.Slice(start, _reader.Consumed - start);
            foreach (var segment in partialSlice)
            {
                _state.PartialBuffer.Append(segment.Span);
            }
            
            _reader.Rewind(_reader.Consumed - start);
            needMoreData = true;
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

            // Check if we hit end of buffer without newline (and not completed)
            if (_reader.End && !_isCompleted)
            {
                // Need more data - save state for resumption
                _state.BeginPartialToken(ParseMode.InSingleLineComment, startPosition);
                _state.CurrentCommentStyle = style;
                
                // Copy what we have so far to the partial buffer
                var partialSlice = _reader.Sequence.Slice(start, _reader.Consumed - start);
                foreach (var segment in partialSlice)
                {
                    _state.PartialBuffer.Append(segment.Span);
                }
                
                _reader.Rewind(_reader.Consumed - start);
                needMoreData = true;
                return null;
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
