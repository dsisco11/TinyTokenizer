using System.Collections.Immutable;
using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Red node wrapper for block structures.
/// Provides position-aware access to children with lazy creation and caching.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class RedBlock : RedNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Position}..{EndPosition}] '{Opener}' ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";

    // Lazy child cache - initialized on first child access
    private RedNode?[]? _children;
    
    // Lazy opener/closer red nodes
    private RedLeaf? _openerNode;
    private RedLeaf? _closerNode;
    
    /// <summary>
    /// Creates a new red block wrapping a green block.
    /// </summary>
    internal RedBlock(GreenBlock green, RedNode? parent, int position, int siblingIndex = -1)
        : base(green, parent, position, siblingIndex)
    {
    }
    
    /// <summary>The underlying green block.</summary>
    internal new GreenBlock Green => (GreenBlock)base.Green;
    
    /// <summary>The opening delimiter character.</summary>
    public char Opener => Green.Opener;
    
    /// <summary>The closing delimiter character.</summary>
    public char Closer => Green.Closer;
    
    /// <summary>The opening delimiter node with its trivia.</summary>
    public RedLeaf OpenerNode
    {
        get
        {
            if (_openerNode == null)
            {
                var node = (RedLeaf)Green.OpenerNode.CreateRed(this, Position);
                Interlocked.CompareExchange(ref _openerNode, node, null);
            }
            return _openerNode!;
        }
    }
    
    /// <summary>The closing delimiter node with its trivia.</summary>
    public RedLeaf CloserNode
    {
        get
        {
            if (_closerNode == null)
            {
                var closerPosition = EndPosition - Green.CloserNode.Width;
                var node = (RedLeaf)Green.CloserNode.CreateRed(this, closerPosition);
                Interlocked.CompareExchange(ref _closerNode, node, null);
            }
            return _closerNode!;
        }
    }
    
    /// <summary>Number of children in this block.</summary>
    public int ChildCount => Green.SlotCount;
    
    /// <summary>Leading trivia before the opening delimiter (from opener node).</summary>
    internal ImmutableArray<GreenTrivia> GreenLeadingTrivia => Green.OpenerNode.LeadingTrivia;
    
    /// <summary>Inner trivia after opener (from opener node's trailing trivia, for blocks like "{ }").</summary>
    internal ImmutableArray<GreenTrivia> GreenInnerTrivia => Green.OpenerNode.TrailingTrivia;
    
    /// <summary>Trailing trivia after the closing delimiter (from closer node).</summary>
    internal ImmutableArray<GreenTrivia> GreenTrailingTrivia => Green.CloserNode.TrailingTrivia;
    
    /// <summary>
    /// Gets the leading trivia before the opening delimiter.
    /// </summary>
    /// <returns>An enumerable of trivia items before the opening delimiter.</returns>
    /// <seealso cref="GetTrailingTrivia"/>
    /// <seealso cref="GetInnerTrivia"/>
    public IEnumerable<Trivia> GetLeadingTrivia()
    {
        foreach (var green in Green.OpenerNode.LeadingTrivia)
        {
            yield return new Trivia(green);
        }
    }
    
    /// <summary>
    /// Gets the inner trivia (trivia after opener when block is empty, for blocks like "{ }").
    /// </summary>
    /// <returns>An enumerable of trivia items after the opening delimiter.</returns>
    /// <seealso cref="GetLeadingTrivia"/>
    /// <seealso cref="GetTrailingTrivia"/>
    public IEnumerable<Trivia> GetInnerTrivia()
    {
        foreach (var green in Green.OpenerNode.TrailingTrivia)
        {
            yield return new Trivia(green);
        }
    }
    
    /// <summary>
    /// Gets the trailing trivia after the closing delimiter.
    /// </summary>
    /// <returns>An enumerable of trivia items after the closing delimiter.</returns>
    /// <seealso cref="GetLeadingTrivia"/>
    /// <seealso cref="GetInnerTrivia"/>
    public IEnumerable<Trivia> GetTrailingTrivia()
    {
        foreach (var green in Green.CloserNode.TrailingTrivia)
        {
            yield return new Trivia(green);
        }
    }
    
    /// <summary>
    /// Gets whether this block has any leading trivia.
    /// </summary>
    public bool HasLeadingTrivia => !Green.OpenerNode.LeadingTrivia.IsEmpty;
    
    /// <summary>
    /// Gets whether this block has any inner trivia.
    /// </summary>
    public bool HasInnerTrivia => !Green.OpenerNode.TrailingTrivia.IsEmpty;
    
    /// <summary>
    /// Gets whether this block has any trailing trivia.
    /// </summary>
    public bool HasTrailingTrivia => !Green.CloserNode.TrailingTrivia.IsEmpty;
    
    /// <summary>Width of leading trivia.</summary>
    public int LeadingTriviaWidth => Green.LeadingTriviaWidth;
    
    /// <summary>Width of trailing trivia.</summary>
    public int TrailingTriviaWidth => Green.TrailingTriviaWidth;
    
    /// <summary>
    /// Position of the opening delimiter (after leading trivia).
    /// </summary>
    public int OpenerPosition => Position + Green.OpenerNode.LeadingTriviaWidth;
    
    /// <summary>
    /// Position of the closing delimiter (after closer's leading trivia).
    /// </summary>
    public int CloserPosition => EndPosition - Green.CloserNode.TrailingTriviaWidth - 1;
    
    /// <summary>
    /// Position of the inner content (after opener including opener's trailing trivia).
    /// </summary>
    public int InnerStartPosition => Position + Green.OpenerNode.Width;
    
    /// <summary>
    /// End position of the inner content (before closer's leading trivia).
    /// </summary>
    public int InnerEndPosition => EndPosition - Green.CloserNode.Width;
    
    /// <inheritdoc/>
    public override RedNode? GetChild(int index)
    {
        if (index < 0 || index >= Green.SlotCount)
            return null;
        
        // Lazy init the child array
        _children ??= new RedNode?[Green.SlotCount];
        
        // Check cache
        if (_children[index] != null)
            return _children[index];
        
        // Get green child
        var greenChild = Green.GetSlot(index);
        if (greenChild == null)
            return null;
        
        // Compute position and create red child
        var childPosition = Position + Green.GetSlotOffset(index);
        var redChild = greenChild.CreateRed(this, childPosition, index);
        
        // Cache with thread-safe exchange
        Interlocked.CompareExchange(ref _children[index], redChild, null);
        return _children[index];
    }
    
    /// <summary>
    /// Gets all children as an enumerable (lazy creation).
    /// </summary>
    public new IEnumerable<RedNode> Children
    {
        get
        {
            for (int i = 0; i < ChildCount; i++)
            {
                var child = GetChild(i);
                if (child != null)
                    yield return child;
            }
        }
    }
    
    /// <summary>
    /// Gets children of a specific kind.
    /// </summary>
    public IEnumerable<RedNode> ChildrenOfKind(NodeKind kind)
    {
        foreach (var child in Children)
        {
            if (child.Kind == kind)
                yield return child;
        }
    }
    
    /// <summary>
    /// Gets all leaf children.
    /// </summary>
    public IEnumerable<RedLeaf> LeafChildren
    {
        get
        {
            foreach (var child in Children)
            {
                if (child is RedLeaf leaf)
                    yield return leaf;
            }
        }
    }
    
    /// <summary>
    /// Gets all block children.
    /// </summary>
    public IEnumerable<RedBlock> BlockChildren
    {
        get
        {
            foreach (var child in Children)
            {
                if (child is RedBlock block)
                    yield return block;
            }
        }
    }
    
    /// <summary>
    /// Finds the index of a child node.
    /// </summary>
    public int IndexOf(RedNode child)
    {
        for (int i = 0; i < ChildCount; i++)
        {
            if (ReferenceEquals(GetChild(i), child))
                return i;
        }
        return -1;
    }
}
