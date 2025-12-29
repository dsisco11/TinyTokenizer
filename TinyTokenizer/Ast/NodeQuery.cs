namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for node selectors in the AST.
/// Queries select nodes by kind, content, position, or pattern matching.
/// Position modifiers (Before, After, InnerStart, InnerEnd) create insertion point queries.
/// </summary>
public abstract record NodeQuery
{
    /// <summary>
    /// Selects all nodes matching this query from the tree.
    /// </summary>
    public abstract IEnumerable<RedNode> Select(SyntaxTree tree);
    
    /// <summary>
    /// Selects all nodes matching this query from a subtree.
    /// </summary>
    public abstract IEnumerable<RedNode> Select(RedNode root);
    
    /// <summary>
    /// Tests whether a single node matches this query's criteria.
    /// </summary>
    public abstract bool Matches(RedNode node);
    
    #region Pseudo-Selectors
    
    /// <summary>
    /// Returns a query that selects only the first matching node.
    /// </summary>
    public NodeQuery First() => new FirstNodeQuery(this);
    
    /// <summary>
    /// Returns a query that selects only the last matching node.
    /// </summary>
    public NodeQuery Last() => new LastNodeQuery(this);
    
    /// <summary>
    /// Returns a query that selects only the nth matching node (0-based).
    /// </summary>
    public NodeQuery Nth(int n) => new NthNodeQuery(this, n);
    
    /// <summary>
    /// Returns a query that selects all matching nodes.
    /// This is the default behavior but provided for fluent readability.
    /// </summary>
    public NodeQuery All() => this;
    
    #endregion
    
    #region Filters
    
    /// <summary>
    /// Adds a predicate filter to this query.
    /// </summary>
    public NodeQuery Where(Func<RedNode, bool> predicate) => new PredicateNodeQuery(this, predicate);
    
    /// <summary>
    /// Filters to leaf nodes with exact text match.
    /// </summary>
    public NodeQuery WithText(string text) => 
        new PredicateNodeQuery(this, n => n is RedLeaf leaf && leaf.Text == text);
    
    /// <summary>
    /// Filters to leaf nodes whose text contains the specified substring.
    /// </summary>
    public NodeQuery WithTextContaining(string substring) => 
        new PredicateNodeQuery(this, n => n is RedLeaf leaf && leaf.Text.Contains(substring));
    
    /// <summary>
    /// Filters to leaf nodes whose text starts with the specified prefix.
    /// </summary>
    public NodeQuery WithTextStartingWith(string prefix) => 
        new PredicateNodeQuery(this, n => n is RedLeaf leaf && leaf.Text.StartsWith(prefix));
    
    /// <summary>
    /// Filters to leaf nodes whose text ends with the specified suffix.
    /// </summary>
    public NodeQuery WithTextEndingWith(string suffix) => 
        new PredicateNodeQuery(this, n => n is RedLeaf leaf && leaf.Text.EndsWith(suffix));
    
    #endregion
    
    #region Position Modifiers
    
    /// <summary>
    /// Returns a position query for inserting before each matched node.
    /// </summary>
    public InsertionQuery Before() => new InsertionQuery(this, InsertionPoint.Before);
    
    /// <summary>
    /// Returns a position query for inserting after each matched node.
    /// </summary>
    public InsertionQuery After() => new InsertionQuery(this, InsertionPoint.After);
    
    #endregion
    
    #region Composition Operators
    
    /// <summary>
    /// Creates a union query that matches nodes satisfying either query.
    /// </summary>
    public static NodeQuery operator |(NodeQuery left, NodeQuery right) => 
        new UnionNodeQuery(left, right);
    
    /// <summary>
    /// Creates an intersection query that matches nodes satisfying both queries.
    /// </summary>
    public static NodeQuery operator &(NodeQuery left, NodeQuery right) => 
        new IntersectionNodeQuery(left, right);
    
    #endregion
}

/// <summary>
/// Specifies where to insert relative to a matched node.
/// </summary>
public enum InsertionPoint
{
    /// <summary>Insert before the matched node.</summary>
    Before,
    /// <summary>Insert after the matched node.</summary>
    After,
    /// <summary>Insert at the start of a block's content (after opening delimiter).</summary>
    InnerStart,
    /// <summary>Insert at the end of a block's content (before closing delimiter).</summary>
    InnerEnd,
}

/// <summary>
/// A query that specifies an insertion position relative to matched nodes.
/// </summary>
public sealed record InsertionQuery
{
    /// <summary>Gets the underlying node query.</summary>
    public NodeQuery InnerQuery { get; }
    
    /// <summary>Gets the insertion point relative to matched nodes.</summary>
    public InsertionPoint Point { get; }
    
    internal InsertionQuery(NodeQuery inner, InsertionPoint point)
    {
        InnerQuery = inner;
        Point = point;
    }
    
    /// <summary>
    /// Resolves insertion positions for all matched nodes.
    /// Returns (parent path, child index) pairs for insertion.
    /// </summary>
    public IEnumerable<(NodePath ParentPath, int ChildIndex)> ResolvePositions(SyntaxTree tree)
    {
        foreach (var node in InnerQuery.Select(tree))
        {
            var position = ResolvePosition(node);
            if (position.HasValue)
                yield return position.Value;
        }
    }
    
    private (NodePath ParentPath, int ChildIndex)? ResolvePosition(RedNode node)
    {
        var parent = node.Parent;
        if (parent == null)
            return null; // Can't insert relative to root
        
        // Find the index of this node in its parent
        int childIndex = -1;
        for (int i = 0; i < parent.SlotCount; i++)
        {
            if (ReferenceEquals(parent.GetChild(i), node))
            {
                childIndex = i;
                break;
            }
        }
        
        if (childIndex < 0)
            return null;
        
        var parentPath = NodePath.FromNode(parent);
        
        return Point switch
        {
            InsertionPoint.Before => (parentPath, childIndex),
            InsertionPoint.After => (parentPath, childIndex + 1),
            InsertionPoint.InnerStart when node is RedBlock => (NodePath.FromNode(node), 0),
            InsertionPoint.InnerEnd when node is RedBlock block => (NodePath.FromNode(node), block.ChildCount),
            _ => null
        };
    }
}
