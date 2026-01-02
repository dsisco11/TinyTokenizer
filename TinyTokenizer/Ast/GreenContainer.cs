using System.Collections.Immutable;
using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Abstract base class for green nodes that contain children (blocks, lists, syntax nodes).
/// Provides shared implementation for child access and structural mutations.
/// </summary>
/// <remarks>
/// Container types differ in:
/// - GreenBlock: has delimiters and trivia
/// - GreenList: root sequence, no delimiters
/// - GreenSyntaxNode: semantic wrapper, no delimiters
/// 
/// All support structural sharing through WithSlot/WithChildren/WithInsert/WithReplace.
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal abstract record GreenContainer : GreenNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Width}] ({SlotCount} children)";

    /// <summary>
    /// Gets the child nodes of this container.
    /// </summary>
    public abstract ImmutableArray<GreenNode> Children { get; }
    
    /// <inheritdoc/>
    public override int SlotCount => Children.Length;
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index) =>
        index >= 0 && index < Children.Length ? Children[index] : null;
    
    /// <summary>
    /// Creates a new container with one child replaced.
    /// Other children are shared by reference.
    /// </summary>
    /// <param name="index">Index of child to replace.</param>
    /// <param name="newChild">The replacement child.</param>
    /// <returns>A new container with the child replaced.</returns>
    public abstract GreenContainer WithSlot(int index, GreenNode newChild);
    
    /// <summary>
    /// Creates a new container with all children replaced.
    /// </summary>
    /// <param name="newChildren">The new children.</param>
    /// <returns>A new container with the new children.</returns>
    public abstract GreenContainer WithChildren(ImmutableArray<GreenNode> newChildren);
    
    /// <summary>
    /// Creates a new container with children inserted at the specified index.
    /// Existing children are shared by reference.
    /// </summary>
    /// <param name="index">Index at which to insert.</param>
    /// <param name="nodes">Nodes to insert.</param>
    /// <returns>A new container with the nodes inserted.</returns>
    public abstract GreenContainer WithInsert(int index, ImmutableArray<GreenNode> nodes);
    
    /// <summary>
    /// Creates a new container with a range of children replaced.
    /// </summary>
    /// <param name="index">Start index of range to replace.</param>
    /// <param name="count">Number of children to replace.</param>
    /// <param name="replacement">Replacement nodes.</param>
    /// <returns>A new container with the range replaced.</returns>
    public abstract GreenContainer WithReplace(int index, int count, ImmutableArray<GreenNode> replacement);
    
    /// <summary>
    /// Creates a new container with children removed from the specified range.
    /// Remaining children are shared by reference.
    /// </summary>
    /// <param name="index">Start index of range to remove.</param>
    /// <param name="count">Number of children to remove.</param>
    /// <returns>A new container with the children removed.</returns>
    public virtual GreenContainer WithRemove(int index, int count) =>
        WithReplace(index, count, ImmutableArray<GreenNode>.Empty);
    
    /// <summary>
    /// Computes the character offset of a child slot from this container's content start.
    /// Default implementation is O(index); subclasses may override for O(1).
    /// </summary>
    /// <param name="index">The slot index.</param>
    /// <returns>The character offset from this container's content start.</returns>
    public override int GetSlotOffset(int index)
    {
        int offset = GetLeadingWidth();
        for (int i = 0; i < index && i < Children.Length; i++)
            offset += Children[i].Width;
        return offset;
    }
}
