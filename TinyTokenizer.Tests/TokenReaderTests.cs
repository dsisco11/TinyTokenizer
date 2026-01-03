using System.Collections.Immutable;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the TokenReader ref struct.
/// </summary>
[Trait("Category", "Token")]
public class TokenReaderTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string input)
    {
        var tokenizer = new Tokenizer(input.AsMemory(), TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@', '$'));
        return tokenizer.Tokenize();
    }

    #endregion

    #region Constructor and Properties Tests

    [Fact]
    public void Constructor_WithSpan_InitializesCorrectly()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens.AsSpan());

        Assert.Equal(0, reader.Consumed);
        Assert.Equal(tokens.Length, reader.Remaining);
        Assert.Equal(tokens.Length, reader.Length);
        Assert.False(reader.End);
        Assert.False(reader.AutoSkipWhitespace);
    }

    [Fact]
    public void Constructor_WithImmutableArray_InitializesCorrectly()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);

        Assert.Equal(0, reader.Consumed);
        Assert.Equal(tokens.Length, reader.Remaining);
        Assert.False(reader.End);
    }

    [Fact]
    public void Constructor_WithAutoSkipWhitespace_SetsFlag()
    {
        var tokens = Tokenize("a b");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        Assert.True(reader.AutoSkipWhitespace);
    }

    [Fact]
    public void Current_ReturnsCurrentToken()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.NotNull(reader.Current);
        Assert.Equal("hello", reader.Current!.ContentSpan.ToString());
    }

    [Fact]
    public void Current_AtEnd_ReturnsNull()
    {
        var tokens = Tokenize("x");
        var reader = new TokenReader(tokens);
        reader.AdvanceToEnd();

        Assert.Null(reader.Current);
    }

    [Fact]
    public void UnreadSpan_ReturnsRemainingTokens()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);
        reader.Advance(2);

        var unread = reader.UnreadSpan;
        Assert.Equal(tokens.Length - 2, unread.Length);
    }

    #endregion

    #region TryRead/TryPeek Tests

    [Fact]
    public void TryRead_ReturnsTokenAndAdvances()
    {
        var tokens = Tokenize("hello world");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryRead(out var token));
        Assert.Equal("hello", token!.ContentSpan.ToString());
        Assert.Equal(1, reader.Consumed);
    }

    [Fact]
    public void TryRead_AtEnd_ReturnsFalse()
    {
        var tokens = Tokenize("x");
        var reader = new TokenReader(tokens);
        reader.AdvanceToEnd();

        Assert.False(reader.TryRead(out var token));
        Assert.Null(token);
    }

    [Fact]
    public void TryPeek_ReturnsTokenWithoutAdvancing()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryPeek(out var token));
        Assert.Equal("hello", token!.ContentSpan.ToString());
        Assert.Equal(0, reader.Consumed);
    }

    [Fact]
    public void TryPeek_WithOffset_ReturnsTokenAtOffset()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryPeek(2, out var token));
        Assert.Equal("b", token!.ContentSpan.ToString());
        Assert.Equal(0, reader.Consumed);
    }

    [Fact]
    public void TryReadExact_ReadsSpecifiedCount()
    {
        var tokens = Tokenize("a b c d");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryReadExact(3, out var read));
        Assert.Equal(3, read.Length);
        Assert.Equal(3, reader.Consumed);
    }

    [Fact]
    public void TryReadExact_NotEnoughTokens_ReturnsFalse()
    {
        var tokens = Tokenize("a");
        var reader = new TokenReader(tokens);

        Assert.False(reader.TryReadExact(5, out var read));
        Assert.True(read.IsEmpty);
    }

    #endregion

    #region Type-Safe Reading Tests

    [Fact]
    public void TryRead_Generic_MatchingType_ReturnsTypedToken()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryRead<IdentToken>(out var ident));
        Assert.Equal("hello", ident!.ContentSpan.ToString());
    }

    [Fact]
    public void TryRead_Generic_NonMatchingType_ReturnsFalse()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.False(reader.TryRead<NumericToken>(out var numeric));
        Assert.Null(numeric);
        Assert.Equal(0, reader.Consumed); // Should not advance
    }

    [Fact]
    public void TryPeek_Generic_MatchingType_ReturnsTypedToken()
    {
        var tokens = Tokenize("123");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryPeek<NumericToken>(out var numeric));
        Assert.Equal("123", numeric!.ContentSpan.ToString());
        Assert.Equal(0, reader.Consumed);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public void Advance_MovesPosition()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);

        reader.Advance(2);
        Assert.Equal(2, reader.Consumed);
    }

    [Fact]
    public void Advance_ClampsToEnd()
    {
        var tokens = Tokenize("a");
        var reader = new TokenReader(tokens);

        reader.Advance(100);
        Assert.Equal(tokens.Length, reader.Consumed);
        Assert.True(reader.End);
    }

    [Fact]
    public void Rewind_MovesPositionBack()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);
        reader.Advance(3);

        reader.Rewind(2);
        Assert.Equal(1, reader.Consumed);
    }

    [Fact]
    public void Rewind_ClampsToStart()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);
        reader.Advance(1);

        reader.Rewind(100);
        Assert.Equal(0, reader.Consumed);
    }

    [Fact]
    public void AdvanceToEnd_MovesToEnd()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);

        reader.AdvanceToEnd();
        Assert.True(reader.End);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Reset_MovesToStart()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);
        reader.AdvanceToEnd();

        reader.Reset();
        Assert.Equal(0, reader.Consumed);
        Assert.False(reader.End);
    }

    #endregion

    #region Skip Methods Tests

    [Fact]
    public void AdvancePast_SkipsMatchingTokens()
    {
        var tokens = Tokenize("   hello");
        var reader = new TokenReader(tokens);

        int skipped = reader.AdvancePast(TokenType.Whitespace);
        Assert.Equal(1, skipped);
        Assert.True(reader.TryPeek(out var token));
        Assert.Equal(TokenType.Ident, token!.Type);
    }

    [Fact]
    public void AdvancePastWhitespace_SkipsWhitespace()
    {
        var tokens = Tokenize("   hello");
        var reader = new TokenReader(tokens);

        reader.AdvancePastWhitespace();
        Assert.True(reader.TryPeek(out var token));
        Assert.Equal("hello", token!.ContentSpan.ToString());
    }

    [Fact]
    public void AdvancePastAny_SkipsMultipleTypes()
    {
        // Use two-level tokenizer to get proper comment parsing
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleMultiLine);
        var lexer = new Lexer(options);
        var parser = new TokenParser(options);
        var tokens = parser.ParseToArray(lexer.Lex(" /* comment */  hello"));
        var reader = new TokenReader(tokens);

        int skipped = reader.AdvancePastAny(TokenType.Whitespace, TokenType.Comment);
        Assert.True(skipped >= 1);
        Assert.True(reader.TryPeek(out var token));
        Assert.Equal("hello", token!.ContentSpan.ToString());
    }

    #endregion

    #region Delimiter Reading Tests

    [Fact]
    public void TryAdvanceTo_FindsTokenType()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryAdvanceTo(TokenType.Ident, advancePast: false));
        Assert.Equal(0, reader.Consumed); // First token is Ident
    }

    [Fact]
    public void TryAdvanceTo_AdvancesPast()
    {
        var tokens = Tokenize("hello world");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryAdvanceTo(TokenType.Whitespace, advancePast: true));
        Assert.True(reader.TryPeek(out var token));
        Assert.Equal("world", token!.ContentSpan.ToString());
    }

    [Fact]
    public void TryAdvanceTo_NotFound_ReturnsFalse()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.False(reader.TryAdvanceTo(TokenType.Numeric));
    }

    [Fact]
    public void TryReadTo_ReadsUpToDelimiter()
    {
        var tokens = Tokenize("a b ; c");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryReadTo(out var read, TokenType.Symbol));
        // a, whitespace, b, whitespace (before semicolon)
        Assert.Equal(4, read.Length);
    }

    #endregion

    #region Token-Specific Matching Tests

    [Fact]
    public void IsNext_MatchingType_ReturnsTrue()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNext(TokenType.Ident));
        Assert.Equal(0, reader.Consumed);
    }

    [Fact]
    public void IsNext_WithAdvancePast_Advances()
    {
        var tokens = Tokenize("hello world");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNext(TokenType.Ident, advancePast: true));
        Assert.Equal(1, reader.Consumed);
    }

    [Fact]
    public void IsNextSymbol_MatchingSymbol_ReturnsTrue()
    {
        var tokens = Tokenize(";");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNextSymbol(';'));
    }

    [Fact]
    public void IsNextSymbol_NonMatchingSymbol_ReturnsFalse()
    {
        var tokens = Tokenize(";");
        var reader = new TokenReader(tokens);

        Assert.False(reader.IsNextSymbol(','));
    }

    [Fact]
    public void IsNextOperator_MatchingOperator_ReturnsTrue()
    {
        var tokens = Tokenize("==");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNextOperator("=="));
    }

    [Fact]
    public void IsNextIdent_MatchingName_ReturnsTrue()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNextIdent("hello".AsSpan()));
    }

    [Fact]
    public void IsNextIdent_NonMatchingName_ReturnsFalse()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.False(reader.IsNextIdent("world".AsSpan()));
    }

    #endregion

    #region Block Handling Tests

    [Fact]
    public void TryReadBlock_ReadsAnyBlock()
    {
        var tokens = Tokenize("(content)");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryReadBlock(out var block));
        Assert.NotNull(block);
        Assert.Equal(TokenType.ParenthesisBlock, block!.Type);
    }

    [Fact]
    public void TryReadBlock_WithOpener_ReadsMatchingBlock()
    {
        var tokens = Tokenize("{content}");
        var reader = new TokenReader(tokens);

        Assert.True(reader.TryReadBlock('{', out var block));
        Assert.NotNull(block);
        Assert.Equal(TokenType.BraceBlock, block!.Type);
    }

    [Fact]
    public void TryReadBlock_WithWrongOpener_ReturnsFalse()
    {
        var tokens = Tokenize("(content)");
        var reader = new TokenReader(tokens);

        Assert.False(reader.TryReadBlock('{', out var block));
        Assert.Null(block);
        Assert.Equal(0, reader.Consumed);
    }

    [Fact]
    public void IsNextBlock_MatchingBlock_ReturnsTrue()
    {
        var tokens = Tokenize("[index]");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNextBlock('['));
    }

    [Fact]
    public void IsNextBlock_WithAdvancePast_Advances()
    {
        var tokens = Tokenize("(args) next");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNextBlock('(', advancePast: true));
        Assert.Equal(1, reader.Consumed);
    }

    #endregion

    #region Sequence Matching Tests

    [Fact]
    public void IsNextSequence_MatchingPattern_ReturnsTrue()
    {
        var tokens = Tokenize("hello world");
        var reader = new TokenReader(tokens);

        ReadOnlySpan<TokenType> pattern = [TokenType.Ident, TokenType.Whitespace, TokenType.Ident];
        Assert.True(reader.IsNextSequence(pattern));
    }

    [Fact]
    public void IsNextSequence_NonMatchingPattern_ReturnsFalse()
    {
        var tokens = Tokenize("hello world");
        var reader = new TokenReader(tokens);

        ReadOnlySpan<TokenType> pattern = [TokenType.Ident, TokenType.Ident];
        Assert.False(reader.IsNextSequence(pattern));
    }

    [Fact]
    public void IsNextSequence_WithAdvancePast_Advances()
    {
        var tokens = Tokenize("hello world");
        var reader = new TokenReader(tokens);

        ReadOnlySpan<TokenType> pattern = [TokenType.Ident, TokenType.Whitespace];
        Assert.True(reader.IsNextSequence(pattern, advancePast: true));
        Assert.Equal(2, reader.Consumed);
    }

    [Fact]
    public void IsNextSequence_EmptyPattern_ReturnsTrue()
    {
        var tokens = Tokenize("hello");
        var reader = new TokenReader(tokens);

        Assert.True(reader.IsNextSequence(ReadOnlySpan<TokenType>.Empty));
    }

    #endregion

    #region Auto-Skip Whitespace Tests

    [Fact]
    public void AutoSkipWhitespace_TryRead_SkipsWhitespace()
    {
        var tokens = Tokenize("   hello");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        Assert.True(reader.TryRead(out var token));
        Assert.Equal("hello", token!.ContentSpan.ToString());
    }

    [Fact]
    public void AutoSkipWhitespace_TryPeek_SkipsWhitespace()
    {
        var tokens = Tokenize("   hello");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        Assert.True(reader.TryPeek(out var token));
        Assert.Equal("hello", token!.ContentSpan.ToString());
    }

    [Fact]
    public void AutoSkipWhitespace_IsNext_SkipsWhitespace()
    {
        var tokens = Tokenize("   hello");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        Assert.True(reader.IsNext(TokenType.Ident));
    }

    [Fact]
    public void AutoSkipWhitespace_IsNextSymbol_SkipsWhitespace()
    {
        var tokens = Tokenize("   ;");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        Assert.True(reader.IsNextSymbol(';'));
    }

    [Fact]
    public void AutoSkipWhitespace_TryPeekWithOffset_CountsNonWhitespace()
    {
        var tokens = Tokenize("a   b   c");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        Assert.True(reader.TryPeek(1, out var token));
        Assert.Equal("b", token!.ContentSpan.ToString());
    }

    [Fact]
    public void AutoSkipWhitespace_IsNextSequence_SkipsWhitespace()
    {
        var tokens = Tokenize("a   b   c");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        ReadOnlySpan<TokenType> pattern = [TokenType.Ident, TokenType.Ident, TokenType.Ident];
        Assert.True(reader.IsNextSequence(pattern));
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void ParseFunctionCall_WithTokenReader()
    {
        var tokens = Tokenize("func(a, b)");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        // Read function name
        Assert.True(reader.TryRead<IdentToken>(out var funcName));
        Assert.Equal("func", funcName!.ContentSpan.ToString());

        // Read argument block
        Assert.True(reader.TryReadBlock('(', out var argsBlock));
        Assert.NotNull(argsBlock);
    }

    [Fact]
    public void ParseAssignment_WithTokenReader()
    {
        var tokens = Tokenize("x = 42");
        var reader = new TokenReader(tokens, autoSkipWhitespace: true);

        // Read target
        Assert.True(reader.TryRead<IdentToken>(out var target));
        Assert.Equal("x", target!.ContentSpan.ToString());

        // Check for equals operator
        Assert.True(reader.IsNextOperator("=", advancePast: true));

        // Read value
        Assert.True(reader.TryRead<NumericToken>(out var value));
        Assert.Equal("42", value!.ContentSpan.ToString());
    }

    [Fact]
    public void BacktrackOnFailure_WithRewind()
    {
        var tokens = Tokenize("a b c");
        var reader = new TokenReader(tokens);

        int startPosition = reader.Consumed;

        // Try to match a pattern that fails
        reader.TryRead(out _); // a
        reader.TryRead(out _); // whitespace
        
        // Pattern didn't match, rewind
        reader.Rewind(reader.Consumed - startPosition);

        Assert.Equal(0, reader.Consumed);
    }

    #endregion
}
