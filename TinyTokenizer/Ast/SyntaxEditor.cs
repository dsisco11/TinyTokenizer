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
///     .Replace(Query.Numeric.First(), "42")
///     .Remove(Query.Ident.WithText("unused"))
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
    
    #region Insert
    
    /// <summary>
    /// Queues an insertion of text at positions resolved by the query.
    /// </summary>
    /// <param name="query">An insertion query specifying where to insert.</param>
    /// <param name="text">The text to insert (will be parsed into nodes).</param>
    public SyntaxEditor Insert(InsertionQuery query, string text)
    {
        var positions = query.ResolvePositions(_tree).ToList();
        
        foreach (var (parentPath, childIndex, position) in positions)
        {
            _edits.Add(new InsertEdit(parentPath, childIndex, text, position) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of pre-built nodes at positions resolved by the query.
    /// </summary>
    public SyntaxEditor Insert(InsertionQuery query, ImmutableArray<GreenNode> nodes)
    {
        var positions = query.ResolvePositions(_tree).ToList();
        
        foreach (var (parentPath, childIndex, position) in positions)
        {
            _edits.Add(new InsertNodesEdit(parentPath, childIndex, nodes, position) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    #endregion
    
    #region Remove
    
    /// <summary>
    /// Queues removal of all nodes matching the query.
    /// </summary>
    public SyntaxEditor Remove(NodeQuery query)
    {
        var nodes = query.Select(_tree).ToList();
        
        foreach (var node in nodes)
        {
            var path = NodePath.FromNode(node);
            _edits.Add(new RemoveEdit(path, node.Position) { SequenceNumber = _sequenceNumber++ });
        }
        
        return this;
    }
    
    #endregion
    
    #region Replace
    
    /// <summary>
    /// Queues replacement of all nodes matching the query with new text.
    /// </summary>
    public SyntaxEditor Replace(NodeQuery query, string text)
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
    /// Queues replacement of all nodes matching the query using a transformer function.
    /// </summary>
    public SyntaxEditor Replace(NodeQuery query, Func<RedNode, string> replacer)
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
    /// Queues replacement of all nodes matching the query with pre-built nodes.
    /// </summary>
    public SyntaxEditor Replace(NodeQuery query, ImmutableArray<GreenNode> nodes)
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
    /// Extracts leading and trailing trivia from a node.
    /// </summary>
    private static (ImmutableArray<GreenTrivia> Leading, ImmutableArray<GreenTrivia> Trailing) GetTrivia(RedNode node)
    {
        if (node is RedLeaf leaf)
        {
            var greenLeaf = (GreenLeaf)leaf.Green;
            return (greenLeaf.LeadingTrivia, greenLeaf.TrailingTrivia);
        }
        
        // For blocks/containers, we might want to preserve outer trivia
        // For now, return empty trivia for non-leaves
        return (ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty);
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
    private readonly NodePath _parentPath;
    private readonly int _childIndex;
    private readonly string _text;
    private readonly int _position;
    
    public InsertEdit(NodePath parentPath, int childIndex, string text, int position)
    {
        _parentPath = parentPath;
        _childIndex = childIndex;
        _text = text;
        _position = position;
    }
    
    public override int Position => _position;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var lexer = new GreenLexer(options);
        var nodes = lexer.ParseToGreenNodes(_text);
        return builder.InsertAt(_parentPath.ToArray(), _childIndex, nodes);
    }
}

internal sealed class InsertNodesEdit : PendingEdit
{
    private readonly NodePath _parentPath;
    private readonly int _childIndex;
    private readonly ImmutableArray<GreenNode> _nodes;
    private readonly int _position;
    
    public InsertNodesEdit(NodePath parentPath, int childIndex, ImmutableArray<GreenNode> nodes, int position)
    {
        _parentPath = parentPath;
        _childIndex = childIndex;
        _nodes = nodes;
        _position = position;
    }
    
    public override int Position => _position;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        return builder.InsertAt(_parentPath.ToArray(), _childIndex, _nodes);
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
