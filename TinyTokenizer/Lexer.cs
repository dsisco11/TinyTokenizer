using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// A stateless lexer that classifies characters into simple tokens.
/// Level 1 of the two-level tokenizer architecture.
/// Never backtracks and never fails — pure character classification.
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

            // Digits (consecutive digit characters only)
            if (char.IsDigit(c))
            {
                while (position < length && char.IsDigit(input.Span[position]))
                {
                    position++;
                }
                yield return new SimpleToken(SimpleTokenType.Digits, input.Slice(start, position - start), start);
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

    /// <summary>
    /// Lexes the input string into an immutable array of simple tokens.
    /// </summary>
    public ImmutableArray<SimpleToken> LexToArray(string input)
    {
        return [.. Lex(input)];
    }

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
            '.' => SimpleTokenType.Dot,
            _ => null
        };
    }
}
