namespace TinyTokenizer.Ast;

/// <summary>
/// Unified enum for all node kinds in the AST.
/// Combines leaf token types, container block types, and semantic node types.
/// </summary>
public enum NodeKind : ushort
{
    // ============ Leaves (terminals) ============
    
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
    
    // ============ Containers (non-terminals) ============
    
    /// <summary>Brace block: { }</summary>
    BraceBlock = 100,
    
    /// <summary>Bracket block: [ ]</summary>
    BracketBlock,
    
    /// <summary>Parenthesis block: ( )</summary>
    ParenBlock,
    
    /// <summary>Root token list (top-level sequence).</summary>
    TokenList,
    
    // ============ Semantic nodes (user-defined) ============
    
    /// <summary>
    /// Base value for semantic node kinds.
    /// User-defined semantic nodes are assigned values starting from this value.
    /// </summary>
    Semantic = 1000,
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
    public static bool IsContainer(this NodeKind kind) => (ushort)kind >= 100 && (ushort)kind < 1000;
    
    /// <summary>
    /// Checks if the kind is a semantic (user-defined) node.
    /// </summary>
    public static bool IsSemantic(this NodeKind kind) => (ushort)kind >= 1000;
    
    /// <summary>
    /// Creates a semantic node kind from an offset (0-based index within semantic range).
    /// </summary>
    public static NodeKind SemanticKind(int offset) => (NodeKind)(1000 + offset);
}
