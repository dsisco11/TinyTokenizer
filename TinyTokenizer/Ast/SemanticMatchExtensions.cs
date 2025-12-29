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
    public static IEnumerable<T> Match<T>(this RedNode root, Schema schema, SemanticContext? context = null)
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
    public static IEnumerable<SemanticNode> MatchAll(this RedNode root, Schema schema, SemanticContext? context = null)
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
    public static SemanticNodeQuery Semantic(this Schema schema, string name)
    {
        var kind = schema.GetKind(name);
        return new SemanticNodeQuery(kind);
    }
    
    /// <summary>
    /// Creates a query that matches semantic nodes by NodeKind.
    /// </summary>
    public static SemanticNodeQuery Semantic(NodeKind kind)
    {
        return new SemanticNodeQuery(kind);
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
    /// This generic overload is preferred over <see cref="Semantic(Schema, string)"/> as it provides
    /// compile-time type safety and uses the NodeKind directly for optimized scanning.
    /// </remarks>
    public static SemanticNodeQuery Semantic<T>(this Schema schema) where T : SemanticNode
    {
        var definition = schema.GetDefinition<T>();
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Semantic node type '{typeof(T).Name}' is not registered in this schema.");
        }
        return new SemanticNodeQuery(definition.Kind);
    }
    
    #endregion
}

/// <summary>
/// Query that matches nodes by semantic NodeKind.
/// </summary>
public sealed record SemanticNodeQuery : NodeQuery<SemanticNodeQuery>
{
    private readonly NodeKind _kind;
    private readonly Func<RedNode, bool>? _predicate;
    private readonly SelectionMode _mode;
    private readonly int _modeArg;
    
    public SemanticNodeQuery(NodeKind kind) : this(kind, null, SelectionMode.All, 0) { }
    
    private SemanticNodeQuery(NodeKind kind, Func<RedNode, bool>? predicate, SelectionMode mode, int modeArg)
    {
        _kind = kind;
        _predicate = predicate;
        _mode = mode;
        _modeArg = modeArg;
    }
    
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
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
    
    public override bool Matches(RedNode node) => 
        node.Kind == _kind && (_predicate == null || _predicate(node));
    
    public override bool MatchesGreen(GreenNode node) => node.Kind == _kind;
    
    protected override SemanticNodeQuery CreateFiltered(Func<RedNode, bool> predicate) =>
        new(_kind, CombinePredicates(_predicate, predicate), _mode, _modeArg);
    
    protected override SemanticNodeQuery CreateFirst() => new(_kind, _predicate, SelectionMode.First, 0);
    protected override SemanticNodeQuery CreateLast() => new(_kind, _predicate, SelectionMode.Last, 0);
    protected override SemanticNodeQuery CreateNth(int n) => new(_kind, _predicate, SelectionMode.Nth, n);
    protected override SemanticNodeQuery CreateSkip(int count) => new(_kind, _predicate, SelectionMode.Skip, count);
    protected override SemanticNodeQuery CreateTake(int count) => new(_kind, _predicate, SelectionMode.Take, count);
    
    private static Func<RedNode, bool>? CombinePredicates(Func<RedNode, bool>? a, Func<RedNode, bool> b) =>
        a == null ? b : n => a(n) && b(n);
}
