using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Represents a simple block delimited by matching brackets.
/// Contains both the full content (with delimiters) and inner content (without delimiters),
/// along with recursively parsed child tokens.
/// Aligns with W3C CSS Syntax "simple block" concept.
/// </summary>
public sealed record SimpleBlock : Token
{
    /// <inheritdoc/>
    public override TokenType Type => BlockType;

    /// <summary>
    /// Gets the specific block type (BraceBlock, BracketBlock, or ParenthesisBlock).
    /// </summary>
    public required TokenType BlockType { get; init; }

    /// <summary>
    /// Gets the inner content of the block excluding the delimiters.
    /// </summary>
    public required ReadOnlyMemory<char> InnerContent { get; init; }

    /// <summary>
    /// Gets the recursively parsed child tokens within this block.
    /// </summary>
    public required ImmutableArray<Token> Children { get; init; }

    /// <summary>
    /// Gets the opening delimiter token.
    /// </summary>
    public required SimpleToken OpeningDelimiter { get; init; }

    /// <summary>
    /// Gets the closing delimiter token.
    /// </summary>
    public required SimpleToken ClosingDelimiter { get; init; }

    /// <summary>
    /// Gets the inner content as a <see cref="ReadOnlySpan{T}"/> for efficient processing.
    /// </summary>
    public ReadOnlySpan<char> InnerContentSpan => InnerContent.Span;
}
