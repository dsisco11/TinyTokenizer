using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// An async tokenizer that streams tokens from a <see cref="PipeReader"/> using
/// <see cref="IAsyncEnumerable{T}"/> for on-demand parsing without pre-loading all tokens.
/// Uses the two-level architecture: Lexer (Level 1) → TokenParser (Level 2).
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
    /// Streams tokens asynchronously from the input.
    /// Uses the two-level Lexer → TokenParser architecture for clean streaming.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of tokens.</returns>
    public IAsyncEnumerable<Token> TokenizeAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Create an async enumerable of memory chunks from the reader
        var chunks = ReadChunksAsync(cancellationToken);

        // Level 1: Lexer produces SimpleTokens from chunks
        var simpleTokens = _lexer.LexAsync(chunks, cancellationToken);

        // Level 2: TokenParser produces semantic Tokens from SimpleTokens
        return _parser.ParseAsync(simpleTokens, cancellationToken);
    }

    /// <summary>
    /// Reads character chunks from the underlying pipe reader.
    /// </summary>
    private async IAsyncEnumerable<ReadOnlyMemory<char>> ReadChunksAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readResult = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (readResult.IsCanceled)
            {
                yield break;
            }

            var buffer = readResult.Buffer;
            bool isCompleted = readResult.IsCompleted;

            if (!buffer.IsEmpty)
            {
                // Copy to a new array since buffer may be reused
                var chars = new char[buffer.Length];
                for (int i = 0; i < buffer.Length; i++)
                {
                    chars[i] = buffer.Span[i];
                }
                
                _reader.AdvanceTo((int)buffer.Length);
                
                yield return chars.AsMemory();
            }

            if (isCompleted)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _reader.DisposeAsync().ConfigureAwait(false);
        }
    }
}
