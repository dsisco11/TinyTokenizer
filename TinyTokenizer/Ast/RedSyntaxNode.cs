using System.Collections.Immutable;
using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for schema-defined red syntax nodes.
/// Provides typed access to the underlying GreenSyntaxNode and child navigation.
/// Derived classes add domain-specific properties for accessing matched syntax parts.
/// </summary>
/// <remarks>
/// SyntaxNode extends RedNode, making semantic constructs full participants in the tree.
/// This enables unified tree traversal, queries, and editing that works with both
/// structural nodes (RedLeaf, RedBlock) and semantic nodes (FunctionCallSyntax, etc.).
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

    private readonly RedNode?[] _children;
    
    /// <summary>
    /// Creates a red syntax node from a creation context.
    /// </summary>
    /// <param name="context">The creation context containing green node and position info.</param>
    protected SyntaxNode(CreationContext context)
        : base(context)
    {
        _children = new RedNode?[Green.SlotCount];
    }
    
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
