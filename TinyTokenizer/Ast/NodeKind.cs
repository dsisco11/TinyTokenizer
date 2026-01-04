namespace TinyTokenizer.Ast;

/// <summary>
/// Unified enum for all node kinds in the AST.
/// Combines leaf token types, container block types, keyword types, and semantic node types.
/// </summary>
/// <remarks>
/// <para>Value ranges:</para>
/// <list type="bullet">
/// <item><description>0-99: Leaf (terminal) nodes</description></item>
/// <item><description>100-999: Container (block) nodes</description></item>
/// <item><description>1000-99999: Keyword nodes (user-defined via Schema)</description></item>
/// <item><description>100000+: Semantic nodes (user-defined via Schema)</description></item>
/// </list>
/// </remarks>
public enum NodeKind : uint
{
    // ============ Leaves (terminals): 0-99 ============
    
    /// <summary>Identifier token.</summary>
    Ident,
    
    /// <summary>Numeric literal (integer or floating point).</summary>
    Numeric,
    
    /// <summary>String literal (including quotes).</summary>
    String,
    
    /// <summary>Operator token (e.g., +, -, ==, =>).</summary>
    Operator,
    
    /// <summary>Symbol token (e.g., ;, ,, .).</summary>
    Symbol,
    
    /// <summary>Tagged identifier (e.g., #define, @attribute).</summary>
    TaggedIdent,
    
    /// <summary>Error token for malformed input.</summary>
    Error,
    
    /// <summary>End-of-file marker (holds trailing trivia at end of source).</summary>
    EndOfFile,
    
    // ============ Containers (non-terminals): 100-999 ============
    
    /// <summary>Brace block: { }</summary>
    BraceBlock = 100,
    
    /// <summary>Bracket block: [ ]</summary>
    BracketBlock,
    
    /// <summary>Parenthesis block: ( )</summary>
    ParenBlock,
    
    /// <summary>Root token list (top-level sequence).</summary>
    TokenList,
    
    // ============ Keywords (user-defined): 1000-99999 ============
    
    /// <summary>
    /// Base value for keyword node kinds.
    /// User-defined keywords are assigned values starting from this value.
    /// Each keyword in each category gets a unique NodeKind.
    /// </summary>
    Keyword = 1000,
    
    // ============ Semantic nodes (user-defined): 100000+ ============
    
    /// <summary>
    /// Base value for semantic node kinds.
    /// User-defined semantic nodes are assigned values starting from this value.
    /// </summary>
    Semantic = 100000,
}

/// <summary>
/// Extension methods for <see cref="NodeKind"/>.
/// </summary>
public static class NodeKindExtensions
{
    /// <summary>
    /// Checks if the kind is a leaf (terminal) node.
    /// </summary>
    public static bool IsLeaf(this NodeKind kind) => (uint)kind < 100;
    
    /// <summary>
    /// Checks if the kind is a container (block) node.
    /// </summary>
    public static bool IsContainer(this NodeKind kind) => (uint)kind >= 100 && (uint)kind < 1000;
    
    /// <summary>
    /// Checks if the kind is a keyword node.
    /// </summary>
    public static bool IsKeyword(this NodeKind kind) => (uint)kind >= 1000 && (uint)kind < 100000;
    
    /// <summary>
    /// Checks if the kind is a semantic (user-defined) node.
    /// </summary>
    public static bool IsSemantic(this NodeKind kind) => (uint)kind >= 100000;
    
    /// <summary>
    /// Creates a keyword node kind from an offset (0-based index within keyword range).
    /// </summary>
    public static NodeKind KeywordKind(int offset) => (NodeKind)(1000 + offset);
    
    /// <summary>
    /// Creates a semantic node kind from an offset (0-based index within semantic range).
    /// </summary>
    public static NodeKind SemanticKind(int offset) => (NodeKind)(100000 + offset);
}
