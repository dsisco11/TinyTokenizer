using System.Collections.Immutable;
using CommunityToolkit.HighPerformance.Buffers;

namespace TinyTokenizer.Ast;

/// <summary>
/// Fluent editor for making batched mutations to a SyntaxTree.
/// Mutations are queued and applied atomically when Commit() is called.
/// Supports undo via the underlying SyntaxTree's history.
/// </summary>
/// <example>
/// <code>
/// var tree = SyntaxTree.Parse("function foo() { return 1; }");
/// 
/// tree.CreateEditor()
///     .InsertAfter(Query.BraceBlock.First().Start(), "console.log('enter');")
///     .Replace(Query.AnyNumeric.First(), "42")
///     .Remove(Query.AnyIdent.WithText("unused"))
///     .Commit();
/// </code>
/// </example>
public sealed class SyntaxEditor
{
    private readonly SyntaxTree _tree;
    private readonly TokenizerOptions _options;
    private readonly List<PendingEdit> _edits = new();
    private int _sequenceNumber = 0;
    
    /// <summary>
    /// Creates a new editor for the specified tree.
    /// </summary>
    internal SyntaxEditor(SyntaxTree tree, TokenizerOptions? options = null)
    {
        _tree = tree;
        _options = options ?? TokenizerOptions.Default;
    }
    
    /// <summary>
    /// Gets the number of pending edits.
    /// </summary>
    public int PendingEditCount => _edits.Count;
    
    /// <summary>
    /// Gets whether there are pending edits.
    /// </summary>
    public bool HasPendingEdits => _edits.Count > 0;
    
    #region Region Resolution
    
    /// <summary>
    /// Gets regions from a query, using IRegionQuery if available,
    /// otherwise falling back to match-based resolution.
    /// </summary>
    private IEnumerable<QueryRegion> GetRegions(INodeQuery query)
    {
        if (query is IRegionQuery regionQuery)
        {
            return regionQuery.SelectRegions(_tree);
        }
        
        // Fallback for queries that don't implement IRegionQuery
        return GetRegionsFromMatches(query);
    }
    
    private IEnumerable<QueryRegion> GetRegionsFromMatches(INodeQuery query)
    {
        // Use Select + TryMatch to get matches without SelectMatches
        foreach (var node in query.Select(_tree))
        {
            if (query.TryMatch(node, out var consumedCount))
            {
                var parent = node.Parent;
                if (parent != null)
                {
                    yield return new QueryRegion(
                        parent: parent,
                        startSlot: node.SiblingIndex,
                        endSlot: node.SiblingIndex + consumedCount,
                        firstNode: node,
                        position: node.Position
                    );
                }
            }
        }
    }
    
    #endregion
    
    #region Insert (Query-based)
    
    /// <summary>
    /// Queues an insertion of text before all nodes matching the query.
    /// For range queries (e.g., Query.Between), inserts before the start of the matched range.
    /// For empty regions (e.g., empty blocks via Inner()), inserts at the region position.
    /// </summary>
    /// <param name="query">A query specifying which nodes to insert before.</param>
    /// <param name="text">The text to insert (will be parsed into nodes).</param>
    /// <example>
    /// <code>
    /// // Insert before the closing brace (at end of block content)
    /// editor.InsertBefore(Query.BraceBlock.First().End(), "// last line")
    /// </code>
    /// </example>
    public SyntaxEditor InsertBefore(INodeQuery query, string text)
    {
        foreach (var region in GetRegions(query))
        {
            _edits.Add(new InsertAtSlotEdit(region.ParentPath, region.StartSlot, region.Position, text) 
            { 
                SequenceNumber = _sequenceNumber++ 
            });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text after all nodes matching the query.
    /// For range queries (e.g., Query.Between), inserts after the end of the matched range.
    /// For empty regions (e.g., empty blocks via Inner()), inserts at the region position.
    /// </summary>
    /// <param name="query">A query specifying which nodes to insert after.</param>
    /// <param name="text">The text to insert (will be parsed into nodes).</param>
    /// <example>
    /// <code>
    /// // Insert after the opening brace (at start of block content)
    /// editor.InsertAfter(Query.BraceBlock.First().Start(), "// first line")
    /// </code>
    /// </example>
    public SyntaxEditor InsertAfter(INodeQuery query, string text)
    {
        foreach (var region in GetRegions(query))
        {
            _edits.Add(new InsertAtSlotEdit(region.ParentPath, region.EndSlot, region.EndPosition, text) 
            { 
                SequenceNumber = _sequenceNumber++ 
            });
        }
        return this;
    }
    
    #endregion
    
    #region Insert (RedNode-based)
    
    /// <summary>
    /// Queues an insertion of text before the specified node.
    /// </summary>
    /// <param name="target">The node to insert before.</param>
    /// <param name="text">The text to insert (will be parsed into nodes).</param>
    /// <exception cref="ArgumentException">Thrown if the target node has no parent.</exception>
    public SyntaxEditor InsertBefore(SyntaxNode target, string text)
    {
        var pos = CreateInsertionPosition(target, before: true);
        _edits.Add(new InsertEdit(pos, text) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text before each of the specified nodes.
    /// </summary>
    public SyntaxEditor InsertBefore(IEnumerable<SyntaxNode> targets, string text)
    {
        foreach (var target in targets)
        {
            InsertBefore(target, text);
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text after the specified node.
    /// </summary>
    /// <param name="target">The node to insert after.</param>
    /// <param name="text">The text to insert (will be parsed into nodes).</param>
    /// <exception cref="ArgumentException">Thrown if the target node has no parent.</exception>
    public SyntaxEditor InsertAfter(SyntaxNode target, string text)
    {
        var pos = CreateInsertionPosition(target, before: false);
        _edits.Add(new InsertEdit(pos, text) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text after each of the specified nodes.
    /// </summary>
    public SyntaxEditor InsertAfter(IEnumerable<SyntaxNode> targets, string text)
    {
        foreach (var target in targets)
        {
            InsertAfter(target, text);
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of a single node before the specified target.
    /// </summary>
    public SyntaxEditor InsertBefore(SyntaxNode target, SyntaxNode nodeToInsert)
    {
        return InsertBefore(target, [nodeToInsert.Green]);
    }
    
    /// <summary>
    /// Queues an insertion of a single node before the specified target.
    /// </summary>
    internal SyntaxEditor InsertBefore(SyntaxNode target, GreenNode nodeToInsert)
    {
        return InsertBefore(target, [nodeToInsert]);
    }
    
    /// <summary>
    /// Queues an insertion of nodes before the specified target.
    /// </summary>
    public SyntaxEditor InsertBefore(SyntaxNode target, IEnumerable<SyntaxNode> nodesToInsert)
    {
        return InsertBefore(target, ToGreenNodes(nodesToInsert));
    }
    
    /// <summary>
    /// Queues an insertion of nodes before the specified target.
    /// </summary>
    internal SyntaxEditor InsertBefore(SyntaxNode target, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        var pos = CreateInsertionPosition(target, before: true);
        _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes before each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    public SyntaxEditor InsertBefore(IEnumerable<SyntaxNode> targets, IEnumerable<SyntaxNode> nodesToInsert)
    {
        var nodes = ToGreenNodes(nodesToInsert);
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, before: true);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes before each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    internal SyntaxEditor InsertBefore(IEnumerable<SyntaxNode> targets, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, before: true);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of a single node after the specified target.
    /// </summary>
    public SyntaxEditor InsertAfter(SyntaxNode target, SyntaxNode nodeToInsert)
    {
        return InsertAfter(target, [nodeToInsert.Green]);
    }
    
    /// <summary>
    /// Queues an insertion of a single node after the specified target.
    /// </summary>
    internal SyntaxEditor InsertAfter(SyntaxNode target, GreenNode nodeToInsert)
    {
        return InsertAfter(target, [nodeToInsert]);
    }
    
    /// <summary>
    /// Queues an insertion of nodes after the specified target.
    /// </summary>
    public SyntaxEditor InsertAfter(SyntaxNode target, IEnumerable<SyntaxNode> nodesToInsert)
    {
        return InsertAfter(target, ToGreenNodes(nodesToInsert));
    }
    
    /// <summary>
    /// Queues an insertion of nodes after the specified target.
    /// </summary>
    internal SyntaxEditor InsertAfter(SyntaxNode target, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        var pos = CreateInsertionPosition(target, before: false);
        _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes after each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    public SyntaxEditor InsertAfter(IEnumerable<SyntaxNode> targets, IEnumerable<SyntaxNode> nodesToInsert)
    {
        var nodes = ToGreenNodes(nodesToInsert);
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, before: false);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes after each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    internal SyntaxEditor InsertAfter(IEnumerable<SyntaxNode> targets, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, before: false);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    #endregion
    
    #region Remove (Query-based)
    
    /// <summary>
    /// Queues removal of all nodes matching the query.
    /// For range queries (e.g., Query.Between), removes all nodes in the matched range.
    /// </summary>
    public SyntaxEditor Remove(INodeQuery query)
    {
        foreach (var region in GetRegions(query))
        {
            if (!region.IsEmpty)
            {
                var path = region.FirstNode != null 
                    ? NodePath.FromNode(region.FirstNode) 
                    : region.ParentPath.Child(region.StartSlot);
                _edits.Add(new RemoveEdit(path, region.Position, region.SlotCount) { SequenceNumber = _sequenceNumber++ });
            }
            // Empty regions: nothing to remove, silently skip
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues removal of all nodes matching any of the queries.
    /// </summary>
    public SyntaxEditor Remove(IEnumerable<INodeQuery> queries)
    {
        foreach (var query in queries)
        {
            Remove(query);
        }
        return this;
    }
    
    #endregion
    
    #region Remove (RedNode-based)
    
    /// <summary>
    /// Queues removal of the specified node.
    /// </summary>
    public SyntaxEditor Remove(SyntaxNode node)
    {
        var path = NodePath.FromNode(node);
        _edits.Add(new RemoveEdit(path, node.Position) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues removal of all specified nodes.
    /// </summary>
    public SyntaxEditor Remove(IEnumerable<SyntaxNode> nodes)
    {
        foreach (var node in nodes)
        {
            Remove(node);
        }
        return this;
    }
    
    #endregion
    
    #region Replace (Query-based)
    
    /// <summary>
    /// Queues replacement of all nodes matching the query with new text.
    /// For range queries (e.g., Query.Between), replaces all nodes in the matched range.
    /// </summary>
    public SyntaxEditor Replace(INodeQuery query, string text)
    {
        foreach (var region in GetRegions(query))
        {
            var (leading, trailing) = GetTriviaForRegion(region);
            _edits.Add(new ReplaceRegionEdit(region, text, leading, trailing) 
            { 
                SequenceNumber = _sequenceNumber++ 
            });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all nodes matching any of the queries with new text.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<INodeQuery> queries, string text)
    {
        foreach (var query in queries)
        {
            Replace(query, text);
        }
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all nodes matching the query using a transformer function.
    /// The transformer receives the full RedNode, allowing access to content including trivia.
    /// Use <see cref="Edit(INodeQuery, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(INodeQuery query, Func<SyntaxNode, string> replacer)
    {
        foreach (var region in GetRegions(query))
        {
            var node = region.FirstNode;
            if (node == null) continue;
            
            var text = replacer(node);
            var (leading, trailing) = GetTriviaForRegion(region);
            _edits.Add(new ReplaceRegionEdit(region, text, leading, trailing) 
            { 
                SequenceNumber = _sequenceNumber++ 
            });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all nodes matching any of the queries using a transformer function.
    /// The transformer receives the full RedNode, allowing access to content including trivia.
    /// Use <see cref="Edit(IEnumerable{INodeQuery}, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<INodeQuery> queries, Func<SyntaxNode, string> replacer)
    {
        foreach (var query in queries)
        {
            Replace(query, replacer);
        }
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all nodes matching the query with pre-built nodes.
    /// </summary>
    internal SyntaxEditor Replace(INodeQuery query, ImmutableArray<GreenNode> nodes)
    {
        foreach (var region in GetRegions(query))
        {
            var (leading, trailing) = GetTriviaForRegion(region);
            _edits.Add(new ReplaceNodesRegionEdit(region, nodes, leading, trailing) 
            { 
                SequenceNumber = _sequenceNumber++ 
            });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all nodes matching any of the queries with pre-built nodes.
    /// </summary>
    internal SyntaxEditor Replace(IEnumerable<INodeQuery> queries, ImmutableArray<GreenNode> nodes)
    {
        foreach (var query in queries)
        {
            Replace(query, nodes);
        }
        return this;
    }
    
    #endregion
    
    #region Replace (RedNode-based)
    
    /// <summary>
    /// Queues replacement of the specified node with new text.
    /// </summary>
    public SyntaxEditor Replace(SyntaxNode node, string text)
    {
        var path = NodePath.FromNode(node);
        var (leading, trailing) = GetTrivia(node);
        _edits.Add(new ReplaceEdit(path, text, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all specified nodes with new text.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<SyntaxNode> nodes, string text)
    {
        foreach (var node in nodes)
        {
            Replace(node, text);
        }
        return this;
    }
    
    /// <summary>
    /// Queues replacement of the specified node using a transformer function.
    /// The transformer receives the full RedNode, allowing access to content including trivia.
    /// Use <see cref="Edit(SyntaxNode, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(SyntaxNode node, Func<SyntaxNode, string> replacer)
    {
        var text = replacer(node);
        var path = NodePath.FromNode(node);
        var (leading, trailing) = GetTrivia(node);
        _edits.Add(new ReplaceEdit(path, text, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all specified nodes using a transformer function.
    /// The transformer receives the full RedNode, allowing access to content including trivia.
    /// Use <see cref="Edit(IEnumerable{SyntaxNode}, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<SyntaxNode> nodes, Func<SyntaxNode, string> replacer)
    {
        foreach (var node in nodes)
        {
            Replace(node, replacer);
        }
        return this;
    }
    
    /// <summary>
    /// Queues replacement of the specified node with a single node.
    /// </summary>
    public SyntaxEditor Replace(SyntaxNode node, SyntaxNode replacement)
    {
        return Replace(node, [replacement.Green]);
    }
    
    /// <summary>
    /// Queues replacement of the specified node with a single node.
    /// </summary>
    internal SyntaxEditor Replace(SyntaxNode node, GreenNode replacement)
    {
        return Replace(node, [replacement]);
    }
    
    /// <summary>
    /// Queues replacement of the specified node with multiple nodes.
    /// </summary>
    public SyntaxEditor Replace(SyntaxNode node, IEnumerable<SyntaxNode> replacements)
    {
        return Replace(node, ToGreenNodes(replacements));
    }
    
    /// <summary>
    /// Queues replacement of the specified node with multiple nodes.
    /// </summary>
    internal SyntaxEditor Replace(SyntaxNode node, IEnumerable<GreenNode> replacements)
    {
        var greenNodes = replacements.ToImmutableArray();
        var path = NodePath.FromNode(node);
        var (leading, trailing) = GetTrivia(node);
        _edits.Add(new ReplaceNodesEdit(path, greenNodes, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    #endregion
    
    #region Edit (Query-based)
    
    /// <summary>
    /// Queues a transformation edit of all nodes matching the query.
    /// The transformer receives the node's content WITHOUT trivia, and trivia is automatically preserved.
    /// For range queries, receives the concatenated content of all matched nodes.
    /// </summary>
    /// <param name="query">Query to select nodes to edit.</param>
    /// <param name="transformer">Function that transforms the node's content (without trivia).</param>
    public SyntaxEditor Edit(INodeQuery query, Func<string, string> transformer)
    {
        foreach (var region in GetRegions(query))
        {
            if (region.IsEmpty) continue;
            
            var firstNode = region.FirstNode;
            if (firstNode == null) continue;
            
            var path = NodePath.FromNode(firstNode);
            var (leading, trailing) = GetTriviaForRegion(region);
            var contentWithoutTrivia = GetContentWithoutTriviaForRegion(region);
            var newText = transformer(contentWithoutTrivia);
            _edits.Add(new ReplaceEdit(path, newText, region.Position, leading, trailing, region.SlotCount) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues a transformation edit of all nodes matching any of the queries.
    /// The transformer receives the node's content WITHOUT trivia, and trivia is automatically preserved.
    /// </summary>
    public SyntaxEditor Edit(IEnumerable<INodeQuery> queries, Func<string, string> transformer)
    {
        foreach (var query in queries)
        {
            Edit(query, transformer);
        }
        return this;
    }
    
    #endregion
    
    #region Edit (RedNode-based)
    
    /// <summary>
    /// Queues a transformation edit of the specified node.
    /// The transformer receives the node's content WITHOUT trivia, and trivia is automatically preserved.
    /// </summary>
    /// <param name="node">The node to edit.</param>
    /// <param name="transformer">Function that transforms the node's content (without trivia).</param>
    public SyntaxEditor Edit(SyntaxNode node, Func<string, string> transformer)
    {
        var path = NodePath.FromNode(node);
        var (leading, trailing) = GetTrivia(node);
        var contentWithoutTrivia = GetContentWithoutTrivia(node);
        var newText = transformer(contentWithoutTrivia);
        _edits.Add(new ReplaceEdit(path, newText, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues a transformation edit of all specified nodes.
    /// The transformer receives each node's content WITHOUT trivia, and trivia is automatically preserved.
    /// </summary>
    public SyntaxEditor Edit(IEnumerable<SyntaxNode> nodes, Func<string, string> transformer)
    {
        foreach (var node in nodes)
        {
            Edit(node, transformer);
        }
        return this;
    }
    
    #endregion
    
    #region Private Helpers
    
    /// <summary>
    /// Gets the content of a node without its leading and trailing trivia.
    /// For leaves, returns the token text. For blocks, returns the full content minus outer trivia.
    /// For syntax nodes (containers), returns full content minus the trivia from first/last children.
    /// Uses precomputed trivia widths from green nodes for efficiency.
    /// </summary>
    private static string GetContentWithoutTrivia(SyntaxNode node)
    {
        if (node is SyntaxToken leaf)
        {
            return leaf.Text;
        }
        
        if (node is SyntaxBlock block)
        {
            // For blocks, we want the full content minus leading/trailing trivia
            var fullText = node.ToText();
            var leadingWidth = block.LeadingTriviaWidth;
            var trailingWidth = block.TrailingTriviaWidth;
            
            if (leadingWidth == 0 && trailingWidth == 0)
                return fullText;
            
            var contentLength = fullText.Length - leadingWidth - trailingWidth;
            if (contentLength <= 0)
                return string.Empty;
            
            return fullText.AsSpan(leadingWidth, contentLength).ToString();
        }
        
        // For syntax nodes and other containers, use precomputed trivia widths
        var green = node.Green;
        var leadingTriviaWidth = green.GetLeadingTriviaWidth();
        var trailingTriviaWidth = green.GetTrailingTriviaWidth();
        
        if (leadingTriviaWidth == 0 && trailingTriviaWidth == 0)
        {
            return node.ToText();
        }
        
        var text = node.ToText();
        var contentLen = text.Length - leadingTriviaWidth - trailingTriviaWidth;
        if (contentLen <= 0)
            return string.Empty;
        
        return text.AsSpan(leadingTriviaWidth, contentLen).ToString();
    }
    
    /// <summary>
    /// Gets the concatenated content of a range of siblings without leading/trailing trivia.
    /// Leading trivia from first node and trailing trivia from last node are excluded.
    /// Uses precomputed trivia widths from green nodes for efficiency.
    /// </summary>
    private static string GetContentWithoutTriviaForRange(SyntaxNode startNode, int count)
    {
        if (count <= 0)
            return string.Empty;
        
        if (count == 1)
            return GetContentWithoutTrivia(startNode);
        
        // For multiple nodes, concatenate their content using pooled buffer
        using var buffer = new ArrayPoolBufferWriter<char>();
        var current = startNode;
        
        for (int i = 0; i < count && current != null; i++)
        {
            var content = current.ToText();
            
            if (i == 0)
            {
                // First node: exclude leading trivia only (use precomputed width)
                var leadingWidth = current.Green.GetLeadingTriviaWidth();
                var span = content.AsSpan(leadingWidth);
                var dest = buffer.GetSpan(span.Length);
                span.CopyTo(dest);
                buffer.Advance(span.Length);
            }
            else if (i == count - 1)
            {
                // Last node: exclude trailing trivia only (use precomputed width)
                var trailingWidth = current.Green.GetTrailingTriviaWidth();
                var span = content.AsSpan(0, content.Length - trailingWidth);
                var dest = buffer.GetSpan(span.Length);
                span.CopyTo(dest);
                buffer.Advance(span.Length);
            }
            else
            {
                // Middle nodes: include everything
                var span = content.AsSpan();
                var dest = buffer.GetSpan(span.Length);
                span.CopyTo(dest);
                buffer.Advance(span.Length);
            }
            
            current = current.NextSibling();
        }
        
        return buffer.WrittenSpan.ToString();
    }
    
    /// <summary>
    /// Extracts leading and trailing trivia from a node.
    /// Uses green node's GetFirstLeaf/GetLastLeaf for O(depth) access instead of recursive red node traversal.
    /// </summary>
    private static (ImmutableArray<GreenTrivia> Leading, ImmutableArray<GreenTrivia> Trailing) GetTrivia(SyntaxNode node)
    {
        // Delegate to green node which efficiently finds first/last leaf
        var green = node.Green;
        return (green.GetLeadingTrivia(), green.GetTrailingTrivia());
    }
    
    /// <summary>
    /// Gets trivia for a query region.
    /// For empty regions, returns empty trivia.
    /// For non-empty regions, returns leading from first and trailing from last.
    /// </summary>
    private static (ImmutableArray<GreenTrivia> Leading, ImmutableArray<GreenTrivia> Trailing) GetTriviaForRegion(QueryRegion region)
    {
        if (region.IsEmpty)
            return (ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty);
        
        var first = region.FirstNode;
        var last = region.LastNode;
        
        var leading = first != null ? GetTrivia(first).Leading : ImmutableArray<GreenTrivia>.Empty;
        var trailing = last != null ? GetTrivia(last).Trailing : ImmutableArray<GreenTrivia>.Empty;
        
        return (leading, trailing);
    }
    
    /// <summary>
    /// Gets the content of a region without leading/trailing trivia.
    /// </summary>
    private static string GetContentWithoutTriviaForRegion(QueryRegion region)
    {
        if (region.IsEmpty)
            return string.Empty;
        
        var first = region.FirstNode;
        if (first == null)
            return string.Empty;
        
        return GetContentWithoutTriviaForRange(first, region.SlotCount);
    }
    
    /// <summary>
    /// Creates an InsertionPosition for inserting before or after a target node.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the target node has no parent.</exception>
    private static InsertionPosition CreateInsertionPosition(SyntaxNode target, bool before)
    {
        var parent = target.Parent;
        if (parent == null)
            throw new ArgumentException("Cannot insert relative to root node", nameof(target));
        
        var childIndex = target.SiblingIndex;
        if (childIndex < 0)
            throw new ArgumentException("Could not determine sibling index of target node", nameof(target));
        
        var parentPath = NodePath.FromNode(parent);
        var targetPath = NodePath.FromNode(target);
        var (targetLeading, targetTrailing) = GetTrivia(target);
        
        return before
            ? new InsertionPosition(parentPath, childIndex, target.Position, targetPath, targetLeading, targetTrailing)
            : new InsertionPosition(parentPath, childIndex + 1, target.EndPosition, targetPath, targetLeading, targetTrailing);
    }
    
    /// <summary>
    /// Converts an enumerable of RedNodes to an ImmutableArray of their underlying GreenNodes.
    /// </summary>
    private static ImmutableArray<GreenNode> ToGreenNodes(IEnumerable<SyntaxNode> redNodes)
    {
        return redNodes.Select(n => n.Green).ToImmutableArray();
    }
    
    #endregion
    
    #region Commit / Rollback
    
    /// <summary>
    /// Applies all pending edits atomically.
    /// The tree's undo stack is updated, allowing Undo() to revert.
    /// Automatically performs incremental syntax rebinding at the lowest common ancestor
    /// of all affected paths if the tree has a schema with syntax definitions.
    /// </summary>
    public void Commit()
    {
        if (_edits.Count == 0)
            return;
        
        // Collect affected paths before sorting (for incremental rebinding)
        var affectedPaths = _edits
            .Select(e => e.AffectedPath)
            .Where(p => !p.IsRoot)
            .ToList();
        
        // Sort edits by document position descending (process from end to start).
        // For same position, use sequence number descending (later queued edits first for same position).
        // This ensures that edits at later positions are applied first, so their
        // paths remain valid as earlier edits don't shift their positions.
        var sortedEdits = _edits
            .OrderByDescending(e => e.Position)
            .ThenByDescending(e => e.SequenceNumber)
            .ToList();
        
        _tree.Edit(_ =>
        {
            var root = _tree.GreenRoot;
            
            foreach (var edit in sortedEdits)
            {
                // Create a fresh builder for each edit using the current root state
                var builder = new GreenTreeBuilder(root);
                root = edit.Apply(root, builder, _options);
            }
            
            return root;
        });
        
        _edits.Clear();
        _sequenceNumber = 0;
        
        // Perform incremental rebinding at the lowest common ancestor of all affected paths
        if (affectedPaths.Count > 0)
        {
            // Find LCA of all affected paths
            var lca = affectedPaths[0];
            for (int i = 1; i < affectedPaths.Count; i++)
            {
                lca = lca.CommonAncestor(affectedPaths[i]);
                if (lca.IsRoot)
                    break; // Already at root, no point continuing
            }
            
            _tree.RebindAt(lca);
        }
        else
        {
            // All edits affect root level - do full rebind
            _tree.Rebind();
        }
    }
    
    /// <summary>
    /// Discards all pending edits without applying them.
    /// </summary>
    public void Rollback()
    {
        _edits.Clear();
    }
    
    #endregion
}

#region Pending Edit Types

internal abstract class PendingEdit
{
    /// <summary>
    /// The document position of this edit, used for sorting edits in reverse order.
    /// </summary>
    public abstract int Position { get; }
    
    /// <summary>
    /// The path to the parent node affected by this edit.
    /// Used for incremental rebinding.
    /// </summary>
    public abstract NodePath AffectedPath { get; }
    
    /// <summary>
    /// Sequence number for stable sorting of same-position edits.
    /// </summary>
    public int SequenceNumber { get; init; }
    
    public abstract GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options);
}

internal sealed class InsertEdit : PendingEdit
{
    private readonly InsertionPosition _insertPos;
    private readonly string _text;
    
    public InsertEdit(InsertionPosition insertPos, string text)
    {
        _insertPos = insertPos;
        _text = text;
    }
    
    public override int Position => _insertPos.Position;
    
    public override NodePath AffectedPath => _insertPos.ParentPath;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var lexer = new GreenLexer(options);
        var nodes = lexer.ParseToGreenNodes(_text);
        
        // Roslyn-style insertion: insert content before the target node's leading trivia.
        // Both inserted content and target keep their own trivia - no transfer occurs.
        // This means "insert X before Y" places X before Y's leading whitespace/comments.
        return builder.InsertAt(_insertPos.ParentPath.ToArray(), _insertPos.ChildIndex, nodes);
    }
}

internal sealed class InsertNodesEdit : PendingEdit
{
    private readonly InsertionPosition _insertPos;
    private readonly ImmutableArray<GreenNode> _nodes;
    
    public InsertNodesEdit(InsertionPosition insertPos, ImmutableArray<GreenNode> nodes)
    {
        _insertPos = insertPos;
        _nodes = nodes;
    }
    
    public override int Position => _insertPos.Position;
    
    public override NodePath AffectedPath => _insertPos.ParentPath;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        return builder.InsertAt(_insertPos.ParentPath.ToArray(), _insertPos.ChildIndex, _nodes);
    }
}

internal sealed class RemoveEdit : PendingEdit
{
    private readonly NodePath _path;
    private readonly int _position;
    private readonly int _count;
    
    public RemoveEdit(NodePath path, int position, int count = 1)
    {
        _path = path;
        _position = position;
        _count = count;
    }
    
    public override int Position => _position;
    
    public override NodePath AffectedPath => _path.Parent();
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var parentPath = _path.Parent();
        var childIndex = _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
        return builder.RemoveAt(parentPath.ToArray(), childIndex, _count);
    }
}

internal sealed class ReplaceEdit : PendingEdit
{
    private readonly NodePath _path;
    private readonly string _text;
    private readonly int _position;
    private readonly int _count;
    private readonly ImmutableArray<GreenTrivia> _leadingTrivia;
    private readonly ImmutableArray<GreenTrivia> _trailingTrivia;
    
    public ReplaceEdit(NodePath path, string text, int position, 
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default,
        int count = 1)
    {
        _path = path;
        _text = text;
        _position = position;
        _count = count;
        _leadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        _trailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
    }
    
    public override int Position => _position;
    
    public override NodePath AffectedPath => _path.Parent();
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var parentPath = _path.Parent();
        var childIndex = _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
        
        var lexer = new GreenLexer(options);
        var nodes = lexer.ParseToGreenNodes(_text);
        
        // Transfer trivia from original node to replacement nodes
        if (nodes.Length > 0)
        {
            nodes = TransferTrivia(nodes, _leadingTrivia, _trailingTrivia);
        }
        
        return builder.ReplaceAt(parentPath.ToArray(), childIndex, _count, nodes);
    }
    
    /// <summary>
    /// Transfers leading trivia to first node and trailing trivia to last node.
    /// </summary>
    private static ImmutableArray<GreenNode> TransferTrivia(
        ImmutableArray<GreenNode> nodes,
        ImmutableArray<GreenTrivia> leading,
        ImmutableArray<GreenTrivia> trailing)
    {
        if (leading.IsEmpty && trailing.IsEmpty)
            return nodes;
        
        var result = nodes.ToBuilder();
        
        // Add leading trivia to first node
        if (!leading.IsEmpty && result[0] is GreenLeaf firstLeaf)
        {
            var newLeading = firstLeaf.LeadingTrivia.IsEmpty 
                ? leading 
                : leading.AddRange(firstLeaf.LeadingTrivia);
            result[0] = firstLeaf.WithLeadingTrivia(newLeading);
        }
        
        // Add trailing trivia to last node
        if (!trailing.IsEmpty && result[^1] is GreenLeaf lastLeaf)
        {
            var newTrailing = lastLeaf.TrailingTrivia.IsEmpty 
                ? trailing 
                : lastLeaf.TrailingTrivia.AddRange(trailing);
            result[^1] = lastLeaf.WithTrailingTrivia(newTrailing);
        }
        
        return result.ToImmutable();
    }
}

internal sealed class ReplaceNodesEdit : PendingEdit
{
    private readonly NodePath _path;
    private readonly ImmutableArray<GreenNode> _nodes;
    private readonly int _position;
    private readonly int _count;
    private readonly ImmutableArray<GreenTrivia> _leadingTrivia;
    private readonly ImmutableArray<GreenTrivia> _trailingTrivia;
    
    public ReplaceNodesEdit(NodePath path, ImmutableArray<GreenNode> nodes, int position,
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default,
        int count = 1)
    {
        _path = path;
        _nodes = nodes;
        _position = position;
        _count = count;
        _leadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        _trailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
    }
    
    public override int Position => _position;
    
    public override NodePath AffectedPath => _path.Parent();
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var parentPath = _path.Parent();
        var childIndex = _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
        
        var nodes = _nodes;
        
        // Transfer trivia from original node to replacement nodes
        if (nodes.Length > 0 && (!_leadingTrivia.IsEmpty || !_trailingTrivia.IsEmpty))
        {
            nodes = TransferTrivia(nodes, _leadingTrivia, _trailingTrivia);
        }
        
        return builder.ReplaceAt(parentPath.ToArray(), childIndex, _count, nodes);
    }
    
    /// <summary>
    /// Transfers leading trivia to first node and trailing trivia to last node.
    /// </summary>
    private static ImmutableArray<GreenNode> TransferTrivia(
        ImmutableArray<GreenNode> nodes,
        ImmutableArray<GreenTrivia> leading,
        ImmutableArray<GreenTrivia> trailing)
    {
        if (leading.IsEmpty && trailing.IsEmpty)
            return nodes;
        
        var result = nodes.ToBuilder();
        
        // Add leading trivia to first node
        if (!leading.IsEmpty && result[0] is GreenLeaf firstLeaf)
        {
            var newLeading = firstLeaf.LeadingTrivia.IsEmpty 
                ? leading 
                : leading.AddRange(firstLeaf.LeadingTrivia);
            result[0] = firstLeaf.WithLeadingTrivia(newLeading);
        }
        
        // Add trailing trivia to last node
        if (!trailing.IsEmpty && result[^1] is GreenLeaf lastLeaf)
        {
            var newTrailing = lastLeaf.TrailingTrivia.IsEmpty 
                ? trailing 
                : lastLeaf.TrailingTrivia.AddRange(trailing);
            result[^1] = lastLeaf.WithTrailingTrivia(newTrailing);
        }
        
        return result.ToImmutable();
    }
}

internal sealed class InsertAtSlotEdit : PendingEdit
{
    private readonly NodePath _parentPath;
    private readonly int _slot;
    private readonly int _position;
    private readonly string _text;
    
    public InsertAtSlotEdit(NodePath parentPath, int slot, int position, string text)
    {
        _parentPath = parentPath;
        _slot = slot;
        _position = position;
        _text = text;
    }
    
    public override int Position => _position;
    
    public override NodePath AffectedPath => _parentPath;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var lexer = new GreenLexer(options);
        var newNodes = lexer.ParseToGreenNodes(_text);
        
        return builder.InsertAt(_parentPath.ToArray(), _slot, newNodes);
    }
}

internal sealed class ReplaceRegionEdit : PendingEdit
{
    private readonly QueryRegion _region;
    private readonly string _text;
    private readonly ImmutableArray<GreenTrivia> _leadingTrivia;
    private readonly ImmutableArray<GreenTrivia> _trailingTrivia;
    
    public ReplaceRegionEdit(QueryRegion region, string text,
        ImmutableArray<GreenTrivia> leadingTrivia,
        ImmutableArray<GreenTrivia> trailingTrivia)
    {
        _region = region;
        _text = text;
        _leadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        _trailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
    }
    
    public override int Position => _region.Position;
    
    public override NodePath AffectedPath => _region.ParentPath;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var lexer = new GreenLexer(options);
        var newNodes = lexer.ParseToGreenNodes(_text);
        
        if (newNodes.Length > 0)
        {
            newNodes = TransferTrivia(newNodes, _leadingTrivia, _trailingTrivia);
        }
        
        return builder.ReplaceAt(
            _region.ParentPath.ToArray(),
            _region.StartSlot,
            _region.SlotCount,
            newNodes
        );
    }
    
    private static ImmutableArray<GreenNode> TransferTrivia(
        ImmutableArray<GreenNode> nodes,
        ImmutableArray<GreenTrivia> leading,
        ImmutableArray<GreenTrivia> trailing)
    {
        if (leading.IsEmpty && trailing.IsEmpty)
            return nodes;
        
        var result = nodes.ToBuilder();
        
        if (!leading.IsEmpty && result[0] is GreenLeaf firstLeaf)
        {
            var newLeading = firstLeaf.LeadingTrivia.IsEmpty 
                ? leading 
                : leading.AddRange(firstLeaf.LeadingTrivia);
            result[0] = firstLeaf.WithLeadingTrivia(newLeading);
        }
        
        if (!trailing.IsEmpty && result[^1] is GreenLeaf lastLeaf)
        {
            var newTrailing = lastLeaf.TrailingTrivia.IsEmpty 
                ? trailing 
                : lastLeaf.TrailingTrivia.AddRange(trailing);
            result[^1] = lastLeaf.WithTrailingTrivia(newTrailing);
        }
        
        return result.ToImmutable();
    }
}

internal sealed class ReplaceNodesRegionEdit : PendingEdit
{
    private readonly QueryRegion _region;
    private readonly ImmutableArray<GreenNode> _nodes;
    private readonly ImmutableArray<GreenTrivia> _leadingTrivia;
    private readonly ImmutableArray<GreenTrivia> _trailingTrivia;
    
    public ReplaceNodesRegionEdit(QueryRegion region, ImmutableArray<GreenNode> nodes,
        ImmutableArray<GreenTrivia> leadingTrivia,
        ImmutableArray<GreenTrivia> trailingTrivia)
    {
        _region = region;
        _nodes = nodes;
        _leadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        _trailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
    }
    
    public override int Position => _region.Position;
    
    public override NodePath AffectedPath => _region.ParentPath;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var nodes = _nodes;
        
        if (nodes.Length > 0 && (!_leadingTrivia.IsEmpty || !_trailingTrivia.IsEmpty))
        {
            nodes = TransferTrivia(nodes, _leadingTrivia, _trailingTrivia);
        }
        
        return builder.ReplaceAt(
            _region.ParentPath.ToArray(),
            _region.StartSlot,
            _region.SlotCount,
            nodes
        );
    }
    
    private static ImmutableArray<GreenNode> TransferTrivia(
        ImmutableArray<GreenNode> nodes,
        ImmutableArray<GreenTrivia> leading,
        ImmutableArray<GreenTrivia> trailing)
    {
        if (leading.IsEmpty && trailing.IsEmpty)
            return nodes;
        
        var result = nodes.ToBuilder();
        
        if (!leading.IsEmpty && result[0] is GreenLeaf firstLeaf)
        {
            var newLeading = firstLeaf.LeadingTrivia.IsEmpty 
                ? leading 
                : leading.AddRange(firstLeaf.LeadingTrivia);
            result[0] = firstLeaf.WithLeadingTrivia(newLeading);
        }
        
        if (!trailing.IsEmpty && result[^1] is GreenLeaf lastLeaf)
        {
            var newTrailing = lastLeaf.TrailingTrivia.IsEmpty 
                ? trailing 
                : lastLeaf.TrailingTrivia.AddRange(trailing);
            result[^1] = lastLeaf.WithTrailingTrivia(newTrailing);
        }
        
        return result.ToImmutable();
    }
}

/// <summary>
/// Contains all information needed to perform an insertion.
/// </summary>
internal readonly record struct InsertionPosition(
    NodePath ParentPath,
    int ChildIndex,
    int Position,
    NodePath? TargetPath,
    ImmutableArray<GreenTrivia> TargetLeadingTrivia,
    ImmutableArray<GreenTrivia> TargetTrailingTrivia);

#endregion
