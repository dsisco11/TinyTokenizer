namespace TinyTokenizer.Ast;

/// <summary>
/// Marker interface for syntax nodes that contain one or more named blocks.
/// Enables the <see cref="BlockContainerQueryExtensions.Block{T}"/> extension method
/// for accessing named blocks within syntax nodes.
/// </summary>
/// <example>
/// <code>
/// public sealed class FunctionSyntax : SyntaxNode, IBlockContainerNode
/// {
///     public SyntaxBlock Parameters => GetTypedChild&lt;SyntaxBlock&gt;(2);
///     public SyntaxBlock Body => GetTypedChild&lt;SyntaxBlock&gt;(3);
///     
///     public IReadOnlyList&lt;string&gt; BlockNames => ["params", "body"];
///     
///     public SyntaxBlock GetBlock(string? name) => name switch
///     {
///         null or "body" => Body,  // default block
///         "params" => Parameters,
///         _ => throw new ArgumentException($"Unknown block: {name}")
///     };
/// }
/// 
/// // Usage:
/// editor.InsertAfter(Query.Syntax&lt;FunctionSyntax&gt;().Block("body").Start(), "// start")
/// editor.InsertBefore(Query.Syntax&lt;FunctionSyntax&gt;().Block("body").End(), "// end")
/// </code>
/// </example>
public interface IBlockContainerNode
{
    /// <summary>
    /// Gets the names of all blocks in this syntax node.
    /// The first name is the default block for single-argument overloads.
    /// </summary>
    IReadOnlyList<string> BlockNames { get; }
    
    /// <summary>
    /// Gets a block by name.
    /// </summary>
    /// <param name="name">
    /// The name of the block, or null to get the default block.
    /// For nodes with multiple blocks, null throws if there's ambiguity.
    /// </param>
    /// <returns>The block with the specified name.</returns>
    /// <exception cref="ArgumentException">Thrown when the block name is unknown.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when name is null and the node has multiple blocks (ambiguous).
    /// </exception>
    SyntaxBlock GetBlock(string? name = null);
}

/// <summary>
/// Extension methods for querying and inserting into block container syntax nodes.
/// </summary>
public static class BlockContainerQueryExtensions
{
    /// <summary>
    /// Creates a query that selects a named block from matched syntax nodes.
    /// Use with <see cref="BoundaryQuery"/> methods like <see cref="BlockNodeQuery.Start"/> 
    /// and <see cref="BlockNodeQuery.End"/> for block content insertion.
    /// </summary>
    /// <typeparam name="T">The syntax node type that implements <see cref="IBlockContainerNode"/>.</typeparam>
    /// <param name="query">The query to create a block query from.</param>
    /// <param name="blockName">
    /// The name of the block, or null to use the default block.
    /// For nodes with multiple blocks, null throws if ambiguous.
    /// </param>
    /// <returns>A query that selects the named block from matched syntax nodes.</returns>
    /// <example>
    /// <code>
    /// // Insert at the start of a function's body block
    /// editor.InsertAfter(Query.Syntax&lt;FunctionSyntax&gt;().Block("body").Start(), "// start")
    /// 
    /// // Insert at the end of the default block
    /// editor.InsertBefore(Query.Syntax&lt;FunctionSyntax&gt;().Block().End(), "// end")
    /// </code>
    /// </example>
    public static NamedBlockQuery Block<T>(this SyntaxNodeQuery<T> query, string? blockName = null)
        where T : SyntaxNode, IBlockContainerNode
    {
        return new NamedBlockQuery(query, blockName);
    }
}

/// <summary>
/// A query that selects a named block from matched <see cref="IBlockContainerNode"/> syntax nodes.
/// </summary>
public sealed record NamedBlockQuery : INodeQuery
{
    /// <summary>Gets the underlying syntax node query.</summary>
    public INodeQuery InnerQuery { get; }
    
    /// <summary>Gets the block name (null for default block).</summary>
    public string? BlockName { get; }
    
    internal NamedBlockQuery(INodeQuery innerQuery, string? blockName)
    {
        InnerQuery = innerQuery;
        BlockName = blockName;
    }
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxTree tree) => Select(tree.Root);
    
    /// <inheritdoc/>
    public IEnumerable<SyntaxNode> Select(SyntaxNode root)
    {
        foreach (var node in InnerQuery.Select(root))
        {
            if (node is IBlockContainerNode container)
            {
                yield return container.GetBlock(BlockName);
            }
        }
    }
    
    /// <inheritdoc/>
    public bool Matches(SyntaxNode node)
    {
        // A named block query matches if the node is a block that is a child of
        // a syntax node that matches the inner query
        if (node is not SyntaxBlock block)
            return false;
        
        var parent = block.Parent;
        if (parent is not IBlockContainerNode container)
            return false;
        
        if (!InnerQuery.Matches(parent))
            return false;
        
        // Check if this is the right named block
        try
        {
            var namedBlock = container.GetBlock(BlockName);
            return ReferenceEquals(namedBlock.Green, block.Green) && namedBlock.Position == block.Position;
        }
        catch
        {
            return false;
        }
    }
    
    /// <inheritdoc/>
    public bool TryMatch(SyntaxNode startNode, out int consumedCount)
    {
        if (Matches(startNode))
        {
            consumedCount = 1;
            return true;
        }
        consumedCount = 0;
        return false;
    }
    
    /// <summary>
    /// Returns a query that selects the opening delimiter (start) of matched blocks.
    /// Use with <c>InsertAfter</c> to insert at the beginning of block content.
    /// </summary>
    public BoundaryQuery Start() => new BoundaryQuery(this, BoundarySide.Start);
    
    /// <summary>
    /// Returns a query that selects the closing delimiter (end) of matched blocks.
    /// Use with <c>InsertBefore</c> to insert at the end of block content.
    /// </summary>
    public BoundaryQuery End() => new BoundaryQuery(this, BoundarySide.End);
}

/// <summary>
/// Extension methods for directly accessing inner start/end of named blocks in block container queries.
/// These provide a convenient shorthand for the more explicit Block().Start() / Block().End() pattern.
/// </summary>
public static class BlockContainerInsertExtensions
{
    /// <summary>
    /// Returns a boundary query for the inner start of a named block.
    /// Shorthand for <c>.Block(blockName).Start()</c>.
    /// Use with <c>InsertAfter</c> to insert at the beginning of block content.
    /// </summary>
    /// <typeparam name="T">The syntax node type that implements <see cref="IBlockContainerNode"/>.</typeparam>
    /// <param name="query">The query to create a boundary from.</param>
    /// <param name="blockName">The name of the block, or null for the default block.</param>
    /// <returns>A boundary query for the block's inner start position.</returns>
    /// <example>
    /// <code>
    /// // Insert at the start of a function's body block
    /// editor.InsertAfter(Query.Syntax&lt;FunctionSyntax&gt;().Named("main").InnerStart("body"), "// first line")
    /// </code>
    /// </example>
    public static BoundaryQuery InnerStart<T>(this SyntaxNodeQuery<T> query, string? blockName = null)
        where T : SyntaxNode, IBlockContainerNode
    {
        return query.Block(blockName).Start();
    }
    
    /// <summary>
    /// Returns a boundary query for the inner end of a named block.
    /// Shorthand for <c>.Block(blockName).End()</c>.
    /// Use with <c>InsertBefore</c> to insert at the end of block content.
    /// </summary>
    /// <typeparam name="T">The syntax node type that implements <see cref="IBlockContainerNode"/>.</typeparam>
    /// <param name="query">The query to create a boundary from.</param>
    /// <param name="blockName">The name of the block, or null for the default block.</param>
    /// <returns>A boundary query for the block's inner end position.</returns>
    /// <example>
    /// <code>
    /// // Insert at the end of a function's body block  
    /// editor.InsertBefore(Query.Syntax&lt;FunctionSyntax&gt;().Named("main").InnerEnd("body"), "// last line")
    /// </code>
    /// </example>
    public static BoundaryQuery InnerEnd<T>(this SyntaxNodeQuery<T> query, string? blockName = null)
        where T : SyntaxNode, IBlockContainerNode
    {
        return query.Block(blockName).End();
    }
}
