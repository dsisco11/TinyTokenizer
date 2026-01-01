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
    internal SyntaxTree(GreenNode greenRoot, Schema? schema = null)
    {
        _greenRoot = greenRoot;
        Schema = schema;
    }
    
    /// <summary>
    /// The schema used for tokenization and semantic analysis.
    /// May be null if tree was created without a schema.
    /// </summary>
    public Schema? Schema { get; }
    
    /// <summary>
    /// The green root node (internal implementation detail).
    /// </summary>
    internal GreenNode GreenRoot => _greenRoot;
    
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
    internal void Edit(Func<GreenTreeBuilder, GreenNode> mutation)
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
    internal void SetRoot(GreenNode newRoot)
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
    /// Creates a syntax tree by parsing source text with a schema.
    /// The schema provides both tokenization settings and semantic definitions.
    /// If the schema has syntax definitions, binding is automatically applied.
    /// </summary>
    /// <param name="source">The source text to parse.</param>
    /// <param name="schema">The schema for tokenization and semantic matching.</param>
    public static SyntaxTree Parse(string source, Schema schema)
    {
        var opts = schema.ToTokenizerOptions();
        var lexer = new GreenLexer(opts);
        var tree = lexer.Parse(source);
        var root = tree.GreenRoot;
        
        // Auto-bind if schema has syntax definitions
        if (!schema.SyntaxDefinitions.IsEmpty)
        {
            var binder = new SyntaxBinder(schema);
            root = binder.Bind(root);
        }
        
        return new SyntaxTree(root, schema);
    }
    
    /// <summary>
    /// Creates a syntax tree by parsing source text with a schema.
    /// If the schema has syntax definitions, binding is automatically applied.
    /// </summary>
    public static SyntaxTree Parse(ReadOnlyMemory<char> source, Schema schema)
    {
        var opts = schema.ToTokenizerOptions();
        var lexer = new GreenLexer(opts);
        var tree = lexer.Parse(source);
        var root = tree.GreenRoot;
        
        // Auto-bind if schema has syntax definitions
        if (!schema.SyntaxDefinitions.IsEmpty)
        {
            var binder = new SyntaxBinder(schema);
            root = binder.Bind(root);
        }
        
        return new SyntaxTree(root, schema);
    }
    
    /// <summary>
    /// Creates a syntax tree by parsing source text with default options.
    /// </summary>
    public static SyntaxTree Parse(string source, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new GreenLexer(opts);
        return lexer.Parse(source);
    }
    
    /// <summary>
    /// Creates a syntax tree by parsing source text with default options.
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
    
    /// <summary>
    /// Creates a syntax tree by parsing source text and applying syntax binding.
    /// This parses the source, then transforms the tree to recognize syntax patterns
    /// defined in the schema (e.g., function calls, property access).
    /// </summary>
    /// <param name="source">The source text to parse.</param>
    /// <param name="schema">The schema for tokenization and syntax definitions.</param>
    public static SyntaxTree ParseAndBind(string source, Schema schema)
    {
        var opts = schema.ToTokenizerOptions();
        var lexer = new GreenLexer(opts);
        var tree = lexer.Parse(source);
        
        // Apply syntax binding
        var binder = new SyntaxBinder(schema);
        var boundRoot = binder.Bind(tree.GreenRoot);
        
        return new SyntaxTree(boundRoot, schema);
    }
    
    /// <summary>
    /// Creates a syntax tree by parsing source text and applying syntax binding.
    /// </summary>
    public static SyntaxTree ParseAndBind(ReadOnlyMemory<char> source, Schema schema)
    {
        var opts = schema.ToTokenizerOptions();
        var lexer = new GreenLexer(opts);
        var tree = lexer.Parse(source);
        
        // Apply syntax binding
        var binder = new SyntaxBinder(schema);
        var boundRoot = binder.Bind(tree.GreenRoot);
        
        return new SyntaxTree(boundRoot, schema);
    }
    
    #endregion
    
    #region Syntax Binding
    
    /// <summary>
    /// Applies syntax binding to this tree, recognizing syntax patterns and
    /// wrapping matched subtrees in typed syntax nodes.
    /// Requires a schema with syntax definitions to be attached.
    /// </summary>
    /// <returns>A new syntax tree with syntax bindings applied.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no schema is attached.</exception>
    public SyntaxTree Bind()
    {
        if (Schema == null)
            throw new InvalidOperationException(
                "No schema attached. Use SyntaxTree.Parse(source, schema) or provide schema parameter.");
        
        return Bind(Schema);
    }
    
    /// <summary>
    /// Applies syntax binding to this tree using the specified schema.
    /// </summary>
    /// <param name="schema">The schema containing syntax definitions.</param>
    /// <returns>A new syntax tree with syntax bindings applied.</returns>
    public SyntaxTree Bind(Schema schema)
    {
        var binder = new SyntaxBinder(schema);
        var boundRoot = binder.Bind(_greenRoot);
        return new SyntaxTree(boundRoot, schema);
    }
    
    #endregion
    
    #region Editor
    
    /// <summary>
    /// Creates a new SyntaxEditor for making batched mutations to this tree.
    /// </summary>
    public SyntaxEditor CreateEditor(TokenizerOptions? options = null)
    {
        return new SyntaxEditor(this, options);
    }
    
    #endregion
    
    #region Semantic Matching
    
    /// <summary>
    /// Finds all semantic nodes of the specified type in the tree.
    /// Requires a schema to be attached (via Parse with schema).
    /// </summary>
    /// <typeparam name="T">The semantic node type to match.</typeparam>
    /// <param name="context">Optional semantic context for factory.</param>
    /// <returns>All matching semantic nodes.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no schema is attached.</exception>
    public IEnumerable<T> Match<T>(SemanticContext? context = null) where T : SemanticNode
    {
        if (Schema == null)
            throw new InvalidOperationException(
                "No schema attached. Use SyntaxTree.Parse(source, schema) or provide schema parameter.");
        
        return Match<T>(Schema, context);
    }
    
    /// <summary>
    /// Finds all semantic nodes of the specified type using an explicit schema.
    /// </summary>
    public IEnumerable<T> Match<T>(Schema schema, SemanticContext? context = null) where T : SemanticNode
    {
        var definition = schema.GetDefinition<T>();
        if (definition == null)
            yield break;
        
        var walker = new TreeWalker(Root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            foreach (var pattern in definition.Patterns)
            {
                if (pattern.TryMatch(node, out var match))
                {
                    var semantic = definition.TryCreate(match, context);
                    if (semantic != null)
                    {
                        yield return (T)semantic;
                        break;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Finds all semantic nodes of any registered type in the tree.
    /// Requires a schema to be attached.
    /// </summary>
    public IEnumerable<SemanticNode> MatchAll(SemanticContext? context = null)
    {
        if (Schema == null)
            throw new InvalidOperationException(
                "No schema attached. Use SyntaxTree.Parse(source, schema) or provide schema parameter.");
        
        return MatchAll(Schema, context);
    }
    
    /// <summary>
    /// Finds all semantic nodes using an explicit schema.
    /// </summary>
    public IEnumerable<SemanticNode> MatchAll(Schema schema, SemanticContext? context = null)
    {
        foreach (var definition in schema.SortedDefinitions)
        {
            var walker = new TreeWalker(Root);
            foreach (var node in walker.DescendantsAndSelf())
            {
                foreach (var pattern in definition.Patterns)
                {
                    if (pattern.TryMatch(node, out var match))
                    {
                        var semantic = definition.TryCreate(match, context);
                        if (semantic != null)
                        {
                            yield return semantic;
                            break;
                        }
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region Query
    
    /// <summary>
    /// Selects all nodes matching the query from this tree.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <returns>All matching nodes in document order.</returns>
    public IEnumerable<RedNode> Select(INodeQuery query) => query.Select(this);
    
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
            var walker = new TreeWalker(Root, NodeFilter.Leaves);
            foreach (var node in walker.DescendantsAndSelf())
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
        var walker = new TreeWalker(Root);
        foreach (var node in walker.DescendantsAndSelf())
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
                foreach (var trivia in block.InnerTrivia)
                    builder.Append(trivia.Text);
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
