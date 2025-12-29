namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for navigation wrappers around green nodes (red layer).
/// Red nodes are ephemeral - created on demand and discarded after mutations.
/// They provide parent links and computed absolute positions.
/// </summary>
public abstract class RedNode
{
    private readonly GreenNode _green;
    private readonly RedNode? _parent;
    private readonly int _position;
    
    /// <summary>
    /// Creates a new red node wrapping a green node.
    /// </summary>
    protected RedNode(GreenNode green, RedNode? parent, int position)
    {
        _green = green;
        _parent = parent;
        _position = position;
    }
    
    /// <summary>The underlying green node containing the actual data.</summary>
    public GreenNode Green => _green;
    
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
        var redChild = (T)greenChild.CreateRed(this, childPosition);
        
        Interlocked.CompareExchange(ref field, redChild, null);
        return field;
    }
    
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
    public int SiblingIndex
    {
        get
        {
            if (_parent == null)
                return -1;
            
            for (int i = 0; i < _parent.SlotCount; i++)
            {
                if (ReferenceEquals(_parent.GetChild(i), this))
                    return i;
            }
            return -1;
        }
    }
    
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
}
