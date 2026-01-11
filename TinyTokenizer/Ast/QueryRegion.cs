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
/// Low-allocation traversal helper for region resolution.
/// Maintains an incremental slot-index stack while walking the tree and only snapshots
/// a <see cref="NodePath"/> when a match is found.
/// </summary>
internal static class RegionTraversal
{
    internal delegate bool TryGetRegionDelegate(SyntaxNode node, out int consumedCount);

    internal static IEnumerable<QueryRegion> SelectRegions(SyntaxNode root, TryGetRegionDelegate tryGetRegion)
    {
        ArgumentNullException.ThrowIfNull(tryGetRegion);

        var pathStack = new List<int>(8);
        var current = root;

        while (true)
        {
            if (current.Parent != null && tryGetRegion(current, out var consumedCount))
            {
                var parent = current.Parent;
                var startSlot = current.SiblingIndex;
                yield return new QueryRegion(
                    parentPath: CreateParentPath(pathStack),
                    parent: parent,
                    startSlot: startSlot,
                    endSlot: startSlot + consumedCount,
                    firstNode: current,
                    position: current.Position
                );
            }

            if (TryMoveToFirstChild(ref current, pathStack))
                continue;

            if (!TryMoveToNextSiblingOrAncestor(ref current, pathStack))
                break;
        }
    }

    private static NodePath CreateParentPath(List<int> pathStack)
    {
        // pathStack is the path to the CURRENT node; parent path is pathStack without the last element.
        var parentDepth = pathStack.Count - 1;
        if (parentDepth <= 0)
            return NodePath.Root;

        var builder = ImmutableArray.CreateBuilder<int>(parentDepth);
        for (int i = 0; i < parentDepth; i++)
            builder.Add(pathStack[i]);

        return new NodePath(builder.ToImmutable());
    }

    private static bool TryMoveToFirstChild(ref SyntaxNode current, List<int> pathStack)
    {
        if (current.SlotCount == 0)
            return false;

        for (int i = 0; i < current.SlotCount; i++)
        {
            var child = current.GetChild(i);
            if (child != null)
            {
                pathStack.Add(i);
                current = child;
                return true;
            }
        }

        return false;
    }

    private static bool TryMoveToNextSiblingOrAncestor(ref SyntaxNode current, List<int> pathStack)
    {
        while (pathStack.Count > 0)
        {
            var parent = current.Parent;
            if (parent == null)
                return false;

            var currentIndex = pathStack[^1];
            pathStack.RemoveAt(pathStack.Count - 1);

            for (int i = currentIndex + 1; i < parent.SlotCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling != null)
                {
                    pathStack.Add(i);
                    current = sibling;
                    return true;
                }
            }

            current = parent;
        }

        return false;
    }
}
