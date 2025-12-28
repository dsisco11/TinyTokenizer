using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the Red-Green tree AST implementation.
/// </summary>
public class SyntaxTreeTests
{
    #region Basic Parsing
    
    [Fact]
    public void Parse_SimpleExpression_CreatesTree()
    {
        var tree = SyntaxTree.Parse("a + b");
        
        Assert.NotNull(tree.Root);
        Assert.Equal(5, tree.Width);
        Assert.Equal("a + b", tree.ToFullString());
    }
    
    [Fact]
    public void Parse_WithBlock_CreatesNestedStructure()
    {
        var tree = SyntaxTree.Parse("{ x }");
        
        Assert.NotNull(tree.Root);
        Assert.Equal("{ x }", tree.ToFullString());
        
        // Root should be a list containing a block
        var root = tree.Root;
        Assert.True(root.IsContainer);
    }
    
    [Fact]
    public void Parse_EmptyInput_CreatesEmptyTree()
    {
        var tree = SyntaxTree.Parse("");
        
        Assert.NotNull(tree.Root);
        Assert.Equal(0, tree.Width);
        Assert.Equal("", tree.ToFullString());
    }
    
    #endregion
    
    #region Green Node Properties
    
    [Fact]
    public void GreenLeaf_Width_IncludesTrivia()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        var trailing = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        var leaf = new GreenLeaf(NodeKind.Ident, "foo", leading, trailing);
        
        Assert.Equal(6, leaf.Width); // 2 + 3 + 1
        Assert.Equal(3, leaf.TextWidth);
        Assert.Equal(2, leaf.LeadingTriviaWidth);
        Assert.Equal(1, leaf.TrailingTriviaWidth);
        Assert.Equal(2, leaf.TextOffset);
    }
    
    [Fact]
    public void GreenLeaf_NoTrivia_WidthEqualsTextLength()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "hello");
        
        Assert.Equal(5, leaf.Width);
        Assert.Equal(5, leaf.TextWidth);
        Assert.Equal(0, leaf.TextOffset);
    }
    
    [Fact]
    public void GreenBlock_Width_IncludesDelimitersAndChildren()
    {
        var children = ImmutableArray.Create<GreenNode>(
            new GreenLeaf(NodeKind.Ident, "x")
        );
        var block = new GreenBlock('{', children);
        
        Assert.Equal(3, block.Width); // { + x + }
        Assert.Equal('{', block.Opener);
        Assert.Equal('}', block.Closer);
    }
    
    #endregion
    
    #region Red Node Navigation
    
    [Fact]
    public void RedNode_Parent_NavigatesUpward()
    {
        var tree = SyntaxTree.Parse("{ a }");
        var root = tree.Root;
        
        // Find a leaf and check parent chain
        var leaf = root.DescendantsAndSelf().OfType<RedLeaf>().FirstOrDefault(l => l.Text == "a");
        Assert.NotNull(leaf);
        Assert.NotNull(leaf.Parent);
        Assert.Same(root, leaf.Root);
    }
    
    [Fact]
    public void RedNode_Position_IsComputed()
    {
        var tree = SyntaxTree.Parse("ab cd");
        var root = tree.Root;
        
        Assert.Equal(0, root.Position);
        Assert.Equal(5, root.EndPosition);
    }
    
    [Fact]
    public void RedBlock_ChildAccess_IsCached()
    {
        var tree = SyntaxTree.Parse("{ x }");
        var root = tree.Root as RedList;
        Assert.NotNull(root);
        
        var child1 = root.GetChild(0);
        var child2 = root.GetChild(0);
        
        Assert.Same(child1, child2); // Should be cached
    }
    
    #endregion
    
    #region Structural Sharing Mutations
    
    [Fact]
    public void GreenBlock_WithSlot_SharesSiblings()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var block = new GreenBlock('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        var newChild = new GreenLeaf(NodeKind.Ident, "X");
        var newBlock = block.WithSlot(1, newChild);
        
        // Siblings should be shared
        Assert.Same(child1, newBlock.GetSlot(0));
        Assert.Same(child3, newBlock.GetSlot(2));
        Assert.NotSame(child2, newBlock.GetSlot(1));
    }
    
    [Fact]
    public void GreenBlock_WithInsert_PreservesExisting()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var block = new GreenBlock('{', ImmutableArray.Create<GreenNode>(child1, child2));
        
        var newChild = new GreenLeaf(NodeKind.Ident, "X");
        var newBlock = block.WithInsert(1, ImmutableArray.Create<GreenNode>(newChild));
        
        Assert.Equal(3, newBlock.SlotCount);
        Assert.Same(child1, newBlock.GetSlot(0));
        Assert.Same(newChild, newBlock.GetSlot(1));
        Assert.Same(child2, newBlock.GetSlot(2));
    }
    
    #endregion
    
    #region SyntaxTree Mutations
    
    [Fact]
    public void SyntaxTree_Edit_CreatesNewVersion()
    {
        var tree = SyntaxTree.Parse("{ a }");
        var originalText = tree.ToFullString();
        
        tree.Edit(builder =>
        {
            var newLeaf = new GreenLeaf(NodeKind.Ident, "b");
            return builder.InsertAt(Array.Empty<int>(), 0, ImmutableArray.Create<GreenNode>(newLeaf));
        });
        
        Assert.NotEqual(originalText, tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxTree_Undo_RestoresPrevious()
    {
        var tree = SyntaxTree.Parse("{ a }");
        var originalText = tree.ToFullString();
        
        tree.Edit(builder =>
        {
            var newLeaf = new GreenLeaf(NodeKind.Ident, "b");
            return builder.InsertAt(Array.Empty<int>(), 0, ImmutableArray.Create<GreenNode>(newLeaf));
        });
        
        Assert.True(tree.CanUndo);
        tree.Undo();
        
        Assert.Equal(originalText, tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxTree_Redo_RestoresUndone()
    {
        var tree = SyntaxTree.Parse("{ a }");
        
        tree.Edit(builder =>
        {
            var newLeaf = new GreenLeaf(NodeKind.Ident, "b");
            return builder.InsertAt(Array.Empty<int>(), 0, ImmutableArray.Create<GreenNode>(newLeaf));
        });
        
        var afterEdit = tree.ToFullString();
        
        tree.Undo();
        Assert.True(tree.CanRedo);
        
        tree.Redo();
        Assert.Equal(afterEdit, tree.ToFullString());
    }
    
    #endregion
    
    #region NodePath
    
    [Fact]
    public void NodePath_Navigate_FindsNode()
    {
        var tree = SyntaxTree.Parse("{ a }");
        var root = tree.Root;
        
        var path = new NodePath(0); // First child of root
        var found = path.Navigate(root);
        
        Assert.NotNull(found);
    }
    
    [Fact]
    public void NodePath_FromNode_BuildsCorrectPath()
    {
        var tree = SyntaxTree.Parse("{ a }");
        var leaf = tree.Leaves.FirstOrDefault();
        
        if (leaf != null)
        {
            var path = NodePath.FromNode(leaf);
            var navigated = path.Navigate(tree.Root);
            
            Assert.Same(leaf, navigated);
        }
    }
    
    [Fact]
    public void NodePath_Root_IsEmpty()
    {
        var path = NodePath.Root;
        
        Assert.True(path.IsRoot);
        Assert.Equal(0, path.Depth);
        Assert.Equal("/", path.ToString());
    }
    
    #endregion
    
    #region Token Interning
    
    [Fact]
    public void GreenNodeCache_ReturnsSameInstance()
    {
        var leaf1 = GreenNodeCache.GetOrCreate(NodeKind.Symbol, "{");
        var leaf2 = GreenNodeCache.GetOrCreate(NodeKind.Symbol, "{");
        
        Assert.Same(leaf1, leaf2);
    }
    
    [Fact]
    public void GreenNodeCache_DifferentText_ReturnsDifferent()
    {
        var leaf1 = GreenNodeCache.GetOrCreate(NodeKind.Ident, "foo");
        var leaf2 = GreenNodeCache.GetOrCreate(NodeKind.Ident, "bar");
        
        Assert.NotSame(leaf1, leaf2);
    }
    
    #endregion
    
    #region FindNodeAt
    
    [Fact]
    public void FindNodeAt_ReturnsDeepestNode()
    {
        var tree = SyntaxTree.Parse("abc");
        var node = tree.FindNodeAt(1);
        
        Assert.NotNull(node);
    }
    
    [Fact]
    public void FindNodeAt_OutOfRange_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        var node = tree.FindNodeAt(100);
        
        Assert.Null(node);
    }
    
    #endregion
    
    #region GreenLexer Direct Path
    
    [Fact]
    public void GreenLexer_Parse_ProducesSameResult()
    {
        var source = "foo + bar";
        
        var lexer = new GreenLexer();
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
    }
    
    [Fact]
    public void GreenLexer_ParseWithBlocks_HandlesNesting()
    {
        var source = "{ a + b }";
        
        var lexer = new GreenLexer();
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
    }
    
    [Fact]
    public void GreenLexer_ParseWithComments_AttachesTrivia()
    {
        var source = "x // comment\ny";
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        
        var lexer = new GreenLexer(options);
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
    }
    
    [Fact]
    public void GreenLexer_ParseString_PreservesContent()
    {
        var source = "\"hello world\"";
        
        var lexer = new GreenLexer();
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
        
        var stringLeaf = tree.Leaves.FirstOrDefault(l => l.Kind == NodeKind.String);
        Assert.NotNull(stringLeaf);
        Assert.Equal(source, stringLeaf.Text);
    }
    
    [Fact]
    public void GreenLexer_ParseNumeric_HandlesDecimals()
    {
        var source = "123.456";
        
        var lexer = new GreenLexer();
        var tree = lexer.Parse(source);
        
        var numLeaf = tree.Leaves.FirstOrDefault(l => l.Kind == NodeKind.Numeric);
        Assert.NotNull(numLeaf);
        Assert.Equal("123.456", numLeaf.Text);
    }
    
    [Fact]
    public void GreenLexer_ParseOperators_RecognizesMultiChar()
    {
        var source = "a == b";
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        
        var lexer = new GreenLexer(options);
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
        
        var opLeaf = tree.Leaves.FirstOrDefault(l => l.Kind == NodeKind.Operator);
        Assert.NotNull(opLeaf);
        Assert.Equal("==", opLeaf.Text);
    }
    
    [Fact]
    public void GreenLexer_ParseTaggedIdent_RecognizesTags()
    {
        var source = "#define FOO";
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        
        var lexer = new GreenLexer(options);
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
        
        var tagLeaf = tree.Leaves.FirstOrDefault(l => l.Kind == NodeKind.TaggedIdent);
        Assert.NotNull(tagLeaf);
        Assert.Equal("#define", tagLeaf.Text);
    }
    
    [Fact]
    public void GreenLexer_RoundTrip_PreservesAllContent()
    {
        var source = @"function test() {
    // comment
    var x = 123.45;
    return x + 1;
}";
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .WithOperators(CommonOperators.CFamily);
        
        var lexer = new GreenLexer(options);
        var tree = lexer.Parse(source);
        
        Assert.Equal(source, tree.ToFullString());
    }
    
    #endregion
    
    #region SyntaxEditor and Query
    
    [Fact]
    public void Query_Ident_SelectsIdentifiers()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        
        var idents = Q.Ident.Select(tree).ToList();
        
        Assert.Equal(3, idents.Count);
    }
    
    [Fact]
    public void Query_WithText_FiltersExactMatch()
    {
        var tree = SyntaxTree.Parse("foo bar foo");
        
        var foos = Q.Ident.WithText("foo").Select(tree).ToList();
        
        Assert.Equal(2, foos.Count);
    }
    
    [Fact]
    public void Query_First_SelectsOnlyFirst()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        var first = Q.Ident.First().Select(tree).ToList();
        
        Assert.Single(first);
        Assert.Equal("a", ((RedLeaf)first[0]).Text);
    }
    
    [Fact]
    public void Query_Last_SelectsOnlyLast()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        var last = Q.Ident.Last().Select(tree).ToList();
        
        Assert.Single(last);
        Assert.Equal("c", ((RedLeaf)last[0]).Text);
    }
    
    [Fact]
    public void Query_BraceBlock_SelectsBlocks()
    {
        var tree = SyntaxTree.Parse("{ a } [ b ]");
        
        var braceBlocks = Q.BraceBlock.Select(tree).ToList();
        var bracketBlocks = Q.BracketBlock.Select(tree).ToList();
        
        Assert.Single(braceBlocks);
        Assert.Single(bracketBlocks);
    }
    
    [Fact]
    public void SyntaxEditor_Replace_ChangesNode()
    {
        var tree = SyntaxTree.Parse("foo");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), "bar")
            .Commit();
        
        Assert.Equal("bar", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Replace_WithTransformer()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), n => ((RedLeaf)n).Text.ToUpper())
            .Commit();
        
        Assert.Equal("HELLO", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Remove_DeletesNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        var originalWidth = tree.Width;
        
        tree.CreateEditor()
            .Remove(Q.Ident.WithText("b"))
            .Commit();
        
        var text = tree.ToFullString();
        Assert.DoesNotContain("b", text);
    }
    
    [Fact]
    public void SyntaxEditor_Insert_Before()
    {
        var tree = SyntaxTree.Parse("world");
        
        tree.CreateEditor()
            .Insert(Q.Ident.First().Before(), "hello ")
            .Commit();
        
        Assert.Equal("hello world", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Insert_After()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Insert(Q.Ident.First().After(), " world")
            .Commit();
        
        Assert.Equal("hello world", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Insert_IntoBlockStart()
    {
        var tree = SyntaxTree.Parse("{b}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "a ")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Insert_IntoBlockEnd()
    {
        var tree = SyntaxTree.Parse("{a}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerEnd(), " b")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Rollback_DiscardsChanges()
    {
        var tree = SyntaxTree.Parse("original");
        
        var editor = tree.CreateEditor();
        editor.Replace(Q.Ident.First(), "changed");
        editor.Rollback();
        
        Assert.Equal("original", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxEditor_Commit_SupportsUndo()
    {
        var tree = SyntaxTree.Parse("original");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), "changed")
            .Commit();
        
        Assert.Equal("changed", tree.ToFullString());
        Assert.True(tree.CanUndo);
        
        tree.Undo();
        Assert.Equal("original", tree.ToFullString());
    }
    
    #endregion
}
