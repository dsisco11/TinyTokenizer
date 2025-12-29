namespace TinyTokenizer.Ast;

/// <summary>
/// Static factory for creating node queries.
/// Provides a fluent CSS-like selector API for AST nodes.
/// </summary>
/// <example>
/// <code>
/// // Select by kind
/// Query.Ident.WithText("foo").First()
/// 
/// // Select blocks and get inner positions
/// Query.BraceBlock.First().InnerStart()
/// 
/// // Combine queries
/// Query.Ident | Query.Numeric
/// </code>
/// </example>
public static class Query
{
    #region Kind Queries
    
    /// <summary>Creates a query matching nodes of the specified kind.</summary>
    public static NodeQuery Kind(NodeKind kind) => new KindNodeQuery(kind);
    
    /// <summary>Matches identifier nodes.</summary>
    public static NodeQuery Ident => new KindNodeQuery(NodeKind.Ident);
    
    /// <summary>Matches numeric literal nodes.</summary>
    public static NodeQuery Numeric => new KindNodeQuery(NodeKind.Numeric);
    
    /// <summary>Matches string literal nodes.</summary>
    public static NodeQuery String => new KindNodeQuery(NodeKind.String);
    
    /// <summary>Matches operator nodes.</summary>
    public static NodeQuery Operator => new KindNodeQuery(NodeKind.Operator);
    
    /// <summary>Matches symbol nodes.</summary>
    public static NodeQuery Symbol => new KindNodeQuery(NodeKind.Symbol);
    
    /// <summary>Matches tagged identifier nodes (e.g., #define, @attribute).</summary>
    public static NodeQuery TaggedIdent => new KindNodeQuery(NodeKind.TaggedIdent);
    
    /// <summary>Matches error nodes.</summary>
    public static NodeQuery Error => new KindNodeQuery(NodeKind.Error);
    
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
    public static NodeQuery Any => new AnyNodeQuery();
    
    /// <summary>Matches only leaf nodes (non-containers).</summary>
    public static NodeQuery Leaf => new LeafNodeQuery();
    
    #endregion
}
