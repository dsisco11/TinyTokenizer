using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Green node for block structures: { }, [ ], ( ).
/// Contains children and delimiter information. Supports structural sharing mutations.
/// </summary>
/// <remarks>
/// Opener and closer are represented as GreenLeaf nodes with their own trivia:
/// - OpenerNode.LeadingTrivia: trivia before the opening delimiter
/// - OpenerNode.TrailingTrivia: trivia after opener, before first child
/// - CloserNode.LeadingTrivia: trivia after last child, before closing delimiter
/// - CloserNode.TrailingTrivia: trivia after the closing delimiter
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed record GreenBlock : GreenContainer
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Width}] '{Opener}' ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";

    private readonly ImmutableArray<GreenNode> _children;
    private readonly int _width;
    private readonly int[]? _childOffsets; // Pre-computed for ≥10 children (O(1) lookup)
    
    /// <inheritdoc/>
    public override NodeKind Kind { get; }
    
    /// <summary>The opening delimiter node (e.g., '{', '[', '(') with its trivia.</summary>
    public GreenLeaf OpenerNode { get; }
    
    /// <summary>The closing delimiter node (e.g., '}', ']', ')') with its trivia.</summary>
    public GreenLeaf CloserNode { get; }
    
    /// <summary>The opening delimiter character.</summary>
    public char Opener => OpenerNode.Text[0];
    
    /// <summary>The closing delimiter character.</summary>
    public char Closer => CloserNode.Text[0];
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <summary>Total width of leading trivia (from opener node).</summary>
    public int LeadingTriviaWidth => OpenerNode.LeadingTriviaWidth;
    
    /// <summary>Total width of trailing trivia (from closer node).</summary>
    public int TrailingTriviaWidth => CloserNode.TrailingTriviaWidth;
    
    /// <summary>Leading trivia before the opener (convenience accessor for OpenerNode.LeadingTrivia).</summary>
    public ImmutableArray<GreenTrivia> LeadingTrivia => OpenerNode.LeadingTrivia;
    
    /// <summary>Trailing trivia after the closer (convenience accessor for CloserNode.TrailingTrivia).</summary>
    public ImmutableArray<GreenTrivia> TrailingTrivia => CloserNode.TrailingTrivia;
    
    /// <summary>
    /// Creates a new block node with explicit opener/closer leaf nodes.
    /// </summary>
    public GreenBlock(
        GreenLeaf openerNode,
        GreenLeaf closerNode,
        ImmutableArray<GreenNode> children)
    {
        OpenerNode = openerNode;
        CloserNode = closerNode;
        Kind = GetBlockKind(Opener);
        _children = children.IsDefault ? ImmutableArray<GreenNode>.Empty : children;
        
        // Compute width: opener (with trivia) + children + closer (with trivia)
        int childrenWidth = 0;
        foreach (var child in _children)
            childrenWidth += child.Width;
        
        _width = OpenerNode.Width + childrenWidth + CloserNode.Width;
        
        // Pre-compute child offsets for large blocks
        if (_children.Length >= 10)
        {
            _childOffsets = new int[_children.Length];
            int offset = OpenerNode.Width; // After opener (including its trivia)
            for (int i = 0; i < _children.Length; i++)
            {
                _childOffsets[i] = offset;
                offset += _children[i].Width;
            }
        }
    }
    
    /// <summary>
    /// Creates a new block node with automatic opener/closer creation.
    /// This is a convenience factory that creates GreenLeaf nodes for the delimiters.
    /// </summary>
    public static GreenBlock Create(
        char opener,
        ImmutableArray<GreenNode> children,
        ImmutableArray<GreenTrivia> openerLeadingTrivia = default,
        ImmutableArray<GreenTrivia> openerTrailingTrivia = default,
        ImmutableArray<GreenTrivia> closerLeadingTrivia = default,
        ImmutableArray<GreenTrivia> closerTrailingTrivia = default)
    {
        var openerNode = GreenNodeCache.CreateDelimiter(opener, openerLeadingTrivia, openerTrailingTrivia);
        var closerNode = GreenNodeCache.CreateDelimiter(TokenizerCore.GetClosingDelimiter(opener), closerLeadingTrivia, closerTrailingTrivia);
        return new GreenBlock(openerNode, closerNode, children);
    }
    
    /// <summary>Gets the children of this block.</summary>
    public override ImmutableArray<GreenNode> Children => _children;
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index)
        => index >= 0 && index < _children.Length ? _children[index] : null;
    
    /// <inheritdoc/>
    public override int GetSlotOffset(int index)
    {
        if (_childOffsets != null)
            return _childOffsets[index]; // O(1)
        
        // O(index) for small blocks
        int offset = OpenerNode.Width; // After opener (including its trivia)
        for (int i = 0; i < index; i++)
            offset += _children[i].Width;
        return offset;
    }
    
    /// <inheritdoc/>
    protected override int GetLeadingWidth() => OpenerNode.Width; // Opener including its trivia
    
    /// <inheritdoc/>
    public override SyntaxNode CreateRed(SyntaxNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        => new SyntaxBlock(this, parent, position, siblingIndex, tree);
    
    /// <inheritdoc/>
    public override void WriteTo(IBufferWriter<char> writer)
    {
        OpenerNode.WriteTo(writer);
        
        foreach (var child in _children)
            child.WriteTo(writer);
        
        CloserNode.WriteTo(writer);
    }
    
    #region Structural Sharing Mutations
    
    /// <inheritdoc/>
    public override GreenBlock WithSlot(int index, GreenNode newChild)
    {
        if (index < 0 || index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var newChildren = _children.SetItem(index, newChild);
        return new GreenBlock(OpenerNode, CloserNode, newChildren);
    }
    
    /// <inheritdoc/>
    public override GreenBlock WithChildren(ImmutableArray<GreenNode> newChildren)
        => new(OpenerNode, CloserNode, newChildren);
    
    /// <inheritdoc/>
    public override GreenBlock WithInsert(int index, ImmutableArray<GreenNode> nodes)
    {
        if (index < 0 || index > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.InsertRange(index, nodes);
        return new GreenBlock(OpenerNode, CloserNode, builder.ToImmutable());
    }
    
    /// <inheritdoc/>
    public override GreenBlock WithRemove(int index, int count)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        return new GreenBlock(OpenerNode, CloserNode, builder.ToImmutable());
    }
    
    /// <inheritdoc/>
    public override GreenBlock WithReplace(int index, int count, ImmutableArray<GreenNode> replacement)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        builder.InsertRange(index, replacement);
        return new GreenBlock(OpenerNode, CloserNode, builder.ToImmutable());
    }
    
    /// <summary>
    /// Creates a new block with a different opener node.
    /// </summary>
    public GreenBlock WithOpenerNode(GreenLeaf openerNode)
        => new(openerNode, CloserNode, _children);
    
    /// <summary>
    /// Creates a new block with a different closer node.
    /// </summary>
    public GreenBlock WithCloserNode(GreenLeaf closerNode)
        => new(OpenerNode, closerNode, _children);
    
    /// <summary>
    /// Creates a new block with different leading trivia (on the opener node).
    /// </summary>
    public GreenBlock WithLeadingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(OpenerNode.WithLeadingTrivia(trivia), CloserNode, _children);
    
    /// <summary>
    /// Creates a new block with different trailing trivia (on the closer node).
    /// </summary>
    public GreenBlock WithTrailingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(OpenerNode, CloserNode.WithTrailingTrivia(trivia), _children);
    
    #endregion
    
    #region Helpers
    
    private static NodeKind GetBlockKind(char opener) => opener switch
    {
        '{' => NodeKind.BraceBlock,
        '[' => NodeKind.BracketBlock,
        '(' => NodeKind.ParenBlock,
        _ => throw new ArgumentException($"Unknown opener: {opener}", nameof(opener))
    };
    
    #endregion
}
