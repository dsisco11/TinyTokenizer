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
        
        internal CreationContext(GreenSyntaxNode green, RedNode? parent, int position)
        {
            Green = green;
            Parent = parent;
            Position = position;
        }
    }
    
    private readonly RedNode?[] _children;
    
    /// <summary>
    /// Creates a red syntax node from a creation context.
    /// </summary>
    /// <param name="context">The creation context containing green node and position info.</param>
    protected SyntaxNode(CreationContext context)
        : base(context.Green, context.Parent, context.Position)
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

#region Example Syntax Nodes (for reference/testing)

/// <summary>
/// A function call syntax node: identifier followed by parenthesized arguments.
/// Pattern: Ident + ParenBlock
/// Example: foo(a, b, c)
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class FunctionCallSyntax : SyntaxNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"FunctionCall[{Position}..{EndPosition}] \"{Truncate(Name, 20)}\" ({Arguments.ChildCount} args)";

    /// <summary>
    /// Creates a function call red syntax node.
    /// </summary>
    internal FunctionCallSyntax(CreationContext context)
        : base(context)
    {
    }
    
    /// <summary>The function name node.</summary>
    public RedLeaf NameNode => GetTypedChild<RedLeaf>(0);
    
    /// <summary>The function name as text.</summary>
    public string Name => NameNode.Text;
    
    /// <summary>The arguments block (parentheses).</summary>
    public RedBlock Arguments => GetTypedChild<RedBlock>(1);
    
    /// <summary>
    /// Gets the argument nodes (children of the arguments block, excluding symbols).
    /// </summary>
    public IEnumerable<RedNode> ArgumentNodes =>
        Arguments.Children.Where(c => c.Kind != NodeKind.Symbol);
}

/// <summary>
/// An array/indexer access syntax node: identifier followed by bracketed index.
/// Pattern: Ident + BracketBlock
/// Example: arr[0], dict["key"]
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ArrayAccessSyntax : SyntaxNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"ArrayAccess[{Position}..{EndPosition}] \"{Truncate(Target, 20)}\"";

    /// <summary>
    /// Creates an array access red syntax node.
    /// </summary>
    internal ArrayAccessSyntax(CreationContext context)
        : base(context)
    {
    }
    
    /// <summary>The target being accessed.</summary>
    public RedLeaf TargetNode => GetTypedChild<RedLeaf>(0);
    
    /// <summary>The target name as text.</summary>
    public string Target => TargetNode.Text;
    
    /// <summary>The index block (brackets).</summary>
    public RedBlock IndexBlock => GetTypedChild<RedBlock>(1);
    
    /// <summary>
    /// Gets the index nodes (children of the index block, excluding symbols).
    /// </summary>
    public IEnumerable<RedNode> IndexNodes =>
        IndexBlock.Children.Where(c => c.Kind != NodeKind.Symbol);
}

/// <summary>
/// A property/member access syntax node: identifier, dot, identifier.
/// Pattern: Ident + Symbol(".") + Ident
/// Example: obj.property, namespace.Class
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class PropertyAccessSyntax : SyntaxNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"PropertyAccess[{Position}..{EndPosition}] \"{Truncate(FullPath, 20)}\"";

    /// <summary>
    /// Creates a property access red syntax node.
    /// </summary>
    internal PropertyAccessSyntax(CreationContext context)
        : base(context)
    {
    }
    
    /// <summary>The object being accessed.</summary>
    public RedLeaf ObjectNode => GetTypedChild<RedLeaf>(0);
    
    /// <summary>The object name as text.</summary>
    public string Object => ObjectNode.Text;
    
    /// <summary>The dot separator.</summary>
    public RedLeaf DotNode => GetTypedChild<RedLeaf>(1);
    
    /// <summary>The property being accessed.</summary>
    public RedLeaf PropertyNode => GetTypedChild<RedLeaf>(2);
    
    /// <summary>The property name as text.</summary>
    public string Property => PropertyNode.Text;
    
    /// <summary>The full member access path (e.g., "obj.property").</summary>
    public string FullPath => $"{Object}.{Property}";
}

#endregion
