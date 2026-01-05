using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Base class for semantic nodes - typed wrappers around matched AST node sequences.
/// Semantic nodes provide domain-specific accessors for patterns like function calls,
/// property accesses, etc.
/// </summary>
public abstract class SemanticNode
{
    private readonly NodeMatch _match;
    private readonly NodeKind _kind;
    
    /// <summary>
    /// Creates a semantic node from a pattern match.
    /// </summary>
    /// <param name="match">The match result containing captured parts.</param>
    /// <param name="kind">The assigned NodeKind for this semantic node type.</param>
    protected SemanticNode(NodeMatch match, NodeKind kind)
    {
        _match = match;
        _kind = kind;
    }
    
    /// <summary>
    /// The assigned NodeKind for this semantic node (from schema registration).
    /// </summary>
    public NodeKind Kind => _kind;
    
    /// <summary>
    /// The first matched node (anchor point for position information).
    /// </summary>
    public SyntaxNode Anchor => _match.Parts[0];
    
    /// <summary>
    /// Absolute position in source text (from anchor).
    /// </summary>
    public int Position => _match.Position;
    
    /// <summary>
    /// Total width of all matched nodes.
    /// </summary>
    public int Width => _match.Width;
    
    /// <summary>
    /// End position (exclusive) in source text.
    /// </summary>
    public int EndPosition => Position + Width;
    
    /// <summary>
    /// Number of parts captured by the pattern.
    /// </summary>
    public int PartCount => _match.Parts.Length;
    
    /// <summary>
    /// All captured parts from the pattern match.
    /// </summary>
    public ImmutableArray<SyntaxNode> Parts => _match.Parts;
    
    /// <summary>
    /// Gets a captured part by index.
    /// </summary>
    protected SyntaxNode Part(int index) => _match.Parts[index];
    
    /// <summary>
    /// Gets a captured part by index, cast to the specified type.
    /// </summary>
    protected T Part<T>(int index) where T : SyntaxNode => (T)_match.Parts[index];
    
    /// <summary>
    /// Tries to get a captured part by index, returning null if out of range.
    /// </summary>
    protected SyntaxNode? TryGetPart(int index) =>
        index >= 0 && index < _match.Parts.Length ? _match.Parts[index] : null;
    
    /// <summary>
    /// Tries to get a captured part by index, cast to the specified type.
    /// Returns null if out of range or wrong type.
    /// </summary>
    protected T? TryGetPart<T>(int index) where T : SyntaxNode =>
        TryGetPart(index) as T;
    
    /// <summary>
    /// Gets the underlying match result.
    /// </summary>
    protected NodeMatch Match => _match;
}

#region Built-in Semantic Nodes

/// <summary>
/// Function name: an identifier immediately followed by a parenthesis block.
/// Pattern: Ident (when followed by ParenBlock)
/// Example: In "func(a, b)", matches just "func"
/// 
/// Note: This captures only the function name, not the arguments.
/// Use the sibling ParenBlock to access arguments separately.
/// </summary>
public sealed class FunctionNameNode : SemanticNode
{
    public FunctionNameNode(NodeMatch match, NodeKind kind) : base(match, kind) { }
    
    /// <summary>The function name node.</summary>
    public SyntaxToken NameNode => Part<SyntaxToken>(0);
    
    /// <summary>The function name as text.</summary>
    public string Name => NameNode.Text;
    
    /// <summary>
    /// Gets the arguments block following this function name, if present.
    /// </summary>
    public RedBlock? Arguments => NameNode.NextSibling() as RedBlock;
}

/// <summary>
/// Array/indexer access: identifier followed by bracketed index.
/// Pattern: Ident + BracketBlock
/// Example: arr[0]
/// </summary>
public sealed class ArrayAccessNode : SemanticNode
{
    public ArrayAccessNode(NodeMatch match, NodeKind kind) : base(match, kind) { }
    
    /// <summary>The array/object being accessed.</summary>
    public SyntaxToken TargetNode => Part<SyntaxToken>(0);
    
    /// <summary>The target name as text.</summary>
    public string Target => TargetNode.Text;
    
    /// <summary>The index block (brackets).</summary>
    public RedBlock IndexBlock => Part<RedBlock>(1);
    
    /// <summary>The index expression nodes.</summary>
    public IEnumerable<SyntaxNode> IndexNodes =>
        IndexBlock.Children.Where(c => c.Kind != NodeKind.Symbol);
}

/// <summary>
/// Property/member access: identifier, dot, identifier.
/// Pattern: Ident + Symbol(".") + Ident
/// Example: obj.property
/// </summary>
public sealed class PropertyAccessNode : SemanticNode
{
    public PropertyAccessNode(NodeMatch match, NodeKind kind) : base(match, kind) { }
    
    /// <summary>The object being accessed.</summary>
    public SyntaxToken ObjectNode => Part<SyntaxToken>(0);
    
    /// <summary>The object name as text.</summary>
    public string Object => ObjectNode.Text;
    
    /// <summary>The property being accessed.</summary>
    public SyntaxToken PropertyNode => Part<SyntaxToken>(2);
    
    /// <summary>The property name as text.</summary>
    public string Property => PropertyNode.Text;
}

/// <summary>
/// Method call: identifier, dot, identifier, parenthesized arguments.
/// Pattern: Ident + Symbol(".") + Ident + ParenBlock
/// Example: obj.method(args)
/// </summary>
public sealed class MethodCallNode : SemanticNode
{
    public MethodCallNode(NodeMatch match, NodeKind kind) : base(match, kind) { }
    
    /// <summary>The object being called on.</summary>
    public SyntaxToken ObjectNode => Part<SyntaxToken>(0);
    
    /// <summary>The object name as text.</summary>
    public string Object => ObjectNode.Text;
    
    /// <summary>The method being called.</summary>
    public SyntaxToken MethodNode => Part<SyntaxToken>(2);
    
    /// <summary>The method name as text.</summary>
    public string Method => MethodNode.Text;
    
    /// <summary>The arguments block (parentheses).</summary>
    public RedBlock Arguments => Part<RedBlock>(3);
    
    /// <summary>The argument nodes.</summary>
    public IEnumerable<SyntaxNode> ArgumentNodes =>
        Arguments.Children.Where(c => c.Kind != NodeKind.Symbol);
}

#endregion
