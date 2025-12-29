using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// Extension methods for async tokenization from various input sources.
/// </summary>
public static class AsyncTokenizerExtensions
{
    #region PipeReader Extensions

    /// <summary>
    /// Tokenizes bytes from a <see cref="PipeReader"/> asynchronously.
    /// </summary>
    /// <param name="pipeReader">The pipe reader providing bytes.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding for decoding bytes. Defaults to UTF-8.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An immutable array of tokens.</returns>
    public static async Task<ImmutableArray<Token>> TokenizeAsync(
        this PipeReader pipeReader,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        await using var tokenizer = new AsyncPipeTokenizer(pipeReader, options, encoding);
        return await tokenizer.TokenizeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tokenizes bytes from a <see cref="PipeReader"/> asynchronously as a stream.
    /// </summary>
    public static async IAsyncEnumerable<Token> TokenizeStreamingAsync(
        this PipeReader pipeReader,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var tokenizer = new AsyncPipeTokenizer(pipeReader, options, encoding);
        await foreach (var token in tokenizer.TokenizeStreamingAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    #endregion

    #region Stream Extensions

    /// <summary>
    /// Tokenizes bytes from a <see cref="Stream"/> asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding for decoding bytes. Defaults to UTF-8.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after tokenization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An immutable array of tokens.</returns>
    public static async Task<ImmutableArray<Token>> TokenizeAsync(
        this Stream stream,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        await using var tokenizer = new AsyncPipeTokenizer(stream, options, encoding, leaveOpen);
        return await tokenizer.TokenizeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tokenizes bytes from a <see cref="Stream"/> asynchronously as a stream.
    /// </summary>
    public static async IAsyncEnumerable<Token> TokenizeStreamingAsync(
        this Stream stream,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var tokenizer = new AsyncPipeTokenizer(stream, options, encoding, leaveOpen);
        await foreach (var token in tokenizer.TokenizeStreamingAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Tokenizes bytes from a <see cref="Stream"/> and collects all tokens into a list.
    /// </summary>
    public static async Task<List<Token>> TokenizeToListAsync(
        this Stream stream,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        var tokens = await stream.TokenizeAsync(options, encoding, leaveOpen, cancellationToken).ConfigureAwait(false);
        return [.. tokens];
    }

    #endregion

    #region ReadOnlyMemory Extensions

    /// <summary>
    /// Tokenizes a <see cref="ReadOnlyMemory{T}"/> of characters using the two-level architecture.
    /// </summary>
    /// <param name="memory">The character memory to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <returns>An immutable array of tokens.</returns>
    public static ImmutableArray<Token> Tokenize(
        this ReadOnlyMemory<char> memory,
        TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);
        
        var simpleTokens = lexer.Lex(memory);
        return parser.ParseToArray(simpleTokens);
    }

    #endregion

    #region String Extensions

    /// <summary>
    /// Tokenizes a string using the two-level architecture.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <returns>An immutable array of tokens.</returns>
    public static ImmutableArray<Token> TokenizeToTokens(
        this string text,
        TokenizerOptions? options = null)
    {
        return text.AsMemory().Tokenize(options);
    }

    /// <summary>
    /// Creates an async token stream from a string (useful for testing).
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding to use. Defaults to UTF-8.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of tokens.</returns>
    public static async IAsyncEnumerable<Token> TokenizeAsStreamAsync(
        this string text,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        encoding ??= Encoding.UTF8;
        var bytes = encoding.GetBytes(text);
        using var stream = new MemoryStream(bytes);
        
        await foreach (var token in stream.TokenizeStreamingAsync(options, encoding, leaveOpen: true, cancellationToken).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    #endregion

    #region Byte Array Extensions

    /// <summary>
    /// Tokenizes a byte array asynchronously.
    /// </summary>
    /// <param name="bytes">The bytes to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding for decoding bytes. Defaults to UTF-8.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An immutable array of tokens.</returns>
    public static async Task<ImmutableArray<Token>> TokenizeAsync(
        this byte[] bytes,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(bytes);
        return await stream.TokenizeAsync(options, encoding, leaveOpen: true, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Async Pattern Matching Extensions

    /// <summary>
    /// Applies pattern matching to an async token stream.
    /// Collects tokens into a buffer for pattern matching, then yields results.
    /// </summary>
    /// <param name="tokens">The input token stream.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of tokens with patterns applied.</returns>
    public static async IAsyncEnumerable<Token> ApplyPatternsAsync(
        this IAsyncEnumerable<Token> tokens,
        IEnumerable<ITokenDefinition> definitions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Collect all tokens first (pattern matching requires lookahead)
        var tokenList = new List<Token>();
        await foreach (var token in tokens.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            tokenList.Add(token);
        }

        var matcher = new PatternMatcher(definitions);
        var result = matcher.Apply([.. tokenList]);

        foreach (var token in result)
        {
            yield return token;
        }
    }

    /// <summary>
    /// Applies pattern matching to an async token stream.
    /// </summary>
    /// <param name="tokens">The input token stream.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <returns>An async enumerable of tokens with patterns applied.</returns>
    public static IAsyncEnumerable<Token> ApplyPatternsAsync(
        this IAsyncEnumerable<Token> tokens,
        params ITokenDefinition[] definitions)
    {
        return ApplyPatternsAsync(tokens, definitions.AsEnumerable(), CancellationToken.None);
    }

    /// <summary>
    /// Applies pattern matching to an async token stream and returns a diagnostic report.
    /// </summary>
    /// <param name="tokens">The input token stream.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A report containing the output tokens and diagnostic information.</returns>
    public static async Task<PatternMatchReport> ApplyPatternsWithDiagnosticsAsync(
        this IAsyncEnumerable<Token> tokens,
        IEnumerable<ITokenDefinition> definitions,
        CancellationToken cancellationToken = default)
    {
        var tokenList = new List<Token>();
        await foreach (var token in tokens.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            tokenList.Add(token);
        }

        var matcher = new PatternMatcher(definitions, enableDiagnostics: true);
        return matcher.ApplyWithDiagnostics([.. tokenList]);
    }

    /// <summary>
    /// Applies pattern matching to an async token stream and returns a diagnostic report.
    /// </summary>
    /// <param name="tokens">The input token stream.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <returns>A report containing the output tokens and diagnostic information.</returns>
    public static Task<PatternMatchReport> ApplyPatternsWithDiagnosticsAsync(
        this IAsyncEnumerable<Token> tokens,
        params ITokenDefinition[] definitions)
    {
        return ApplyPatternsWithDiagnosticsAsync(tokens, definitions.AsEnumerable(), CancellationToken.None);
    }

    #endregion
}
