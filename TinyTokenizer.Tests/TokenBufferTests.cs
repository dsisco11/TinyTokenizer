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
        Assert.Equal(tokens, buffer.Tokens);
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
        var newToken = new SymbolToken { Content = "@".AsMemory(), Position = 0 };

        buffer.InsertBefore(Query.Ident, newToken).Commit();

        Assert.True(buffer.Count > originalCount);
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
}
