using System.Collections.Immutable;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Ast;

#region NodeMatch Result

/// <summary>
/// Result of a successful pattern match, capturing matched nodes.
/// </summary>
public readonly struct NodeMatch
{
    /// <summary>The starting position of the match in source text.</summary>
    public int Position { get; init; }
    
    /// <summary>The nodes that were captured by the pattern.</summary>
    public ImmutableArray<RedNode> Parts { get; init; }
    
    /// <summary>Number of sibling nodes consumed by this match.</summary>
    public int ConsumedCount { get; init; }
    
    /// <summary>Total width of all matched nodes.</summary>
    public int Width => Parts.IsDefaultOrEmpty ? 0 : Parts.Sum(p => p.Width);
    
    /// <summary>Creates an empty (failed) match.</summary>
    public static NodeMatch Empty => new() { Parts = ImmutableArray<RedNode>.Empty };
    
    /// <summary>Whether this match succeeded (has any parts).</summary>
    public bool IsSuccess => !Parts.IsDefaultOrEmpty && Parts.Length > 0;
}

#endregion

#region Base Pattern

/// <summary>
/// Base class for patterns that match sequences of sibling nodes in the AST.
/// Patterns are tree-aware and operate on RedNode siblings within a parent.
/// </summary>
public abstract record NodePattern
{
    /// <summary>
    /// Attempts to match this pattern starting at the given node.
    /// </summary>
    /// <param name="node">The starting node (first sibling to try matching).</param>
    /// <param name="match">The match result if successful.</param>
    /// <returns>True if the pattern matched.</returns>
    public abstract bool TryMatch(RedNode node, out NodeMatch match);
    
    /// <summary>
    /// Attempts to match this pattern against green nodes starting at the given index.
    /// Used for efficient pattern matching without creating red trees.
    /// </summary>
    /// <param name="siblings">The sibling green nodes to match against.</param>
    /// <param name="startIndex">Index of the first sibling to try matching.</param>
    /// <param name="consumedCount">Number of siblings consumed if matched.</param>
    /// <returns>True if the pattern matched.</returns>
    public abstract bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount);
    
    /// <summary>
    /// Gets a description of this pattern for diagnostics.
    /// </summary>
    public abstract string Description { get; }
    
    #region Combinators
    
    /// <summary>Creates a sequence pattern from multiple queries.</summary>
    public static SequencePattern Sequence(params INodeQuery[] parts) => new(parts);
    
    /// <summary>Creates a sequence pattern from multiple patterns.</summary>
    public static SequencePattern Sequence(params NodePattern[] parts) => new(parts);
    
    /// <summary>Creates an alternative pattern (matches first successful).</summary>
    public static AlternativePattern OneOf(params NodePattern[] alternatives) => new(alternatives);
    
    /// <summary>Creates an optional pattern (matches 0 or 1 times).</summary>
    public static OptionalPattern Optional(NodePattern inner) => new(inner);
    
    /// <summary>Creates an optional pattern from a query.</summary>
    public static OptionalPattern Optional(INodeQuery query) => new(new QueryPattern(query));
    
    /// <summary>Creates a repetition pattern (matches 0 or more times).</summary>
    public static RepeatPattern ZeroOrMore(NodePattern inner) => new(inner, 0, int.MaxValue);
    
    /// <summary>Creates a repetition pattern (matches 1 or more times).</summary>
    public static RepeatPattern OneOrMore(NodePattern inner) => new(inner, 1, int.MaxValue);
    
    /// <summary>Creates a repetition pattern with bounds.</summary>
    public static RepeatPattern Repeat(NodePattern inner, int min, int max) => new(inner, min, max);
    
    #endregion
}

#endregion

#region Query Pattern (Single Node)

/// <summary>
/// Pattern that matches a single node using a NodeQuery.
/// </summary>
public sealed record QueryPattern : NodePattern
{
    private readonly INodeQuery _query;
    
    public QueryPattern(INodeQuery query) => _query = query;
    
    public override bool TryMatch(RedNode node, out NodeMatch match)
    {
        if (_query.Matches(node))
        {
            match = new NodeMatch
            {
                Position = node.Position,
                Parts = [node],
                ConsumedCount = 1
            };
            return true;
        }
        
        match = NodeMatch.Empty;
        return false;
    }
    
    public override bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (startIndex < siblings.Count && _query.MatchesGreen(siblings[startIndex]))
        {
            consumedCount = 1;
            return true;
        }
        
        consumedCount = 0;
        return false;
    }
    
    public override string Description => _query.ToString() ?? "Query";
}

#endregion

#region Sequence Pattern

/// <summary>
/// Pattern that matches a sequence of patterns in order.
/// Each pattern consumes sibling nodes and the next pattern continues from where the previous left off.
/// </summary>
public sealed record SequencePattern : NodePattern
{
    private readonly ImmutableArray<NodePattern> _parts;
    
    public SequencePattern(IEnumerable<INodeQuery> queries)
    {
        _parts = queries.Select(q => (NodePattern)new QueryPattern(q)).ToImmutableArray();
    }
    
    public SequencePattern(IEnumerable<NodePattern> patterns)
    {
        _parts = patterns.ToImmutableArray();
    }
    
    public override bool TryMatch(RedNode node, out NodeMatch match)
    {
        var parts = ImmutableArray.CreateBuilder<RedNode>();
        var current = node;
        int totalConsumed = 0;
        
        foreach (var pattern in _parts)
        {
            if (current == null)
            {
                match = NodeMatch.Empty;
                return false;
            }
            
            if (!pattern.TryMatch(current, out var partMatch))
            {
                match = NodeMatch.Empty;
                return false;
            }
            
            parts.AddRange(partMatch.Parts);
            totalConsumed += partMatch.ConsumedCount;
            
            // Advance to next sibling after consumed nodes
            for (int i = 0; i < partMatch.ConsumedCount; i++)
            {
                current = current?.NextSibling();
            }
        }
        
        match = new NodeMatch
        {
            Position = node.Position,
            Parts = parts.ToImmutable(),
            ConsumedCount = totalConsumed
        };
        return true;
    }
    
    public override bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        int currentIndex = startIndex;
        int totalConsumed = 0;
        
        foreach (var pattern in _parts)
        {
            if (currentIndex >= siblings.Count)
            {
                consumedCount = 0;
                return false;
            }
            
            if (!pattern.TryMatchGreen(siblings, currentIndex, out var partConsumed))
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
    
    public override string Description => 
        string.Join(" ", _parts.Select(p => p.Description));
}

#endregion

#region Alternative Pattern

/// <summary>
/// Pattern that tries alternatives in order and returns the first match.
/// </summary>
public sealed record AlternativePattern : NodePattern
{
    private readonly ImmutableArray<NodePattern> _alternatives;
    
    public AlternativePattern(IEnumerable<NodePattern> alternatives)
    {
        _alternatives = alternatives.ToImmutableArray();
    }
    
    public override bool TryMatch(RedNode node, out NodeMatch match)
    {
        foreach (var alt in _alternatives)
        {
            if (alt.TryMatch(node, out match))
                return true;
        }
        
        match = NodeMatch.Empty;
        return false;
    }
    
    public override bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        foreach (var alt in _alternatives)
        {
            if (alt.TryMatchGreen(siblings, startIndex, out consumedCount))
                return true;
        }
        
        consumedCount = 0;
        return false;
    }
    
    public override string Description => 
        $"({string.Join(" | ", _alternatives.Select(a => a.Description))})";
}

#endregion

#region Optional Pattern

/// <summary>
/// Pattern that matches 0 or 1 occurrences of the inner pattern.
/// Always succeeds - returns empty match if inner doesn't match.
/// </summary>
public sealed record OptionalPattern : NodePattern
{
    private readonly NodePattern _inner;
    
    public OptionalPattern(NodePattern inner) => _inner = inner;
    
    public override bool TryMatch(RedNode node, out NodeMatch match)
    {
        if (_inner.TryMatch(node, out match))
            return true;
        
        // Optional always succeeds with empty match
        match = new NodeMatch
        {
            Position = node.Position,
            Parts = ImmutableArray<RedNode>.Empty,
            ConsumedCount = 0
        };
        return true;
    }
    
    public override bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        // Try to match inner pattern
        if (_inner.TryMatchGreen(siblings, startIndex, out consumedCount))
            return true;
        
        // Optional always succeeds with 0 consumed
        consumedCount = 0;
        return true;
    }
    
    public override string Description => $"{_inner.Description}?";
}

#endregion

#region Repeat Pattern

/// <summary>
/// Pattern that matches the inner pattern multiple times.
/// </summary>
public sealed record RepeatPattern : NodePattern
{
    private readonly NodePattern _inner;
    private readonly int _min;
    private readonly int _max;
    
    public RepeatPattern(NodePattern inner, int min, int max)
    {
        _inner = inner;
        _min = min;
        _max = max;
    }
    
    public override bool TryMatch(RedNode node, out NodeMatch match)
    {
        var parts = ImmutableArray.CreateBuilder<RedNode>();
        var current = node;
        int count = 0;
        int totalConsumed = 0;
        
        while (current != null && count < _max)
        {
            if (!_inner.TryMatch(current, out var partMatch))
                break;
            
            parts.AddRange(partMatch.Parts);
            totalConsumed += partMatch.ConsumedCount;
            count++;
            
            // Advance to next sibling
            for (int i = 0; i < partMatch.ConsumedCount; i++)
            {
                current = current?.NextSibling();
            }
        }
        
        if (count < _min)
        {
            match = NodeMatch.Empty;
            return false;
        }
        
        match = new NodeMatch
        {
            Position = node.Position,
            Parts = parts.ToImmutable(),
            ConsumedCount = totalConsumed
        };
        return true;
    }
    
    public override bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
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
    
    public override string Description => _min == 0 && _max == int.MaxValue
        ? $"{_inner.Description}*"
        : _min == 1 && _max == int.MaxValue
            ? $"{_inner.Description}+"
            : $"{_inner.Description}{{{_min},{_max}}}";
}

#endregion

#region Lookahead Pattern

/// <summary>
/// Pattern that matches a node only if the next sibling matches a lookahead condition.
/// The lookahead is not consumed (zero-width assertion).
/// </summary>
public sealed record LookaheadPattern : NodePattern
{
    private readonly NodePattern _match;
    private readonly NodePattern _lookahead;
    private readonly bool _positive;
    
    /// <summary>
    /// Creates a positive lookahead pattern.
    /// </summary>
    /// <param name="match">Pattern to match and capture.</param>
    /// <param name="lookahead">Pattern that must match next sibling (not consumed).</param>
    public LookaheadPattern(NodePattern match, NodePattern lookahead, bool positive = true)
    {
        _match = match;
        _lookahead = lookahead;
        _positive = positive;
    }
    
    public override bool TryMatch(RedNode node, out NodeMatch match)
    {
        // First, try to match the primary pattern
        if (!_match.TryMatch(node, out var primary))
        {
            match = NodeMatch.Empty;
            return false;
        }
        
        // Find the node after the matched sequence
        var current = node;
        for (int i = 0; i < primary.ConsumedCount; i++)
        {
            current = current?.NextSibling();
        }
        
        // Check lookahead condition
        bool lookaheadMatches = current != null && _lookahead.TryMatch(current, out _);
        
        if (lookaheadMatches != _positive)
        {
            match = NodeMatch.Empty;
            return false;
        }
        
        // Return only the primary match (lookahead not consumed)
        match = primary;
        return true;
    }
    
    public override bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        // First, try to match the primary pattern
        if (!_match.TryMatchGreen(siblings, startIndex, out var primaryConsumed))
        {
            consumedCount = 0;
            return false;
        }
        
        // Check lookahead at the position after primary match
        int lookaheadIndex = startIndex + primaryConsumed;
        bool lookaheadMatches = lookaheadIndex < siblings.Count && 
            _lookahead.TryMatchGreen(siblings, lookaheadIndex, out _);
        
        if (lookaheadMatches != _positive)
        {
            consumedCount = 0;
            return false;
        }
        
        // Return only the primary consumed count (lookahead not consumed)
        consumedCount = primaryConsumed;
        return true;
    }
    
    public override string Description => _positive
        ? $"{_match.Description}(?={_lookahead.Description})"
        : $"{_match.Description}(?!{_lookahead.Description})";
}

#endregion

#region Pattern Builder (Fluent API)

/// <summary>
/// Fluent builder for constructing sequence patterns.
/// </summary>
public sealed class PatternBuilder
{
    private readonly List<NodePattern> _parts = [];
    
    /// <summary>Adds a pattern that matches any identifier.</summary>
    public PatternBuilder Ident()
    {
        _parts.Add(new QueryPattern(Q.Ident));
        return this;
    }
    
    /// <summary>Adds a pattern that matches an identifier with exact text.</summary>
    public PatternBuilder Ident(string text)
    {
        _parts.Add(new QueryPattern(Q.Ident.WithText(text)));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any numeric literal.</summary>
    public PatternBuilder Numeric()
    {
        _parts.Add(new QueryPattern(Q.Numeric));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any string literal.</summary>
    public PatternBuilder String()
    {
        _parts.Add(new QueryPattern(Q.String));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any operator.</summary>
    public PatternBuilder Operator()
    {
        _parts.Add(new QueryPattern(Q.Operator));
        return this;
    }
    
    /// <summary>Adds a pattern that matches a specific operator.</summary>
    public PatternBuilder Operator(string op)
    {
        _parts.Add(new QueryPattern(Q.Operator.WithText(op)));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any symbol.</summary>
    public PatternBuilder Symbol()
    {
        _parts.Add(new QueryPattern(Q.Symbol));
        return this;
    }
    
    /// <summary>Adds a pattern that matches a specific symbol.</summary>
    public PatternBuilder Symbol(string sym)
    {
        _parts.Add(new QueryPattern(Q.Symbol.WithText(sym)));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any brace block.</summary>
    public PatternBuilder BraceBlock()
    {
        _parts.Add(new QueryPattern(Q.BraceBlock));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any bracket block.</summary>
    public PatternBuilder BracketBlock()
    {
        _parts.Add(new QueryPattern(Q.BracketBlock));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any parenthesis block.</summary>
    public PatternBuilder ParenBlock()
    {
        _parts.Add(new QueryPattern(Q.ParenBlock));
        return this;
    }
    
    /// <summary>Adds a pattern that matches any block type.</summary>
    public PatternBuilder AnyBlock()
    {
        _parts.Add(new QueryPattern(Q.AnyBlock));
        return this;
    }
    
    /// <summary>Adds a custom query pattern.</summary>
    public PatternBuilder MatchQuery(INodeQuery query)
    {
        _parts.Add(new QueryPattern(query));
        return this;
    }
    
    /// <summary>Adds a custom pattern.</summary>
    public PatternBuilder Pattern(NodePattern pattern)
    {
        _parts.Add(pattern);
        return this;
    }
    
    /// <summary>Adds an optional pattern.</summary>
    public PatternBuilder Optional(Action<PatternBuilder> configure)
    {
        var inner = new PatternBuilder();
        configure(inner);
        _parts.Add(new OptionalPattern(inner.Build()));
        return this;
    }
    
    /// <summary>Adds an alternative pattern.</summary>
    public PatternBuilder OneOf(params Action<PatternBuilder>[] alternatives)
    {
        var patterns = alternatives.Select(configure =>
        {
            var builder = new PatternBuilder();
            configure(builder);
            return builder.Build();
        }).ToArray();
        _parts.Add(new AlternativePattern(patterns));
        return this;
    }
    
    /// <summary>Builds the sequence pattern.</summary>
    public NodePattern Build()
    {
        if (_parts.Count == 1)
            return _parts[0];
        return new SequencePattern(_parts);
    }
}

#endregion
