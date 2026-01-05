using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for navigation wrappers around green nodes (red layer).
/// Red nodes are ephemeral - created on demand and discarded after mutations.
/// They provide parent links and computed absolute positions.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class RedNode : IFormattable, ITextSerializable
{
    private readonly GreenNode _green;
    private readonly RedNode? _parent;
    private readonly int _position;
    private readonly int _siblingIndex;

    /// <summary>
    /// Gets the debugger display string for this node.
    /// Override in derived classes for specialized display.
    /// </summary>
    protected virtual string DebuggerDisplay =>
        SlotCount > 0
            ? $"{Kind}[{_position}..{EndPosition}] ({SlotCount} children)"
            : $"{Kind}[{_position}..{EndPosition}]";

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

    
    /// <summary>
    /// Creates a new red node wrapping a green node.
    /// Internal constructor for use by built-in red nodes (RedLeaf, RedBlock, RedList).
    /// </summary>
    /// <param name="green">The underlying green node.</param>
    /// <param name="parent">The parent red node, or null if this is the root.</param>
    /// <param name="position">The absolute position in source text.</param>
    /// <param name="siblingIndex">The index of this node within its parent's children, or -1 if root.</param>
    private protected RedNode(GreenNode green, RedNode? parent, int position, int siblingIndex = -1)
    {
        _green = green;
        _parent = parent;
        _position = position;
        _siblingIndex = siblingIndex;
    }
    
    /// <summary>
    /// Creates a new red node from a creation context.
    /// Protected constructor for use by user-defined SyntaxNode subclasses.
    /// </summary>
    /// <param name="context">The creation context containing green node and position info.</param>
    protected RedNode(CreationContext context)
        : this(context.Green, context.Parent, context.Position, context.SiblingIndex)
    {
    }
    
    /// <summary>The underlying green node containing the actual data.</summary>
    internal GreenNode Green => _green;
    
    /// <summary>The parent red node, or null if this is the root.</summary>
    public RedNode? Parent => _parent;
    
    /// <summary>Absolute position (character offset) in source text.</summary>
    public int Position => _position;
    
    /// <summary>Total character width of this node (delegated to green).</summary>
    public int Width => _green.Width;
    
    /// <summary>End position (exclusive) in source text.</summary>
    public int EndPosition => _position + _green.Width;
    
    /// <summary>The kind of this node (delegated to green).</summary>
    public NodeKind Kind => _green.Kind;
    
    /// <summary>Number of child slots (delegated to green).</summary>
    public int SlotCount => _green.SlotCount;
    
    /// <summary>Whether this is a container node.</summary>
    public bool IsContainer => _green.IsContainer;
    
    /// <summary>Whether this is a leaf node.</summary>
    public bool IsLeaf => _green.IsLeaf;
    
    /// <summary>
    /// Gets the child at the specified slot index.
    /// Children are created lazily and cached.
    /// </summary>
    public abstract RedNode? GetChild(int index);
    
    /// <summary>
    /// Lazy child creation with thread-safe caching.
    /// </summary>
    protected T? GetRedChild<T>(ref T? field, int slot) where T : RedNode
    {
        if (field != null)
            return field;
        
        var greenChild = _green.GetSlot(slot);
        if (greenChild == null)
            return null;
        
        var childPosition = _position + _green.GetSlotOffset(slot);
        var redChild = (T)greenChild.CreateRed(this, childPosition, slot);
        
        Interlocked.CompareExchange(ref field, redChild, null);
        return field;
    }
    
    /// <summary>
    /// Gets a child with type checking, throwing if the child is null or wrong type.
    /// Used by derived SyntaxNode classes for typed child access.
    /// </summary>
    /// <typeparam name="T">The expected child type.</typeparam>
    /// <param name="index">The slot index.</param>
    /// <returns>The typed child node.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the child is null or wrong type.</exception>
    protected T GetTypedChild<T>(int index) where T : RedNode
    {
        var child = GetChild(index);
        if (child is not T typed)
        {
            throw new InvalidOperationException(
                $"Expected child at slot {index} to be {typeof(T).Name}, " +
                $"but got {child?.GetType().Name ?? "null"}.");
        }
        return typed;
    }
    
    /// <summary>
    /// Tries to get a child with type checking, returning null if child is null or wrong type.
    /// Used by derived SyntaxNode classes for optional typed child access.
    /// </summary>
    /// <typeparam name="T">The expected child type.</typeparam>
    /// <param name="index">The slot index.</param>
    /// <returns>The typed child node, or null if not found or wrong type.</returns>
    protected T? TryGetTypedChild<T>(int index) where T : RedNode =>
        GetChild(index) as T;
    
    /// <summary>
    /// Enumerates all children of this node.
    /// </summary>
    public IEnumerable<RedNode> Children
    {
        get
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var child = GetChild(i);
                if (child != null)
                    yield return child;
            }
        }
    }
    
    /// <summary>
    /// Gets the root node.
    /// </summary>
    public RedNode Root
    {
        get
        {
            var current = this;
            while (current._parent != null)
                current = current._parent;
            return current;
        }
    }
    
    /// <summary>
    /// Finds the deepest node containing the specified position.
    /// </summary>
    public RedNode? FindNodeAt(int position)
    {
        if (position < _position || position >= EndPosition)
            return null;
        
        foreach (var child in Children)
        {
            var found = child.FindNodeAt(position);
            if (found != null)
                return found;
        }
        
        return this;
    }
    
    /// <summary>
    /// Finds all leaf nodes containing the specified position.
    /// </summary>
    public RedNode? FindLeafAt(int position)
    {
        var node = FindNodeAt(position);
        while (node != null && !node.IsLeaf)
        {
            RedNode? deeperChild = null;
            foreach (var child in node.Children)
            {
                if (position >= child.Position && position < child.EndPosition)
                {
                    deeperChild = child;
                    break;
                }
            }
            if (deeperChild == null)
                break;
            node = deeperChild;
        }
        return node;
    }
    
    #region Sibling Navigation
    
    /// <summary>
    /// Gets the index of this node within its parent's children, or -1 if root.
    /// </summary>
    public int SiblingIndex => _siblingIndex;
    
    /// <summary>
    /// Gets the next sibling node, or null if this is the last child.
    /// </summary>
    public RedNode? NextSibling()
    {
        if (_parent == null)
            return null;
        
        var index = SiblingIndex;
        if (index < 0 || index >= _parent.SlotCount - 1)
            return null;
        
        return _parent.GetChild(index + 1);
    }
    
    /// <summary>
    /// Gets the previous sibling node, or null if this is the first child.
    /// </summary>
    public RedNode? PreviousSibling()
    {
        if (_parent == null)
            return null;
        
        var index = SiblingIndex;
        if (index <= 0)
            return null;
        
        return _parent.GetChild(index - 1);
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
    /// - "P": Position only (e.g., "0")
    /// - "R": Range only (e.g., "0..5")
    /// - "D": Debug info (e.g., "Ident[0..5]")
    /// - "T": Type name (e.g., "RedLeaf")
    /// - "S": Structure dump (full subtree with trivia info)
    /// </param>
    /// <param name="formatProvider">Format provider (unused).</param>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format switch
        {
            null or "" or "G" => ToText(),
            "K" => Kind.ToString(),
            "P" => _position.ToString(),
            "R" => $"{_position}..{EndPosition}",
            "D" => SlotCount > 0 
                ? $"{Kind}[{_position}..{EndPosition}] ({SlotCount} children)" 
                : $"{Kind}[{_position}..{EndPosition}]",
            "T" => GetType().Name,
            "S" => DumpStructure(0),
            _ => ToText()
        };
    }
    
    /// <summary>
    /// Dumps the subtree structure for debugging, including trivia information.
    /// </summary>
    private string DumpStructure(int indent)
    {
        var sb = new StringBuilder();
        var prefix = new string(' ', indent * 2);
        
        // Show trivia info for leaves
        string triviaInfo = "";
        string textContent = "";
        
        if (this is RedLeaf leaf)
        {
            textContent = leaf.Text.Replace("\n", "\\n").Replace("\r", "\\r");
            var leadingNewlines = leaf.Green is GreenLeaf gl 
                ? gl.LeadingTrivia.Count(t => t.Kind == TriviaKind.Newline) 
                : 0;
            var trailingNewlines = leaf.Green is GreenLeaf gl2 
                ? gl2.TrailingTrivia.Count(t => t.Kind == TriviaKind.Newline) 
                : 0;
            if (leadingNewlines > 0 || trailingNewlines > 0)
                triviaInfo = $" [lead:{leadingNewlines}NL, trail:{trailingNewlines}NL]";
        }
        else
        {
            textContent = ToText().Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        if (textContent.Length > 40)
            textContent = textContent[..40] + "...";
        
        // Format: Kind[width] (children) [trivia]: "text"
        var nodeInfo = SlotCount > 0
            ? $"{Kind}[{Width}] ({SlotCount} children)"
            : $"{Kind}[{Width}]";
        
        sb.AppendLine($"{prefix}{nodeInfo}{triviaInfo}: \"{textContent}\"");
        
        foreach (var child in Children)
        {
            if (child is RedNode redChild)
                sb.Append(redChild.DumpStructure(indent + 1));
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Writes the text content to an <see cref="IBufferWriter{T}"/>.
    /// Delegates to the underlying green node.
    /// </summary>
    public void WriteTo(IBufferWriter<char> writer) => _green.WriteTo(writer);

    /// <summary>
    /// Returns the text content of this node.
    /// </summary>
    public string ToText() => _green.ToText();

    /// <summary>
    /// Writes the text content of this node to a StringBuilder.
    /// Delegates to the underlying green node.
    /// </summary>
    public void WriteTo(StringBuilder builder) => _green.WriteTo(builder);

    /// <inheritdoc />
    public void WriteTo(TextWriter writer) => _green.WriteTo(writer);

    /// <inheritdoc />
    public bool TryWriteTo(Span<char> destination, out int charsWritten) => _green.TryWriteTo(destination, out charsWritten);

    /// <inheritdoc />
    public int TextLength => _green.TextLength;
    
    /// <summary>
    /// Returns a debug representation of this node.
    /// Use <see cref="ToText"/> to get the serialized text content.
    /// </summary>
    public override string ToString() => ToString("D", null);
    
    #endregion
}
