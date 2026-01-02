using System.Buffers;
using System.IO;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// Represents an object that can serialize its content back to the original text form.
/// Implement this interface to provide text serialization for tokens, nodes, and trees.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="ToText"/> to get the serialized text as a string.
/// Use the <see cref="WriteTo(StringBuilder)"/> overloads when building larger strings to avoid intermediate allocations.
/// Use <see cref="TryFormat"/> for zero-allocation scenarios with stack-allocated or pooled buffers.
/// </para>
/// <para>
/// Implementers only need to provide <see cref="WriteTo(StringBuilder)"/>; all other methods have default implementations.
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
    /// Writes the text content to a <see cref="StringBuilder"/> for efficient serialization.
    /// This is the core method that implementers must provide.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to write to.</param>
    void WriteTo(StringBuilder builder);

    /// <summary>
    /// Returns the serialized text content as a string.
    /// </summary>
    /// <returns>The text representation of this object.</returns>
    string ToText()
    {
        var sb = new StringBuilder();
        WriteTo(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Writes the text content to a <see cref="TextWriter"/> for streaming output.
    /// Useful for writing directly to files or streams without intermediate string allocation.
    /// </summary>
    /// <param name="writer">The <see cref="TextWriter"/> to write to.</param>
    void WriteTo(TextWriter writer)
    {
        var sb = new StringBuilder();
        WriteTo(sb);
        foreach (var chunk in sb.GetChunks())
        {
            writer.Write(chunk.Span);
        }
    }

    /// <summary>
    /// Writes the text content to an <see cref="IBufferWriter{T}"/> for high-performance scenarios.
    /// Works with <see cref="ArrayBufferWriter{T}"/>, pooled buffers, and pipe writers.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    void WriteTo(IBufferWriter<char> writer)
    {
        var sb = new StringBuilder();
        WriteTo(sb);
        foreach (var chunk in sb.GetChunks())
        {
            var span = writer.GetSpan(chunk.Length);
            chunk.Span.CopyTo(span);
            writer.Advance(chunk.Length);
        }
    }

    /// <summary>
    /// Tries to format the text content into the provided span.
    /// This is the most efficient method for zero-allocation scenarios.
    /// </summary>
    /// <param name="destination">The span to write the text content to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><c>true</c> if the formatting was successful; <c>false</c> if the destination was too small.</returns>
    bool TryFormat(Span<char> destination, out int charsWritten)
    {
        var sb = new StringBuilder();
        WriteTo(sb);
        
        if (sb.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        
        sb.CopyTo(0, destination, sb.Length);
        charsWritten = sb.Length;
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
            var sb = new StringBuilder();
            WriteTo(sb);
            return sb.Length;
        }
    }
}
