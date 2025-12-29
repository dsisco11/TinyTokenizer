using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Represents a path from root to a specific node in the tree.
/// Enables efficient re-navigation after mutations.
/// </summary>
public readonly struct NodePath : IEquatable<NodePath>
{
    private readonly ImmutableArray<int> _indices;
    
    /// <summary>
    /// Creates a path from an array of indices.
    /// </summary>
    public NodePath(ImmutableArray<int> indices)
    {
        _indices = indices.IsDefault ? ImmutableArray<int>.Empty : indices;
    }
    
    /// <summary>
    /// Creates a path from indices.
    /// </summary>
    public NodePath(params int[] indices)
    {
        _indices = [.. indices];
    }
    
    /// <summary>
    /// The indices from root to target.
    /// </summary>
    public ImmutableArray<int> Indices => _indices.IsDefault ? ImmutableArray<int>.Empty : _indices;
    
    /// <summary>
    /// The depth of this path (number of indices).
    /// </summary>
    public int Depth => _indices.IsDefault ? 0 : _indices.Length;
    
    /// <summary>
    /// Whether this is an empty (root) path.
    /// </summary>
    public bool IsRoot => Depth == 0;
    
    /// <summary>
    /// The empty/root path.
    /// </summary>
    public static NodePath Root => new(ImmutableArray<int>.Empty);
    
    /// <summary>
    /// Navigates to this path from a red root.
    /// </summary>
    /// <param name="root">The root node to start from.</param>
    /// <returns>The node at this path, or null if the path is invalid.</returns>
    public RedNode? Navigate(RedNode root)
    {
        RedNode current = root;
        foreach (var index in Indices)
        {
            var child = current.GetChild(index);
            if (child == null)
                return null;
            current = child;
        }
        return current;
    }
    
    /// <summary>
    /// Builds a path from root to the specified node.
    /// </summary>
    public static NodePath FromNode(RedNode node)
    {
        var indices = new Stack<int>();
        var current = node;
        
        while (current.Parent != null)
        {
            // Find our index in parent
            var parent = current.Parent;
            for (int i = 0; i < parent.SlotCount; i++)
            {
                if (ReferenceEquals(parent.GetChild(i), current))
                {
                    indices.Push(i);
                    break;
                }
            }
            current = parent;
        }
        
        return new NodePath(ImmutableArray.CreateRange(indices.Reverse()));
    }
    
    /// <summary>
    /// Creates a child path by appending an index.
    /// </summary>
    public NodePath Child(int index)
    {
        return new NodePath(Indices.Add(index));
    }
    
    /// <summary>
    /// Creates the parent path by removing the last index.
    /// </summary>
    public NodePath Parent()
    {
        if (IsRoot)
            return this;
        return new NodePath(Indices.RemoveAt(Indices.Length - 1));
    }
    
    /// <summary>
    /// Gets the index at the specified depth.
    /// </summary>
    public int this[int depth] => Indices[depth];
    
    /// <summary>
    /// Gets the path as a span for use with GreenTreeBuilder.
    /// </summary>
    public ReadOnlySpan<int> AsSpan() => Indices.AsSpan();
    
    /// <summary>
    /// Gets the path as an array.
    /// </summary>
    public int[] ToArray() => [.. Indices];
    
    public bool Equals(NodePath other)
    {
        if (Depth != other.Depth)
            return false;
        
        for (int i = 0; i < Depth; i++)
        {
            if (Indices[i] != other.Indices[i])
                return false;
        }
        return true;
    }
    
    public override bool Equals(object? obj) => obj is NodePath other && Equals(other);
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var index in Indices)
            hash.Add(index);
        return hash.ToHashCode();
    }
    
    public static bool operator ==(NodePath left, NodePath right) => left.Equals(right);
    public static bool operator !=(NodePath left, NodePath right) => !left.Equals(right);
    
    public override string ToString()
    {
        if (IsRoot)
            return "/";
        return "/" + string.Join("/", Indices);
    }
}
