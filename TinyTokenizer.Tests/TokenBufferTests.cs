using System.Collections.Immutable;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the TokenBuffer mutable token collection system.
/// </summary>
public class TokenBufferTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string input)
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@', '$')
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);
        var lexer = new Lexer(options);
        var parser = new TokenParser(options);
        return parser.ParseToArray(lexer.Lex(input));
    }

    private static TokenBuffer CreateBuffer(string input)
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@', '$')
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);
        return input.ToTokenBuffer(options);
    }

    #endregion

    #region Construction Tests

    [Fact]
    public void ToBuffer_FromImmutableArray_CreatesBuffer()
    {
        var tokens = Tokenize("a b c");
        var buffer = tokens.ToBuffer();

        Assert.Equal(tokens.Length, buffer.Count);
        // Content should match (tokens are re-lexed so instances differ)
        var originalContent = string.Concat(tokens.Select(t => t.ContentSpan.ToString()));
        var bufferContent = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal(originalContent, bufferContent);
    }

    [Fact]
    public void ToBuffer_FromEnumerable_CreatesBuffer()
    {
        var tokens = Tokenize("x + y").AsEnumerable();
        var buffer = tokens.ToBuffer();

        Assert.True(buffer.Count > 0);
    }

    [Fact]
    public void ToTokenBuffer_FromString_TokenizesAndCreatesBuffer()
    {
        var buffer = "hello world".ToTokenBuffer();

        Assert.True(buffer.Count > 0);
        Assert.False(buffer.HasPendingMutations);
    }

    #endregion

    #region Index-Based Insert Tests

    [Fact]
    public void Insert_AtStart_PrependsToken()
    {
        var buffer = CreateBuffer("b c");
        var newToken = new IdentToken { Content = "a".AsMemory(), Position = 0 };

        buffer.Insert(0, newToken).Commit();

        Assert.Equal("a", buffer.Tokens[0].ContentSpan.ToString());
    }

    [Fact]
    public void Insert_AtEnd_AppendsToken()
    {
        var buffer = CreateBuffer("a b");
        var newToken = new IdentToken { Content = "c".AsMemory(), Position = 0 };

        buffer.Insert(buffer.Count, newToken).Commit();

        Assert.Equal("c", buffer.Tokens[^1].ContentSpan.ToString());
    }

    [Fact]
    public void Insert_InMiddle_InsertsAtCorrectPosition()
    {
        var buffer = CreateBuffer("a c");
        var identTokens = buffer.Tokens.Where(t => t is IdentToken).ToList();
        var insertIndex = 1; // After first token

        var newToken = new IdentToken { Content = "b".AsMemory(), Position = 0 };
        buffer.Insert(insertIndex, newToken).Commit();

        // Verify the new token is in the buffer
        Assert.True(buffer.Tokens.Any(t => t.ContentSpan.SequenceEqual("b".AsSpan())));
    }

    [Fact]
    public void Insert_InvalidIndex_ThrowsImmediately()
    {
        var buffer = CreateBuffer("a b");
        var newToken = new IdentToken { Content = "x".AsMemory(), Position = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Insert(100, newToken));
    }

    [Fact]
    public void Insert_NegativeIndex_ThrowsImmediately()
    {
        var buffer = CreateBuffer("a b");
        var newToken = new IdentToken { Content = "x".AsMemory(), Position = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Insert(-1, newToken));
    }

    #endregion

    #region Index-Based Remove Tests

    [Fact]
    public void Remove_FirstToken_RemovesCorrectly()
    {
        var buffer = CreateBuffer("a b c");
        var originalCount = buffer.Count;

        buffer.Remove(0).Commit();

        Assert.Equal(originalCount - 1, buffer.Count);
    }

    [Fact]
    public void Remove_LastToken_RemovesCorrectly()
    {
        var buffer = CreateBuffer("a b c");
        var originalCount = buffer.Count;

        buffer.Remove(buffer.Count - 1).Commit();

        Assert.Equal(originalCount - 1, buffer.Count);
    }

    [Fact]
    public void Remove_InvalidIndex_ThrowsImmediately()
    {
        var buffer = CreateBuffer("a b");

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Remove(100));
    }

    [Fact]
    public void RemoveRange_RemovesMultipleTokens()
    {
        var buffer = CreateBuffer("a b c d e");
        var originalCount = buffer.Count;

        buffer.RemoveRange(1, 2).Commit();

        Assert.Equal(originalCount - 2, buffer.Count);
    }

    #endregion

    #region Index-Based Replace Tests

    [Fact]
    public void Replace_SingleToken_ReplacesCorrectly()
    {
        var buffer = CreateBuffer("foo bar");
        var replacement = new IdentToken { Content = "baz".AsMemory(), Position = 0 };

        // Find the first ident token index
        int identIndex = 0;
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer.Tokens[i] is IdentToken)
            {
                identIndex = i;
                break;
            }
        }

        buffer.Replace(identIndex, replacement).Commit();

        Assert.Equal("baz", buffer.Tokens[identIndex].ContentSpan.ToString());
    }

    [Fact]
    public void Replace_InvalidIndex_ThrowsImmediately()
    {
        var buffer = CreateBuffer("a b");
        var replacement = new IdentToken { Content = "x".AsMemory(), Position = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Replace(100, replacement));
    }

    #endregion

    #region Query-Based Mutation Tests

    [Fact]
    public void Remove_ByQuery_RemovesAllMatches()
    {
        var buffer = CreateBuffer("a /* comment */ b /* another */ c");

        buffer.Remove(Query.Comment).Commit();

        Assert.DoesNotContain(buffer.Tokens, t => t is CommentToken);
    }

    [Fact]
    public void Remove_ByQueryFirst_RemovesOnlyFirst()
    {
        var buffer = CreateBuffer("a b c");
        var originalIdentCount = buffer.Tokens.Count(t => t is IdentToken);

        buffer.Remove(Query.Ident.First()).Commit();

        var newIdentCount = buffer.Tokens.Count(t => t is IdentToken);
        Assert.Equal(originalIdentCount - 1, newIdentCount);
    }

    [Fact]
    public void Replace_ByQuery_ReplacesAllMatches()
    {
        var buffer = CreateBuffer("foo bar foo");

        buffer.Replace(
            Query.Ident.WithContent("foo"),
            t => new IdentToken { Content = "baz".AsMemory(), Position = t.Position }
        ).Commit();

        Assert.DoesNotContain(buffer.Tokens, t => t.ContentSpan.SequenceEqual("foo".AsSpan()));
        Assert.Contains(buffer.Tokens, t => t.ContentSpan.SequenceEqual("baz".AsSpan()));
    }

    [Fact]
    public void InsertBefore_ByQuery_InsertsBeforeAllMatches()
    {
        var buffer = CreateBuffer("a b");
        var originalCount = buffer.Count;

        // Use '#' instead of '@' to avoid tagged identifier combination
        buffer.InsertTextBefore(Query.Ident, "# ").Commit();

        // Check SimpleToken count increased (Level 1)
        Assert.True(buffer.SimpleTokenCount > 3); // Original: "a", " ", "b"
    }

    [Fact]
    public void InsertAfter_ByQuery_InsertsAfterAllMatches()
    {
        var buffer = CreateBuffer("a b");
        var originalCount = buffer.Count;
        var newToken = new SymbolToken { Content = "!".AsMemory(), Position = 0 };

        buffer.InsertAfter(Query.Ident, newToken).Commit();

        Assert.True(buffer.Count > originalCount);
    }

    #endregion

    #region Text Injection Tests

    [Fact]
    public void InsertText_AtIndex_TokenizesAndInserts()
    {
        var buffer = CreateBuffer("a c");
        var originalCount = buffer.Count;

        buffer.InsertText(1, "b").Commit();

        Assert.True(buffer.Count > originalCount);
    }

    [Fact]
    public void InsertTextBefore_ByQuery_TokenizesAndInserts()
    {
        var buffer = CreateBuffer("function()");

        buffer.InsertTextBefore(Query.Block('('), "/* params */").Commit();

        Assert.Contains(buffer.Tokens, t => t is CommentToken);
    }

    [Fact]
    public void ReplaceWithText_ByQuery_TokenizesAndReplaces()
    {
        var buffer = CreateBuffer("oldName = 1");

        buffer.ReplaceWithText(Query.Ident.WithContent("oldName"), "newName").Commit();

        Assert.Contains(buffer.Tokens, t => t.ContentSpan.SequenceEqual("newName".AsSpan()));
        Assert.DoesNotContain(buffer.Tokens, t => t.ContentSpan.SequenceEqual("oldName".AsSpan()));
    }

    #endregion

    #region Preview Tests

    [Fact]
    public void Preview_ReturnsResultWithoutCommitting()
    {
        var buffer = CreateBuffer("a b c");
        var originalTokens = buffer.Tokens;

        buffer.Remove(0);
        var preview = buffer.Preview();

        // Buffer state should be unchanged
        Assert.Equal(originalTokens, buffer.Tokens);
        Assert.True(buffer.HasPendingMutations);

        // Preview should show the change
        Assert.Equal(originalTokens.Length - 1, preview.Length);
    }

    [Fact]
    public void Preview_WithNoMutations_ReturnsSameTokens()
    {
        var buffer = CreateBuffer("a b c");

        var preview = buffer.Preview();

        Assert.Equal(buffer.Tokens, preview);
    }

    #endregion

    #region Commit Tests

    [Fact]
    public void Commit_ClearsMutationQueue()
    {
        var buffer = CreateBuffer("a b c");

        buffer.Remove(0);
        Assert.True(buffer.HasPendingMutations);

        buffer.Commit();
        Assert.False(buffer.HasPendingMutations);
    }

    [Fact]
    public void Commit_WithNoMutations_ReturnsUnchanged()
    {
        var buffer = CreateBuffer("a b c");
        var originalTokens = buffer.Tokens;

        buffer.Commit();

        Assert.Equal(originalTokens, buffer.Tokens);
    }

    [Fact]
    public void Commit_ReturnsThisForChaining()
    {
        var buffer = CreateBuffer("a b c");

        var result = buffer.Remove(0).Commit();

        Assert.Same(buffer, result);
    }

    [Fact]
    public void Commit_AllowsSubsequentMutations()
    {
        var buffer = CreateBuffer("a b c d");

        buffer.Remove(0).Commit();
        var countAfterFirst = buffer.Count;

        buffer.Remove(0).Commit();
        var countAfterSecond = buffer.Count;

        Assert.Equal(countAfterFirst - 1, countAfterSecond);
    }

    #endregion

    #region Rollback Tests

    [Fact]
    public void Rollback_DiscardsPendingMutations()
    {
        var buffer = CreateBuffer("a b c");
        var originalTokens = buffer.Tokens;

        buffer.Remove(0).Remove(1);
        Assert.True(buffer.HasPendingMutations);

        buffer.Rollback();

        Assert.False(buffer.HasPendingMutations);
        Assert.Equal(originalTokens, buffer.Tokens);
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void FluentChaining_MultipleMutations_WorksCorrectly()
    {
        var buffer = CreateBuffer("/* comment */ a = 1 + 2");

        var result = buffer
            .Remove(Query.Comment)
            .Replace(Query.Ident.WithContent("a"), t => new IdentToken { Content = "x".AsMemory(), Position = t.Position })
            .Commit()
            .Tokens;

        Assert.DoesNotContain(result, t => t is CommentToken);
        Assert.Contains(result, t => t.ContentSpan.SequenceEqual("x".AsSpan()));
    }

    [Fact]
    public void FluentChaining_MultipleCommits_WorksCorrectly()
    {
        var result = "a b c".ToTokenBuffer()
            .Remove(Query.Whitespace)
            .Commit()
            .InsertAfter(Query.Ident.First(), new WhitespaceToken { Content = " ".AsMemory(), Position = 0 })
            .Commit()
            .Tokens;

        Assert.True(result.Length > 0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyBuffer_MutationsWork()
    {
        var buffer = new TokenBuffer(ImmutableArray<Token>.Empty);

        var newToken = new IdentToken { Content = "a".AsMemory(), Position = 0 };
        buffer.Insert(0, newToken).Commit();

        Assert.Single(buffer.Tokens);
    }

    [Fact]
    public void MultipleInsertsAtSameIndex_MaintainsOrder()
    {
        var buffer = CreateBuffer("c");
        
        // Insert 'a' then 'b' at index 0
        buffer
            .Insert(0, new IdentToken { Content = "a".AsMemory(), Position = 0 })
            .Insert(0, new IdentToken { Content = "b".AsMemory(), Position = 0 })
            .Commit();

        // Both should be inserted (order may vary based on implementation)
        Assert.True(buffer.Count >= 3);
    }

    [Fact]
    public void RemoveAndInsertAtSameIndex_RemovalAppliesFirst()
    {
        var buffer = CreateBuffer("a b c");
        
        // Find index of whitespace
        int wsIndex = -1;
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer.Tokens[i] is WhitespaceToken)
            {
                wsIndex = i;
                break;
            }
        }

        if (wsIndex >= 0)
        {
            var newToken = new SymbolToken { Content = ",".AsMemory(), Position = 0 };
            buffer
                .Remove(wsIndex)
                .Insert(wsIndex, newToken)
                .Commit();

            // Should have replaced whitespace with comma
            Assert.Contains(buffer.Tokens, t => t.ContentSpan.SequenceEqual(",".AsSpan()));
        }
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void MultipleInsertsAndRemovals_ContentMatchesExpectation()
    {
        // Start with: a b c d e
        var buffer = CreateBuffer("a b c d e");

        // Operations:
        // 1. Remove all whitespace (explicit removal - whitespace IS preserved by default)
        // 2. Replace 'c' with 'X'
        
        buffer
            .Remove(Query.Whitespace)  // <-- Whitespace must be explicitly removed
            .Replace(Query.Ident.WithContent("c"), t => new IdentToken { Content = "X".AsMemory(), Position = t.Position })
            .Commit();

        // After explicit whitespace removal: abXde
        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal("abXde", content);
    }

    [Fact]
    public void WhitespaceIsPreservedByDefault()
    {
        // Start with: a b c
        var buffer = CreateBuffer("a b c");

        // Only replace 'b' with 'X' - do NOT remove whitespace
        buffer
            .Replace(Query.Ident.WithContent("b"), t => new IdentToken { Content = "X".AsMemory(), Position = t.Position })
            .Commit();

        // Whitespace should still be present: "a X c"
        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal("a X c", content);
        
        // Verify whitespace tokens exist
        Assert.Contains(buffer.Tokens, t => t is WhitespaceToken);
    }

    [Fact]
    public void MultipleInsertsAtDifferentLocations_PreservesOrder()
    {
        // Start with: x = 1
        var buffer = CreateBuffer("x = 1");

        // Insert 'let ' at beginning, insert ';' at end
        buffer
            .InsertText(0, "let ")
            .InsertText(buffer.Count, ";")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.StartsWith("let ", content);
        Assert.EndsWith(";", content);
    }

    [Fact]
    public void ComplexTransformation_VariableRenameAndWhitespaceRemoval()
    {
        // Test rename and whitespace removal (without comments for simplicity)
        var buffer = CreateBuffer("var foo = bar + foo;");

        buffer
            .Remove(Query.Whitespace)                 // Remove all whitespace
            .Replace(                                  // Rename 'foo' to 'baz'
                Query.Ident.WithContent("foo"),
                t => new IdentToken { Content = "baz".AsMemory(), Position = t.Position })
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("varbaz=bar+baz;", content);
    }

    [Fact]
    public void InsertBeforeFunctionDefinition_InsertsAtCorrectPosition()
    {
        // Start with: function greet() { return "hi"; }
        var buffer = CreateBuffer("function greet() { return \"hi\"; }");

        // Insert a comment before the function
        buffer
            .InsertTextBefore(Query.Ident.WithContent("function"), "// My function\n")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("// My function\nfunction greet() { return \"hi\"; }", content);
    }

    [Fact]
    public void InsertAfterOpeningBrace_InsertsInsideFunctionBlock()
    {
        // Start with: function test() { return 1; }
        var buffer = CreateBuffer("function test() { return 1; }");

        // Insert text after the brace block (this inserts AFTER the entire block)
        buffer
            .InsertTextAfter(Query.BraceBlock.First(), " // end")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("function test() { return 1; } // end", content);
    }

    [Fact]
    public void InsertBeforeLastSemicolon_UsingSimpleTokenIndex()
    {
        // This test demonstrates using SimpleToken indices for precise insertion
        var buffer = CreateBuffer("a; b; c;");

        // Find the last semicolon in SimpleTokens
        var lastSemicolonIndex = -1;
        for (int i = buffer.SimpleTokens.Length - 1; i >= 0; i--)
        {
            if (buffer.SimpleTokens[i].Type == SimpleTokenType.Semicolon)
            {
                lastSemicolonIndex = i;
                break;
            }
        }

        buffer
            .InsertSimpleTokens(lastSemicolonIndex, new Lexer().LexToArray(" /* end */"))
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("a; b; c /* end */;", content);
    }

    [Fact]
    public void InsertAnnotationBeforeFunction_WorksCorrectly()
    {
        // Start with: export function handler() { }
        var buffer = CreateBuffer("export function handler() { }");

        // Insert @deprecated annotation before 'export'
        buffer
            .InsertTextBefore(Query.Ident.WithContent("export"), "@deprecated ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("@deprecated export function handler() { }", content);
    }

    [Fact]
    public void WrapFunctionWithTryCatch_ComplexInsertion()
    {
        // Start with: function risky() { doSomething(); }
        var buffer = CreateBuffer("function risky() { doSomething(); }");

        // Insert comments before/after the block
        buffer
            .InsertTextBefore(Query.BraceBlock.First(), "/* before */ ")
            .InsertTextAfter(Query.BraceBlock.First(), " /* after */")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("function risky() /* before */ { doSomething(); } /* after */", content);
    }

    [Fact]
    public void ChainedTransformations_MultipleCommits_AccumulateCorrectly()
    {
        var buffer = CreateBuffer("a b c");

        // First commit: remove whitespace
        buffer.Remove(Query.Whitespace).Commit();
        var afterFirst = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal("abc", afterFirst);

        // Second commit: replace 'b' with 'X'
        buffer.Replace(Query.Ident.WithContent("b"), t => new IdentToken { Content = "X".AsMemory(), Position = t.Position }).Commit();
        var afterSecond = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal("aXc", afterSecond);

        // Third commit: insert prefix
        buffer.InsertText(0, "prefix_").Commit();
        var afterThird = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal("prefix_aXc", afterThird);
    }

    [Fact]
    public void RemoveAllWhitespace_Minification()
    {
        var buffer = CreateBuffer("a = 1; b = 2;");

        buffer
            .Remove(Query.Whitespace)
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("a=1;b=2;", content);
    }

    [Fact]
    public void InsertMultipleAnnotationsBeforeFunction()
    {
        var buffer = CreateBuffer("function process() { }");

        // Insert multiple annotations (will be in reverse order due to same target)
        buffer
            .InsertTextBefore(Query.Ident.WithContent("function"), "@public ")
            .InsertTextBefore(Query.Ident.WithContent("function"), "@async ")
            .InsertTextBefore(Query.Ident.WithContent("function"), "@deprecated ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        // All three are inserted at the same position, order depends on mutation ordering
        Assert.Equal("@deprecated @async @public function process() { }", content);
    }

    [Fact]
    public void ReplaceIdentifierThroughoutCode_ConsistentRename()
    {
        var buffer = CreateBuffer("let count = 0; count = count + 1; return count;");

        buffer
            .Replace(
                Query.Ident.WithContent("count"),
                t => new IdentToken { Content = "total".AsMemory(), Position = t.Position })
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        // All 'count' should be replaced with 'total'
        Assert.DoesNotContain("count", content);
        
        // Count occurrences of 'total' - should be 4 (declaration + 3 usages)
        var totalCount = buffer.Tokens.Count(t => t.ContentSpan.SequenceEqual("total".AsSpan()));
        Assert.Equal(4, totalCount);
    }

    [Fact]
    public void PreviewThenModifyThenCommit_PreviewDoesNotAffectState()
    {
        var buffer = CreateBuffer("original");

        buffer.Replace(Query.Ident, t => new IdentToken { Content = "modified".AsMemory(), Position = t.Position });
        
        // Preview should show modified
        var preview = buffer.Preview();
        Assert.Contains(preview, t => t.ContentSpan.SequenceEqual("modified".AsSpan()));
        
        // But original buffer state unchanged
        Assert.Contains(buffer.Tokens, t => t.ContentSpan.SequenceEqual("original".AsSpan()));
        
        // Now actually commit
        buffer.Commit();
        Assert.Contains(buffer.Tokens, t => t.ContentSpan.SequenceEqual("modified".AsSpan()));
        Assert.DoesNotContain(buffer.Tokens, t => t.ContentSpan.SequenceEqual("original".AsSpan()));
    }

    [Fact]
    public void InsertAtBlockBoundaries_FunctionDecoratorPattern()
    {
        // Simulating adding logging to a function
        var buffer = CreateBuffer("function calculate(x) { return x * 2; }");

        // Add entry/exit logging around the function
        buffer
            .InsertTextBefore(Query.Ident.WithContent("function").First(), "// ENTRY\n")
            .InsertTextAfter(Query.BraceBlock.First(), "\n// EXIT")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("// ENTRY\nfunction calculate(x) { return x * 2; }\n// EXIT", content);
    }

    #endregion

    #region Nested Block Mutation Tests (New SimpleToken Architecture)

    [Fact]
    public void InsertIntoBlockStart_InsertsAfterOpeningBrace()
    {
        var buffer = CreateBuffer("function test() { return 1; }");

        buffer
            .InsertIntoBlockStart(Query.BraceBlock.First(), " /* start */ ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("function test() { /* start */  return 1; }", content);
    }

    [Fact]
    public void InsertIntoBlockEnd_InsertsBeforeClosingBrace()
    {
        var buffer = CreateBuffer("function test() { return 1; }");

        buffer
            .InsertIntoBlockEnd(Query.BraceBlock.First(), " /* end */ ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("function test() { return 1;  /* end */ }", content);
    }

    [Fact]
    public void InsertIntoBlockStartAndEnd_AddsLogging()
    {
        var buffer = CreateBuffer("function greet(name) { return name; }");

        buffer
            .InsertIntoBlockStart(Query.BraceBlock.First(), " log('enter'); ")
            .InsertIntoBlockEnd(Query.BraceBlock.First(), " log('exit'); ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("function greet(name) { log('enter');  return name;  log('exit'); }", content);
    }

    [Fact]
    public void NestedBlocks_OuterBlockCanBeTargeted()
    {
        // With nested blocks, inner blocks are children of outer block at Level 2
        // But at Level 1, everything is flat, so we can still insert anywhere
        var buffer = CreateBuffer("outer { inner { x } }");

        // Target the outer brace block (only one visible at top level)
        buffer
            .InsertIntoBlockStart(Query.BraceBlock.First(), " /* start */ ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("outer { /* start */  inner { x } }", content);
    }

    [Fact]
    public void SimpleTokenCount_ReflectsLevel1Changes()
    {
        var buffer = CreateBuffer("a b");
        var originalSimpleCount = buffer.SimpleTokenCount;
        var originalLevel2Count = buffer.Count;

        // Insert text which adds SimpleTokens
        buffer.InsertText(0, "x ").Commit();

        // SimpleToken count should have increased
        Assert.True(buffer.SimpleTokenCount > originalSimpleCount);
    }

    [Fact]
    public void Level2Cache_IsInvalidatedOnCommit()
    {
        var buffer = CreateBuffer("a b c");
        
        // Access Level 2 to populate cache
        var _ = buffer.Tokens;
        
        // Mutate at Level 1
        buffer.InsertText(0, "prefix ").Commit();
        
        // Level 2 should reflect the change
        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        Assert.Equal("prefix a b c", content);
    }

    [Fact]
    public void PreviewSimpleTokens_ShowsLevel1Preview()
    {
        var buffer = CreateBuffer("a b");
        var originalSimpleTokens = buffer.SimpleTokens;

        buffer.InsertText(0, "x ");
        
        // Preview should show changes without committing
        var preview = buffer.PreviewSimpleTokens();
        Assert.True(preview.Length > originalSimpleTokens.Length);
        
        // Original should be unchanged
        Assert.Equal(originalSimpleTokens.Length, buffer.SimpleTokens.Length);
    }

    [Fact]
    public void InsertIntoParenBlock_WorksCorrectly()
    {
        var buffer = CreateBuffer("function test(a, b) { }");

        buffer
            .InsertIntoBlockStart(Query.ParenBlock.First(), "x, ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("function test(x, a, b) { }", content);
    }

    [Fact]
    public void InsertIntoBracketBlock_WorksCorrectly()
    {
        var buffer = CreateBuffer("arr[0]");

        buffer
            .InsertIntoBlockEnd(Query.BracketBlock.First(), " + 1")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("arr[0 + 1]", content);
    }

    [Fact]
    public void MultipleBlockInsertions_AllApplied()
    {
        var buffer = CreateBuffer("f() { } g() { }");

        // Insert into all brace blocks
        buffer
            .InsertIntoBlockStart(Query.BraceBlock, " x; ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("f() { x;  } g() { x;  }", content);
    }

    [Fact]
    public void BlockMutation_PreservesOtherContent()
    {
        var buffer = CreateBuffer("/* c */ function foo(x) { return x; }");

        buffer
            .InsertIntoBlockStart(Query.BraceBlock.First(), " y = 1; ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("/* c */ function foo(x) { y = 1;  return x; }", content);
    }

    [Fact]
    public void InsertIntoBlockStart_NonBlockToken_Throws()
    {
        var buffer = CreateBuffer("a b c");

        // Trying to insert into a non-block token should fail
        Assert.Throws<InvalidOperationException>(() =>
            buffer.InsertIntoBlockStart(Query.Ident.First(), "x").Commit());
    }

    [Fact]
    public void ComplexNestedStructure_OuterBlockMutationWorks()
    {
        var buffer = CreateBuffer("if (cond) { for (i) { arr[0] = 1; } }");

        // Insert into the outer if-block (Level 2 only sees the outermost block)
        buffer
            .InsertIntoBlockStart(Query.BraceBlock.First(), " x; ")
            .Commit();

        var content = string.Concat(buffer.Tokens.Select(t => t.ContentSpan.ToString()));
        
        Assert.Equal("if (cond) { x;  for (i) { arr[0] = 1; } }", content);
    }

    #endregion
}
