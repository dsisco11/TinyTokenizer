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
/// - TrailingTrivia: trivia after the closing delimiter (used as fallback when leading isn't possible)
/// </remarks>
public sealed record GreenBlock : GreenNode
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
    
    /// <summary>Trivia after the closing delimiter.</summary>
    public ImmutableArray<GreenTrivia> TrailingTrivia { get; }
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <inheritdoc/>
    public override int SlotCount => _children.Length;
    
    /// <summary>Total width of leading trivia.</summary>
    public int LeadingTriviaWidth { get; }
    
    /// <summary>Total width of trailing trivia.</summary>
    public int TrailingTriviaWidth { get; }
    
    /// <summary>
    /// Creates a new block node.
    /// </summary>
    public GreenBlock(
        char opener,
        ImmutableArray<GreenNode> children,
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default)
    {
        Opener = opener;
        Closer = GetMatchingCloser(opener);
        Kind = GetBlockKind(opener);
        _children = children.IsDefault ? ImmutableArray<GreenNode>.Empty : children;
        LeadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        TrailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
        
        LeadingTriviaWidth = ComputeTriviaWidth(LeadingTrivia);
        TrailingTriviaWidth = ComputeTriviaWidth(TrailingTrivia);
        
        // Compute width: leading trivia + opener(1) + children + closer(1) + trailing trivia
        int childrenWidth = 0;
        foreach (var child in _children)
            childrenWidth += child.Width;
        
        _width = LeadingTriviaWidth + 1 + childrenWidth + 1 + TrailingTriviaWidth;
        
        // Pre-compute child offsets for large blocks
        if (_children.Length >= 10)
        {
            _childOffsets = new int[_children.Length];
            int offset = LeadingTriviaWidth + 1; // After leading trivia and opener
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
            return _childOffsets[index]; // O(1)
        
        // O(index) for small blocks
        int offset = LeadingTriviaWidth + 1; // After leading trivia and opener
        for (int i = 0; i < index; i++)
            offset += _children[i].Width;
        return offset;
    }
    
    /// <inheritdoc/>
    protected override int GetLeadingWidth() => LeadingTriviaWidth + 1; // Leading trivia + opener
    
    /// <inheritdoc/>
    public override RedNode CreateRed(RedNode? parent, int position)
        => new RedBlock(this, parent, position);
    
    /// <inheritdoc/>
    public override void WriteTo(StringBuilder builder)
    {
        foreach (var trivia in LeadingTrivia)
            builder.Append(trivia.Text);
        builder.Append(Opener);
        foreach (var child in _children)
            child.WriteTo(builder);
        builder.Append(Closer);
        foreach (var trivia in TrailingTrivia)
            builder.Append(trivia.Text);
    }
    
    #region Structural Sharing Mutations
    
    /// <summary>
    /// Creates a new block with one child replaced.
    /// Other children are shared by reference.
    /// </summary>
    public GreenBlock WithSlot(int index, GreenNode newChild)
    {
        if (index < 0 || index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var newChildren = _children.SetItem(index, newChild);
        return new GreenBlock(Opener, newChildren, LeadingTrivia, TrailingTrivia);
    }
    
    /// <summary>
    /// Creates a new block with children inserted at the specified index.
    /// Existing children are shared by reference.
    /// </summary>
    public GreenBlock WithInsert(int index, ImmutableArray<GreenNode> nodes)
    {
        if (index < 0 || index > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.InsertRange(index, nodes);
        return new GreenBlock(Opener, builder.ToImmutable(), LeadingTrivia, TrailingTrivia);
    }
    
    /// <summary>
    /// Creates a new block with children removed from the specified range.
    /// Remaining children are shared by reference.
    /// </summary>
    public GreenBlock WithRemove(int index, int count)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        return new GreenBlock(Opener, builder.ToImmutable(), LeadingTrivia, TrailingTrivia);
    }
    
    /// <summary>
    /// Creates a new block with a range of children replaced.
    /// </summary>
    public GreenBlock WithReplace(int index, int count, ImmutableArray<GreenNode> replacement)
    {
        if (index < 0 || count < 0 || index + count > _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        
        var builder = _children.ToBuilder();
        builder.RemoveRange(index, count);
        builder.InsertRange(index, replacement);
        return new GreenBlock(Opener, builder.ToImmutable(), LeadingTrivia, TrailingTrivia);
    }
    
    /// <summary>
    /// Creates a new block with different leading trivia.
    /// </summary>
    public GreenBlock WithLeadingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Opener, _children, trivia, TrailingTrivia);
    
    /// <summary>
    /// Creates a new block with different trailing trivia.
    /// </summary>
    public GreenBlock WithTrailingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Opener, _children, LeadingTrivia, trivia);
    
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
