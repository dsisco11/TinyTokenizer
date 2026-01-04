namespace TinyTokenizer.Ast;

/// <summary>
/// Opaque context for creating custom SyntaxNode instances.
/// Used by the library to construct syntax nodes without exposing green layer details.
/// </summary>
/// <remarks>
/// This struct is passed to custom SyntaxNode constructors to provide the necessary
/// green node, parent, and position information without requiring users to
/// directly reference internal green types.
/// </remarks>
public readonly record struct CreationContext
{
    internal GreenNode Green { get; init; }
    
    /// <summary>The parent syntax node, or null if this is the root.</summary>
    public SyntaxNode? Parent { get; init; }
    
    /// <summary>The absolute position in the source text.</summary>
    public int Position { get; init; }
    
    /// <summary>The index of this node within its parent's children, or -1 if root.</summary>
    public int SiblingIndex { get; init; }
    
    /// <summary>The schema for type lookup (optional, for schema-based factory resolution).</summary>
    internal Schema? Schema { get; init; }
    
    internal CreationContext(GreenNode green, SyntaxNode? parent, int position, int siblingIndex = -1, Schema? schema = null)
    {
        Green = green;
        Parent = parent;
        Position = position;
        SiblingIndex = siblingIndex;
        Schema = schema;
    }
}
