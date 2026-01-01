namespace TinyTokenizer.Ast;

/// <summary>
/// Static factory for creating node queries.
/// Provides a fluent CSS-like selector API for AST nodes.
/// </summary>
/// <example>
/// <code>
/// // Select by kind with specific text
/// Query.Ident("foo")
/// Query.Symbol(".")
/// Query.Operator("=>")
/// 
/// // Select any of a kind
/// Query.AnyIdent.Where(n => n.Width > 3)
/// 
/// // Select blocks and get inner positions
/// Query.BraceBlock.Where(n => ...).First().InnerStart()
/// 
/// // Combine queries
/// Query.AnyIdent | Query.AnyNumeric
/// </code>
/// </example>
public static class Query
{
    #region Kind Queries - Named (specific text)
    
    /// <summary>Creates a query matching nodes of the specified kind.</summary>
    public static KindNodeQuery Kind(NodeKind kind) => new KindNodeQuery(kind);
    
    /// <summary>Matches identifier nodes with the specified text.</summary>
    /// <param name="text">The exact text to match.</param>
    public static KindNodeQuery Ident(string text) => new KindNodeQuery(NodeKind.Ident).WithText(text);
    
    /// <summary>Matches numeric literal nodes with the specified text.</summary>
    /// <param name="text">The exact text to match.</param>
    public static KindNodeQuery Numeric(string text) => new KindNodeQuery(NodeKind.Numeric).WithText(text);
    
    /// <summary>Matches string literal nodes with the specified text.</summary>
    /// <param name="text">The exact text to match (including quotes).</param>
    public static KindNodeQuery String(string text) => new KindNodeQuery(NodeKind.String).WithText(text);
    
    /// <summary>Matches operator nodes with the specified text.</summary>
    /// <param name="text">The exact operator to match (e.g., "=>", "==").</param>
    public static KindNodeQuery Operator(string text) => new KindNodeQuery(NodeKind.Operator).WithText(text);
    
    /// <summary>Matches symbol nodes with the specified text.</summary>
    /// <param name="text">The exact symbol to match (e.g., ".", ",", ";").</param>
    public static KindNodeQuery Symbol(string text) => new KindNodeQuery(NodeKind.Symbol).WithText(text);
    
    /// <summary>Matches tagged identifier nodes with the specified text.</summary>
    /// <param name="text">The exact text to match (e.g., "#define", "@attribute").</param>
    public static KindNodeQuery TaggedIdent(string text) => new KindNodeQuery(NodeKind.TaggedIdent).WithText(text);
    
    #endregion
    
    #region Kind Queries - Any (no text filter)
    
    /// <summary>Matches any identifier node.</summary>
    public static KindNodeQuery AnyIdent => new KindNodeQuery(NodeKind.Ident);
    
    /// <summary>Matches any numeric literal node.</summary>
    public static KindNodeQuery AnyNumeric => new KindNodeQuery(NodeKind.Numeric);
    
    /// <summary>Matches any string literal node.</summary>
    public static KindNodeQuery AnyString => new KindNodeQuery(NodeKind.String);
    
    /// <summary>Matches any operator node.</summary>
    public static KindNodeQuery AnyOperator => new KindNodeQuery(NodeKind.Operator);
    
    /// <summary>Matches any symbol node.</summary>
    public static KindNodeQuery AnySymbol => new KindNodeQuery(NodeKind.Symbol);
    
    /// <summary>Matches any tagged identifier node (e.g., #define, @attribute).</summary>
    public static KindNodeQuery AnyTaggedIdent => new KindNodeQuery(NodeKind.TaggedIdent);
    
    /// <summary>Matches any error node.</summary>
    public static KindNodeQuery AnyError => new KindNodeQuery(NodeKind.Error);
    
    #endregion
    
    #region Block Queries
    
    /// <summary>Matches brace blocks { }.</summary>
    public static BlockNodeQuery BraceBlock => new('{');
    
    /// <summary>Matches bracket blocks [ ].</summary>
    public static BlockNodeQuery BracketBlock => new('[');
    
    /// <summary>Matches parenthesis blocks ( ).</summary>
    public static BlockNodeQuery ParenBlock => new('(');
    
    /// <summary>Matches any block regardless of delimiter.</summary>
    public static BlockNodeQuery AnyBlock => new();
    
    #endregion
    
    #region General Queries
    
    /// <summary>Matches any node.</summary>
    public static AnyNodeQuery Any => new AnyNodeQuery();
    
    /// <summary>Matches only leaf nodes (non-containers).</summary>
    public static LeafNodeQuery Leaf => new LeafNodeQuery();
    
    /// <summary>
    /// Matches nodes that are preceded by a newline (in trivia or as whitespace token).
    /// Useful for line-based pattern matching.
    /// </summary>
    public static NewlineNodeQuery Newline => new NewlineNodeQuery();
    
    /// <summary>
    /// Matches nodes that are NOT preceded by a newline.
    /// Useful for matching tokens on the same line.
    /// </summary>
    public static NewlineNodeQuery NotNewline => new NewlineNodeQuery(negated: true);
    
    /// <summary>
    /// Zero-width assertion matching nodes at the beginning of the file.
    /// Matches when the node is the first child of the root.
    /// </summary>
    public static BeginningOfFileQuery BOF => new BeginningOfFileQuery();
    
    /// <summary>
    /// Zero-width assertion matching nodes at the end of the file.
    /// Matches when the node is the last child of the root.
    /// </summary>
    public static EndOfFileQuery EOF => new EndOfFileQuery();
    
    #endregion
    
    #region Composition Queries
    
    /// <summary>
    /// Creates a query that matches if any of the provided queries match (variadic OR).
    /// Short-circuits on first match for efficiency.
    /// </summary>
    /// <param name="queries">The queries to try in order.</param>
    /// <returns>A query that matches any of the provided queries.</returns>
    /// <example>
    /// <code>
    /// // Match identifier OR numeric
    /// Query.AnyOf(Query.AnyIdent, Query.AnyNumeric)
    /// </code>
    /// </example>
    public static AnyOfQuery AnyOf(params INodeQuery[] queries) => new AnyOfQuery(queries);
    
    /// <summary>
    /// Creates a query that matches if any of the provided queries match.
    /// </summary>
    public static AnyOfQuery AnyOf(IEnumerable<INodeQuery> queries) => new AnyOfQuery(queries);
    
    /// <summary>
    /// Creates a query that matches if none of the provided queries match.
    /// Consumes 1 node when all inner queries fail.
    /// </summary>
    /// <param name="queries">The queries that must all NOT match.</param>
    /// <returns>A query that matches when none of the provided queries match.</returns>
    /// <example>
    /// <code>
    /// // Match any token that is not an identifier or operator
    /// Query.NoneOf(Query.AnyIdent, Query.AnyOperator)
    /// </code>
    /// </example>
    public static NoneOfQuery NoneOf(params INodeQuery[] queries) => new NoneOfQuery(queries);
    
    /// <summary>
    /// Creates a query that matches if none of the provided queries match.
    /// </summary>
    public static NoneOfQuery NoneOf(IEnumerable<INodeQuery> queries) => new NoneOfQuery(queries);
    
    /// <summary>
    /// Creates a zero-width negative lookahead assertion.
    /// Succeeds when the inner query does NOT match, without consuming any nodes.
    /// </summary>
    /// <param name="query">The query that must NOT match.</param>
    /// <returns>A zero-width negative assertion query.</returns>
    /// <example>
    /// <code>
    /// // Match any identifier that is NOT "if"
    /// Query.Sequence(Query.Not(Query.Ident("if")), Query.AnyIdent)
    /// </code>
    /// </example>
    public static NotQuery Not(INodeQuery query) => new NotQuery(query);
    
    /// <summary>
    /// Creates a query that matches content between a start and end pattern.
    /// Consumes all nodes from start through end (inclusive).
    /// </summary>
    /// <param name="start">The starting delimiter/pattern.</param>
    /// <param name="end">The ending delimiter/pattern.</param>
    /// <param name="inclusive">If true (default), includes start/end in consumed count.</param>
    /// <returns>A query matching the content between start and end.</returns>
    /// <example>
    /// <code>
    /// // Match content between parentheses
    /// Query.Between(Query.Symbol("("), Query.Symbol(")"))
    /// </code>
    /// </example>
    public static BetweenQuery Between(INodeQuery start, INodeQuery end, bool inclusive = true) => 
        new BetweenQuery(start, end, inclusive);
    
    #endregion
    
    #region Navigation Queries
    
    /// <summary>
    /// Creates a query that matches a sibling at a relative offset.
    /// Zero-width - navigates without consuming the current node.
    /// </summary>
    /// <param name="offset">Relative offset: +1 for next sibling, -1 for previous sibling.</param>
    /// <returns>A sibling navigation query.</returns>
    /// <example>
    /// <code>
    /// Query.Sibling(1)  // Matches next sibling
    /// Query.Sibling(-1) // Matches previous sibling
    /// </code>
    /// </example>
    public static SiblingQuery Sibling(int offset) => new SiblingQuery(offset);
    
    /// <summary>
    /// Creates a query that matches a sibling at a relative offset if it matches the inner query.
    /// </summary>
    public static SiblingQuery Sibling(int offset, INodeQuery innerQuery) => new SiblingQuery(offset, innerQuery);
    
    /// <summary>
    /// Creates a query that matches the direct parent of the current node.
    /// Zero-width query for vertical tree navigation.
    /// </summary>
    public static ParentQuery Parent() => new ParentQuery();
    
    /// <summary>
    /// Creates a query that matches the parent if it satisfies the inner query.
    /// </summary>
    public static ParentQuery Parent(INodeQuery innerQuery) => new ParentQuery(innerQuery);
    
    /// <summary>
    /// Creates a query that matches any ancestor satisfying the inner query.
    /// Walks up the tree until finding a match or reaching the root.
    /// </summary>
    /// <param name="innerQuery">The query that an ancestor must satisfy.</param>
    /// <returns>An ancestor navigation query.</returns>
    public static AncestorQuery Ancestor(INodeQuery innerQuery) => new AncestorQuery(innerQuery);
    
    #endregion
    
    #region Syntax Queries
    
    /// <summary>
    /// Creates a query matching syntax nodes of the specified type.
    /// Resolves the NodeKind from the tree's schema at query time.
    /// </summary>
    /// <typeparam name="T">The syntax node type to match.</typeparam>
    /// <example>
    /// <code>
    /// Query.Syntax&lt;GlslFunctionSyntax&gt;().Where(f => f.Name == "main").Select(tree)
    /// </code>
    /// </example>
    public static SyntaxNodeQuery<T> Syntax<T>() where T : SyntaxNode => new SyntaxNodeQuery<T>();
    
    #endregion
    
    #region Sequence Combinators
    
    /// <summary>
    /// Creates a sequence query that matches the specified queries in order.
    /// Each query consumes one or more sibling nodes.
    /// </summary>
    /// <param name="parts">The queries to match in sequence.</param>
    /// <returns>A sequence query.</returns>
    /// <example>
    /// <code>
    /// // Function call: identifier followed by parenthesis block
    /// Query.Sequence(Query.AnyIdent, Query.ParenBlock)
    /// 
    /// // Function definition: type name(params) { body }
    /// Query.Sequence(Query.AnyIdent, Query.AnyIdent, Query.ParenBlock, Query.BraceBlock)
    /// </code>
    /// </example>
    public static SequenceQuery Sequence(params INodeQuery[] parts) => new(parts);
    
    /// <summary>
    /// Creates a sequence query from an enumerable of queries.
    /// </summary>
    public static SequenceQuery Sequence(IEnumerable<INodeQuery> parts) => new(parts);
    
    #endregion
    
    #region Exact Node Query
    
    /// <summary>
    /// Creates a query matching the exact node instance by reference.
    /// Useful when you have a RedNode reference and want to use it with query-based APIs.
    /// </summary>
    /// <param name="node">The exact node to match.</param>
    /// <remarks>
    /// RedNodes are ephemeral - they are recreated on tree mutations. This query is intended for
    /// immediate use within a single tree traversal, not for queries that survive tree changes.
    /// </remarks>
    /// <example>
    /// <code>
    /// var node = tree.Root.Children.First();
    /// editor.Replace(Query.Exact(node), "newValue");
    /// </code>
    /// </example>
    public static ExactNodeQuery Exact(RedNode node) => new ExactNodeQuery(node);
    
    #endregion
}
