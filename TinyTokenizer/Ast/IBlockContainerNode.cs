namespace TinyTokenizer.Ast;

/// <summary>
/// Marker interface for syntax nodes that contain one or more named blocks.
/// Enables the <see cref="BlockContainerQueryExtensions.InnerStart{T}"/> and
/// <see cref="BlockContainerQueryExtensions.InnerEnd{T}"/> extension methods
/// for concise block content insertion.
/// </summary>
/// <example>
/// <code>
/// public sealed class FunctionSyntax : SyntaxNode, IBlockContainerNode
/// {
///     public RedBlock Parameters => GetTypedChild&lt;RedBlock&gt;(2);
///     public RedBlock Body => GetTypedChild&lt;RedBlock&gt;(3);
///     
///     public IReadOnlyList&lt;string&gt; BlockNames => ["params", "body"];
///     
///     public RedBlock GetBlock(string? name) => name switch
///     {
///         null or "body" => Body,  // default block
///         "params" => Parameters,
///         _ => throw new ArgumentException($"Unknown block: {name}")
///     };
/// }
/// 
/// // Usage:
/// Query.Syntax&lt;FunctionSyntax&gt;().Named("main").InnerStart("body")
/// Query.Syntax&lt;FunctionSyntax&gt;().Named("main").InnerEnd()  // defaults to first block
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
    /// Creates an insertion query for the start of a named block's content.
    /// </summary>
    /// <typeparam name="T">The syntax node type that implements <see cref="IBlockContainerNode"/>.</typeparam>
    /// <param name="query">The query to create an insertion point from.</param>
    /// <param name="blockName">
    /// The name of the block, or null to use the default block.
    /// For nodes with multiple blocks, null throws if ambiguous.
    /// </param>
    /// <returns>An insertion query for the block's inner start position.</returns>
    public static InsertionQuery InnerStart<T>(this SyntaxNodeQuery<T> query, string? blockName = null)
        where T : SyntaxNode, IBlockContainerNode
    {
        return new InsertionQuery(query, InsertionPoint.NamedBlockInnerStart, blockName);
    }
    
    /// <summary>
    /// Creates an insertion query for the end of a named block's content.
    /// </summary>
    /// <typeparam name="T">The syntax node type that implements <see cref="IBlockContainerNode"/>.</typeparam>
    /// <param name="query">The query to create an insertion point from.</param>
    /// <param name="blockName">
    /// The name of the block, or null to use the default block.
    /// For nodes with multiple blocks, null throws if ambiguous.
    /// </param>
    /// <returns>An insertion query for the block's inner end position.</returns>
    public static InsertionQuery InnerEnd<T>(this SyntaxNodeQuery<T> query, string? blockName = null)
        where T : SyntaxNode, IBlockContainerNode
    {
        return new InsertionQuery(query, InsertionPoint.NamedBlockInnerEnd, blockName);
    }
}
