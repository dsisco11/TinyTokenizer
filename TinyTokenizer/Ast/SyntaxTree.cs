using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Main entry point for navigating and mutating a token tree.
/// Wraps a green tree and provides lazy red node creation.
/// Supports undo via green root history.
/// </summary>
public class SyntaxTree
{
    private GreenNode _greenRoot;
    private RedNode? _redRoot;
    private readonly Stack<GreenNode> _undoStack = new();
    private readonly Stack<GreenNode> _redoStack = new();
    
    /// <summary>
    /// Creates a new syntax tree from a green root.
    /// </summary>
    public SyntaxTree(GreenNode greenRoot)
    {
        _greenRoot = greenRoot;
    }
    
    /// <summary>
    /// The green root node.
    /// </summary>
    public GreenNode GreenRoot => _greenRoot;
    
    /// <summary>
    /// The red root node. Created lazily and cached until mutation.
    /// </summary>
    public RedNode Root => _redRoot ??= _greenRoot.CreateRed(null, 0);
    
    /// <summary>
    /// Total width (character count) of the tree.
    /// </summary>
    public int Width => _greenRoot.Width;
    
    /// <summary>
    /// Whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;
    
    /// <summary>
    /// Whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;
    
    #region Mutation
    
    /// <summary>
    /// Applies a mutation to the tree.
    /// The mutation function receives a builder and returns a new green root.
    /// Supports undo/redo.
    /// </summary>
    public void Edit(Func<GreenTreeBuilder, GreenNode> mutation)
    {
        _undoStack.Push(_greenRoot);
        _redoStack.Clear();
        
        var builder = new GreenTreeBuilder(_greenRoot);
        _greenRoot = mutation(builder);
        _redRoot = null; // Invalidate red cache
    }
    
    /// <summary>
    /// Replaces the green root directly (advanced usage).
    /// Supports undo/redo.
    /// </summary>
    public void SetRoot(GreenNode newRoot)
    {
        _undoStack.Push(_greenRoot);
        _redoStack.Clear();
        _greenRoot = newRoot;
        _redRoot = null;
    }
    
    /// <summary>
    /// Undoes the last mutation.
    /// </summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0)
            return false;
        
        _redoStack.Push(_greenRoot);
        _greenRoot = _undoStack.Pop();
        _redRoot = null;
        return true;
    }
    
    /// <summary>
    /// Redoes the last undone mutation.
    /// </summary>
    public bool Redo()
    {
        if (_redoStack.Count == 0)
            return false;
        
        _undoStack.Push(_greenRoot);
        _greenRoot = _redoStack.Pop();
        _redRoot = null;
        return true;
    }
    
    /// <summary>
    /// Clears undo/redo history.
    /// </summary>
    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
    
    #endregion
    
    #region Factory Methods
    
    /// <summary>
    /// Creates a syntax tree by parsing source text.
    /// Uses the optimized GreenLexer path (direct to GreenNode, no intermediate Token allocation).
    /// </summary>
    public static SyntaxTree Parse(string source, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new GreenLexer(opts);
        return lexer.Parse(source);
    }
    
    /// <summary>
    /// Creates a syntax tree by parsing source text.
    /// Uses the optimized GreenLexer path (direct to GreenNode, no intermediate Token allocation).
    /// </summary>
    public static SyntaxTree Parse(ReadOnlyMemory<char> source, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new GreenLexer(opts);
        return lexer.Parse(source);
    }
    
    /// <summary>
    /// Creates an empty syntax tree.
    /// </summary>
    public static SyntaxTree Empty => new(new GreenList(ImmutableArray<GreenNode>.Empty));
    
    #endregion
    
    #region Query
    
    /// <summary>
    /// Finds the deepest node containing the specified position.
    /// </summary>
    public RedNode? FindNodeAt(int position) => Root.FindNodeAt(position);
    
    /// <summary>
    /// Finds the leaf at the specified position.
    /// </summary>
    public RedNode? FindLeafAt(int position) => Root.FindLeafAt(position);
    
    /// <summary>
    /// Gets all leaves in document order.
    /// </summary>
    public IEnumerable<RedLeaf> Leaves
    {
        get
        {
            foreach (var node in Root.DescendantsAndSelf())
            {
                if (node is RedLeaf leaf)
                    yield return leaf;
            }
        }
    }
    
    /// <summary>
    /// Gets all nodes of a specific kind.
    /// </summary>
    public IEnumerable<RedNode> NodesOfKind(NodeKind kind)
    {
        foreach (var node in Root.DescendantsAndSelf())
        {
            if (node.Kind == kind)
                yield return node;
        }
    }
    
    #endregion
    
    #region Output
    
    /// <summary>
    /// Reconstructs the source text from the tree.
    /// </summary>
    public string ToFullString()
    {
        var builder = new System.Text.StringBuilder(_greenRoot.Width);
        AppendTo(builder, _greenRoot);
        return builder.ToString();
    }
    
    private static void AppendTo(System.Text.StringBuilder builder, GreenNode node)
    {
        switch (node)
        {
            case GreenLeaf leaf:
                foreach (var trivia in leaf.LeadingTrivia)
                    builder.Append(trivia.Text);
                builder.Append(leaf.Text);
                foreach (var trivia in leaf.TrailingTrivia)
                    builder.Append(trivia.Text);
                break;
                
            case GreenBlock block:
                foreach (var trivia in block.LeadingTrivia)
                    builder.Append(trivia.Text);
                builder.Append(block.Opener);
                for (int i = 0; i < block.SlotCount; i++)
                {
                    var child = block.GetSlot(i);
                    if (child != null)
                        AppendTo(builder, child);
                }
                builder.Append(block.Closer);
                foreach (var trivia in block.TrailingTrivia)
                    builder.Append(trivia.Text);
                break;
                
            case GreenList list:
                for (int i = 0; i < list.SlotCount; i++)
                {
                    var child = list.GetSlot(i);
                    if (child != null)
                        AppendTo(builder, child);
                }
                break;
        }
    }
    
    #endregion
}
