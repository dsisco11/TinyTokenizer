namespace TinyTokenizer.Ast;

#region Kind Query

/// <summary>
/// Matches nodes of a specific kind.
/// </summary>
public sealed record KindNodeQuery : NodeQuery<KindNodeQuery>
{
    /// <summary>The kind to match.</summary>
    public NodeKind Kind { get; }
    
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    private readonly string? _textConstraint;
    
    /// <summary>Creates a query matching nodes of the specified kind.</summary>
    public KindNodeQuery(NodeKind kind) : this(kind, null, SelectionMode.All, 0, null) { }
    
    private KindNodeQuery(NodeKind kind, Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg, string? textConstraint = null)
    {
        Kind = kind;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
        _textConstraint = textConstraint;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf().Where(Matches);
        
        return _mode switch
        {
            SelectionMode.First => matches.Take(1),
            SelectionMode.Last => matches.TakeLast(1),
            SelectionMode.Nth => matches.Skip(_modeArg).Take(1),
            SelectionMode.Skip => matches.Skip(_modeArg),
            SelectionMode.Take => matches.Take(_modeArg),
            _ => matches
        };
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => 
        node.Kind == Kind && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node)
    {
        if (node.Kind != Kind)
            return false;
        
        // Check text constraint at green level for efficient pattern matching
        if (_textConstraint != null && node is GreenLeaf leaf && leaf.Text != _textConstraint)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Filters to leaf nodes with exact text match.
    /// Overridden to enable green-level text matching for efficient pattern binding.
    /// </summary>
    public new KindNodeQuery WithText(string text) =>
        new(Kind, CombinePredicates(_predicate, n => n is RedLeaf leaf && leaf.Text == text), _mode, _modeArg, text);
    
    protected override KindNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(Kind, CombinePredicates(_predicate, predicate), _mode, _modeArg, _textConstraint);
    
    protected override KindNodeQuery CreateFirst() => new(Kind, _predicate, SelectionMode.First, 0, _textConstraint);
    protected override KindNodeQuery CreateLast() => new(Kind, _predicate, SelectionMode.Last, 0, _textConstraint);
    protected override KindNodeQuery CreateNth(int n) => new(Kind, _predicate, SelectionMode.Nth, n, _textConstraint);
    protected override KindNodeQuery CreateSkip(int count) => new(Kind, _predicate, SelectionMode.Skip, count, _textConstraint);
    protected override KindNodeQuery CreateTake(int count) => new(Kind, _predicate, SelectionMode.Take, count, _textConstraint);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

#endregion

#region Block Query

/// <summary>
/// Matches block nodes, optionally filtered by opener character.
/// </summary>
public record BlockNodeQuery : NodeQuery<BlockNodeQuery>
{
    private readonly char? _opener;
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    /// <summary>Gets the opener character filter, or null to match any block.</summary>
    public char? Opener => _opener;
    
    /// <summary>Creates a query matching blocks with the specified opener (null for any).</summary>
    public BlockNodeQuery(char? opener = null) : this(opener, null, SelectionMode.All, 0) { }
    
    private protected BlockNodeQuery(char? opener, Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _opener = opener;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf().Where(Matches);
        
        return _mode switch
        {
            SelectionMode.First => matches.Take(1),
            SelectionMode.Last => matches.TakeLast(1),
            SelectionMode.Nth => matches.Skip(_modeArg).Take(1),
            SelectionMode.Skip => matches.Skip(_modeArg),
            SelectionMode.Take => matches.Take(_modeArg),
            _ => matches
        };
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node)
    {
        if (node is not RedBlock block)
            return false;
        
        if (_opener != null && block.Opener != _opener.Value)
            return false;
        
        return _predicate == null || _predicate(node);
    }
    
    internal override bool MatchesGreen(GreenNode node) => 
        node is GreenBlock block && (_opener == null || block.Opener == _opener);
    
    protected override BlockNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(_opener, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override BlockNodeQuery CreateFirst() => new(_opener, _predicate, SelectionMode.First, 0);
    protected override BlockNodeQuery CreateLast() => new(_opener, _predicate, SelectionMode.Last, 0);
    protected override BlockNodeQuery CreateNth(int n) => new(_opener, _predicate, SelectionMode.Nth, n);
    protected override BlockNodeQuery CreateSkip(int count) => new(_opener, _predicate, SelectionMode.Skip, count);
    protected override BlockNodeQuery CreateTake(int count) => new(_opener, _predicate, SelectionMode.Take, count);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
    
    /// <summary>
    /// Returns a position query for inserting at the start of block content.
    /// </summary>
    public InsertionQuery InnerStart() => new InsertionQuery(this, InsertionPoint.InnerStart);
    
    /// <summary>
    /// Returns a position query for inserting at the end of block content.
    /// </summary>
    public InsertionQuery InnerEnd() => new InsertionQuery(this, InsertionPoint.InnerEnd);
}

#endregion

#region Any Query

/// <summary>
/// Matches any node.
/// </summary>
public sealed record AnyNodeQuery : NodeQuery<AnyNodeQuery>
{
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public AnyNodeQuery() : this(null, SelectionMode.All, 0) { }
    
    private AnyNodeQuery(Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var matches = new TreeWalker(root).DescendantsAndSelf();
        if (_predicate != null)
            matches = matches.Where(_predicate);
        
        return _mode switch
        {
            SelectionMode.First => matches.Take(1),
            SelectionMode.Last => matches.TakeLast(1),
            SelectionMode.Nth => matches.Skip(_modeArg).Take(1),
            SelectionMode.Skip => matches.Skip(_modeArg),
            SelectionMode.Take => matches.Take(_modeArg),
            _ => matches
        };
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => _predicate == null || _predicate(node);
    
    internal override bool MatchesGreen(GreenNode node) => true;
    
    protected override AnyNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override AnyNodeQuery CreateFirst() => new(_predicate, SelectionMode.First, 0);
    protected override AnyNodeQuery CreateLast() => new(_predicate, SelectionMode.Last, 0);
    protected override AnyNodeQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n);
    protected override AnyNodeQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count);
    protected override AnyNodeQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

#endregion

#region Leaf Query

/// <summary>
/// Matches only leaf nodes.
/// </summary>
public sealed record LeafNodeQuery : NodeQuery<LeafNodeQuery>
{
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public LeafNodeQuery() : this(null, SelectionMode.All, 0) { }
    
    private LeafNodeQuery(Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root, NodeFilter.Leaves);
        var matches = walker.DescendantsAndSelf().Where(Matches);
        
        return _mode switch
        {
            SelectionMode.First => matches.Take(1),
            SelectionMode.Last => matches.TakeLast(1),
            SelectionMode.Nth => matches.Skip(_modeArg).Take(1),
            SelectionMode.Skip => matches.Skip(_modeArg),
            SelectionMode.Take => matches.Take(_modeArg),
            _ => matches
        };
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => 
        node is RedLeaf && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => node is GreenLeaf;
    
    protected override LeafNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override LeafNodeQuery CreateFirst() => new(_predicate, SelectionMode.First, 0);
    protected override LeafNodeQuery CreateLast() => new(_predicate, SelectionMode.Last, 0);
    protected override LeafNodeQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n);
    protected override LeafNodeQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count);
    protected override LeafNodeQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

#endregion

#region Newline Query

/// <summary>
/// Matches nodes that represent or are preceded by a newline.
/// Checks:
/// 1. The node itself is a whitespace token containing newline characters.
/// 2. The node's leading trivia contains a newline.
/// 3. The previous sibling's trailing trivia contains a newline.
/// </summary>
/// <remarks>
/// This query is particularly useful for line-based pattern matching, such as
/// matching directive lines that should consume tokens until a newline.
/// </remarks>
public sealed record NewlineNodeQuery : NodeQuery<NewlineNodeQuery>
{
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    private readonly bool _negated;
    
    public NewlineNodeQuery(bool negated = false) : this(null, SelectionMode.All, 0, negated) { }
    
    private NewlineNodeQuery(Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg, bool negated = false)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
        _negated = negated;
    }
    
    /// <summary>
    /// Returns a negated query that matches nodes NOT preceded by a newline.
    /// </summary>
    public NewlineNodeQuery Negate() => new(_predicate, _mode, _modeArg, !_negated);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf().Where(Matches);
        
        return _mode switch
        {
            SelectionMode.First => matches.Take(1),
            SelectionMode.Last => matches.TakeLast(1),
            SelectionMode.Nth => matches.Skip(_modeArg).Take(1),
            SelectionMode.Skip => matches.Skip(_modeArg),
            SelectionMode.Take => matches.Take(_modeArg),
            _ => matches
        };
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node)
    {
        bool hasNewline = HasNewline(node);
        bool result = _negated ? !hasNewline : hasNewline;
        return result && (_predicate == null || _predicate(node));
    }
    
    internal override bool MatchesGreen(GreenNode node)
    {
        bool hasNewline = HasGreenNewline(node);
        return _negated ? !hasNewline : hasNewline;
    }
    
    private static bool HasGreenNewline(GreenNode node)
    {
        // Check leading trivia for newline
        var leadingTrivia = node switch
        {
            GreenLeaf gl => gl.LeadingTrivia,
            GreenBlock gb => gb.LeadingTrivia,
            _ => System.Collections.Immutable.ImmutableArray<GreenTrivia>.Empty
        };
        
        foreach (var t in leadingTrivia)
        {
            if (t.Kind == TriviaKind.Newline)
                return true;
        }
        
        return false;
    }
    
    private static bool HasNewline(RedNode node)
    {
        // Check 1: Does leading trivia contain newline?
        var leadingTrivia = node.Green switch
        {
            GreenLeaf gl => gl.LeadingTrivia,
            GreenBlock gb => gb.LeadingTrivia,
            _ => System.Collections.Immutable.ImmutableArray<GreenTrivia>.Empty
        };
        
        foreach (var t in leadingTrivia)
        {
            if (t.Kind == TriviaKind.Newline)
                return true;
        }
        
        // Check 2: Does previous sibling's trailing trivia contain newline?
        var prev = node.PreviousSibling();
        if (prev != null)
        {
            var trailingTrivia = prev.Green switch
            {
                GreenLeaf gl => gl.TrailingTrivia,
                GreenBlock gb => gb.TrailingTrivia,
                _ => System.Collections.Immutable.ImmutableArray<GreenTrivia>.Empty
            };
            
            foreach (var t in trailingTrivia)
            {
                if (t.Kind == TriviaKind.Newline)
                    return true;
            }
        }
        
        return false;
    }
    
    protected override NewlineNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg, _negated);
    
    protected override NewlineNodeQuery CreateFirst() => new(_predicate, SelectionMode.First, 0, _negated);
    protected override NewlineNodeQuery CreateLast() => new(_predicate, SelectionMode.Last, 0, _negated);
    protected override NewlineNodeQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n, _negated);
    protected override NewlineNodeQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count, _negated);
    protected override NewlineNodeQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count, _negated);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

#endregion

#region Selection Mode

/// <summary>
/// Internal enum for selection mode in queries.
/// </summary>
internal enum SelectionMode
{
    All,
    First,
    Last,
    Nth,
    Skip,
    Take
}

#endregion

#region Composition Queries

/// <summary>
/// Matches nodes that satisfy either of two queries (OR logic).
/// For sequence matching, tries left first, then right if left fails.
/// </summary>
public sealed record UnionNodeQuery : INodeQuery
{
    private readonly INodeQuery _left;
    private readonly INodeQuery _right;
    
    internal UnionNodeQuery(INodeQuery left, INodeQuery right)
    {
        _left = left;
        _right = right;
    }
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root)
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
    public bool Matches(RedNode node) => _left.Matches(node) || _right.Matches(node);
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        // Try left first (like alternation - first match wins)
        if (_left.TryMatch(startNode, out consumedCount))
            return true;
        return _right.TryMatch(startNode, out consumedCount);
    }
}

/// <summary>
/// Matches nodes that satisfy both queries (AND logic).
/// For sequence matching, both must match and consume the same count.
/// </summary>
public sealed record IntersectionNodeQuery : INodeQuery
{
    private readonly INodeQuery _left;
    private readonly INodeQuery _right;
    
    internal IntersectionNodeQuery(INodeQuery left, INodeQuery right)
    {
        _left = left;
        _right = right;
    }
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<RedNode> Select(RedNode root)
    {
        var leftMatches = new HashSet<RedNode>(_left.Select(root), ReferenceEqualityComparer.Instance);
        
        foreach (var node in _right.Select(root))
        {
            if (leftMatches.Contains(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(RedNode node) => _left.Matches(node) && _right.Matches(node);
    
    /// <inheritdoc/>
    public bool TryMatch(RedNode startNode, out int consumedCount)
    {
        // Both must match with same consumed count
        if (_left.TryMatch(startNode, out var leftCount) && 
            _right.TryMatch(startNode, out var rightCount) &&
            leftCount == rightCount)
        {
            consumedCount = leftCount;
            return true;
        }
        consumedCount = 0;
        return false;
    }
}

#endregion

#region Exact Node Query

/// <summary>
/// Matches a specific node instance by reference equality.
/// Used when you have a RedNode reference and want to create a query targeting exactly that node.
/// </summary>
/// <remarks>
/// RedNodes are ephemeral - they are recreated on tree mutations. This query is intended for
/// immediate use within a single tree traversal, not for queries that survive tree changes.
/// </remarks>
public sealed record ExactNodeQuery : NodeQuery<ExactNodeQuery>
{
    private readonly RedNode _target;
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    /// <summary>Creates a query matching the exact node instance.</summary>
    public ExactNodeQuery(RedNode target) : this(target, null, SelectionMode.All, 0) { }
    
    private ExactNodeQuery(RedNode target, Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _target = target;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <summary>The target node this query matches.</summary>
    public RedNode Target => _target;
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(SyntaxTree tree)
    {
        // If the target node matches our criteria, return it
        if (Matches(_target))
            return [_target];
        return [];
    }
    
    /// <inheritdoc/>
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        // Check if target is within this subtree and matches
        if (Matches(_target) && IsDescendantOrSelf(_target, root))
            return [_target];
        return [];
    }
    
    /// <inheritdoc/>
    public override bool Matches(RedNode node) => 
        ReferenceEquals(node, _target) && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => ReferenceEquals(node, _target.Green);
    
    protected override ExactNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(_target, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override ExactNodeQuery CreateFirst() => new(_target, _predicate, SelectionMode.First, 0);
    protected override ExactNodeQuery CreateLast() => new(_target, _predicate, SelectionMode.Last, 0);
    protected override ExactNodeQuery CreateNth(int n) => new(_target, _predicate, SelectionMode.Nth, n);
    protected override ExactNodeQuery CreateSkip(int count) => new(_target, _predicate, SelectionMode.Skip, count);
    protected override ExactNodeQuery CreateTake(int count) => new(_target, _predicate, SelectionMode.Take, count);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
    
    private static bool IsDescendantOrSelf(RedNode node, RedNode root)
    {
        var current = node;
        while (current != null)
        {
            if (ReferenceEquals(current, root))
                return true;
            current = current.Parent;
        }
        return false;
    }
}

#endregion
