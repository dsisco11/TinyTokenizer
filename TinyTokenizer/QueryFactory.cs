namespace TinyTokenizer;

/// <summary>
/// Factory class providing fluent methods for creating <see cref="TokenQuery"/> instances.
/// Follows a CSS-like selector pattern for intuitive token selection.
/// </summary>
/// <example>
/// <code>
/// // Select all comments
/// Query.Comment
/// 
/// // Select the first identifier with specific content
/// Query.Ident.WithContent("foo").First()
/// 
/// // Select all parenthesis blocks
/// Query.Block('(')
/// 
/// // Combine queries with union (OR)
/// Query.Comment | Query.Whitespace
/// 
/// // Combine queries with intersection (AND)
/// Query.Ident &amp; Query.WithContentStartingWith("_")
/// </code>
/// </example>
public static class Query
{
    #region Type Selectors

    /// <summary>
    /// Selects any token.
    /// </summary>
    public static TokenQuery Any => new AnyQuery();

    /// <summary>
    /// Selects all <see cref="IdentToken"/> tokens.
    /// </summary>
    public static TokenQuery Ident => new TypeQuery<IdentToken>();

    /// <summary>
    /// Selects all <see cref="WhitespaceToken"/> tokens.
    /// </summary>
    public static TokenQuery Whitespace => new TypeQuery<WhitespaceToken>();

    /// <summary>
    /// Selects all <see cref="CommentToken"/> tokens.
    /// </summary>
    public static TokenQuery Comment => new TypeQuery<CommentToken>();

    /// <summary>
    /// Selects all <see cref="StringToken"/> tokens.
    /// </summary>
    public static TokenQuery String => new TypeQuery<StringToken>();

    /// <summary>
    /// Selects all <see cref="NumericToken"/> tokens.
    /// </summary>
    public static TokenQuery Numeric => new TypeQuery<NumericToken>();

    /// <summary>
    /// Selects all <see cref="OperatorToken"/> tokens.
    /// </summary>
    public static TokenQuery Operator => new TypeQuery<OperatorToken>();

    /// <summary>
    /// Selects all <see cref="SymbolToken"/> tokens.
    /// </summary>
    public static TokenQuery Symbol => new TypeQuery<SymbolToken>();

    /// <summary>
    /// Selects all <see cref="TaggedIdentToken"/> tokens.
    /// </summary>
    public static TokenQuery TaggedIdent => new TypeQuery<TaggedIdentToken>();

    /// <summary>
    /// Selects all <see cref="ErrorToken"/> tokens.
    /// </summary>
    public static TokenQuery Error => new TypeQuery<ErrorToken>();

    /// <summary>
    /// Selects all <see cref="CompositeToken"/> tokens.
    /// </summary>
    public static TokenQuery Composite => new TypeQuery<CompositeToken>();

    #endregion

    #region Block Selectors

    /// <summary>
    /// Selects all <see cref="SimpleBlock"/> tokens.
    /// </summary>
    public static TokenQuery Block() => new BlockQuery();

    /// <summary>
    /// Selects all <see cref="SimpleBlock"/> tokens with the specified opener.
    /// </summary>
    /// <param name="opener">The opening delimiter character ('(', '[', or '{').</param>
    public static TokenQuery Block(char opener) => new BlockQuery { Opener = opener };

    /// <summary>
    /// Selects all brace blocks: { }
    /// </summary>
    public static TokenQuery BraceBlock => new BlockQuery { Opener = '{' };

    /// <summary>
    /// Selects all bracket blocks: [ ]
    /// </summary>
    public static TokenQuery BracketBlock => new BlockQuery { Opener = '[' };

    /// <summary>
    /// Selects all parenthesis blocks: ( )
    /// </summary>
    public static TokenQuery ParenBlock => new BlockQuery { Opener = '(' };

    #endregion

    #region Index Selectors

    /// <summary>
    /// Selects the token at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the token to select.</param>
    public static TokenQuery Index(int index) => new IndexQuery { Index = index };

    /// <summary>
    /// Selects tokens within the specified range.
    /// </summary>
    /// <param name="start">The start index (inclusive).</param>
    /// <param name="end">The end index (exclusive).</param>
    public static TokenQuery Range(int start, int end) => new RangeQuery { Start = start, End = end };

    /// <summary>
    /// Selects the first token in the array.
    /// </summary>
    public static TokenQuery First => new FirstIndexQuery();

    /// <summary>
    /// Selects the last token in the array.
    /// </summary>
    public static TokenQuery Last => new LastIndexQuery();

    #endregion

    #region Pattern Selector

    /// <summary>
    /// Selects tokens that match the specified pattern definition.
    /// Bridges the TokenQuery system with the PatternMatcher system.
    /// </summary>
    /// <param name="definition">The pattern definition to match.</param>
    public static TokenQuery Pattern(ITokenDefinition definition) => new PatternQuery(definition);

    #endregion

    #region Content Filters (Standalone)

    /// <summary>
    /// Creates a query that matches any token with the specified exact content.
    /// </summary>
    /// <param name="content">The exact content to match.</param>
    public static TokenQuery WithContent(string content) => 
        Any.WithContent(content);

    /// <summary>
    /// Creates a query that matches any token containing the specified substring.
    /// </summary>
    /// <param name="substring">The substring to search for.</param>
    public static TokenQuery WithContentContaining(string substring) => 
        Any.WithContentContaining(substring);

    /// <summary>
    /// Creates a query that matches any token starting with the specified prefix.
    /// </summary>
    /// <param name="prefix">The prefix to match.</param>
    public static TokenQuery WithContentStartingWith(string prefix) => 
        Any.WithContentStartingWith(prefix);

    /// <summary>
    /// Creates a query that matches any token ending with the specified suffix.
    /// </summary>
    /// <param name="suffix">The suffix to match.</param>
    public static TokenQuery WithContentEndingWith(string suffix) => 
        Any.WithContentEndingWith(suffix);

    /// <summary>
    /// Creates a query that matches any token satisfying the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test tokens against.</param>
    public static TokenQuery Where(Func<Token, bool> predicate) => 
        Any.Where(predicate);

    #endregion

    #region Type-Specific Factories

    /// <summary>
    /// Provides methods for creating queries targeting specific token types.
    /// </summary>
    public static class Of
    {
        /// <summary>
        /// Creates a query for the specified token type.
        /// </summary>
        /// <typeparam name="T">The token type to select.</typeparam>
        public static TokenQuery Type<T>() where T : Token => new TypeQuery<T>();
    }

    #endregion
}
