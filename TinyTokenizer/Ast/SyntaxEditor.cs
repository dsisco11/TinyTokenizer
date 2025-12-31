using System.Collections.Immutable;

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
///     .Insert(Query.BraceBlock.First().InnerStart(), "console.log('enter');")
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
    
    #region Insert (Query-based)
    
    /// <summary>
    /// Queues an insertion of text at positions resolved by the query.
    /// </summary>
    /// <param name="query">An insertion query specifying where to insert.</param>
    /// <param name="text">The text to insert (will be parsed into nodes).</param>
    public SyntaxEditor Insert(InsertionQuery query, string text)
    {
        var positions = query.ResolvePositions(_tree).ToList();
        
        foreach (var pos in positions)
        {
            _edits.Add(new InsertEdit(pos, text) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text at positions resolved by multiple queries.
    /// </summary>
    public SyntaxEditor Insert(IEnumerable<InsertionQuery> queries, string text)
    {
        foreach (var query in queries)
        {
            Insert(query, text);
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of pre-built nodes at positions resolved by the query.
    /// </summary>
    internal SyntaxEditor Insert(InsertionQuery query, ImmutableArray<GreenNode> nodes)
    {
        var positions = query.ResolvePositions(_tree).ToList();
        
        foreach (var pos in positions)
        {
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of pre-built nodes at positions resolved by multiple queries.
    /// </summary>
    internal SyntaxEditor Insert(IEnumerable<InsertionQuery> queries, ImmutableArray<GreenNode> nodes)
    {
        foreach (var query in queries)
        {
            Insert(query, nodes);
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
    public SyntaxEditor InsertBefore(RedNode target, string text)
    {
        var pos = CreateInsertionPosition(target, InsertionPoint.Before);
        _edits.Add(new InsertEdit(pos, text) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text before each of the specified nodes.
    /// </summary>
    public SyntaxEditor InsertBefore(IEnumerable<RedNode> targets, string text)
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
    public SyntaxEditor InsertAfter(RedNode target, string text)
    {
        var pos = CreateInsertionPosition(target, InsertionPoint.After);
        _edits.Add(new InsertEdit(pos, text) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of text after each of the specified nodes.
    /// </summary>
    public SyntaxEditor InsertAfter(IEnumerable<RedNode> targets, string text)
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
    public SyntaxEditor InsertBefore(RedNode target, RedNode nodeToInsert)
    {
        return InsertBefore(target, [nodeToInsert.Green]);
    }
    
    /// <summary>
    /// Queues an insertion of a single node before the specified target.
    /// </summary>
    internal SyntaxEditor InsertBefore(RedNode target, GreenNode nodeToInsert)
    {
        return InsertBefore(target, [nodeToInsert]);
    }
    
    /// <summary>
    /// Queues an insertion of nodes before the specified target.
    /// </summary>
    public SyntaxEditor InsertBefore(RedNode target, IEnumerable<RedNode> nodesToInsert)
    {
        return InsertBefore(target, ToGreenNodes(nodesToInsert));
    }
    
    /// <summary>
    /// Queues an insertion of nodes before the specified target.
    /// </summary>
    internal SyntaxEditor InsertBefore(RedNode target, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        var pos = CreateInsertionPosition(target, InsertionPoint.Before);
        _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes before each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    public SyntaxEditor InsertBefore(IEnumerable<RedNode> targets, IEnumerable<RedNode> nodesToInsert)
    {
        var nodes = ToGreenNodes(nodesToInsert);
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, InsertionPoint.Before);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes before each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    internal SyntaxEditor InsertBefore(IEnumerable<RedNode> targets, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, InsertionPoint.Before);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of a single node after the specified target.
    /// </summary>
    public SyntaxEditor InsertAfter(RedNode target, RedNode nodeToInsert)
    {
        return InsertAfter(target, [nodeToInsert.Green]);
    }
    
    /// <summary>
    /// Queues an insertion of a single node after the specified target.
    /// </summary>
    internal SyntaxEditor InsertAfter(RedNode target, GreenNode nodeToInsert)
    {
        return InsertAfter(target, [nodeToInsert]);
    }
    
    /// <summary>
    /// Queues an insertion of nodes after the specified target.
    /// </summary>
    public SyntaxEditor InsertAfter(RedNode target, IEnumerable<RedNode> nodesToInsert)
    {
        return InsertAfter(target, ToGreenNodes(nodesToInsert));
    }
    
    /// <summary>
    /// Queues an insertion of nodes after the specified target.
    /// </summary>
    internal SyntaxEditor InsertAfter(RedNode target, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        var pos = CreateInsertionPosition(target, InsertionPoint.After);
        _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes after each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    public SyntaxEditor InsertAfter(IEnumerable<RedNode> targets, IEnumerable<RedNode> nodesToInsert)
    {
        var nodes = ToGreenNodes(nodesToInsert);
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, InsertionPoint.After);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of nodes after each of the specified targets.
    /// The same nodes are inserted at each target position.
    /// </summary>
    internal SyntaxEditor InsertAfter(IEnumerable<RedNode> targets, IEnumerable<GreenNode> nodesToInsert)
    {
        var nodes = nodesToInsert.ToImmutableArray();
        foreach (var target in targets)
        {
            var pos = CreateInsertionPosition(target, InsertionPoint.After);
            _edits.Add(new InsertNodesEdit(pos, nodes) { SequenceNumber = _sequenceNumber++ });
        }
        return this;
    }
    
    #endregion
    
    #region Remove (Query-based)
    
    /// <summary>
    /// Queues removal of all nodes matching the query.
    /// </summary>
    public SyntaxEditor Remove(INodeQuery query)
    {
        var nodes = query.Select(_tree).ToList();
        
        foreach (var node in nodes)
        {
            var path = NodePath.FromNode(node);
            _edits.Add(new RemoveEdit(path, node.Position) { SequenceNumber = _sequenceNumber++ });
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
    public SyntaxEditor Remove(RedNode node)
    {
        var path = NodePath.FromNode(node);
        _edits.Add(new RemoveEdit(path, node.Position) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues removal of all specified nodes.
    /// </summary>
    public SyntaxEditor Remove(IEnumerable<RedNode> nodes)
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
    /// </summary>
    public SyntaxEditor Replace(INodeQuery query, string text)
    {
        var nodes = query.Select(_tree).ToList();
        
        foreach (var node in nodes)
        {
            var path = NodePath.FromNode(node);
            var (leading, trailing) = GetTrivia(node);
            _edits.Add(new ReplaceEdit(path, text, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
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
    public SyntaxEditor Replace(INodeQuery query, Func<RedNode, string> replacer)
    {
        var nodes = query.Select(_tree).ToList();
        
        foreach (var node in nodes)
        {
            var text = replacer(node);
            var path = NodePath.FromNode(node);
            var (leading, trailing) = GetTrivia(node);
            _edits.Add(new ReplaceEdit(path, text, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all nodes matching any of the queries using a transformer function.
    /// The transformer receives the full RedNode, allowing access to content including trivia.
    /// Use <see cref="Edit(IEnumerable{INodeQuery}, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<INodeQuery> queries, Func<RedNode, string> replacer)
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
        var matchedNodes = query.Select(_tree).ToList();
        
        foreach (var node in matchedNodes)
        {
            var path = NodePath.FromNode(node);
            var (leading, trailing) = GetTrivia(node);
            _edits.Add(new ReplaceNodesEdit(path, nodes, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
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
    public SyntaxEditor Replace(RedNode node, string text)
    {
        var path = NodePath.FromNode(node);
        var (leading, trailing) = GetTrivia(node);
        _edits.Add(new ReplaceEdit(path, text, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
        return this;
    }
    
    /// <summary>
    /// Queues replacement of all specified nodes with new text.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<RedNode> nodes, string text)
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
    /// Use <see cref="Edit(RedNode, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(RedNode node, Func<RedNode, string> replacer)
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
    /// Use <see cref="Edit(IEnumerable{RedNode}, Func{string, string})"/> if you want to transform only the content without trivia.
    /// </summary>
    public SyntaxEditor Replace(IEnumerable<RedNode> nodes, Func<RedNode, string> replacer)
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
    public SyntaxEditor Replace(RedNode node, RedNode replacement)
    {
        return Replace(node, [replacement.Green]);
    }
    
    /// <summary>
    /// Queues replacement of the specified node with a single node.
    /// </summary>
    internal SyntaxEditor Replace(RedNode node, GreenNode replacement)
    {
        return Replace(node, [replacement]);
    }
    
    /// <summary>
    /// Queues replacement of the specified node with multiple nodes.
    /// </summary>
    public SyntaxEditor Replace(RedNode node, IEnumerable<RedNode> replacements)
    {
        return Replace(node, ToGreenNodes(replacements));
    }
    
    /// <summary>
    /// Queues replacement of the specified node with multiple nodes.
    /// </summary>
    internal SyntaxEditor Replace(RedNode node, IEnumerable<GreenNode> replacements)
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
    /// </summary>
    /// <param name="query">Query to select nodes to edit.</param>
    /// <param name="transformer">Function that transforms the node's content (without trivia).</param>
    public SyntaxEditor Edit(INodeQuery query, Func<string, string> transformer)
    {
        var nodes = query.Select(_tree).ToList();
        
        foreach (var node in nodes)
        {
            var path = NodePath.FromNode(node);
            var (leading, trailing) = GetTrivia(node);
            var contentWithoutTrivia = GetContentWithoutTrivia(node);
            var newText = transformer(contentWithoutTrivia);
            _edits.Add(new ReplaceEdit(path, newText, node.Position, leading, trailing) { SequenceNumber = _sequenceNumber++ });
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
    public SyntaxEditor Edit(RedNode node, Func<string, string> transformer)
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
    public SyntaxEditor Edit(IEnumerable<RedNode> nodes, Func<string, string> transformer)
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
    /// </summary>
    private static string GetContentWithoutTrivia(RedNode node)
    {
        if (node is RedLeaf leaf)
        {
            return leaf.Text;
        }
        
        if (node is RedBlock block)
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
            
            return fullText.Substring(leadingWidth, contentLength);
        }
        
        // For other node types (containers), return full text
        // They typically don't have trivia directly attached
        return node.ToText();
    }
    
    /// <summary>
    /// Extracts leading and trailing trivia from a node.
    /// </summary>
    private static (ImmutableArray<GreenTrivia> Leading, ImmutableArray<GreenTrivia> Trailing) GetTrivia(RedNode node)
    {
        if (node is RedLeaf leaf)
        {
            var greenLeaf = (GreenLeaf)leaf.Green;
            return (greenLeaf.LeadingTrivia, greenLeaf.TrailingTrivia);
        }
        
        if (node is RedBlock block)
        {
            return (block.LeadingTrivia, block.TrailingTrivia);
        }
        
        // For other containers, return empty trivia
        return (ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty);
    }
    
    /// <summary>
    /// Creates an InsertionPosition for inserting before or after a target node.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the target node has no parent.</exception>
    private static InsertionPosition CreateInsertionPosition(RedNode target, InsertionPoint point)
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
        
        return point switch
        {
            InsertionPoint.Before => new InsertionPosition(
                parentPath, childIndex, target.Position, point, targetPath, targetLeading, targetTrailing),
            InsertionPoint.After => new InsertionPosition(
                parentPath, childIndex + 1, target.EndPosition, point, targetPath, targetLeading, targetTrailing),
            _ => throw new ArgumentException($"Unsupported insertion point: {point}", nameof(point))
        };
    }
    
    /// <summary>
    /// Converts an enumerable of RedNodes to an ImmutableArray of their underlying GreenNodes.
    /// </summary>
    private static ImmutableArray<GreenNode> ToGreenNodes(IEnumerable<RedNode> redNodes)
    {
        return redNodes.Select(n => n.Green).ToImmutableArray();
    }
    
    #endregion
    
    #region Commit / Rollback
    
    /// <summary>
    /// Applies all pending edits atomically.
    /// The tree's undo stack is updated, allowing Undo() to revert.
    /// </summary>
    public void Commit()
    {
        if (_edits.Count == 0)
            return;
        
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
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        return builder.InsertAt(_insertPos.ParentPath.ToArray(), _insertPos.ChildIndex, _nodes);
    }
}

internal sealed class RemoveEdit : PendingEdit
{
    private readonly NodePath _path;
    private readonly int _position;
    
    public RemoveEdit(NodePath path, int position)
    {
        _path = path;
        _position = position;
    }
    
    public override int Position => _position;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var parentPath = _path.Parent();
        var childIndex = _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
        return builder.RemoveAt(parentPath.ToArray(), childIndex, 1);
    }
}

internal sealed class ReplaceEdit : PendingEdit
{
    private readonly NodePath _path;
    private readonly string _text;
    private readonly int _position;
    private readonly ImmutableArray<GreenTrivia> _leadingTrivia;
    private readonly ImmutableArray<GreenTrivia> _trailingTrivia;
    
    public ReplaceEdit(NodePath path, string text, int position, 
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default)
    {
        _path = path;
        _text = text;
        _position = position;
        _leadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        _trailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
    }
    
    public override int Position => _position;
    
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
        
        return builder.ReplaceAt(parentPath.ToArray(), childIndex, 1, nodes);
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
    private readonly ImmutableArray<GreenTrivia> _leadingTrivia;
    private readonly ImmutableArray<GreenTrivia> _trailingTrivia;
    
    public ReplaceNodesEdit(NodePath path, ImmutableArray<GreenNode> nodes, int position,
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default)
    {
        _path = path;
        _nodes = nodes;
        _position = position;
        _leadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        _trailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
    }
    
    public override int Position => _position;
    
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
        
        return builder.ReplaceAt(parentPath.ToArray(), childIndex, 1, nodes);
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

#endregion
