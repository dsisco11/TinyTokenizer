using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Internal interface for green-layer node matching.
/// Used for efficient pattern matching without creating red trees.
/// </summary>
internal interface IGreenNodeQuery
{
    /// <summary>
    /// Tests whether a green node matches this query's criteria.
    /// </summary>
    bool MatchesGreen(GreenNode node);
    
    /// <summary>
    /// Attempts to match this query against green nodes starting at the given index.
    /// </summary>
    bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount);
}

/// <summary>
/// Non-generic interface for node queries, enabling polymorphic collections and composition.
/// Queries can match single nodes (via Matches) or sequences of siblings (via TryMatch).
/// </summary>
public interface INodeQuery
{
    /// <summary>
    /// Selects all nodes matching this query from the tree.
    /// </summary>
    IEnumerable<SyntaxNode> Select(SyntaxTree tree);
    
    /// <summary>
    /// Selects all nodes matching this query from a subtree.
    /// </summary>
    IEnumerable<SyntaxNode> Select(SyntaxNode root);
    
    /// <summary>
    /// Tests whether a single node matches this query's criteria.
    /// For sequence queries, returns true if the sequence can start at this node.
    /// </summary>
    bool Matches(SyntaxNode node);
    
    /// <summary>
    /// Attempts to match this query starting at the given node, consuming siblings.
    /// </summary>
    /// <param name="startNode">The first sibling node to try matching.</param>
    /// <param name="consumedCount">Number of sibling nodes consumed if matched.</param>
    /// <returns>True if the query matched.</returns>
    bool TryMatch(SyntaxNode startNode, out int consumedCount);
}

/// <summary>
/// Base class for node selectors using CRTP (Curiously Recurring Template Pattern).
/// The generic parameter TSelf allows filtering methods to return the derived type,
/// preserving type-specific methods through the fluent chain.
/// </summary>
/// <typeparam name="TSelf">The derived query type.</typeparam>
public abstract record NodeQuery<TSelf> : INodeQuery, IGreenNodeQuery where TSelf : NodeQuery<TSelf>
{
    /// <summary>
    /// Selects all nodes matching this query from the tree.
    /// </summary>
    public abstract IEnumerable<SyntaxNode> Select(SyntaxTree tree);
    
    /// <summary>
    /// Selects all nodes matching this query from a subtree.
    /// </summary>
    public abstract IEnumerable<SyntaxNode> Select(SyntaxNode root);
    
    /// <summary>
    /// Tests whether a single node matches this query's criteria.
    /// </summary>
    public abstract bool Matches(SyntaxNode node);
    
    /// <summary>
    /// Tests whether a green node matches this query's criteria.
    /// </summary>
    internal abstract bool MatchesGreen(GreenNode node);
    
    // Explicit interface implementation
    bool IGreenNodeQuery.MatchesGreen(GreenNode node) => MatchesGreen(node);
    
    /// <summary>
    /// Attempts to match this query starting at the given node.
    /// Default implementation for single-node queries: matches one node, consumes 1.
    /// Override for sequence queries that consume multiple siblings.
    /// </summary>
    public virtual bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        if (Matches(startNode))
        {
            consumedCount = 1;
            return true;
        }
        consumedCount = 0;
        return false;
    }
    
    /// <summary>
    /// Attempts to match this query against green nodes starting at the given index.
    /// Default implementation for single-node queries: matches one node, consumes 1.
    /// </summary>
    internal virtual bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (startIndex < siblings.Count && MatchesGreen(siblings[startIndex]))
        {
            consumedCount = 1;
            return true;
        }
        consumedCount = 0;
        return false;
    }
    
    // Explicit interface implementation
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
        => TryMatchGreen(siblings, startIndex, out consumedCount);
    
    #region CRTP Factory Methods
    
    /// <summary>Creates a filtered version of this query.</summary>
    protected abstract TSelf CreateFiltered(Func<SyntaxNode, bool> predicate);
    
    /// <summary>Creates a version selecting only the first match.</summary>
    protected abstract TSelf CreateFirst();
    
    /// <summary>Creates a version selecting only the last match.</summary>
    protected abstract TSelf CreateLast();
    
    /// <summary>Creates a version selecting only the nth match.</summary>
    protected abstract TSelf CreateNth(int n);
    
    /// <summary>Creates a version skipping the first n matches.</summary>
    protected abstract TSelf CreateSkip(int count);
    
    /// <summary>Creates a version taking only the first n matches.</summary>
    protected abstract TSelf CreateTake(int count);
    
    #endregion
    
    #region Pseudo-Selectors (return TSelf)
    
    /// <summary>
    /// Returns a query that selects only the first matching node.
    /// </summary>
    public TSelf First() => CreateFirst();
    
    /// <summary>
    /// Returns a query that selects only the last matching node.
    /// </summary>
    public TSelf Last() => CreateLast();
    
    /// <summary>
    /// Returns a query that selects only the nth matching node (0-based).
    /// </summary>
    public TSelf Nth(int n) => CreateNth(n);
    
    /// <summary>
    /// Returns a query that skips the first n matching nodes.
    /// </summary>
    public TSelf Skip(int count) => CreateSkip(count);
    
    /// <summary>
    /// Returns a query that takes only the first n matching nodes.
    /// </summary>
    public TSelf Take(int count) => CreateTake(count);
    
    /// <summary>
    /// Returns a query that selects all matching nodes.
    /// This is the default behavior but provided for fluent readability.
    /// </summary>
    public TSelf All() => (TSelf)this;
    
    #endregion
    
    #region Filters (return TSelf)
    
    /// <summary>
    /// Adds a predicate filter to this query.
    /// </summary>
    public TSelf Where(Func<SyntaxNode, bool> predicate) => CreateFiltered(predicate);
    
    /// <summary>
    /// Filters to leaf nodes with exact text match.
    /// </summary>
    public TSelf WithText(string text) => 
        CreateFiltered(n => n is SyntaxToken leaf && leaf.Text == text);
    
    /// <summary>
    /// Filters to leaf nodes whose text contains the specified substring.
    /// </summary>
    public TSelf WithTextContaining(string substring) => 
        CreateFiltered(n => n is SyntaxToken leaf && leaf.Text.Contains(substring));
    
    /// <summary>
    /// Filters to leaf nodes whose text starts with the specified prefix.
    /// </summary>
    public TSelf WithTextStartingWith(string prefix) => 
        CreateFiltered(n => n is SyntaxToken leaf && leaf.Text.StartsWith(prefix));
    
    /// <summary>
    /// Filters to leaf nodes whose text ends with the specified suffix.
    /// </summary>
    public TSelf WithTextEndingWith(string suffix) => 
        CreateFiltered(n => n is SyntaxToken leaf && leaf.Text.EndsWith(suffix));
    
    #endregion
    
    #region Composition Operators
    
    /// <summary>
    /// Creates a union query that matches nodes satisfying either query.
    /// Note: Returns INodeQuery since the result may combine different query types.
    /// </summary>
    public static INodeQuery operator |(NodeQuery<TSelf> left, INodeQuery right) => 
        new UnionNodeQuery(left, right);
    
    /// <summary>
    /// Creates an intersection query that matches nodes satisfying both queries.
    /// Note: Returns INodeQuery since the result may combine different query types.
    /// </summary>
    public static INodeQuery operator &(NodeQuery<TSelf> left, INodeQuery right) => 
        new IntersectionNodeQuery(left, right);
    
    #endregion
}
