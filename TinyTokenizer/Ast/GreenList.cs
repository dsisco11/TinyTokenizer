using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// Green node for the root token list (top-level sequence of nodes).
/// Similar to GreenBlock but without delimiters.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed record GreenList : GreenContainer
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"List[{Width}] ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";

    private readonly ImmutableArray<GreenNode> _children;
    private readonly int _width;
    private readonly int[]? _childOffsets;
    
    /// <inheritdoc/>
    public override NodeKind Kind => NodeKind.TokenList;
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <inheritdoc/>
    public override ImmutableArray<GreenNode> Children => _children;
    
    /// <summary>
    /// Creates a new token list.
    /// </summary>
    public GreenList(ImmutableArray<GreenNode> children)
    {
        _children = children.IsDefault ? ImmutableArray<GreenNode>.Empty : children;
        
        // Compute width
        int width = 0;
        foreach (var child in _children)
            width += child.Width;
        _width = width;
        
        // Pre-compute offsets for large lists
        if (_children.Length >= 10)
        {
            _childOffsets = new int[_children.Length];
            int offset = 0;
            for (int i = 0; i < _children.Length; i++)
            {
                _childOffsets[i] = offset;
                offset += _children[i].Width;
            }
        }
    }
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index)
        => index >= 0 && index < _children.Length ? _children[index] : null;
    
    /// <inheritdoc/>
    public override int GetSlotOffset(int index)
    {
        if (_childOffsets != null)
            return _childOffsets[index];
        
        int offset = 0;
        for (int i = 0; i < index; i++)
            offset += _children[i].Width;
        return offset;
    }
    
    /// <inheritdoc/>
    public override RedNode CreateRed(RedNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        => new RedList(this, parent, position, siblingIndex, tree);
    
    /// <inheritdoc/>
    public override void WriteTo(IBufferWriter<char> writer)
    {
        foreach (var child in _children)
            child.WriteTo(writer);
    }
    
    #region Structural Sharing Mutations
    
    /// <inheritdoc/>
    public override GreenList WithSlot(int index, GreenNode newChild)
    {
        if (index < 0 || index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        return new GreenList(_children.SetItem(index, newChild));
    }
    
    /// <inheritdoc/>
    public override GreenList WithChildren(ImmutableArray<GreenNode> newChildren)
        => new(newChildren);
    
    /// <inheritdoc/>
    public override GreenList WithInsert(int index, ImmutableArray<GreenNode> nodes)
    {
        if (index < 0 || index > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.InsertRange(index, nodes);
        return new GreenList(builder.ToImmutable());
    }
    
    /// <inheritdoc/>
    public override GreenList WithRemove(int index, int count)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        return new GreenList(builder.ToImmutable());
    }
    
    /// <inheritdoc/>
    public override GreenList WithReplace(int index, int count, ImmutableArray<GreenNode> replacement)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        builder.InsertRange(index, replacement);
        return new GreenList(builder.ToImmutable());
    }
    
    #endregion
}

/// <summary>
/// Red node wrapper for the root token list.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class RedList : RedNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"List[{Position}..{EndPosition}] ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";
    
    /// <summary>
    /// Creates a new red list.
    /// </summary>
    internal RedList(GreenList green, RedNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        : base(green, parent, position, siblingIndex, tree)
    {
    }
    
    /// <summary>The underlying green list.</summary>
    internal new GreenList Green => (GreenList)base.Green;
    
    /// <summary>Number of children.</summary>
    public int ChildCount => Green.SlotCount;
    
    /// <inheritdoc/>
    public override RedNode? GetChild(int index)
    {
        if (index < 0 || index >= Green.SlotCount)
            return null;
        
        var greenChild = Green.GetSlot(index);
        if (greenChild == null)
            return null;
        
        var childPosition = Position + Green.GetSlotOffset(index);
        return greenChild.CreateRed(this, childPosition, index, Tree);
    }
}
