using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// A wrapper around <see cref="PipeReader"/> that decodes UTF-8 bytes to characters.
/// Handles multi-byte character sequences that may span buffer boundaries.
/// </summary>
public sealed class DecodingPipeReader : IAsyncDisposable
{
    private readonly PipeReader _pipeReader;
    private readonly Decoder _decoder;
    private readonly bool _leaveOpen;
    private readonly int _minBufferSize;
    
    // Buffer for decoded characters
    private char[]? _charBuffer;
    private int _charBufferLength;
    private int _charBufferConsumed;
    
    // Tracking for incomplete multi-byte sequences
    private readonly byte[] _incompleteBytes = new byte[4]; // Max UTF-8 sequence length
    private int _incompleteBytesCount;
    
    /// <summary>
    /// Gets whether the underlying pipe has completed (no more data).
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="DecodingPipeReader"/>.
    /// </summary>
    /// <param name="pipeReader">The underlying pipe reader providing bytes.</param>
    /// <param name="encoding">The encoding to use for decoding. Defaults to UTF-8.</param>
    /// <param name="leaveOpen">Whether to leave the pipe reader open when disposed.</param>
    /// <param name="minBufferSize">Minimum buffer size for decoded characters.</param>
    public DecodingPipeReader(
        PipeReader pipeReader,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int minBufferSize = 4096)
    {
        _pipeReader = pipeReader ?? throw new ArgumentNullException(nameof(pipeReader));
        _decoder = (encoding ?? Encoding.UTF8).GetDecoder();
        _leaveOpen = leaveOpen;
        _minBufferSize = minBufferSize;
    }

    /// <summary>
    /// Creates a <see cref="DecodingPipeReader"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="encoding">The encoding to use for decoding. Defaults to UTF-8.</param>
    /// <param name="leaveOpen">Whether to leave the stream open when disposed.</param>
    /// <param name="bufferSize">Buffer size for reading from the stream.</param>
    /// <returns>A new <see cref="DecodingPipeReader"/>.</returns>
    public static DecodingPipeReader Create(
        Stream stream,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int bufferSize = 4096)
    {
        var pipeReader = PipeReader.Create(stream, new StreamPipeReaderOptions(
            leaveOpen: leaveOpen,
            bufferSize: bufferSize));
        
        return new DecodingPipeReader(pipeReader, encoding, leaveOpen: false);
    }

    /// <summary>
    /// Reads decoded characters from the pipe.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing decoded characters and completion status.</returns>
    public async ValueTask<DecodingReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        // If we have buffered characters remaining, return those first
        if (_charBufferConsumed < _charBufferLength)
        {
            return new DecodingReadResult(
                new ReadOnlyMemory<char>(_charBuffer, _charBufferConsumed, _charBufferLength - _charBufferConsumed),
                IsCompleted,
                isCanceled: false);
        }

        // Read more bytes from the pipe
        ReadResult readResult = await _pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);

        if (readResult.IsCanceled)
        {
            return new DecodingReadResult(ReadOnlyMemory<char>.Empty, isCompleted: false, isCanceled: true);
        }

        ReadOnlySequence<byte> buffer = readResult.Buffer;
        
        if (buffer.IsEmpty && readResult.IsCompleted)
        {
            IsCompleted = true;
            
            // Flush any remaining incomplete bytes
            if (_incompleteBytesCount > 0)
            {
                // Try to decode incomplete sequence - may produce replacement character
                EnsureCharBuffer(4);
                int charsWritten = _decoder.GetChars(
                    _incompleteBytes.AsSpan(0, _incompleteBytesCount),
                    _charBuffer.AsSpan(),
                    flush: true);
                _incompleteBytesCount = 0;
                
                if (charsWritten > 0)
                {
                    _charBufferLength = charsWritten;
                    _charBufferConsumed = 0;
                    return new DecodingReadResult(
                        new ReadOnlyMemory<char>(_charBuffer, 0, charsWritten),
                        isCompleted: true,
                        isCanceled: false);
                }
            }
            
            return new DecodingReadResult(ReadOnlyMemory<char>.Empty, isCompleted: true, isCanceled: false);
        }

        // Decode the bytes
        int totalBytes = (int)Math.Min(buffer.Length, int.MaxValue);
        int estimatedChars = Encoding.UTF8.GetMaxCharCount(totalBytes + _incompleteBytesCount);
        EnsureCharBuffer(estimatedChars);

        int bytesConsumed = 0;
        int charsProduced = 0;

        // First, handle any incomplete bytes from previous read
        if (_incompleteBytesCount > 0)
        {
            // Try to complete the sequence with new bytes
            int bytesNeeded = GetBytesNeededForSequence(_incompleteBytes[0]) - _incompleteBytesCount;
            int bytesToCopy = Math.Min(bytesNeeded, totalBytes);
            
            if (bytesToCopy > 0)
            {
                CopyFromSequence(buffer, 0, _incompleteBytes.AsSpan(_incompleteBytesCount, bytesToCopy));
                _incompleteBytesCount += bytesToCopy;
                bytesConsumed += bytesToCopy;
                
                // Try to decode the complete sequence
                charsProduced = _decoder.GetChars(
                    _incompleteBytes.AsSpan(0, _incompleteBytesCount),
                    _charBuffer.AsSpan(),
                    flush: false);
                _incompleteBytesCount = 0;
            }
        }

        // Decode remaining bytes
        if (bytesConsumed < totalBytes)
        {
            var remainingBuffer = buffer.Slice(bytesConsumed);
            
            foreach (var segment in remainingBuffer)
            {
                // Check for incomplete sequence at the end
                int incompleteStart = FindIncompleteSequenceStart(segment.Span);
                
                if (incompleteStart >= 0 && incompleteStart < segment.Length)
                {
                    // Decode complete portion
                    if (incompleteStart > 0)
                    {
                        charsProduced += _decoder.GetChars(
                            segment.Span[..incompleteStart],
                            _charBuffer.AsSpan(charsProduced),
                            flush: false);
                        bytesConsumed += incompleteStart;
                    }
                    
                    // Save incomplete bytes for next read
                    int incompleteLength = segment.Length - incompleteStart;
                    segment.Span[incompleteStart..].CopyTo(_incompleteBytes);
                    _incompleteBytesCount = incompleteLength;
                    bytesConsumed += incompleteLength;
                }
                else
                {
                    // Decode entire segment
                    charsProduced += _decoder.GetChars(
                        segment.Span,
                        _charBuffer.AsSpan(charsProduced),
                        flush: false);
                    bytesConsumed += segment.Length;
                }
            }
        }

        // Advance the pipe reader
        _pipeReader.AdvanceTo(buffer.GetPosition(bytesConsumed), buffer.End);
        
        IsCompleted = readResult.IsCompleted && _incompleteBytesCount == 0;
        
        _charBufferLength = charsProduced;
        _charBufferConsumed = 0;

        return new DecodingReadResult(
            new ReadOnlyMemory<char>(_charBuffer, 0, charsProduced),
            IsCompleted,
            isCanceled: false);
    }

    /// <summary>
    /// Advances the reader past the consumed characters.
    /// </summary>
    /// <param name="consumed">Number of characters consumed.</param>
    public void AdvanceTo(int consumed)
    {
        _charBufferConsumed += consumed;
        
        if (_charBufferConsumed >= _charBufferLength)
        {
            _charBufferConsumed = 0;
            _charBufferLength = 0;
        }
    }

    /// <summary>
    /// Cancels pending read operations.
    /// </summary>
    public void CancelPendingRead()
    {
        _pipeReader.CancelPendingRead();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_leaveOpen)
        {
            await _pipeReader.CompleteAsync().ConfigureAwait(false);
        }
        
        if (_charBuffer != null)
        {
            ArrayPool<char>.Shared.Return(_charBuffer);
            _charBuffer = null;
        }
    }

    #region Private Helpers

    private void EnsureCharBuffer(int minSize)
    {
        if (_charBuffer == null || _charBuffer.Length < minSize)
        {
            if (_charBuffer != null)
            {
                ArrayPool<char>.Shared.Return(_charBuffer);
            }
            _charBuffer = ArrayPool<char>.Shared.Rent(Math.Max(minSize, _minBufferSize));
        }
    }

    private static int GetBytesNeededForSequence(byte firstByte)
    {
        if ((firstByte & 0x80) == 0) return 1;      // 0xxxxxxx - ASCII
        if ((firstByte & 0xE0) == 0xC0) return 2;   // 110xxxxx
        if ((firstByte & 0xF0) == 0xE0) return 3;   // 1110xxxx
        if ((firstByte & 0xF8) == 0xF0) return 4;   // 11110xxx
        return 1; // Invalid, treat as single byte
    }

    private static int FindIncompleteSequenceStart(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty) return -1;

        // Check last 1-3 bytes for incomplete sequence
        for (int i = Math.Max(0, span.Length - 3); i < span.Length; i++)
        {
            byte b = span[i];
            int needed = GetBytesNeededForSequence(b);
            int available = span.Length - i;
            
            if (needed > available)
            {
                return i;
            }
        }

        return -1;
    }

    private static void CopyFromSequence(ReadOnlySequence<byte> sequence, long offset, Span<byte> destination)
    {
        var sliced = sequence.Slice(offset, destination.Length);
        sliced.CopyTo(destination);
    }

    #endregion
}

/// <summary>
/// Result from a <see cref="DecodingPipeReader.ReadAsync"/> operation.
/// </summary>
public readonly struct DecodingReadResult
{
    /// <summary>
    /// Gets the decoded characters.
    /// </summary>
    public ReadOnlyMemory<char> Buffer { get; }

    /// <summary>
    /// Gets whether the pipe has completed (no more data).
    /// </summary>
    public bool IsCompleted { get; }

    /// <summary>
    /// Gets whether the read was canceled.
    /// </summary>
    public bool IsCanceled { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DecodingReadResult"/>.
    /// </summary>
    public DecodingReadResult(ReadOnlyMemory<char> buffer, bool isCompleted, bool isCanceled)
    {
        Buffer = buffer;
        IsCompleted = isCompleted;
        IsCanceled = isCanceled;
    }
}
