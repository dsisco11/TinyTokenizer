namespace TinyTokenizer;

/// <summary>
/// Represents the current parsing mode for resumable tokenization.
/// Used by the async tokenizer to track state across buffer boundaries.
/// </summary>
public enum ParseMode
{
    /// <summary>
    /// Normal parsing mode, ready to parse any token type.
    /// </summary>
    Normal,

    /// <summary>
    /// Currently parsing inside a string literal.
    /// </summary>
    InString,

    /// <summary>
    /// Currently parsing inside a multi-line comment.
    /// </summary>
    InMultiLineComment,

    /// <summary>
    /// Currently parsing inside a single-line comment.
    /// </summary>
    InSingleLineComment,

    /// <summary>
    /// Currently parsing inside a block (waiting for closing delimiter).
    /// </summary>
    InBlock,

    /// <summary>
    /// Currently accumulating numeric digits.
    /// </summary>
    InNumeric,

    /// <summary>
    /// Currently accumulating text characters.
    /// </summary>
    InText,

    /// <summary>
    /// Currently accumulating whitespace characters.
    /// </summary>
    InWhitespace
}
