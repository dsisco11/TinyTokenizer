using System.Buffers;
using System.Collections.Immutable;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the SequenceTokenizer which uses SequenceReader&lt;char&gt; for parsing.
/// </summary>
public class SequenceTokenizerTests
{
    #region Helper Methods

    private static ParseResult TokenizeSequence(string source, TokenizerOptions? options = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return new ParseResult
            {
                Tokens = ImmutableArray<Token>.Empty,
                Status = ParseStatus.Complete
            };
        }

        var sequence = new ReadOnlySequence<char>(source.AsMemory());
        var tokenizer = new SequenceTokenizer(sequence, options);
        return tokenizer.Tokenize();
    }

    private static ParseResult TokenizeMultiSegment(string[] segments, TokenizerOptions? options = null)
    {
        // Create a multi-segment sequence
        var sequence = CreateMultiSegmentSequence(segments);
        var tokenizer = new SequenceTokenizer(sequence, options);
        return tokenizer.Tokenize();
    }

    private static ReadOnlySequence<char> CreateMultiSegmentSequence(string[] segments)
    {
        if (segments.Length == 0)
            return ReadOnlySequence<char>.Empty;

        if (segments.Length == 1)
            return new ReadOnlySequence<char>(segments[0].AsMemory());

        // Create linked memory segments
        var first = new MemorySegment<char>(segments[0].AsMemory());
        var current = first;

        for (int i = 1; i < segments.Length; i++)
        {
            current = current.Append(segments[i].AsMemory());
        }

        return new ReadOnlySequence<char>(first, 0, current, current.Memory.Length);
    }

    #endregion

    #region Basic Tokenization Tests (Parity with Tokenizer)

    [Fact]
    public void TokenizeSequence_EmptyString_ReturnsEmptyArray()
    {
        var result = TokenizeSequence("");

        Assert.Empty(result.Tokens);
        Assert.Equal(ParseStatus.Complete, result.Status);
    }

    [Fact]
    public void TokenizeSequence_PlainText_ReturnsSingleTextToken()
    {
        var result = TokenizeSequence("hello");

        Assert.Single(result.Tokens);
        var token = Assert.IsType<TextToken>(result.Tokens[0]);
        Assert.Equal("hello", token.ContentSpan.ToString());
        Assert.Equal(0, token.Position);
    }

    [Fact]
    public void TokenizeSequence_Whitespace_ReturnsSingleWhitespaceToken()
    {
        var result = TokenizeSequence("   ");

        Assert.Single(result.Tokens);
        var token = Assert.IsType<WhitespaceToken>(result.Tokens[0]);
        Assert.Equal("   ", token.ContentSpan.ToString());
    }

    [Fact]
    public void TokenizeSequence_Symbol_ReturnsSingleSymbolToken()
    {
        var result = TokenizeSequence("/");

        Assert.Single(result.Tokens);
        var token = Assert.IsType<SymbolToken>(result.Tokens[0]);
        Assert.Equal('/', token.Symbol);
    }

    [Fact]
    public void TokenizeSequence_TextWithWhitespace_ReturnsMixedTokens()
    {
        var result = TokenizeSequence("hello world");

        Assert.Equal(3, result.Tokens.Length);
        Assert.IsType<TextToken>(result.Tokens[0]);
        Assert.IsType<WhitespaceToken>(result.Tokens[1]);
        Assert.IsType<TextToken>(result.Tokens[2]);
    }

    #endregion

    #region Block Tokenization Tests

    [Fact]
    public void TokenizeSequence_SimpleBraceBlock_ReturnsBlockToken()
    {
        var result = TokenizeSequence("{content}");

        Assert.Single(result.Tokens);
        var block = Assert.IsType<BlockToken>(result.Tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);
        Assert.Equal("{content}", block.FullContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
        Assert.Equal(0, block.Position);
    }

    [Fact]
    public void TokenizeSequence_NestedBlocks_ReturnsRecursiveStructure()
    {
        var result = TokenizeSequence("{[()]}");

        Assert.Single(result.Tokens);
        var braceBlock = Assert.IsType<BlockToken>(result.Tokens[0]);
        Assert.Equal(TokenType.BraceBlock, braceBlock.Type);
        Assert.Single(braceBlock.Children);

        var bracketBlock = Assert.IsType<BlockToken>(braceBlock.Children[0]);
        Assert.Equal(TokenType.BracketBlock, bracketBlock.Type);
    }

    [Fact]
    public void TokenizeSequence_BlockWithChildren_ReturnsNestedTokens()
    {
        var result = TokenizeSequence("{hello world}");

        Assert.Single(result.Tokens);
        var block = Assert.IsType<BlockToken>(result.Tokens[0]);
        Assert.Equal(3, block.Children.Length);
    }

    #endregion

    #region String Literal Tests

    [Fact]
    public void TokenizeSequence_DoubleQuotedString_ReturnsStringToken()
    {
        var result = TokenizeSequence("\"hello world\"");

        Assert.Single(result.Tokens);
        var str = Assert.IsType<StringToken>(result.Tokens[0]);
        Assert.Equal("\"hello world\"", str.ContentSpan.ToString());
        Assert.Equal("hello world", str.Value.ToString());
        Assert.Equal('"', str.Quote);
    }

    [Fact]
    public void TokenizeSequence_SingleQuotedString_ReturnsStringToken()
    {
        var result = TokenizeSequence("'hello'");

        Assert.Single(result.Tokens);
        var str = Assert.IsType<StringToken>(result.Tokens[0]);
        Assert.Equal("'hello'", str.ContentSpan.ToString());
        Assert.Equal('\'', str.Quote);
    }

    [Fact]
    public void TokenizeSequence_StringWithEscape_PreservesEscape()
    {
        var result = TokenizeSequence("\"hello\\\"world\"");

        Assert.Single(result.Tokens);
        var str = Assert.IsType<StringToken>(result.Tokens[0]);
        Assert.Equal("\"hello\\\"world\"", str.ContentSpan.ToString());
    }

    #endregion

    #region Numeric Tests

    [Fact]
    public void TokenizeSequence_Integer_ReturnsNumericToken()
    {
        var result = TokenizeSequence("12345");

        Assert.Single(result.Tokens);
        var num = Assert.IsType<NumericToken>(result.Tokens[0]);
        Assert.Equal("12345", num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
    }

    [Fact]
    public void TokenizeSequence_FloatingPoint_ReturnsNumericToken()
    {
        var result = TokenizeSequence("123.456");

        Assert.Single(result.Tokens);
        var num = Assert.IsType<NumericToken>(result.Tokens[0]);
        Assert.Equal("123.456", num.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    #endregion

    #region Comment Tests

    [Fact]
    public void TokenizeSequence_SingleLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var result = TokenizeSequence("// this is a comment\ncode", options);

        Assert.Equal(3, result.Tokens.Length);
        var comment = Assert.IsType<CommentToken>(result.Tokens[0]);
        Assert.Equal("// this is a comment", comment.ContentSpan.ToString());
        Assert.False(comment.IsMultiLine);
    }

    [Fact]
    public void TokenizeSequence_MultiLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var result = TokenizeSequence("/* comment */", options);

        Assert.Single(result.Tokens);
        var comment = Assert.IsType<CommentToken>(result.Tokens[0]);
        Assert.Equal("/* comment */", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    #endregion

    #region Multi-Segment Sequence Tests

    [Fact]
    public void TokenizeSequence_MultiSegment_SimpleText_Works()
    {
        var result = TokenizeMultiSegment(["hel", "lo"]);

        Assert.Single(result.Tokens);
        var token = Assert.IsType<TextToken>(result.Tokens[0]);
        Assert.Equal("hello", token.ContentSpan.ToString());
    }

    [Fact]
    public void TokenizeSequence_MultiSegment_BlockAcrossSegments_Works()
    {
        var result = TokenizeMultiSegment(["{con", "tent}"]);

        Assert.Single(result.Tokens);
        var block = Assert.IsType<BlockToken>(result.Tokens[0]);
        Assert.Equal("{content}", block.FullContentSpan.ToString());
    }

    [Fact]
    public void TokenizeSequence_MultiSegment_MixedTokens_Works()
    {
        var result = TokenizeMultiSegment(["hello ", "world"]);

        Assert.Equal(3, result.Tokens.Length);
        Assert.IsType<TextToken>(result.Tokens[0]);
        Assert.IsType<WhitespaceToken>(result.Tokens[1]);
        Assert.IsType<TextToken>(result.Tokens[2]);
    }

    [Fact]
    public void TokenizeSequence_MultiSegment_StringAcrossSegments_Works()
    {
        var result = TokenizeMultiSegment(["\"hel", "lo\""]);

        Assert.Single(result.Tokens);
        var str = Assert.IsType<StringToken>(result.Tokens[0]);
        Assert.Equal("\"hello\"", str.ContentSpan.ToString());
    }

    #endregion

    #region Position Tracking Tests

    [Fact]
    public void TokenizeSequence_Position_TracksCorrectly()
    {
        var result = TokenizeSequence("abc def");

        Assert.Equal(3, result.Tokens.Length);
        Assert.Equal(0, result.Tokens[0].Position); // "abc"
        Assert.Equal(3, result.Tokens[1].Position); // " "
        Assert.Equal(4, result.Tokens[2].Position); // "def"
    }

    [Fact]
    public void TokenizeSequence_BlockPosition_TracksCorrectly()
    {
        var result = TokenizeSequence("x{y}z");

        Assert.Equal(3, result.Tokens.Length);
        Assert.Equal(0, result.Tokens[0].Position); // "x"
        Assert.Equal(1, result.Tokens[1].Position); // "{y}"
        Assert.Equal(4, result.Tokens[2].Position); // "z"
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void TokenizeSequence_UnexpectedClosingDelimiter_ReturnsErrorToken()
    {
        var result = TokenizeSequence("}");

        Assert.Single(result.Tokens);
        var error = Assert.IsType<ErrorToken>(result.Tokens[0]);
        Assert.Contains("Unexpected closing delimiter", error.ErrorMessage);
    }

    [Fact]
    public void TokenizeSequence_UnclosedBlock_ReturnsErrorToken()
    {
        var result = TokenizeSequence("{");

        Assert.Single(result.Tokens);
        var error = Assert.IsType<ErrorToken>(result.Tokens[0]);
        Assert.Contains("Unclosed block", error.ErrorMessage);
    }

    [Fact]
    public void TokenizeSequence_ErrorRecovery_ContinuesFromNextCharacter()
    {
        var result = TokenizeSequence("}hello");

        Assert.Equal(2, result.Tokens.Length);
        Assert.IsType<ErrorToken>(result.Tokens[0]);
        Assert.IsType<TextToken>(result.Tokens[1]);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void ReadOnlySequence_Tokenize_Extension_Works()
    {
        var sequence = new ReadOnlySequence<char>("hello world".AsMemory());
        var result = sequence.Tokenize();

        Assert.Equal(3, result.Tokens.Length);
    }

    [Fact]
    public void ReadOnlyMemory_TokenizeAsSequence_Extension_Works()
    {
        var memory = "hello world".AsMemory();
        var result = memory.TokenizeAsSequence();

        Assert.Equal(3, result.Tokens.Length);
    }

    [Fact]
    public void String_TokenizeAsSequence_Extension_Works()
    {
        var result = "hello world".TokenizeAsSequence();

        Assert.Equal(3, result.Tokens.Length);
    }

    #endregion
}

/// <summary>
/// Helper class for creating multi-segment sequences in tests.
/// </summary>
internal sealed class MemorySegment<T> : ReadOnlySequenceSegment<T>
{
    public MemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public MemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new MemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };
        Next = segment;
        return segment;
    }
}
