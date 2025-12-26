namespace TinyTokenizer;

#region Base Token

/// <summary>
/// Abstract base record for all token types.
/// Tokens are immutable and reference content via <see cref="ReadOnlyMemory{T}"/> to avoid copying.
/// </summary>
/// <param name="Content">The token content as memory.</param>
/// <param name="Type">The token type.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public abstract record Token(ReadOnlyMemory<char> Content, TokenType Type, long Position)
{
    /// <summary>
    /// Gets the content as a <see cref="ReadOnlySpan{T}"/> for efficient processing.
    /// </summary>
    public ReadOnlySpan<char> ContentSpan => Content.Span;
}

#endregion

#region Concrete Token Types

/// <summary>
/// Represents identifier/text content that is not a block, symbol, or whitespace.
/// </summary>
/// <param name="Content">The identifier content.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public sealed record IdentToken(ReadOnlyMemory<char> Content, long Position = 0) 
    : Token(Content, TokenType.Ident, Position);

/// <summary>
/// Represents whitespace characters (spaces, tabs, newlines).
/// </summary>
/// <param name="Content">The whitespace content.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public sealed record WhitespaceToken(ReadOnlyMemory<char> Content, long Position = 0) 
    : Token(Content, TokenType.Whitespace, Position);

/// <summary>
/// Represents a symbol character such as /, :, ,, ;, etc.
/// </summary>
/// <param name="Content">The symbol content.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public sealed record SymbolToken(ReadOnlyMemory<char> Content, long Position = 0) 
    : Token(Content, TokenType.Symbol, Position)
{
    /// <summary>
    /// Gets the symbol character.
    /// </summary>
    public char Symbol => Content.Span[0];
}

/// <summary>
/// Represents a numeric literal (integer or floating-point).
/// </summary>
/// <param name="Content">The numeric content.</param>
/// <param name="NumericType">The type of numeric literal.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public sealed record NumericToken(ReadOnlyMemory<char> Content, NumericType NumericType, long Position = 0) 
    : Token(Content, TokenType.Numeric, Position);

/// <summary>
/// Represents a string literal delimited by single or double quotes.
/// </summary>
/// <param name="Content">The full string content including quotes.</param>
/// <param name="Quote">The quote character used.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public sealed record StringToken(ReadOnlyMemory<char> Content, char Quote, long Position = 0) 
    : Token(Content, TokenType.String, Position)
{
    /// <summary>
    /// Gets the string value without the surrounding quotes.
    /// </summary>
    public ReadOnlySpan<char> Value => Content.Span.Length >= 2 
        ? Content.Span[1..^1] 
        : ReadOnlySpan<char>.Empty;
}

/// <summary>
/// Represents a comment token.
/// </summary>
/// <param name="Content">The comment content including delimiters.</param>
/// <param name="IsMultiLine">Whether this is a multi-line comment.</param>
/// <param name="Position">The absolute position in the source where this token starts.</param>
public sealed record CommentToken(ReadOnlyMemory<char> Content, bool IsMultiLine, long Position = 0) 
    : Token(Content, TokenType.Comment, Position);

#endregion
