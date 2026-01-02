namespace TinyTokenizer.Ast;

/// <summary>
/// Extension methods for composing queries using fluent syntax.
/// </summary>
public static class QueryExtensions
{
    #region Optional
    
    /// <summary>
    /// Makes this query optional (matches zero or one occurrence).
    /// </summary>
    public static OptionalQuery Optional(this INodeQuery query) => new(query);
    
    #endregion
    
    #region Repeat
    
    /// <summary>
    /// Matches this query zero or more times.
    /// </summary>
    public static RepeatQuery ZeroOrMore(this INodeQuery query) => new(query, 0, int.MaxValue);
    
    /// <summary>
    /// Matches this query one or more times.
    /// </summary>
    public static RepeatQuery OneOrMore(this INodeQuery query) => new(query, 1, int.MaxValue);
    
    /// <summary>
    /// Matches this query exactly n times.
    /// </summary>
    public static RepeatQuery Exactly(this INodeQuery query, int count) => new(query, count, count);
    
    /// <summary>
    /// Matches this query between min and max times (inclusive).
    /// </summary>
    public static RepeatQuery Repeat(this INodeQuery query, int min, int max) => new(query, min, max);
    
    /// <summary>
    /// Matches this query at least min times.
    /// </summary>
    public static RepeatQuery AtLeast(this INodeQuery query, int min) => new(query, min, int.MaxValue);
    
    /// <summary>
    /// Matches this query at most max times.
    /// </summary>
    public static RepeatQuery AtMost(this INodeQuery query, int max) => new(query, 0, max);
    
    #endregion
    
    #region Until
    
    /// <summary>
    /// Matches this query repeatedly until the terminator is encountered.
    /// The terminator is NOT consumed.
    /// </summary>
    public static RepeatUntilQuery Until(this INodeQuery query, INodeQuery terminator) => new(query, terminator);
    
    #endregion
    
    #region Lookahead
    
    /// <summary>
    /// Matches this query only if followed by the lookahead query.
    /// The lookahead is not consumed (zero-width positive assertion).
    /// </summary>
    public static LookaheadQuery FollowedBy(this INodeQuery query, INodeQuery lookahead) => new(query, lookahead, positive: true);
    
    /// <summary>
    /// Matches this query only if NOT followed by the lookahead query.
    /// The lookahead is not consumed (zero-width negative assertion).
    /// </summary>
    public static LookaheadQuery NotFollowedBy(this INodeQuery query, INodeQuery lookahead) => new(query, lookahead, positive: false);
    
    #endregion
    
    #region Negation
    
    /// <summary>
    /// Creates a zero-width negative lookahead assertion.
    /// Succeeds when this query does NOT match, without consuming any nodes.
    /// </summary>
    /// <remarks>
    /// Use in sequences to assert absence:
    /// <code>Query.AnyIdent.Not().Then(Query.AnyIdent)</code>
    /// </remarks>
    public static NotQuery Not(this INodeQuery query) => new(query);
    
    #endregion
    
    #region AnyOf / NoneOf
    
    /// <summary>
    /// Creates a query that matches this query OR any of the provided queries.
    /// </summary>
    public static AnyOfQuery Or(this INodeQuery query, params INodeQuery[] others)
    {
        var all = new INodeQuery[others.Length + 1];
        all[0] = query;
        Array.Copy(others, 0, all, 1, others.Length);
        return new AnyOfQuery(all);
    }
    
    /// <summary>
    /// Creates a query that matches if this query AND all others do NOT match.
    /// Consumes 1 node when all queries fail.
    /// </summary>
    public static NoneOfQuery ExceptFor(this INodeQuery query, params INodeQuery[] others)
    {
        var all = new INodeQuery[others.Length + 1];
        all[0] = query;
        Array.Copy(others, 0, all, 1, others.Length);
        return new NoneOfQuery(all);
    }
    
    #endregion
    
    #region Between
    
    /// <summary>
    /// Creates a query matching content between this query (start) and the end query.
    /// Consumes all nodes from start through end (inclusive).
    /// </summary>
    public static BetweenQuery Between(this INodeQuery start, INodeQuery end, bool inclusive = true) =>
        new(start, end, inclusive);
    
    #endregion
    
    #region Navigation
    
    /// <summary>
    /// Creates a query that checks the sibling at the specified offset.
    /// Zero-width - navigates without consuming the current node.
    /// </summary>
    /// <param name="query">The query to use for matching the sibling.</param>
    /// <param name="offset">Relative offset: +1 for next, -1 for previous.</param>
    public static SiblingQuery AtSibling(this INodeQuery query, int offset) => new(offset, query);
    
    /// <summary>
    /// Creates a query that checks the next sibling (+1 offset).
    /// </summary>
    public static SiblingQuery NextSibling(this INodeQuery query) => new(1, query);
    
    /// <summary>
    /// Creates a query that checks the previous sibling (-1 offset).
    /// </summary>
    public static SiblingQuery PreviousSibling(this INodeQuery query) => new(-1, query);
    
    /// <summary>
    /// Creates a query that checks if the parent matches this query.
    /// </summary>
    public static ParentQuery AsParent(this INodeQuery query) => new(query);
    
    /// <summary>
    /// Creates a query that checks if any ancestor matches this query.
    /// </summary>
    public static AncestorQuery AsAncestor(this INodeQuery query) => new(query);
    
    #endregion
    
    #region Sequence Building (Then)
    
    /// <summary>
    /// Creates a sequence of this query followed by another query.
    /// </summary>
    public static SequenceQuery Then(this INodeQuery first, INodeQuery second) => new(first, second);
    
    /// <summary>
    /// Appends another query to this sequence.
    /// </summary>
    public static SequenceQuery Then(this SequenceQuery sequence, INodeQuery next)
    {
        // Flatten to avoid nested sequences
        var parts = new List<INodeQuery>();
        foreach (var node in GetSequenceParts(sequence))
            parts.Add(node);
        parts.Add(next);
        return new SequenceQuery(parts);
    }
    
    private static IEnumerable<INodeQuery> GetSequenceParts(SequenceQuery sequence)
    {
        // Use reflection to get the parts - or we could expose them
        // For now, let's add a property to SequenceQuery
        return sequence.Parts;
    }
    
    #endregion
}
