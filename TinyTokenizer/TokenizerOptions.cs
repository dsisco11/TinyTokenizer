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
/// Provides predefined sets of operators for common programming language families.
/// </summary>
public static class CommonOperators
{
    /// <summary>
    /// Universal operators common to most programming languages: ==, !=, &amp;&amp;, ||
    /// </summary>
    public static ImmutableHashSet<string> Universal { get; } = ImmutableHashSet.Create(
        "==", "!=", "&&", "||"
    );

    /// <summary>
    /// C-family operators (C, C++, C#, Java, JavaScript, etc.)
    /// Includes: ==, !=, &amp;&amp;, ||, &lt;=, &gt;=, ++, --, +=, -=, *=, /=, %=, &amp;=, |=, ^=, &lt;&lt;, &gt;&gt;, -&gt;, ::
    /// </summary>
    public static ImmutableHashSet<string> CFamily { get; } = ImmutableHashSet.Create(
        "==", "!=", "&&", "||",
        "<=", ">=", "++", "--",
        "+=", "-=", "*=", "/=", "%=",
        "&=", "|=", "^=",
        "<<", ">>",
        "->", "::"
    );

    /// <summary>
    /// JavaScript/TypeScript operators.
    /// Includes C-family operators plus: ===, !==, =&gt;, ?., ??, ??=, **
    /// </summary>
    public static ImmutableHashSet<string> JavaScript { get; } = CFamily.Union(ImmutableHashSet.Create(
        "===", "!==", "=>", "?.", "??", "??=", "**"
    ));

    /// <summary>
    /// Python operators.
    /// Includes: ==, !=, &amp;&amp;, ||, &lt;=, &gt;=, //, **, -&gt;, :=, @
    /// </summary>
    public static ImmutableHashSet<string> Python { get; } = ImmutableHashSet.Create(
        "==", "!=", "&&", "||",
        "<=", ">=",
        "//", "**",
        "->", ":="
    );

    /// <summary>
    /// SQL operators.
    /// Includes: ==, !=, &lt;&gt;, &lt;=, &gt;=, ||, ::
    /// </summary>
    public static ImmutableHashSet<string> Sql { get; } = ImmutableHashSet.Create(
        "==", "!=", "<>", "<=", ">=", "||", "::"
    );
}

/// <summary>
/// Configuration options for the tokenizer.
/// </summary>
public sealed record TokenizerOptions
{
    #region Default Symbol Set

    /// <summary>
    /// The default set of symbol characters recognized by the tokenizer.
    /// Note: Some characters like '.', '/', '*', and '\\' have dedicated SimpleTokenType values
    /// in the two-level architecture (Lexer + TokenParser), but are still included here
    /// for backward compatibility with the original Tokenizer.
    /// </summary>
    private static readonly ImmutableHashSet<char> DefaultSymbols = ImmutableHashSet.Create(
        '/', ':', ',', ';', '=', '+', '-', '*', '<', '>', '!', '&', '|', '.', '@', '#', '?', '%', '^', '~', '\\'
    );

    /// <summary>
    /// The default set of operators (multi-character sequences).
    /// Defaults to universal operators: ==, !=, &amp;&amp;, ||
    /// </summary>
    private static readonly ImmutableHashSet<string> DefaultOperators = CommonOperators.Universal;

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

    /// <summary>
    /// Gets the set of operator strings to recognize.
    /// When the tokenizer encounters a sequence of symbol characters that matches
    /// one of these operators, it emits an <see cref="OperatorToken"/> instead of
    /// individual <see cref="SymbolToken"/>s.
    /// Longer operators are matched first (greedy matching).
    /// </summary>
    public ImmutableHashSet<string> Operators { get; init; }

    /// <summary>
    /// Gets whether directive parsing is enabled.
    /// When true, sequences like #identifier are parsed as <see cref="DirectiveToken"/>s.
    /// </summary>
    public bool EnableDirectives { get; init; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of <see cref="TokenizerOptions"/> with default settings.
    /// </summary>
    public TokenizerOptions()
    {
        Symbols = DefaultSymbols;
        CommentStyles = ImmutableArray<CommentStyle>.Empty;
        Operators = DefaultOperators;
        EnableDirectives = false;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TokenizerOptions"/> with the specified symbols.
    /// </summary>
    /// <param name="symbols">The set of symbol characters to recognize.</param>
    public TokenizerOptions(ImmutableHashSet<char> symbols)
    {
        Symbols = symbols;
        CommentStyles = ImmutableArray<CommentStyle>.Empty;
        Operators = DefaultOperators;
        EnableDirectives = false;
    }

    #endregion

    #region Static Instances

    /// <summary>
    /// Gets the default tokenizer options.
    /// </summary>
    public static TokenizerOptions Default { get; } = new();

    #endregion

    #region Builder Methods - Symbols

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

    #region Builder Methods - Comments

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

    #region Builder Methods - Operators

    /// <summary>
    /// Creates a new options instance with the specified operators.
    /// </summary>
    /// <param name="operators">The operators to recognize.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with the specified operators.</returns>
    public TokenizerOptions WithOperators(params string[] operators)
    {
        return this with { Operators = ImmutableHashSet.Create(operators) };
    }

    /// <summary>
    /// Creates a new options instance with the specified operator set.
    /// </summary>
    /// <param name="operators">The operator set to use.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with the specified operators.</returns>
    public TokenizerOptions WithOperators(ImmutableHashSet<string> operators)
    {
        return this with { Operators = operators };
    }

    /// <summary>
    /// Creates a new options instance with additional operators.
    /// </summary>
    /// <param name="operators">The operators to add.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> with the additional operators.</returns>
    public TokenizerOptions WithAdditionalOperators(params string[] operators)
    {
        return this with { Operators = Operators.Union(operators) };
    }

    /// <summary>
    /// Creates a new options instance without the specified operators.
    /// </summary>
    /// <param name="operators">The operators to remove.</param>
    /// <returns>A new <see cref="TokenizerOptions"/> without the specified operators.</returns>
    public TokenizerOptions WithoutOperators(params string[] operators)
    {
        return this with { Operators = Operators.Except(operators) };
    }

    /// <summary>
    /// Creates a new options instance with no operators (all symbol characters emit as individual SymbolTokens).
    /// </summary>
    /// <returns>A new <see cref="TokenizerOptions"/> with no operators.</returns>
    public TokenizerOptions WithNoOperators()
    {
        return this with { Operators = ImmutableHashSet<string>.Empty };
    }

    #endregion

    #region Builder Methods - Directives

    /// <summary>
    /// Creates a new options instance with directive parsing enabled.
    /// </summary>
    /// <returns>A new <see cref="TokenizerOptions"/> with directive parsing enabled.</returns>
    public TokenizerOptions WithDirectives()
    {
        return this with { EnableDirectives = true };
    }

    /// <summary>
    /// Creates a new options instance with directive parsing disabled.
    /// </summary>
    /// <returns>A new <see cref="TokenizerOptions"/> with directive parsing disabled.</returns>
    public TokenizerOptions WithoutDirectives()
    {
        return this with { EnableDirectives = false };
    }

    #endregion
}
