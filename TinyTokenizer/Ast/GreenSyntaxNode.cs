using System.Collections.Immutable;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// A green node representing a schema-defined syntax construct (e.g., function call, variable declaration).
/// Wraps underlying structural green nodes as children and specifies the concrete red node type to create.
/// </summary>
/// <remarks>
/// GreenSyntaxNode enables schema-defined patterns to be materialized into the green tree during binding.
/// When the red tree is created, it produces typed red nodes (e.g., RedFunctionCall) that provide
/// domain-specific accessors for the matched syntax pattern.
/// </remarks>
public sealed record GreenSyntaxNode : GreenNode
{
    private readonly ImmutableArray<GreenNode> _children;
    private readonly NodeKind _kind;
    private readonly Type _redType;
    private readonly int _width;
    
    /// <summary>
    /// Creates a green syntax node wrapping the specified children.
    /// </summary>
    /// <param name="kind">The semantic NodeKind for this syntax construct.</param>
    /// <param name="redType">The concrete RedSyntaxNode subclass to instantiate.</param>
    /// <param name="children">The child green nodes that make up this syntax construct.</param>
    public GreenSyntaxNode(NodeKind kind, Type redType, ImmutableArray<GreenNode> children)
    {
        _kind = kind;
        _redType = redType;
        _children = children;
        
        // Calculate total width
        int width = 0;
        foreach (var child in children)
        {
            width += child.Width;
        }
        _width = width;
    }
    
    /// <summary>
    /// Creates a green syntax node from params array of children.
    /// </summary>
    public GreenSyntaxNode(NodeKind kind, Type redType, params GreenNode[] children)
        : this(kind, redType, ImmutableArray.Create(children))
    {
    }
    
    /// <inheritdoc/>
    public override NodeKind Kind => _kind;
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <inheritdoc/>
    public override int SlotCount => _children.Length;
    
    /// <summary>
    /// The concrete RedSyntaxNode subclass to instantiate when creating the red node.
    /// </summary>
    public Type RedType => _redType;
    
    /// <summary>
    /// The child green nodes.
    /// </summary>
    public ImmutableArray<GreenNode> Children => _children;
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index) =>
        index >= 0 && index < _children.Length ? _children[index] : null;
    
    /// <inheritdoc/>
    public override void WriteTo(StringBuilder builder)
    {
        foreach (var child in _children)
            child.WriteTo(builder);
    }
    
    /// <inheritdoc/>
    public override RedNode CreateRed(RedNode? parent, int position) =>
        SyntaxRedFactory.Create(this, parent, position);
}

/// <summary>
/// Factory for creating typed red syntax nodes from green syntax nodes.
/// Uses compiled delegates for efficient instantiation.
/// </summary>
internal static class SyntaxRedFactory
{
    private static readonly Dictionary<Type, Func<GreenSyntaxNode, RedNode?, int, SyntaxNode>> _factories = new();
    private static readonly object _lock = new();
    
    /// <summary>
    /// Creates a red syntax node of the appropriate type for the green node.
    /// </summary>
    public static SyntaxNode Create(GreenSyntaxNode green, RedNode? parent, int position)
    {
        var factory = GetOrCreateFactory(green.RedType);
        return factory(green, parent, position);
    }
    
    private static Func<GreenSyntaxNode, RedNode?, int, SyntaxNode> GetOrCreateFactory(Type redType)
    {
        lock (_lock)
        {
            if (_factories.TryGetValue(redType, out var existing))
                return existing;
            
            // Find constructor: (GreenSyntaxNode green, RedNode? parent, int position)
            var ctor = redType.GetConstructor(new[] { typeof(GreenSyntaxNode), typeof(RedNode), typeof(int) });
            if (ctor == null)
            {
                throw new InvalidOperationException(
                    $"RedSyntaxNode type '{redType.Name}' must have a constructor " +
                    $"with signature (GreenSyntaxNode green, RedNode? parent, int position).");
            }
            
            // Create factory delegate
            Func<GreenSyntaxNode, RedNode?, int, SyntaxNode> factory = 
                (g, p, pos) => (SyntaxNode)ctor.Invoke(new object?[] { g, p, pos });
            
            _factories[redType] = factory;
            return factory;
        }
    }
    
    /// <summary>
    /// Registers a custom factory for a red syntax node type.
    /// Useful for avoiding reflection overhead in hot paths.
    /// </summary>
    public static void RegisterFactory<T>(Func<GreenSyntaxNode, RedNode?, int, T> factory) 
        where T : SyntaxNode
    {
        lock (_lock)
        {
            _factories[typeof(T)] = (g, p, pos) => factory(g, p, pos);
        }
    }
}
