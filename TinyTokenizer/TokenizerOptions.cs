using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Defines a comment style with start and optional end delimiters.
/// </summary>
/// <param name="Start">The string that starts a comment.</param>
/// <param name="End">The string that ends a comment (null for single-line comments that end at newline).</param>
public sealed record CommentStyle(string Start, string? End = null)
{
    /// <summary>
    /// Gets whether this is a multi-line comment style.
    /// </summary>
    public bool IsMultiLine => End is not null;

    /// <summary>
    /// C-style single-line comment: //
    /// </summary>
    public static CommentStyle CStyleSingleLine { get; } = new("//");

    /// <summary>
    /// C-style multi-line comment: /* */
    /// </summary>
    public static CommentStyle CStyleMultiLine { get; } = new("/*", "*/");

    /// <summary>
    /// Hash single-line comment: #
    /// </summary>
    public static CommentStyle HashSingleLine { get; } = new("#");

    /// <summary>
    /// SQL-style single-line comment: --
    /// </summary>
    public static CommentStyle SqlSingleLine { get; } = new("--");

    /// <summary>
    /// HTML/XML comment: &lt;!-- --&gt;
    /// </summary>
    public static CommentStyle HtmlComment { get; } = new("<!--", "-->");
}

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

    /// <summary>
    /// Gets the list of comment styles to recognize.
    /// </summary>
    public ImmutableArray<CommentStyle> CommentStyles { get; init; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="TokenizerOptions"/> with default settings.
    /// </summary>
    public TokenizerOptions()
    {
        Symbols = DefaultSymbols;
        CommentStyles = ImmutableArray<CommentStyle>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TokenizerOptions"/> with the specified symbols.
    /// </summary>
    /// <param name="symbols">The set of symbol characters to recognize.</param>
    public TokenizerOptions(ImmutableHashSet<char> symbols)
    {
        Symbols = symbols;
        CommentStyles = ImmutableArray<CommentStyle>.Empty;
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

    /// <summary>
    /// Creates a new options instance with the specified comment styles.
    /// </summary>
    /// <param name="commentStyles">The comment styles to recognize.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with the specified comment styles.</returns>
    public TokenizerOptions WithCommentStyles(params CommentStyle[] commentStyles)
    {
        return this with { CommentStyles = ImmutableArray.Create(commentStyles) };
    }

    /// <summary>
    /// Creates a new options instance with additional comment styles.
    /// </summary>
    /// <param name="commentStyles">The comment styles to add.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with the additional comment styles.</returns>
    public TokenizerOptions WithAdditionalCommentStyles(params CommentStyle[] commentStyles)
    {
        return this with { CommentStyles = CommentStyles.AddRange(commentStyles) };
    }

    #endregion
}
