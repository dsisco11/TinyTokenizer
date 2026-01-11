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
        : this(
            children.IsDefault ? ImmutableArray<GreenNode>.Empty : children,
            Compute(children.IsDefault ? ImmutableArray<GreenNode>.Empty : children))
    {
    }

    private GreenList(ImmutableArray<GreenNode> children, ListComputed computed)
        : base(computed.Flags)
    {
        _children = children;
        _width = computed.Width;
        _childOffsets = computed.ChildOffsets;
    }

    private static ListComputed Compute(ImmutableArray<GreenNode> children)
    {
        if (children.Length == 0)
            return new ListComputed(Width: 0, Flags: GreenNodeFlags.None, ChildOffsets: null);

        int width = 0;
        var contains = GreenNodeFlags.None;

        int[]? offsets = null;
        if (children.Length >= 10)
            offsets = new int[children.Length];

        int offset = 0;
        for (int i = 0; i < children.Length; i++)
        {
            if (offsets != null)
                offsets[i] = offset;

            var child = children[i];
            width += child.Width;
            offset += child.Width;
            contains |= child.Flags & GreenNodeFlagMasks.Contains;
        }

        // Token-centric boundary semantics: lists do not own boundary trivia.
        var flags = contains;
        return new ListComputed(width, flags, offsets);
    }

    private readonly record struct ListComputed(int Width, GreenNodeFlags Flags, int[]? ChildOffsets);
    
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
    public override SyntaxNode CreateRed(SyntaxNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        => new SyntaxList(this, parent, position, siblingIndex, tree);
    
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
