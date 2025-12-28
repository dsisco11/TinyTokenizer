namespace TinyTokenizer.Ast;

/// <summary>
/// Unified enum for all node kinds in the AST.
/// Combines leaf token types and container block types.
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
    
    // ============ Containers (non-terminals) ============
    
    /// <summary>Brace block: { }</summary>
    BraceBlock = 100,
    
    /// <summary>Bracket block: [ ]</summary>
    BracketBlock,
    
    /// <summary>Parenthesis block: ( )</summary>
    ParenBlock,
    
    /// <summary>Root token list (top-level sequence).</summary>
    TokenList,
}
