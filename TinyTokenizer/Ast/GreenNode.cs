using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for immutable, position-independent syntax nodes (green layer).
/// Green nodes store width (not absolute position) and can be freely shared
/// across different tree versions for structural sharing during mutations.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal abstract record GreenNode : IFormattable
{
    /// <summary>
    /// Gets the debugger display string for this node.
    /// Override in derived classes for specialized display.
    /// </summary>
    protected virtual string DebuggerDisplay =>
        SlotCount > 0
            ? $"{Kind}[{Width}] ({SlotCount} children)"
            : $"{Kind}[{Width}]";

    /// <summary>
    /// Truncates a string to the specified length, replacing quotes with single quotes.
    /// </summary>
    protected static string Truncate(string text, int maxLength)
    {
        var escaped = text.Replace('"', '\'');
        if (escaped.Length <= maxLength)
            return escaped;
        return escaped[..maxLength] + "...";
    }

    /// <summary>The kind of this node.</summary>
    public abstract NodeKind Kind { get; }
    
    /// <summary>
    /// Total character width of this node, including any trivia.
    /// For containers, this includes delimiters and all children.
    /// </summary>
    public abstract int Width { get; }
    
    /// <summary>
    /// Number of child slots. Returns 0 for leaf nodes.
    /// </summary>
    public abstract int SlotCount { get; }
    
    /// <summary>
    /// Gets the child at the specified slot index.
    /// </summary>
    /// <param name="index">The slot index.</param>
    /// <returns>The child node, or null if the slot is empty.</returns>
    public abstract GreenNode? GetSlot(int index);
    
    /// <summary>
    /// Creates a red (navigation) wrapper for this green node.
    /// </summary>
    /// <param name="parent">The parent red node, or null for root.</param>
    /// <param name="position">The absolute position in source text.</param>
    /// <returns>A new red node wrapping this green node.</returns>
    public abstract RedNode CreateRed(RedNode? parent, int position);
    
    /// <summary>
    /// Writes the text content of this node to a StringBuilder.
    /// </summary>
    /// <param name="builder">The StringBuilder to write to.</param>
    public abstract void WriteTo(StringBuilder builder);
    
    /// <summary>
    /// Computes the character offset of a child slot from this node's start.
    /// Default implementation is O(index); containers with many children may override for O(1).
    /// </summary>
    /// <param name="index">The slot index.</param>
    /// <returns>The character offset from this node's start position.</returns>
    public virtual int GetSlotOffset(int index)
    {
        int offset = GetLeadingWidth();
        for (int i = 0; i < index; i++)
        {
            var child = GetSlot(i);
            if (child != null)
                offset += child.Width;
        }
        return offset;
    }
    
    /// <summary>
    /// Gets the width of content before the first child slot.
    /// For blocks, this is the opening delimiter width.
    /// </summary>
    protected virtual int GetLeadingWidth() => 0;
    
    /// <summary>
    /// Whether this node is a container (has children) vs a leaf.
    /// </summary>
    public bool IsContainer => this is GreenContainer;
    
    /// <summary>
    /// Whether this node is a leaf (no children).
    /// </summary>
    public bool IsLeaf => !IsContainer;
    
    #region IFormattable
    
    /// <summary>
    /// Formats this node using the specified format string.
    /// </summary>
    /// <param name="format">
    /// Format string:
    /// - null, "", or "G": Full text content (serialized form)
    /// - "K": Kind only (e.g., "Ident")
    /// - "W": Width only (e.g., "5")
    /// - "D": Debug info (e.g., "Ident[5]")
    /// - "T": Type name (e.g., "GreenLeaf")
    /// </param>
    /// <param name="formatProvider">Format provider (unused).</param>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format switch
        {
            null or "" or "G" => ToText(),
            "K" => Kind.ToString(),
            "W" => Width.ToString(),
            "D" => SlotCount > 0 
                ? $"{Kind}[{Width}] ({SlotCount} children)" 
                : $"{Kind}[{Width}]",
            "T" => GetType().Name,
            _ => ToText()
        };
    }
    
    /// <summary>
    /// Returns the text content of this node.
    /// </summary>
    public string ToText()
    {
        var sb = new StringBuilder(Width);
        WriteTo(sb);
        return sb.ToString();
    }
    
    /// <summary>
    /// Returns the text content of this node.
    /// </summary>
    public override string ToString() => ToText();
    
    #endregion
}
