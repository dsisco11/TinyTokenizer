using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace TinyTokenizer;

/// <summary>
/// A stateless lexer that classifies characters into simple tokens.
/// Level 1 of the two-level tokenizer architecture.
/// Never backtracks and never fails — purely streaming character classification.
/// </summary>
public sealed class Lexer
{
    private readonly ImmutableHashSet<char> _symbols;

    /// <summary>
    /// Initializes a new instance of <see cref="Lexer"/> with default symbol set.
    /// </summary>
    public Lexer() : this(TokenizerOptions.Default.Symbols)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Lexer"/> with the specified symbol set.
    /// </summary>
    /// <param name="symbols">The set of characters to classify as symbols.</param>
    public Lexer(ImmutableHashSet<char> symbols)
    {
        _symbols = symbols;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Lexer"/> with options.
    /// </summary>
    /// <param name="options">The tokenizer options.</param>
    public Lexer(TokenizerOptions options) : this(options.Symbols)
    {
    }

    #region Synchronous Lexing

    /// <summary>
    /// Lexes the input into simple tokens.
    /// </summary>
    /// <param name="input">The input to lex.</param>
    /// <returns>An enumerable of simple tokens.</returns>
    public IEnumerable<SimpleToken> Lex(ReadOnlyMemory<char> input)
    {
        if (input.IsEmpty)
            yield break;

        int position = 0;
        int length = input.Length;

        while (position < length)
        {
            char c = input.Span[position];
            int start = position;

            // Single-character tokens with dedicated types
            var singleCharType = ClassifySingleChar(c);
            if (singleCharType.HasValue)
            {
                position++;
                yield return new SimpleToken(singleCharType.Value, input.Slice(start, 1), start);
                continue;
            }

            // Newline (handle \r\n as single token)
            if (c == '\n')
            {
                position++;
                yield return new SimpleToken(SimpleTokenType.Newline, input.Slice(start, 1), start);
                continue;
            }
            if (c == '\r')
            {
                position++;
                int tokenLength = 1;
                if (position < length && input.Span[position] == '\n')
                {
                    position++;
                    tokenLength = 2;
                }
                yield return new SimpleToken(SimpleTokenType.Newline, input.Slice(start, tokenLength), start);
                continue;
            }

            // Whitespace (excluding newlines)
            if (char.IsWhiteSpace(c))
            {
                while (position < length)
                {
                    char current = input.Span[position];
                    if (!char.IsWhiteSpace(current) || current == '\n' || current == '\r')
                        break;
                    position++;
                }
                yield return new SimpleToken(SimpleTokenType.Whitespace, input.Slice(start, position - start), start);
                continue;
            }

            // Numeric (digits with optional single decimal point)
            if (char.IsDigit(c))
            {
                bool hasDecimal = false;
                while (position < length)
                {
                    char current = input.Span[position];
                    if (char.IsDigit(current))
                    {
                        position++;
                    }
                    else if (current == '.' && !hasDecimal && position + 1 < length && char.IsDigit(input.Span[position + 1]))
                    {
                        hasDecimal = true;
                        position++;
                    }
                    else
                    {
                        break;
                    }
                }
                yield return new SimpleToken(SimpleTokenType.Numeric, input.Slice(start, position - start), start);
                continue;
            }

            // Symbol (from configured set, excluding special chars handled above)
            if (_symbols.Contains(c))
            {
                position++;
                yield return new SimpleToken(SimpleTokenType.Symbol, input.Slice(start, 1), start);
                continue;
            }

            // Text (everything else that forms identifier-like content)
            while (position < length)
            {
                char current = input.Span[position];
                if (char.IsWhiteSpace(current) ||
                    ClassifySingleChar(current).HasValue ||
                    _symbols.Contains(current))
                {
                    break;
                }
                position++;
            }

            if (position > start)
            {
                yield return new SimpleToken(SimpleTokenType.Text, input.Slice(start, position - start), start);
            }
        }
    }

    /// <summary>
    /// Lexes the input string into simple tokens.
    /// </summary>
    public IEnumerable<SimpleToken> Lex(string input)
    {
        return Lex(input.AsMemory());
    }

    /// <summary>
    /// Lexes the input into an immutable array of simple tokens.
    /// </summary>
    public ImmutableArray<SimpleToken> LexToArray(ReadOnlyMemory<char> input)
    {
        return [.. Lex(input)];
    }

    #endregion

    #region Asynchronous Lexing

    /// <summary>
    /// Lexes a stream of characters into simple tokens asynchronously.
    /// </summary>
    /// <param name="chars">The async enumerable of characters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of simple tokens.</returns>
    public async IAsyncEnumerable<SimpleToken> LexAsync(
        IAsyncEnumerable<char> chars,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long position = 0;
        var buffer = new List<char>();
        SimpleTokenType? currentType = null;
        long tokenStart = 0;

        await foreach (var c in chars.WithCancellation(cancellationToken))
        {
            var charType = ClassifyChar(c);

            // Handle newlines specially
            if (c == '\r' || c == '\n')
            {
                // Flush any pending token
                if (buffer.Count > 0 && currentType.HasValue)
                {
                    yield return CreateToken(currentType.Value, buffer, tokenStart);
                    buffer.Clear();
                    currentType = null;
                }

                // For \r, we need to peek for \n
                if (c == '\r')
                {
                    buffer.Add(c);
                    tokenStart = position;
                    currentType = SimpleTokenType.Newline;
                    // Will be flushed on next char or end
                }
                else // \n
                {
                    if (currentType == SimpleTokenType.Newline && buffer.Count == 1 && buffer[0] == '\r')
                    {
                        // This is \r\n - add \n and emit
                        buffer.Add(c);
                        yield return CreateToken(SimpleTokenType.Newline, buffer, tokenStart);
                        buffer.Clear();
                        currentType = null;
                    }
                    else
                    {
                        yield return new SimpleToken(SimpleTokenType.Newline, new[] { c }.AsMemory(), position);
                    }
                }

                position++;
                continue;
            }

            // Flush pending \r if next char is not \n
            if (currentType == SimpleTokenType.Newline && buffer.Count == 1 && buffer[0] == '\r')
            {
                yield return CreateToken(SimpleTokenType.Newline, buffer, tokenStart);
                buffer.Clear();
                currentType = null;
            }

            // Single-character tokens
            if (IsSingleCharType(charType))
            {
                // Flush any pending token
                if (buffer.Count > 0 && currentType.HasValue)
                {
                    yield return CreateToken(currentType.Value, buffer, tokenStart);
                    buffer.Clear();
                    currentType = null;
                }

                yield return new SimpleToken(charType, new[] { c }.AsMemory(), position);
                position++;
                continue;
            }

            // Groupable tokens (Text, Whitespace, Numeric)
            if (currentType == charType)
            {
                buffer.Add(c);
            }
            else
            {
                // Type changed - flush previous token
                if (buffer.Count > 0 && currentType.HasValue)
                {
                    yield return CreateToken(currentType.Value, buffer, tokenStart);
                    buffer.Clear();
                }

                buffer.Add(c);
                currentType = charType;
                tokenStart = position;
            }

            position++;
        }

        // Flush remaining buffer
        if (buffer.Count > 0 && currentType.HasValue)
        {
            yield return CreateToken(currentType.Value, buffer, tokenStart);
        }
    }

    /// <summary>
    /// Lexes a stream of memory chunks into simple tokens asynchronously.
    /// </summary>
    public async IAsyncEnumerable<SimpleToken> LexAsync(
        IAsyncEnumerable<ReadOnlyMemory<char>> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long absolutePosition = 0;
        var pendingBuffer = new List<char>();
        SimpleTokenType? pendingType = null;
        long pendingStart = 0;
        bool pendingHasDecimal = false;

        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            // Lex the chunk
            foreach (var token in Lex(chunk))
            {
                var adjustedToken = new SimpleToken(
                    token.Type,
                    token.Content,
                    absolutePosition + token.Position);

                // Special case: pending Numeric + Symbol('.') could be start of decimal
                if (pendingType == SimpleTokenType.Numeric && 
                    !pendingHasDecimal &&
                    adjustedToken.Type == SimpleTokenType.Symbol && 
                    adjustedToken.ContentEquals('.'))
                {
                    // Add the decimal to pending buffer, marking we have a decimal
                    pendingBuffer.Add('.');
                    pendingHasDecimal = true;
                    continue;
                }

                // Special case: pending Numeric (with decimal) + more Numeric digits
                if (pendingType == SimpleTokenType.Numeric && 
                    pendingHasDecimal && 
                    adjustedToken.Type == SimpleTokenType.Numeric)
                {
                    // Merge the digits after the decimal
                    AppendToBuffer(pendingBuffer, adjustedToken.Content);
                    continue;
                }

                // Check if we can merge with pending (same type)
                if (pendingType.HasValue && CanMerge(pendingType.Value, adjustedToken.Type))
                {
                    // Merge into pending buffer
                    AppendToBuffer(pendingBuffer, adjustedToken.Content);
                }
                else
                {
                    // Flush pending if any
                    if (pendingBuffer.Count > 0 && pendingType.HasValue)
                    {
                        yield return CreateToken(pendingType.Value, pendingBuffer, pendingStart);
                        pendingBuffer.Clear();
                        pendingHasDecimal = false;
                    }

                    // Check if this token might need merging with next chunk
                    if (IsAtChunkBoundary(token, chunk) && CanMergeAcrossChunks(adjustedToken.Type))
                    {
                        pendingType = adjustedToken.Type;
                        pendingStart = adjustedToken.Position;
                        AppendToBuffer(pendingBuffer, adjustedToken.Content);
                    }
                    else
                    {
                        pendingType = null;
                        yield return adjustedToken;
                    }
                }
            }

            absolutePosition += chunk.Length;
        }

        // Flush remaining pending
        if (pendingBuffer.Count > 0 && pendingType.HasValue)
        {
            yield return CreateToken(pendingType.Value, pendingBuffer, pendingStart);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Classifies a single character that should always be its own token.
    /// </summary>
    private static SimpleTokenType? ClassifySingleChar(char c)
    {
        return c switch
        {
            '{' => SimpleTokenType.OpenBrace,
            '}' => SimpleTokenType.CloseBrace,
            '[' => SimpleTokenType.OpenBracket,
            ']' => SimpleTokenType.CloseBracket,
            '(' => SimpleTokenType.OpenParen,
            ')' => SimpleTokenType.CloseParen,
            '\'' => SimpleTokenType.SingleQuote,
            '"' => SimpleTokenType.DoubleQuote,
            '\\' => SimpleTokenType.Backslash,
            '/' => SimpleTokenType.Slash,
            '*' => SimpleTokenType.Asterisk,
            _ => null
        };
    }

    /// <summary>
    /// Classifies a character into its token type.
    /// </summary>
    private SimpleTokenType ClassifyChar(char c)
    {
        var singleChar = ClassifySingleChar(c);
        if (singleChar.HasValue)
            return singleChar.Value;

        if (c == '\r' || c == '\n')
            return SimpleTokenType.Newline;

        if (char.IsWhiteSpace(c))
            return SimpleTokenType.Whitespace;

        if (char.IsDigit(c))
            return SimpleTokenType.Numeric;

        if (_symbols.Contains(c))
            return SimpleTokenType.Symbol;

        return SimpleTokenType.Text;
    }

    /// <summary>
    /// Checks if a token type is always a single character.
    /// </summary>
    private static bool IsSingleCharType(SimpleTokenType type)
    {
        return type switch
        {
            SimpleTokenType.OpenBrace => true,
            SimpleTokenType.CloseBrace => true,
            SimpleTokenType.OpenBracket => true,
            SimpleTokenType.CloseBracket => true,
            SimpleTokenType.OpenParen => true,
            SimpleTokenType.CloseParen => true,
            SimpleTokenType.SingleQuote => true,
            SimpleTokenType.DoubleQuote => true,
            SimpleTokenType.Backslash => true,
            SimpleTokenType.Slash => true,
            SimpleTokenType.Asterisk => true,
            SimpleTokenType.Symbol => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if two token types can be merged.
    /// </summary>
    private static bool CanMerge(SimpleTokenType current, SimpleTokenType next)
    {
        if (current != next)
            return false;

        return current switch
        {
            SimpleTokenType.Text => true,
            SimpleTokenType.Whitespace => true,
            SimpleTokenType.Numeric => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a token type can be merged across chunk boundaries.
    /// </summary>
    private static bool CanMergeAcrossChunks(SimpleTokenType type)
    {
        return type switch
        {
            SimpleTokenType.Text => true,
            SimpleTokenType.Whitespace => true,
            SimpleTokenType.Numeric => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a token is at the end of its chunk.
    /// </summary>
    private static bool IsAtChunkBoundary(SimpleToken token, ReadOnlyMemory<char> chunk)
    {
        return token.Position + token.Length == chunk.Length;
    }

    /// <summary>
    /// Creates a token from a buffer.
    /// </summary>
    private static SimpleToken CreateToken(SimpleTokenType type, List<char> buffer, long position)
    {
        var content = buffer.ToArray().AsMemory();
        return new SimpleToken(type, content, position);
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

    #endregion
}
