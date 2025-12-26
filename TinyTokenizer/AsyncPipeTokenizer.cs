using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// An async tokenizer that streams tokens from a <see cref="PipeReader"/> using
/// <see cref="IAsyncEnumerable{T}"/> for on-demand parsing without pre-loading all tokens.
/// </summary>
public sealed class AsyncPipeTokenizer : IAsyncDisposable
{
    private readonly DecodingPipeReader _reader;
    private readonly TokenizerOptions _options;
    private readonly TokenizerState _state;
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
        _options = options ?? TokenizerOptions.Default;
        _state = new TokenizerState();
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
        _options = options ?? TokenizerOptions.Default;
        _state = new TokenizerState();
    }

    /// <summary>
    /// Streams tokens asynchronously from the input.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of tokens.</returns>
    public async IAsyncEnumerable<Token> TokenizeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Buffer for accumulating partial data
        var accumulatedBuffer = new ArrayBufferWriter<char>();
        
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

            // Combine with any accumulated partial data
            ReadOnlySequence<char> sequenceToProcess;
            if (accumulatedBuffer.WrittenCount > 0)
            {
                // Append new data to accumulated buffer
                if (!buffer.IsEmpty)
                {
                    buffer.Span.CopyTo(accumulatedBuffer.GetSpan(buffer.Span.Length));
                    accumulatedBuffer.Advance(buffer.Span.Length);
                }
                
                sequenceToProcess = new ReadOnlySequence<char>(accumulatedBuffer.WrittenMemory);
            }
            else
            {
                sequenceToProcess = new ReadOnlySequence<char>(buffer);
            }

            if (sequenceToProcess.IsEmpty && isCompleted)
            {
                // Handle any remaining state (unclosed blocks, etc.)
                foreach (var errorToken in FlushRemainingState())
                {
                    yield return errorToken;
                }
                yield break;
            }

            // Parse the current buffer (using helper to avoid ref struct in async method)
            var result = ParseSequence(sequenceToProcess, isCompleted);

            // Yield all parsed tokens
            foreach (var token in result.Tokens)
            {
                yield return token;
            }

            // Handle result status
            switch (result.Status)
            {
                case ParseStatus.Complete:
                    // All done
                    _reader.AdvanceTo((int)buffer.Length);
                    accumulatedBuffer.Clear();
                    yield break;

                case ParseStatus.NeedMoreData:
                    // Save unconsumed data for next iteration
                    var consumedLength = sequenceToProcess.GetOffset(result.Consumed);
                    var remainingLength = sequenceToProcess.Length - consumedLength;
                    
                    if (remainingLength > 0)
                    {
                        var remaining = sequenceToProcess.Slice(result.Consumed);
                        accumulatedBuffer.Clear();
                        remaining.CopyTo(accumulatedBuffer.GetSpan((int)remaining.Length));
                        accumulatedBuffer.Advance((int)remaining.Length);
                    }
                    else
                    {
                        accumulatedBuffer.Clear();
                    }
                    
                    _reader.AdvanceTo((int)buffer.Length);

                    if (isCompleted)
                    {
                        // No more data coming, flush what we have
                        foreach (var errorToken in FlushRemainingState())
                        {
                            yield return errorToken;
                        }
                        yield break;
                    }
                    break;

                case ParseStatus.Continue:
                case ParseStatus.Error:
                    // Continue processing, advance past consumed
                    var consumed = sequenceToProcess.GetOffset(result.Consumed);
                    var leftover = sequenceToProcess.Length - consumed;
                    
                    if (leftover > 0)
                    {
                        var leftoverSlice = sequenceToProcess.Slice(result.Consumed);
                        accumulatedBuffer.Clear();
                        leftoverSlice.CopyTo(accumulatedBuffer.GetSpan((int)leftoverSlice.Length));
                        accumulatedBuffer.Advance((int)leftoverSlice.Length);
                    }
                    else
                    {
                        accumulatedBuffer.Clear();
                    }
                    
                    _reader.AdvanceTo((int)buffer.Length);

                    if (isCompleted)
                    {
                        yield break;
                    }
                    break;
            }

            // Update absolute position
            _state.AbsolutePosition += sequenceToProcess.GetOffset(result.Consumed);
        }
    }

    /// <summary>
    /// Helper method to parse a sequence without ref struct in async context.
    /// </summary>
    private ParseResult ParseSequence(ReadOnlySequence<char> sequence, bool isCompleted)
    {
        var tokenizer = new SequenceTokenizer(sequence, _options, _state, isCompleted);
        return tokenizer.Tokenize();
    }

    /// <summary>
    /// Flushes any remaining state as error tokens.
    /// </summary>
    private IEnumerable<ErrorToken> FlushRemainingState()
    {
        // Handle unclosed blocks
        while (_state.BlockStack.Count > 0)
        {
            var block = _state.BlockStack.Pop();
            yield return new ErrorToken(
                ReadOnlyMemory<char>.Empty,
                $"Unclosed block starting with '{block.OpeningDelimiter}' at position {block.StartPosition}",
                block.StartPosition);
        }

        // Handle partial tokens
        if (_state.Mode != ParseMode.Normal && _state.PartialBuffer.Length > 0)
        {
            var content = _state.PartialBuffer.ToString().AsMemory();
            var message = _state.Mode switch
            {
                ParseMode.InString => $"Unterminated string literal starting at position {_state.TokenStartPosition}",
                ParseMode.InMultiLineComment => $"Unterminated multi-line comment starting at position {_state.TokenStartPosition}",
                _ => $"Incomplete token at position {_state.TokenStartPosition}"
            };

            yield return new ErrorToken(content, message, _state.TokenStartPosition);
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
