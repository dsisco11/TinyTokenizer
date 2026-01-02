using System.Text;

namespace TinyTokenizer;

/// <summary>
/// Represents an object that can serialize its content back to the original text form.
/// Implement this interface to provide text serialization for tokens, nodes, and trees.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="ToText"/> to get the serialized text as a string.
/// Use <see cref="WriteTo"/> when building larger strings to avoid intermediate allocations.
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
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to write to.</param>
    void WriteTo(StringBuilder builder);

    /// <summary>
    /// Returns the serialized text content.
    /// </summary>
    /// <returns>The text representation of this object.</returns>
    string ToText();
}
