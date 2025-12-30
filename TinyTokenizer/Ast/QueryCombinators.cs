using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

#region Sequence Query

/// <summary>
/// Matches a sequence of queries in order, consuming multiple sibling nodes.
/// </summary>
public sealed record SequenceQuery : INodeQuery
{
    private readonly ImmutableArray<INodeQuery> _parts;
    
    /// <summary>
    /// Gets the parts of this sequence.
    /// </summary>
    public ImmutableArray<INodeQuery> Parts => _parts;
    
    /// <summary>
    /// Creates a sequence query from the specified parts.
    /// </summary>
    public SequenceQuery(IEnumerable<INodeQuery> parts)
    {
        _parts = parts.ToImmutableArray();
    }
    
    /// <summary>
    /// Creates a sequence query from the specified parts.
    /// </summary>
    public SequenceQuery(params INodeQuery[] parts)
    {
        _parts = [.. parts];
    }
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root)
    {
        // For tree selection, we find places where the sequence matches
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(RedNode node)
    {
        // For single-node matching, check if sequence can start here
        return TryMatch(node, out _);
    }
    
    /// <inheritdoc/>
    public bool MatchesGreen(GreenNode node)
    {
        // Can't fully evaluate sequence without sibling context
        // Return true if first part matches
        return _parts.Length > 0 && _parts[0].MatchesGreen(node);
    }
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        var current = startNode;
        int totalConsumed = 0;
        
        foreach (var part in _parts)
        {
            if (current == null)
            {
                consumedCount = 0;
                return false;
            }
            
            if (!part.TryMatch(current, out var partConsumed))
            {
                consumedCount = 0;
                return false;
            }
            
            totalConsumed += partConsumed;
            
            // Advance to next sibling after consumed nodes
            for (int i = 0; i < partConsumed; i++)
            {
                current = current?.NextSibling();
            }
        }
        
        consumedCount = totalConsumed;
        return true;
    }
    
    /// <inheritdoc/>
    public bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        int currentIndex = startIndex;
        int totalConsumed = 0;
        
        foreach (var part in _parts)
        {
            if (currentIndex >= siblings.Count)
            {
                consumedCount = 0;
                return false;
            }
            
            if (!part.TryMatchGreen(siblings, currentIndex, out var partConsumed))
            {
                consumedCount = 0;
                return false;
            }
            
            totalConsumed += partConsumed;
            currentIndex += partConsumed;
        }
        
        consumedCount = totalConsumed;
        return true;
    }
}

#endregion

#region Optional Query

/// <summary>
/// Matches zero or one occurrence of the inner query.
/// Always succeeds - returns empty match if inner doesn't match.
/// </summary>
public sealed record OptionalQuery : INodeQuery
{
    private readonly INodeQuery _inner;
    
    public OptionalQuery(INodeQuery inner) => _inner = inner;
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => _inner.Select(tree);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root) => _inner.Select(root);
    
    /// <inheritdoc/>
    public bool Matches(RedNode node) => true; // Optional always "matches"
    
    /// <inheritdoc/>
    public bool MatchesGreen(GreenNode node) => true;
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        // Try to match inner, but always succeed
        if (_inner.TryMatch(startNode, out consumedCount))
            return true;
        
        consumedCount = 0;
        return true; // Optional always succeeds with 0 consumed
    }
    
    /// <inheritdoc/>
    public bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (_inner.TryMatchGreen(siblings, startIndex, out consumedCount))
            return true;
        
        consumedCount = 0;
        return true;
    }
}

#endregion

#region Repeat Query

/// <summary>
/// Matches the inner query multiple times (min to max occurrences).
/// </summary>
public sealed record RepeatQuery : INodeQuery
{
    private readonly INodeQuery _inner;
    private readonly int _min;
    private readonly int _max;
    
    public RepeatQuery(INodeQuery inner, int min, int max)
    {
        _inner = inner;
        _min = min;
        _max = max;
    }
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(RedNode node) => TryMatch(node, out _);
    
    /// <inheritdoc/>
    public bool MatchesGreen(GreenNode node) => _inner.MatchesGreen(node) || _min == 0;
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        var current = startNode;
        int count = 0;
        int totalConsumed = 0;
        
        while (current != null && count < _max)
        {
            if (!_inner.TryMatch(current, out var partConsumed))
                break;
            
            totalConsumed += partConsumed;
            count++;
            
            // Advance to next sibling
            for (int i = 0; i < partConsumed; i++)
            {
                current = current?.NextSibling();
            }
        }
        
        if (count < _min)
        {
            consumedCount = 0;
            return false;
        }
        
        consumedCount = totalConsumed;
        return true;
    }
    
    /// <inheritdoc/>
    public bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        int currentIndex = startIndex;
        int count = 0;
        int totalConsumed = 0;
        
        while (currentIndex < siblings.Count && count < _max)
        {
            if (!_inner.TryMatchGreen(siblings, currentIndex, out var partConsumed))
                break;
            
            totalConsumed += partConsumed;
            currentIndex += partConsumed;
            count++;
        }
        
        if (count < _min)
        {
            consumedCount = 0;
            return false;
        }
        
        consumedCount = totalConsumed;
        return true;
    }
}

#endregion

#region RepeatUntil Query

/// <summary>
/// Matches the inner query repeatedly until a terminator is encountered.
/// The terminator is NOT consumed (lookahead-style matching).
/// </summary>
public sealed record RepeatUntilQuery : INodeQuery
{
    private readonly INodeQuery _inner;
    private readonly INodeQuery _terminator;
    
    public RepeatUntilQuery(INodeQuery inner, INodeQuery terminator)
    {
        _inner = inner;
        _terminator = terminator;
    }
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(RedNode node) => TryMatch(node, out _);
    
    /// <inheritdoc/>
    public bool MatchesGreen(GreenNode node) => true; // Can match zero items
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        var current = startNode;
        int totalConsumed = 0;
        
        while (current != null)
        {
            // Check if terminator matches (lookahead - don't consume)
            if (TerminatorMatches(current))
                break;
            
            // Try to match inner
            if (!_inner.TryMatch(current, out var partConsumed))
                break;
            
            totalConsumed += partConsumed;
            
            // Advance to next sibling
            for (int i = 0; i < partConsumed; i++)
            {
                current = current?.NextSibling();
            }
        }
        
        // RepeatUntil always succeeds (can match zero items)
        consumedCount = totalConsumed;
        return true;
    }
    
    private bool TerminatorMatches(RedNode node)
    {
        // Direct match on the node
        if (_terminator.TryMatch(node, out _))
            return true;
        
        // For newline terminators, also check leading trivia
        if (_terminator is NewlineNodeQuery)
        {
            var trivia = node.Green switch
            {
                GreenLeaf leaf => leaf.LeadingTrivia,
                GreenBlock block => block.LeadingTrivia,
                _ => ImmutableArray<GreenTrivia>.Empty
            };
            
            foreach (var t in trivia)
            {
                if (t.Kind == TriviaKind.Newline)
                    return true;
            }
        }
        
        return false;
    }
    
    /// <inheritdoc/>
    public bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        int currentIndex = startIndex;
        int totalConsumed = 0;
        
        while (currentIndex < siblings.Count)
        {
            // Check if terminator matches (lookahead - don't consume)
            if (TerminatorMatchesGreen(siblings, currentIndex))
                break;
            
            // Try to match inner
            if (!_inner.TryMatchGreen(siblings, currentIndex, out var partConsumed))
                break;
            
            totalConsumed += partConsumed;
            currentIndex += partConsumed;
        }
        
        consumedCount = totalConsumed;
        return true;
    }
    
    private bool TerminatorMatchesGreen(IReadOnlyList<GreenNode> siblings, int index)
    {
        // Direct match
        if (_terminator.TryMatchGreen(siblings, index, out _))
            return true;
        
        // For newline terminators, check leading trivia
        if (_terminator is NewlineNodeQuery)
        {
            var greenNode = siblings[index];
            var trivia = greenNode switch
            {
                GreenLeaf leaf => leaf.LeadingTrivia,
                GreenBlock block => block.LeadingTrivia,
                _ => ImmutableArray<GreenTrivia>.Empty
            };
            
            foreach (var t in trivia)
            {
                if (t.Kind == TriviaKind.Newline)
                    return true;
            }
        }
        
        return false;
    }
}

#endregion

#region Lookahead Query

/// <summary>
/// Matches the inner query only if followed by (or not followed by) the lookahead query.
/// The lookahead is not consumed (zero-width assertion).
/// </summary>
public sealed record LookaheadQuery : INodeQuery
{
    private readonly INodeQuery _inner;
    private readonly INodeQuery _lookahead;
    private readonly bool _positive;
    
    /// <summary>
    /// Creates a lookahead query.
    /// </summary>
    /// <param name="inner">The query to match and capture.</param>
    /// <param name="lookahead">The query that must (or must not) match after inner.</param>
    /// <param name="positive">True for positive lookahead, false for negative.</param>
    public LookaheadQuery(INodeQuery inner, INodeQuery lookahead, bool positive = true)
    {
        _inner = inner;
        _lookahead = lookahead;
        _positive = positive;
    }
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(RedNode node) => TryMatch(node, out _);
    
    /// <inheritdoc/>
    public bool MatchesGreen(GreenNode node) => _inner.MatchesGreen(node);
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        // First, try to match the inner query
        if (!_inner.TryMatch(startNode, out var innerConsumed))
        {
            consumedCount = 0;
            return false;
        }
        
        // Find the node after the matched sequence
        var current = startNode;
        for (int i = 0; i < innerConsumed; i++)
        {
            current = current?.NextSibling();
        }
        
        // Check lookahead condition
        bool lookaheadMatches = current != null && _lookahead.TryMatch(current, out _);
        
        if (lookaheadMatches != _positive)
        {
            consumedCount = 0;
            return false;
        }
        
        // Return only the inner consumed count (lookahead not consumed)
        consumedCount = innerConsumed;
        return true;
    }
    
    /// <inheritdoc/>
    public bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        // First, try to match the inner query
        if (!_inner.TryMatchGreen(siblings, startIndex, out var innerConsumed))
        {
            consumedCount = 0;
            return false;
        }
        
        // Check lookahead at the position after inner match
        int lookaheadIndex = startIndex + innerConsumed;
        bool lookaheadMatches = lookaheadIndex < siblings.Count && 
            _lookahead.TryMatchGreen(siblings, lookaheadIndex, out _);
        
        if (lookaheadMatches != _positive)
        {
            consumedCount = 0;
            return false;
        }
        
        consumedCount = innerConsumed;
        return true;
    }
}

#endregion
