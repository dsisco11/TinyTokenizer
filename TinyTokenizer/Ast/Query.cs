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
}
