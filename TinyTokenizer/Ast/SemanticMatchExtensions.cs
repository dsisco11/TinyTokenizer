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
    public static NodeQuery Semantic(this Schema schema, string name)
    {
        var kind = schema.GetKind(name);
        return new SemanticNodeQuery(kind);
    }
    
    /// <summary>
    /// Creates a query that matches semantic nodes by NodeKind.
    /// </summary>
    public static NodeQuery Semantic(NodeKind kind)
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
    public static NodeQuery Semantic<T>(this Schema schema) where T : SemanticNode
    {
        return schema.Semantic<T>();
    }
    
    #endregion
}

/// <summary>
/// Query that matches nodes by semantic NodeKind.
/// </summary>
internal sealed record SemanticNodeQuery : NodeQuery
{
    private readonly NodeKind _kind;
    
    public SemanticNodeQuery(NodeKind kind) => _kind = kind;
    
    public override IEnumerable<RedNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    public override IEnumerable<RedNode> Select(RedNode root)
    {
        var walker = new TreeWalker(root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            if (Matches(node))
                yield return node;
        }
    }
    
    public override bool Matches(RedNode node) => node.Kind == _kind;
}
