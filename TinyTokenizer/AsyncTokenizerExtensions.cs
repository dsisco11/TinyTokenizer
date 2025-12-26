using System.Buffers;
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
    /// <returns>An async enumerable of tokens.</returns>
    public static async IAsyncEnumerable<Token> TokenizeAsync(
        this PipeReader pipeReader,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var tokenizer = new AsyncPipeTokenizer(pipeReader, options, encoding);
        
        await foreach (var token in tokenizer.TokenizeAsync(cancellationToken).ConfigureAwait(false))
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
    /// <returns>An async enumerable of tokens.</returns>
    public static async IAsyncEnumerable<Token> TokenizeAsync(
        this Stream stream,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var tokenizer = new AsyncPipeTokenizer(stream, options, encoding, leaveOpen);
        
        await foreach (var token in tokenizer.TokenizeAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Tokenizes bytes from a <see cref="Stream"/> and collects all tokens.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding for decoding bytes. Defaults to UTF-8.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after tokenization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all tokens.</returns>
    public static async Task<List<Token>> TokenizeToListAsync(
        this Stream stream,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        var tokens = new List<Token>();
        
        await foreach (var token in stream.TokenizeAsync(options, encoding, leaveOpen, cancellationToken).ConfigureAwait(false))
        {
            tokens.Add(token);
        }
        
        return tokens;
    }

    #endregion

    #region ReadOnlySequence Extensions

    /// <summary>
    /// Tokenizes a <see cref="ReadOnlySequence{T}"/> of characters synchronously.
    /// </summary>
    /// <param name="sequence">The character sequence to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <returns>The parse result containing tokens.</returns>
    public static ParseResult Tokenize(
        this ReadOnlySequence<char> sequence,
        TokenizerOptions? options = null)
    {
        var tokenizer = new SequenceTokenizer(sequence, options);
        return tokenizer.Tokenize();
    }

    /// <summary>
    /// Tokenizes a <see cref="ReadOnlySequence{T}"/> of characters with state for streaming.
    /// </summary>
    /// <param name="sequence">The character sequence to tokenize.</param>
    /// <param name="state">State for resumable parsing across buffer boundaries.</param>
    /// <param name="isCompleted">Whether this is the final buffer.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <returns>The parse result containing tokens.</returns>
    public static ParseResult Tokenize(
        this ReadOnlySequence<char> sequence,
        TokenizerState state,
        bool isCompleted,
        TokenizerOptions? options = null)
    {
        var tokenizer = new SequenceTokenizer(sequence, options, state, isCompleted);
        return tokenizer.Tokenize();
    }

    #endregion

    #region ReadOnlyMemory Extensions

    /// <summary>
    /// Tokenizes a <see cref="ReadOnlyMemory{T}"/> of characters as a sequence.
    /// </summary>
    /// <param name="memory">The character memory to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <returns>The parse result containing tokens.</returns>
    public static ParseResult TokenizeAsSequence(
        this ReadOnlyMemory<char> memory,
        TokenizerOptions? options = null)
    {
        var sequence = new ReadOnlySequence<char>(memory);
        return sequence.Tokenize(options);
    }

    #endregion

    #region String Extensions

    /// <summary>
    /// Tokenizes a string as a sequence.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <returns>The parse result containing tokens.</returns>
    public static ParseResult TokenizeAsSequence(
        this string text,
        TokenizerOptions? options = null)
    {
        var sequence = new ReadOnlySequence<char>(text.AsMemory());
        return sequence.Tokenize(options);
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
        
        await foreach (var token in stream.TokenizeAsync(options, encoding, leaveOpen: true, cancellationToken).ConfigureAwait(false))
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
    /// <returns>An async enumerable of tokens.</returns>
    public static async IAsyncEnumerable<Token> TokenizeAsync(
        this byte[] bytes,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(bytes);
        
        await foreach (var token in stream.TokenizeAsync(options, encoding, leaveOpen: true, cancellationToken).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    #endregion
}
