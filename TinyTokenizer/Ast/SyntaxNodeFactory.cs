using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace TinyTokenizer.Ast;

/// <summary>
/// Factory for creating typed syntax nodes from green syntax nodes.
/// Uses compiled delegates for efficient instantiation.
/// </summary>
internal static class SyntaxNodeFactory
{
    private static readonly ConcurrentDictionary<Type, Func<CreationContext, SyntaxNode>> _factories = new();
    
    /// <summary>
    /// Creates a syntax node of the appropriate type for the green node.
    /// Uses Schema-based type lookup from the SyntaxTree.
    /// </summary>
    /// <param name="green">The green syntax node to wrap.</param>
    /// <param name="parent">The parent red node.</param>
    /// <param name="position">Absolute position in source.</param>
    /// <param name="siblingIndex">Index within parent's children.</param>
    /// <param name="tree">The containing syntax tree (required for schema lookup).</param>
    /// <exception cref="ArgumentNullException">Thrown if tree is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if tree has no schema or schema has no type for this node kind.</exception>
    public static SyntaxNode Create(GreenSyntaxNode green, RedNode? parent, int position, int siblingIndex, SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        
        var schema = tree.Schema ?? throw new InvalidOperationException(
            "SyntaxTree must have an attached Schema to create typed syntax nodes.");
        
        Type? redType = schema.GetSyntaxRedType(green.Kind) ?? throw new InvalidOperationException(
            $"No type registered in Schema for green node of kind '{green.Kind}'.");
        
        var factory = GetOrCreateFactory(redType);
        var context = new CreationContext(green, parent, position, siblingIndex, tree);
        return factory(context);
    }
    
    private static Func<CreationContext, SyntaxNode> GetOrCreateFactory(Type redType)
    {
        return _factories.GetOrAdd(redType, type =>
        {
            // Find constructor: (CreationContext context)
            var ctor = type.GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null,
                [typeof(CreationContext)],
                null);
            if (ctor == null)
            {
                throw new InvalidOperationException(
                    $"SyntaxNode type '{type.Name}' must have a constructor " +
                    $"with signature (CreationContext context). " +
                    $"The constructor can be internal or protected.");
            }
            
            // Compile a fast factory delegate using expression trees
            var param = Expression.Parameter(typeof(CreationContext), "ctx");
            var newExpr = Expression.New(ctor, param);
            var lambda = Expression.Lambda<Func<CreationContext, SyntaxNode>>(newExpr, param);
            return lambda.Compile();
        });
    }
    
    /// <summary>
    /// Registers a custom factory for a red syntax node type.
    /// </summary>
    public static void RegisterFactory<T>(Func<CreationContext, T> factory) 
        where T : SyntaxNode
    {
        _factories[typeof(T)] = ctx => factory(ctx);
    }
}
