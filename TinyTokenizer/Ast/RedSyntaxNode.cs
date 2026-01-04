using System.Collections.Immutable;
using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for schema-defined red syntax nodes.
/// Provides typed access to the underlying GreenSyntaxNode and child navigation.
/// Derived classes add domain-specific properties for accessing matched syntax parts.
/// </summary>
/// <remarks>
/// RedSyntaxNode extends RedNode, making semantic constructs full participants in the tree.
/// This enables unified tree traversal, queries, and editing that works with both
/// structural nodes (RedLeaf, RedBlock) and semantic nodes (RedFunctionCall, etc.).
/// </remarks>
/// <example>
/// <code>
/// public sealed class FunctionCallSyntax : SyntaxNode
/// {
///     internal FunctionCallSyntax(CreationContext context)
///         : base(context) { }
///     
///     public RedLeaf NameNode => GetTypedChild&lt;RedLeaf&gt;(0);
///     public string Name => NameNode.Text;
///     public RedBlock Arguments => GetTypedChild&lt;RedBlock&gt;(1);
/// }
/// </code>
/// </example>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public abstract class SyntaxNode : RedNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Position}..{EndPosition}] ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";

    /// <summary>
    /// Opaque context for creating SyntaxNode instances.
    /// Used by the library to construct syntax nodes without exposing green layer details.
    /// </summary>
    /// <remarks>
    /// This struct is passed to SyntaxNode constructors to provide the necessary
    /// green node, parent, and position information without requiring users to
    /// directly reference internal green types.
    /// </remarks>
    public readonly record struct CreationContext
    {
        internal GreenSyntaxNode Green { get; init; }
        
        /// <summary>The parent red node, or null if this is the root.</summary>
        public RedNode? Parent { get; init; }
        
        /// <summary>The absolute position in the source text.</summary>
        public int Position { get; init; }
        
        /// <summary>The index of this node within its parent's children, or -1 if root.</summary>
        public int SiblingIndex { get; init; }
        
        internal CreationContext(GreenSyntaxNode green, RedNode? parent, int position, int siblingIndex = -1)
        {
            Green = green;
            Parent = parent;
            Position = position;
            SiblingIndex = siblingIndex;
        }
    }
    
    private readonly RedNode?[] _children;
    
    /// <summary>
    /// Creates a red syntax node from a creation context.
    /// </summary>
    /// <param name="context">The creation context containing green node and position info.</param>
    protected SyntaxNode(CreationContext context)
        : base(context.Green, context.Parent, context.Position, context.SiblingIndex)
    {
        _children = new RedNode?[context.Green.SlotCount];
    }
    
    /// <summary>
    /// Gets the underlying green syntax node with proper typing.
    /// </summary>
    internal new GreenSyntaxNode Green => (GreenSyntaxNode)base.Green;
    
    /// <summary>
    /// Gets the child at the specified slot index, creating lazily if needed.
    /// </summary>
    public override RedNode? GetChild(int index)
    {
        if (index < 0 || index >= _children.Length)
            return null;
        
        return GetRedChild(ref _children[index], index);
    }
    
    /// <summary>
    /// Gets a child with type checking, throwing if the child is null or wrong type.
    /// </summary>
    /// <typeparam name="T">The expected child type.</typeparam>
    /// <param name="index">The slot index.</param>
    /// <returns>The typed child node.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the child is null or wrong type.</exception>
    protected T GetTypedChild<T>(int index) where T : RedNode
    {
        var child = GetChild(index);
        if (child is not T typed)
        {
            throw new InvalidOperationException(
                $"Expected child at slot {index} to be {typeof(T).Name}, " +
                $"but got {child?.GetType().Name ?? "null"}.");
        }
        return typed;
    }
    
    /// <summary>
    /// Tries to get a child with type checking, returning null if child is null or wrong type.
    /// </summary>
    protected T? TryGetTypedChild<T>(int index) where T : RedNode =>
        GetChild(index) as T;
    
    /// <summary>
    /// Gets all children of this syntax node.
    /// </summary>
    public new IEnumerable<RedNode> Children
    {
        get
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var child = GetChild(i);
                if (child != null)
                    yield return child;
            }
        }
    }
    
    /// <summary>
    /// Gets the number of child slots in this syntax node.
    /// </summary>
    public int ChildCount => _children.Length;
}
