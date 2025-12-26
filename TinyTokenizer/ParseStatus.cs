namespace TinyTokenizer;

/// <summary>
/// Represents the result status of a synchronous parse batch operation.
/// </summary>
public enum ParseStatus
{
    /// <summary>
    /// Parsing completed successfully for this batch.
    /// More tokens may be available if there is more input.
    /// </summary>
    Continue,

    /// <summary>
    /// Need more data to complete the current token.
    /// The caller should provide more input and retry.
    /// </summary>
    NeedMoreData,

    /// <summary>
    /// A parsing error was encountered.
    /// An ErrorToken was emitted and parsing continued.
    /// </summary>
    Error,

    /// <summary>
    /// Input stream completed successfully.
    /// No more tokens will be produced.
    /// </summary>
    Complete
}
