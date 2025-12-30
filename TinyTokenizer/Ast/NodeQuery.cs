using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Non-generic interface for node queries, enabling polymorphic collections and composition.
/// Queries can match single nodes (via Matches) or sequences of siblings (via TryMatch).
/// </summary>
public interface INodeQuery
{
    /// <summary>
    /// Selects all nodes matching this query from the tree.
    /// </summary>
    IEnumerable<RedNode> Select(SyntaxTree tree);
    
    /// <summary>
    /// Selects all nodes matching this query from a subtree.
    /// </summary>
    IEnumerable<RedNode> Select(RedNode root);
    
    /// <summary>
    /// Tests whether a single node matches this query's criteria.
    /// For sequence queries, returns true if the sequence can start at this node.
    /// </summary>
    bool Matches(RedNode node);
    
    /// <summary>
    /// Tests whether a green node matches this query's criteria.
    /// Used for efficient pattern matching without creating red trees.
    /// </summary>
    bool MatchesGreen(GreenNode node);
    
    /// <summary>
    /// Attempts to match this query starting at the given node, consuming siblings.
    /// </summary>
    /// <param name="startNode">The first sibling node to try matching.</param>
    /// <param name="consumedCount">Number of sibling nodes consumed if matched.</param>
    /// <returns>True if the query matched.</returns>
    bool TryMatch(RedNode startNode, out int consumedCount);
    
    /// <summary>
    /// Attempts to match this query against green nodes starting at the given index.
    /// Used for efficient pattern matching without creating red trees.
    /// </summary>
    /// <param name="siblings">The sibling green nodes to match against.</param>
    /// <param name="startIndex">Index of the first sibling to try matching.</param>
    /// <param name="consumedCount">Number of siblings consumed if matched.</param>
    /// <returns>True if the query matched.</returns>
    bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount);
}

/// <summary>
/// Base class for node selectors using CRTP (Curiously Recurring Template Pattern).
/// The generic parameter TSelf allows filtering methods to return the derived type,
/// preserving type-specific methods through the fluent chain.
/// </summary>
/// <typeparam name="TSelf">The derived query type.</typeparam>
public abstract record NodeQuery<TSelf> : INodeQuery where TSelf : NodeQuery<TSelf>
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
    
    /// <summary>
    /// Tests whether a green node matches this query's criteria.
    /// Default implementation checks kind; override for more specific matching.
    /// </summary>
    public abstract bool MatchesGreen(GreenNode node);
    
    /// <summary>
    /// Attempts to match this query starting at the given node.
    /// Default implementation for single-node queries: matches one node, consumes 1.
    /// Override for sequence queries that consume multiple siblings.
    /// </summary>
    public virtual bool TryMatch(RedNode startNode, out int consumedCount)
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
    /// Override for sequence queries that consume multiple siblings.
    /// </summary>
    public virtual bool TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (startIndex < siblings.Count && MatchesGreen(siblings[startIndex]))
        {
            consumedCount = 1;
            return true;
        }
        consumedCount = 0;
        return false;
    }
    
    #region CRTP Factory Methods
    
    /// <summary>Creates a filtered version of this query.</summary>
    protected abstract TSelf CreateFiltered(Func<RedNode, bool> predicate);
    
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
    public TSelf Where(Func<RedNode, bool> predicate) => CreateFiltered(predicate);
    
    /// <summary>
    /// Filters to leaf nodes with exact text match.
    /// </summary>
    public TSelf WithText(string text) => 
        CreateFiltered(n => n is RedLeaf leaf && leaf.Text == text);
    
    /// <summary>
    /// Filters to leaf nodes whose text contains the specified substring.
    /// </summary>
    public TSelf WithTextContaining(string substring) => 
        CreateFiltered(n => n is RedLeaf leaf && leaf.Text.Contains(substring));
    
    /// <summary>
    /// Filters to leaf nodes whose text starts with the specified prefix.
    /// </summary>
    public TSelf WithTextStartingWith(string prefix) => 
        CreateFiltered(n => n is RedLeaf leaf && leaf.Text.StartsWith(prefix));
    
    /// <summary>
    /// Filters to leaf nodes whose text ends with the specified suffix.
    /// </summary>
    public TSelf WithTextEndingWith(string suffix) => 
        CreateFiltered(n => n is RedLeaf leaf && leaf.Text.EndsWith(suffix));
    
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
    /// <summary>Insert at the start of a named block's content (for IBlockContainerNode).</summary>
    NamedBlockInnerStart,
    /// <summary>Insert at the end of a named block's content (for IBlockContainerNode).</summary>
    NamedBlockInnerEnd,
}

/// <summary>
/// A query that specifies an insertion position relative to matched nodes.
/// </summary>
public sealed record InsertionQuery
{
    /// <summary>Gets the underlying node query.</summary>
    public INodeQuery InnerQuery { get; }
    
    /// <summary>Gets the insertion point relative to matched nodes.</summary>
    public InsertionPoint Point { get; }
    
    /// <summary>Gets the block name for named block insertion points, or null.</summary>
    public string? BlockName { get; }
    
    internal InsertionQuery(INodeQuery inner, InsertionPoint point, string? blockName = null)
    {
        InnerQuery = inner;
        Point = point;
        BlockName = blockName;
    }
    
    /// <summary>
    /// Resolves insertion positions for all matched nodes.
    /// Returns tuples containing path, index, position, and trivia context for proper insertion.
    /// </summary>
    public IEnumerable<InsertionPosition> ResolvePositions(SyntaxTree tree)
    {
        foreach (var node in InnerQuery.Select(tree))
        {
            var position = ResolvePosition(node);
            if (position.HasValue)
                yield return position.Value;
        }
    }
    
    private InsertionPosition? ResolvePosition(RedNode node)
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
        var targetPath = NodePath.FromNode(node);
        
        // Get trivia from target node for Before/After insertions
        var (targetLeading, targetTrailing) = GetNodeTrivia(node);
        
        return Point switch
        {
            InsertionPoint.Before => new InsertionPosition(
                parentPath, childIndex, node.Position, Point, targetPath, targetLeading, targetTrailing),
            InsertionPoint.After => new InsertionPosition(
                parentPath, childIndex + 1, node.EndPosition, Point, targetPath, targetLeading, targetTrailing),
            InsertionPoint.InnerStart when node is RedBlock block => new InsertionPosition(
                NodePath.FromNode(node), 0, block.Position + 1, Point, null, 
                ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty),
            InsertionPoint.InnerEnd when node is RedBlock block => new InsertionPosition(
                NodePath.FromNode(node), block.ChildCount, block.EndPosition - 1, Point, null,
                ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty),
            InsertionPoint.NamedBlockInnerStart when node is IBlockContainerNode container => 
                ResolveNamedBlockPosition(node, container.GetBlock(BlockName), isStart: true),
            InsertionPoint.NamedBlockInnerEnd when node is IBlockContainerNode container => 
                ResolveNamedBlockPosition(node, container.GetBlock(BlockName), isStart: false),
            _ => null
        };
    }
    
    private static InsertionPosition ResolveNamedBlockPosition(RedNode syntaxNode, RedBlock block, bool isStart)
    {
        var blockPath = NodePath.FromNode(block);
        
        if (isStart)
        {
            return new InsertionPosition(
                blockPath, 0, block.Position + 1, InsertionPoint.InnerStart, null,
                ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty);
        }
        else
        {
            return new InsertionPosition(
                blockPath, block.ChildCount, block.EndPosition - 1, InsertionPoint.InnerEnd, null,
                ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty);
        }
    }
    
    private static (ImmutableArray<GreenTrivia> Leading, ImmutableArray<GreenTrivia> Trailing) GetNodeTrivia(RedNode node)
    {
        return node.Green switch
        {
            GreenLeaf leaf => (leaf.LeadingTrivia, leaf.TrailingTrivia),
            GreenBlock block => (block.LeadingTrivia, block.TrailingTrivia),
            _ => (ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty)
        };
    }
}

/// <summary>
/// Contains all information needed to perform an insertion.
/// </summary>
public readonly record struct InsertionPosition(
    NodePath ParentPath,
    int ChildIndex,
    int Position,
    InsertionPoint Point,
    NodePath? TargetPath,
    ImmutableArray<GreenTrivia> TargetLeadingTrivia,
    ImmutableArray<GreenTrivia> TargetTrailingTrivia);
