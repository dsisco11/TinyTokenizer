namespace TinyTokenizer.Ast;

/// <summary>
/// Specifies which node types to include during traversal.
/// Similar to DOM TreeWalker's whatToShow bitmask.
/// </summary>
[Flags]
public enum NodeFilter
{
    /// <summary>Show no nodes.</summary>
    None = 0,
    
    /// <summary>Show leaf nodes (identifiers, literals, operators, symbols).</summary>
    Leaves = 1 << 0,
    
    /// <summary>Show block nodes (brace, bracket, paren blocks).</summary>
    Blocks = 1 << 1,
    
    /// <summary>Show the root list node.</summary>
    Root = 1 << 2,
    
    /// <summary>Show all nodes.</summary>
    All = Leaves | Blocks | Root
}

/// <summary>
/// Result of a custom node filter function.
/// </summary>
public enum FilterResult
{
    /// <summary>Accept the node and include it in traversal results.</summary>
    Accept,
    
    /// <summary>Skip this node but continue to its children.</summary>
    Skip,
    
    /// <summary>Reject this node and all its descendants.</summary>
    Reject
}

/// <summary>
/// Traverses an AST in a configurable manner, similar to DOM TreeWalker.
/// Provides forward and backward traversal with filtering.
/// </summary>
public sealed class TreeWalker
{
    private readonly SyntaxNode _root;
    private readonly NodeFilter _whatToShow;
    private readonly Func<SyntaxNode, FilterResult>? _filter;
    private SyntaxNode _current;
    
    /// <summary>
    /// Creates a new TreeWalker.
    /// </summary>
    /// <param name="root">The root node to traverse within.</param>
    /// <param name="whatToShow">Bitmask of node types to include.</param>
    /// <param name="filter">Optional custom filter function.</param>
    public TreeWalker(SyntaxNode root, NodeFilter whatToShow = NodeFilter.All, Func<SyntaxNode, FilterResult>? filter = null)
    {
        _root = root;
        _whatToShow = whatToShow;
        _filter = filter;
        _current = root;
    }
    
    /// <summary>The root node of this TreeWalker.</summary>
    public SyntaxNode Root => _root;
    
    /// <summary>The current node position.</summary>
    public SyntaxNode Current => _current;
    
    /// <summary>Compares nodes by their underlying green node identity since red nodes are ephemeral.</summary>
    private static bool SameNode(SyntaxNode? a, SyntaxNode? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return ReferenceEquals(a.Green, b.Green);
    }
    
    /// <summary>The filter settings.</summary>
    public NodeFilter WhatToShow => _whatToShow;
    
    #region Navigation
    
    /// <summary>
    /// Moves to the parent of the current node.
    /// </summary>
    /// <returns>The parent node, or null if at root or parent is outside root.</returns>
    public SyntaxNode? ParentNode()
    {
        var node = _current;
        while (!SameNode(node, _root) && node.Parent != null)
        {
            node = node.Parent;
            if (AcceptsNode(node))
            {
                _current = node;
                return node;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Moves to the first child of the current node.
    /// </summary>
    /// <returns>The first accepted child, or null if none.</returns>
    public SyntaxNode? FirstChild()
    {
        return TraverseChildren(forward: true);
    }
    
    /// <summary>
    /// Moves to the last child of the current node.
    /// </summary>
    /// <returns>The last accepted child, or null if none.</returns>
    public SyntaxNode? LastChild()
    {
        return TraverseChildren(forward: false);
    }
    
    /// <summary>
    /// Moves to the next sibling of the current node.
    /// </summary>
    /// <returns>The next accepted sibling, or null if none.</returns>
    public SyntaxNode? NextSibling()
    {
        return TraverseSiblings(forward: true);
    }
    
    /// <summary>
    /// Moves to the previous sibling of the current node.
    /// </summary>
    /// <returns>The previous accepted sibling, or null if none.</returns>
    public SyntaxNode? PreviousSibling()
    {
        return TraverseSiblings(forward: false);
    }
    
    /// <summary>
    /// Moves to the next node in document order (depth-first).
    /// </summary>
    /// <returns>The next accepted node, or null if at end.</returns>
    public SyntaxNode? NextNode()
    {
        var node = _current;
        
        // Try first child
        if (node.SlotCount > 0)
        {
            for (int i = 0; i < node.SlotCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                {
                    var result = TestNode(child);
                    if (result == FilterResult.Accept)
                    {
                        _current = child;
                        return child;
                    }
                    if (result == FilterResult.Skip)
                    {
                        _current = child;
                        return NextNode();
                    }
                    // Reject: continue to next child
                }
            }
        }
        
        // Try next sibling, or ancestor's next sibling
        while (!SameNode(node, _root))
        {
            var sibling = GetNextSiblingOf(node);
            if (sibling != null)
            {
                var result = TestNode(sibling);
                if (result == FilterResult.Accept)
                {
                    _current = sibling;
                    return sibling;
                }
                if (result == FilterResult.Skip)
                {
                    _current = sibling;
                    return NextNode();
                }
                node = sibling;
                continue;
            }
            
            // Move to parent
            if (node.Parent == null)
                break;
            node = node.Parent;
        }
        
        return null;
    }
    
    /// <summary>
    /// Moves to the previous node in document order (reverse depth-first).
    /// </summary>
    /// <returns>The previous accepted node, or null if at start.</returns>
    public SyntaxNode? PreviousNode()
    {
        var node = _current;
        
        // Try previous sibling's last descendant
        var prevSibling = GetPreviousSiblingOf(node);
        if (prevSibling != null)
        {
            // Go to the deepest last child
            var deepest = GetDeepestLast(prevSibling);
            var result = TestNode(deepest);
            if (result == FilterResult.Accept)
            {
                _current = deepest;
                return deepest;
            }
            if (result == FilterResult.Skip)
            {
                _current = deepest;
                return PreviousNode();
            }
        }
        
        // Try parent
        if (!SameNode(node, _root) && node.Parent != null)
        {
            var result = TestNode(node.Parent);
            if (result == FilterResult.Accept)
            {
                _current = node.Parent;
                return node.Parent;
            }
            if (result == FilterResult.Skip)
            {
                _current = node.Parent;
                return PreviousNode();
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region Enumeration
    
    /// <summary>
    /// Enumerates all descendants of the root that pass the filter.
    /// </summary>
    public IEnumerable<SyntaxNode> Descendants()
    {
        var saved = _current;
        _current = _root;
        
        while (NextNode() is { } node)
        {
            yield return node;
        }
        
        _current = saved;
    }
    
    /// <summary>
    /// Enumerates the root and all its descendants that pass the filter.
    /// </summary>
    public IEnumerable<SyntaxNode> DescendantsAndSelf()
    {
        if (AcceptsNode(_root))
            yield return _root;
        
        foreach (var node in Descendants())
            yield return node;
    }
    
    /// <summary>
    /// Enumerates ancestors from current to root that pass the filter.
    /// </summary>
    public IEnumerable<SyntaxNode> Ancestors()
    {
        var node = _current.Parent;
        while (node != null)
        {
            if (AcceptsNode(node))
                yield return node;
            if (SameNode(node, _root))
                break;
            node = node.Parent;
        }
    }
    
    /// <summary>
    /// Enumerates following siblings that pass the filter.
    /// </summary>
    public IEnumerable<SyntaxNode> FollowingSiblings()
    {
        var sibling = GetNextSiblingOf(_current);
        while (sibling != null)
        {
            if (AcceptsNode(sibling))
                yield return sibling;
            sibling = GetNextSiblingOf(sibling);
        }
    }
    
    /// <summary>
    /// Enumerates preceding siblings that pass the filter.
    /// </summary>
    public IEnumerable<SyntaxNode> PrecedingSiblings()
    {
        var sibling = GetPreviousSiblingOf(_current);
        while (sibling != null)
        {
            if (AcceptsNode(sibling))
                yield return sibling;
            sibling = GetPreviousSiblingOf(sibling);
        }
    }
    
    #endregion
    
    #region Private Helpers
    
    private SyntaxNode? TraverseChildren(bool forward)
    {
        var parent = _current;
        if (parent.SlotCount == 0)
            return null;
        
        int start = forward ? 0 : parent.SlotCount - 1;
        int end = forward ? parent.SlotCount : -1;
        int step = forward ? 1 : -1;
        
        for (int i = start; i != end; i += step)
        {
            var child = parent.GetChild(i);
            if (child == null)
                continue;
            
            var result = TestNode(child);
            if (result == FilterResult.Accept)
            {
                _current = child;
                return child;
            }
            if (result == FilterResult.Skip)
            {
                // Recurse into skipped node's children
                _current = child;
                var found = TraverseChildren(forward);
                if (found != null)
                    return found;
                _current = parent;
            }
        }
        
        return null;
    }
    
    private SyntaxNode? TraverseSiblings(bool forward)
    {
        var node = _current;
        
        while (true)
        {
            var sibling = forward ? GetNextSiblingOf(node) : GetPreviousSiblingOf(node);
            if (sibling == null)
            {
                // Move to parent and try its sibling
                if (node.Parent == null || SameNode(node.Parent, _root))
                    return null;
                node = node.Parent;
                continue;
            }
            
            var result = TestNode(sibling);
            if (result == FilterResult.Accept)
            {
                _current = sibling;
                return sibling;
            }
            if (result == FilterResult.Skip)
            {
                // Try children of skipped sibling
                _current = sibling;
                var found = TraverseChildren(forward);
                if (found != null)
                    return found;
            }
            node = sibling;
        }
    }
    
    private FilterResult TestNode(SyntaxNode node)
    {
        // First check whatToShow
        if (!MatchesWhatToShow(node))
            return FilterResult.Skip;
        
        // Then apply custom filter
        if (_filter != null)
            return _filter(node);
        
        return FilterResult.Accept;
    }
    
    private bool AcceptsNode(SyntaxNode node)
    {
        return TestNode(node) == FilterResult.Accept;
    }
    
    private bool MatchesWhatToShow(SyntaxNode node)
    {
        if (_whatToShow == NodeFilter.All)
            return true;
        
        if (node.IsLeaf && _whatToShow.HasFlag(NodeFilter.Leaves))
            return true;
        
        if (node.IsContainer && node.Kind != NodeKind.TokenList && _whatToShow.HasFlag(NodeFilter.Blocks))
            return true;
        
        if (node.Kind == NodeKind.TokenList && _whatToShow.HasFlag(NodeFilter.Root))
            return true;
        
        return false;
    }
    
    private static SyntaxNode? GetNextSiblingOf(SyntaxNode node)
    {
        if (node.Parent == null)
            return null;
        
        var parent = node.Parent;
        var siblingIndex = node.SiblingIndex;
        
        // If sibling index is valid, just get the next sibling directly
        if (siblingIndex >= 0 && siblingIndex < parent.SlotCount - 1)
        {
            return parent.GetChild(siblingIndex + 1);
        }
        
        return null;
    }
    
    private static SyntaxNode? GetPreviousSiblingOf(SyntaxNode node)
    {
        if (node.Parent == null)
            return null;
        
        var parent = node.Parent;
        var siblingIndex = node.SiblingIndex;
        
        // If sibling index is valid and not the first, get the previous sibling directly
        if (siblingIndex > 0)
        {
            return parent.GetChild(siblingIndex - 1);
        }
        
        return null;
    }
    
    private static SyntaxNode GetDeepestLast(SyntaxNode node)
    {
        while (node.SlotCount > 0)
        {
            SyntaxNode? lastChild = null;
            for (int i = node.SlotCount - 1; i >= 0; i--)
            {
                lastChild = node.GetChild(i);
                if (lastChild != null)
                    break;
            }
            if (lastChild == null)
                break;
            node = lastChild;
        }
        return node;
    }
    
    #endregion
}

/// <summary>
/// Extension methods for creating TreeWalkers.
/// </summary>
public static class TreeWalkerExtensions
{
    /// <summary>
    /// Creates a TreeWalker for this node.
    /// </summary>
    public static TreeWalker CreateTreeWalker(
        this SyntaxNode root,
        NodeFilter whatToShow = NodeFilter.All,
        Func<SyntaxNode, FilterResult>? filter = null)
    {
        return new TreeWalker(root, whatToShow, filter);
    }
    
    /// <summary>
    /// Creates a TreeWalker for this tree.
    /// </summary>
    public static TreeWalker CreateTreeWalker(
        this SyntaxTree tree,
        NodeFilter whatToShow = NodeFilter.All,
        Func<SyntaxNode, FilterResult>? filter = null)
    {
        return new TreeWalker(tree.Root, whatToShow, filter);
    }
}
