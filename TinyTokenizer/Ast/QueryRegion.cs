using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Represents a resolved region in the syntax tree.
/// A region is a contiguous range of slots within a parent container.
/// May be empty (StartSlot == EndSlot) representing a zero-width position.
/// </summary>
/// <remarks>
/// This is an internal type used by <see cref="SyntaxEditor"/> to translate
/// queries into edit operations. Users interact with the public Query API.
/// <para>
/// <see cref="ParentPath"/> is computed lazily to avoid tree-walking overhead
/// when regions are enumerated but not all are used for editing.
/// </para>
/// </remarks>
internal readonly struct QueryRegion
{
    private readonly NodePath? _parentPath;
    private readonly SyntaxNode _parent;
    
    /// <summary>
    /// Creates a new region with lazy path computation.
    /// </summary>
    public QueryRegion(
        SyntaxNode parent,
        int startSlot,
        int endSlot,
        SyntaxNode? firstNode,
        int position)
    {
        _parentPath = null;
        _parent = parent;
        StartSlot = startSlot;
        EndSlot = endSlot;
        FirstNode = firstNode;
        Position = position;
    }
    
    /// <summary>
    /// Creates a new region with pre-computed path.
    /// Use when path is already available to avoid recomputation.
    /// </summary>
    public QueryRegion(
        NodePath parentPath,
        SyntaxNode parent,
        int startSlot,
        int endSlot,
        SyntaxNode? firstNode,
        int position)
    {
        _parentPath = parentPath;
        _parent = parent;
        StartSlot = startSlot;
        EndSlot = endSlot;
        FirstNode = firstNode;
        Position = position;
    }
    
    /// <summary>
    /// Path to the parent container holding this region.
    /// Computed lazily on first access.
    /// </summary>
    public NodePath ParentPath => _parentPath ?? NodePath.FromNode(_parent);
    
    /// <summary>Parent container node.</summary>
    public SyntaxNode Parent => _parent;
    
    /// <summary>First slot index in the region (inclusive).</summary>
    public int StartSlot { get; }
    
    /// <summary>Slot index after the last slot in the region (exclusive).</summary>
    public int EndSlot { get; }
    
    /// <summary>Number of slots in the region.</summary>
    public int SlotCount => EndSlot - StartSlot;
    
    /// <summary>Whether this region is empty (zero-width position).</summary>
    public bool IsEmpty => StartSlot == EndSlot;
    
    /// <summary>The first node in the region, if any exist. Null for empty regions.</summary>
    public SyntaxNode? FirstNode { get; }
    
    /// <summary>Document position at the start of this region.</summary>
    public int Position { get; }
    
    /// <summary>Enumerates all nodes currently in this region.</summary>
    public IEnumerable<SyntaxNode> Nodes
    {
        get
        {
            for (int i = StartSlot; i < EndSlot; i++)
            {
                var child = Parent.GetChild(i);
                if (child != null)
                    yield return child;
            }
        }
    }
    
    /// <summary>Gets the last node in the region, or null if empty.</summary>
    public SyntaxNode? LastNode
    {
        get
        {
            if (IsEmpty) return null;
            for (int i = EndSlot - 1; i >= StartSlot; i--)
            {
                var child = Parent.GetChild(i);
                if (child != null)
                    return child;
            }
            return null;
        }
    }
    
    /// <summary>Document position at the end of this region.</summary>
    public int EndPosition => LastNode?.EndPosition ?? Position;
}

/// <summary>
/// Internal interface for queries that can resolve to regions.
/// Used by SyntaxEditor to translate queries into edit operations.
/// </summary>
internal interface IRegionQuery
{
    /// <summary>Resolves this query to regions in the tree.</summary>
    IEnumerable<QueryRegion> SelectRegions(SyntaxTree tree);
    
    /// <summary>Resolves this query to regions in a subtree.</summary>
    IEnumerable<QueryRegion> SelectRegions(SyntaxNode root);
}

/// <summary>
/// A tree walker that incrementally tracks the path during traversal.
/// O(1) per traversal step instead of O(depth) for NodePath.FromNode().
/// </summary>
internal sealed class PathTrackingWalker
{
    private readonly SyntaxNode _root;
    private readonly List<int> _pathStack;
    private SyntaxNode _current;
    
    public PathTrackingWalker(SyntaxNode root)
    {
        _root = root;
        _current = root;
        _pathStack = new List<int>(8); // Pre-allocate for typical tree depth
    }
    
    /// <summary>The current node.</summary>
    public SyntaxNode Current => _current;
    
    /// <summary>
    /// Gets the current path as a NodePath.
    /// The path leads to the CURRENT node (not its parent).
    /// </summary>
    public NodePath CurrentPath => new NodePath(ImmutableArray.CreateRange(_pathStack));
    
    /// <summary>
    /// Gets the path to the parent of the current node.
    /// Returns Root path if current is the root.
    /// </summary>
    public NodePath ParentPath
    {
        get
        {
            if (_pathStack.Count == 0)
                return NodePath.Root;
            
            // Return path without the last index (current node's sibling index)
            return new NodePath(ImmutableArray.CreateRange(_pathStack.Take(_pathStack.Count - 1)));
        }
    }
    
    /// <summary>
    /// Enumerates all descendants of the root in document order,
    /// yielding each node along with its parent path.
    /// </summary>
    public IEnumerable<(SyntaxNode Node, NodePath ParentPath)> DescendantsAndSelfWithPath()
    {
        // Yield root first
        yield return (_root, NodePath.Root);
        
        // Reset state for traversal
        _current = _root;
        _pathStack.Clear();
        
        while (MoveNext())
        {
            yield return (_current, ParentPath);
        }
    }
    
    /// <summary>
    /// Moves to the next node in document order (depth-first pre-order).
    /// Returns true if moved, false if at end.
    /// </summary>
    private bool MoveNext()
    {
        // Try first child
        if (_current.SlotCount > 0)
        {
            for (int i = 0; i < _current.SlotCount; i++)
            {
                var child = _current.GetChild(i);
                if (child != null)
                {
                    _pathStack.Add(i);
                    _current = child;
                    return true;
                }
            }
        }
        
        // Try next sibling or ancestor's next sibling
        while (_pathStack.Count > 0)
        {
            var parent = _current.Parent;
            if (parent == null)
                break;
            
            var currentIndex = _pathStack[_pathStack.Count - 1];
            _pathStack.RemoveAt(_pathStack.Count - 1);
            
            // Try next sibling
            for (int i = currentIndex + 1; i < parent.SlotCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling != null)
                {
                    _pathStack.Add(i);
                    _current = sibling;
                    return true;
                }
            }
            
            // Move up to try parent's siblings
            _current = parent;
        }
        
        return false;
    }
}
