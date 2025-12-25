using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Represents a declaration block delimited by matching brackets.
/// Contains both the full content (with delimiters) and inner content (without delimiters),
/// along with recursively parsed child tokens.
/// </summary>
public sealed record BlockToken : Token
{
    /// <summary>
    /// Gets the full content of the block including the opening and closing delimiters.
    /// </summary>
    public ReadOnlyMemory<char> FullContent { get; }

    /// <summary>
    /// Gets the inner content of the block excluding the delimiters.
    /// </summary>
    public ReadOnlyMemory<char> InnerContent { get; }

    /// <summary>
    /// Gets the recursively parsed child tokens within this block.
    /// </summary>
    public ImmutableArray<Token> Children { get; }

    /// <summary>
    /// Gets the opening delimiter character.
    /// </summary>
    public char OpeningDelimiter { get; }

    /// <summary>
    /// Gets the closing delimiter character.
    /// </summary>
    public char ClosingDelimiter { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BlockToken"/>.
    /// </summary>
    /// <param name="fullContent">The full content including delimiters.</param>
    /// <param name="innerContent">The inner content excluding delimiters.</param>
    /// <param name="children">The child tokens parsed from the inner content.</param>
    /// <param name="type">The token type indicating the delimiter type.</param>
    /// <param name="openingDelimiter">The opening delimiter character.</param>
    /// <param name="closingDelimiter">The closing delimiter character.</param>
    public BlockToken(
        ReadOnlyMemory<char> fullContent,
        ReadOnlyMemory<char> innerContent,
        ImmutableArray<Token> children,
        TokenType type,
        char openingDelimiter,
        char closingDelimiter)
        : base(fullContent, type)
    {
        FullContent = fullContent;
        InnerContent = innerContent;
        Children = children;
        OpeningDelimiter = openingDelimiter;
        ClosingDelimiter = closingDelimiter;
    }

    /// <summary>
    /// Gets the inner content as a <see cref="ReadOnlySpan{T}"/> for efficient processing.
    /// </summary>
    public ReadOnlySpan<char> InnerContentSpan => InnerContent.Span;

    /// <summary>
    /// Gets the full content as a <see cref="ReadOnlySpan{T}"/> for efficient processing.
    /// </summary>
    public ReadOnlySpan<char> FullContentSpan => FullContent.Span;
}
