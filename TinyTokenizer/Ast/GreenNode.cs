using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for immutable, position-independent syntax nodes (green layer).
/// Green nodes store width (not absolute position) and can be freely shared
/// across different tree versions for structural sharing during mutations.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal abstract record GreenNode : IFormattable, ITextSerializable
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
    /// <param name="siblingIndex">The index of this node within its parent's children, or -1 if root.</param>
    /// <param name="tree">The containing syntax tree.</param>
    /// <returns>A new red node wrapping this green node.</returns>
    public abstract SyntaxNode CreateRed(SyntaxNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null);
    
    #region ITextSerializable
    
    /// <summary>
    /// Writes the text content of this node to an <see cref="IBufferWriter{T}"/>.
    /// This is the core serialization method that derived classes must implement.
    /// </summary>
    /// <param name="writer">The buffer writer to write to.</param>
    public abstract void WriteTo(IBufferWriter<char> writer);

    /// <summary>
    /// Writes the text content of this node to a StringBuilder.
    /// </summary>
    /// <param name="builder">The StringBuilder to write to.</param>
    public void WriteTo(StringBuilder builder)
    {
        if (Width == 0) return;
        var buffer = new ArrayBufferWriter<char>(Width);
        WriteTo(buffer);
        builder.Append(buffer.WrittenSpan);
    }
    
    /// <summary>
    /// Returns the text content of this node.
    /// </summary>
    public string ToText()
    {
        if (Width == 0) return string.Empty;
        var buffer = new ArrayBufferWriter<char>(Width);
        WriteTo(buffer);
        return new string(buffer.WrittenSpan);
    }

    /// <summary>
    /// Writes the text content to a <see cref="TextWriter"/>.
    /// </summary>
    public void WriteTo(TextWriter writer)
    {
        if (Width == 0) return;
        var buffer = new ArrayBufferWriter<char>(Width);
        WriteTo(buffer);
        writer.Write(buffer.WrittenSpan);
    }

    /// <summary>
    /// Tries to write the text content into the provided span.
    /// </summary>
    public bool TryWriteTo(Span<char> destination, out int charsWritten)
    {
        if (Width == 0)
        {
            charsWritten = 0;
            return true;
        }
        
        if (Width > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        
        var buffer = new ArrayBufferWriter<char>(Width);
        WriteTo(buffer);
        buffer.WrittenSpan.CopyTo(destination);
        charsWritten = buffer.WrittenCount;
        return true;
    }

    /// <inheritdoc />
    public int TextLength => Width;
    
    #endregion
    
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
    
    #region Trivia Access
    
    /// <summary>
    /// Gets the leading trivia of this node by finding the first leaf descendant.
    /// For leaves, returns the leaf's leading trivia directly.
    /// </summary>
    public ImmutableArray<GreenTrivia> GetLeadingTrivia()
    {
        var firstLeaf = GetFirstLeaf();
        return firstLeaf?.LeadingTrivia ?? ImmutableArray<GreenTrivia>.Empty;
    }
    
    /// <summary>
    /// Gets the trailing trivia of this node by finding the last leaf descendant.
    /// For leaves, returns the leaf's trailing trivia directly.
    /// </summary>
    public ImmutableArray<GreenTrivia> GetTrailingTrivia()
    {
        var lastLeaf = GetLastLeaf();
        return lastLeaf?.TrailingTrivia ?? ImmutableArray<GreenTrivia>.Empty;
    }
    
    /// <summary>
    /// Gets the width of leading trivia (precomputed for leaves).
    /// </summary>
    public int GetLeadingTriviaWidth()
    {
        var firstLeaf = GetFirstLeaf();
        return firstLeaf?.LeadingTriviaWidth ?? 0;
    }
    
    /// <summary>
    /// Gets the width of trailing trivia (precomputed for leaves).
    /// </summary>
    public int GetTrailingTriviaWidth()
    {
        var lastLeaf = GetLastLeaf();
        return lastLeaf?.TrailingTriviaWidth ?? 0;
    }
    
    /// <summary>
    /// Gets the first leaf descendant of this node.
    /// For leaves, returns self. For containers, descends to find leftmost leaf.
    /// </summary>
    internal GreenLeaf? GetFirstLeaf()
    {
        if (this is GreenLeaf leaf)
            return leaf;
        
        for (int i = 0; i < SlotCount; i++)
        {
            var child = GetSlot(i);
            if (child != null)
            {
                var firstLeaf = child.GetFirstLeaf();
                if (firstLeaf != null)
                    return firstLeaf;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the last leaf descendant of this node.
    /// For leaves, returns self. For containers, descends to find rightmost leaf.
    /// </summary>
    internal GreenLeaf? GetLastLeaf()
    {
        if (this is GreenLeaf leaf)
            return leaf;
        
        for (int i = SlotCount - 1; i >= 0; i--)
        {
            var child = GetSlot(i);
            if (child != null)
            {
                var lastLeaf = child.GetLastLeaf();
                if (lastLeaf != null)
                    return lastLeaf;
            }
        }
        
        return null;
    }
    
    #endregion
    
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
    
    #endregion
    
    /// <summary>
    /// Returns a debug representation of this node.
    /// Use <see cref="ToText"/> to get the serialized text content.
    /// </summary>
    public override string ToString() => ToString("D", null);
}
