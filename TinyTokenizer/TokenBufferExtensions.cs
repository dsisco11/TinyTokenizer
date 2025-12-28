using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Extension methods for creating <see cref="TokenBuffer"/> instances.
/// </summary>
public static class TokenBufferExtensions
{
    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the token array.
    /// </summary>
    /// <param name="tokens">The token array to wrap.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this ImmutableArray<Token> tokens)
    {
        return new TokenBuffer(tokens);
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the token array with specified options.
    /// </summary>
    /// <param name="tokens">The token array to wrap.</param>
    /// <param name="options">The default tokenizer options for text injection.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this ImmutableArray<Token> tokens, TokenizerOptions options)
    {
        return new TokenBuffer(tokens, options);
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the token enumerable.
    /// </summary>
    /// <param name="tokens">The tokens to wrap.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this IEnumerable<Token> tokens)
    {
        return new TokenBuffer(tokens.ToImmutableArray());
    }

    /// <summary>
    /// Creates a new <see cref="TokenBuffer"/> from the token enumerable with specified options.
    /// </summary>
    /// <param name="tokens">The tokens to wrap.</param>
    /// <param name="options">The default tokenizer options for text injection.</param>
    /// <returns>A new token buffer.</returns>
    public static TokenBuffer ToBuffer(this IEnumerable<Token> tokens, TokenizerOptions options)
    {
        return new TokenBuffer(tokens.ToImmutableArray(), options);
    }

    /// <summary>
    /// Tokenizes the input string and creates a new <see cref="TokenBuffer"/>.
    /// </summary>
    /// <param name="input">The input string to tokenize.</param>
    /// <param name="options">Optional tokenizer options.</param>
    /// <returns>A new token buffer containing the tokenized input.</returns>
    public static TokenBuffer ToTokenBuffer(this string input, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);
        var tokens = parser.ParseToArray(lexer.Lex(input));
        return new TokenBuffer(tokens, opts);
    }

    /// <summary>
    /// Tokenizes the input memory and creates a new <see cref="TokenBuffer"/>.
    /// </summary>
    /// <param name="input">The input memory to tokenize.</param>
    /// <param name="options">Optional tokenizer options.</param>
    /// <returns>A new token buffer containing the tokenized input.</returns>
    public static TokenBuffer ToTokenBuffer(this ReadOnlyMemory<char> input, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);
        var tokens = parser.ParseToArray(lexer.Lex(input));
        return new TokenBuffer(tokens, opts);
    }
}
