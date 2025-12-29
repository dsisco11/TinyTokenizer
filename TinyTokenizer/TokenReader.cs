using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace TinyTokenizer;

/// <summary>
/// A ref struct reader for navigating token sequences, following .NET SequenceReader&lt;T&gt; conventions.
/// Provides position tracking, peek/read operations, and token-specific matching helpers.
/// </summary>
public ref struct TokenReader
{
    private readonly ReadOnlySpan<Token> _tokens;
    private int _position;
    private readonly bool _autoSkipWhitespace;

    #region Constructors

    /// <summary>
    /// Initializes a new TokenReader over the specified token span.
    /// </summary>
    /// <param name="tokens">The tokens to read.</param>
    /// <param name="autoSkipWhitespace">Whether to automatically skip whitespace tokens before read operations.</param>
    public TokenReader(ReadOnlySpan<Token> tokens, bool autoSkipWhitespace = false)
    {
        _tokens = tokens;
        _position = 0;
        _autoSkipWhitespace = autoSkipWhitespace;
    }

    /// <summary>
    /// Initializes a new TokenReader over the specified immutable array of tokens.
    /// </summary>
    /// <param name="tokens">The tokens to read.</param>
    /// <param name="autoSkipWhitespace">Whether to automatically skip whitespace tokens before read operations.</param>
    public TokenReader(ImmutableArray<Token> tokens, bool autoSkipWhitespace = false)
        : this(tokens.AsSpan(), autoSkipWhitespace)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of tokens that have been consumed (read past).
    /// </summary>
    public readonly int Consumed => _position;

    /// <summary>
    /// Gets the number of tokens remaining to be read.
    /// </summary>
    public readonly int Remaining => _tokens.Length - _position;

    /// <summary>
    /// Gets the total number of tokens in the sequence.
    /// </summary>
    public readonly int Length => _tokens.Length;

    /// <summary>
    /// Gets whether the reader has reached the end of the token sequence.
    /// </summary>
    public readonly bool End => _position >= _tokens.Length;

    /// <summary>
    /// Gets the unread portion of the token sequence.
    /// </summary>
    public readonly ReadOnlySpan<Token> UnreadSpan => _tokens[_position..];

    /// <summary>
    /// Gets the current token without advancing, or null if at end.
    /// </summary>
    public readonly Token? Current => _position < _tokens.Length ? _tokens[_position] : null;

    /// <summary>
    /// Gets whether auto-skip whitespace mode is enabled.
    /// </summary>
    public readonly bool AutoSkipWhitespace => _autoSkipWhitespace;

    #endregion

    #region Core Reading

    /// <summary>
    /// Tries to read the next token and advance the position.
    /// </summary>
    /// <param name="token">When successful, the token that was read.</param>
    /// <returns>True if a token was read; false if at end.</returns>
    public bool TryRead(out Token? token)
    {
        if (_autoSkipWhitespace)
            AdvancePastWhitespace();

        if (_position < _tokens.Length)
        {
            token = _tokens[_position];
            _position++;
            return true;
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Tries to peek at the next token without advancing.
    /// </summary>
    /// <param name="token">When successful, the token at the current position.</param>
    /// <returns>True if a token exists; false if at end.</returns>
    public bool TryPeek(out Token? token)
    {
        var peekPosition = _position;
        
        if (_autoSkipWhitespace)
        {
            while (peekPosition < _tokens.Length && _tokens[peekPosition].Type == TokenType.Whitespace)
                peekPosition++;
        }

        if (peekPosition < _tokens.Length)
        {
            token = _tokens[peekPosition];
            return true;
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Tries to peek at a token at the specified offset without advancing.
    /// </summary>
    /// <param name="offset">The offset from the current position (0 = current).</param>
    /// <param name="token">When successful, the token at the offset.</param>
    /// <returns>True if a token exists at the offset; false otherwise.</returns>
    public bool TryPeek(int offset, out Token? token)
    {
        var peekPosition = _position;

        if (_autoSkipWhitespace)
        {
            // Skip whitespace, then count non-whitespace tokens
            int nonWhitespaceCount = 0;
            while (peekPosition < _tokens.Length)
            {
                if (_tokens[peekPosition].Type != TokenType.Whitespace)
                {
                    if (nonWhitespaceCount == offset)
                        break;
                    nonWhitespaceCount++;
                }
                peekPosition++;
            }
        }
        else
        {
            peekPosition += offset;
        }

        if (peekPosition >= 0 && peekPosition < _tokens.Length)
        {
            token = _tokens[peekPosition];
            return true;
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Tries to read exactly the specified number of tokens.
    /// </summary>
    /// <param name="count">The number of tokens to read.</param>
    /// <param name="tokens">When successful, a span containing the tokens.</param>
    /// <returns>True if enough tokens were available; false otherwise.</returns>
    public bool TryReadExact(int count, out ReadOnlySpan<Token> tokens)
    {
        if (_autoSkipWhitespace)
            AdvancePastWhitespace();

        if (_position + count <= _tokens.Length)
        {
            tokens = _tokens.Slice(_position, count);
            _position += count;
            return true;
        }

        tokens = default;
        return false;
    }

    #endregion

    #region Type-Safe Reading

    /// <summary>
    /// Tries to read the next token if it matches the specified type.
    /// </summary>
    /// <typeparam name="T">The expected token type.</typeparam>
    /// <param name="token">When successful, the token cast to the specified type.</param>
    /// <returns>True if the next token matched and was read; false otherwise.</returns>
    public bool TryRead<T>(out T? token) where T : Token
    {
        if (_autoSkipWhitespace)
            AdvancePastWhitespace();

        if (_position < _tokens.Length && _tokens[_position] is T typed)
        {
            token = typed;
            _position++;
            return true;
        }

        token = null;
        return false;
    }

    /// <summary>
    /// Tries to peek at the next token if it matches the specified type.
    /// </summary>
    /// <typeparam name="T">The expected token type.</typeparam>
    /// <param name="token">When successful, the token cast to the specified type.</param>
    /// <returns>True if the next token matches the type; false otherwise.</returns>
    public bool TryPeek<T>(out T? token) where T : Token
    {
        var peekPosition = _position;
        
        if (_autoSkipWhitespace)
        {
            while (peekPosition < _tokens.Length && _tokens[peekPosition].Type == TokenType.Whitespace)
                peekPosition++;
        }

        if (peekPosition < _tokens.Length && _tokens[peekPosition] is T typed)
        {
            token = typed;
            return true;
        }

        token = null;
        return false;
    }

    #endregion

    #region Navigation

    /// <summary>
    /// Advances the reader by the specified number of tokens.
    /// </summary>
    /// <param name="count">The number of tokens to advance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count = 1)
    {
        _position = Math.Min(_position + count, _tokens.Length);
    }

    /// <summary>
    /// Rewinds the reader by the specified number of tokens.
    /// </summary>
    /// <param name="count">The number of tokens to rewind.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Rewind(int count = 1)
    {
        _position = Math.Max(_position - count, 0);
    }

    /// <summary>
    /// Advances the reader to the end of the token sequence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceToEnd()
    {
        _position = _tokens.Length;
    }

    /// <summary>
    /// Resets the reader to the beginning of the token sequence.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _position = 0;
    }

    #endregion

    #region Skip Methods

    /// <summary>
    /// Advances past all consecutive tokens of the specified type.
    /// </summary>
    /// <param name="type">The token type to skip.</param>
    /// <returns>The number of tokens skipped.</returns>
    public int AdvancePast(TokenType type)
    {
        int skipped = 0;
        while (_position < _tokens.Length && _tokens[_position].Type == type)
        {
            _position++;
            skipped++;
        }
        return skipped;
    }

    /// <summary>
    /// Advances past all consecutive tokens matching any of the specified types.
    /// </summary>
    /// <param name="types">The token types to skip.</param>
    /// <returns>The number of tokens skipped.</returns>
    public int AdvancePastAny(params TokenType[] types)
    {
        int skipped = 0;
        while (_position < _tokens.Length)
        {
            var currentType = _tokens[_position].Type;
            bool match = false;
            foreach (var type in types)
            {
                if (currentType == type)
                {
                    match = true;
                    break;
                }
            }
            if (!match) break;
            _position++;
            skipped++;
        }
        return skipped;
    }

    /// <summary>
    /// Advances past all consecutive whitespace tokens.
    /// </summary>
    /// <returns>The number of whitespace tokens skipped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AdvancePastWhitespace()
    {
        return AdvancePast(TokenType.Whitespace);
    }

    #endregion

    #region Delimiter Reading

    /// <summary>
    /// Tries to advance to the first token of the specified type.
    /// </summary>
    /// <param name="type">The token type to find.</param>
    /// <param name="advancePast">Whether to advance past the found token.</param>
    /// <returns>True if the token type was found; false if end was reached.</returns>
    public bool TryAdvanceTo(TokenType type, bool advancePast = true)
    {
        while (_position < _tokens.Length)
        {
            if (_tokens[_position].Type == type)
            {
                if (advancePast)
                    _position++;
                return true;
            }
            _position++;
        }
        return false;
    }

    /// <summary>
    /// Tries to advance to the first token matching any of the specified types.
    /// </summary>
    /// <param name="types">The token types to find.</param>
    /// <param name="advancePast">Whether to advance past the found token.</param>
    /// <returns>True if any type was found; false if end was reached.</returns>
    public bool TryAdvanceToAny(ReadOnlySpan<TokenType> types, bool advancePast = true)
    {
        while (_position < _tokens.Length)
        {
            var currentType = _tokens[_position].Type;
            foreach (var type in types)
            {
                if (currentType == type)
                {
                    if (advancePast)
                        _position++;
                    return true;
                }
            }
            _position++;
        }
        return false;
    }

    /// <summary>
    /// Tries to read all tokens up to (but not including) the delimiter.
    /// </summary>
    /// <param name="tokens">When successful, the tokens read before the delimiter.</param>
    /// <param name="delimiter">The token type to stop at.</param>
    /// <param name="advancePast">Whether to advance past the delimiter.</param>
    /// <returns>True if the delimiter was found; false if end was reached.</returns>
    public bool TryReadTo(out ReadOnlySpan<Token> tokens, TokenType delimiter, bool advancePast = true)
    {
        int start = _position;
        while (_position < _tokens.Length)
        {
            if (_tokens[_position].Type == delimiter)
            {
                tokens = _tokens[start.._position];
                if (advancePast)
                    _position++;
                return true;
            }
            _position++;
        }
        tokens = default;
        _position = start; // Reset on failure
        return false;
    }

    #endregion

    #region Token-Specific Matching

    /// <summary>
    /// Checks if the next token is of the specified type.
    /// </summary>
    /// <param name="type">The token type to check for.</param>
    /// <param name="advancePast">Whether to advance past the token if it matches.</param>
    /// <returns>True if the next token matches; false otherwise.</returns>
    public bool IsNext(TokenType type, bool advancePast = false)
    {
        var checkPosition = _position;
        
        if (_autoSkipWhitespace && type != TokenType.Whitespace)
        {
            while (checkPosition < _tokens.Length && _tokens[checkPosition].Type == TokenType.Whitespace)
                checkPosition++;
        }

        if (checkPosition < _tokens.Length && _tokens[checkPosition].Type == type)
        {
            if (advancePast)
                _position = checkPosition + 1;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the next token is a symbol with the specified character.
    /// </summary>
    /// <param name="symbol">The symbol character to check for.</param>
    /// <param name="advancePast">Whether to advance past the token if it matches.</param>
    /// <returns>True if the next token is the specified symbol; false otherwise.</returns>
    public bool IsNextSymbol(char symbol, bool advancePast = false)
    {
        var checkPosition = _position;
        
        if (_autoSkipWhitespace)
        {
            while (checkPosition < _tokens.Length && _tokens[checkPosition].Type == TokenType.Whitespace)
                checkPosition++;
        }

        if (checkPosition < _tokens.Length && 
            _tokens[checkPosition].Type == TokenType.Symbol &&
            ((SymbolToken)_tokens[checkPosition]).Symbol == symbol)
        {
            if (advancePast)
                _position = checkPosition + 1;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the next token is an operator with the specified value.
    /// </summary>
    /// <param name="op">The operator string to check for.</param>
    /// <param name="advancePast">Whether to advance past the token if it matches.</param>
    /// <returns>True if the next token is the specified operator; false otherwise.</returns>
    public bool IsNextOperator(string op, bool advancePast = false)
    {
        var checkPosition = _position;
        
        if (_autoSkipWhitespace)
        {
            while (checkPosition < _tokens.Length && _tokens[checkPosition].Type == TokenType.Whitespace)
                checkPosition++;
        }

        if (checkPosition < _tokens.Length && 
            _tokens[checkPosition].Type == TokenType.Operator &&
            ((OperatorToken)_tokens[checkPosition]).Operator == op)
        {
            if (advancePast)
                _position = checkPosition + 1;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the next token is an identifier.
    /// </summary>
    /// <param name="advancePast">Whether to advance past the token if it matches.</param>
    /// <returns>True if the next token is an identifier; false otherwise.</returns>
    public bool IsNextIdent(bool advancePast = false)
    {
        return IsNext(TokenType.Ident, advancePast);
    }

    /// <summary>
    /// Checks if the next token is an identifier with the specified name.
    /// </summary>
    /// <param name="name">The identifier name to check for.</param>
    /// <param name="advancePast">Whether to advance past the token if it matches.</param>
    /// <returns>True if the next token is the specified identifier; false otherwise.</returns>
    public bool IsNextIdent(ReadOnlySpan<char> name, bool advancePast = false)
    {
        var checkPosition = _position;
        
        if (_autoSkipWhitespace)
        {
            while (checkPosition < _tokens.Length && _tokens[checkPosition].Type == TokenType.Whitespace)
                checkPosition++;
        }

        if (checkPosition < _tokens.Length && 
            _tokens[checkPosition].Type == TokenType.Ident &&
            _tokens[checkPosition].ContentSpan.SequenceEqual(name))
        {
            if (advancePast)
                _position = checkPosition + 1;
            return true;
        }
        return false;
    }

    #endregion

    #region Block Handling

    /// <summary>
    /// Tries to read a block token (brace, bracket, or parenthesis block).
    /// </summary>
    /// <param name="block">When successful, the block that was read.</param>
    /// <returns>True if a block was read; false otherwise.</returns>
    public bool TryReadBlock(out SimpleBlock? block)
    {
        if (_autoSkipWhitespace)
            AdvancePastWhitespace();

        if (_position < _tokens.Length)
        {
            var type = _tokens[_position].Type;
            if (type == TokenType.BraceBlock || 
                type == TokenType.BracketBlock || 
                type == TokenType.ParenthesisBlock)
            {
                block = (SimpleBlock)_tokens[_position];
                _position++;
                return true;
            }
        }

        block = null;
        return false;
    }

    /// <summary>
    /// Tries to read a block token with the specified opening delimiter.
    /// </summary>
    /// <param name="opener">The opening delimiter character ('(', '[', or '{').</param>
    /// <param name="block">When successful, the block that was read.</param>
    /// <returns>True if a matching block was read; false otherwise.</returns>
    public bool TryReadBlock(char opener, out SimpleBlock? block)
    {
        if (_autoSkipWhitespace)
            AdvancePastWhitespace();

        if (_position < _tokens.Length)
        {
            var token = _tokens[_position];
            var expectedType = opener switch
            {
                '(' => TokenType.ParenthesisBlock,
                '[' => TokenType.BracketBlock,
                '{' => TokenType.BraceBlock,
                _ => (TokenType?)null
            };

            if (expectedType.HasValue && token.Type == expectedType.Value)
            {
                block = (SimpleBlock)token;
                _position++;
                return true;
            }
        }

        block = null;
        return false;
    }

    /// <summary>
    /// Checks if the next token is a block with the specified opening delimiter.
    /// </summary>
    /// <param name="opener">The opening delimiter character ('(', '[', or '{').</param>
    /// <param name="advancePast">Whether to advance past the block if it matches.</param>
    /// <returns>True if the next token is the specified block type; false otherwise.</returns>
    public bool IsNextBlock(char opener, bool advancePast = false)
    {
        var checkPosition = _position;
        
        if (_autoSkipWhitespace)
        {
            while (checkPosition < _tokens.Length && _tokens[checkPosition].Type == TokenType.Whitespace)
                checkPosition++;
        }

        var expectedType = opener switch
        {
            '(' => TokenType.ParenthesisBlock,
            '[' => TokenType.BracketBlock,
            '{' => TokenType.BraceBlock,
            _ => (TokenType?)null
        };

        if (expectedType.HasValue && 
            checkPosition < _tokens.Length && 
            _tokens[checkPosition].Type == expectedType.Value)
        {
            if (advancePast)
                _position = checkPosition + 1;
            return true;
        }
        return false;
    }

    #endregion

    #region Sequence Matching

    /// <summary>
    /// Checks if the next tokens match the specified type sequence.
    /// </summary>
    /// <param name="pattern">The pattern of token types to match.</param>
    /// <param name="advancePast">Whether to advance past all matched tokens.</param>
    /// <returns>True if the sequence matches; false otherwise.</returns>
    public bool IsNextSequence(ReadOnlySpan<TokenType> pattern, bool advancePast = false)
    {
        if (pattern.IsEmpty)
            return true;

        var checkPosition = _position;

        foreach (var expectedType in pattern)
        {
            if (_autoSkipWhitespace && expectedType != TokenType.Whitespace)
            {
                while (checkPosition < _tokens.Length && _tokens[checkPosition].Type == TokenType.Whitespace)
                    checkPosition++;
            }

            if (checkPosition >= _tokens.Length || _tokens[checkPosition].Type != expectedType)
                return false;

            checkPosition++;
        }

        if (advancePast)
            _position = checkPosition;

        return true;
    }

    #endregion
}
