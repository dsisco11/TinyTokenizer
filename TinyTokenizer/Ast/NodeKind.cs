namespace TinyTokenizer.Ast;

/// <summary>
/// Unified enum for all node kinds in the AST.
/// Combines leaf token types, container block types, keyword types, and semantic node types.
/// </summary>
/// <remarks>
/// <para>Value ranges (ushort-compatible, max 65535):</para>
/// <list type="bullet">
/// <item><description>0-99: Leaf (terminal) nodes</description></item>
/// <item><description>100-499: Container (block) nodes</description></item>
/// <item><description>500-1999: Keyword nodes (user-defined via Schema, ~1500 keywords)</description></item>
/// <item><description>2000-65535: Semantic nodes (user-defined via Schema, ~63535 nodes)</description></item>
/// </list>
/// </remarks>
public enum NodeKind : ushort
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
    
    // ============ Containers (non-terminals): 100-499 ============
    
    /// <summary>Brace block: { }</summary>
    BraceBlock = 100,
    
    /// <summary>Bracket block: [ ]</summary>
    BracketBlock,
    
    /// <summary>Parenthesis block: ( )</summary>
    ParenBlock,
    
    /// <summary>Root token list (top-level sequence).</summary>
    TokenList,
    
    // ============ Keywords (user-defined): 500-1999 ============
    
    /// <summary>
    /// Base value for keyword node kinds.
    /// User-defined keywords are assigned values starting from this value.
    /// Each keyword in each category gets a unique NodeKind.
    /// Supports up to ~1500 keywords.
    /// </summary>
    Keyword = 500,
    
    // ============ Semantic nodes (user-defined): 2000-65535 ============
    
    /// <summary>
    /// Base value for semantic node kinds.
    /// User-defined semantic nodes are assigned values starting from this value.
    /// Supports up to ~63535 semantic node types.
    /// </summary>
    Semantic = 2000,
}

/// <summary>
/// Extension methods for <see cref="NodeKind"/>.
/// </summary>
public static class NodeKindExtensions
{
    /// <summary>
    /// Checks if the kind is a leaf (terminal) node.
    /// </summary>
    public static bool IsLeaf(this NodeKind kind) => (ushort)kind < 100;
    
    /// <summary>
    /// Checks if the kind is a container (block) node.
    /// </summary>
    public static bool IsContainer(this NodeKind kind) => (ushort)kind >= 100 && (ushort)kind < 500;
    
    /// <summary>
    /// Checks if the kind is a keyword node.
    /// </summary>
    public static bool IsKeyword(this NodeKind kind) => (ushort)kind >= 500 && (ushort)kind < 2000;
    
    /// <summary>
    /// Checks if the kind is a semantic (user-defined) node.
    /// </summary>
    public static bool IsSemantic(this NodeKind kind) => (ushort)kind >= 2000;
    
    /// <summary>
    /// Creates a keyword node kind from an offset (0-based index within keyword range).
    /// </summary>
    public static NodeKind KeywordKind(int offset) => (NodeKind)(500 + offset);
    
    /// <summary>
    /// Creates a semantic node kind from an offset (0-based index within semantic range).
    /// </summary>
    public static NodeKind SemanticKind(int offset) => (NodeKind)(2000 + offset);
}
