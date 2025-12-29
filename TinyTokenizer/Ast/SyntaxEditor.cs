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
        
        foreach (var (parentPath, childIndex) in positions)
        {
            _edits.Add(new InsertEdit(parentPath, childIndex, text));
        }
        
        return this;
    }
    
    /// <summary>
    /// Queues an insertion of pre-built nodes at positions resolved by the query.
    /// </summary>
    public SyntaxEditor Insert(InsertionQuery query, ImmutableArray<GreenNode> nodes)
    {
        var positions = query.ResolvePositions(_tree).ToList();
        
        foreach (var (parentPath, childIndex) in positions)
        {
            _edits.Add(new InsertNodesEdit(parentPath, childIndex, nodes));
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
            _edits.Add(new RemoveEdit(path));
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
            _edits.Add(new ReplaceEdit(path, text));
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
            _edits.Add(new ReplaceEdit(path, text));
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
            _edits.Add(new ReplaceNodesEdit(path, nodes));
        }
        
        return this;
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
        
        // Sort edits by path depth (deepest first) then by index (highest first)
        // This ensures we don't invalidate paths when applying edits
        var sortedEdits = _edits
            .OrderByDescending(e => e.GetDepth())
            .ThenByDescending(e => e.GetIndex())
            .ToList();
        
        _tree.Edit(builder =>
        {
            var root = _tree.GreenRoot;
            
            foreach (var edit in sortedEdits)
            {
                root = edit.Apply(root, builder, _options);
            }
            
            return root;
        });
        
        _edits.Clear();
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
    public abstract int GetDepth();
    public abstract int GetIndex();
    public abstract GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options);
}

internal sealed class InsertEdit : PendingEdit
{
    private readonly NodePath _parentPath;
    private readonly int _childIndex;
    private readonly string _text;
    
    public InsertEdit(NodePath parentPath, int childIndex, string text)
    {
        _parentPath = parentPath;
        _childIndex = childIndex;
        _text = text;
    }
    
    public override int GetDepth() => _parentPath.Depth;
    public override int GetIndex() => _childIndex;
    
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
    
    public InsertNodesEdit(NodePath parentPath, int childIndex, ImmutableArray<GreenNode> nodes)
    {
        _parentPath = parentPath;
        _childIndex = childIndex;
        _nodes = nodes;
    }
    
    public override int GetDepth() => _parentPath.Depth;
    public override int GetIndex() => _childIndex;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        return builder.InsertAt(_parentPath.ToArray(), _childIndex, _nodes);
    }
}

internal sealed class RemoveEdit : PendingEdit
{
    private readonly NodePath _path;
    
    public RemoveEdit(NodePath path)
    {
        _path = path;
    }
    
    public override int GetDepth() => _path.Depth;
    public override int GetIndex() => _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
    
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
    
    public ReplaceEdit(NodePath path, string text)
    {
        _path = path;
        _text = text;
    }
    
    public override int GetDepth() => _path.Depth;
    public override int GetIndex() => _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var parentPath = _path.Parent();
        var childIndex = _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
        
        var lexer = new GreenLexer(options);
        var nodes = lexer.ParseToGreenNodes(_text);
        
        return builder.ReplaceAt(parentPath.ToArray(), childIndex, 1, nodes);
    }
}

internal sealed class ReplaceNodesEdit : PendingEdit
{
    private readonly NodePath _path;
    private readonly ImmutableArray<GreenNode> _nodes;
    
    public ReplaceNodesEdit(NodePath path, ImmutableArray<GreenNode> nodes)
    {
        _path = path;
        _nodes = nodes;
    }
    
    public override int GetDepth() => _path.Depth;
    public override int GetIndex() => _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
    
    public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
    {
        var parentPath = _path.Parent();
        var childIndex = _path.Depth > 0 ? _path[_path.Depth - 1] : 0;
        
        return builder.ReplaceAt(parentPath.ToArray(), childIndex, 1, _nodes);
    }
}

#endregion
