namespace TinyTokenizer;

#region Base Token

/// <summary>
/// Abstract base record for all token types.
/// Tokens are immutable and reference content via <see cref="ReadOnlyMemory{T}"/> to avoid copying.
/// </summary>
public abstract record Token(ReadOnlyMemory<char> Content, TokenType Type)
{
    /// <summary>
    /// Gets the content as a <see cref="ReadOnlySpan{T}"/> for efficient processing.
    /// </summary>
    public ReadOnlySpan<char> ContentSpan => Content.Span;
}

#endregion

#region Concrete Token Types

/// <summary>
/// Represents plain text content that is not a block, symbol, or whitespace.
/// </summary>
public sealed record TextToken(ReadOnlyMemory<char> Content) 
    : Token(Content, TokenType.Text);

/// <summary>
/// Represents whitespace characters (spaces, tabs, newlines).
/// </summary>
public sealed record WhitespaceToken(ReadOnlyMemory<char> Content) 
    : Token(Content, TokenType.Whitespace);

/// <summary>
/// Represents a symbol character such as /, :, ,, ;, etc.
/// </summary>
public sealed record SymbolToken(ReadOnlyMemory<char> Content) 
    : Token(Content, TokenType.Symbol)
{
    /// <summary>
    /// Gets the symbol character.
    /// </summary>
    public char Symbol => Content.Span[0];
}

/// <summary>
/// Represents a numeric literal (integer or floating-point).
/// </summary>
public sealed record NumericToken(ReadOnlyMemory<char> Content, NumericType NumericType) 
    : Token(Content, TokenType.Numeric);

/// <summary>
/// Represents a string literal delimited by single or double quotes.
/// </summary>
public sealed record StringToken(ReadOnlyMemory<char> Content, char Quote) 
    : Token(Content, TokenType.String)
{
    /// <summary>
    /// Gets the string value without the surrounding quotes.
    /// </summary>
    public ReadOnlySpan<char> Value => Content.Span.Length >= 2 
        ? Content.Span[1..^1] 
        : ReadOnlySpan<char>.Empty;
}

#endregion
