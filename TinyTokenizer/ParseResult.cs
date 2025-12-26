using System.Buffers;
using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Represents the result of a synchronous parse batch operation.
/// Contains the tokens parsed, the status, and position information for buffer management.
/// </summary>
public readonly struct ParseResult
{
    /// <summary>
    /// Gets the tokens parsed in this batch.
    /// </summary>
    public ImmutableArray<Token> Tokens { get; init; }

    /// <summary>
    /// Gets the parse status indicating how to proceed.
    /// </summary>
    public ParseStatus Status { get; init; }

    /// <summary>
    /// Gets the position up to which data has been consumed (can be released).
    /// </summary>
    public SequencePosition Consumed { get; init; }

    /// <summary>
    /// Gets the position up to which data has been examined (for backpressure).
    /// </summary>
    public SequencePosition Examined { get; init; }

    /// <summary>
    /// Gets the error message if Status is Error, otherwise null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful parse result with tokens.
    /// </summary>
    public static ParseResult Success(ImmutableArray<Token> tokens, SequencePosition consumed, SequencePosition examined)
    {
        return new ParseResult
        {
            Tokens = tokens,
            Status = ParseStatus.Continue,
            Consumed = consumed,
            Examined = examined,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a result indicating more data is needed.
    /// </summary>
    public static ParseResult NeedMore(ImmutableArray<Token> tokens, SequencePosition consumed, SequencePosition examined)
    {
        return new ParseResult
        {
            Tokens = tokens,
            Status = ParseStatus.NeedMoreData,
            Consumed = consumed,
            Examined = examined,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a result indicating parsing is complete.
    /// </summary>
    public static ParseResult Completed(ImmutableArray<Token> tokens, SequencePosition consumed, SequencePosition examined)
    {
        return new ParseResult
        {
            Tokens = tokens,
            Status = ParseStatus.Complete,
            Consumed = consumed,
            Examined = examined,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a result indicating an error occurred (but parsing continued).
    /// </summary>
    public static ParseResult WithError(ImmutableArray<Token> tokens, SequencePosition consumed, SequencePosition examined, string errorMessage)
    {
        return new ParseResult
        {
            Tokens = tokens,
            Status = ParseStatus.Error,
            Consumed = consumed,
            Examined = examined,
            ErrorMessage = errorMessage
        };
    }
}
