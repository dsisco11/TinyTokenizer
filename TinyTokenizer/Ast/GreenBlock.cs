using System.Collections.Immutable;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// Green node for block structures: { }, [ ], ( ).
/// Contains children and delimiter information. Supports structural sharing mutations.
/// </summary>
/// <remarks>
/// Uses leading-preferred trivia model:
/// - LeadingTrivia: trivia before the opening delimiter
/// - InnerTrivia: trivia after the opener when block is empty (for blocks like "{ }")
/// - TrailingTrivia: trivia after the closing delimiter (used as fallback when leading isn't possible)
/// </remarks>
internal sealed record GreenBlock : GreenContainer
{
    private readonly ImmutableArray<GreenNode> _children;
    private readonly int _width;
    private readonly int[]? _childOffsets; // Pre-computed for ≥10 children (O(1) lookup)
    
    /// <inheritdoc/>
    public override NodeKind Kind { get; }
    
    /// <summary>The opening delimiter character.</summary>
    public char Opener { get; }
    
    /// <summary>The closing delimiter character.</summary>
    public char Closer { get; }
    
    /// <summary>Trivia before the opening delimiter.</summary>
    public ImmutableArray<GreenTrivia> LeadingTrivia { get; }
    
    /// <summary>Trivia inside empty blocks (after opener, before closer when no children).</summary>
    public ImmutableArray<GreenTrivia> InnerTrivia { get; }
    
    /// <summary>Trivia after the closing delimiter.</summary>
    public ImmutableArray<GreenTrivia> TrailingTrivia { get; }
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <summary>Total width of leading trivia.</summary>
    public int LeadingTriviaWidth { get; }
    
    /// <summary>Total width of inner trivia.</summary>
    public int InnerTriviaWidth { get; }
    
    /// <summary>Total width of trailing trivia.</summary>
    public int TrailingTriviaWidth { get; }
    
    /// <summary>
    /// Creates a new block node.
    /// </summary>
    public GreenBlock(
        char opener,
        ImmutableArray<GreenNode> children,
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default,
        ImmutableArray<GreenTrivia> innerTrivia = default)
    {
        Opener = opener;
        Closer = GetMatchingCloser(opener);
        Kind = GetBlockKind(opener);
        _children = children.IsDefault ? ImmutableArray<GreenNode>.Empty : children;
        LeadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        InnerTrivia = innerTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : innerTrivia;
        TrailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
        
        LeadingTriviaWidth = ComputeTriviaWidth(LeadingTrivia);
        InnerTriviaWidth = ComputeTriviaWidth(InnerTrivia);
        TrailingTriviaWidth = ComputeTriviaWidth(TrailingTrivia);
        
        // Compute width: leading trivia + opener(1) + inner trivia + children + closer(1) + trailing trivia
        int childrenWidth = 0;
        foreach (var child in _children)
            childrenWidth += child.Width;
        
        _width = LeadingTriviaWidth + 1 + InnerTriviaWidth + childrenWidth + 1 + TrailingTriviaWidth;
        
        // Pre-compute child offsets for large blocks
        if (_children.Length >= 10)
        {
            _childOffsets = new int[_children.Length];
            int offset = LeadingTriviaWidth + 1 + InnerTriviaWidth; // After leading trivia, opener, and inner trivia
            for (int i = 0; i < _children.Length; i++)
            {
                _childOffsets[i] = offset;
                offset += _children[i].Width;
            }
        }
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
        int offset = LeadingTriviaWidth + 1 + InnerTriviaWidth; // After leading trivia, opener, and inner trivia
        for (int i = 0; i < index; i++)
            offset += _children[i].Width;
        return offset;
    }
    
    /// <inheritdoc/>
    protected override int GetLeadingWidth() => LeadingTriviaWidth + 1 + InnerTriviaWidth; // Leading trivia + opener + inner trivia
    
    /// <inheritdoc/>
    public override RedNode CreateRed(RedNode? parent, int position)
        => new RedBlock(this, parent, position);
    
    /// <inheritdoc/>
    public override void WriteTo(StringBuilder builder)
    {
        foreach (var trivia in LeadingTrivia)
            builder.Append(trivia.Text);
        builder.Append(Opener);
        foreach (var trivia in InnerTrivia)
            builder.Append(trivia.Text);
        foreach (var child in _children)
            child.WriteTo(builder);
        builder.Append(Closer);
        foreach (var trivia in TrailingTrivia)
            builder.Append(trivia.Text);
    }
    
    #region Structural Sharing Mutations
    
    /// <inheritdoc/>
    public override GreenBlock WithSlot(int index, GreenNode newChild)
    {
        if (index < 0 || index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var newChildren = _children.SetItem(index, newChild);
        return new GreenBlock(Opener, newChildren, LeadingTrivia, TrailingTrivia, InnerTrivia);
    }
    
    /// <inheritdoc/>
    public override GreenBlock WithChildren(ImmutableArray<GreenNode> newChildren)
        => new(Opener, newChildren, LeadingTrivia, TrailingTrivia, InnerTrivia);
    
    /// <inheritdoc/>
    public override GreenBlock WithInsert(int index, ImmutableArray<GreenNode> nodes)
    {
        if (index < 0 || index > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.InsertRange(index, nodes);
        return new GreenBlock(Opener, builder.ToImmutable(), LeadingTrivia, TrailingTrivia, InnerTrivia);
    }
    
    /// <inheritdoc/>
    public override GreenBlock WithRemove(int index, int count)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        return new GreenBlock(Opener, builder.ToImmutable(), LeadingTrivia, TrailingTrivia, InnerTrivia);
    }
    
    /// <inheritdoc/>
    public override GreenBlock WithReplace(int index, int count, ImmutableArray<GreenNode> replacement)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        builder.InsertRange(index, replacement);
        return new GreenBlock(Opener, builder.ToImmutable(), LeadingTrivia, TrailingTrivia, InnerTrivia);
    }
    
    /// <summary>
    /// Creates a new block with different leading trivia.
    /// </summary>
    public GreenBlock WithLeadingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Opener, _children, trivia, TrailingTrivia, InnerTrivia);
    
    /// <summary>
    /// Creates a new block with different trailing trivia.
    /// </summary>
    public GreenBlock WithTrailingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Opener, _children, LeadingTrivia, trivia, InnerTrivia);
    
    /// <summary>
    /// Creates a new block with different inner trivia.
    /// </summary>
    public GreenBlock WithInnerTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Opener, _children, LeadingTrivia, TrailingTrivia, trivia);
    
    #endregion
    
    #region Helpers
    
    private static char GetMatchingCloser(char opener) => opener switch
    {
        '{' => '}',
        '[' => ']',
        '(' => ')',
        _ => throw new ArgumentException($"Unknown opener: {opener}", nameof(opener))
    };
    
    private static NodeKind GetBlockKind(char opener) => opener switch
    {
        '{' => NodeKind.BraceBlock,
        '[' => NodeKind.BracketBlock,
        '(' => NodeKind.ParenBlock,
        _ => throw new ArgumentException($"Unknown opener: {opener}", nameof(opener))
    };
    
    private static int ComputeTriviaWidth(ImmutableArray<GreenTrivia> trivia)
    {
        int width = 0;
        foreach (var t in trivia)
            width += t.Width;
        return width;
    }
    
    #endregion
}
