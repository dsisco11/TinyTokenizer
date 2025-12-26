using System.Collections.Generic;

namespace TinyTokenizer;

/// <summary>
/// Tracks context for a block being parsed, used for nested block handling
/// in async/streaming tokenization where state must persist across buffer boundaries.
/// </summary>
public sealed class BlockContext
{
    /// <summary>
    /// Gets the opening delimiter character for this block.
    /// </summary>
    public char OpeningDelimiter { get; }

    /// <summary>
    /// Gets the closing delimiter character expected for this block.
    /// </summary>
    public char ClosingDelimiter { get; }

    /// <summary>
    /// Gets the token type for this block.
    /// </summary>
    public TokenType BlockType { get; }

    /// <summary>
    /// Gets the absolute position in the source where this block started.
    /// </summary>
    public long StartPosition { get; }

    /// <summary>
    /// Gets the list of child tokens parsed so far within this block.
    /// </summary>
    public List<Token> Children { get; } = new();

    /// <summary>
    /// Gets or sets the position where inner content starts (after opening delimiter).
    /// </summary>
    public long InnerStartPosition { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="BlockContext"/>.
    /// </summary>
    /// <param name="openingDelimiter">The opening delimiter character.</param>
    /// <param name="closingDelimiter">The closing delimiter character.</param>
    /// <param name="blockType">The token type for this block.</param>
    /// <param name="startPosition">The absolute position where this block started.</param>
    public BlockContext(char openingDelimiter, char closingDelimiter, TokenType blockType, long startPosition)
    {
        OpeningDelimiter = openingDelimiter;
        ClosingDelimiter = closingDelimiter;
        BlockType = blockType;
        StartPosition = startPosition;
        InnerStartPosition = startPosition + 1;
    }
}
