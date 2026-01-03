using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// A high-performance tokenizer that parses text into a series of tokens using <see cref="ReadOnlySpan{T}"/>.
/// This is a ref struct to allow holding spans internally during parsing.
/// </summary>
public ref struct Tokenizer
{
    #region Fields

    private readonly ReadOnlyMemory<char> _source;
    private readonly ReadOnlySpan<char> _span;
    private readonly TokenizerOptions _options;
    private readonly OperatorTrie _operatorTrie;
    private int _position;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="Tokenizer"/> with the specified source and options.
    /// </summary>
    /// <param name="source">The source text to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    public Tokenizer(ReadOnlyMemory<char> source, TokenizerOptions? options = null)
    {
        _source = source;
        _span = source.Span;
        _options = options ?? TokenizerOptions.Default;
        _position = 0;
        // Build operator trie for O(k) greedy matching
        _operatorTrie = new OperatorTrie();
        foreach (var op in _options.Operators)
        {
            _operatorTrie.Add(op);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tokenizes the source text and returns an immutable array of tokens.
    /// </summary>
    /// <returns>An immutable array of tokens parsed from the source.</returns>
    public ImmutableArray<Token> Tokenize()
    {
        return TokenizeInternal(null);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Internal tokenization method that handles recursive block parsing.
    /// </summary>
    /// <param name="expectedCloser">The expected closing delimiter when parsing inside a block, or null for top-level.</param>
    /// <returns>An immutable array of tokens.</returns>
    private ImmutableArray<Token> TokenizeInternal(char? expectedCloser)
    {
        var tokens = ImmutableArray.CreateBuilder<Token>();

        while (_position < _span.Length)
        {
            char current = _span[_position];

            // Check for expected closing delimiter (when inside a block)
            if (expectedCloser.HasValue && current == expectedCloser.Value)
            {
                // Don't consume the closer; let the caller handle it
                break;
            }

            // Check for unexpected closing delimiter
            if (TokenizerCore.IsClosingDelimiter(current))
            {
                tokens.Add(new ErrorToken
                {
                    Content = _source.Slice(_position, 1),
                    ErrorMessage = $"Unexpected closing delimiter '{current}'",
                    Position = _position
                });
                _position++;
                continue;
            }

            // Check for opening delimiter (start of block)
            if (TokenizerCore.TryGetClosingDelimiter(current, out char closer))
            {
                var blockToken = ParseBlock(current, closer);
                tokens.Add(blockToken);
                continue;
            }

            // Check for comment
            var commentStyle = TryMatchCommentStart();
            if (commentStyle is not null)
            {
                tokens.Add(ParseComment(commentStyle));
                continue;
            }

            // Check for string literal (single or double quotes)
            if (current == '"' || current == '\'')
            {
                tokens.Add(ParseString(current));
                continue;
            }

            // Check for numeric literal
            if (char.IsDigit(current) || (current == '.' && _position + 1 < _span.Length && char.IsDigit(_span[_position + 1])))
            {
                tokens.Add(ParseNumeric());
                continue;
            }

            // Check for operator (multi-character sequence like ==, !=, &&, ||)
            var operatorToken = TryParseOperator();
            if (operatorToken != null)
            {
                tokens.Add(operatorToken);
                continue;
            }

            // Check for symbol
            if (_options.Symbols.Contains(current))
            {
                tokens.Add(new SymbolToken { Content = _source.Slice(_position, 1), Position = _position });
                _position++;
                continue;
            }

            // Check for whitespace
            if (char.IsWhiteSpace(current))
            {
                tokens.Add(ParseWhitespace());
                continue;
            }

            // Otherwise, it's text
            tokens.Add(ParseText());
        }

        return tokens.ToImmutable();
    }

    /// <summary>
    /// Parses a block starting at the current position.
    /// </summary>
    /// <param name="opener">The opening delimiter character.</param>
    /// <param name="closer">The expected closing delimiter character.</param>
    /// <returns>A <see cref="SimpleBlock"/> or <see cref="ErrorToken"/> if the block is malformed.</returns>
    private Token ParseBlock(char opener, char closer)
    {
        int startPosition = _position;
        TokenizerCore.TryGetBlockTokenType(opener, out TokenType blockType);
        
        // Create opening delimiter SimpleToken
        var openingDelimiter = new SimpleToken(
            TokenizerCore.GetOpeningDelimiterType(opener),
            _source.Slice(startPosition, 1),
            startPosition);

        // Consume the opening delimiter
        _position++;
        int innerStart = _position;

        // Recursively tokenize the inner content
        var children = TokenizeInternal(closer);

        int innerEnd = _position;

        // Check if we found the closing delimiter
        if (_position < _span.Length && _span[_position] == closer)
        {
            // Create closing delimiter SimpleToken
            var closingDelimiter = new SimpleToken(
                TokenizerCore.GetClosingDelimiterType(closer),
                _source.Slice(_position, 1),
                _position);
            
            // Consume the closing delimiter
            _position++;

            var fullContent = _source.Slice(startPosition, _position - startPosition);
            var innerContent = _source.Slice(innerStart, innerEnd - innerStart);

            return new SimpleBlock
            {
                Content = fullContent,
                InnerContent = innerContent,
                Children = children,
                BlockType = blockType,
                OpeningDelimiter = openingDelimiter,
                ClosingDelimiter = closingDelimiter,
                Position = startPosition
            };
        }
        else
        {
            // Unclosed block - emit error token for the opening delimiter
            // The children are lost, but we've already advanced past them
            return new ErrorToken
            {
                Content = _source.Slice(startPosition, 1),
                ErrorMessage = $"Unclosed block starting with '{opener}'",
                Position = startPosition
            };
        }
    }

    /// <summary>
    /// Parses whitespace starting at the current position.
    /// </summary>
    /// <returns>A <see cref="WhitespaceToken"/>.</returns>
    private WhitespaceToken ParseWhitespace()
    {
        int start = _position;

        while (_position < _span.Length && char.IsWhiteSpace(_span[_position]))
        {
            _position++;
        }

        return new WhitespaceToken { Content = _source.Slice(start, _position - start), Position = start };
    }

    /// <summary>
    /// Parses text starting at the current position.
    /// Text ends when a delimiter, symbol, or whitespace is encountered.
    /// </summary>
    /// <returns>A <see cref="IdentToken"/>.</returns>
    private IdentToken ParseText()
    {
        int start = _position;

        while (_position < _span.Length)
        {
            char current = _span[_position];

            // Stop at delimiters
            if (TokenizerCore.IsOpeningDelimiter(current) || TokenizerCore.IsClosingDelimiter(current))
            {
                break;
            }

            // Stop at symbols
            if (_options.Symbols.Contains(current))
            {
                break;
            }

            // Stop at whitespace
            if (char.IsWhiteSpace(current))
            {
                break;
            }

            // Stop at quotes (string literals)
            if (current == '"' || current == '\'')
            {
                break;
            }

            _position++;
        }

        return new IdentToken { Content = _source.Slice(start, _position - start), Position = start };
    }

    /// <summary>
    /// Parses a string literal starting at the current position.
    /// </summary>
    /// <param name="quote">The quote character (' or ").</param>
    /// <returns>A <see cref="StringToken"/> or <see cref="SymbolToken"/> if the string is unterminated.</returns>
    private Token ParseString(char quote)
    {
        int start = _position;

        // Consume the opening quote
        _position++;

        while (_position < _span.Length)
        {
            char current = _span[_position];

            // Check for escape sequence
            if (current == '\\' && _position + 1 < _span.Length)
            {
                // Skip the escape character and the next character
                _position += 2;
                continue;
            }

            // Check for closing quote
            if (current == quote)
            {
                _position++;
                return new StringToken { Content = _source.Slice(start, _position - start), Quote = quote, Position = start };
            }

            _position++;
        }

        // Unterminated string - reset position and emit the quote as a symbol
        _position = start + 1;
        return new SymbolToken { Content = _source.Slice(start, 1), Position = start };
    }

    /// <summary>
    /// Parses a numeric literal starting at the current position.
    /// Supports integers and floating-point numbers.
    /// </summary>
    /// <returns>A <see cref="NumericToken"/>.</returns>
    private NumericToken ParseNumeric()
    {
        int start = _position;
        bool hasDecimalPoint = false;

        while (_position < _span.Length)
        {
            char current = _span[_position];

            if (char.IsDigit(current))
            {
                _position++;
                continue;
            }

            // Allow one decimal point
            if (current == '.' && !hasDecimalPoint)
            {
                // Check if there's a digit after the decimal point
                if (_position + 1 < _span.Length && char.IsDigit(_span[_position + 1]))
                {
                    hasDecimalPoint = true;
                    _position++;
                    continue;
                }
            }

            break;
        }

        var numericType = hasDecimalPoint ? NumericType.FloatingPoint : NumericType.Integer;
        return new NumericToken { Content = _source.Slice(start, _position - start), NumericType = numericType, Position = start };
    }

    /// <summary>
    /// Tries to match a comment start delimiter at the current position.
    /// </summary>
    /// <returns>The matched <see cref="CommentStyle"/> or null if no match.</returns>
    private CommentStyle? TryMatchCommentStart()
    {
        foreach (var style in _options.CommentStyles)
        {
            if (MatchesAt(_position, style.Start))
            {
                return style;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if the given string matches at the specified position.
    /// </summary>
    /// <param name="position">The position to check at.</param>
    /// <param name="value">The string to match.</param>
    /// <returns>True if the string matches at the position.</returns>
    private bool MatchesAt(int position, string value)
    {
        if (position + value.Length > _span.Length)
        {
            return false;
        }

        return _span.Slice(position, value.Length).SequenceEqual(value.AsSpan());
    }

    /// <summary>
    /// Parses a comment starting at the current position.
    /// </summary>
    /// <param name="style">The comment style to parse.</param>
    /// <returns>A <see cref="CommentToken"/>.</returns>
    private CommentToken ParseComment(CommentStyle style)
    {
        int start = _position;

        // Consume the start delimiter
        _position += style.Start.Length;

        if (style.IsMultiLine)
        {
            // Multi-line comment: look for the end delimiter
            var remaining = _span[_position..];
            var endSpan = style.End.AsSpan();
            
            while (remaining.Length > 0)
            {
                // Find the first character of the end delimiter
                int idx = remaining.IndexOf(endSpan[0]);
                if (idx < 0)
                {
                    // End delimiter not found, consume everything
                    _position = _span.Length;
                    break;
                }

                _position += idx;
                
                // Check if full end delimiter matches
                if (MatchesAt(_position, style.End!))
                {
                    _position += style.End!.Length;
                    break;
                }
                
                // Not a match, advance past this character and continue
                _position++;
                remaining = _span[_position..];
            }
        }
        else
        {
            // Single-line comment: consume until end of line
            var remaining = _span[_position..];
            int idx = remaining.IndexOfAny('\n', '\r');
            
            if (idx < 0)
            {
                // No newline found, consume to end
                _position = _span.Length;
            }
            else
            {
                _position += idx;
            }
        }

        return new CommentToken { Content = _source.Slice(start, _position - start), IsMultiLine = style.IsMultiLine, Position = start };
    }

    /// <summary>
    /// Tries to parse an operator at the current position.
    /// Uses trie-based greedy matching for O(k) lookup where k = operator length.
    /// </summary>
    /// <returns>An <see cref="OperatorToken"/> if an operator is matched, null otherwise.</returns>
    private OperatorToken? TryParseOperator()
    {
        if (_operatorTrie.IsEmpty)
            return null;

        var remaining = _span[_position..];

        // Use trie for O(k) greedy matching
        if (_operatorTrie.TryMatch(remaining, out var matchedOp) && matchedOp is not null)
        {
            var start = _position;
            _position += matchedOp.Length;
            return new OperatorToken { Content = _source.Slice(start, matchedOp.Length), Position = start };
        }

        return null;
    }

    #endregion
}
