using System.Runtime.CompilerServices;

namespace TinyTokenizer.Ast;

/// <summary>
/// Compares red nodes by their underlying green node identity.
/// Used for deduplication since red nodes are ephemeral but wrap the same green nodes.
/// </summary>
internal sealed class RedNodeGreenComparer : IEqualityComparer<SyntaxNode>
{
    public static readonly RedNodeGreenComparer Instance = new();
    
    private RedNodeGreenComparer() { }
    
    public bool Equals(SyntaxNode? x, SyntaxNode? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return ReferenceEquals(x.Green, y.Green);
    }
    
    public int GetHashCode(SyntaxNode obj) => RuntimeHelpers.GetHashCode(obj.Green);
}

#region Kind Query

/// <summary>
/// Matches nodes of a specific kind.
/// </summary>
public sealed record KindNodeQuery : NodeQuery<KindNodeQuery>
{
    /// <summary>The kind to match.</summary>
    public NodeKind Kind { get; }
    
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    private readonly string? _textConstraint;
    
    /// <summary>Creates a query matching nodes of the specified kind.</summary>
    public KindNodeQuery(NodeKind kind) : this(kind, null, SelectionMode.All, 0, null) { }
    
    private KindNodeQuery(NodeKind kind, Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg, string? textConstraint = null)
    {
        Kind = kind;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
        _textConstraint = textConstraint;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public override bool Matches(SyntaxNode node) => 
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
        new(Kind, CombinePredicates(_predicate, n => n is SyntaxToken leaf && leaf.Text == text), _mode, _modeArg, text);
    
    protected override KindNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(Kind, CombinePredicates(_predicate, predicate), _mode, _modeArg, _textConstraint);
    
    protected override KindNodeQuery CreateFirst() => new(Kind, _predicate, SelectionMode.First, 0, _textConstraint);
    protected override KindNodeQuery CreateLast() => new(Kind, _predicate, SelectionMode.Last, 0, _textConstraint);
    protected override KindNodeQuery CreateNth(int n) => new(Kind, _predicate, SelectionMode.Nth, n, _textConstraint);
    protected override KindNodeQuery CreateSkip(int count) => new(Kind, _predicate, SelectionMode.Skip, count, _textConstraint);
    protected override KindNodeQuery CreateTake(int count) => new(Kind, _predicate, SelectionMode.Take, count, _textConstraint);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
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
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    /// <summary>Gets the opener character filter, or null to match any block.</summary>
    public char? Opener => _opener;
    
    /// <summary>Creates a query matching blocks with the specified opener (null for any).</summary>
    public BlockNodeQuery(char? opener = null) : this(opener, null, SelectionMode.All, 0) { }
    
    private protected BlockNodeQuery(char? opener, Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _opener = opener;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public override bool Matches(SyntaxNode node)
    {
        if (node is not SyntaxBlock block)
            return false;
        
        if (_opener != null && block.Opener != _opener.Value)
            return false;
        
        return _predicate == null || _predicate(node);
    }
    
    internal override bool MatchesGreen(GreenNode node) => 
        node is GreenBlock block && (_opener == null || block.Opener == _opener);
    
    protected override BlockNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(_opener, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override BlockNodeQuery CreateFirst() => new(_opener, _predicate, SelectionMode.First, 0);
    protected override BlockNodeQuery CreateLast() => new(_opener, _predicate, SelectionMode.Last, 0);
    protected override BlockNodeQuery CreateNth(int n) => new(_opener, _predicate, SelectionMode.Nth, n);
    protected override BlockNodeQuery CreateSkip(int count) => new(_opener, _predicate, SelectionMode.Skip, count);
    protected override BlockNodeQuery CreateTake(int count) => new(_opener, _predicate, SelectionMode.Take, count);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
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
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public AnyNodeQuery() : this(null, SelectionMode.All, 0) { }
    
    private AnyNodeQuery(Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public override bool Matches(SyntaxNode node) => _predicate == null || _predicate(node);
    
    internal override bool MatchesGreen(GreenNode node) => true;
    
    protected override AnyNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override AnyNodeQuery CreateFirst() => new(_predicate, SelectionMode.First, 0);
    protected override AnyNodeQuery CreateLast() => new(_predicate, SelectionMode.Last, 0);
    protected override AnyNodeQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n);
    protected override AnyNodeQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count);
    protected override AnyNodeQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

#endregion

#region Leaf Query

/// <summary>
/// Matches only leaf nodes.
/// </summary>
public sealed record LeafNodeQuery : NodeQuery<LeafNodeQuery>
{
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public LeafNodeQuery() : this(null, SelectionMode.All, 0) { }
    
    private LeafNodeQuery(Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public override bool Matches(SyntaxNode node) => 
        node is SyntaxToken && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => node is GreenLeaf;
    
    protected override LeafNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override LeafNodeQuery CreateFirst() => new(_predicate, SelectionMode.First, 0);
    protected override LeafNodeQuery CreateLast() => new(_predicate, SelectionMode.Last, 0);
    protected override LeafNodeQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n);
    protected override LeafNodeQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count);
    protected override LeafNodeQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
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
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    private readonly bool _negated;
    
    public NewlineNodeQuery(bool negated = false) : this(null, SelectionMode.All, 0, negated) { }
    
    private NewlineNodeQuery(Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg, bool negated = false)
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
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public override bool Matches(SyntaxNode node)
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
    
    private static bool HasNewline(SyntaxNode node)
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
    
    protected override NewlineNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg, _negated);
    
    protected override NewlineNodeQuery CreateFirst() => new(_predicate, SelectionMode.First, 0, _negated);
    protected override NewlineNodeQuery CreateLast() => new(_predicate, SelectionMode.Last, 0, _negated);
    protected override NewlineNodeQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n, _negated);
    protected override NewlineNodeQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count, _negated);
    protected override NewlineNodeQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count, _negated);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
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
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var seen = new HashSet<SyntaxNode>(RedNodeGreenComparer.Instance);
        
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
    public bool Matches(SyntaxNode node) => _left.Matches(node) || _right.Matches(node);
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
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
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var leftMatches = new HashSet<SyntaxNode>(_left.Select(root), RedNodeGreenComparer.Instance);
        
        foreach (var node in _right.Select(root))
        {
            if (leftMatches.Contains(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node) => _left.Matches(node) && _right.Matches(node);
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
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

#region AnyOf Query

/// <summary>
/// Matches nodes that satisfy any of the provided queries (variadic OR).
/// Short-circuits on first match for efficiency.
/// </summary>
public sealed record AnyOfQuery : INodeQuery, IGreenNodeQuery
{
    private readonly IReadOnlyList<INodeQuery> _queries;
    
    /// <summary>Creates a query that matches any of the specified queries.</summary>
    public AnyOfQuery(params INodeQuery[] queries) => _queries = queries;
    
    /// <summary>Creates a query that matches any of the specified queries.</summary>
    public AnyOfQuery(IEnumerable<INodeQuery> queries) => _queries = queries.ToArray();
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var seen = new HashSet<SyntaxNode>(RedNodeGreenComparer.Instance);
        
        foreach (var query in _queries)
        {
            foreach (var node in query.Select(root))
            {
                if (seen.Add(node))
                    yield return node;
            }
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        foreach (var query in _queries)
        {
            if (query.Matches(node))
                return true;
        }
        return false;
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Try each query in order (first match wins)
        foreach (var query in _queries)
        {
            if (query.TryMatch(startNode, out consumedCount))
                return true;
        }
        consumedCount = 0;
        return false;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        foreach (var query in _queries)
        {
            if (query is IGreenNodeQuery greenQuery && greenQuery.MatchesGreen(node))
                return true;
        }
        return false;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        foreach (var query in _queries)
        {
            if (query is IGreenNodeQuery greenQuery && 
                greenQuery.TryMatchGreen(siblings, startIndex, out consumedCount))
                return true;
        }
        consumedCount = 0;
        return false;
    }
}

#endregion

#region NoneOf Query

/// <summary>
/// Matches nodes that do NOT satisfy any of the provided queries.
/// Inverse of AnyOf - consumes 1 node when all inner queries fail.
/// </summary>
public sealed record NoneOfQuery : INodeQuery, IGreenNodeQuery
{
    private readonly IReadOnlyList<INodeQuery> _queries;
    
    /// <summary>Creates a query that matches when none of the specified queries match.</summary>
    public NoneOfQuery(params INodeQuery[] queries) => _queries = queries;
    
    /// <summary>Creates a query that matches when none of the specified queries match.</summary>
    public NoneOfQuery(IEnumerable<INodeQuery> queries) => _queries = queries.ToArray();
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (Matches(node))
                yield return node;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        foreach (var query in _queries)
        {
            if (query.Matches(node))
                return false;
        }
        return true;
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Fails if any query matches
        foreach (var query in _queries)
        {
            if (query.TryMatch(startNode, out _))
            {
                consumedCount = 0;
                return false;
            }
        }
        // None matched, consume 1
        consumedCount = 1;
        return true;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        foreach (var query in _queries)
        {
            if (query is IGreenNodeQuery greenQuery && greenQuery.MatchesGreen(node))
                return false;
        }
        return true;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        if (startIndex >= siblings.Count)
        {
            consumedCount = 0;
            return false;
        }
        
        foreach (var query in _queries)
        {
            if (query is IGreenNodeQuery greenQuery && 
                greenQuery.TryMatchGreen(siblings, startIndex, out _))
            {
                consumedCount = 0;
                return false;
            }
        }
        // None matched, consume 1
        consumedCount = 1;
        return true;
    }
}

#endregion

#region BOF Query

/// <summary>
/// Zero-width assertion that matches when the node is at the beginning of the file.
/// Matches when the node is the first child of the root (SiblingIndex == 0 at root level).
/// </summary>
public sealed record BeginningOfFileQuery : INodeQuery, IGreenNodeQuery
{
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        // BOF only matches the first node in the file
        var firstChild = root.Children.FirstOrDefault();
        if (firstChild != null)
            yield return firstChild;
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        // Node is at BOF if it's the first child of its parent and parent is root
        if (node.SiblingIndex != 0)
            return false;
        
        // Check if parent is the root (no grandparent)
        var parent = node.Parent;
        return parent != null && parent.Parent == null;
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Zero-width assertion - never consumes
        consumedCount = 0;
        return Matches(startNode);
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        // At green level, we can't determine position - always return true
        // The red-level check will handle the actual verification
        return true;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        consumedCount = 0;
        return startIndex == 0; // First in the sibling list
    }
}

#endregion

#region EOF Query

/// <summary>
/// Zero-width assertion that matches when the node is at the end of the file.
/// Matches when the node is the last child of the root (no following siblings at root level).
/// </summary>
public sealed record EndOfFileQuery : INodeQuery, IGreenNodeQuery
{
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        // EOF only matches the last node in the file
        var lastChild = root.Children.LastOrDefault();
        if (lastChild != null)
            yield return lastChild;
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        // Node is at EOF if it has no next sibling and parent is root
        if (node.NextSibling() != null)
            return false;
        
        // Check if parent is the root (no grandparent)
        var parent = node.Parent;
        return parent != null && parent.Parent == null;
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Zero-width assertion - never consumes
        consumedCount = 0;
        return Matches(startNode);
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.MatchesGreen(GreenNode node)
    {
        // At green level, we can't determine position - always return true
        return true;
    }
    
    /// <inheritdoc/>
    bool IGreenNodeQuery.TryMatchGreen(IReadOnlyList<GreenNode> siblings, int startIndex, out int consumedCount)
    {
        consumedCount = 0;
        return startIndex == siblings.Count - 1; // Last in the sibling list
    }
}

#endregion

#region Sibling Query

/// <summary>
/// Navigates to a sibling at a relative offset from the current node.
/// Zero-width query - navigates but doesn't consume the current node.
/// </summary>
public sealed record SiblingQuery : INodeQuery
{
    private readonly int _offset;
    private readonly INodeQuery? _innerQuery;
    
    /// <summary>Creates a query that matches the sibling at the specified offset.</summary>
    /// <param name="offset">Relative offset: +1 for next sibling, -1 for previous sibling, etc.</param>
    public SiblingQuery(int offset) : this(offset, null) { }
    
    /// <summary>Creates a query that matches the sibling at the specified offset if it matches the inner query.</summary>
    public SiblingQuery(int offset, INodeQuery? innerQuery)
    {
        _offset = offset;
        _innerQuery = innerQuery;
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            var sibling = GetSiblingAtOffset(node);
            if (sibling != null && (_innerQuery == null || _innerQuery.Matches(sibling)))
                yield return sibling;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        var sibling = GetSiblingAtOffset(node);
        if (sibling == null)
            return false;
        return _innerQuery == null || _innerQuery.Matches(sibling);
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Zero-width - navigates without consuming
        consumedCount = 0;
        return Matches(startNode);
    }
    
    private SyntaxNode? GetSiblingAtOffset(SyntaxNode node)
    {
        if (_offset == 0)
            return node;
        
        var current = node;
        if (_offset > 0)
        {
            for (int i = 0; i < _offset && current != null; i++)
                current = current.NextSibling();
        }
        else
        {
            for (int i = 0; i < -_offset && current != null; i++)
                current = current.PreviousSibling();
        }
        return current;
    }
}

#endregion

#region Parent Query

/// <summary>
/// Matches the direct parent of the current node.
/// Zero-width query for vertical tree navigation.
/// </summary>
public sealed record ParentQuery : INodeQuery
{
    private readonly INodeQuery? _innerQuery;
    
    /// <summary>Creates a query that matches the parent node.</summary>
    public ParentQuery() => _innerQuery = null;
    
    /// <summary>Creates a query that matches the parent if it satisfies the inner query.</summary>
    public ParentQuery(INodeQuery? innerQuery) => _innerQuery = innerQuery;
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var seen = new HashSet<SyntaxNode>(RedNodeGreenComparer.Instance);
        var walker = new TreeWalker(root);
        
        foreach (var node in walker.DescendantsAndSelf())
        {
            var parent = node.Parent;
            if (parent != null && seen.Add(parent))
            {
                if (_innerQuery == null || _innerQuery.Matches(parent))
                    yield return parent;
            }
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        var parent = node.Parent;
        if (parent == null)
            return false;
        return _innerQuery == null || _innerQuery.Matches(parent);
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Zero-width - navigates without consuming
        consumedCount = 0;
        return Matches(startNode);
    }
}

#endregion

#region Ancestor Query

/// <summary>
/// Matches any ancestor of the current node that satisfies the inner query.
/// Walks up the tree until finding a match or reaching the root.
/// Zero-width query for vertical tree navigation.
/// </summary>
public sealed record AncestorQuery : INodeQuery
{
    private readonly INodeQuery _innerQuery;
    
    /// <summary>Creates a query that matches the first ancestor satisfying the inner query.</summary>
    public AncestorQuery(INodeQuery innerQuery) => _innerQuery = innerQuery;
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var seen = new HashSet<SyntaxNode>(RedNodeGreenComparer.Instance);
        var walker = new TreeWalker(root);
        
        foreach (var node in walker.DescendantsAndSelf())
        {
            var ancestor = FindMatchingAncestor(node);
            if (ancestor != null && seen.Add(ancestor))
                yield return ancestor;
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        return FindMatchingAncestor(node) != null;
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        // Zero-width - navigates without consuming
        consumedCount = 0;
        return Matches(startNode);
    }
    
    private SyntaxNode? FindMatchingAncestor(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (_innerQuery.Matches(current))
                return current;
            current = current.Parent;
        }
        return null;
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
    private readonly SyntaxNode _target;
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    /// <summary>Creates a query matching the exact node instance.</summary>
    public ExactNodeQuery(SyntaxNode target) : this(target, null, SelectionMode.All, 0) { }
    
    private ExactNodeQuery(SyntaxNode target, Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _target = target;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <summary>The target node this query matches.</summary>
    public SyntaxNode Target => _target;
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree)
    {
        // If the target node matches our criteria, return it
        if (Matches(_target))
            return [_target];
        return [];
    }
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        // Check if target is within this subtree and matches
        if (Matches(_target) && IsDescendantOrSelf(_target, root))
            return [_target];
        return [];
    }
    
    /// <inheritdoc/>
    public override bool Matches(SyntaxNode node) => 
        RedNodeGreenComparer.Instance.Equals(node, _target) && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => ReferenceEquals(node, _target.Green);
    
    protected override ExactNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(_target, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override ExactNodeQuery CreateFirst() => new(_target, _predicate, SelectionMode.First, 0);
    protected override ExactNodeQuery CreateLast() => new(_target, _predicate, SelectionMode.Last, 0);
    protected override ExactNodeQuery CreateNth(int n) => new(_target, _predicate, SelectionMode.Nth, n);
    protected override ExactNodeQuery CreateSkip(int count) => new(_target, _predicate, SelectionMode.Skip, count);
    protected override ExactNodeQuery CreateTake(int count) => new(_target, _predicate, SelectionMode.Take, count);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
    
    private static bool IsDescendantOrSelf(SyntaxNode node, SyntaxNode root)
    {
        var current = node;
        while (current != null)
        {
            if (RedNodeGreenComparer.Instance.Equals(current, root))
                return true;
            current = current.Parent;
        }
        return false;
    }
}

#endregion

#region Keyword Queries

/// <summary>
/// Matches any keyword node (NodeKind in the keyword range 1000-99999).
/// </summary>
public sealed record AnyKeywordQuery : NodeQuery<AnyKeywordQuery>
{
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    /// <summary>Creates a query matching any keyword node.</summary>
    public AnyKeywordQuery() : this(null, SelectionMode.All, 0) { }
    
    private AnyKeywordQuery(Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
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
    public override bool Matches(SyntaxNode node) => 
        node.Kind.IsKeyword() && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => node.Kind.IsKeyword();
    
    protected override AnyKeywordQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override AnyKeywordQuery CreateFirst() => new(_predicate, SelectionMode.First, 0);
    protected override AnyKeywordQuery CreateLast() => new(_predicate, SelectionMode.Last, 0);
    protected override AnyKeywordQuery CreateNth(int n) => new(_predicate, SelectionMode.Nth, n);
    protected override AnyKeywordQuery CreateSkip(int count) => new(_predicate, SelectionMode.Skip, count);
    protected override AnyKeywordQuery CreateTake(int count) => new(_predicate, SelectionMode.Take, count);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

/// <summary>
/// Matches keyword nodes in a specific category.
/// Requires a schema to be attached to the tree for category resolution.
/// </summary>
public sealed record KeywordCategoryQuery : NodeQuery<KeywordCategoryQuery>
{
    private readonly string _categoryName;
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    /// <summary>Creates a query matching keywords in the specified category.</summary>
    /// <param name="categoryName">The keyword category name (e.g., "TypeNames", "ControlFlow").</param>
    public KeywordCategoryQuery(string categoryName) : this(categoryName, null, SelectionMode.All, 0) { }
    
    private KeywordCategoryQuery(string categoryName, Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _categoryName = categoryName;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    /// <summary>Gets the category name being matched.</summary>
    public string CategoryName => _categoryName;
    
    /// <inheritdoc/>
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree)
    {
        var schema = tree.Schema;
        if (schema == null || !schema.HasKeywords)
            return [];
        
        var categoryKinds = schema.GetKeywordsInCategory(_categoryName);
        if (categoryKinds.IsEmpty)
            return [];
        
        var kindSet = categoryKinds.ToHashSet();
        var walker = new TreeWalker(tree.Root);
        var matches = walker.DescendantsAndSelf()
            .Where(n => kindSet.Contains(n.Kind) && (_predicate == null || _predicate(n)));
        
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
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        // Without tree context, we can't resolve category - match any keyword
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf()
            .Where(n => n.Kind.IsKeyword() && (_predicate == null || _predicate(n)));
        
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
    public override bool Matches(SyntaxNode node) => 
        node.Kind.IsKeyword() && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => node.Kind.IsKeyword();
    
    protected override KeywordCategoryQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(_categoryName, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override KeywordCategoryQuery CreateFirst() => new(_categoryName, _predicate, SelectionMode.First, 0);
    protected override KeywordCategoryQuery CreateLast() => new(_categoryName, _predicate, SelectionMode.Last, 0);
    protected override KeywordCategoryQuery CreateNth(int n) => new(_categoryName, _predicate, SelectionMode.Nth, n);
    protected override KeywordCategoryQuery CreateSkip(int count) => new(_categoryName, _predicate, SelectionMode.Skip, count);
    protected override KeywordCategoryQuery CreateTake(int count) => new(_categoryName, _predicate, SelectionMode.Take, count);
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

#endregion
