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
/// Interface for queries that require schema resolution before green-tree matching.
/// Used by queries like <see cref="Query.Keyword(string)"/> that need to resolve
/// keyword text to NodeKind values for efficient matching.
/// </summary>
/// <remarks>
/// SyntaxBinder checks for this interface and calls <see cref="ResolveWithSchema"/>
/// before performing green-tree pattern matching. This allows queries to cache
/// schema-derived data (like NodeKind values) for O(1) matching instead of string comparison.
/// </remarks>
internal interface ISchemaResolvableQuery
{
    /// <summary>
    /// Resolves schema-dependent data before matching.
    /// Called by SyntaxBinder before green-tree matching, and by Select(SyntaxTree) for red-tree queries.
    /// </summary>
    /// <param name="schema">The schema to resolve against.</param>
    /// <remarks>
    /// Implementations should cache resolved data and set a flag indicating resolution is complete.
    /// This method may be called multiple times; subsequent calls should be no-ops.
    /// </remarks>
    void ResolveWithSchema(Schema schema);
    
    /// <summary>
    /// Gets whether this query has been resolved with a schema.
    /// Queries return no matches if not resolved (schemaless trees).
    /// </summary>
    bool IsResolved { get; }
}

/// <summary>
/// Non-generic interface for node queries, enabling polymorphic collections and composition.
/// Queries can match single nodes (via Matches) or sequences of siblings (via TryMatch).
/// </summary>
public interface INodeQuery
{
    /// <summary>
    /// Selects all nodes matching this query from the tree.
    /// For range queries (e.g., BetweenQuery), returns only the start node of each match.
    /// </summary>
    IEnumerable<SyntaxNode> Select(SyntaxTree tree);
    
    /// <summary>
    /// Selects all nodes matching this query from a subtree.
    /// For range queries, returns only the start node of each match.
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
public abstract record NodeQuery<TSelf> : INodeQuery, IGreenNodeQuery, IRegionQuery where TSelf : NodeQuery<TSelf>
{
    /// <summary>
    /// Selects all nodes matching this query from the tree.
    /// </summary>
    public abstract IEnumerable<SyntaxNode> Select(SyntaxTree tree);
    
    /// <summary>
    /// Selects all nodes matching this query from a subtree.
    /// </summary>
    public abstract IEnumerable<SyntaxNode> Select(SyntaxNode root);
    
    #region IRegionQuery Implementation
    
    /// <summary>
    /// Resolves this query to regions in the tree.
    /// Default implementation calls <see cref="SelectRegionsFromTree"/> which can be overridden
    /// by schema-resolvable queries to resolve before matching.
    /// </summary>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxTree tree) 
        => SelectRegionsFromTree(tree);
    
    /// <summary>
    /// Selects regions from a tree. Override in derived classes to add schema resolution.
    /// Default implementation just calls <see cref="SelectRegionsCore"/> with tree.Root.
    /// </summary>
    internal virtual IEnumerable<QueryRegion> SelectRegionsFromTree(SyntaxTree tree)
        => SelectRegionsCore(tree.Root);
    
    /// <summary>
    /// Resolves this query to regions in a subtree.
    /// </summary>
    IEnumerable<QueryRegion> IRegionQuery.SelectRegions(SyntaxNode root) 
        => SelectRegionsCore(root);
    
    /// <summary>
    /// Default region resolution: traverses tree with PathTrackingWalker, calls TryMatch once per node,
    /// then applies selection filtering (First/Last/Nth via ApplyRegionFilter).
    /// Uses incremental path tracking for O(1) per node instead of O(depth).
    /// </summary>
    internal virtual IEnumerable<QueryRegion> SelectRegionsCore(SyntaxNode root)
    {
        return ApplyRegionFilter(SelectAllRegions(root));
    }
    
    /// <summary>
    /// Traverses tree and yields a region for each matching node.
    /// Uses PathTrackingWalker for O(1) path computation per node.
    /// </summary>
    private IEnumerable<QueryRegion> SelectAllRegions(SyntaxNode root)
    {
        var walker = new PathTrackingWalker(root);
        foreach (var (node, parentPath) in walker.DescendantsAndSelfWithPath())
        {
            if (TryMatch(node, out var consumedCount))
            {
                var parent = node.Parent;
                if (parent != null)
                {
                    yield return new QueryRegion(
                        parentPath: parentPath,
                        parent: parent,
                        startSlot: node.SiblingIndex,
                        endSlot: node.SiblingIndex + consumedCount,
                        firstNode: node,
                        position: node.Position
                    );
                }
            }
        }
    }
    
    /// <summary>
    /// Gets the selection mode for this query. Override in derived classes.
    /// </summary>
    internal virtual SelectionMode Mode => SelectionMode.All;
    
    /// <summary>
    /// Gets the selection mode argument (e.g., N for Nth, count for Skip/Take).
    /// </summary>
    internal virtual int ModeArg => 0;
    
    /// <summary>
    /// Applies selection mode filtering (First/Last/Nth/Skip/Take) to regions.
    /// Uses <see cref="Mode"/> and <see cref="ModeArg"/> properties.
    /// </summary>
    internal IEnumerable<QueryRegion> ApplyRegionFilter(IEnumerable<QueryRegion> regions) =>
        SelectionModeHelper.Apply(regions, Mode, ModeArg);
    
    #endregion
    
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
