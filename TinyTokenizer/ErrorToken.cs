namespace TinyTokenizer;

/// <summary>
/// Represents a parsing error encountered during tokenization.
/// The tokenizer continues from the next character after emitting an error token.
/// </summary>
public sealed record ErrorToken : Token
{
    /// <summary>
    /// Gets the error message describing the parsing failure.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ErrorToken"/>.
    /// </summary>
    /// <param name="content">The content at the error position.</param>
    /// <param name="errorMessage">A message describing the error.</param>
    /// <param name="position">The position in the source where the error occurred.</param>
    public ErrorToken(ReadOnlyMemory<char> content, string errorMessage, long position)
        : base(content, TokenType.Error, position)
    {
        ErrorMessage = errorMessage;
    }
}
