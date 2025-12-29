namespace TinyTokenizer.Ast;

/// <summary>
/// Static factory for creating node queries.
/// Provides a fluent CSS-like selector API for AST nodes.
/// </summary>
/// <example>
/// <code>
/// // Select by kind - Where() preserves type
/// Query.Ident.Where(n => ...).WithText("foo").First()
/// 
/// // Select blocks and get inner positions - InnerStart() available after Where()
/// Query.BraceBlock.Where(n => ...).First().InnerStart()
/// 
/// // Combine queries
/// Query.Ident | Query.Numeric
/// </code>
/// </example>
public static class Query
{
    #region Kind Queries
    
    /// <summary>Creates a query matching nodes of the specified kind.</summary>
    public static KindNodeQuery Kind(NodeKind kind) => new KindNodeQuery(kind);
    
    /// <summary>Matches identifier nodes.</summary>
    public static KindNodeQuery Ident => new KindNodeQuery(NodeKind.Ident);
    
    /// <summary>Matches numeric literal nodes.</summary>
    public static KindNodeQuery Numeric => new KindNodeQuery(NodeKind.Numeric);
    
    /// <summary>Matches string literal nodes.</summary>
    public static KindNodeQuery String => new KindNodeQuery(NodeKind.String);
    
    /// <summary>Matches operator nodes.</summary>
    public static KindNodeQuery Operator => new KindNodeQuery(NodeKind.Operator);
    
    /// <summary>Matches symbol nodes.</summary>
    public static KindNodeQuery Symbol => new KindNodeQuery(NodeKind.Symbol);
    
    /// <summary>Matches tagged identifier nodes (e.g., #define, @attribute).</summary>
    public static KindNodeQuery TaggedIdent => new KindNodeQuery(NodeKind.TaggedIdent);
    
    /// <summary>Matches error nodes.</summary>
    public static KindNodeQuery Error => new KindNodeQuery(NodeKind.Error);
    
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
}
