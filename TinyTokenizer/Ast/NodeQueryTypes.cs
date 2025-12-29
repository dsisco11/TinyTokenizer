namespace TinyTokenizer.Ast;

#region Kind Query

/// <summary>
/// Matches nodes of a specific kind.
/// </summary>
public sealed record KindNodeQuery : NodeQuery
{
    /// <summary>The kind to match.</summary>
    public NodeKind Kind { get; }
    
    /// <summary>Creates a query matching nodes of the specified kind.</summary>
    public KindNodeQuery(NodeKind kind) => Kind = kind;
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (Matches(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => node.Kind == Kind;
}

#endregion

#region Block Query

/// <summary>
/// Matches block nodes, optionally filtered by opener character.
/// </summary>
public record BlockNodeQuery : NodeQuery
{
    private readonly char? _opener;
    
    /// <summary>Gets the opener character filter, or null to match any block.</summary>
    public char? Opener => _opener;
    
    /// <summary>Creates a query matching blocks with the specified opener (null for any).</summary>
    public BlockNodeQuery(char? opener = null) => _opener = opener;
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (Matches(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node)
    {
        if (node is not RedBlock block)
            return false;
        
        if (_opener == null)
            return true;
        
        return block.Opener == _opener.Value;
    }
    
    #region Block-Specific Pseudo-Selectors
    
    /// <summary>
    /// Returns a query that selects only the first matching block.
    /// </summary>
    public new BlockNodeQuery First() => new FirstBlockQuery(this);
    
    /// <summary>
    /// Returns a query that selects only the last matching block.
    /// </summary>
    public new BlockNodeQuery Last() => new LastBlockQuery(this);
    
    /// <summary>
    /// Returns a query that selects only the nth matching block (0-based).
    /// </summary>
    public new BlockNodeQuery Nth(int n) => new NthBlockQuery(this, n);
    
    #endregion
    
    /// <summary>
    /// Returns a position query for inserting at the start of block content.
    /// </summary>
    public InsertionQuery InnerStart() => new InsertionQuery(this, InsertionPoint.InnerStart);
    
    /// <summary>
    /// Returns a position query for inserting at the end of block content.
    /// </summary>
    public InsertionQuery InnerEnd() => new InsertionQuery(this, InsertionPoint.InnerEnd);
}

/// <summary>
/// Wraps a BlockNodeQuery to select only the first match, preserving block methods.
/// </summary>
public sealed record FirstBlockQuery : BlockNodeQuery
{
    private readonly BlockNodeQuery _inner;
    
    internal FirstBlockQuery(BlockNodeQuery inner) : base(inner.Opener) => _inner = inner;
    
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        foreach (var node in _inner.Select(root))
        {
            yield return node;
            yield break;
        }
    }
}

/// <summary>
/// Wraps a BlockNodeQuery to select only the last match, preserving block methods.
/// </summary>
public sealed record LastBlockQuery : BlockNodeQuery
{
    private readonly BlockNodeQuery _inner;
    
    internal LastBlockQuery(BlockNodeQuery inner) : base(inner.Opener) => _inner = inner;
    
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        RedNode? last = null;
        foreach (var node in _inner.Select(root))
        {
            last = node;
        }
        if (last != null)
            yield return last;
    }
}

/// <summary>
/// Wraps a BlockNodeQuery to select only the nth match, preserving block methods.
/// </summary>
public sealed record NthBlockQuery : BlockNodeQuery
{
    private readonly BlockNodeQuery _inner;
    private readonly int _n;
    
    internal NthBlockQuery(BlockNodeQuery inner, int n) : base(inner.Opener)
    {
        _inner = inner;
        _n = n;
    }
    
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        int count = 0;
        foreach (var node in _inner.Select(root))
        {
            if (count == _n)
            {
                yield return node;
                yield break;
            }
            count++;
        }
    }
}

#endregion

#region Any Query

/// <summary>
/// Matches any node.
/// </summary>
public sealed record AnyNodeQuery : NodeQuery
{
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root) => new TreeWalker(root).DescendantsAndSelf();
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => true;
}

#endregion

#region Leaf Query

/// <summary>
/// Matches only leaf nodes.
/// </summary>
public sealed record LeafNodeQuery : NodeQuery
{
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root, NodeFilter.Leaves);
        foreach (var node in walker.DescendantsAndSelf())
        {
            yield return node;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => node is RedLeaf;
}

#endregion

#region Pseudo-Selector Queries

/// <summary>
/// Wraps a query to select only the first match.
/// </summary>
public sealed record FirstNodeQuery : NodeQuery
{
    private readonly NodeQuery _inner;
    
    internal FirstNodeQuery(NodeQuery inner) => _inner = inner;
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        foreach (var node in _inner.Select(root))
        {
            yield return node;
            yield break;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _inner.Matches(node);
}

/// <summary>
/// Wraps a query to select only the last match.
/// </summary>
public sealed record LastNodeQuery : NodeQuery
{
    private readonly NodeQuery _inner;
    
    internal LastNodeQuery(NodeQuery inner) => _inner = inner;
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        RedNode? last = null;
        foreach (var node in _inner.Select(root))
        {
            last = node;
        }
        
        if (last != null)
            yield return last;
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _inner.Matches(node);
}

/// <summary>
/// Wraps a query to select only the nth match (0-based).
/// </summary>
public sealed record NthNodeQuery : NodeQuery
{
    private readonly NodeQuery _inner;
    private readonly int _n;
    
    internal NthNodeQuery(NodeQuery inner, int n)
    {
        _inner = inner;
        _n = n;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        int count = 0;
        foreach (var node in _inner.Select(root))
        {
            if (count == _n)
            {
                yield return node;
                yield break;
            }
            count++;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _inner.Matches(node);
}

#endregion

#region Filter Queries

/// <summary>
/// Wraps a query with an additional predicate filter.
/// </summary>
public sealed record PredicateNodeQuery : NodeQuery
{
    private readonly NodeQuery _inner;
    private readonly Func<RedNode, bool> _predicate;
    
    internal PredicateNodeQuery(NodeQuery inner, Func<RedNode, bool> predicate)
    {
        _inner = inner;
        _predicate = predicate;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        foreach (var node in _inner.Select(root))
        {
            if (_predicate(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _inner.Matches(node) && _predicate(node);
}

#endregion

#region Composition Queries

/// <summary>
/// Matches nodes that satisfy either of two queries (OR logic).
/// </summary>
public sealed record UnionNodeQuery : NodeQuery
{
    private readonly NodeQuery _left;
    private readonly NodeQuery _right;
    
    internal UnionNodeQuery(NodeQuery left, NodeQuery right)
    {
        _left = left;
        _right = right;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var seen = new HashSet<RedNode>(ReferenceEqualityComparer.Instance);
        
        foreach (var node in _left.Select(root))
        {
            if (seen.Add(node))
                yield return node;
        }
        
        foreach (var node in _right.Select(root))
        {
            if (seen.Add(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _left.Matches(node) || _right.Matches(node);
}

/// <summary>
/// Matches nodes that satisfy both queries (AND logic).
/// </summary>
public sealed record IntersectionNodeQuery : NodeQuery
{
    private readonly NodeQuery _left;
    private readonly NodeQuery _right;
    
    internal IntersectionNodeQuery(NodeQuery left, NodeQuery right)
    {
        _left = left;
        _right = right;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var leftMatches = new HashSet<RedNode>(_left.Select(root), ReferenceEqualityComparer.Instance);
        
        foreach (var node in _right.Select(root))
        {
            if (leftMatches.Contains(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _left.Matches(node) && _right.Matches(node);
}

#endregion
