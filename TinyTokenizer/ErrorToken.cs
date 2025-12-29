namespace TinyTokenizer;

/// <summary>
/// Represents a parsing error encountered during tokenization.
/// The tokenizer continues from the next character after emitting an error token.
/// </summary>
public sealed record ErrorToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Error;

    /// <summary>
    /// Gets the error message describing the parsing failure.
    /// </summary>
    public required string ErrorMessage { get; init; }
}
