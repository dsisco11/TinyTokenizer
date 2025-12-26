using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace TinyTokenizer;

/// <summary>
/// A stateless lexer that classifies characters into simple tokens.
/// Level 1 of the two-level tokenizer architecture.
/// Never backtracks and never fails — pure character classification.
/// </summary>
public sealed class Lexer
{
    #region Static SearchValues

    /// <summary>
    /// All characters that have dedicated single-char token types.
    /// Used for fast rejection before the switch in ClassifySingleChar.
    /// </summary>
    private static readonly SearchValues<char> SingleCharTokens =
        SearchValues.Create("{}[]()'\"\\/.*");

    /// <summary>
    /// All Unicode whitespace characters excluding CR and LF.
    /// Built by enumerating all char values where char.IsWhiteSpace is true.
    /// </summary>
    private static readonly SearchValues<char> NonNewlineWhitespace = BuildNonNewlineWhitespace();

    /// <summary>
    /// Builds the SearchValues for all whitespace characters except \r and \n.
    /// </summary>
    private static SearchValues<char> BuildNonNewlineWhitespace()
    {
        var whitespaceChars = new List<char>();
        for (int i = 0; i <= char.MaxValue; i++)
        {
            char c = (char)i;
            if (char.IsWhiteSpace(c) && c != '\r' && c != '\n')
            {
                whitespaceChars.Add(c);
            }
        }
        return SearchValues.Create(CollectionsMarshal.AsSpan(whitespaceChars));
    }

    #endregion

    #region Instance Fields

    private readonly SearchValues<char> _symbols;
    private readonly SearchValues<char> _identTerminators;

    #endregion

    #region Constructors

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
        // Build SearchValues for symbols (vectorized Contains)
        _symbols = SearchValues.Create(symbols.ToArray());

        // Build identifier terminators: whitespace + single-char tokens + symbols
        _identTerminators = BuildIdentTerminators(symbols);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Lexer"/> with options.
    /// </summary>
    /// <param name="options">The tokenizer options.</param>
    public Lexer(TokenizerOptions options) : this(options.Symbols)
    {
    }

    /// <summary>
    /// Builds a SearchValues containing all characters that terminate an identifier.
    /// </summary>
    private static SearchValues<char> BuildIdentTerminators(ImmutableHashSet<char> symbols)
    {
        var terminators = new HashSet<char>();

        // Add all non-newline whitespace
        for (int i = 0; i <= char.MaxValue; i++)
        {
            char c = (char)i;
            if (char.IsWhiteSpace(c))
            {
                terminators.Add(c);
            }
        }

        // Add single-char token characters
        terminators.UnionWith("{}[]()'\"\\/.*");

        // Add configured symbols
        terminators.UnionWith(symbols);

        return SearchValues.Create(CollectionsMarshal.AsSpan(terminators.ToList()));
    }

    #endregion

    #region Public Methods

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
            // Note: Access input.Span fresh each iteration (cannot store across yield)
            char c = input.Span[position];
            int start = position;

            // Fast path: check if char is a single-char token using SearchValues
            if (SingleCharTokens.Contains(c))
            {
                var singleCharType = ClassifySingleChar(c);
                if (singleCharType.HasValue)
                {
                    position++;
                    yield return new SimpleToken(singleCharType.Value, input.Slice(start, 1), start);
                    continue;
                }
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

            // Whitespace (excluding newlines) - use vectorized IndexOfAnyExcept
            if (NonNewlineWhitespace.Contains(c))
            {
                int wsEnd = input.Span[position..].IndexOfAnyExcept(NonNewlineWhitespace);
                int wsLength = wsEnd < 0 ? length - position : wsEnd;
                position += wsLength;
                yield return new SimpleToken(SimpleTokenType.Whitespace, input.Slice(start, wsLength), start);
                continue;
            }

            // Digits (consecutive digit characters only) - use vectorized IndexOfAnyExceptInRange
            if (char.IsAsciiDigit(c))
            {
                int digitEnd = input.Span[position..].IndexOfAnyExceptInRange('0', '9');
                int digitLength = digitEnd < 0 ? length - position : digitEnd;
                position += digitLength;
                yield return new SimpleToken(SimpleTokenType.Digits, input.Slice(start, digitLength), start);
                continue;
            }

            // Symbol (from configured set, excluding special chars handled above)
            if (_symbols.Contains(c))
            {
                position++;
                yield return new SimpleToken(SimpleTokenType.Symbol, input.Slice(start, 1), start);
                continue;
            }

            // Identifier (everything else) - use vectorized IndexOfAny for terminators
            int identEnd = input.Span[position..].IndexOfAny(_identTerminators);
            int identLength = identEnd < 0 ? length - position : identEnd;

            if (identLength > 0)
            {
                position += identLength;
                yield return new SimpleToken(SimpleTokenType.Ident, input.Slice(start, identLength), start);
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

    #endregion

    #region Private Methods

    /// <summary>
    /// Classifies a single character that should always be its own token.
    /// Uses fast rejection via SearchValues before the switch.
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

    #endregion
}
