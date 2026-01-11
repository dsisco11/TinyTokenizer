using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Extension methods for semantic pattern matching.
/// These are convenience wrappers - prefer using SyntaxTree.Match&lt;T&gt;() directly.
/// </summary>
public static class SemanticMatchExtensions
{
    #region RedNode Extensions
    
    /// <summary>
    /// Finds all semantic nodes of the specified type starting from this node.
    /// </summary>
    public static IEnumerable<T> Match<T>(this SyntaxNode root, Schema schema, SemanticContext? context = null)
        where T : SemanticNode
    {
        var definition = schema.GetDefinition<T>();
        if (definition == null)
            yield break;
        
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            foreach (var pattern in definition.Patterns)
            {
                if (pattern.TryMatch(node, out var match))
                {
                    var semantic = definition.TryCreate(match, context);
                    if (semantic != null)
                    {
                        yield return (T)semantic;
                        break;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Finds all semantic nodes starting from this node.
    /// </summary>
    public static IEnumerable<SemanticNode> MatchAll(this SyntaxNode root, Schema schema, SemanticContext? context = null)
    {
        foreach (var definition in schema.SortedDefinitions)
        {
            var walker = new TreeWalker(root);
            foreach (var node in walker.DescendantsAndSelf())
            {
                foreach (var pattern in definition.Patterns)
                {
                    if (pattern.TryMatch(node, out var match))
                    {
                        var semantic = definition.TryCreate(match, context);
                        if (semantic != null)
                        {
                            yield return semantic;
                            break;
                        }
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region Query Integration
    
    /// <summary>
    /// Creates a query that matches semantic nodes by definition name.
    /// </summary>
    public static SyntaxNodeQuery Syntax(this Schema schema, string name)
    {
        var kind = schema.GetKind(name);
        return new SyntaxNodeQuery(kind);
    }
    
    /// <summary>
    /// Creates a query that matches semantic nodes by NodeKind.
    /// </summary>
    public static SyntaxNodeQuery Syntax(NodeKind kind)
    {
        return new SyntaxNodeQuery(kind);
    }
    
    /// <summary>
    /// Creates a query that matches semantic nodes of the specified type.
    /// Resolves the NodeKind from the type's registration in this schema.
    /// </summary>
    /// <typeparam name="T">The semantic node type to query for.</typeparam>
    /// <param name="schema">The schema containing the type registration.</param>
    /// <returns>A NodeQuery that matches nodes of the specified semantic type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not registered in this schema.</exception>
    /// <remarks>
    /// This generic overload is preferred over <see cref="Syntax(Schema, string)"/> as it provides
    /// compile-time type safety and uses the NodeKind directly for optimized scanning.
    /// </remarks>
    public static SyntaxNodeQuery Syntax<T>(this Schema schema) where T : SemanticNode
    {
        var definition = schema.GetDefinition<T>();
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Semantic node type '{typeof(T).Name}' is not registered in this schema.");
        }
        return new SyntaxNodeQuery(definition.Kind);
    }
    
    #endregion
}

/// <summary>
/// Query that matches nodes by semantic NodeKind.
/// </summary>
public sealed record SyntaxNodeQuery : NodeQuery<SyntaxNodeQuery>
{
    private readonly NodeKind _kind;
    private readonly Func<SyntaxNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public SyntaxNodeQuery(NodeKind kind) : this(kind, null, SelectionMode.All, 0) { }
    
    private SyntaxNodeQuery(NodeKind kind, Func<SyntaxNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _kind = kind;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf().Where(Matches);

        return SelectionModeHelper.Apply(matches, _mode, _modeArg);
    }
    
    public override bool Matches(SyntaxNode node) => 
        node.Kind == _kind && (_predicate == null || _predicate(node));
    
    internal override bool MatchesGreen(GreenNode node) => node.Kind == _kind;
    
    protected override SyntaxNodeQuery CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new(_kind, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override SyntaxNodeQuery CreateFirst() => new(_kind, _predicate, SelectionMode.First, 0);
    protected override SyntaxNodeQuery CreateLast() => new(_kind, _predicate, SelectionMode.Last, 0);
    protected override SyntaxNodeQuery CreateNth(int n) => new(_kind, _predicate, SelectionMode.Nth, n);
    protected override SyntaxNodeQuery CreateSkip(int count) => new(_kind, _predicate, SelectionMode.Skip, count);
    protected override SyntaxNodeQuery CreateTake(int count) => new(_kind, _predicate, SelectionMode.Take, count);
    
    internal override SelectionMode Mode => _mode;
    internal override int ModeArg => _modeArg;
    
    private static Func<SyntaxNode, bool>? CombinePredicates(Func<SyntaxNode, bool>? a, Func<SyntaxNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}

/// <summary>
/// Generic query that matches syntax nodes by C# type.
/// Resolves NodeKind from the tree's schema at Select time for optimized matching.
/// Falls back to type checking when no schema is available.
/// </summary>
/// <typeparam name="T">The syntax node type to match.</typeparam>
public sealed record SyntaxNodeQuery<T> : NodeQuery<SyntaxNodeQuery<T>> where T : SyntaxNode
{
    private readonly Func<T, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public SyntaxNodeQuery() : this(null, SelectionMode.All, 0) { }
    
    internal SyntaxNodeQuery(Func<T, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    public override IEnumerable<SyntaxNode> Select(SyntaxTree tree)
    {
        // Try to resolve NodeKind from schema for optimized matching
        var def = tree.Schema?.GetSyntaxDefinition<T>();
        if (def != null)
        {
            // Use kind-based matching (fast path)
            return SelectWithKind(tree.Root, def.Kind);
        }
        
        // Fallback: match by C# type
        return Select(tree.Root);
    }
    
    private IEnumerable<SyntaxNode> SelectWithKind(SyntaxNode root, NodeKind kind)
    {
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf()
            .Where(n => n.Kind == kind && (_predicate == null || _predicate((T)n)));
        
        return ApplyMode(matches);
    }
    
    public override IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        // Match by C# type (no schema available)
        var walker = new TreeWalker(root);
        var matches = walker.DescendantsAndSelf().Where(Matches);
        
        return ApplyMode(matches);
    }
    
    private IEnumerable<SyntaxNode> ApplyMode(IEnumerable<SyntaxNode> matches)
    {
        return SelectionModeHelper.Apply(matches, _mode, _modeArg);
    }
    
    /// <summary>
    /// Selects and casts to the strongly-typed syntax node.
    /// </summary>
    public IEnumerable<T> SelectTyped(SyntaxTree tree) => Select(tree).Cast<T>();
    
    /// <summary>
    /// Selects and casts to the strongly-typed syntax node.
    /// </summary>
    public IEnumerable<T> SelectTyped(SyntaxNode root) => Select(root).Cast<T>();
    
    public override bool Matches(SyntaxNode node) => 
        node is T typed && (_predicate == null || _predicate(typed));
    
    // MatchesGreen requires a Schema to resolve Type -> NodeKind mapping.
    // Since we don't have schema context here, we conservatively return true for 
    // GreenSyntaxNode instances and let the red-level Matches() do final type checking.
    internal override bool MatchesGreen(GreenNode node) => 
        node is GreenSyntaxNode;
    
    /// <summary>
    /// Filters by a strongly-typed predicate.
    /// </summary>
    public SyntaxNodeQuery<T> Where(Func<T, bool> predicate)
    {
        if (_predicate == null)
            return new SyntaxNodeQuery<T>(predicate, _mode, _modeArg);
        
        var combined = (T n) => _predicate(n) && predicate(n);
        return new SyntaxNodeQuery<T>(combined, _mode, _modeArg);
    }
    
    protected override SyntaxNodeQuery<T> CreateFiltered(Func<SyntaxNode, bool> predicate) =>
        new SyntaxNodeQuery<T>(n => (_predicate == null || _predicate(n)) && predicate(n), _mode, _modeArg);
    
    protected override SyntaxNodeQuery<T> CreateFirst() => new SyntaxNodeQuery<T>(_predicate, SelectionMode.First, 0);
    protected override SyntaxNodeQuery<T> CreateLast() => new SyntaxNodeQuery<T>(_predicate, SelectionMode.Last, 0);
    protected override SyntaxNodeQuery<T> CreateNth(int n) => new SyntaxNodeQuery<T>(_predicate, SelectionMode.Nth, n);
    protected override SyntaxNodeQuery<T> CreateSkip(int count) => new SyntaxNodeQuery<T>(_predicate, SelectionMode.Skip, count);
    protected override SyntaxNodeQuery<T> CreateTake(int count) => new SyntaxNodeQuery<T>(_predicate, SelectionMode.Take, count);
    
    internal override SelectionMode Mode => _mode;
    internal override int ModeArg => _modeArg;
}
