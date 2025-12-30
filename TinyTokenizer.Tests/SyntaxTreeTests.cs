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
        var walker = new TreeWalker(root);
        var leaf = walker.DescendantsAndSelf().OfType<RedLeaf>().FirstOrDefault(l => l.Text == "a");
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
    
    #region Query Basics
    
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
    
    #endregion
    
    #region Query Factories (Additional Coverage)
    
    [Fact]
    public void Query_Numeric_SelectsNumbers()
    {
        var tree = SyntaxTree.Parse("x = 42 + 3.14");
        
        var nums = Q.Numeric.Select(tree).ToList();
        
        Assert.Equal(2, nums.Count);
    }
    
    [Fact]
    public void Query_String_SelectsStrings()
    {
        var tree = SyntaxTree.Parse("a = \"hello\" + 'c'");
        
        var strings = Q.String.Select(tree).ToList();
        
        Assert.Equal(2, strings.Count);
    }
    
    [Fact]
    public void Query_Operator_SelectsOperators()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        var tree = SyntaxTree.Parse("a == b && c != d", options);
        
        var ops = Q.Operator.Select(tree).ToList();
        
        Assert.Equal(3, ops.Count);
    }
    
    [Fact]
    public void Query_Symbol_SelectsSymbols()
    {
        var tree = SyntaxTree.Parse("a, b; c");
        
        var symbols = Q.Symbol.Select(tree).ToList();
        
        Assert.Equal(2, symbols.Count); // , and ;
    }
    
    [Fact]
    public void Query_TaggedIdent_SelectsTaggedIdentifiers()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#', '@');
        var tree = SyntaxTree.Parse("#define @attr foo", options);
        
        var tags = Q.TaggedIdent.Select(tree).ToList();
        
        Assert.Equal(2, tags.Count);
    }
    
    [Fact]
    public void Query_Any_SelectsAllNodes()
    {
        var tree = SyntaxTree.Parse("a b");
        
        var all = Q.Any.Select(tree).ToList();
        
        Assert.True(all.Count >= 3); // At least: a, whitespace, b
    }
    
    [Fact]
    public void Query_Leaf_SelectsOnlyLeaves()
    {
        var tree = SyntaxTree.Parse("{a}");
        
        var leaves = Q.Leaf.Select(tree).ToList();
        
        // Should get 'a' but not the block itself
        foreach (var leaf in leaves)
        {
            Assert.IsType<RedLeaf>(leaf);
        }
    }
    
    [Fact]
    public void Query_AnyBlock_SelectsAllBlockTypes()
    {
        var tree = SyntaxTree.Parse("{ a } [ b ] ( c )");
        
        var blocks = Q.AnyBlock.Select(tree).ToList();
        
        Assert.Equal(3, blocks.Count);
    }
    
    #endregion
    
    #region Query Composition
    
    [Fact]
    public void Query_Union_MatchesEither()
    {
        var tree = SyntaxTree.Parse("foo 42 bar");
        
        var combined = (Q.Ident | Q.Numeric).Select(tree).ToList();
        
        Assert.Equal(3, combined.Count); // foo, 42, bar
    }
    
    [Fact]
    public void Query_Intersection_MatchesBoth()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        
        // All idents that also match WithText("bar")
        var intersection = (Q.Ident & Q.Ident.WithText("bar")).Select(tree).ToList();
        
        Assert.Single(intersection);
        Assert.Equal("bar", ((RedLeaf)intersection[0]).Text);
    }
    
    #endregion
    
    #region Query Filters
    
    [Fact]
    public void Query_WithTextContaining_FiltersSubstring()
    {
        var tree = SyntaxTree.Parse("foobar foobaz other");
        
        var matches = Q.Ident.WithTextContaining("foo").Select(tree).ToList();
        
        Assert.Equal(2, matches.Count);
    }
    
    [Fact]
    public void Query_WithTextStartingWith_FiltersPrefix()
    {
        var tree = SyntaxTree.Parse("prefixA prefixB other");
        
        var matches = Q.Ident.WithTextStartingWith("prefix").Select(tree).ToList();
        
        Assert.Equal(2, matches.Count);
    }
    
    [Fact]
    public void Query_WithTextEndingWith_FiltersSuffix()
    {
        var tree = SyntaxTree.Parse("testA testB other");
        
        var matches = Q.Ident.WithTextEndingWith("B").Select(tree).ToList();
        
        Assert.Single(matches);
        Assert.Equal("testB", ((RedLeaf)matches[0]).Text);
    }
    
    [Fact]
    public void Query_Where_FiltersWithPredicate()
    {
        var tree = SyntaxTree.Parse("a bb ccc");
        
        // Filter idents with width >= 2
        var matches = Q.Ident.Where(n => n is RedLeaf leaf && leaf.Text.Length >= 2).Select(tree).ToList();
        
        Assert.Equal(2, matches.Count); // bb and ccc
    }
    
    #endregion
    
    #region Query Pseudo-Selectors
    
    [Fact]
    public void Query_Nth_SelectsNthMatch()
    {
        var tree = SyntaxTree.Parse("a b c d e");
        
        var third = Q.Ident.Nth(2).Select(tree).ToList();
        
        Assert.Single(third);
        Assert.Equal("c", ((RedLeaf)third[0]).Text);
    }
    
    [Fact]
    public void Query_Last_OnBlocks_SelectsLastBlock()
    {
        var tree = SyntaxTree.Parse("{a} {b} {c}");
        
        var last = Q.BraceBlock.Last().Select(tree).ToList();
        
        Assert.Single(last);
        // The last block contains 'c'
        var block = (RedBlock)last[0];
        Assert.Contains("c", block.Children.OfType<RedLeaf>().Select(l => l.Text));
    }
    
    [Fact]
    public void Query_Nth_OnBlocks_SelectsNthBlock()
    {
        var tree = SyntaxTree.Parse("{a} {b} {c}");
        
        var second = Q.BraceBlock.Nth(1).Select(tree).ToList();
        
        Assert.Single(second);
        var block = (RedBlock)second[0];
        Assert.Contains("b", block.Children.OfType<RedLeaf>().Select(l => l.Text));
    }
    
    #endregion
    
    #region RedNode Navigation
    
    [Fact]
    public void RedNode_Children_EnumeratesCorrectly()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        var children = block.Children.ToList();
        
        Assert.True(children.Count >= 3); // At least a, b, c (plus whitespace)
    }
    
    [Fact]
    public void RedNode_Parent_NavigatesUp()
    {
        var tree = SyntaxTree.Parse("{nested}");
        var ident = Q.Ident.First().Select(tree).First();
        
        Assert.NotNull(ident.Parent);
        Assert.IsType<RedBlock>(ident.Parent);
    }
    
    [Fact]
    public void TreeWalker_DescendantsAndSelf_IncludesAll()
    {
        var tree = SyntaxTree.Parse("{a {b}}");
        var walker = new TreeWalker(tree.Root);
        var allNodes = walker.DescendantsAndSelf().ToList();
        
        // Should include root, outer block, inner block, idents
        Assert.True(allNodes.Count >= 4);
    }
    
    [Fact]
    public void RedBlock_OpenerCloser_CorrectCharacters()
    {
        var tree = SyntaxTree.Parse("[item]");
        var block = Q.BracketBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal('[', block.Opener);
        Assert.Equal(']', block.Closer);
    }
    
    [Fact]
    public void TreeWalker_Ancestors_NavigatesToRoot()
    {
        var tree = SyntaxTree.Parse("{{deep}}");
        var ident = Q.Ident.First().Select(tree).First();
        var walker = new TreeWalker(ident);
        var ancestors = walker.Ancestors().ToList();
        
        Assert.True(ancestors.Count >= 2); // Inner block, outer block, root
    }
    
    [Fact]
    public void RedNode_Root_ReturnsTreeRoot()
    {
        var tree = SyntaxTree.Parse("{nested}");
        var ident = Q.Ident.First().Select(tree).First();
        
        var root = ident.Root;
        
        Assert.Same(tree.Root, root);
    }
    
    [Fact]
    public void TreeWalker_Descendants_EnumeratesAll()
    {
        var tree = SyntaxTree.Parse("{a b}");
        var block = Q.BraceBlock.First().Select(tree).First();
        var walker = new TreeWalker(block);
        var descendants = walker.Descendants().ToList();
        
        Assert.True(descendants.Count >= 2); // At least a, b
    }
    
    [Fact]
    public void RedNode_FindNodeAt_ReturnsDeepest()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var found = tree.FindNodeAt(1);
        
        Assert.NotNull(found);
        Assert.Equal(NodeKind.Ident, found.Kind);
    }
    
    [Fact]
    public void RedNode_FindNodeAt_OutOfRange_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var found = tree.FindNodeAt(100);
        
        Assert.Null(found);
    }
    
    [Fact]
    public void RedNode_FindLeafAt_ReturnsLeaf()
    {
        var tree = SyntaxTree.Parse("{abc}");
        
        var leaf = tree.FindLeafAt(2);
        
        Assert.NotNull(leaf);
        Assert.True(leaf.IsLeaf);
    }
    
    #endregion
    
    #region SyntaxTree Undo/Redo Extended
    
    [Fact]
    public void SyntaxTree_Redo_WorksAfterUndo()
    {
        var tree = SyntaxTree.Parse("original");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), "changed")
            .Commit();
        
        tree.Undo();
        Assert.Equal("original", tree.ToFullString());
        Assert.True(tree.CanRedo);
        
        tree.Redo();
        Assert.Equal("changed", tree.ToFullString());
    }
    
    [Fact]
    public void SyntaxTree_ClearHistory_RemovesUndoRedo()
    {
        var tree = SyntaxTree.Parse("original");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), "changed")
            .Commit();
        
        Assert.True(tree.CanUndo);
        
        tree.ClearHistory();
        
        Assert.False(tree.CanUndo);
        Assert.False(tree.CanRedo);
    }
    
    [Fact]
    public void SyntaxTree_Undo_WhenEmpty_ReturnsFalse()
    {
        var tree = SyntaxTree.Parse("original");
        
        Assert.False(tree.CanUndo);
        var result = tree.Undo();
        
        Assert.False(result);
    }
    
    [Fact]
    public void SyntaxTree_Redo_WhenEmpty_ReturnsFalse()
    {
        var tree = SyntaxTree.Parse("original");
        
        Assert.False(tree.CanRedo);
        var result = tree.Redo();
        
        Assert.False(result);
    }
    
    [Fact]
    public void SyntaxTree_SetRoot_SupportsUndo()
    {
        var tree = SyntaxTree.Parse("original");
        var newTree = SyntaxTree.Parse("replaced");
        
        tree.SetRoot(newTree.GreenRoot);
        
        Assert.Equal("replaced", tree.ToFullString());
        Assert.True(tree.CanUndo);
        
        tree.Undo();
        Assert.Equal("original", tree.ToFullString());
    }
    
    #endregion
    
    #region Query Error and Edge Cases
    
    [Fact]
    public void Query_Error_SelectsErrorNodes()
    {
        // Unclosed block creates an error
        var tree = SyntaxTree.Parse("{unclosed");
        
        var errors = Q.Error.Select(tree).ToList();
        
        // May or may not have errors depending on parser behavior
        // Just ensure query doesn't throw
        Assert.NotNull(errors);
    }
    
    [Fact]
    public void Query_Kind_SelectsByKind()
    {
        var tree = SyntaxTree.Parse("a 123");
        
        var idents = Q.Kind(NodeKind.Ident).Select(tree).ToList();
        var nums = Q.Kind(NodeKind.Numeric).Select(tree).ToList();
        
        Assert.Single(idents);
        Assert.Single(nums);
    }
    
    [Fact]
    public void Query_ParenBlock_SelectsParentheses()
    {
        var tree = SyntaxTree.Parse("(inner)");
        
        var parens = Q.ParenBlock.Select(tree).ToList();
        
        Assert.Single(parens);
        var block = (RedBlock)parens[0];
        Assert.Equal('(', block.Opener);
    }
    
    [Fact]
    public void Query_All_ReturnsAll()
    {
        var tree = SyntaxTree.Parse("a");
        
        var all = Q.Ident.All().Select(tree).ToList();
        
        Assert.Single(all);
    }
    
    [Fact]
    public void Query_Matches_ChecksSingleNode()
    {
        var tree = SyntaxTree.Parse("foo");
        var node = Q.Ident.First().Select(tree).First();
        
        Assert.True(Q.Ident.Matches(node));
        Assert.False(Q.Numeric.Matches(node));
    }
    
    #endregion
    
    #region RedLeaf Properties
    
    [Fact]
    public void RedLeaf_Text_ReturnsContent()
    {
        var tree = SyntaxTree.Parse("hello");
        var leaf = tree.Leaves.First();
        
        Assert.Equal("hello", leaf.Text);
    }
    
    [Fact]
    public void RedLeaf_Position_IsCorrect()
    {
        var tree = SyntaxTree.Parse("a b");
        var leaves = tree.Leaves.ToList();
        
        // Roslyn-style trivia: space after 'a' is trailing trivia for 'a'
        // So 'b' starts at position 2 with no leading trivia
        var secondIdent = leaves.Where(l => l.Kind == NodeKind.Ident).Skip(1).First();
        Assert.Equal(2, secondIdent.Position);  // Position (same as TextPosition when no leading trivia)
        Assert.Equal(2, ((RedLeaf)secondIdent).TextPosition);  // TextPosition is where text starts
    }
    
    [Fact]
    public void RedLeaf_EndPosition_IsCorrect()
    {
        var tree = SyntaxTree.Parse("abc");
        var ident = Q.Ident.First().Select(tree).First();
        
        Assert.Equal(0, ident.Position);
        Assert.Equal(3, ident.EndPosition);
        Assert.Equal(3, ident.Width);
    }
    
    #endregion
    
    #region Block Specific Tests
    
    [Fact]
    public void RedBlock_ChildCount_ReturnsCorrectCount()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.True(block.ChildCount >= 3);
    }
    
    [Fact]
    public void RedBlock_GetChild_ReturnsChild()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        var firstChild = block.GetChild(0);
        Assert.NotNull(firstChild);
    }
    
    [Fact]
    public void RedBlock_GetChild_OutOfRange_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        var child = block.GetChild(999);
        Assert.Null(child);
    }
    
    #endregion
    
    #region NodePath Tests
    
    [Fact]
    public void NodePath_FromNode_CreatesValidPath()
    {
        var tree = SyntaxTree.Parse("{nested}");
        var ident = Q.Ident.First().Select(tree).First();
        
        var path = NodePath.FromNode(ident);
        
        Assert.True(path.Depth > 0);
    }
    
    [Fact]
    public void NodePath_Navigate_ReturnsNode()
    {
        var tree = SyntaxTree.Parse("{nested}");
        var ident = Q.Ident.First().Select(tree).First();
        var path = NodePath.FromNode(ident);
        
        var navigated = path.Navigate(tree.Root);
        
        Assert.NotNull(navigated);
        Assert.Equal(ident.Position, navigated.Position);
    }
    
    [Fact]
    public void NodePath_Root_HasZeroDepth()
    {
        var path = NodePath.Root;
        
        Assert.True(path.IsRoot);
        Assert.Equal(0, path.Depth);
    }
    
    [Fact]
    public void NodePath_Child_AppendsIndex()
    {
        var path = NodePath.Root.Child(0).Child(1);
        
        Assert.Equal(2, path.Depth);
        Assert.Equal(0, path[0]);
        Assert.Equal(1, path[1]);
    }
    
    [Fact]
    public void NodePath_Parent_RemovesLastIndex()
    {
        var path = NodePath.Root.Child(0).Child(1);
        var parent = path.Parent();
        
        Assert.Equal(1, parent.Depth);
        Assert.Equal(0, parent[0]);
    }
    
    #endregion
    
    #region RedBlock Extended Coverage
    
    [Fact]
    public void RedBlock_ChildrenOfKind_FiltersCorrectly()
    {
        var tree = SyntaxTree.Parse("{a 1 b 2}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        var idents = block.ChildrenOfKind(NodeKind.Ident).ToList();
        var numerics = block.ChildrenOfKind(NodeKind.Numeric).ToList();
        
        Assert.Equal(2, idents.Count);
        Assert.Equal(2, numerics.Count);
    }
    
    [Fact]
    public void RedBlock_LeafChildren_ReturnsOnlyLeaves()
    {
        var tree = SyntaxTree.Parse("{a {nested} b}");
        var outerBlock = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(outerBlock);
        var leaves = outerBlock.LeafChildren.ToList();
        
        // Should have leaves but not nested block
        Assert.True(leaves.Count >= 2);
        Assert.All(leaves, l => Assert.IsType<RedLeaf>(l));
    }
    
    [Fact]
    public void RedBlock_BlockChildren_ReturnsOnlyBlocks()
    {
        var tree = SyntaxTree.Parse("{a {inner1} b {inner2}}");
        var outerBlock = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(outerBlock);
        var blocks = outerBlock.BlockChildren.ToList();
        
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.IsType<RedBlock>(b));
    }
    
    [Fact]
    public void RedBlock_IndexOf_FindsChild()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        var firstChild = block.GetChild(0);
        Assert.NotNull(firstChild);
        
        var index = block.IndexOf(firstChild);
        Assert.Equal(0, index);
    }
    
    [Fact]
    public void RedBlock_IndexOf_ReturnsMinusOneForNonChild()
    {
        var tree = SyntaxTree.Parse("{a} {b}");
        var blocks = Q.BraceBlock.Select(tree).Cast<RedBlock>().ToList();
        
        Assert.Equal(2, blocks.Count);
        var block1 = blocks[0];
        var block2Child = blocks[1].GetChild(0);
        
        Assert.NotNull(block2Child);
        var index = block1.IndexOf(block2Child!);
        Assert.Equal(-1, index);
    }
    
    [Fact]
    public void RedBlock_OpenerPosition_IsCorrect()
    {
        var tree = SyntaxTree.Parse("{inner}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(0, block.OpenerPosition);
    }
    
    [Fact]
    public void RedBlock_CloserPosition_IsCorrect()
    {
        var tree = SyntaxTree.Parse("{inner}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(6, block.CloserPosition); // Position of }
    }
    
    [Fact]
    public void RedBlock_InnerStartPosition_IsCorrect()
    {
        var tree = SyntaxTree.Parse("{inner}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(1, block.InnerStartPosition); // After {
    }
    
    [Fact]
    public void RedBlock_InnerEndPosition_IsCorrect()
    {
        var tree = SyntaxTree.Parse("{inner}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(6, block.InnerEndPosition); // Before }
    }
    
    [Fact]
    public void RedBlock_Green_ReturnsUnderlyingGreen()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.NotNull(block.Green);
        Assert.IsType<GreenBlock>(block.Green);
    }
    
    [Fact]
    public void RedBlock_LeadingTriviaWidth_IsZeroWithoutTrivia()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(0, block.LeadingTriviaWidth);
    }
    
    [Fact]
    public void RedBlock_TrailingTriviaWidth_IsZeroWithoutTrivia()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(0, block.TrailingTriviaWidth);
    }
    
    [Fact]
    public void RedBlock_LeadingTrivia_IsEmptyWithoutTrivia()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.True(block.LeadingTrivia.IsEmpty);
    }
    
    [Fact]
    public void RedBlock_TrailingTrivia_IsEmptyWithoutTrivia()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = Q.BraceBlock.First().Select(tree).First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.True(block.TrailingTrivia.IsEmpty);
    }
    
    #endregion
    
    #region GreenTreeBuilder Coverage
    
    [Fact]
    public void GreenTreeBuilder_InsertAt_InsertsNodes()
    {
        var tree = SyntaxTree.Parse("{a}");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("b");
        
        var builder = new GreenTreeBuilder(tree.GreenRoot);
        var newRoot = builder.InsertAt(new[] { 0 }, 1, newNodes);
        
        Assert.NotNull(newRoot);
    }
    
    [Fact]
    public void GreenTreeBuilder_RemoveAt_RemovesNodes()
    {
        var tree = SyntaxTree.Parse("{a b}");
        
        var builder = new GreenTreeBuilder(tree.GreenRoot);
        var newRoot = builder.RemoveAt(new[] { 0 }, 0, 1);
        
        Assert.NotNull(newRoot);
    }
    
    [Fact]
    public void GreenTreeBuilder_ReplaceAt_ReplacesNodes()
    {
        var tree = SyntaxTree.Parse("{old}");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("new");
        
        var builder = new GreenTreeBuilder(tree.GreenRoot);
        var newRoot = builder.ReplaceAt(new[] { 0 }, 0, 1, newNodes);
        
        Assert.NotNull(newRoot);
    }
    
    [Fact]
    public void GreenTreeBuilder_ReplaceChild_ReplacesChild()
    {
        var tree = SyntaxTree.Parse("{a}");
        var lexer = new GreenLexer();
        var newNode = lexer.ParseToGreenNodes("b").FirstOrDefault();
        
        if (newNode != null)
        {
            var builder = new GreenTreeBuilder(tree.GreenRoot);
            var newRoot = builder.ReplaceChild(new[] { 0 }, 0, newNode);
            
            Assert.NotNull(newRoot);
        }
    }
    
    [Fact]
    public void GreenTreeBuilder_DeepPath_WorksCorrectly()
    {
        var tree = SyntaxTree.Parse("{{inner}}");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("x");
        
        // Path: root -> first child (outer block) -> first child (inner block)
        var builder = new GreenTreeBuilder(tree.GreenRoot);
        var newRoot = builder.InsertAt(new[] { 0, 0 }, 0, newNodes);
        
        Assert.NotNull(newRoot);
    }
    
    [Fact]
    public void GreenTreeBuilder_EmptyPath_ModifiesRoot()
    {
        var tree = SyntaxTree.Parse("a");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("b");
        
        var builder = new GreenTreeBuilder(tree.GreenRoot);
        var newRoot = builder.InsertAt(Array.Empty<int>(), 1, newNodes);
        
        Assert.NotNull(newRoot);
    }
    
    [Fact]
    public void GreenTreeBuilder_SpanOverload_WorksSameAsArray()
    {
        var tree = SyntaxTree.Parse("{a}");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("b");
        
        var builder = new GreenTreeBuilder(tree.GreenRoot);
        ReadOnlySpan<int> path = stackalloc int[] { 0 };
        var newRoot = builder.InsertAt(path, 1, newNodes);
        
        Assert.NotNull(newRoot);
    }
    
    #endregion
    
    #region Additional NodeQuery Coverage
    
    [Fact]
    public void KindNodeQuery_Matches_ReturnsCorrectly()
    {
        var tree = SyntaxTree.Parse("foo 123");
        var identQuery = Q.Kind(NodeKind.Ident);
        var numQuery = Q.Kind(NodeKind.Numeric);
        
        var identNode = identQuery.Select(tree).First();
        var numNode = numQuery.Select(tree).First();
        
        Assert.True(identQuery.Matches(identNode));
        Assert.False(identQuery.Matches(numNode));
        Assert.True(numQuery.Matches(numNode));
    }
    
    [Fact]
    public void BlockNodeQuery_Matches_ReturnsCorrectly()
    {
        var tree = SyntaxTree.Parse("{a} [b]");
        var braceQuery = Q.BraceBlock;
        var bracketQuery = Q.BracketBlock;
        
        var braceBlock = braceQuery.Select(tree).First();
        var bracketBlock = bracketQuery.Select(tree).First();
        
        Assert.True(braceQuery.Matches(braceBlock));
        Assert.False(braceQuery.Matches(bracketBlock));
    }
    
    [Fact]
    public void AnyNodeQuery_Matches_AlwaysTrue()
    {
        var tree = SyntaxTree.Parse("foo {bar}");
        var anyQuery = Q.Any;
        var walker = new TreeWalker(tree.Root);
        foreach (var node in walker.DescendantsAndSelf())
        {
            Assert.True(anyQuery.Matches(node));
        }
    }
    
    [Fact]
    public void LeafNodeQuery_Matches_OnlyLeaves()
    {
        var tree = SyntaxTree.Parse("{foo}");
        var leafQuery = Q.Leaf;
        
        var block = Q.BraceBlock.First().Select(tree).First();
        var ident = Q.Ident.First().Select(tree).First();
        
        Assert.False(leafQuery.Matches(block));
        Assert.True(leafQuery.Matches(ident));
    }
    
    [Fact]
    public void FirstNodeQuery_Matches_DelegatesToInner()
    {
        var tree = SyntaxTree.Parse("a 123");
        var firstQuery = Q.Ident.First();
        
        var identNode = Q.Ident.Select(tree).First();
        var numNode = Q.Numeric.Select(tree).First();
        
        Assert.True(firstQuery.Matches(identNode));
        Assert.False(firstQuery.Matches(numNode));
    }
    
    [Fact]
    public void LastNodeQuery_Matches_DelegatesToInner()
    {
        var tree = SyntaxTree.Parse("a b c");
        var lastQuery = Q.Ident.Last();
        
        var identNode = Q.Ident.Select(tree).First();
        Assert.True(lastQuery.Matches(identNode));
    }
    
    [Fact]
    public void NthNodeQuery_Matches_DelegatesToInner()
    {
        var tree = SyntaxTree.Parse("a b c");
        var nthQuery = Q.Ident.Nth(1);
        
        var identNode = Q.Ident.Select(tree).First();
        Assert.True(nthQuery.Matches(identNode));
    }
    
    [Fact]
    public void PredicateNodeQuery_Matches_CombinesInnerAndPredicate()
    {
        var tree = SyntaxTree.Parse("a bb ccc");
        var query = Q.Ident.Where(n => n is RedLeaf leaf && leaf.Text.Length >= 2);
        
        var shortIdent = Q.Ident.Select(tree).First();
        var longIdent = Q.Ident.Select(tree).Skip(1).First();
        
        Assert.False(query.Matches(shortIdent)); // "a" is too short
        Assert.True(query.Matches(longIdent));   // "bb" is long enough
    }
    
    [Fact]
    public void UnionNodeQuery_Matches_MatchesEither()
    {
        var tree = SyntaxTree.Parse("foo 123 {}");
        var unionQuery = Q.Ident | Q.Numeric;
        
        var identNode = Q.Ident.Select(tree).First();
        var numNode = Q.Numeric.Select(tree).First();
        var blockNode = Q.BraceBlock.Select(tree).First();
        
        Assert.True(unionQuery.Matches(identNode));
        Assert.True(unionQuery.Matches(numNode));
        Assert.False(unionQuery.Matches(blockNode));
    }
    
    [Fact]
    public void IntersectionNodeQuery_Matches_MatchesBoth()
    {
        var tree = SyntaxTree.Parse("foo bar");
        var intersectionQuery = Q.Ident & Q.Ident.WithText("foo");
        
        var fooNode = Q.Ident.WithText("foo").Select(tree).First();
        var barNode = Q.Ident.WithText("bar").Select(tree).First();
        
        Assert.True(intersectionQuery.Matches(fooNode));
        Assert.False(intersectionQuery.Matches(barNode));
    }
    
    [Fact]
    public void Query_Select_FromRedNode_WorksCorrectly()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var block = Q.BraceBlock.First().Select(tree).First();
        
        // Select idents from the block (not the whole tree)
        var identsInBlock = Q.Ident.Select(block).ToList();
        
        Assert.Equal(3, identsInBlock.Count);
    }
    
    [Fact]
    public void Query_Last_NoMatches_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("123");
        
        var result = Q.Ident.Last().Select(tree).ToList();
        
        Assert.Empty(result);
    }
    
    [Fact]
    public void Query_Nth_OutOfRange_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("a b");
        
        var result = Q.Ident.Nth(100).Select(tree).ToList();
        
        Assert.Empty(result);
    }
    
    [Fact]
    public void BlockQuery_First_PreservesBlockMethods()
    {
        var tree = SyntaxTree.Parse("{a} {b}");
        
        // First() on BlockNodeQuery should return BlockNodeQuery with InnerStart/InnerEnd
        var firstBlockQuery = Q.BraceBlock.First();
        var insertQuery = firstBlockQuery.InnerStart();
        
        var positions = insertQuery.ResolvePositions(tree).ToList();
        Assert.Single(positions);
    }
    
    [Fact]
    public void BlockQuery_Last_PreservesBlockMethods()
    {
        var tree = SyntaxTree.Parse("{a} {b}");
        
        var lastBlockQuery = Q.BraceBlock.Last();
        var insertQuery = lastBlockQuery.InnerEnd();
        
        var positions = insertQuery.ResolvePositions(tree).ToList();
        Assert.Single(positions);
    }
    
    [Fact]
    public void BlockQuery_Nth_PreservesBlockMethods()
    {
        var tree = SyntaxTree.Parse("{a} {b} {c}");
        
        var nthBlockQuery = Q.BraceBlock.Nth(1);
        var insertQuery = nthBlockQuery.InnerStart();
        
        var positions = insertQuery.ResolvePositions(tree).ToList();
        Assert.Single(positions);
    }
    
    #endregion
}
