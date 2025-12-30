namespace TinyTokenizer.Ast;

/// <summary>
/// Marker interface for syntax nodes that have a name property.
/// Enables the <see cref="NamedNodeQueryExtensions.Named{T}"/> extension method
/// for concise querying by name.
/// </summary>
/// <example>
/// <code>
/// public sealed class FunctionSyntax : SyntaxNode, INamedNode
/// {
///     public string Name => NameNode.Text;
///     // ...
/// }
/// 
/// // Usage:
/// Query.Syntax&lt;FunctionSyntax&gt;().Named("main")
/// </code>
/// </example>
public interface INamedNode
{
    /// <summary>
    /// Gets the name of this syntax node.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Extension methods for querying named syntax nodes.
/// </summary>
public static class NamedNodeQueryExtensions
{
    /// <summary>
    /// Filters the query to match only nodes with the specified name.
    /// </summary>
    /// <typeparam name="T">The syntax node type that implements <see cref="INamedNode"/>.</typeparam>
    /// <param name="query">The query to filter.</param>
    /// <param name="name">The name to match.</param>
    /// <returns>A filtered query matching only nodes with the specified name.</returns>
    /// <example>
    /// <code>
    /// var mainFunc = Query.Syntax&lt;FunctionSyntax&gt;().Named("main");
    /// </code>
    /// </example>
    public static SyntaxNodeQuery<T> Named<T>(this SyntaxNodeQuery<T> query, string name)
        where T : SyntaxNode, INamedNode
    {
        return query.Where(n => n.Name == name);
    }
}
