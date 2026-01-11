using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

#region Not Query

/// <summary>
/// Zero-width negative lookahead assertion.
/// Succeeds when the inner query does NOT match the current node.
/// Never consumes any nodes (consumedCount = 0).
/// </summary>
/// <remarks>
/// Use this in sequences to assert absence without consuming:
/// <code>Query.Sequence(Query.Not(Query.Ident("if")), Query.AnyIdent)</code>
/// This matches any identifier that is NOT "if".
/// </remarks>
public sealed record NotQuery : INodeQuery, IGreenNodeQuery, ISchemaResolvableQuery
{
    private readonly INodeQuery _inner;
    
    /// <summary>Creates a negative lookahead assertion.</summary>
    public NotQuery(INodeQuery inner) => _inner = inner;
    
    /// <inheritdoc/>
    public bool IsResolved => _inner is not ISchemaResolvableQuery r || r.IsResolved;
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        if (_inner is ISchemaResolvableQuery resolvable && !resolvable.IsResolved)
            resolvable.ResolveWithSchema(schema);
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (!_inner.Matches(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => !_inner.Matches(node);
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Zero-width assertion - never consumes
        consumedCount = 0;
        return !_inner.TryMatch(startNode, out _);
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        if (_inner is IGreenNodeQuery greenQuery)
            return !greenQuery.MatchesGreen(node);
        return true; // If inner doesn't support green, assume it doesn't match
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        consumedCount = 0;
        if (_inner is IGreenNodeQuery greenQuery)
            return !greenQuery.TryMatchGreen(siblings, startIndex, out _);
        return true; // If inner doesn't support green, assume it doesn't match
    }
}

#endregion

#region Between Query

/// <summary>
/// Matches and captures content between a start and end query/delimiter.
/// Consumes all nodes from start through end (inclusive of delimiters).
/// </summary>
/// <remarks>
/// Useful for extracting content between matching patterns:
/// <code>Query.Between(Query.Symbol("("), Query.Symbol(")"))</code>
/// </remarks>
public sealed record BetweenQuery : INodeQuery, IGreenNodeQuery, IRegionQuery, ISchemaResolvableQuery
{
    private readonly INodeQuery _start;
    private readonly INodeQuery _end;
    private readonly bool _inclusive;
    
    /// <summary>Creates a query matching content between start and end.</summary>
    /// <param name="start">The starting delimiter/pattern.</param>
    /// <param name="end">The ending delimiter/pattern.</param>
    /// <param name="inclusive">If true, includes start/end in consumed count; if false, only content between.</param>
    public BetweenQuery(INodeQuery start, INodeQuery end, bool inclusive = true)
    {
        _start = start;
        _end = end;
        _inclusive = inclusive;
    }
    
    /// <inheritdoc/>
    public bool IsResolved =>
        (_start is not ISchemaResolvableQuery rs || rs.IsResolved) &&
        (_end is not ISchemaResolvableQuery re || re.IsResolved);
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        if (_start is ISchemaResolvableQuery rs && !rs.IsResolved)
            rs.ResolveWithSchema(schema);
        if (_end is ISchemaResolvableQuery re && !re.IsResolved)
            re.ResolveWithSchema(schema);
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => TryMatch(node, out _);
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        consumedCount = 0;
        
        // First, check if start matches
        if (!_start.TryMatch(startNode, out var startConsumed))
            return false;
        
        // Navigate past start
        var current = startNode;
        for (int i = 0; i < startConsumed && current != null; i++)
            current = current.NextSibling();
        
        int totalConsumed = startConsumed;
        
        // Look for end
        while (current != null)
        {
            if (_end.TryMatch(current, out var endConsumed))
            {
                totalConsumed += endConsumed;
                consumedCount = _inclusive ? totalConsumed : totalConsumed - startConsumed - endConsumed;
                return true;
            }
            
            totalConsumed++;
            current = current.NextSibling();
        }
        
        // End not found
        return false;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        // Check if start matches the first node
        return _start is IGreenNodeQuery greenQuery && greenQuery.MatchesGreen(node);
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        consumedCount = 0;
        
        if (_start is not IGreenNodeQuery startGreen || _end is not IGreenNodeQuery endGreen)
            return false;
        
        // Check start matches
        if (!startGreen.TryMatchGreen(siblings, startIndex, out var startConsumed))
            return false;
        
        int currentIndex = startIndex + startConsumed;
        int totalConsumed = startConsumed;
        
        // Look for end
        while (currentIndex < siblings.Count)
        {
            if (endGreen.TryMatchGreen(siblings, currentIndex, out var endConsumed))
            {
                totalConsumed += endConsumed;
                consumedCount = _inclusive ? totalConsumed : totalConsumed - startConsumed - endConsumed;
                return true;
            }
            
            totalConsumed++;
            currentIndex++;
        }
        
        // End not found
        return false;
    }
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => ((IRegionQuery)this).SelectRegions(tree.Root);
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root)
    {
        return RegionTraversal.SelectRegions(root, TryMatch);
    }
}

#endregion

#region Sequence Query

/// <summary>
/// Matches a sequence of queries in order, consuming multiple sibling nodes.
/// </summary>
public sealed record SequenceQuery : INodeQuery, IGreenNodeQuery, IRegionQuery, ISchemaResolvableQuery
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
    public bool IsResolved
    {
        get
        {
            foreach (var part in _parts)
            {
                if (part is ISchemaResolvableQuery r && !r.IsResolved)
                    return false;
            }

            return true;
        }
    }
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        foreach (var part in _parts)
        {
            if (part is ISchemaResolvableQuery resolvable && !resolvable.IsResolved)
                resolvable.ResolveWithSchema(schema);
        }
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public bool Matches(SyntaxNode node)
    {
        // For single-node matching, check if sequence can start here
        return TryMatch(node, out _);
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
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
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        // Sequence matches if first part matches
        if (_parts.Length == 0) return true;
        return _parts[0] is IGreenNodeQuery greenQuery && greenQuery.MatchesGreen(node);
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
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
            
            if (part is not IGreenNodeQuery greenQuery || 
                !greenQuery.TryMatchGreen(siblings, currentIndex, out var partConsumed))
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
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => ((IRegionQuery)this).SelectRegions(tree.Root);
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root)
    {
        return RegionTraversal.SelectRegions(root, TryMatch);
    }
}

#endregion

#region Optional Query

/// <summary>
/// Matches zero or one occurrence of the inner query.
/// Always succeeds - returns empty match if inner doesn't match.
/// </summary>
public sealed record OptionalQuery : INodeQuery, IGreenNodeQuery, IRegionQuery, ISchemaResolvableQuery
{
    private readonly INodeQuery _inner;
    
    public OptionalQuery(INodeQuery inner) => _inner = inner;
    
    /// <inheritdoc/>
    public bool IsResolved => _inner is not ISchemaResolvableQuery r || r.IsResolved;
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        if (_inner is ISchemaResolvableQuery resolvable && !resolvable.IsResolved)
            resolvable.ResolveWithSchema(schema);
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => _inner.Select(tree);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root) => _inner.Select(root);
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => true; // Optional always "matches"
    
    bool IGreenNodeQuery.MatchesGreen(GreenNode node) => true;
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Try to match inner, but always succeed
        if (_inner.TryMatch(startNode, out consumedCount))
            return true;
        
        consumedCount = 0;
        return true; // Optional always succeeds with 0 consumed
    }
    
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (_inner is IGreenNodeQuery greenQuery && 
            greenQuery.TryMatchGreen(siblings, startIndex, out consumedCount))
            return true;
        
        consumedCount = 0;
        return true; // Optional always succeeds with 0 consumed
    }
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => ((IRegionQuery)this).SelectRegions(tree.Root);
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root)
    {
        // Optional delegates to inner query - only yield when inner consumes > 0
        return RegionTraversal.SelectRegions(root, TryGetInnerRegion);

        bool TryGetInnerRegion(SyntaxNode node, out int consumedCount)
        {
            if (_inner.TryMatch(node, out consumedCount) && consumedCount > 0)
                return true;

            consumedCount = 0;
            return false;
        }
    }
}

#endregion

#region Repeat Query

/// <summary>
/// Matches the inner query multiple times (min to max occurrences).
/// </summary>
public sealed record RepeatQuery : INodeQuery, IGreenNodeQuery, IRegionQuery, ISchemaResolvableQuery
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
    public bool IsResolved => _inner is not ISchemaResolvableQuery r || r.IsResolved;
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        if (_inner is ISchemaResolvableQuery resolvable && !resolvable.IsResolved)
            resolvable.ResolveWithSchema(schema);
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => TryMatch(node, out _);
    
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        if (_min == 0) return true;
        return _inner is IGreenNodeQuery greenQuery && greenQuery.MatchesGreen(node);
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
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
    
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (_inner is not IGreenNodeQuery greenQuery)
        {
            consumedCount = 0;
            return _min == 0;
        }
        
        int currentIndex = startIndex;
        int count = 0;
        int totalConsumed = 0;
        
        while (currentIndex < siblings.Count && count < _max)
        {
            if (!greenQuery.TryMatchGreen(siblings, currentIndex, out var partConsumed))
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
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => ((IRegionQuery)this).SelectRegions(tree.Root);
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root)
    {
        return RegionTraversal.SelectRegions(root, TryGetNonEmptyRegion);

        bool TryGetNonEmptyRegion(SyntaxNode node, out int consumedCount)
        {
            if (TryMatch(node, out consumedCount) && consumedCount > 0)
                return true;

            consumedCount = 0;
            return false;
        }
    }
}

#endregion

#region RepeatUntil Query

/// <summary>
/// Matches the inner query repeatedly until a terminator is encountered.
/// The terminator is NOT consumed (lookahead-style matching).
/// </summary>
public sealed record RepeatUntilQuery : INodeQuery, IGreenNodeQuery, IRegionQuery, ISchemaResolvableQuery
{
    private readonly INodeQuery _inner;
    private readonly INodeQuery _terminator;
    
    public RepeatUntilQuery(INodeQuery inner, INodeQuery terminator)
    {
        _inner = inner;
        _terminator = terminator;
    }
    
    /// <inheritdoc/>
    public bool IsResolved =>
        (_inner is not ISchemaResolvableQuery ri || ri.IsResolved) &&
        (_terminator is not ISchemaResolvableQuery rt || rt.IsResolved);
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        if (_inner is ISchemaResolvableQuery ri && !ri.IsResolved)
            ri.ResolveWithSchema(schema);
        if (_terminator is ISchemaResolvableQuery rt && !rt.IsResolved)
            rt.ResolveWithSchema(schema);
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => TryMatch(node, out _);
    
    bool IGreenNodeQuery.MatchesGreen(GreenNode node) => true; // Can match zero items
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
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
    
    private bool TerminatorMatches(SyntaxNode node)
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
    
    internal bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        int currentIndex = startIndex;
        int totalConsumed = 0;
        
        // Check if inner query supports green matching
        if (_inner is not IGreenNodeQuery innerGreen)
        {
            consumedCount = 0;
            return true; // Can match zero items
        }
        
        while (currentIndex < siblings.Count)
        {
            // Check if terminator matches (lookahead - don't consume)
            if (TerminatorMatchesGreen(siblings, currentIndex))
                break;
            
            // Try to match inner
            if (!innerGreen.TryMatchGreen(siblings, currentIndex, out var partConsumed))
                break;
            
            totalConsumed += partConsumed;
            currentIndex += partConsumed;
        }
        
        consumedCount = totalConsumed;
        return true;
    }
    
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
        => TryMatchGreen(siblings, startIndex, out consumedCount);
    
    private bool TerminatorMatchesGreen(IReadOnlyList<GreenNode> siblings, int index)
    {
        // Direct match
        if (_terminator is IGreenNodeQuery terminatorGreen && 
            terminatorGreen.TryMatchGreen(siblings, index, out _))
            return true;
        
        // For newline terminators, check both leading trivia of current node
        // AND trailing trivia of previous node (to handle trailing-trivia model)
        if (_terminator is NewlineNodeQuery)
        {
            var greenNode = siblings[index];
            
            // Check leading trivia of current node
            var leadingTrivia = greenNode switch
            {
                GreenLeaf leaf => leaf.LeadingTrivia,
                GreenBlock block => block.LeadingTrivia,
                _ => ImmutableArray<GreenTrivia>.Empty
            };
            
            foreach (var t in leadingTrivia)
            {
                if (t.Kind == TriviaKind.Newline)
                    return true;
            }
            
            // Check trailing trivia of previous node (if exists)
            // This handles the trailing-trivia model where newlines are attached
            // to the previous token rather than leading trivia of the next token
            if (index > 0)
            {
                var prevNode = siblings[index - 1];
                var trailingTrivia = prevNode switch
                {
                    GreenLeaf leaf => leaf.TrailingTrivia,
                    GreenBlock block => block.TrailingTrivia,
                    _ => ImmutableArray<GreenTrivia>.Empty
                };
                
                foreach (var t in trailingTrivia)
                {
                    if (t.Kind == TriviaKind.Newline)
                        return true;
                }
            }
        }
        
        return false;
    }
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => ((IRegionQuery)this).SelectRegions(tree.Root);
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root)
    {
        return RegionTraversal.SelectRegions(root, TryMatch);
    }
}

#endregion

#region Lookahead Query

/// <summary>
/// Matches the inner query only if followed by (or not followed by) the lookahead query.
/// The lookahead is not consumed (zero-width assertion).
/// </summary>
public sealed record LookaheadQuery : INodeQuery, IGreenNodeQuery, IRegionQuery, ISchemaResolvableQuery
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
    public bool IsResolved =>
        (_inner is not ISchemaResolvableQuery ri || ri.IsResolved) &&
        (_lookahead is not ISchemaResolvableQuery rl || rl.IsResolved);
    
    /// <inheritdoc/>
    public void ResolveWithSchema(Schema schema)
    {
        if (_inner is ISchemaResolvableQuery ri && !ri.IsResolved)
            ri.ResolveWithSchema(schema);
        if (_lookahead is ISchemaResolvableQuery rl && !rl.IsResolved)
            rl.ResolveWithSchema(schema);
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (TryMatch(node, out _))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => TryMatch(node, out _);
    
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        return _inner is IGreenNodeQuery greenQuery && greenQuery.MatchesGreen(node);
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
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
    
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        // Check if inner query supports green matching
        if (_inner is not IGreenNodeQuery innerGreen)
        {
            consumedCount = 0;
            return false;
        }
        
        // First, try to match the inner query
        if (!innerGreen.TryMatchGreen(siblings, startIndex, out var innerConsumed))
        {
            consumedCount = 0;
            return false;
        }
        
        // Check lookahead at the position after inner match
        int lookaheadIndex = startIndex + innerConsumed;
        bool lookaheadMatches = lookaheadIndex < siblings.Count && 
            _lookahead is IGreenNodeQuery lookaheadGreen &&
            lookaheadGreen.TryMatchGreen(siblings, lookaheadIndex, out _);
        
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
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => ((IRegionQuery)this).SelectRegions(tree.Root);
    
    /// <inheritdoc/>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root)
    {
        return RegionTraversal.SelectRegions(root, TryMatch);
    }
}

#endregion
