using System.Diagnostics;

namespace TinyTokenizer.Ast;

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
    /// Creates a function call syntax node.
    /// </summary>
    internal FunctionCallSyntax(CreationContext context)
        : base(context)
    {
    }
    
    /// <summary>The function name node.</summary>
    public SyntaxToken NameNode => GetTypedChild<SyntaxToken>(0);
    
    /// <summary>The function name as text.</summary>
    public string Name => NameNode.Text;
    
    /// <summary>The arguments block (parentheses).</summary>
    public SyntaxBlock Arguments => GetTypedChild<SyntaxBlock>(1);
    
    /// <summary>
    /// Gets the argument nodes (children of the arguments block, excluding symbols).
    /// </summary>
    public IEnumerable<SyntaxNode> ArgumentNodes =>
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
    /// Creates an array access syntax node.
    /// </summary>
    internal ArrayAccessSyntax(CreationContext context)
        : base(context)
    {
    }
    
    /// <summary>The target being accessed.</summary>
    public SyntaxToken TargetNode => GetTypedChild<SyntaxToken>(0);
    
    /// <summary>The target name as text.</summary>
    public string Target => TargetNode.Text;
    
    /// <summary>The index block (brackets).</summary>
    public SyntaxBlock IndexBlock => GetTypedChild<SyntaxBlock>(1);
    
    /// <summary>
    /// Gets the index nodes (children of the index block, excluding symbols).
    /// </summary>
    public IEnumerable<SyntaxNode> IndexNodes =>
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
    /// Creates a property access syntax node.
    /// </summary>
    internal PropertyAccessSyntax(CreationContext context)
        : base(context)
    {
    }
    
    /// <summary>The object being accessed.</summary>
    public SyntaxToken ObjectNode => GetTypedChild<SyntaxToken>(0);
    
    /// <summary>The object name as text.</summary>
    public string Object => ObjectNode.Text;
    
    /// <summary>The dot separator.</summary>
    public SyntaxToken DotNode => GetTypedChild<SyntaxToken>(1);
    
    /// <summary>The property being accessed.</summary>
    public SyntaxToken PropertyNode => GetTypedChild<SyntaxToken>(2);
    
    /// <summary>The property name as text.</summary>
    public string Property => PropertyNode.Text;
    
    /// <summary>The full member access path (e.g., "obj.property").</summary>
    public string FullPath => $"{Object}.{Property}";
}

#endregion
