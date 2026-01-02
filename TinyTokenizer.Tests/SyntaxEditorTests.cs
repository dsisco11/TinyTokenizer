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
            .Replace(Q.AnyIdent.First(), "bar")
            .Commit();
        
        Assert.Equal("bar", tree.ToText());
    }

    [Fact]
    public void Replace_WithTransformer_AppliesTransformation()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Replace(Q.AnyIdent.First(), n => ((RedLeaf)n).Text.ToUpper())
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
                capturedText = ((RedLeaf)n).Text;
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
            .Insert(Q.AnyIdent.First().Before(), "hello ")
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }

    [Fact]
    public void Insert_After_InsertsAfterNode()
    {
        var tree = SyntaxTree.Parse("hello");
        
        tree.CreateEditor()
            .Insert(Q.AnyIdent.First().After(), " world")
            .Commit();
        
        Assert.Equal("hello world", tree.ToText());
    }

    [Fact]
    public void Insert_InnerStart_InsertsAtBlockStart()
    {
        var tree = SyntaxTree.Parse("{b}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "a ")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToText());
    }

    [Fact]
    public void Insert_InnerEnd_InsertsAtBlockEnd()
    {
        var tree = SyntaxTree.Parse("{a}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerEnd(), " b")
            .Commit();
        
        Assert.Equal("{a b}", tree.ToText());
    }

    [Fact]
    public void Insert_WithGreenNodes_InsertsProvidedNodes()
    {
        var tree = SyntaxTree.Parse("x");
        var lexer = new GreenLexer();
        var nodes = lexer.ParseToGreenNodes(" inserted");
        
        tree.CreateEditor()
            .Insert(Q.AnyIdent.First().After(), nodes)
            .Commit();
        
        Assert.Equal("x inserted", tree.ToText());
    }

    [Fact]
    public void Insert_BeforeMultiple_InsertsBeforeEach()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        tree.CreateEditor()
            .Insert(Q.AnyIdent.Before(), "_")
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
            .Insert(Q.BraceBlock.First().InnerStart(), "content")
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
            .Insert(Q.BraceBlock.First().InnerEnd(), " extra")
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
        var result3 = result2.Insert(Q.AnyIdent.First().Before(), "b");
        
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
            .Insert(Q.AnyIdent.First().Before(), "A")
            .Insert(Q.AnyIdent.First().After(), "B")
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
            .Replace(Q.AnyIdent.Where(n => n is RedLeaf l && l.Text.Length > 5), "X")
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
            .Insert(Q.BracketBlock.First().InnerStart(), "a ")
            .Commit();
        
        Assert.Equal("[a b]", tree.ToText());
    }

    [Fact]
    public void Insert_ParenBlock_InnerEnd()
    {
        var tree = SyntaxTree.Parse("(a)");
        
        tree.CreateEditor()
            .Insert(Q.ParenBlock.First().InnerEnd(), " b")
            .Commit();
        
        Assert.Equal("(a b)", tree.ToText());
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
            .Insert(Q.BraceBlock.First().Before(), "/* comment */")
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
            .Insert(Q.BraceBlock.First().InnerStart(), "first; ")
            .Commit();
        
        Assert.Equal("function {first; existing}", tree.ToText());
    }

    [Fact]
    public void Insert_AtFunctionEnd_InsertsBeforeClosingBrace()
    {
        // Simulates: inserting a return statement at the end of a function body
        var tree = SyntaxTree.Parse("function {existing}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerEnd(), " return")
            .Commit();
        
        Assert.Equal("function {existing return}", tree.ToText());
    }

    [Fact]
    public void Insert_AfterFunctionBlock_InsertsAfterClosingBrace()
    {
        // Simulates: inserting code after a function definition
        var tree = SyntaxTree.Parse("function {body}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().After(), " nextFunction")
            .Commit();
        
        Assert.Equal("function {body} nextFunction", tree.ToText());
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
            .Insert(Q.BraceBlock.First().InnerStart(), "first; ")
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
            .Insert(Q.BraceBlock.Nth(1).InnerStart(), "nested; ")
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
            .Insert(Q.BraceBlock.Before(), "/* fn */")
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
            .Insert(Q.BraceBlock.First().InnerStart(), "statement;")
            .Commit();
        
        Assert.Equal("function {statement;}", tree.ToText());
    }

    [Fact]
    public void Insert_FunctionWithWhitespace_PreservesFormatting()
    {
        // Test that whitespace/trivia is preserved correctly
        var tree = SyntaxTree.Parse("fn { body }");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "new; ")
            .Commit();
        
        var text = tree.ToText();
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
        
        Assert.Equal("prefix {only}", tree.ToText());
    }

    [Fact]
    public void Insert_AfterLastBlock_InsertsAtDocumentEnd()
    {
        var tree = SyntaxTree.Parse("{only}");
        
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().After(), " suffix")
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
            .OfType<RedBlock>()
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
            .Replace(worldNode, n => ((RedLeaf)n).Text.ToUpper())
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
            .Replace(nodes, n => $"[{((RedLeaf)n).Text}]")
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
            .Replace(queries, n => $"[{((RedLeaf)n).Text}]")
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
                var leaf = (RedLeaf)node;
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
            .Insert(Q.Syntax<TestTaggedNode>().Before(), "// comment\n")
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
}
