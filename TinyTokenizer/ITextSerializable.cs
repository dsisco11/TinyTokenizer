using System.Buffers;
using System.IO;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// Adapter that wraps a <see cref="StringBuilder"/> as an <see cref="IBufferWriter{T}"/>.
/// Used internally to provide default implementations.
/// </summary>
internal sealed class StringBuilderBufferWriter : IBufferWriter<char>
{
    private readonly StringBuilder _builder;

    public StringBuilderBufferWriter(StringBuilder builder) => _builder = builder;

    public void Advance(int count) { } // StringBuilder.Append already advanced

    public Memory<char> GetMemory(int sizeHint = 0) => throw new NotSupportedException("Use GetSpan instead");

    public Span<char> GetSpan(int sizeHint = 0)
    {
        // We can't return a span that StringBuilder will use, so we use a workaround:
        // The caller will copy to this span, then we append to StringBuilder
        throw new NotSupportedException("StringBuilderBufferWriter requires direct append pattern");
    }

    /// <summary>
    /// Appends the span directly to the StringBuilder.
    /// </summary>
    public void Write(ReadOnlySpan<char> span) => _builder.Append(span);
}

/// <summary>
/// Represents an object that can serialize its content back to the original text form.
/// Implement this interface to provide text serialization for tokens, nodes, and trees.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="ToText"/> to get the serialized text as a string.
/// Use the <see cref="WriteTo(IBufferWriter{char})"/> overload for high-performance scenarios with pooled buffers.
/// Use <see cref="TryWriteTo"/> for zero-allocation scenarios with stack-allocated buffers.
/// </para>
/// <para>
/// Implementers only need to provide <see cref="WriteTo(IBufferWriter{char})"/>; all other methods have default implementations.
/// Override specific methods for better performance when the underlying data structure allows it.
/// </para>
/// <para>
/// Note: <see cref="object.ToString"/> should return a debug representation, not serialized text.
/// Always use <see cref="ToText"/> to obtain the original or equivalent text form.
/// </para>
/// </remarks>
public interface ITextSerializable
{
    /// <summary>
    /// Writes the text content to an <see cref="IBufferWriter{T}"/> for high-performance scenarios.
    /// This is the core method that implementers must provide.
    /// Works with <see cref="ArrayBufferWriter{T}"/>, pooled buffers, and pipe writers.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    void WriteTo(IBufferWriter<char> writer);

    /// <summary>
    /// Returns the serialized text content as a string.
    /// </summary>
    /// <returns>The text representation of this object.</returns>
    string ToText()
    {
        var buffer = new ArrayBufferWriter<char>();
        WriteTo(buffer);
        return new string(buffer.WrittenSpan);
    }

    /// <summary>
    /// Writes the text content to a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to write to.</param>
    void WriteTo(StringBuilder builder)
    {
        var buffer = new ArrayBufferWriter<char>();
        WriteTo(buffer);
        builder.Append(buffer.WrittenSpan);
    }

    /// <summary>
    /// Writes the text content to a <see cref="TextWriter"/> for streaming output.
    /// Useful for writing directly to files or streams without intermediate string allocation.
    /// </summary>
    /// <param name="writer">The <see cref="TextWriter"/> to write to.</param>
    void WriteTo(TextWriter writer)
    {
        var buffer = new ArrayBufferWriter<char>();
        WriteTo(buffer);
        writer.Write(buffer.WrittenSpan);
    }

    /// <summary>
    /// Tries to write the text content into the provided span.
    /// This is the most efficient method for zero-allocation scenarios.
    /// </summary>
    /// <param name="destination">The span to write the text content to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><c>true</c> if the write was successful; <c>false</c> if the destination was too small.</returns>
    bool TryWriteTo(Span<char> destination, out int charsWritten)
    {
        var length = TextLength;
        if (length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        
        var buffer = new ArrayBufferWriter<char>(length);
        WriteTo(buffer);
        buffer.WrittenSpan.CopyTo(destination);
        charsWritten = buffer.WrittenCount;
        return true;
    }

    /// <summary>
    /// Gets the length of the serialized text content.
    /// Override for O(1) performance when the length is known without serialization.
    /// </summary>
    int TextLength
    {
        get
        {
            var buffer = new ArrayBufferWriter<char>();
            WriteTo(buffer);
            return buffer.WrittenCount;
        }
    }
}
