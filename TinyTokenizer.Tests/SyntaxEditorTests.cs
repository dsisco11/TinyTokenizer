using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the SyntaxEditor fluent editing API.
/// </summary>
[Trait("Category", "Editor")]
public class SyntaxEditorTests
{
    #region Replace Operations

    [Fact]
    public void Replace_SingleNode_ChangesContent()
    {
        var tree = SyntaxTree.Parse("foo");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), "bar")
            .Commit();
        
        Assert.Equal("bar", tree.ToText());
    }

    [Fact]
    public void Replace_WithTransformer_AppliesTransformation()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), n => ((SyntaxToken)n).Text.ToUpper())
            .Commit();
        
        Assert.Equal("HELLO", tree.ToText());
    }

    [Fact]
    public void Replace_MultipleNodes_ChangesAll()
    {
        var tree = SyntaxTree.Parse("a b a");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("a"), "X")
            .Commit();
        
        Assert.Equal("X b X", tree.ToText());
    }

    [Fact]
    public void Replace_WithGreenNodes_UsesProvidedNodes()
    {
        var tree = SyntaxTree.Parse("old");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("new");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), newNodes)
            .Commit();
        
        Assert.Equal("new", tree.ToText());
    }

    [Fact]
    public void Replace_NoMatches_LeavesTreeUnchanged()
    {
        var tree = SyntaxTree.Parse("foo bar");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("nonexistent"), "replacement")
            .Commit();
        
        Assert.Equal("foo bar", tree.ToText());
    }

    [Fact]
    public void Replace_WithTransformer_ReceivesCorrectNode()
    {
        var tree = SyntaxTree.Parse("test");
        string? capturedText = null;
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), n =>
            {
                capturedText = ((SyntaxToken)n).Text;
                return "replaced";
            })
            .Commit();
        
        Assert.Equal("test", capturedText);
        Assert.Equal("replaced", tree.ToText());
    }

    [Fact]
    public void Replace_Block_ReplacesEntireBlock()
    {
        var tree = SyntaxTree.Parse("{old}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First(), "{new}")
            .Commit();
        
        Assert.Equal("{new}", tree.ToText());
    }

    #endregion

    #region Remove Operations

    [Fact]
    public void Remove_SingleNode_DeletesNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.WithText("b"))
            .Commit();
        
        var text = tree.ToText();
        Assert.DoesNotContain("b", text);
    }

    [Fact]
    public void Remove_MultipleNodes_DeletesAll()
    {
        var tree = SyntaxTree.Parse("a b a c a");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.WithText("a"))
            .Commit();
        
        var text = tree.ToText();
        Assert.DoesNotContain("a", text);
        Assert.Contains("b", text);
        Assert.Contains("c", text);
    }

    [Fact]
    public void Remove_NoMatches_LeavesTreeUnchanged()
    {
        var tree = SyntaxTree.Parse("foo bar");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.WithText("nonexistent"))
            .Commit();
        
        Assert.Equal("foo bar", tree.ToText());
    }

    [Fact]
    public void Remove_Block_RemovesEntireBlock()
    {
        var tree = SyntaxTree.Parse("before {content} after");
        
        tree.CreateEditor()
            .Remove(Q.BraceBlock.First())
            .Commit();
        
        var text = tree.ToText();
        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("content", text);
    }

    [Fact]
    public void Remove_FirstOfMany_RemovesOnlyFirst()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.First())
            .Commit();
        
        var text = tree.ToText();
        Assert.DoesNotContain("a", text);
        Assert.Contains("b", text);
        Assert.Contains("c", text);
    }

    #endregion

    #region Insert Operations

    [Fact]
    public void Insert_Before_InsertsBeforeNode()
    {
        var tree = SyntaxTree.Parse("world");
        
        tree.CreateEditor()
            .InsertBefore(Q.AnyIdent.First(), "hello ")
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }

    [Fact]
    public void Insert_After_InsertsAfterNode()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .InsertAfter(Q.AnyIdent.First(), " world")
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }

    [Fact]
    public void Insert_InnerStart_InsertsAtBlockStart()
    {
        var tree = SyntaxTree.Parse("{b}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "a ")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToText());
    }

    [Fact]
    public void Insert_InnerEnd_InsertsAtBlockEnd()
    {
        var tree = SyntaxTree.Parse("{a}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First().End(), " b")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToText());
    }

    [Fact]
    public void Insert_WithGreenNodes_InsertsProvidedNodes()
    {
        var tree = SyntaxTree.Parse("x");
        
        // The new API uses string parsing instead of pre-built GreenNodes
        tree.CreateEditor()
            .InsertAfter(Q.AnyIdent.First(), " inserted")
            .Commit();
        
        Assert.Equal("x inserted", tree.ToText());
    }

    [Fact]
    public void Insert_BeforeMultiple_InsertsBeforeEach()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .InsertBefore(Q.AnyIdent, "_")
            .Commit();
        
        // With leading trivia transfer: inserted content takes target's leading trivia
        // " b" has leading space, so "_" takes the space, becoming " _b"
        var text = tree.ToText();
        Assert.Contains("_a", text);  // Before 'a' (no leading trivia)
        Assert.Contains("_b", text);  // '_' took the space from 'b', so it's now " _b" 
        Assert.Contains("_c", text);  // Same for 'c'
        Assert.Equal("_a _b _c", text);  // Full result
    }

    [Fact]
    public void Insert_IntoEmptyBlock_AddsContent()
    {
        var tree = SyntaxTree.Parse("{}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "content")
            .Commit();
        
        Assert.Equal("{content}", tree.ToText());
    }

    #endregion

    #region Pending Edits Tracking

    [Fact]
    public void PendingEditCount_StartsAtZero()
    {
        var tree = SyntaxTree.Parse("test");
        var editor = tree.CreateEditor();
        
        Assert.Equal(0, editor.PendingEditCount);
        Assert.False(editor.HasPendingEdits);
    }

    [Fact]
    public void PendingEditCount_IncreasesWithEdits()
    {
        var tree = SyntaxTree.Parse("a b c");
        var editor = tree.CreateEditor();
        
        editor.Remove(Q.AnyIdent.First());
        Assert.Equal(1, editor.PendingEditCount);
        Assert.True(editor.HasPendingEdits);
        
        editor.Remove(Q.AnyIdent.First());
        Assert.Equal(2, editor.PendingEditCount);
    }

    [Fact]
    public void PendingEditCount_ResetsAfterCommit()
    {
        var tree = SyntaxTree.Parse("a b");
        var editor = tree.CreateEditor();
        
        editor.Remove(Q.AnyIdent.First());
        Assert.True(editor.HasPendingEdits);
        
        editor.Commit();
        Assert.Equal(0, editor.PendingEditCount);
        Assert.False(editor.HasPendingEdits);
    }

    [Fact]
    public void PendingEditCount_ResetsAfterRollback()
    {
        var tree = SyntaxTree.Parse("a b");
        var editor = tree.CreateEditor();
        
        editor.Remove(Q.AnyIdent.First());
        Assert.True(editor.HasPendingEdits);
        
        editor.Rollback();
        Assert.Equal(0, editor.PendingEditCount);
        Assert.False(editor.HasPendingEdits);
    }

    #endregion

    #region Rollback

    [Fact]
    public void Rollback_DiscardsAllPendingEdits()
    {
        var tree = SyntaxTree.Parse("original");
        
        var editor = tree.CreateEditor();
        editor.Replace(Q.AnyIdent.First(), "changed");
        editor.Rollback();
        
        Assert.Equal("original", tree.ToText());
    }

    [Fact]
    public void Rollback_AllowsNewEdits()
    {
        var tree = SyntaxTree.Parse("original");
        
        var editor = tree.CreateEditor();
        editor.Replace(Q.AnyIdent.First(), "discarded");
        editor.Rollback();
        
        editor.Replace(Q.AnyIdent.First(), "new");
        editor.Commit();
        
        Assert.Equal("new", tree.ToText());
    }

    #endregion

    #region Commit and Undo Integration

    [Fact]
    public void Commit_SupportsUndo()
    {
        var tree = SyntaxTree.Parse("original");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), "changed")
            .Commit();
        
        Assert.Equal("changed", tree.ToText());
        Assert.True(tree.CanUndo);
        
        tree.Undo();
        Assert.Equal("original", tree.ToText());
    }

    [Fact]
    public void Commit_WithNoEdits_DoesNotAddUndoHistory()
    {
        var tree = SyntaxTree.Parse("unchanged");
        
        tree.CreateEditor().Commit();
        
        Assert.Equal("unchanged", tree.ToText());
        Assert.False(tree.CanUndo);
    }

    [Fact]
    public void Commit_SupportsRedo()
    {
        var tree = SyntaxTree.Parse("original");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), "changed")
            .Commit();
        
        tree.Undo();
        Assert.Equal("original", tree.ToText());
        Assert.True(tree.CanRedo);
        
        tree.Redo();
        Assert.Equal("changed", tree.ToText());
    }

    [Fact]
    public void MultipleCommits_CreateSeparateUndoSteps()
    {
        var tree = SyntaxTree.Parse("a");
        
        tree.CreateEditor().Replace(Q.AnyIdent.First(), "b").Commit();
        tree.CreateEditor().Replace(Q.AnyIdent.First(), "c").Commit();
        
        Assert.Equal("c", tree.ToText());
        
        tree.Undo();
        Assert.Equal("b", tree.ToText());
        
        tree.Undo();
        Assert.Equal("a", tree.ToText());
    }

    #endregion

    #region Batched Operations

    [Fact]
    public void BatchedOperations_ApplyAtomically()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("a"), "X")
            .Replace(Q.AnyIdent.WithText("b"), "Y")
            .Replace(Q.AnyIdent.WithText("c"), "Z")
            .Commit();
        
        Assert.Equal("X Y Z", tree.ToText());
    }

    [Fact]
    public void BatchedOperations_SingleUndoStep()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("a"), "X")
            .Replace(Q.AnyIdent.WithText("b"), "Y")
            .Replace(Q.AnyIdent.WithText("c"), "Z")
            .Commit();
        
        // All three changes should undo in one step
        tree.Undo();
        Assert.Equal("a b c", tree.ToText());
    }

    [Fact]
    public void MixedOperations_ApplyCorrectly()
    {
        var tree = SyntaxTree.Parse("a {b} c");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.WithText("a"))
            .InsertBefore(Q.BraceBlock.First().End(), " extra")
            .Replace(Q.AnyIdent.WithText("c"), "z")
            .Commit();
        
        var text = tree.ToText();
        Assert.DoesNotContain("a", text.Split(' ').First()); // 'a' removed from start
        Assert.Contains("extra", text);
        Assert.Contains("z", text);
    }

    #endregion

    #region Fluent API

    [Fact]
    public void FluentApi_ReturnsSameEditor()
    {
        var tree = SyntaxTree.Parse("test");
        var editor = tree.CreateEditor();
        
        var result1 = editor.Replace(Q.AnyIdent.First(), "a");
        var result2 = result1.Remove(Q.AnyIdent.Last());
        var result3 = result2.InsertBefore(Q.AnyIdent.First(), "b");
        
        Assert.Same(editor, result1);
        Assert.Same(editor, result2);
        Assert.Same(editor, result3);
    }

    [Fact]
    public void FluentApi_FullChain()
    {
        var tree = SyntaxTree.Parse("{x}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "start ")
            .InsertBefore(Q.BraceBlock.First().End(), " end")
            .Replace(Q.AnyIdent.WithText("x"), "middle")
            .Commit();
        
        // Note: The order of operations may affect result
        var text = tree.ToText();
        Assert.Contains("start", text);
        Assert.Contains("end", text);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Edit_EmptyTree_HandlesGracefully()
    {
        var tree = SyntaxTree.Parse("");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.First())
            .Commit();
        
        Assert.Equal("", tree.ToText());
    }

    [Fact]
    public void Edit_DeeplyNested_WorksCorrectly()
    {
        var tree = SyntaxTree.Parse("{{{deep}}}");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("deep"), "replaced")
            .Commit();
        
        Assert.Equal("{{{replaced}}}", tree.ToText());
    }

    [Fact]
    public void Edit_WithCustomOptions_UsesOptions()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        var tree = SyntaxTree.Parse("a + b", options);
        
        tree.CreateEditor(options)
            .Replace(Q.AnyOperator.First(), "-")
            .Commit();
        
        Assert.Equal("a - b", tree.ToText());
    }

    [Fact]
    public void Insert_AdjacentPositions_HandlesCorrectly()
    {
        var tree = SyntaxTree.Parse("x");
        
        tree.CreateEditor()
            .InsertBefore(Q.AnyIdent.First(), "A")
            .InsertAfter(Q.AnyIdent.First(), "B")
            .Commit();
        
        Assert.Equal("AxB", tree.ToText());
    }

    [Fact]
    public void Remove_AllNodes_ResultsInEmptyTree()
    {
        var tree = SyntaxTree.Parse("a");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.First())
            .Commit();
        
        Assert.Equal("", tree.ToText());
    }

    #endregion

    #region Query Integration

    [Fact]
    public void Edit_WithUnionQuery_AffectsBothTypes()
    {
        var tree = SyntaxTree.Parse("foo 42 bar");
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent | Q.AnyNumeric)
            .Commit();
        
        // All idents and numbers removed, only whitespace remains
        var text = tree.ToText().Trim();
        Assert.DoesNotContain("foo", text);
        Assert.DoesNotContain("42", text);
        Assert.DoesNotContain("bar", text);
    }

    [Fact]
    public void Edit_WithFilteredQuery_OnlyAffectsMatches()
    {
        var tree = SyntaxTree.Parse("short verylongword tiny");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.Where(n => n is SyntaxToken l && l.Text.Length > 5), "X")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("short", text);
        Assert.Contains("X", text);
        Assert.Contains("tiny", text);
        Assert.DoesNotContain("verylongword", text);
    }

    [Fact]
    public void Edit_WithNthQuery_OnlyAffectsNth()
    {
        var tree = SyntaxTree.Parse("a b c d e");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.Nth(2), "X")
            .Commit();
        
        Assert.Equal("a b X d e", tree.ToText());
    }

    #endregion

    #region Block-Specific Operations

    [Fact]
    public void Insert_BracketBlock_InnerStart()
    {
        var tree = SyntaxTree.Parse("[b]");
        
        tree.CreateEditor()
            .InsertAfter(Q.BracketBlock.First().Start(), "a ")
            .Commit();
        
        Assert.Equal("[a b]", tree.ToText());
    }

    [Fact]
    public void Insert_ParenBlock_InnerEnd()
    {
        var tree = SyntaxTree.Parse("(a)");
        
        tree.CreateEditor()
            .InsertBefore(Q.ParenBlock.First().End(), " b")
            .Commit();
        
        Assert.Equal("(a b)", tree.ToText());
    }

    #endregion

    #region Inner() Query Tests
    
    [Fact]
    public void Inner_Select_ReturnsInnerChildren()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        
        var inner = tree.Select(Q.BraceBlock.First().Inner()).ToList();
        
        // "a", "b", "c" (whitespace is trivia attached to tokens)
        Assert.Equal(3, inner.Count);
    }
    
    [Fact]
    public void Inner_Replace_ReplacesContentPreservingDelimiters()
    {
        var tree = SyntaxTree.Parse("{old content}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), "new")
            .Commit();
        
        Assert.Equal("{new}", tree.ToText());
    }
    
    [Fact]
    public void Inner_Replace_EmptyBlock_InsertsContent()
    {
        var tree = SyntaxTree.Parse("{}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), "inserted")
            .Commit();
        
        Assert.Equal("{inserted}", tree.ToText());
    }
    
    [Fact]
    public void Inner_Replace_WithWhitespace()
    {
        var tree = SyntaxTree.Parse("{ old }");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), " new ")
            .Commit();
        
        // Original trivia (" ") + new content (" new ") + original trailing trivia (" ")
        // But trivia from "old" is transferred: leading space + content + trailing space
        Assert.Equal("{  new  }", tree.ToText());
    }
    
    [Fact]
    public void Inner_Replace_MultipleBlocks()
    {
        var tree = SyntaxTree.Parse("{a} {b}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.Inner(), "X")
            .Commit();
        
        Assert.Equal("{X} {X}", tree.ToText());
    }
    
    [Fact]
    public void Inner_Remove_ClearsBlockContent()
    {
        var tree = SyntaxTree.Parse("{content}");
        
        tree.CreateEditor()
            .Remove(Q.BraceBlock.First().Inner())
            .Commit();
        
        Assert.Equal("{}", tree.ToText());
    }
    
    [Fact]
    public void Inner_Remove_EmptyBlock_NoChange()
    {
        var tree = SyntaxTree.Parse("{}");
        
        tree.CreateEditor()
            .Remove(Q.BraceBlock.First().Inner())
            .Commit();
        
        Assert.Equal("{}", tree.ToText());
    }
    
    [Fact]
    public void Inner_InsertBefore_InsertsAtStart()
    {
        var tree = SyntaxTree.Parse("{existing}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First().Inner(), "prefix ")
            .Commit();
        
        Assert.Equal("{prefix existing}", tree.ToText());
    }
    
    [Fact]
    public void Inner_InsertAfter_InsertsAtEnd()
    {
        var tree = SyntaxTree.Parse("{existing}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Inner(), " suffix")
            .Commit();
        
        Assert.Equal("{existing suffix}", tree.ToText());
    }
    
    [Fact]
    public void Inner_InsertBefore_EmptyBlock_InsertsContent()
    {
        var tree = SyntaxTree.Parse("{}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First().Inner(), "new")
            .Commit();
        
        Assert.Equal("{new}", tree.ToText());
    }
    
    [Fact]
    public void Inner_InsertAfter_EmptyBlock_InsertsContent()
    {
        var tree = SyntaxTree.Parse("{}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Inner(), "new")
            .Commit();
        
        Assert.Equal("{new}", tree.ToText());
    }
    
    [Fact]
    public void Inner_BracketBlock_Works()
    {
        var tree = SyntaxTree.Parse("[old]");
        
        tree.CreateEditor()
            .Replace(Q.BracketBlock.First().Inner(), "new")
            .Commit();
        
        Assert.Equal("[new]", tree.ToText());
    }
    
    [Fact]
    public void Inner_ParenBlock_Works()
    {
        var tree = SyntaxTree.Parse("(old)");
        
        tree.CreateEditor()
            .Replace(Q.ParenBlock.First().Inner(), "new")
            .Commit();
        
        Assert.Equal("(new)", tree.ToText());
    }
    
    [Fact]
    public void Inner_NestedBlocks_WorksOnOuter()
    {
        var tree = SyntaxTree.Parse("{outer {inner}}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), "replaced")
            .Commit();
        
        Assert.Equal("{replaced}", tree.ToText());
    }
    
    [Fact]
    public void Inner_NestedBlocks_WorksOnInner()
    {
        var tree = SyntaxTree.Parse("{outer {inner}}");
        
        // Get the inner brace block (second one)
        var innerBlock = tree.Select(Q.BraceBlock).Skip(1).First();
        
        tree.CreateEditor()
            .Replace(innerBlock, "{replaced}")
            .Commit();
        
        Assert.Equal("{outer {replaced}}", tree.ToText());
    }
    
    [Fact]
    public void Inner_WithFirst_SelectsFirstBlockInner()
    {
        var tree = SyntaxTree.Parse("{a} {b}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), "X")
            .Commit();
        
        Assert.Equal("{X} {b}", tree.ToText());
    }
    
    [Fact]
    public void Inner_WithLast_SelectsLastBlockInner()
    {
        var tree = SyntaxTree.Parse("{a} {b}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.Last().Inner(), "X")
            .Commit();
        
        Assert.Equal("{a} {X}", tree.ToText());
    }
    
    [Fact]
    public void Inner_WithPredicate_SelectsMatchingBlockInner()
    {
        var tree = SyntaxTree.Parse("{short} {longer content}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.Where(b => b.Width > 10).First().Inner(), "X")
            .Commit();
        
        Assert.Equal("{short} {X}", tree.ToText());
    }

    #endregion

    #region NamedBlockQuery.Inner() Tests

    private static Schema CreateBlockContainerTestSchema()
    {
        return Schema.Create()
            .DefineSyntax(Syntax.Define<TestBlockContainerSyntax>("testBlockContainer")
                .Match(Query.AnyIdent, Query.BraceBlock)
                .Build())
            .Build();
    }

    private sealed class TestBlockContainerSyntax : SyntaxNode, IBlockContainerNode
    {
        internal TestBlockContainerSyntax(CreationContext context)
            : base(context)
        {
        }

        public SyntaxToken NameNode => GetTypedChild<SyntaxToken>(0);
        public SyntaxBlock Body => GetTypedChild<SyntaxBlock>(1);

        public IReadOnlyList<string> BlockNames => ["body"];

        public SyntaxBlock GetBlock(string? name = null) => name switch
        {
            null or "body" => Body,
            _ => throw new ArgumentException($"Unknown block: {name}")
        };
    }

    [Fact]
    public void NamedBlockInner_Replace_ReplacesContentPreservingDelimiters()
    {
        var schema = CreateBlockContainerTestSchema();
        var tree = SyntaxTree.Parse("f{old}", schema);

        tree.CreateEditor()
            .Replace(Query.Syntax<TestBlockContainerSyntax>().Block("body").Inner(), "new")
            .Commit();

        Assert.Equal("f{new}", tree.ToText());
    }

    [Fact]
    public void NamedBlockInner_Replace_EmptyBlock_InsertsContent()
    {
        var schema = CreateBlockContainerTestSchema();
        var tree = SyntaxTree.Parse("f{}", schema);

        tree.CreateEditor()
            .Replace(Query.Syntax<TestBlockContainerSyntax>().Block("body").Inner(), "inserted")
            .Commit();

        Assert.Equal("f{inserted}", tree.ToText());
    }

    #endregion

    #region Function-Like Block Insertion Scenarios

    [Fact]
    public void Insert_BeforeFunctionBlock_InsertsBeforeOpeningBrace()
    {
        // Simulates: inserting a comment or decorator before a function block
        // With trivia transfer: inserted content takes target's leading trivia (space before {)
        var tree = SyntaxTree.Parse("function {body}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First(), "/* comment */")
            .Commit();
        
        // /* comment */ takes the space from {, so result is: "function /* comment */{body}"
        var text = tree.ToText();
        Assert.Equal("function /* comment */{body}", text);
    }

    [Fact]
    public void Insert_AtFunctionStart_InsertsAfterOpeningBrace()
    {
        // Simulates: inserting a statement at the top of a function body
        var tree = SyntaxTree.Parse("function {existing}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "first; ")
            .Commit();
        
        Assert.Equal("function {first; existing}", tree.ToText());
    }

    [Fact]
    public void Insert_AtFunctionEnd_InsertsBeforeClosingBrace()
    {
        // Simulates: inserting a return statement at the end of a function body
        var tree = SyntaxTree.Parse("function {existing}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First().End(), " return")
            .Commit();
        
        Assert.Equal("function {existing return}", tree.ToText());
    }

    [Fact]
    public void Insert_AfterFunctionBlock_InsertsAfterClosingBrace()
    {
        // Simulates: inserting code after a function definition
        var tree = SyntaxTree.Parse("function {body}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First(), " nextFunction")
            .Commit();
        
        Assert.Equal("function {body} nextFunction", tree.ToText());
    }

    [Fact]
    public void Insert_MultipleFunctionPositions_AllInsertCorrectly()
    {
        // Simulates: inserting at multiple positions in a single commit
        var tree = SyntaxTree.Parse("fn {body}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First(), "/* before */ ")
            .InsertAfter(Q.BraceBlock.First().Start(), "start; ")
            .InsertBefore(Q.BraceBlock.First().End(), " end;")
            .InsertAfter(Q.BraceBlock.First(), " /* after */")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("/* before */", text);
        Assert.Contains("{start;", text);
        Assert.Contains("end;}", text);
        Assert.Contains("} /* after */", text);
    }

    [Fact]
    public void Insert_NestedFunctionBlocks_InsertsAtCorrectLevel()
    {
        // Simulates: a function with an inner block (like if/while)
        var tree = SyntaxTree.Parse("function {outer {inner}}");
        
        // Insert at the outer function's start
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "first; ")
            .Commit();
        
        var text = tree.ToText();
        Assert.Equal("function {first; outer {inner}}", text);
    }

    [Fact]
    public void Insert_InnerBlockStart_InsertsInNestedBlock()
    {
        // Simulates: inserting inside an inner block (like inside an if statement)
        var tree = SyntaxTree.Parse("function {outer {inner}}");
        
        // Find the inner block (second brace block)
        var innerBlocks = Q.BraceBlock.Select(tree).ToList();
        Assert.Equal(2, innerBlocks.Count);
        
        // Use Nth(1) to get the second (inner) block
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.Nth(1).Start(), "nested; ")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("{nested; inner}", text);
    }

    [Fact]
    public void Insert_BeforeAndAfterMultipleFunctions_HandlesCorrectly()
    {
        // Simulates: inserting around multiple function definitions
        // Block trailing trivia (space after closer) is kept with the block, not attached to inner content
        var tree = SyntaxTree.Parse("{first} {second}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock, "/* fn */")
            .Commit();
        
        var text = tree.ToText();
        // First block: "{first} " (space is block's trailing trivia)
        // Second block: "{second}" (no leading trivia)
        Assert.Equal("/* fn */{first} /* fn */{second}", text);
    }

    [Fact]
    public void Insert_EmptyFunctionBody_InsertsCorrectly()
    {
        // Simulates: adding content to an empty function body
        var tree = SyntaxTree.Parse("function {}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "statement;")
            .Commit();
        
        Assert.Equal("function {statement;}", tree.ToText());
    }

    [Fact]
    public void Insert_FunctionWithWhitespace_PreservesFormatting()
    {
        // Test that whitespace/trivia is preserved correctly
        // With the new GreenBlock design, opener's trailing trivia (space after {) is preserved
        var tree = SyntaxTree.Parse("fn { body }");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "new; ")
            .Commit();
        
        var text = tree.ToText();
        // The space after { is now preserved as OpenerNode.TrailingTrivia
        Assert.Contains("{ new;", text);
        Assert.Contains("body", text);
    }

    [Fact]
    public void Insert_BeforeFirstBlock_InsertsAtDocumentStart()
    {
        var tree = SyntaxTree.Parse("{only}");
        
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.First(), "prefix ")
            .Commit();
        
        Assert.Equal("prefix {only}", tree.ToText());
    }

    [Fact]
    public void Insert_AfterLastBlock_InsertsAtDocumentEnd()
    {
        var tree = SyntaxTree.Parse("{only}");
        
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First(), " suffix")
            .Commit();
        
        Assert.Equal("{only} suffix", tree.ToText());
    }

    #endregion

    #region Replace Operations for Blocks

    [Fact]
    public void Replace_NestedBlock_PreservesOuter()
    {
        // Simpler case: replace inner content of a block
        var tree = SyntaxTree.Parse("{a}");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("a"), "replaced")
            .Commit();
        
        Assert.Equal("{replaced}", tree.ToText());
    }
    
    [Fact]
    public void Replace_DeepNestedBlock_PreservesOuter()
    {
        var tree = SyntaxTree.Parse("{outer {inner}}");
        
        // Find the inner block
        var allBlocks = Q.BraceBlock.Select(tree).ToList();
        Assert.Equal(2, allBlocks.Count);
        
        // Replace just the identifier inside the inner block
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("inner"), "replaced")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("outer", text);
        Assert.Contains("replaced", text);
        Assert.DoesNotContain("inner", text);
    }

    #endregion

    #region CreateEditor Factory

    [Fact]
    public void CreateEditor_WithoutOptions_UsesDefaultOptions()
    {
        var tree = SyntaxTree.Parse("test");
        var editor = tree.CreateEditor();
        
        Assert.NotNull(editor);
        Assert.Equal(0, editor.PendingEditCount);
    }

    [Fact]
    public void CreateEditor_WithOptions_AcceptsOptions()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        var tree = SyntaxTree.Parse("test", options);
        var editor = tree.CreateEditor(options);
        
        Assert.NotNull(editor);
    }

    [Fact]
    public void CreateEditor_MultipleTimes_IndependentEditors()
    {
        var tree = SyntaxTree.Parse("test");
        
        var editor1 = tree.CreateEditor();
        var editor2 = tree.CreateEditor();
        
        editor1.Replace(Q.AnyIdent.First(), "a");
        
        Assert.Equal(1, editor1.PendingEditCount);
        Assert.Equal(0, editor2.PendingEditCount);
    }

    [Fact]
    public void Replace_CommentOutFunctionCall_InMiddleOfMethod()
    {
        // Arrange: a simple sequence with a function call in the middle
        var source = "setup(); doSomething(x, y); cleanup();";
        var tree = SyntaxTree.Parse(source);
        
        // To comment out a function call (ident + paren block), we use two operations:
        // 1. Replace the identifier with the commented version of the full call
        // 2. Remove the arguments block that follows
        var funcNameQuery = Q.Ident("doSomething").FollowedBy(Q.ParenBlock);
        
        // Find the paren block for doSomething - it's the second one (after setup's args)
        var doSomethingArgs = tree.Root.Children
            .OfType<SyntaxBlock>()
            .Skip(1)  // Skip setup()'s args
            .First();
        
        // Act: comment out the function call
        tree.CreateEditor()
            .Replace(funcNameQuery, "/* doSomething(x, y) */")
            .Remove(Q.ParenBlock.Where(b => b.Position == doSomethingArgs.Position))
            .Commit();
        
        // Assert: the function call should now be commented out
        // Note: trivia from the removed block stays with the block (after closer)
        var result = tree.ToText();
        Assert.Equal("setup(); /* doSomething(x, y) */; cleanup();", result);
    }

    [Fact]
    public void Replace_FunctionName_PreservesArguments()
    {
        // This test demonstrates the actual behavior: FollowedBy queries select
        // only the identifier, so Replace only affects the identifier.
        var source = "before(); target(a); after();";
        var tree = SyntaxTree.Parse(source);
        
        // FollowedBy is a lookahead - it matches identifiers followed by paren block
        // but only the identifier is selected/replaced
        var funcNameQuery = Q.Ident("target").FollowedBy(Q.ParenBlock);
        
        // Act: replace only the function name
        tree.CreateEditor()
            .Replace(funcNameQuery, "renamed")
            .Commit();
        
        // Assert: the identifier is replaced but arguments remain
        var result = tree.ToText();
        Assert.Equal("before(); renamed(a); after();", result);
    }

    [Fact]
    public void Replace_SingleIdentifier_UsingTransformer()
    {
        // Arrange: use transformer to wrap identifier in comment
        var source = "log(message);";
        var tree = SyntaxTree.Parse(source);
        
        // Use FollowedBy as lookahead to find function name
        var funcNameQuery = Q.Ident("log").FollowedBy(Q.ParenBlock);
        
        // Act: comment out using transformer to preserve original text
        tree.CreateEditor()
            .Replace(funcNameQuery, node => $"/* {node.ToText()} */")
            .Commit();
        
        // Assert: only the identifier is wrapped, args remain
        var result = tree.ToText();
        Assert.Equal("/* log */(message);", result);
    }

    [Fact]
    public void Replace_AndRemove_CommentOutEntireFunctionCall()
    {
        // This test shows how to fully comment out a function call
        // by combining replace and remove operations
        var source = "before(); log(msg); after();";
        var tree = SyntaxTree.Parse(source);
        
        // Find the function name identifier using FollowedBy for matching
        var funcNameQuery = Q.Ident("log").FollowedBy(Q.ParenBlock);
        
        // Act: replace function name with commented call, then remove args block
        tree.CreateEditor()
            .Replace(funcNameQuery, "/* log(msg) */")
            .Remove(Q.ParenBlock.Nth(1)) // The second paren block is log's args
            .Commit();
        
        // Assert: the call should be commented out
        // Note: trivia from the removed block stays with the block (after closer)
        var result = tree.ToText();
        Assert.Equal("before(); /* log(msg) */; after();", result);
    }

    #endregion
    
    #region Query.Exact and RedNode-based Operations
    
    [Fact]
    public void QueryExact_MatchesSpecificNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        // In this AST, whitespace is trivia attached to tokens, not separate nodes
        // "a b c" has 3 ident children: "a", "b", "c"
        var nodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ToList();
        var targetNode = nodes[1]; // "b"
        
        var matched = Q.Exact(targetNode).Select(tree).ToList();
        
        Assert.Single(matched);
        Assert.Same(targetNode, matched[0]);
    }
    
    [Fact]
    public void QueryExact_DoesNotMatchOtherNodes()
    {
        var tree = SyntaxTree.Parse("a b c");
        var nodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ToList();
        var targetNode = nodes[1]; // "b"
        
        // Query for the exact node, but check against different nodes
        Assert.False(Q.Exact(targetNode).Matches(nodes[0])); // "a"
        Assert.False(Q.Exact(targetNode).Matches(nodes[2])); // "c"
        Assert.True(Q.Exact(targetNode).Matches(targetNode)); // "b"
    }
    
    [Fact]
    public void Replace_RedNode_ReplacesSpecificNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        // Get the "b" identifier (second ident)
        var bNode = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ElementAt(1);
        
        tree.CreateEditor()
            .Replace(bNode, "X")
            .Commit();
        
        Assert.Equal("a X c", tree.ToText());
    }
    
    [Fact]
    public void Replace_RedNode_WithTransformer()
    {
        var tree = SyntaxTree.Parse("hello world");
        // Get "world" (second ident)
        var worldNode = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ElementAt(1);
        
        tree.CreateEditor()
            .Replace(worldNode, n => ((SyntaxToken)n).Text.ToUpper())
            .Commit();
        
        Assert.Equal("hello WORLD", tree.ToText());
    }
    
    [Fact]
    public void Replace_MultipleRedNodes_ReplacesAll()
    {
        var tree = SyntaxTree.Parse("a b c");
        var nodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ToList();
        
        tree.CreateEditor()
            .Replace(nodes, "X")
            .Commit();
        
        Assert.Equal("X X X", tree.ToText());
    }
    
    [Fact]
    public void Replace_MultipleRedNodes_WithTransformer()
    {
        var tree = SyntaxTree.Parse("a b c");
        var nodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ToList();
        
        tree.CreateEditor()
            .Replace(nodes, n => $"[{((SyntaxToken)n).Text}]")
            .Commit();
        
        // When replacing a token, its leading trivia is transferred to the replacement.
        // The replacement text "[x]" doesn't include the space, so the space (leading trivia)
        // goes before the bracket.
        var text = tree.ToText();
        Assert.Contains("[a]", text);
        Assert.Contains("[b]", text);
        Assert.Contains("[c]", text);
    }
    
    [Fact]
    public void Remove_RedNode_RemovesSpecificNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        // Get "b" identifier
        var bNode = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ElementAt(1);
        
        tree.CreateEditor()
            .Remove(bNode)
            .Commit();
        
        var text = tree.ToText();
        // After removal, the trivia (space before b) might remain or be gone depending on trivia attachment
        // The key assertion is that "b" is removed
        Assert.DoesNotContain("b", text.Replace(" ", "").Replace("  ", "")); // ignore whitespace for this test
        Assert.Contains("a", text);
        Assert.Contains("c", text);
    }
    
    [Fact]
    public void Remove_MultipleRedNodes_RemovesAll()
    {
        var tree = SyntaxTree.Parse("a b c");
        var identNodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).Take(2).ToList();
        
        tree.CreateEditor()
            .Remove(identNodes)
            .Commit();
        
        var text = tree.ToText();
        var textWithoutSpaces = text.Replace(" ", "");
        Assert.DoesNotContain("a", textWithoutSpaces);
        Assert.DoesNotContain("b", textWithoutSpaces);
        Assert.Contains("c", textWithoutSpaces);
    }
    
    [Fact]
    public void InsertBefore_RedNode_Text()
    {
        var tree = SyntaxTree.Parse("world");
        var worldNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        
        tree.CreateEditor()
            .InsertBefore(worldNode, "hello ")
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }
    
    [Fact]
    public void InsertAfter_RedNode_Text()
    {
        var tree = SyntaxTree.Parse("hello");
        var helloNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        
        tree.CreateEditor()
            .InsertAfter(helloNode, " world")
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }
    
    [Fact]
    public void InsertBefore_MultipleRedNodes_Text()
    {
        var tree = SyntaxTree.Parse("a b c");
        var identNodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ToList();
        
        tree.CreateEditor()
            .InsertBefore(identNodes, "[")
            .Commit();
        
        // When inserting before a token, the insertion goes before the token's position.
        // The token retains its leading trivia (whitespace), so we get "[]a []b []c".
        var text = tree.ToText();
        Assert.Equal("[]a []b []c", text);
    }
    
    [Fact]
    public void InsertAfter_MultipleRedNodes_Text()
    {
        var tree = SyntaxTree.Parse("a b c");
        var identNodes = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ToList();
        
        tree.CreateEditor()
            .InsertAfter(identNodes, "]")
            .Commit();
        
        // When inserting after a token, the insertion goes after the token's text.
        // Whitespace (leading trivia of next token) comes after our insertion.
        var text = tree.ToText();
        Assert.Equal("a ]b ]c]", text);
    }
    
    /// <summary>
    /// Verifies trivia preservation when inserting after a node.
    /// Trivia model: trailing trivia = consume until newline, leading trivia = remaining unconsumed.
    /// For "a b" (no newline), the space is trailing trivia on 'a'.
    /// </summary>
    [Fact]
    public void InsertAfter_ShouldPreserveTrailingTrivia()
    {
        var tree = SyntaxTree.Parse("a b");
        
        // Verify initial trivia state: space is trailing on 'a' (no newline to stop collection)
        var leaves = tree.Leaves.ToList();
        Assert.Equal(2, leaves.Count);
        Assert.Equal("a", leaves[0].Text);
        Assert.Equal("b", leaves[1].Text);
        Assert.Equal(1, leaves[0].TrailingTriviaWidth); // 'a' has trailing space
        Assert.Equal(0, leaves[1].LeadingTriviaWidth);  // 'b' has no leading trivia
        
        var aNode = tree.Root.Children.First(n => n is SyntaxToken t && t.Text == "a");
        
        tree.CreateEditor()
            .InsertAfter(aNode, "X")
            .Commit();
        
        // InsertAfter inserts after the node INCLUDING its trailing trivia
        var actualText = tree.ToText();
        Assert.Equal("a Xb", actualText);
        
        var leavesAfter = tree.Leaves.ToList();
        Assert.Equal(3, leavesAfter.Count);
        
        var aAfter = leavesAfter.First(l => l.Text == "a");
        var xAfter = leavesAfter.First(l => l.Text == "X");
        var bAfter = leavesAfter.First(l => l.Text == "b");
        
        // 'a' keeps trailing space, 'X' and 'b' have no trivia
        Assert.Equal(1, aAfter.TrailingTriviaWidth);
        Assert.Equal(0, xAfter.LeadingTriviaWidth);
        Assert.Equal(0, xAfter.TrailingTriviaWidth);
        Assert.Equal(0, bAfter.LeadingTriviaWidth);
    }
    
    /// <summary>
    /// Verifies trivia preservation when inserting before a node.
    /// For "a b" (no newline), the space is trailing trivia on 'a', not leading on 'b'.
    /// </summary>
    [Fact]
    public void InsertBefore_ShouldPreserveTrivia()
    {
        var tree = SyntaxTree.Parse("a b");
        
        // Verify initial trivia state
        var leaves = tree.Leaves.ToList();
        Assert.Equal(2, leaves.Count);
        Assert.Equal("a", leaves[0].Text);
        Assert.Equal("b", leaves[1].Text);
        Assert.Equal(1, leaves[0].TrailingTriviaWidth); // 'a' has trailing space
        Assert.Equal(0, leaves[1].LeadingTriviaWidth);  // 'b' has no leading trivia
        
        var bNode = tree.Root.Children.First(n => n is SyntaxToken t && t.Text == "b");
        
        tree.CreateEditor()
            .InsertBefore(bNode, "X")
            .Commit();
        
        var actualText = tree.ToText();
        var leavesAfter = tree.Leaves.ToList();
        Assert.Equal(3, leavesAfter.Count);
        
        var aAfter = leavesAfter.First(l => l.Text == "a");
        var xAfter = leavesAfter.First(l => l.Text == "X");
        var bAfter = leavesAfter.First(l => l.Text == "b");
        
        // InsertBefore inserts before the target (b has no leading trivia to consider)
        Assert.Equal("a Xb", actualText);
        Assert.Equal(1, aAfter.TrailingTriviaWidth);  // 'a' keeps trailing space
        Assert.Equal(0, xAfter.LeadingTriviaWidth);
        Assert.Equal(0, xAfter.TrailingTriviaWidth);
        Assert.Equal(0, bAfter.LeadingTriviaWidth);
    }

    /// <summary>
    /// Tests inserting after a node that has a trailing newline, where the following node has leading trivia.
    /// Trivia model: trailing = up to newline, leading = after newline.
    /// 
    /// Input: "a\n  b" where:
    ///   - 'a' has trailing trivia: "\n"
    ///   - 'b' has leading trivia: "  " (indentation)
    /// 
    /// After InsertAfter(a, " X"):
    ///   - 'a' keeps trailing "\n"
    ///   - 'X' is inserted with its leading space
    ///   - 'b' should retain its leading "  " trivia
    /// </summary>
    [Fact]
    public void InsertAfter_WithNewline_ShouldNotStealLeadingTriviaFromFollowingNode()
    {
        var tree = SyntaxTree.Parse("a\n  b");
        
        // Verify initial trivia state
        var leaves = tree.Leaves.ToList();
        Assert.Equal(2, leaves.Count);
        Assert.Equal("a", leaves[0].Text);
        Assert.Equal("b", leaves[1].Text);
        Assert.Equal(1, leaves[0].TrailingTriviaWidth); // 'a' has trailing newline
        Assert.Equal(2, leaves[1].LeadingTriviaWidth);  // 'b' has leading "  " (indentation after newline)
        
        var aNode = tree.Root.Children.First(n => n is SyntaxToken t && t.Text == "a");
        
        tree.CreateEditor()
            .InsertAfter(aNode, " X")
            .Commit();
        
        var actualText = tree.ToText();
        var leavesAfter = tree.Leaves.ToList();
        Assert.Equal(3, leavesAfter.Count);
        
        var aAfter = leavesAfter.First(l => l.Text == "a");
        var xAfter = leavesAfter.First(l => l.Text == "X");
        var bAfter = leavesAfter.First(l => l.Text == "b");
        
        // 'a' keeps its trailing newline
        // ' X' is parsed as X with leading space trivia
        // 'b' should still have its leading "  " trivia (not stolen by X)
        Assert.Equal("a\n X  b", actualText);
        Assert.Equal(1, aAfter.TrailingTriviaWidth);   // 'a' keeps trailing newline
        Assert.Equal(1, xAfter.LeadingTriviaWidth);    // X has leading space from " X"
        Assert.Equal(0, xAfter.TrailingTriviaWidth);   // X has no trailing trivia
        Assert.Equal(2, bAfter.LeadingTriviaWidth);    // b retains its "  " leading trivia
    }

    /// <summary>
    /// Tests inserting after a node with trailing newline, where inserted text has no leading whitespace.
    /// </summary>
    [Fact]
    public void InsertAfter_WithNewline_NoLeadingWhitespace_PreservesFollowingTrivia()
    {
        var tree = SyntaxTree.Parse("a\n  b");
        
        var leaves = tree.Leaves.ToList();
        Assert.Equal(2, leaves[1].LeadingTriviaWidth);  // 'b' has leading "  "
        
        var aNode = tree.Root.Children.First(n => n is SyntaxToken t && t.Text == "a");
        
        tree.CreateEditor()
            .InsertAfter(aNode, "X")
            .Commit();
        
        var actualText = tree.ToText();
        var leavesAfter = tree.Leaves.ToList();
        Assert.Equal(3, leavesAfter.Count);
        
        var xAfter = leavesAfter.First(l => l.Text == "X");
        var bAfter = leavesAfter.First(l => l.Text == "b");
        
        // X has no trivia, b retains its leading trivia
        Assert.Equal("a\nX  b", actualText);
        Assert.Equal(0, xAfter.LeadingTriviaWidth);
        Assert.Equal(0, xAfter.TrailingTriviaWidth);
        Assert.Equal(2, bAfter.LeadingTriviaWidth);    // b retains its "  " leading trivia
    }

    /// <summary>
    /// Tests inserting text with its own trailing newline after a node.
    /// The inserted text's trailing trivia should be consumed properly.
    /// </summary>
    [Fact]
    public void InsertAfter_InsertedTextWithTrailingNewline_PreservesFollowingTrivia()
    {
        var tree = SyntaxTree.Parse("a\n  b");
        
        var leaves = tree.Leaves.ToList();
        Assert.Equal(2, leaves[1].LeadingTriviaWidth);  // 'b' has leading "  "
        
        var aNode = tree.Root.Children.First(n => n is SyntaxToken t && t.Text == "a");
        
        tree.CreateEditor()
            .InsertAfter(aNode, "X\n")
            .Commit();
        
        var actualText = tree.ToText();
        var leavesAfter = tree.Leaves.ToList();
        Assert.Equal(3, leavesAfter.Count);
        
        var aAfter = leavesAfter.First(l => l.Text == "a");
        var xAfter = leavesAfter.First(l => l.Text == "X");
        var bAfter = leavesAfter.First(l => l.Text == "b");
        
        // 'a' has trailing newline, X has trailing newline, b retains leading trivia
        Assert.Equal("a\nX\n  b", actualText);
        Assert.Equal(1, aAfter.TrailingTriviaWidth);   // 'a' keeps trailing newline
        Assert.Equal(0, xAfter.LeadingTriviaWidth);    // X has no leading trivia
        Assert.Equal(1, xAfter.TrailingTriviaWidth);   // X has trailing newline
        Assert.Equal(2, bAfter.LeadingTriviaWidth);    // b retains its "  " leading trivia
    }

    #region Green Flag Mutation Tests

    private static void AssertHasFlags(GreenNodeFlags actual, GreenNodeFlags expected)
    {
        Assert.True((actual & expected) == expected, $"Expected flags to include {expected} but was {actual}");
    }

    private static void AssertNotHasFlags(GreenNodeFlags actual, GreenNodeFlags unexpected)
    {
        Assert.True((actual & unexpected) == 0, $"Expected flags to NOT include {unexpected} but was {actual}");
    }

    private static SyntaxToken FindToken(SyntaxTree tree, NodeKind kind, string text, int occurrence = 0)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(text);

        var matches = tree
            .Select(Query.Kind(kind).WithText(text))
            .OfType<SyntaxToken>()
            .ToList();

        Assert.True(matches.Count > 0, $"Expected to find at least 1 token of kind '{kind}' with text '{text}', but found none.");
        Assert.True(
            occurrence >= 0 && occurrence < matches.Count,
            $"Expected occurrence {occurrence} for token kind '{kind}' text '{text}', but only found {matches.Count} match(es)."
        );

        return matches[occurrence];
    }

    private static void AssertReparseOracleFlagsMatch(SyntaxTree tree, SyntaxTree oracle)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(oracle);

        Assert.Equal(oracle.GreenRoot.Flags, tree.GreenRoot.Flags);

        var treeLeaves = tree.Leaves.ToList();
        var oracleLeaves = oracle.Leaves.ToList();
        Assert.Equal(oracleLeaves.Count, treeLeaves.Count);

        for (int i = 0; i < treeLeaves.Count; i++)
        {
            Assert.Equal(oracleLeaves[i].Kind, treeLeaves[i].Kind);
            Assert.Equal(oracleLeaves[i].Text, treeLeaves[i].Text);

            var expected = oracleLeaves[i].Green.Flags;
            var actual = treeLeaves[i].Green.Flags;
            Assert.True(
                expected == actual,
                $"Leaf flags mismatch at index {i}: {treeLeaves[i].Kind} '{treeLeaves[i].Text}'. Expected={expected} Actual={actual}"
            );
        }
    }

    [Fact]
    public void Replace_OwnLineCommentLeadingTrivia_PreservesGreenBoundaryFlags_OnReplacement()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tree = SyntaxTree.Parse("a\n// c\nb", options);

        var bBefore = Assert.Single(tree.Select(Q.Ident("b")).OfType<SyntaxToken>());
        AssertHasFlags(bBefore.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia | GreenNodeFlags.HasLeadingCommentTrivia);

        tree.CreateEditor()
            .Replace(Q.Ident("b"), "X")
            .Commit();

        var xAfter = Assert.Single(tree.Select(Q.Ident("X")).OfType<SyntaxToken>());
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia | GreenNodeFlags.HasLeadingCommentTrivia);
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.ContainsNewlineTrivia | GreenNodeFlags.ContainsCommentTrivia);
    }

    [Fact]
    public void Replace_TokenOwningTrailingNewline_PreservesGreenBoundaryFlags_OnReplacement()
    {
        var tree = SyntaxTree.Parse("a\nb");

        var aBefore = FindToken(tree, NodeKind.Ident, "a");
        AssertHasFlags(aBefore.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);

        tree.CreateEditor()
            .Replace(Q.Ident("a"), "X")
            .Commit();

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var bAfter = FindToken(tree, NodeKind.Ident, "b");

        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertNotHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void Replace_SameLineCommentTrailingTrivia_PreservesGreenBoundaryFlags_OnReplacement()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tree = SyntaxTree.Parse("a // c\nb", options);

        var aBefore = FindToken(tree, NodeKind.Ident, "a");
        AssertHasFlags(aBefore.Green.Flags, GreenNodeFlags.HasTrailingCommentTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);

        tree.CreateEditor()
            .Replace(Q.Ident("a"), "X")
            .Commit();

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var bAfter = FindToken(tree, NodeKind.Ident, "b");

        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingCommentTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertNotHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingCommentTrivia | GreenNodeFlags.HasLeadingNewlineTrivia);
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsCommentTrivia | GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void Replace_TokenOwningTrailingWhitespace_PreservesGreenBoundaryFlags_OnReplacement()
    {
        var tree = SyntaxTree.Parse("a b");

        var aBefore = FindToken(tree, NodeKind.Ident, "a");
        AssertHasFlags(aBefore.Green.Flags, GreenNodeFlags.HasTrailingWhitespaceTrivia);

        tree.CreateEditor()
            .Replace(Q.Ident("a"), "X")
            .Commit();

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var bAfter = FindToken(tree, NodeKind.Ident, "b");

        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingWhitespaceTrivia);
        AssertNotHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
    }

    [Fact]
    public void InsertAfter_InsertedTextWithTrailingNewline_SetsGreenFlags_OnInsertedAndFollowingTokens()
    {
        var tree = SyntaxTree.Parse("a b");
        var aNode = Assert.Single(tree.Select(Q.Ident("a")).OfType<SyntaxToken>());

        tree.CreateEditor()
            .InsertAfter(aNode, " X\n")
            .Commit();

        var xAfter = Assert.Single(tree.Select(Q.Ident("X")).OfType<SyntaxToken>());
        var bAfter = Assert.Single(tree.Select(Q.Ident("b")).OfType<SyntaxToken>());

        // Inserted token owns the trailing newline; following token should NOT gain leading newline ownership.
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia | GreenNodeFlags.ContainsNewlineTrivia);
        AssertNotHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);

        // Root list should reflect subtree contains.
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void InsertBefore_DoesNotStealLeadingWhitespace_FromFollowingToken_GreenFlags()
    {
        // In the token-centric trivia model, indentation after a newline is leading trivia on the following token.
        var tree = SyntaxTree.Parse("a\n  b");

        var bBefore = FindToken(tree, NodeKind.Ident, "b");
        AssertHasFlags(bBefore.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);

        tree.CreateEditor()
            .InsertBefore(bBefore, "X")
            .Commit();

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var bAfter = FindToken(tree, NodeKind.Ident, "b");

        // Insertion must not transfer whitespace ownership from b to X.
        AssertHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertNotHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);

        // Root should reflect subtree contains.
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
    }

    [Fact]
    public void InsertAfter_InsertedTextWithTrailingCRLF_SetsGreenFlags_OnInsertedAndFollowingTokens()
    {
        var tree = SyntaxTree.Parse("a b");
        var aNode = FindToken(tree, NodeKind.Ident, "a");

        tree.CreateEditor()
            .InsertAfter(aNode, " X\r\n")
            .Commit();

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var bAfter = FindToken(tree, NodeKind.Ident, "b");

        // Inserted token owns the trailing newline; following token should NOT gain leading newline ownership.
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia | GreenNodeFlags.ContainsNewlineTrivia);
        AssertNotHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);

        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void Remove_TokenOwningTrailingNewline_RemovesNewlineBoundaryAndContainsFlags()
    {
        var tree = SyntaxTree.Parse("a\nb");
        var aNode = Assert.Single(tree.Select(Q.Ident("a")).OfType<SyntaxToken>());

        // Sanity: 'a' owns the trailing newline in the token-centric trivia model.
        AssertHasFlags(aNode.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);

        tree.CreateEditor()
            .Remove(Q.Ident("a"))
            .Commit();

        Assert.Equal("b", tree.ToText());
        var bAfter = Assert.Single(tree.Select(Q.Ident("b")).OfType<SyntaxToken>());
        AssertNotHasFlags(bAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertNotHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void Remove_OneNewlineOwner_DoesNotClearContainsNewlineTrivia_WhenOthersRemain()
    {
        var tree = SyntaxTree.Parse("a\nb\nc");
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);

        tree.CreateEditor()
            .Remove(Q.Ident("a"))
            .Commit();

        // The newline between b and c should still exist.
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void Remove_OneCommentOwner_DoesNotClearContainsCommentTrivia_WhenOthersRemain()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tree = SyntaxTree.Parse("a // c1\nb // c2\nc", options);
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsCommentTrivia);

        tree.CreateEditor()
            .Remove(Q.Ident("a"))
            .Commit();

        // The comment after b should still exist.
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsCommentTrivia);
    }

    [Fact]
    public void Remove_OneWhitespaceOwner_DoesNotClearContainsWhitespaceTrivia_WhenOthersRemain()
    {
        // Two separate whitespace regions: after 'a' and after 'b'.
        var tree = SyntaxTree.Parse("a  b c");
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);

        tree.CreateEditor()
            .Remove(Q.Ident("a"))
            .Commit();

        // The remaining space between b and c should still exist.
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
    }

    [Fact]
    public void Replace_MultipleNodes_TransfersLeadingToFirst_AndTrailingToLast_GreenBoundaryFlags()
    {
        // b owns leading whitespace (indentation) and trailing newline.
        var tree = SyntaxTree.Parse("a\n  b\nc");

        var bBefore = FindToken(tree, NodeKind.Ident, "b");
        AssertHasFlags(bBefore.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);

        tree.CreateEditor()
            .Replace(Q.Ident("b"), "X Y")
            .Commit();

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var yAfter = FindToken(tree, NodeKind.Ident, "Y");
        var cAfter = FindToken(tree, NodeKind.Ident, "c");

        // Leading boundary transfers to first replacement node.
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertNotHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);

        // Trailing boundary transfers to last replacement node.
        AssertHasFlags(yAfter.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertNotHasFlags(yAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);

        // Following token should not incorrectly gain leading newline ownership.
        AssertNotHasFlags(cAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);

        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia | GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void Replace_WithBlockAtEdges_TransfersTriviaToBlockBoundaries_GreenFlags()
    {
        // Replacement begins/ends with a container (block). Trivia should attach to opener/closer boundaries.
        var tree = SyntaxTree.Parse("a\n  b\nc");

        var bBefore = FindToken(tree, NodeKind.Ident, "b");
        AssertHasFlags(bBefore.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);

        tree.CreateEditor()
            .Replace(Q.Ident("b"), "{x}")
            .Commit();

        var block = Assert.Single(tree.Select(Q.BraceBlock).OfType<SyntaxBlock>());

        // Boundary trivia should be preserved on the block boundaries.
        AssertHasFlags(block.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);

        // Containers must not accidentally carry child boundary flags beyond their own semantics.
        // (Block boundary flags are only opener-leading and closer-trailing.)
        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia | GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void UndoRedo_RestoresLeafBoundaryFlags_NotJustRootFlags()
    {
        var tree = SyntaxTree.Parse("a b");
        var aBefore = FindToken(tree, NodeKind.Ident, "a");
        var bBefore = FindToken(tree, NodeKind.Ident, "b");

        var aFlagsBefore = aBefore.Green.Flags;
        var bFlagsBefore = bBefore.Green.Flags;
        var rootFlagsBefore = tree.GreenRoot.Flags;

        tree.CreateEditor()
            .InsertAfter(aBefore, " X\n")
            .Commit();

        var aAfter = FindToken(tree, NodeKind.Ident, "a");
        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var bAfter = FindToken(tree, NodeKind.Ident, "b");

        var aFlagsAfter = aAfter.Green.Flags;
        var xFlagsAfter = xAfter.Green.Flags;
        var bFlagsAfter = bAfter.Green.Flags;
        var rootFlagsAfter = tree.GreenRoot.Flags;

        // Sanity: mutation should introduce a newline owner.
        AssertHasFlags(xFlagsAfter, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertHasFlags(rootFlagsAfter, GreenNodeFlags.ContainsNewlineTrivia);

        Assert.True(tree.Undo());

        var aUndo = FindToken(tree, NodeKind.Ident, "a");
        var bUndo = FindToken(tree, NodeKind.Ident, "b");
        Assert.Equal(aFlagsBefore, aUndo.Green.Flags);
        Assert.Equal(bFlagsBefore, bUndo.Green.Flags);
        Assert.Equal(rootFlagsBefore, tree.GreenRoot.Flags);

        Assert.True(tree.Redo());

        var aRedo = FindToken(tree, NodeKind.Ident, "a");
        var xRedo = FindToken(tree, NodeKind.Ident, "X");
        var bRedo = FindToken(tree, NodeKind.Ident, "b");
        Assert.Equal(aFlagsAfter, aRedo.Green.Flags);
        Assert.Equal(xFlagsAfter, xRedo.Green.Flags);
        Assert.Equal(bFlagsAfter, bRedo.Green.Flags);
        Assert.Equal(rootFlagsAfter, tree.GreenRoot.Flags);
    }

    [Fact]
    public void SchemaRebind_ReplaceInsideSyntaxNode_PreservesLeafBoundaryFlags_AndKeepsSyntaxContainersBoundaryFree()
    {
        const GreenNodeFlags boundaryMask =
            GreenNodeFlags.HasLeadingNewlineTrivia |
            GreenNodeFlags.HasTrailingNewlineTrivia |
            GreenNodeFlags.HasLeadingWhitespaceTrivia |
            GreenNodeFlags.HasTrailingWhitespaceTrivia |
            GreenNodeFlags.HasLeadingCommentTrivia |
            GreenNodeFlags.HasTrailingCommentTrivia;

        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .WithTagPrefixes('@')
            .DefineSyntax(Syntax.Define<TestTaggedNode>("testTagged")
                .Match(Q.AnyTaggedIdent, Q.AnyString)
                .Build())
            .Build();

        var tree = SyntaxTree.Parse("before\n  @tag \"value\" // c\nnext", schema);

        var syntaxBefore = Assert.Single(tree.Select(Q.Syntax<TestTaggedNode>()).OfType<TestTaggedNode>());
        AssertNotHasFlags(syntaxBefore.Green.Flags, boundaryMask);

        var tagBefore = FindToken(tree, NodeKind.TaggedIdent, "@tag");
        var valueBefore = FindToken(tree, NodeKind.String, "\"value\"");
        var nextBefore = FindToken(tree, NodeKind.Ident, "next");

        // Indentation is leading whitespace on the first token inside the syntax node.
        AssertHasFlags(tagBefore.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);

        // The string token owns same-line comment + newline.
        AssertHasFlags(valueBefore.Green.Flags, GreenNodeFlags.HasTrailingCommentTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertNotHasFlags(nextBefore.Green.Flags, GreenNodeFlags.HasLeadingCommentTrivia | GreenNodeFlags.HasLeadingNewlineTrivia);

        tree.CreateEditor()
            .Replace(Q.String("\"value\""), "\"X\"")
            .Commit();

        var syntaxAfter = Assert.Single(tree.Select(Q.Syntax<TestTaggedNode>()).OfType<TestTaggedNode>());
        AssertNotHasFlags(syntaxAfter.Green.Flags, boundaryMask);

        var tagAfter = FindToken(tree, NodeKind.TaggedIdent, "@tag");
        var xAfter = FindToken(tree, NodeKind.String, "\"X\"");
        var nextAfter = FindToken(tree, NodeKind.Ident, "next");

        AssertHasFlags(tagAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingCommentTrivia | GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertNotHasFlags(nextAfter.Green.Flags, GreenNodeFlags.HasLeadingCommentTrivia | GreenNodeFlags.HasLeadingNewlineTrivia);

        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia | GreenNodeFlags.ContainsCommentTrivia | GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void SchemaRebind_InsertBeforeSyntaxNode_DoesNotStealLeadingWhitespace_AndKeepsSyntaxContainersBoundaryFree()
    {
        const GreenNodeFlags boundaryMask =
            GreenNodeFlags.HasLeadingNewlineTrivia |
            GreenNodeFlags.HasTrailingNewlineTrivia |
            GreenNodeFlags.HasLeadingWhitespaceTrivia |
            GreenNodeFlags.HasTrailingWhitespaceTrivia |
            GreenNodeFlags.HasLeadingCommentTrivia |
            GreenNodeFlags.HasTrailingCommentTrivia;

        var schema = Schema.Create()
            .WithTagPrefixes('@')
            .DefineSyntax(Syntax.Define<TestTaggedNode>("testTagged")
                .Match(Q.AnyTaggedIdent, Q.AnyString)
                .Build())
            .Build();

        var tree = SyntaxTree.Parse("before\n  @tag \"value\"\nafter", schema);

        var syntaxBefore = Assert.Single(tree.Select(Q.Syntax<TestTaggedNode>()).OfType<TestTaggedNode>());
        AssertNotHasFlags(syntaxBefore.Green.Flags, boundaryMask);

        var tagBefore = FindToken(tree, NodeKind.TaggedIdent, "@tag");
        AssertHasFlags(tagBefore.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);

        tree.CreateEditor()
            .InsertBefore(Q.Syntax<TestTaggedNode>(), "X\n")
            .Commit();

        var syntaxAfter = Assert.Single(tree.Select(Q.Syntax<TestTaggedNode>()).OfType<TestTaggedNode>());
        AssertNotHasFlags(syntaxAfter.Green.Flags, boundaryMask);

        var xAfter = FindToken(tree, NodeKind.Ident, "X");
        var tagAfter = FindToken(tree, NodeKind.TaggedIdent, "@tag");
        var afterAfter = FindToken(tree, NodeKind.Ident, "after");

        // Inserted token owns its trailing newline; syntax node's first token keeps its indentation.
        AssertHasFlags(xAfter.Green.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertHasFlags(tagAfter.Green.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertNotHasFlags(afterAfter.Green.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);

        AssertHasFlags(tree.GreenRoot.Flags, GreenNodeFlags.ContainsWhitespaceTrivia | GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void ReparseOracle_AfterComplexEdit_MatchesRootAndLeafFlags_WithOptions()
    {
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);

        const string source = "a{\n  b // c1\n  d(e) /* c2 */\n}\n";
        var tree = SyntaxTree.Parse(source, options);

        tree.CreateEditor(options)
            .Replace(Q.Ident("b"), "{x}")
            .InsertAfter(Q.Ident("d"), ".Y\n")
            .Commit();

        var editedText = tree.ToText();
        var oracle = SyntaxTree.Parse(editedText, options);

        AssertReparseOracleFlagsMatch(tree, oracle);
    }

    [Fact]
    public void ReparseOracle_AfterComplexEdit_MatchesRootAndLeafFlags_WithSchemaAndBinding()
    {
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
            .WithTagPrefixes('@')
            .DefineSyntax(Syntax.Define<TestTaggedNode>("testTagged")
                .Match(Q.AnyTaggedIdent, Q.AnyString)
                .Build())
            .Build();

        var tree = SyntaxTree.Parse("before\n  @tag \"value\" // c\nafter", schema);

        tree.CreateEditor()
            .InsertBefore(Q.Syntax<TestTaggedNode>(), "X\n")
            .Replace(Q.String("\"value\""), "\"Z\"")
            .Commit();

        var editedText = tree.ToText();
        var oracle = SyntaxTree.Parse(editedText, schema);

        AssertReparseOracleFlagsMatch(tree, oracle);
    }

    [Fact]
    public void ReparseOracle_AfterRemoveReplaceAndCRLFInsert_MatchesRootAndLeafFlags_WithOptions()
    {
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);

        const string source = "a // c1\r\n  b /* c2 */\r\nc  d";
        var tree = SyntaxTree.Parse(source, options);

        tree.CreateEditor(options)
            // Remove token that owns trailing comment+newline
            .Remove(Q.Ident("a"))
            // Replace a token that owns leading indentation
            .Replace(Q.Ident("b"), "{x}")
            // Insert a token that owns CRLF; avoid leading trivia by starting with a symbol
            .InsertAfter(Q.Ident("c"), ".Y\r\n")
            .Commit();

        var editedText = tree.ToText();
        var oracle = SyntaxTree.Parse(editedText, options);

        AssertReparseOracleFlagsMatch(tree, oracle);
    }

    [Fact]
    public void ReparseOracle_AfterEditsOnMultipleSyntaxNodes_MatchesRootAndLeafFlags_WithSchemaAndBinding()
    {
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .WithTagPrefixes('@')
            .DefineSyntax(Syntax.Define<TestTaggedNode>("testTagged")
                .Match(Q.AnyTaggedIdent, Q.AnyString)
                .Build())
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("funcCall")
                .Match(Q.AnyIdent, Q.ParenBlock)
                .Build())
            .Build();

        var tree = SyntaxTree.Parse("@tag \"value\"\nfoo(a, b)\n", schema);

        tree.CreateEditor()
            .Replace(Q.String("\"value\""), "\"Z\"")
            // Insert inside the function call argument list. Start with a symbol to avoid leading trivia.
            .InsertAfter(Q.Ident("a"), ",c")
            .Commit();

        var editedText = tree.ToText();
        var oracle = SyntaxTree.Parse(editedText, schema);

        AssertReparseOracleFlagsMatch(tree, oracle);
    }

    [Fact]
    public void ReparseOracle_AfterDeeplyNestedBlockEdits_MatchesRootAndLeafFlags_WithOptions()
    {
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);

        const string source =
            "root {\n" +
            "  a(b[c{d(e)}]) // c1\n" +
            "  { x /* c2 */ }\n" +
            "}\n";

        var tree = SyntaxTree.Parse(source, options);

        // Perform multiple edits inside deeply nested structures.
        tree.CreateEditor(options)
            // Replace an inner identifier.
            .Replace(Q.Ident("d"), "D")
            // Insert inside the deepest paren. Start with a symbol to avoid leading-trivia ambiguity.
            .InsertAfter(Q.Ident("e"), ".Y\n")
            // Replace an identifier with a block to exercise block-edge trivia semantics.
            .Replace(Q.Ident("x"), "{z}")
            .Commit();

        var editedText = tree.ToText();
        var oracle = SyntaxTree.Parse(editedText, options);

        AssertReparseOracleFlagsMatch(tree, oracle);
    }

    [Fact]
    public void InsertAfter_BlockContainsNewlineFlag_UpdatesAfterMutation()
    {
        var tree = SyntaxTree.Parse("{a}");
        var blockBefore = (SyntaxBlock)Assert.Single(tree.Select(Q.BraceBlock));

        AssertNotHasFlags(blockBefore.Green.Flags, GreenNodeFlags.ContainsNewlineTrivia);

        var aNode = Assert.Single(tree.Select(Q.Ident("a")).OfType<SyntaxToken>());
        tree.CreateEditor()
            .InsertAfter(aNode, "X\n")
            .Commit();

        var blockAfter = (SyntaxBlock)Assert.Single(tree.Select(Q.BraceBlock));
        AssertHasFlags(blockAfter.Green.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void GreenFlags_UndoRedo_RestoreFlagState_AfterMutation()
    {
        var tree = SyntaxTree.Parse("a b");
        var before = tree.GreenRoot.Flags;
        AssertNotHasFlags(before, GreenNodeFlags.ContainsNewlineTrivia);

        var aNode = Assert.Single(tree.Select(Q.Ident("a")).OfType<SyntaxToken>());
        tree.CreateEditor()
            .InsertAfter(aNode, "X\n")
            .Commit();

        var after = tree.GreenRoot.Flags;
        AssertHasFlags(after, GreenNodeFlags.ContainsNewlineTrivia);

        Assert.True(tree.Undo());
        Assert.Equal(before, tree.GreenRoot.Flags);

        Assert.True(tree.Redo());
        Assert.Equal(after, tree.GreenRoot.Flags);
    }

    #endregion

    /// <summary>
    /// Tests that block opener trivia follows the correct trivia model:
    ///   - Trailing trivia: up to AND INCLUDING the newline
    ///   - Leading trivia: content AFTER the newline (e.g., indentation)
    /// 
    /// For "{\n    x}":
    ///   - '{' (opener) trailing trivia: "\n" (just the newline, width 1 or 2 for CRLF)
    ///   - 'x' leading trivia: "    " (indentation, width 4)
    /// </summary>
    [Fact]
    public void BlockOpener_TrailingTrivia_ShouldStopAtNewline()
    {
        // Parse a simple block with newline and indentation
        var tree = SyntaxTree.Parse("{\n    x\n}");
        
        // Find the block
        var block = tree.Root.Children.OfType<SyntaxBlock>().First();
        Assert.Equal('{', block.Opener);
        
        // Get the opener node and first child
        var openerNode = block.OpenerNode;
        var firstChild = block.InnerChildren.OfType<SyntaxToken>().First(t => t.Text == "x");
        
        // BUG: The opener's trailing trivia includes the indentation that should be on 'x'
        // Expected: opener trailing = 1 (LF) or 2 (CRLF), x leading = 4 (indentation)
        // Actual:   opener trailing = 5 or 6 (newline + indent), x leading = 0
        
        // This is the EXPECTED behavior (currently fails):
        Assert.True(openerNode.TrailingTriviaWidth <= 2, 
            $"Opener trailing trivia should only be newline (1-2 chars), but was {openerNode.TrailingTriviaWidth}");
        Assert.Equal(4, firstChild.LeadingTriviaWidth);
    }

    /// <summary>
    /// Tests that inserting after block opener preserves first child's indentation.
    /// 
    /// Input: "{\n    x\n}"
    /// After InsertAfter(opener, "/* comment */\n"):
    /// Result: "{\n/* comment */\n    x\n}" where 'x' still has 4-space indentation
    /// </summary>
    [Fact]
    public void InsertAfterBlockOpener_ShouldPreserveFirstChildIndentation()
    {
        var tree = SyntaxTree.Parse("{\n    x\n}");
        
        // Find the block and its opener
        var block = tree.Root.Children.OfType<SyntaxBlock>().First();
        var openerNode = block.OpenerNode;
        var firstChild = block.InnerChildren.OfType<SyntaxToken>().First(t => t.Text == "x");
        
        // Verify original leading trivia is preserved (4 spaces of indentation)
        Assert.Equal(4, firstChild.LeadingTriviaWidth);
        
        // Insert a comment after the opener node
        tree.CreateEditor()
            .InsertAfter(openerNode, "/* comment */\n")
            .Commit();
        
        var result = tree.ToText();
        
        // Re-find the 'x' token after edit
        var blockAfter = tree.Root.Children.OfType<SyntaxBlock>().First();
        var xAfter = blockAfter.InnerChildren.OfType<SyntaxToken>().First(t => t.Text == "x");
        
        // 'x' should retain its 4-space indentation after the edit
        Assert.Equal(4, xAfter.LeadingTriviaWidth);
        
        // The output should show proper formatting with indentation preserved
        Assert.Contains("/* comment */\n    x", result);
    }

    [Fact]
    public void InsertBefore_RedNode_GreenNodes()
    {
        var tree = SyntaxTree.Parse("world");
        var worldNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        var lexer = new GreenLexer();
        var nodesToInsert = lexer.ParseToGreenNodes("hello ");
        
        tree.CreateEditor()
            .InsertBefore(worldNode, nodesToInsert.AsEnumerable())
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }
    
    [Fact]
    public void InsertAfter_RedNode_GreenNodes()
    {
        var tree = SyntaxTree.Parse("hello");
        var helloNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        var lexer = new GreenLexer();
        var nodesToInsert = lexer.ParseToGreenNodes(" world");
        
        tree.CreateEditor()
            .InsertAfter(helloNode, nodesToInsert.AsEnumerable())
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }
    
    [Fact]
    public void Replace_RedNode_WithRedNode()
    {
        var tree = SyntaxTree.Parse("old");
        var oldNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        
        // Create a new tree to get a RedNode from it
        var sourceTree = SyntaxTree.Parse("new");
        var newRedNode = sourceTree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        
        tree.CreateEditor()
            .Replace(oldNode, newRedNode)
            .Commit();
        
        Assert.Equal("new", tree.ToText());
    }
    
    [Fact]
    public void Replace_RedNode_WithGreenNode()
    {
        var tree = SyntaxTree.Parse("old");
        var oldNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        var lexer = new GreenLexer();
        var newGreenNode = lexer.ParseToGreenNodes("new").First();
        
        tree.CreateEditor()
            .Replace(oldNode, newGreenNode)
            .Commit();
        
        Assert.Equal("new", tree.ToText());
    }
    
    [Fact]
    public void Replace_RedNode_WithMultipleRedNodes()
    {
        var tree = SyntaxTree.Parse("x");
        var xNode = tree.Root.Children.First(n => n.Kind == NodeKind.Ident);
        
        var sourceTree = SyntaxTree.Parse("a b c");
        // Get all children (identifiers and their trivia are combined in nodes)
        var replacements = sourceTree.Root.Children;
        
        tree.CreateEditor()
            .Replace(xNode, replacements)
            .Commit();
        
        Assert.Equal("a b c", tree.ToText());
    }
    
    [Fact]
    public void InsertBefore_RootNode_ThrowsArgumentException()
    {
        var tree = SyntaxTree.Parse("test");
        
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            tree.CreateEditor()
                .InsertBefore(tree.Root, "prefix")
                .Commit();
        });
        
        Assert.Contains("root", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void QueryExact_WithQueryBasedEditor_WorksCorrectly()
    {
        var tree = SyntaxTree.Parse("a b c");
        var bNode = tree.Root.Children.Where(n => n.Kind == NodeKind.Ident).ElementAt(1); // "b"
        
        // Use Query.Exact with query-based Replace method
        tree.CreateEditor()
            .Replace(Q.Exact(bNode), "X")
            .Commit();
        
        Assert.Equal("a X c", tree.ToText());
    }
    
    [Fact]
    public void BatchRemove_WithQueries_RemovesAllMatches()
    {
        var tree = SyntaxTree.Parse("a1 b2 c3");
        var queries = new INodeQuery[] 
        { 
            Q.Ident("a1"), 
            Q.Ident("c3") 
        };
        
        tree.CreateEditor()
            .Remove(queries)
            .Commit();
        
        var text = tree.ToText();
        var textNoSpaces = text.Replace(" ", "");
        Assert.DoesNotContain("a1", textNoSpaces);
        Assert.DoesNotContain("c3", textNoSpaces);
        Assert.Contains("b2", textNoSpaces);
    }
    
    [Fact]
    public void BatchReplace_WithQueries_ReplacesAllMatches()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        var queries = new INodeQuery[] 
        { 
            Q.Ident("foo"), 
            Q.Ident("baz") 
        };
        
        tree.CreateEditor()
            .Replace(queries, "X")
            .Commit();
        
        Assert.Equal("X bar X", tree.ToText());
    }
    
    [Fact]
    public void BatchReplace_WithQueries_AndTransformer()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        var queries = new INodeQuery[] 
        { 
            Q.Ident("foo"), 
            Q.Ident("baz") 
        };
        
        tree.CreateEditor()
            .Replace(queries, n => $"[{((SyntaxToken)n).Text}]")
            .Commit();
        
        // Trivia behavior: "foo" has no leading trivia, "bar" has leading space, "baz" has leading space.
        // When we replace "foo" with "[foo]", the original token had no leading trivia.
        // When we replace "baz" with "[baz]", its leading trivia (space) is preserved before the replacement.
        var text = tree.ToText();
        Assert.Contains("[foo]", text);
        Assert.Contains("bar", text);
        Assert.Contains("[baz]", text);
    }
    
    #endregion
    
    #region Edit Operations (Content Transformation)
    
    [Fact]
    public void Edit_SingleNode_TransformsContent()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent.First(), content => content.ToUpper())
            .Commit();
        
        Assert.Equal("HELLO", tree.ToText());
    }
    
    [Fact]
    public void Edit_WithTrivia_PreservesTrivia()
    {
        // "  foo" has leading whitespace trivia on the identifier
        var tree = SyntaxTree.Parse("  foo");
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent.First(), content => content.ToUpper())
            .Commit();
        
        // Leading trivia should be preserved, content transformed
        Assert.Equal("  FOO", tree.ToText());
    }
    
    [Fact]
    public void Edit_TransformerReceivesContentWithoutTrivia()
    {
        var tree = SyntaxTree.Parse("  hello  ");
        string? capturedContent = null;
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent.First(), content =>
            {
                capturedContent = content;
                return content.ToUpper();
            })
            .Commit();
        
        // Transformer should receive "hello" without surrounding whitespace
        Assert.Equal("hello", capturedContent);
    }
    
    [Fact]
    public void Edit_MultipleNodes_TransformsAll()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent, content => $"[{content}]")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("[foo]", text);
        Assert.Contains("[bar]", text);
        Assert.Contains("[baz]", text);
    }
    
    [Fact]
    public void Edit_MultipleNodes_PreservesAllTrivia()
    {
        var tree = SyntaxTree.Parse("foo  bar   baz");
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent, content => content.ToUpper())
            .Commit();
        
        // Should preserve the spacing between tokens
        Assert.Equal("FOO  BAR   BAZ", tree.ToText());
    }
    
    [Fact]
    public void Edit_WithQuery_OnlyAffectsMatchingNodes()
    {
        var tree = SyntaxTree.Parse("foo bar foo");
        
        tree.CreateEditor()
            .Edit(Q.Ident("foo"), content => "X")
            .Commit();
        
        Assert.Equal("X bar X", tree.ToText());
    }
    
    [Fact]
    public void Edit_NoMatches_LeavesTreeUnchanged()
    {
        var tree = SyntaxTree.Parse("foo bar");
        
        tree.CreateEditor()
            .Edit(Q.Ident("nonexistent"), content => "X")
            .Commit();
        
        Assert.Equal("foo bar", tree.ToText());
    }
    
    [Fact]
    public void Edit_RedNode_DirectNodeTransform()
    {
        var tree = SyntaxTree.Parse("hello world");
        var firstIdent = Q.AnyIdent.First().Select(tree).First();
        
        tree.CreateEditor()
            .Edit(firstIdent, content => content.ToUpper())
            .Commit();
        
        Assert.Equal("HELLO world", tree.ToText());
    }
    
    [Fact]
    public void Edit_RedNode_PreservesTrivia()
    {
        var tree = SyntaxTree.Parse("  hello  ");
        var ident = Q.AnyIdent.First().Select(tree).First();
        
        tree.CreateEditor()
            .Edit(ident, content => "WORLD")
            .Commit();
        
        // Leading and trailing trivia should be preserved
        Assert.Equal("  WORLD  ", tree.ToText());
    }
    
    [Fact]
    public void Edit_MultipleRedNodes_TransformsAll()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        var idents = Q.AnyIdent.Select(tree).ToList();
        
        tree.CreateEditor()
            .Edit(idents, content => $"({content})")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("(foo)", text);
        Assert.Contains("(bar)", text);
        Assert.Contains("(baz)", text);
    }
    
    [Fact]
    public void Edit_WithMultipleQueries_TransformsAllMatches()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        var queries = new INodeQuery[]
        {
            Q.Ident("foo"),
            Q.Ident("baz")
        };
        
        tree.CreateEditor()
            .Edit(queries, content => $"[{content}]")
            .Commit();
        
        var text = tree.ToText();
        Assert.Contains("[foo]", text);
        Assert.Contains("bar", text);
        Assert.Contains("[baz]", text);
        Assert.DoesNotContain("[bar]", text);
    }
    
    [Fact]
    public void Edit_VsReplace_EditExcludesTrivia()
    {
        // This test demonstrates the difference between Edit and Replace
        var tree1 = SyntaxTree.Parse("  hello  ");
        var tree2 = SyntaxTree.Parse("  hello  ");
        
        // Edit: transformer receives "hello" (no trivia), trivia preserved
        tree1.CreateEditor()
            .Edit(Q.AnyIdent.First(), content =>
            {
                // content is "hello", not "  hello  "
                Assert.Equal("hello", content);
                return content.ToUpper();
            })
            .Commit();
        
        // Replace with transformer: receives full RedNode, can access trivia
        tree2.CreateEditor()
            .Replace(Q.AnyIdent.First(), node =>
            {
                var leaf = (SyntaxToken)node;
                // Text property gives content without trivia
                Assert.Equal("hello", leaf.Text);
                // But we have access to trivia via the node
                Assert.True(leaf.LeadingTriviaWidth > 0);
                return leaf.Text.ToUpper();
            })
            .Commit();
        
        // Both should produce the same result in this case
        Assert.Equal("  HELLO  ", tree1.ToText());
        Assert.Equal("  HELLO  ", tree2.ToText());
    }
    
    [Fact]
    public void Edit_Block_TransformsBlockContent()
    {
        var tree = SyntaxTree.Parse("{inner}");
        
        tree.CreateEditor()
            .Edit(Q.BraceBlock.First(), content => content.ToUpper())
            .Commit();
        
        Assert.Equal("{INNER}", tree.ToText());
    }
    
    [Fact]
    public void Edit_Block_PreservesBlockTrivia()
    {
        // When a block has trivia attached directly to it (not separate whitespace nodes),
        // the Edit method should preserve that trivia
        var tree = SyntaxTree.Parse("foo {inner}");
        
        tree.CreateEditor()
            .Edit(Q.BraceBlock.First(), content =>
            {
                // Content should be the block without leading trivia (space before {)
                Assert.StartsWith("{", content);
                Assert.EndsWith("}", content);
                return "{EDITED}";
            })
            .Commit();
        
        // The space before the block (leading trivia) should be preserved
        Assert.Equal("foo {EDITED}", tree.ToText());
    }
    
    [Fact]
    public void Edit_NumericContent_TransformsNumber()
    {
        var tree = SyntaxTree.Parse("x = 42");
        
        tree.CreateEditor()
            .Edit(Q.AnyNumeric.First(), content =>
            {
                var num = int.Parse(content);
                return (num * 2).ToString();
            })
            .Commit();
        
        Assert.Equal("x = 84", tree.ToText());
    }
    
    [Fact]
    public void Edit_Chained_MultipleEditsApplied()
    {
        var tree = SyntaxTree.Parse("foo 123 bar");
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent, content => content.ToUpper())
            .Edit(Q.AnyNumeric.First(), content => (int.Parse(content) * 2).ToString())
            .Commit();
        
        Assert.Equal("FOO 246 BAR", tree.ToText());
    }
    
    [Fact]
    public void Edit_PendingEditCount_IncrementsCorrectly()
    {
        var tree = SyntaxTree.Parse("a b c");
        var editor = tree.CreateEditor();
        
        Assert.Equal(0, editor.PendingEditCount);
        Assert.False(editor.HasPendingEdits);
        
        editor.Edit(Q.AnyIdent, content => content.ToUpper());
        
        // Should have 3 pending edits (one for each identifier)
        Assert.Equal(3, editor.PendingEditCount);
        Assert.True(editor.HasPendingEdits);
        
        editor.Commit();
        
        Assert.Equal(0, editor.PendingEditCount);
        Assert.False(editor.HasPendingEdits);
    }
    
    [Fact]
    public void Edit_Rollback_DiscardsEdits()
    {
        var tree = SyntaxTree.Parse("hello");
        var editor = tree.CreateEditor();
        
        editor.Edit(Q.AnyIdent.First(), content => content.ToUpper());
        Assert.True(editor.HasPendingEdits);
        
        editor.Rollback();
        
        Assert.False(editor.HasPendingEdits);
        Assert.Equal("hello", tree.ToText()); // Tree unchanged
    }
    
    [Fact]
    public void Edit_LeadingTrivia_NotDuplicatedInContent()
    {
        // Regression test: leading trivia should not appear in the transformer's input
        var tree = SyntaxTree.Parse("  foo");
        string? capturedContent = null;
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent.First(), content =>
            {
                capturedContent = content;
                return "BAR";
            })
            .Commit();
        
        // Transformer should receive "foo" not "  foo"
        Assert.Equal("foo", capturedContent);
        // Result should preserve the leading trivia before the replacement
        Assert.Equal("  BAR", tree.ToText());
    }
    
    [Fact]
    public void Edit_LeadingTrivia_NotDuplicatedInOutput()
    {
        // Regression test: when Edit preserves trivia, it should not double the whitespace
        var tree = SyntaxTree.Parse("a  b");
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent, content => content.ToUpper())
            .Commit();
        
        // The "  " between a and b is leading trivia on b
        // After edit, should still be exactly "  " not "    "
        Assert.Equal("A  B", tree.ToText());
    }
    
    [Fact]
    public void Edit_ResultLengthMatchesExpected()
    {
        // Verify the exact string length to catch trivia duplication
        var original = "  hello  ";
        var tree = SyntaxTree.Parse(original);
        
        tree.CreateEditor()
            .Edit(Q.AnyIdent.First(), content => "X")
            .Commit();
        
        var result = tree.ToText();
        // Original: "  hello  " (9 chars)
        // Expected: "  X  " (5 chars) - same trivia, shorter content
        Assert.Equal("  X  ", result);
        Assert.Equal(5, result.Length);
    }
    
    [Fact]
    public void Edit_SyntaxNode_ExtractsFirstChildTrivia()
    {
        // Regression test: for syntax nodes (containers), trivia should be extracted
        // from the first/last children, not from the syntax node itself
        var schema = Schema.Create()
            .WithTagPrefixes('@')
            .DefineSyntax(Syntax.Define<TestTaggedNode>("testTagged")
                .Match(Q.AnyTaggedIdent, Q.AnyString)
                .Build())
            .Build();
        
        var tree = SyntaxTree.Parse("before\n@tag \"value\"", schema);
        
        tree.CreateEditor()
            .Edit(Q.Syntax<TestTaggedNode>(), content =>
            {
                // Content should NOT include the leading newline
                Assert.DoesNotContain("\n", content.Split('@')[0]); // Nothing before @
                return $"// {content}";
            })
            .Commit();
        
        var result = tree.ToText();
        // The leading trivia (newline) should be preserved before the replacement
        Assert.Contains("before\n// @tag", result);
    }
    
    [Fact]
    public void Insert_WithSchemaCommentStyles_InsertsComment()
    {
        // Test that comment insertion works when schema has comment styles
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
            .WithTagPrefixes('@')
            .DefineSyntax(Syntax.Define<TestTaggedNode>("testTagged")
                .Match(Q.AnyTaggedIdent, Q.AnyString)
                .Build())
            .Build();
        
        var tree = SyntaxTree.Parse("before\n@tag \"value\"", schema);
        
        tree.CreateEditor()
            .InsertBefore(Q.Syntax<TestTaggedNode>(), "// comment\n")
            .Commit();
        
        var result = tree.ToText();
        // The comment should be inserted before @tag (after "before\n")
        Assert.Contains("// comment", result);
        Assert.Contains("@tag \"value\"", result);
    }
    
    /// <summary>
    /// Test syntax node for Edit tests.
    /// </summary>
    private sealed class TestTaggedNode : SyntaxNode
    {
        public TestTaggedNode(CreationContext context) : base(context) { }
    }
    
    #endregion
    
    #region Trivia Preservation Bug Tests
    
    /// <summary>
    /// Minimal reproduction of trivia loss bug: when using a schema with CommentStyles
    /// and nested block structures, the indentation before "} else {" is lost.
    /// 
    /// Trigger conditions:
    /// 1. Schema with WithCommentStyles(...)
    /// 2. Nested blocks (e.g., if inside if)
    /// 
    /// Expected: "\n    } else {\n" (4 spaces before })
    /// Actual:   "\n} else {\n" (no spaces before })
    /// </summary>
    [Fact]
    public void TriviaPreservation_NestedBlocksWithSchema_IndentationBeforeElseLost()
    {
        const string source = @"void test() {
    if (a) {
        if (b) {
            x = 1;
        }
    } else {
        y = 0;
    }
}";
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .Build();
            
        var tree = SyntaxTree.Parse(source, schema);
        var result = tree.ToText().ReplaceLineEndings("\n");
        
        // The "    } else {" should preserve its 4-space indentation
        Assert.Contains("\n    } else {\n", result);
    }
    
    #endregion
    
    #region Query.Between with SyntaxEditor
    
    /// <summary>
    /// Tests that Query.Between correctly replaces a range of nodes.
    /// </summary>
    [Fact]
    public void Replace_QueryBetween_ReplacesEntireRange()
    {
        // Arrange: a, b, c - we want to replace from 'a' to 'c' inclusive
        var tree = SyntaxTree.Parse("x a b c y");
        
        // Act: replace everything from 'a' to 'c' (inclusive)
        tree.CreateEditor()
            .Replace(Q.Between(Q.Ident("a"), Q.Ident("c"), inclusive: true), "REPLACED")
            .Commit();
        
        // Assert: the range [a, b, c] should be replaced with REPLACED
        Assert.Equal("x REPLACED y", tree.ToText());
    }
    
    /// <summary>
    /// Tests that Remove with Query.Between removes the entire matched range.
    /// </summary>
    [Fact]
    public void Remove_QueryBetween_RemovesEntireRange()
    {
        var tree = SyntaxTree.Parse("start a b c end");
        
        tree.CreateEditor()
            .Remove(Q.Between(Q.Ident("a"), Q.Ident("c"), inclusive: true))
            .Commit();
        
        // Trivia handling: leading trivia of 'a' (space) is removed with 'a',
        // but 'end' keeps its leading trivia (space)
        Assert.Equal("start end", tree.ToText());
    }
    
    /// <summary>
    /// Tests that InsertBefore with Query.Between inserts before the start of the range.
    /// </summary>
    [Fact]
    public void InsertBefore_QueryBetween_InsertsBeforeRange()
    {
        var tree = SyntaxTree.Parse("x a b c y");
        
        tree.CreateEditor()
            .InsertBefore(Q.Between(Q.Ident("a"), Q.Ident("c"), inclusive: true), "BEFORE ")
            .Commit();
        
        Assert.Equal("x BEFORE a b c y", tree.ToText());
    }
    
    /// <summary>
    /// Tests that InsertAfter with Query.Between inserts after the end of the range.
    /// Note: With trailing trivia model, the preceding token keeps its trailing whitespace,
    /// so inserted content should use trailing space (not leading) for proper separation.
    /// </summary>
    [Fact]
    public void InsertAfter_QueryBetween_InsertsAfterRange()
    {
        // Test with whitespace-separated tokens where spaces are trailing trivia
        var tree = SyntaxTree.Parse("x a b c y");
        
        // Insert "AFTER " after the range a..c
        // - c has trailing trivia (space) -> "c "
        // - Insert "AFTER " (with trailing space for separation from y)
        // - y has no trivia -> "y"
        tree.CreateEditor()
            .InsertAfter(Q.Between(Q.Ident("a"), Q.Ident("c"), inclusive: true), "AFTER ")
            .Commit();
        
        Assert.Equal("x a b c AFTER y", tree.ToText());
    }
    
    /// <summary>
    /// Tests that InsertAfter preserves the target token's trailing trivia and
    /// uses trailing space on inserted content for proper separation.
    /// </summary>
    /// <remarks>
    /// In TinyAst's trivia model, same-line whitespace is trailing trivia on the preceding token.
    /// When inserting after a token, the token keeps its trailing trivia, so inserted content
    /// should use trailing whitespace (not leading) for proper spacing with following tokens.
    /// </remarks>
    [Fact]
    public void InsertAfter_RedNode_InMiddle_PreservesFollowingTrivia()
    {
        var tree = SyntaxTree.Parse("x a b c y");
        
        // Verify trivia model: each token (except last) has trailing whitespace
        var beforeTrivia = string.Join(", ", tree.Leaves.Select(l => 
            $"{l.Text}(T:{l.TrailingTriviaWidth})"));
        Assert.Equal("x(T:1), a(T:1), b(T:1), c(T:1), y(T:0)", beforeTrivia);
        
        var cNode = tree.Root.Children.First(n => n is SyntaxToken leaf && leaf.Text == "c");
        
        // Insert "AFTER " with trailing space for proper separation from y
        tree.CreateEditor()
            .InsertAfter(cNode, "AFTER ")
            .Commit();
        
        Assert.Equal("x a b c AFTER y", tree.ToText());
    }
    
    /// <summary>
    /// Tests that Edit with Query.Between transforms the concatenated content of the range.
    /// </summary>
    [Fact]
    public void Edit_QueryBetween_TransformsRangeContent()
    {
        var tree = SyntaxTree.Parse("x abc def ghi y");
        
        tree.CreateEditor()
            .Edit(Q.Between(Q.Ident("abc"), Q.Ident("ghi"), inclusive: true), content => content.ToUpper())
            .Commit();
        
        // The content between abc and ghi (inclusive) should be uppercased
        Assert.Equal("x ABC DEF GHI y", tree.ToText());
    }
    
    /// <summary>
    /// Tests Query.Sequence with SyntaxEditor Replace.
    /// </summary>
    [Fact]
    public void Replace_QuerySequence_ReplacesAllMatchedNodes()
    {
        var tree = SyntaxTree.Parse("a = 1 ; b = 2");
        
        // Replace sequence "a =" with "x ="
        // Note: '=' is parsed as an operator by default, not a symbol
        tree.CreateEditor()
            .Replace(Q.Sequence(Q.Ident("a"), Q.Operator("=")), "x =")
            .Commit();
        
        Assert.Equal("x = 1 ; b = 2", tree.ToText());
    }
    
    #endregion
}
