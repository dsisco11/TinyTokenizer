using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Builder for creating new green trees with structural sharing.
/// Mutations only recreate nodes along the edit path; siblings are shared by reference.
/// </summary>
public class GreenTreeBuilder
{
    private readonly GreenNode _root;
    
    /// <summary>
    /// Creates a new builder from an existing green root.
    /// </summary>
    public GreenTreeBuilder(GreenNode root)
    {
        _root = root;
    }
    
    /// <summary>
    /// Inserts nodes at a path. Returns new root with structural sharing.
    /// </summary>
    /// <param name="path">Sequence of child indices from root to target container.</param>
    /// <param name="insertIndex">Index within the target container to insert at.</param>
    /// <param name="nodes">Nodes to insert.</param>
    public GreenNode InsertAt(ReadOnlySpan<int> path, int insertIndex, ImmutableArray<GreenNode> nodes)
    {
        return InsertAtRecursive(_root, path, 0, insertIndex, nodes);
    }
    
    /// <summary>
    /// Inserts nodes at a path (array overload).
    /// </summary>
    public GreenNode InsertAt(int[] path, int insertIndex, ImmutableArray<GreenNode> nodes)
    {
        return InsertAt(path.AsSpan(), insertIndex, nodes);
    }
    
    /// <summary>
    /// Removes nodes at a path. Returns new root with structural sharing.
    /// </summary>
    /// <param name="path">Sequence of child indices from root to target container.</param>
    /// <param name="startIndex">Index of first node to remove.</param>
    /// <param name="count">Number of nodes to remove.</param>
    public GreenNode RemoveAt(ReadOnlySpan<int> path, int startIndex, int count)
    {
        return ReplaceAtRecursive(_root, path, 0, startIndex, count, ImmutableArray<GreenNode>.Empty);
    }
    
    /// <summary>
    /// Removes nodes at a path (array overload).
    /// </summary>
    public GreenNode RemoveAt(int[] path, int startIndex, int count)
    {
        return RemoveAt(path.AsSpan(), startIndex, count);
    }
    
    /// <summary>
    /// Replaces nodes at a path. Returns new root with structural sharing.
    /// </summary>
    /// <param name="path">Sequence of child indices from root to target container.</param>
    /// <param name="startIndex">Index of first node to replace.</param>
    /// <param name="count">Number of nodes to replace.</param>
    /// <param name="replacement">Replacement nodes.</param>
    public GreenNode ReplaceAt(ReadOnlySpan<int> path, int startIndex, int count, ImmutableArray<GreenNode> replacement)
    {
        return ReplaceAtRecursive(_root, path, 0, startIndex, count, replacement);
    }
    
    /// <summary>
    /// Replaces nodes at a path (array overload).
    /// </summary>
    public GreenNode ReplaceAt(int[] path, int startIndex, int count, ImmutableArray<GreenNode> replacement)
    {
        return ReplaceAt(path.AsSpan(), startIndex, count, replacement);
    }
    
    /// <summary>
    /// Replaces a single child at a path. Returns new root with structural sharing.
    /// </summary>
    public GreenNode ReplaceChild(ReadOnlySpan<int> path, int childIndex, GreenNode newChild)
    {
        return ReplaceChildRecursive(_root, path, 0, childIndex, newChild);
    }
    
    /// <summary>
    /// Replaces a single child at a path (array overload).
    /// </summary>
    public GreenNode ReplaceChild(int[] path, int childIndex, GreenNode newChild)
    {
        return ReplaceChild(path.AsSpan(), childIndex, newChild);
    }
    
    /// <summary>
    /// Updates the leading trivia of a node at the specified path.
    /// </summary>
    /// <param name="path">Sequence of child indices from root to target container.</param>
    /// <param name="childIndex">Index of the child node to update.</param>
    /// <param name="newLeadingTrivia">The new leading trivia.</param>
    public GreenNode UpdateLeadingTrivia(ReadOnlySpan<int> path, int childIndex, ImmutableArray<GreenTrivia> newLeadingTrivia)
    {
        return UpdateLeadingTriviaRecursive(_root, path, 0, childIndex, newLeadingTrivia);
    }
    
    /// <summary>
    /// Updates the leading trivia of a node at the specified path (array overload).
    /// </summary>
    public GreenNode UpdateLeadingTrivia(int[] path, int childIndex, ImmutableArray<GreenTrivia> newLeadingTrivia)
    {
        return UpdateLeadingTrivia(path.AsSpan(), childIndex, newLeadingTrivia);
    }
    
    #region Recursive Implementation
    
    private static GreenNode InsertAtRecursive(
        GreenNode current,
        ReadOnlySpan<int> path,
        int pathIndex,
        int insertIndex,
        ImmutableArray<GreenNode> nodes)
    {
        if (pathIndex >= path.Length)
        {
            // Reached target - do the insertion
            return current switch
            {
                GreenBlock block => block.WithInsert(insertIndex, nodes),
                GreenList list => list.WithInsert(insertIndex, nodes),
                _ => throw new InvalidOperationException("Cannot insert into leaf node")
            };
        }
        
        // Descend into child
        int childSlot = path[pathIndex];
        var child = current.GetSlot(childSlot)
            ?? throw new InvalidOperationException($"No child at slot {childSlot}");
        
        var modifiedChild = InsertAtRecursive(child, path, pathIndex + 1, insertIndex, nodes);
        
        // Replace the child slot with modified version
        return current switch
        {
            GreenBlock block => block.WithSlot(childSlot, modifiedChild),
            GreenList list => list.WithSlot(childSlot, modifiedChild),
            _ => throw new InvalidOperationException("Cannot descend into leaf node")
        };
    }
    
    private static GreenNode ReplaceAtRecursive(
        GreenNode current,
        ReadOnlySpan<int> path,
        int pathIndex,
        int startIndex,
        int count,
        ImmutableArray<GreenNode> replacement)
    {
        if (pathIndex >= path.Length)
        {
            // Reached target - do the replacement
            return current switch
            {
                GreenBlock block => block.WithReplace(startIndex, count, replacement),
                GreenList list => list.WithReplace(startIndex, count, replacement),
                _ => throw new InvalidOperationException("Cannot replace in leaf node")
            };
        }
        
        // Descend into child
        int childSlot = path[pathIndex];
        var child = current.GetSlot(childSlot)
            ?? throw new InvalidOperationException($"No child at slot {childSlot}");
        
        var modifiedChild = ReplaceAtRecursive(child, path, pathIndex + 1, startIndex, count, replacement);
        
        return current switch
        {
            GreenBlock block => block.WithSlot(childSlot, modifiedChild),
            GreenList list => list.WithSlot(childSlot, modifiedChild),
            _ => throw new InvalidOperationException("Cannot descend into leaf node")
        };
    }
    
    private static GreenNode ReplaceChildRecursive(
        GreenNode current,
        ReadOnlySpan<int> path,
        int pathIndex,
        int childIndex,
        GreenNode newChild)
    {
        if (pathIndex >= path.Length)
        {
            // Reached target - replace the child
            return current switch
            {
                GreenBlock block => block.WithSlot(childIndex, newChild),
                GreenList list => list.WithSlot(childIndex, newChild),
                _ => throw new InvalidOperationException("Cannot replace child in leaf node")
            };
        }
        
        // Descend into child
        int childSlot = path[pathIndex];
        var child = current.GetSlot(childSlot)
            ?? throw new InvalidOperationException($"No child at slot {childSlot}");
        
        var modifiedChild = ReplaceChildRecursive(child, path, pathIndex + 1, childIndex, newChild);
        
        return current switch
        {
            GreenBlock block => block.WithSlot(childSlot, modifiedChild),
            GreenList list => list.WithSlot(childSlot, modifiedChild),
            _ => throw new InvalidOperationException("Cannot descend into leaf node")
        };
    }
    
    private static GreenNode UpdateLeadingTriviaRecursive(
        GreenNode current,
        ReadOnlySpan<int> path,
        int pathIndex,
        int childIndex,
        ImmutableArray<GreenTrivia> newLeadingTrivia)
    {
        if (pathIndex >= path.Length)
        {
            // Reached target container - update the child's leading trivia
            var child = current.GetSlot(childIndex)
                ?? throw new InvalidOperationException($"No child at slot {childIndex}");
            
            var modifiedChild = child switch
            {
                GreenLeaf leaf => leaf.WithLeadingTrivia(newLeadingTrivia),
                GreenBlock block => new GreenBlock(block.Opener, block.Children, newLeadingTrivia, block.TrailingTrivia),
                _ => child // Can't update trivia on other node types
            };
            
            return current switch
            {
                GreenBlock block => block.WithSlot(childIndex, modifiedChild),
                GreenList list => list.WithSlot(childIndex, modifiedChild),
                _ => throw new InvalidOperationException("Cannot update child in leaf node")
            };
        }
        
        // Descend into child
        int childSlot = path[pathIndex];
        var pathChild = current.GetSlot(childSlot)
            ?? throw new InvalidOperationException($"No child at slot {childSlot}");
        
        var modifiedPathChild = UpdateLeadingTriviaRecursive(pathChild, path, pathIndex + 1, childIndex, newLeadingTrivia);
        
        return current switch
        {
            GreenBlock block => block.WithSlot(childSlot, modifiedPathChild),
            GreenList list => list.WithSlot(childSlot, modifiedPathChild),
            _ => throw new InvalidOperationException("Cannot descend into leaf node")
        };
    }
    
    #endregion
}
