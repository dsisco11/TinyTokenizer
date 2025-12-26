using System.Text;

namespace TinyTokenizer;

/// <summary>
/// Maintains tokenizer state across async/await boundaries.
/// This class can be stored in fields and survive async operations,
/// unlike ref structs like SequenceReader.
/// </summary>
public sealed class TokenizerState
{
    /// <summary>
    /// Gets or sets the current parsing mode.
    /// </summary>
    public ParseMode Mode { get; set; } = ParseMode.Normal;

    /// <summary>
    /// Gets the stack of block contexts for nested block parsing.
    /// </summary>
    public Stack<BlockContext> BlockStack { get; } = new();

    /// <summary>
    /// Gets the buffer for accumulating partial token content across buffer boundaries.
    /// </summary>
    public StringBuilder PartialBuffer { get; } = new();

    /// <summary>
    /// Gets or sets the absolute position in the source stream.
    /// </summary>
    public long AbsolutePosition { get; set; }

    /// <summary>
    /// Gets or sets the position where the current partial token started.
    /// </summary>
    public long TokenStartPosition { get; set; }

    /// <summary>
    /// Gets or sets the quote character when parsing a string literal.
    /// </summary>
    public char? StringQuote { get; set; }

    /// <summary>
    /// Gets or sets whether the previous character was an escape character.
    /// </summary>
    public bool IsEscaped { get; set; }

    /// <summary>
    /// Gets or sets the comment style being parsed, if any.
    /// </summary>
    public CommentStyle? CurrentCommentStyle { get; set; }

    /// <summary>
    /// Gets or sets whether a decimal point has been encountered in the current numeric token.
    /// </summary>
    public bool HasDecimalPoint { get; set; }

    /// <summary>
    /// Resets the state to initial values for a fresh parse operation.
    /// </summary>
    public void Reset()
    {
        Mode = ParseMode.Normal;
        BlockStack.Clear();
        PartialBuffer.Clear();
        AbsolutePosition = 0;
        TokenStartPosition = 0;
        StringQuote = null;
        IsEscaped = false;
        CurrentCommentStyle = null;
        HasDecimalPoint = false;
    }

    /// <summary>
    /// Clears partial token state when a token is completed.
    /// </summary>
    public void ClearPartialToken()
    {
        PartialBuffer.Clear();
        Mode = ParseMode.Normal;
        StringQuote = null;
        IsEscaped = false;
        CurrentCommentStyle = null;
        HasDecimalPoint = false;
    }

    /// <summary>
    /// Begins accumulating a new partial token.
    /// </summary>
    /// <param name="mode">The parsing mode for this token.</param>
    /// <param name="startPosition">The position where this token starts.</param>
    public void BeginPartialToken(ParseMode mode, long startPosition)
    {
        Mode = mode;
        TokenStartPosition = startPosition;
        PartialBuffer.Clear();
    }
}
