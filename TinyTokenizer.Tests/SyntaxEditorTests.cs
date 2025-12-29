using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the SyntaxEditor fluent editing API.
/// </summary>
public class SyntaxEditorTests
{
    #region Replace Operations

    [Fact]
    public void Replace_SingleNode_ChangesContent()
    {
        var tree = SyntaxTree.Parse("foo");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), "bar")
            .Commit();
        
        Assert.Equal("bar", tree.ToFullString());
    }

    [Fact]
    public void Replace_WithTransformer_AppliesTransformation()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), n => ((RedLeaf)n).Text.ToUpper())
            .Commit();
        
        Assert.Equal("HELLO", tree.ToFullString());
    }

    [Fact]
    public void Replace_MultipleNodes_ChangesAll()
    {
        var tree = SyntaxTree.Parse("a b a");
        
        tree.CreateEditor()
            .Replace(Q.Ident.WithText("a"), "X")
            .Commit();
        
        Assert.Equal("X b X", tree.ToFullString());
    }

    [Fact]
    public void Replace_WithGreenNodes_UsesProvidedNodes()
    {
        var tree = SyntaxTree.Parse("old");
        var lexer = new GreenLexer();
        var newNodes = lexer.ParseToGreenNodes("new");
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), newNodes)
            .Commit();
        
        Assert.Equal("new", tree.ToFullString());
    }

    [Fact]
    public void Replace_NoMatches_LeavesTreeUnchanged()
    {
        var tree = SyntaxTree.Parse("foo bar");
        
        tree.CreateEditor()
            .Replace(Q.Ident.WithText("nonexistent"), "replacement")
            .Commit();
        
        Assert.Equal("foo bar", tree.ToFullString());
    }

    [Fact]
    public void Replace_WithTransformer_ReceivesCorrectNode()
    {
        var tree = SyntaxTree.Parse("test");
        string? capturedText = null;
        
        tree.CreateEditor()
            .Replace(Q.Ident.First(), n =>
            {
                capturedText = ((RedLeaf)n).Text;
                return "replaced";
            })
            .Commit();
        
        Assert.Equal("test", capturedText);
        Assert.Equal("replaced", tree.ToFullString());
    }

    [Fact]
    public void Replace_Block_ReplacesEntireBlock()
    {
        var tree = SyntaxTree.Parse("{old}");
        
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First(), "{new}")
            .Commit();
        
        Assert.Equal("{new}", tree.ToFullString());
    }

    #endregion

    #region Remove Operations

    [Fact]
    public void Remove_SingleNode_DeletesNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Remove(Q.Ident.WithText("b"))
            .Commit();
        
        var text = tree.ToFullString();
        Assert.DoesNotContain("b", text);
    }

    [Fact]
    public void Remove_MultipleNodes_DeletesAll()
    {
        var tree = SyntaxTree.Parse("a b a c a");
        
        tree.CreateEditor()
            .Remove(Q.Ident.WithText("a"))
            .Commit();
        
        var text = tree.ToFullString();
        Assert.DoesNotContain("a", text);
        Assert.Contains("b", text);
        Assert.Contains("c", text);
    }

    [Fact]
    public void Remove_NoMatches_LeavesTreeUnchanged()
    {
        var tree = SyntaxTree.Parse("foo bar");
        
        tree.CreateEditor()
            .Remove(Q.Ident.WithText("nonexistent"))
            .Commit();
        
        Assert.Equal("foo bar", tree.ToFullString());
    }

    [Fact]
    public void Remove_Block_RemovesEntireBlock()
    {
        var tree = SyntaxTree.Parse("before {content} after");
        
        tree.CreateEditor()
            .Remove(Q.BraceBlock.First())
            .Commit();
        
        var text = tree.ToFullString();
        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("content", text);
    }

    [Fact]
    public void Remove_FirstOfMany_RemovesOnlyFirst()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Remove(Q.Ident.First())
            .Commit();
        
        var text = tree.ToFullString();
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
            .Insert(Q.Ident.First().Before(), "hello ")
            .Commit();
        
        Assert.Equal("hello world", tree.ToFullString());
    }

    [Fact]
    public void Insert_After_InsertsAfterNode()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Insert(Q.Ident.First().After(), " world")
            .Commit();
        
        Assert.Equal("hello world", tree.ToFullString());
    }

    [Fact]
    public void Insert_InnerStart_InsertsAtBlockStart()
    {
        var tree = SyntaxTree.Parse("{b}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "a ")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToFullString());
    }

    [Fact]
    public void Insert_InnerEnd_InsertsAtBlockEnd()
    {
        var tree = SyntaxTree.Parse("{a}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerEnd(), " b")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToFullString());
    }

    [Fact]
    public void Insert_WithGreenNodes_InsertsProvidedNodes()
    {
        var tree = SyntaxTree.Parse("x");
        var lexer = new GreenLexer();
        var nodes = lexer.ParseToGreenNodes(" inserted");
        
        tree.CreateEditor()
            .Insert(Q.Ident.First().After(), nodes)
            .Commit();
        
        Assert.Equal("x inserted", tree.ToFullString());
    }

    [Fact]
    public void Insert_BeforeMultiple_InsertsBeforeEach()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Insert(Q.Ident.Before(), "_")
            .Commit();
        
        var text = tree.ToFullString();
        Assert.Contains("_a", text);
        Assert.Contains("_b", text);
        Assert.Contains("_c", text);
    }

    [Fact]
    public void Insert_IntoEmptyBlock_AddsContent()
    {
        var tree = SyntaxTree.Parse("{}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "content")
            .Commit();
        
        Assert.Equal("{content}", tree.ToFullString());
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
        
        editor.Remove(Q.Ident.First());
        Assert.Equal(1, editor.PendingEditCount);
        Assert.True(editor.HasPendingEdits);
        
        editor.Remove(Q.Ident.First());
        Assert.Equal(2, editor.PendingEditCount);
    }

    [Fact]
    public void PendingEditCount_ResetsAfterCommit()
    {
        var tree = SyntaxTree.Parse("a b");
        var editor = tree.CreateEditor();
        
        editor.Remove(Q.Ident.First());
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
        
        editor.Remove(Q.Ident.First());
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
        editor.Replace(Q.Ident.First(), "changed");
        editor.Rollback();
        
        Assert.Equal("original", tree.ToFullString());
    }

    [Fact]
    public void Rollback_AllowsNewEdits()
    {
        var tree = SyntaxTree.Parse("original");
        
        var editor = tree.CreateEditor();
        editor.Replace(Q.Ident.First(), "discarded");
        editor.Rollback();
        
        editor.Replace(Q.Ident.First(), "new");
        editor.Commit();
        
        Assert.Equal("new", tree.ToFullString());
    }

    #endregion

    #region Commit and Undo Integration

    [Fact]
    public void Commit_SupportsUndo()
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

    [Fact]
    public void Commit_WithNoEdits_DoesNotAddUndoHistory()
    {
        var tree = SyntaxTree.Parse("unchanged");
        
        tree.CreateEditor().Commit();
        
        Assert.Equal("unchanged", tree.ToFullString());
        Assert.False(tree.CanUndo);
    }

    [Fact]
    public void Commit_SupportsRedo()
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
    public void MultipleCommits_CreateSeparateUndoSteps()
    {
        var tree = SyntaxTree.Parse("a");
        
        tree.CreateEditor().Replace(Q.Ident.First(), "b").Commit();
        tree.CreateEditor().Replace(Q.Ident.First(), "c").Commit();
        
        Assert.Equal("c", tree.ToFullString());
        
        tree.Undo();
        Assert.Equal("b", tree.ToFullString());
        
        tree.Undo();
        Assert.Equal("a", tree.ToFullString());
    }

    #endregion

    #region Batched Operations

    [Fact]
    public void BatchedOperations_ApplyAtomically()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Replace(Q.Ident.WithText("a"), "X")
            .Replace(Q.Ident.WithText("b"), "Y")
            .Replace(Q.Ident.WithText("c"), "Z")
            .Commit();
        
        Assert.Equal("X Y Z", tree.ToFullString());
    }

    [Fact]
    public void BatchedOperations_SingleUndoStep()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Replace(Q.Ident.WithText("a"), "X")
            .Replace(Q.Ident.WithText("b"), "Y")
            .Replace(Q.Ident.WithText("c"), "Z")
            .Commit();
        
        // All three changes should undo in one step
        tree.Undo();
        Assert.Equal("a b c", tree.ToFullString());
    }

    [Fact]
    public void MixedOperations_ApplyCorrectly()
    {
        var tree = SyntaxTree.Parse("a {b} c");
        
        tree.CreateEditor()
            .Remove(Q.Ident.WithText("a"))
            .Insert(Q.BraceBlock.First().InnerEnd(), " extra")
            .Replace(Q.Ident.WithText("c"), "z")
            .Commit();
        
        var text = tree.ToFullString();
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
        
        var result1 = editor.Replace(Q.Ident.First(), "a");
        var result2 = result1.Remove(Q.Ident.Last());
        var result3 = result2.Insert(Q.Ident.First().Before(), "b");
        
        Assert.Same(editor, result1);
        Assert.Same(editor, result2);
        Assert.Same(editor, result3);
    }

    [Fact]
    public void FluentApi_FullChain()
    {
        var tree = SyntaxTree.Parse("{x}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "start ")
            .Insert(Q.BraceBlock.First().InnerEnd(), " end")
            .Replace(Q.Ident.WithText("x"), "middle")
            .Commit();
        
        // Note: The order of operations may affect result
        var text = tree.ToFullString();
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
            .Remove(Q.Ident.First())
            .Commit();
        
        Assert.Equal("", tree.ToFullString());
    }

    [Fact]
    public void Edit_DeeplyNested_WorksCorrectly()
    {
        var tree = SyntaxTree.Parse("{{{deep}}}");
        
        tree.CreateEditor()
            .Replace(Q.Ident.WithText("deep"), "replaced")
            .Commit();
        
        Assert.Equal("{{{replaced}}}", tree.ToFullString());
    }

    [Fact]
    public void Edit_WithCustomOptions_UsesOptions()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        var tree = SyntaxTree.Parse("a + b", options);
        
        tree.CreateEditor(options)
            .Replace(Q.Operator.First(), "-")
            .Commit();
        
        Assert.Equal("a - b", tree.ToFullString());
    }

    [Fact]
    public void Insert_AdjacentPositions_HandlesCorrectly()
    {
        var tree = SyntaxTree.Parse("x");
        
        tree.CreateEditor()
            .Insert(Q.Ident.First().Before(), "A")
            .Insert(Q.Ident.First().After(), "B")
            .Commit();
        
        Assert.Equal("AxB", tree.ToFullString());
    }

    [Fact]
    public void Remove_AllNodes_ResultsInEmptyTree()
    {
        var tree = SyntaxTree.Parse("a");
        
        tree.CreateEditor()
            .Remove(Q.Ident.First())
            .Commit();
        
        Assert.Equal("", tree.ToFullString());
    }

    #endregion

    #region Query Integration

    [Fact]
    public void Edit_WithUnionQuery_AffectsBothTypes()
    {
        var tree = SyntaxTree.Parse("foo 42 bar");
        
        tree.CreateEditor()
            .Remove(Q.Ident | Q.Numeric)
            .Commit();
        
        // All idents and numbers removed, only whitespace remains
        var text = tree.ToFullString().Trim();
        Assert.DoesNotContain("foo", text);
        Assert.DoesNotContain("42", text);
        Assert.DoesNotContain("bar", text);
    }

    [Fact]
    public void Edit_WithFilteredQuery_OnlyAffectsMatches()
    {
        var tree = SyntaxTree.Parse("short verylongword tiny");
        
        tree.CreateEditor()
            .Replace(Q.Ident.Where(n => n is RedLeaf l && l.Text.Length > 5), "X")
            .Commit();
        
        var text = tree.ToFullString();
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
            .Replace(Q.Ident.Nth(2), "X")
            .Commit();
        
        Assert.Equal("a b X d e", tree.ToFullString());
    }

    #endregion

    #region Block-Specific Operations

    [Fact]
    public void Insert_BracketBlock_InnerStart()
    {
        var tree = SyntaxTree.Parse("[b]");
        
        tree.CreateEditor()
            .Insert(Q.BracketBlock.First().InnerStart(), "a ")
            .Commit();
        
        Assert.Equal("[a b]", tree.ToFullString());
    }

    [Fact]
    public void Insert_ParenBlock_InnerEnd()
    {
        var tree = SyntaxTree.Parse("(a)");
        
        tree.CreateEditor()
            .Insert(Q.ParenBlock.First().InnerEnd(), " b")
            .Commit();
        
        Assert.Equal("(a b)", tree.ToFullString());
    }

    #endregion

    #region Function-Like Block Insertion Scenarios

    [Fact]
    public void Insert_BeforeFunctionBlock_InsertsBeforeOpeningBrace()
    {
        // Simulates: inserting a comment or decorator before a function
        var tree = SyntaxTree.Parse("function {body}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().Before(), "/* comment */ ")
            .Commit();
        
        var text = tree.ToFullString();
        Assert.StartsWith("function /* comment */ {", text);
    }

    [Fact]
    public void Insert_AtFunctionStart_InsertsAfterOpeningBrace()
    {
        // Simulates: inserting a statement at the top of a function body
        var tree = SyntaxTree.Parse("function {existing}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "first; ")
            .Commit();
        
        Assert.Equal("function {first; existing}", tree.ToFullString());
    }

    [Fact]
    public void Insert_AtFunctionEnd_InsertsBeforeClosingBrace()
    {
        // Simulates: inserting a return statement at the end of a function body
        var tree = SyntaxTree.Parse("function {existing}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerEnd(), " return")
            .Commit();
        
        Assert.Equal("function {existing return}", tree.ToFullString());
    }

    [Fact]
    public void Insert_AfterFunctionBlock_InsertsAfterClosingBrace()
    {
        // Simulates: inserting code after a function definition
        var tree = SyntaxTree.Parse("function {body}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().After(), " nextFunction")
            .Commit();
        
        Assert.Equal("function {body} nextFunction", tree.ToFullString());
    }

    [Fact]
    public void Insert_MultipleFunctionPositions_AllInsertCorrectly()
    {
        // Simulates: inserting at multiple positions in a single commit
        var tree = SyntaxTree.Parse("fn {body}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().Before(), "/* before */ ")
            .Insert(Q.BraceBlock.First().InnerStart(), "start; ")
            .Insert(Q.BraceBlock.First().InnerEnd(), " end;")
            .Insert(Q.BraceBlock.First().After(), " /* after */")
            .Commit();
        
        var text = tree.ToFullString();
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
            .Insert(Q.BraceBlock.First().InnerStart(), "first; ")
            .Commit();
        
        var text = tree.ToFullString();
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
            .Insert(Q.BraceBlock.Nth(1).InnerStart(), "nested; ")
            .Commit();
        
        var text = tree.ToFullString();
        Assert.Contains("{nested; inner}", text);
    }

    [Fact]
    public void Insert_BeforeAndAfterMultipleFunctions_HandlesCorrectly()
    {
        // Simulates: inserting around multiple function definitions
        var tree = SyntaxTree.Parse("{first} {second}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.Before(), "/* fn */ ")
            .Commit();
        
        var text = tree.ToFullString();
        // Both blocks should have "/* fn */" inserted before them
        Assert.Equal("/* fn */ {first} /* fn */ {second}", text);
    }

    [Fact]
    public void Insert_EmptyFunctionBody_InsertsCorrectly()
    {
        // Simulates: adding content to an empty function body
        var tree = SyntaxTree.Parse("function {}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "statement;")
            .Commit();
        
        Assert.Equal("function {statement;}", tree.ToFullString());
    }

    [Fact]
    public void Insert_FunctionWithWhitespace_PreservesFormatting()
    {
        // Test that whitespace/trivia is preserved correctly
        var tree = SyntaxTree.Parse("fn { body }");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "new; ")
            .Commit();
        
        var text = tree.ToFullString();
        Assert.Contains("{new;", text);
        Assert.Contains("body", text);
    }

    [Fact]
    public void Insert_BeforeFirstBlock_InsertsAtDocumentStart()
    {
        var tree = SyntaxTree.Parse("{only}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().Before(), "prefix ")
            .Commit();
        
        Assert.Equal("prefix {only}", tree.ToFullString());
    }

    [Fact]
    public void Insert_AfterLastBlock_InsertsAtDocumentEnd()
    {
        var tree = SyntaxTree.Parse("{only}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().After(), " suffix")
            .Commit();
        
        Assert.Equal("{only} suffix", tree.ToFullString());
    }

    #endregion

    #region Replace Operations for Blocks

    [Fact]
    public void Replace_NestedBlock_PreservesOuter()
    {
        // Simpler case: replace inner content of a block
        var tree = SyntaxTree.Parse("{a}");
        
        tree.CreateEditor()
            .Replace(Q.Ident.WithText("a"), "replaced")
            .Commit();
        
        Assert.Equal("{replaced}", tree.ToFullString());
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
            .Replace(Q.Ident.WithText("inner"), "replaced")
            .Commit();
        
        var text = tree.ToFullString();
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
        
        editor1.Replace(Q.Ident.First(), "a");
        
        Assert.Equal(1, editor1.PendingEditCount);
        Assert.Equal(0, editor2.PendingEditCount);
    }

    #endregion
}
