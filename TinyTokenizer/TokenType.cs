namespace TinyTokenizer;

/// <summary>
/// Defines the type of numeric literal.
/// </summary>
public enum NumericType
{
    /// <summary>
    /// An integer numeric literal.
    /// </summary>
    Integer,

    /// <summary>
    /// A floating-point numeric literal.
    /// </summary>
    FloatingPoint
}

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
    /// Identifier/text content.
    /// </summary>
    Ident,

    /// <summary>
    /// Whitespace characters (spaces, tabs, newlines).
    /// </summary>
    Whitespace,

    /// <summary>
    /// A numeric literal (integer or floating-point).
    /// </summary>
    Numeric,

    /// <summary>
    /// A string literal delimited by single or double quotes.
    /// </summary>
    String,

    /// <summary>
    /// A comment (single-line or multi-line).
    /// </summary>
    Comment,

    /// <summary>
    /// An error token indicating a parsing failure.
    /// </summary>
    Error,

    /// <summary>
    /// An operator such as ==, !=, &amp;&amp;, ||, etc.
    /// Configured via <see cref="TokenizerOptions.Operators"/>.
    /// </summary>
    Operator,

    /// <summary>
    /// A tagged identifier - a prefix character followed by an identifier.
    /// Examples: #define, @attribute, $variable.
    /// Configured via <see cref="TokenizerOptions.TagPrefixes"/>.
    /// </summary>
    TaggedIdent,

    /// <summary>
    /// A composite token created by pattern matching in Level 3.
    /// Wraps matched token sequences from Level 2 output.
    /// </summary>
    Composite
}
