using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// An async tokenizer that reads from a <see cref="PipeReader"/> and produces tokens.
/// Buffers the entire input, then uses the sync Lexer + TokenParser pipeline.
/// </summary>
public sealed class AsyncPipeTokenizer : IAsyncDisposable
{
    private readonly DecodingPipeReader _reader;
    private readonly Lexer _lexer;
    private readonly TokenParser _parser;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncPipeTokenizer"/>.
    /// </summary>
    /// <param name="pipeReader">The pipe reader providing bytes.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding for decoding bytes. Defaults to UTF-8.</param>
    public AsyncPipeTokenizer(
        PipeReader pipeReader,
        TokenizerOptions? options = null,
        Encoding? encoding = null)
    {
        _reader = new DecodingPipeReader(pipeReader, encoding, leaveOpen: false);
        var opts = options ?? TokenizerOptions.Default;
        _lexer = new Lexer(opts);
        _parser = new TokenParser(opts);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AsyncPipeTokenizer"/> from a stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="options">The tokenizer options. If null, default options are used.</param>
    /// <param name="encoding">The encoding for decoding bytes. Defaults to UTF-8.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposed.</param>
    public AsyncPipeTokenizer(
        Stream stream,
        TokenizerOptions? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false)
    {
        _reader = DecodingPipeReader.Create(stream, encoding, leaveOpen);
        var opts = options ?? TokenizerOptions.Default;
        _lexer = new Lexer(opts);
        _parser = new TokenParser(opts);
    }

    /// <summary>
    /// Tokenizes the input asynchronously and returns all tokens.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An immutable array of tokens.</returns>
    public async Task<ImmutableArray<Token>> TokenizeAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Step 1: Read all input into memory
        var input = await ReadAllAsync(cancellationToken).ConfigureAwait(false);

        // Step 2: Sync lex → parse
        var simpleTokens = _lexer.Lex(input);
        return _parser.ParseToArray(simpleTokens);
    }

    /// <summary>
    /// Tokenizes the input asynchronously and yields tokens as they are parsed.
    /// Note: This still buffers all input before parsing, but yields incrementally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An enumerable of tokens.</returns>
    public async IAsyncEnumerable<Token> TokenizeStreamingAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Step 1: Read all input into memory
        var input = await ReadAllAsync(cancellationToken).ConfigureAwait(false);

        // Step 2: Sync lex → parse, yielding each token
        var simpleTokens = _lexer.Lex(input);
        foreach (var token in _parser.Parse(simpleTokens))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Reads all characters from the underlying reader into memory.
    /// </summary>
    private async Task<ReadOnlyMemory<char>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var buffer = new List<char>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readResult = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (readResult.IsCanceled)
                break;

            var charBuffer = readResult.Buffer;
            if (!charBuffer.IsEmpty)
            {
                for (int i = 0; i < charBuffer.Length; i++)
                {
                    buffer.Add(charBuffer.Span[i]);
                }
                _reader.AdvanceTo((int)charBuffer.Length);
            }

            if (readResult.IsCompleted)
                break;
        }

        return buffer.ToArray().AsMemory();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _reader.DisposeAsync().ConfigureAwait(false);
    }
}
