using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Extension methods for creating <see cref="TokenBuffer"/> instances.
/// </summary>
public static class TokenBufferExtensions
{
    #region From SimpleTokens (Preferred)

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the SimpleToken array.
    /// This is the most efficient way to create a buffer.
    /// </summary>
    /// <param name="simpleTokens">The SimpleToken array to wrap.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this ImmutableArray<SimpleToken> simpleTokens)
    {
        return new TokenBuffer(simpleTokens);
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the SimpleToken array with specified options.
    /// </summary>
    /// <param name="simpleTokens">The SimpleToken array to wrap.</param>
    /// <param name="options">The tokenizer options for Level 2 parsing and text injection.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this ImmutableArray<SimpleToken> simpleTokens, TokenizerOptions options)
    {
        return new TokenBuffer(simpleTokens, options);
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from SimpleToken enumerable.
    /// </summary>
    /// <param name="simpleTokens">The SimpleTokens to wrap.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this IEnumerable<SimpleToken> simpleTokens)
    {
        return new TokenBuffer(simpleTokens.ToImmutableArray());
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from SimpleToken enumerable with specified options.
    /// </summary>
    /// <param name="simpleTokens">The SimpleTokens to wrap.</param>
    /// <param name="options">The tokenizer options for Level 2 parsing and text injection.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this IEnumerable<SimpleToken> simpleTokens, TokenizerOptions options)
    {
        return new TokenBuffer(simpleTokens.ToImmutableArray(), options);
    }

    #endregion

    #region From Level 2 Tokens (Convenience)

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the Level 2 token array.
    /// Note: This re-lexes the content to obtain SimpleTokens.
    /// </summary>
    /// <param name="tokens">The Level 2 token array to convert.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this ImmutableArray<Token> tokens)
    {
        return new TokenBuffer(tokens);
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the Level 2 token array with specified options.
    /// Note: This re-lexes the content to obtain SimpleTokens.
    /// </summary>
    /// <param name="tokens">The Level 2 token array to convert.</param>
    /// <param name="options">The tokenizer options for Level 2 parsing and text injection.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this ImmutableArray<Token> tokens, TokenizerOptions options)
    {
        return new TokenBuffer(tokens, options);
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the Level 2 token enumerable.
    /// Note: This re-lexes the content to obtain SimpleTokens.
    /// </summary>
    /// <param name="tokens">The Level 2 tokens to convert.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this IEnumerable<Token> tokens)
    {
        return new TokenBuffer(tokens.ToImmutableArray());
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the Level 2 token enumerable with specified options.
    /// Note: This re-lexes the content to obtain SimpleTokens.
    /// </summary>
    /// <param name="tokens">The Level 2 tokens to convert.</param>
    /// <param name="options">The tokenizer options for Level 2 parsing and text injection.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this IEnumerable<Token> tokens, TokenizerOptions options)
    {
        return new TokenBuffer(tokens.ToImmutableArray(), options);
    }

    #endregion

    #region From String (Most Convenient)

    /// <summary>
    /// Lexes the input string and creates a new <see cref="TokenBuffer"/>.
    /// This is the most convenient way to create a buffer from source text.
    /// </summary>
    /// <param name="input">The input string to lex.</param>
    /// <param name="options">Optional tokenizer options.</param>
    /// <returns>A new token buffer containing the lexed SimpleTokens.</returns>
    public static TokenBuffer ToTokenBuffer(this string input, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var simpleTokens = lexer.LexToArray(input);
        return new TokenBuffer(simpleTokens, opts);
    }

    /// <summary>
    /// Lexes the input memory and creates a new <see cref="TokenBuffer"/>.
    /// </summary>
    /// <param name="input">The input memory to lex.</param>
    /// <param name="options">Optional tokenizer options.</param>
    /// <returns>A new token buffer containing the lexed SimpleTokens.</returns>
    public static TokenBuffer ToTokenBuffer(this ReadOnlyMemory<char> input, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var simpleTokens = lexer.LexToArray(input);
        return new TokenBuffer(simpleTokens, opts);
    }

    #endregion
}
