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
    
    /// <summary>Gets the inner children of this block (excluding delimiters).</summary>
    public ImmutableArray<GreenNode> InnerChildren => _children;
    
    /// <summary>
    /// Gets all children including delimiters (opener at slot 0, closer at slot N+1).
    /// This is the Roslyn-style slot model where delimiters are traversable children.
    /// </summary>
    public override ImmutableArray<GreenNode> Children
    {
        get
        {
            var builder = ImmutableArray.CreateBuilder<GreenNode>(_children.Length + 2);
            builder.Add(OpenerNode);
            builder.AddRange(_children);
            builder.Add(CloserNode);
            return builder.MoveToImmutable();
        }
    }
    
    /// <summary>
    /// Number of slots: opener + inner children + closer.
    /// </summary>
    public override int SlotCount => _children.Length + 2;
    
    /// <inheritdoc/>
    /// <remarks>
    /// Slot 0 = opener, slots 1..N = inner children, slot N+1 = closer.
    /// </remarks>
    public override GreenNode? GetSlot(int index)
    {
        if (index < 0 || index > _children.Length + 1)
            return null;
        if (index == 0)
            return OpenerNode;
        if (index == _children.Length + 1)
            return CloserNode;
        return _children[index - 1]; // Adjust for opener at slot 0
    }
    
    /// <inheritdoc/>
    /// <remarks>
    /// Returns offset from block start:
    /// - Slot 0 (opener): 0
    /// - Slot 1..N (inner children): opener width + sum of preceding inner children
    /// - Slot N+1 (closer): opener width + all inner children widths
    /// </remarks>
    public override int GetSlotOffset(int index)
    {
        if (index == 0)
            return 0; // Opener starts at block start
        
        if (index == _children.Length + 1)
        {
            // Closer is after opener and all children
            int closerOffset = OpenerNode.Width;
            foreach (var child in _children)
                closerOffset += child.Width;
            return closerOffset;
        }
        
        // Inner child: use precomputed offsets if available
        int innerIndex = index - 1; // Convert to inner children index
        if (_childOffsets != null)
            return _childOffsets[innerIndex]; // O(1)
        
        // O(index) for small blocks
        int offset = OpenerNode.Width; // After opener (including its trivia)
        for (int i = 0; i < innerIndex; i++)
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
    /// <summary>
    /// Creates a new block with one slot replaced.
    /// Slot 0 = opener, slots 1..N = inner children, slot N+1 = closer.
    /// </summary>
    public override GreenBlock WithSlot(int index, GreenNode newChild)
    {
        if (index < 0 || index > _children.Length + 1)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        // Slot 0 = opener
        if (index == 0)
        {
            if (newChild is not GreenLeaf newOpener)
                throw new ArgumentException("Opener slot must be a GreenLeaf", nameof(newChild));
            return new GreenBlock(newOpener, CloserNode, _children);
        }
        
        // Slot N+1 = closer
        if (index == _children.Length + 1)
        {
            if (newChild is not GreenLeaf newCloser)
                throw new ArgumentException("Closer slot must be a GreenLeaf", nameof(newChild));
            return new GreenBlock(OpenerNode, newCloser, _children);
        }
        
        // Inner child slot (1..N)
        var innerIndex = index - 1;
        var newChildren = _children.SetItem(innerIndex, newChild);
        return new GreenBlock(OpenerNode, CloserNode, newChildren);
    }
    
    /// <summary>
    /// Creates a new block with all children replaced (including delimiters).
    /// First element must be opener, last must be closer.
    /// </summary>
    public override GreenBlock WithChildren(ImmutableArray<GreenNode> newChildren)
    {
        if (newChildren.Length < 2)
            throw new ArgumentException("Children must include at least opener and closer", nameof(newChildren));
        
        if (newChildren[0] is not GreenLeaf opener)
            throw new ArgumentException("First child must be opener (GreenLeaf)", nameof(newChildren));
        if (newChildren[^1] is not GreenLeaf closer)
            throw new ArgumentException("Last child must be closer (GreenLeaf)", nameof(newChildren));
        
        // Extract inner children (everything between opener and closer)
        var innerChildren = newChildren.RemoveAt(newChildren.Length - 1).RemoveAt(0);
        return new GreenBlock(opener, closer, innerChildren);
    }
    
    /// <summary>
    /// Creates a new block with inner children replaced (preserves delimiters).
    /// </summary>
    public GreenBlock WithInnerChildren(ImmutableArray<GreenNode> newInnerChildren)
        => new(OpenerNode, CloserNode, newInnerChildren);
    
    /// <summary>
    /// Inserts nodes at the specified slot index.
    /// Slot 0 = opener position, slots 1..N = inner content, slot N+1 = after last inner/before closer.
    /// Cannot insert before slot 0 or after slot N+2 (would be outside block bounds).
    /// </summary>
    /// <remarks>
    /// Inserting at slot 1 places content after the opener.
    /// Inserting at slot N+1 (where N+1 = _children.Length + 1) places content before the closer.
    /// </remarks>
    public override GreenBlock WithInsert(int index, ImmutableArray<GreenNode> nodes)
    {
        // Valid insertion range: 1 through _children.Length + 1 (after opener through before closer)
        // Slot 0 is the opener, slot _children.Length + 1 is the closer
        if (index < 1 || index > _children.Length + 1)
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Insert index must be between 1 and {_children.Length + 1} (inner content range)");
        
        var innerIndex = index - 1; // Convert to inner children index
        var builder = _children.ToBuilder();
        builder.InsertRange(innerIndex, nodes);
        return new GreenBlock(OpenerNode, CloserNode, builder.ToImmutable());
    }
    
    /// <summary>
    /// Removes nodes starting at the specified slot index.
    /// Can only remove inner children (slots 1..N). Cannot remove opener (slot 0) or closer (slot N+1).
    /// </summary>
    public override GreenBlock WithRemove(int index, int count)
    {
        // Valid removal range: slots 1..N (inner children only)
        // Slot 0 is opener, slot _children.Length + 1 is closer - neither can be removed
        if (index < 1 || count < 0)
            throw new ArgumentOutOfRangeException(nameof(index), 
                "Cannot remove opener (slot 0). Use WithSlot to replace delimiters.");
        
        var innerIndex = index - 1;
        if (innerIndex + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(count), 
                "Cannot remove closer. Removal range extends past inner children.");
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(innerIndex, count);
        return new GreenBlock(OpenerNode, CloserNode, builder.ToImmutable());
    }
    
    /// <summary>
    /// Replaces nodes starting at the specified slot index.
    /// Can replace inner children (slots 1..N) or entire block content including delimiters.
    /// </summary>
    /// <remarks>
    /// If replacing only inner content (index >= 1, not touching closer), preserves delimiters.
    /// If replacing from slot 0 through the closer, the replacement becomes the new children
    /// (first replacement node becomes opener if it's a leaf, etc.).
    /// </remarks>
    public override GreenBlock WithReplace(int index, int count, ImmutableArray<GreenNode> replacement)
    {
        var totalSlots = _children.Length + 2; // opener + children + closer
        
        if (index < 0 || count < 0 || index + count > totalSlots)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Replace range [{index}..{index + count}) is out of bounds for slot count {totalSlots}");
        
        // Special case: replacing entire block content (slot 0 through last slot)
        // This replaces opener, all inner children, and closer
        if (index == 0 && count == totalSlots)
        {
            // The replacement becomes the new full children array
            return WithChildren(replacement);
        }
        
        // Special case: replacing from opener through some inner content
        // This is invalid - can't partially replace including opener without replacing all
        if (index == 0)
        {
            throw new ArgumentException(
                "Cannot replace range starting at opener (slot 0) without replacing entire block. " +
                "Use WithSlot to replace just the opener, or WithChildren to replace all.",
                nameof(index));
        }
        
        // Special case: replacing through the closer
        // This is invalid - can't partially replace including closer without replacing all  
        if (index + count == totalSlots && index != 0)
        {
            throw new ArgumentException(
                "Cannot replace range including closer without replacing entire block. " +
                "Use WithSlot to replace just the closer, or WithChildren to replace all.",
                nameof(count));
        }
        
        // Normal case: replacing inner content only (slots 1 through N)
        var innerIndex = index - 1;
        var builder = _children.ToBuilder();
        builder.RemoveRange(innerIndex, count);
        builder.InsertRange(innerIndex, replacement);
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
