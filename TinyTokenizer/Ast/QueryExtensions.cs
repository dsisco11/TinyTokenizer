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
