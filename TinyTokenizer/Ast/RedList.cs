using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Red node wrapper for the root token list.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class RedList : RedNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"List[{Position}..{EndPosition}] ({SlotCount} children) \"{Truncate(ToText(), 20)}\"";
    
    /// <summary>
    /// Creates a new red list.
    /// </summary>
    internal RedList(GreenList green, RedNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        : base(green, parent, position, siblingIndex, tree)
    {
    }
    
    /// <summary>The underlying green list.</summary>
    internal new GreenList Green => (GreenList)base.Green;
    
    /// <summary>Number of children.</summary>
    public int ChildCount => Green.SlotCount;
}
