using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace TinyTokenizer.Ast;

/// <summary>
/// Factory for creating typed syntax nodes from green syntax nodes.
/// Uses compiled delegates for efficient instantiation.
/// Supports looking up RedType from Schema or falling back to green node's RedType.
/// </summary>
internal static class SyntaxRedFactory
{
    private static readonly ConcurrentDictionary<Type, Func<CreationContext, SyntaxNode>> _factories = new();
    
    /// <summary>
    /// Creates a syntax node of the appropriate type for the green node.
    /// Uses Schema-based type lookup if available, otherwise falls back to green.RedType.
    /// </summary>
    public static SyntaxNode Create(GreenSyntaxNode green, RedNode? parent, int position, int siblingIndex = -1, Schema? schema = null)
    {
        // Prefer Schema-based lookup, fall back to green.RedType
        Type? redType = schema?.GetSyntaxRedType(green.Kind);
        redType ??= green.RedType;
        
        var factory = GetOrCreateFactory(redType);
        var context = new CreationContext(green, parent, position, siblingIndex, schema);
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
