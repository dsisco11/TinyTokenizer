namespace TinyTokenizer;

/// <summary>
/// Defines the types of tokens that can be produced by the tokenizer.
/// </summary>
public enum TokenType
{
    /// <summary>
    /// A block delimited by braces: { }
    /// </summary>
    BraceBlock,

    /// <summary>
    /// A block delimited by brackets: [ ]
    /// </summary>
    BracketBlock,

    /// <summary>
    /// A block delimited by parentheses: ( )
    /// </summary>
    ParenthesisBlock,

    /// <summary>
    /// A symbol character such as /, :, ,, ;, etc.
    /// </summary>
    Symbol,

    /// <summary>
    /// Plain text content.
    /// </summary>
    Text,

    /// <summary>
    /// Whitespace characters (spaces, tabs, newlines).
    /// </summary>
    Whitespace,

    /// <summary>
    /// An error token indicating a parsing failure.
    /// </summary>
    Error
}
