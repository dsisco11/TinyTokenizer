using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// A green node representing a schema-defined syntax construct (e.g., function call, variable declaration).
/// Wraps underlying structural green nodes as children and specifies the concrete red node type to create.
/// </summary>
/// <remarks>
/// GreenSyntaxNode enables schema-defined patterns to be materialized into the green tree during binding.
/// When the red tree is created, it produces typed red nodes (e.g., RedFunctionCall) that provide
/// domain-specific accessors for the matched syntax pattern.
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed record GreenSyntaxNode : GreenContainer
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Width}] ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";

    private readonly ImmutableArray<GreenNode> _children;
    private readonly NodeKind _kind;
    private readonly Type _redType;
    private readonly int _width;
    
    /// <summary>
    /// Creates a green syntax node wrapping the specified children.
    /// </summary>
    /// <param name="kind">The semantic NodeKind for this syntax construct.</param>
    /// <param name="redType">The concrete RedSyntaxNode subclass to instantiate.</param>
    /// <param name="children">The child green nodes that make up this syntax construct.</param>
    public GreenSyntaxNode(NodeKind kind, Type redType, ImmutableArray<GreenNode> children)
    {
        _kind = kind;
        _redType = redType;
        _children = children.IsDefault ? ImmutableArray<GreenNode>.Empty : children;
        
        // Calculate total width
        int width = 0;
        foreach (var child in _children)
        {
            width += child.Width;
        }
        _width = width;
    }
    
    /// <summary>
    /// Creates a green syntax node from params array of children.
    /// </summary>
    public GreenSyntaxNode(NodeKind kind, Type redType, params GreenNode[] children)
        : this(kind, redType, ImmutableArray.Create(children))
    {
    }
    
    /// <inheritdoc/>
    public override NodeKind Kind => _kind;
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <summary>
    /// The concrete RedSyntaxNode subclass to instantiate when creating the red node.
    /// </summary>
    public Type RedType => _redType;
    
    /// <inheritdoc/>
    public override ImmutableArray<GreenNode> Children => _children;
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index) =>
        index >= 0 && index < _children.Length ? _children[index] : null;
    
    /// <inheritdoc/>
    public override void WriteTo(IBufferWriter<char> writer)
    {
        foreach (var child in _children)
            child.WriteTo(writer);
    }
    
    /// <inheritdoc/>
    public override RedNode CreateRed(RedNode? parent, int position, int siblingIndex = -1) =>
        SyntaxRedFactory.Create(this, parent, position, siblingIndex);
    
    #region Structural Sharing Mutations
    
    /// <inheritdoc/>
    public override GreenSyntaxNode WithSlot(int index, GreenNode newChild)
    {
        if (index < 0 || index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        return new GreenSyntaxNode(_kind, _redType, _children.SetItem(index, newChild));
    }
    
    /// <inheritdoc/>
    public override GreenSyntaxNode WithChildren(ImmutableArray<GreenNode> newChildren)
        => new(_kind, _redType, newChildren);
    
    /// <inheritdoc/>
    public override GreenSyntaxNode WithInsert(int index, ImmutableArray<GreenNode> nodes)
    {
        if (index < 0 || index > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.InsertRange(index, nodes);
        return new GreenSyntaxNode(_kind, _redType, builder.ToImmutable());
    }
    
    /// <inheritdoc/>
    public override GreenSyntaxNode WithReplace(int index, int count, ImmutableArray<GreenNode> replacement)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        builder.InsertRange(index, replacement);
        return new GreenSyntaxNode(_kind, _redType, builder.ToImmutable());
    }
    
    #endregion
}
