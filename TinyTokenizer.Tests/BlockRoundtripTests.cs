using System.Collections.Immutable;
using System.Text;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for verifying that deeply nested blocks can roundtrip serialize correctly.
/// </summary>
public class BlockRoundtripTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string source, TokenizerOptions? options = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return ImmutableArray<Token>.Empty;
        }

        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);

        var simpleTokens = lexer.Lex(source);
        return parser.ParseToArray(simpleTokens);
    }

    private static string Roundtrip(string source, TokenizerOptions? options = null)
    {
        var tokens = Tokenize(source, options);
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            sb.Append(token.ToText());
        }
        return sb.ToString();
    }

    private static void AssertRoundtrip(string source, TokenizerOptions? options = null)
    {
        var result = Roundtrip(source, options);
        Assert.Equal(source, result);
    }

    #endregion

    #region Simple Block Roundtrip Tests

    [Fact]
    public void SimpleBlock_BraceBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{content}");
    }

    [Fact]
    public void SimpleBlock_BracketBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("[content]");
    }

    [Fact]
    public void SimpleBlock_ParenthesisBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("(content)");
    }

    [Fact]
    public void SimpleBlock_EmptyBraceBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{}");
    }

    [Fact]
    public void SimpleBlock_EmptyBracketBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("[]");
    }

    [Fact]
    public void SimpleBlock_EmptyParenthesisBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("()");
    }

    [Fact]
    public void SimpleBlock_BlockWithWhitespace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{ content with spaces }");
    }

    #endregion

    #region Two-Level Nesting Tests

    [Fact]
    public void NestedBlocks_TwoLevel_BraceInBrace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{inner}}");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_BracketInBrace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{[inner]}");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_ParenInBrace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{(inner)}");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_BraceInBracket_RoundtripsCorrectly()
    {
        AssertRoundtrip("[{inner}]");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_BraceInParen_RoundtripsCorrectly()
    {
        AssertRoundtrip("({inner})");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_AllThreeTypes_RoundtripsCorrectly()
    {
        AssertRoundtrip("{[()]}");
    }

    #endregion

    #region Three-Level Nesting Tests

    [Fact]
    public void NestedBlocks_ThreeLevel_SameType_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{{innermost}}}");
    }

    [Fact]
    public void NestedBlocks_ThreeLevel_MixedTypes_RoundtripsCorrectly()
    {
        AssertRoundtrip("{[(innermost)]}");
    }

    [Fact]
    public void NestedBlocks_ThreeLevel_WithContent_RoundtripsCorrectly()
    {
        AssertRoundtrip("{outer [middle (inner content) middle] outer}");
    }

    #endregion

    #region Deep Nesting Tests

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DeepNesting_BraceOnly_RoundtripsCorrectly(int depth)
    {
        var opening = new string('{', depth);
        var closing = new string('}', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DeepNesting_BracketOnly_RoundtripsCorrectly(int depth)
    {
        var opening = new string('[', depth);
        var closing = new string(']', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DeepNesting_ParenOnly_RoundtripsCorrectly(int depth)
    {
        var opening = new string('(', depth);
        var closing = new string(')', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(15)]
    [InlineData(30)]
    public void DeepNesting_AlternatingTypes_RoundtripsCorrectly(int depth)
    {
        var sb = new StringBuilder();
        var openers = new[] { '{', '[', '(' };
        var closers = new[] { '}', ']', ')' };

        for (int i = 0; i < depth; i++)
        {
            sb.Append(openers[i % 3]);
        }
        sb.Append("innermost");
        for (int i = depth - 1; i >= 0; i--)
        {
            sb.Append(closers[i % 3]);
        }

        var source = sb.ToString();
        AssertRoundtrip(source);
    }

    #endregion

    #region Complex Structure Tests

    [Fact]
    public void ComplexStructure_SiblingBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a}{b}{c}");
    }

    [Fact]
    public void ComplexStructure_MixedSiblingBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{braces}[brackets](parens)");
    }

    [Fact]
    public void ComplexStructure_NestedSiblings_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{a}{b}}");
    }

    [Fact]
    public void ComplexStructure_MultipleSiblingsAtEachLevel_RoundtripsCorrectly()
    {
        AssertRoundtrip("{outer1 {inner1} {inner2} outer2}");
    }

    [Fact]
    public void ComplexStructure_DeepWithSiblings_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a {b [c (d) c] b} a}");
    }

    [Fact]
    public void ComplexStructure_TreeShape_RoundtripsCorrectly()
    {
        // Tree-like structure with branching at multiple levels
        AssertRoundtrip("{root {left {ll}{lr}} {right {rl}{rr}}}");
    }

    #endregion

    #region Content Within Blocks Tests

    [Fact]
    public void ContentWithinBlocks_Identifiers_RoundtripsCorrectly()
    {
        AssertRoundtrip("{foo bar baz}");
    }

    [Fact]
    public void ContentWithinBlocks_Whitespace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{  \t  \n  }");
    }

    [Fact]
    public void ContentWithinBlocks_Strings_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\"string content\"}");
    }

    [Fact]
    public void ContentWithinBlocks_Numbers_RoundtripsCorrectly()
    {
        AssertRoundtrip("{123 456.789 .5}");
    }

    [Fact]
    public void ContentWithinBlocks_Operators_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        AssertRoundtrip("{a == b && c != d}", options);
    }

    [Fact]
    public void ContentWithinBlocks_Comments_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);
        AssertRoundtrip("{/* comment */}", options);
    }

    [Fact]
    public void ContentWithinBlocks_MixedContent_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleSingleLine);

        AssertRoundtrip("{foo = 123; // comment\n}", options);
    }

    #endregion

    #region Real-World Code Patterns Tests

    [Fact]
    public void RealWorld_FunctionBody_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleSingleLine);

        var source = @"{
    var x = 10;
    return x + 1;
}";
        AssertRoundtrip(source, options);
    }

    [Fact]
    public void RealWorld_NestedFunction_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily);

        var source = @"{
    function inner() {
        return {
            value: 42
        };
    }
}";
        AssertRoundtrip(source, options);
    }

    [Fact]
    public void RealWorld_JsonLikeStructure_RoundtripsCorrectly()
    {
        var source = @"{
    ""name"": ""test"",
    ""items"": [
        { ""id"": 1 },
        { ""id"": 2 }
    ],
    ""nested"": {
        ""deep"": {
            ""value"": 123
        }
    }
}";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_ArrayOfArrays_RoundtripsCorrectly()
    {
        var source = "[[1, 2], [3, 4], [[5, 6], [7, 8]]]";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_FunctionCallChain_RoundtripsCorrectly()
    {
        var source = "foo(bar(baz(qux())))";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_MixedExpression_RoundtripsCorrectly()
    {
        var source = "array[index].method(arg1, arg2).property";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_ClassLikeStructure_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);

        var source = @"{
    /* Constructor */
    constructor(name) {
        this.name = name;
    }

    // Method
    greet() {
        return (""Hello, "" + this.name);
    }

    // Nested class
    Inner {
        value = [1, 2, 3];
    }
}";
        AssertRoundtrip(source, options);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void EdgeCase_EmptyNestedBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{{}}}");
    }

    [Fact]
    public void EdgeCase_BlockFollowedByContent_RoundtripsCorrectly()
    {
        AssertRoundtrip("{block}content");
    }

    [Fact]
    public void EdgeCase_ContentFollowedByBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("content{block}");
    }

    [Fact]
    public void EdgeCase_BlocksWithPunctuation_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a;b;c}");
    }

    [Fact]
    public void EdgeCase_DeepEmptyBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{{{{}}}}}");;
    }

    [Fact]
    public void EdgeCase_SingleCharContentAtEachLevel_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a{b{c{d}c}b}a}");
    }

    #endregion

    #region IBufferWriter Serialization Tests

    [Fact]
    public void BufferWriter_SimpleBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        var buffer = new System.Buffers.ArrayBufferWriter<char>();
        tokens[0].WriteTo(buffer);

        Assert.Equal("{content}", new string(buffer.WrittenSpan));
    }

    [Fact]
    public void BufferWriter_NestedBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{{inner}}");
        Assert.Single(tokens);

        var buffer = new System.Buffers.ArrayBufferWriter<char>();
        tokens[0].WriteTo(buffer);

        Assert.Equal("{{inner}}", new string(buffer.WrittenSpan));
    }

    #endregion

    #region StringBuilder Serialization Tests

    [Fact]
    public void StringBuilder_SimpleBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        var sb = new StringBuilder();
        tokens[0].WriteTo(sb);

        Assert.Equal("{content}", sb.ToString());
    }

    [Fact]
    public void StringBuilder_DeepNesting_WritesCorrectly()
    {
        var source = "{{{{{innermost}}}}}";
        var tokens = Tokenize(source);
        Assert.Single(tokens);

        var sb = new StringBuilder();
        tokens[0].WriteTo(sb);

        Assert.Equal(source, sb.ToString());
    }

    #endregion

    #region TryWriteTo Tests

    [Fact]
    public void TryWriteTo_SimpleBlock_SucceedsWithAdequateBuffer()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        Span<char> buffer = stackalloc char[50];
        var success = tokens[0].TryWriteTo(buffer, out var written);

        Assert.True(success);
        Assert.Equal("{content}", new string(buffer[..written]));
    }

    [Fact]
    public void TryWriteTo_SimpleBlock_FailsWithSmallBuffer()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        Span<char> buffer = stackalloc char[3]; // Too small
        var success = tokens[0].TryWriteTo(buffer, out var written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    #endregion

    #region TextWriter Serialization Tests

    [Fact]
    public void TextWriter_SimpleBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        using var sw = new StringWriter();
        tokens[0].WriteTo(sw);

        Assert.Equal("{content}", sw.ToString());
    }

    [Fact]
    public void TextWriter_ComplexNesting_WritesCorrectly()
    {
        var source = "{outer {middle [inner (deepest) inner] middle} outer}";
        var tokens = Tokenize(source);

        using var sw = new StringWriter();
        foreach (var token in tokens)
        {
            token.WriteTo(sw);
        }

        Assert.Equal(source, sw.ToString());
    }

    #endregion

    #region TextLength Property Tests

    [Fact]
    public void TextLength_SimpleBlock_ReturnsCorrectLength()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        Assert.Equal(9, tokens[0].TextLength); // "{content}" = 9 chars
    }

    [Fact]
    public void TextLength_NestedBlock_ReturnsCorrectLength()
    {
        var source = "{{inner}}";
        var tokens = Tokenize(source);
        Assert.Single(tokens);

        Assert.Equal(source.Length, tokens[0].TextLength);
    }

    [Fact]
    public void TextLength_DeepNesting_ReturnsCorrectLength()
    {
        var source = "{{{{{innermost}}}}}";
        var tokens = Tokenize(source);
        Assert.Single(tokens);

        Assert.Equal(source.Length, tokens[0].TextLength);
    }

    #endregion

    #region Special Characters Within Blocks Tests

    [Fact]
    public void SpecialChars_StringWithBracesInBlock_RoundtripsCorrectly()
    {
        // String containing braces should not affect block parsing
        AssertRoundtrip("{\"{}[]()\"}");
    }

    [Fact]
    public void SpecialChars_EscapedQuoteInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\"hello\\\"world\"}");
    }

    [Fact]
    public void SpecialChars_NewlinesInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\n\n\n}");
    }

    [Fact]
    public void SpecialChars_TabsInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\t\t\t}");
    }

    [Fact]
    public void SpecialChars_MixedWhitespaceInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{ \t \n \r\n }");
    }

    #endregion

    #region Stress Tests

    [Fact]
    public void Stress_VeryDeepNesting_RoundtripsCorrectly()
    {
        const int depth = 100;
        var opening = new string('{', depth);
        var closing = new string('}', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Fact]
    public void Stress_WideTree_RoundtripsCorrectly()
    {
        // Create a block with many siblings at each level
        var sb = new StringBuilder();
        sb.Append('{');
        for (int i = 0; i < 50; i++)
        {
            sb.Append($"{{child{i}}}");
        }
        sb.Append('}');

        var source = sb.ToString();
        AssertRoundtrip(source);
    }

    [Fact]
    public void Stress_LargeContent_RoundtripsCorrectly()
    {
        var content = new string('x', 10000);
        var source = $"{{{content}}}";

        AssertRoundtrip(source);
    }

    [Fact]
    public void Stress_ManyNestedBlocksWithContent_RoundtripsCorrectly()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 20; i++)
        {
            sb.Append('{');
            sb.Append($"level{i} ");
        }
        sb.Append("innermost");
        for (int i = 19; i >= 0; i--)
        {
            sb.Append($" level{i}");
            sb.Append('}');
        }

        var source = sb.ToString();
        AssertRoundtrip(source);
    }

    #endregion
}
