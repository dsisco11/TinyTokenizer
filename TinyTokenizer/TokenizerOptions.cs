using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Configuration options for the tokenizer.
/// </summary>
public sealed record TokenizerOptions
{
    #region Default Symbol Set

    /// <summary>
    /// The default set of symbol characters recognized by the tokenizer.
    /// </summary>
    private static readonly ImmutableHashSet<char> DefaultSymbols = ImmutableHashSet.Create(
        '/', ':', ',', ';', '=', '+', '-', '*', '<', '>', '!', '&', '|', '.', '@', '#', '?', '%', '^', '~', '\\'
    );

    #endregion

    #region Properties

    /// <summary>
    /// Gets the set of characters to be recognized as symbol tokens.
    /// </summary>
    public ImmutableHashSet<char> Symbols { get; init; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="TokenizerOptions"/> with default settings.
    /// </summary>
    public TokenizerOptions()
    {
        Symbols = DefaultSymbols;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TokenizerOptions"/> with the specified symbols.
    /// </summary>
    /// <param name="symbols">The set of symbol characters to recognize.</param>
    public TokenizerOptions(ImmutableHashSet<char> symbols)
    {
        Symbols = symbols;
    }

    #endregion

    #region Static Instances

    /// <summary>
    /// Gets the default tokenizer options.
    /// </summary>
    public static TokenizerOptions Default { get; } = new();

    #endregion

    #region Builder Methods

    /// <summary>
    /// Creates a new options instance with additional symbols.
    /// </summary>
    /// <param name="symbols">The symbols to add.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with the additional symbols.</returns>
    public TokenizerOptions WithAdditionalSymbols(params char[] symbols)
    {
        return this with { Symbols = Symbols.Union(symbols) };
    }

    /// <summary>
    /// Creates a new options instance without the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to remove.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> without the specified symbols.</returns>
    public TokenizerOptions WithoutSymbols(params char[] symbols)
    {
        return this with { Symbols = Symbols.Except(symbols) };
    }

    /// <summary>
    /// Creates a new options instance with only the specified symbols.
    /// </summary>
    /// <param name="symbols">The symbols to use.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with only the specified symbols.</returns>
    public TokenizerOptions WithSymbols(params char[] symbols)
    {
        return this with { Symbols = ImmutableHashSet.Create(symbols) };
    }

    #endregion
}
