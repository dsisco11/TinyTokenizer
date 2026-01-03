using System.Collections.Immutable;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the two-level tokenizer architecture (Lexer + TokenParser).
/// </summary>
public class LexerParserTests
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

    #endregion

    #region Lexer Tests

    [Fact]
    public void Lexer_EmptyString_ReturnsEmptyArray()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("");

        Assert.Empty(tokens);
    }

    [Fact]
    public void Lexer_PlainText_ReturnsSingleIdentToken()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("hello");

        Assert.Single(tokens);
        Assert.Equal(SimpleTokenType.Ident, tokens[0].Type);
        Assert.Equal("hello", tokens[0].ContentSpan.ToString());
    }

    [Fact]
    public void Lexer_Digits_ReturnsSingleDigitsToken()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("12345");

        Assert.Single(tokens);
        Assert.Equal(SimpleTokenType.Digits, tokens[0].Type);
        Assert.Equal("12345", tokens[0].ContentSpan.ToString());
    }

    [Fact]
    public void Lexer_Dot_ReturnsSingleDotToken()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray(".");

        Assert.Single(tokens);
        Assert.Equal(SimpleTokenType.Dot, tokens[0].Type);
    }

    [Fact]
    public void Lexer_DecimalNumber_ReturnsDigitsDotDigits()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("123.456");

        Assert.Equal(3, tokens.Length);
        Assert.Equal(SimpleTokenType.Digits, tokens[0].Type);
        Assert.Equal(SimpleTokenType.Dot, tokens[1].Type);
        Assert.Equal(SimpleTokenType.Digits, tokens[2].Type);
    }

    [Fact]
    public void Lexer_Whitespace_ReturnsSingleWhitespaceToken()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("   ");

        Assert.Single(tokens);
        Assert.Equal(SimpleTokenType.Whitespace, tokens[0].Type);
    }

    [Fact]
    public void Lexer_Newline_ReturnsSingleNewlineToken()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("\n");

        Assert.Single(tokens);
        Assert.Equal(SimpleTokenType.Newline, tokens[0].Type);
    }

    [Fact]
    public void Lexer_CrLf_ReturnsSingleNewlineToken()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("\r\n");

        Assert.Single(tokens);
        Assert.Equal(SimpleTokenType.Newline, tokens[0].Type);
        Assert.Equal(2, tokens[0].Length);
    }

    [Fact]
    public void Lexer_Braces_ReturnsSeparateTokens()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("{}");

        Assert.Equal(2, tokens.Length);
        Assert.Equal(SimpleTokenType.OpenBrace, tokens[0].Type);
        Assert.Equal(SimpleTokenType.CloseBrace, tokens[1].Type);
    }

    [Fact]
    public void Lexer_AllDelimiters_ReturnsCorrectTypes()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("{}[]()");

        Assert.Equal(6, tokens.Length);
        Assert.Equal(SimpleTokenType.OpenBrace, tokens[0].Type);
        Assert.Equal(SimpleTokenType.CloseBrace, tokens[1].Type);
        Assert.Equal(SimpleTokenType.OpenBracket, tokens[2].Type);
        Assert.Equal(SimpleTokenType.CloseBracket, tokens[3].Type);
        Assert.Equal(SimpleTokenType.OpenParen, tokens[4].Type);
        Assert.Equal(SimpleTokenType.CloseParen, tokens[5].Type);
    }

    [Fact]
    public void Lexer_Quotes_ReturnsSeparateTokens()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("\"'");

        Assert.Equal(2, tokens.Length);
        Assert.Equal(SimpleTokenType.DoubleQuote, tokens[0].Type);
        Assert.Equal(SimpleTokenType.SingleQuote, tokens[1].Type);
    }

    [Fact]
    public void Lexer_CommentChars_ReturnsSeparateTokens()
    {
        var lexer = new Lexer();
        var tokens = lexer.LexToArray("/*");

        Assert.Equal(2, tokens.Length);
        Assert.Equal(SimpleTokenType.Slash, tokens[0].Type);
        Assert.Equal(SimpleTokenType.Asterisk, tokens[1].Type);
    }

    #endregion

    #region TokenParser Integration Tests

    [Fact]
    public void TokenParser_EmptyString_ReturnsEmptyArray()
    {
        var tokens = Tokenize("");
        Assert.Empty(tokens);
    }

    [Fact]
    public void TokenParser_PlainText_ReturnsSingleIdentToken()
    {
        var tokens = Tokenize("hello");

        Assert.Single(tokens);
        var token = Assert.IsType<IdentToken>(tokens[0]);
        Assert.Equal("hello", token.ContentSpan.ToString());
        Assert.Equal(0, token.Position);
    }

    [Fact]
    public void TokenParser_Whitespace_ReturnsSingleWhitespaceToken()
    {
        var tokens = Tokenize("   ");

        Assert.Single(tokens);
        var token = Assert.IsType<WhitespaceToken>(tokens[0]);
        Assert.Equal("   ", token.ContentSpan.ToString());
    }

    [Fact]
    public void TokenParser_Symbol_ReturnsSingleSymbolToken()
    {
        var tokens = Tokenize(":");

        Assert.Single(tokens);
        var token = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal(':', token.Symbol);
    }

    [Fact]
    public void TokenParser_TextWithWhitespace_ReturnsMixedTokens()
    {
        var tokens = Tokenize("hello world");

        Assert.Equal(3, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
    }

    #endregion

    #region Block Tokenization Tests

    [Fact]
    public void TokenParser_SimpleBraceBlock_ReturnsSimpleBlock()
    {
        var tokens = Tokenize("{content}");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);
        Assert.Equal("{content}", block.ContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
        Assert.Equal(0, block.Position);
    }

    [Fact]
    public void TokenParser_NestedBlocks_ReturnsRecursiveStructure()
    {
        var tokens = Tokenize("{[()]}");

        Assert.Single(tokens);
        var braceBlock = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, braceBlock.Type);
        Assert.Single(braceBlock.Children);

        var bracketBlock = Assert.IsType<SimpleBlock>(braceBlock.Children[0]);
        Assert.Equal(TokenType.BracketBlock, bracketBlock.Type);
    }

    [Fact]
    public void TokenParser_BlockWithChildren_ReturnsNestedTokens()
    {
        var tokens = Tokenize("{hello world}");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(3, block.Children.Length);
    }

    #endregion

    #region String Literal Tests

    [Fact]
    public void TokenParser_DoubleQuotedString_ReturnsStringToken()
    {
        var tokens = Tokenize("\"hello world\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"hello world\"", str.ContentSpan.ToString());
        Assert.Equal("hello world", str.Value.ToString());
        Assert.Equal('"', str.Quote);
    }

    [Fact]
    public void TokenParser_SingleQuotedString_ReturnsStringToken()
    {
        var tokens = Tokenize("'hello'");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("'hello'", str.ContentSpan.ToString());
        Assert.Equal('\'', str.Quote);
    }

    [Fact]
    public void TokenParser_StringWithEscape_PreservesEscape()
    {
        var tokens = Tokenize("\"hello\\\"world\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"hello\\\"world\"", str.ContentSpan.ToString());
    }

    #endregion

    #region Numeric Tests

    [Fact]
    public void TokenParser_Integer_ReturnsNumericToken()
    {
        var tokens = Tokenize("12345");

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("12345", num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
    }

    [Fact]
    public void TokenParser_DecimalNumber_ReturnsFloatingPointToken()
    {
        var tokens = Tokenize("123.456");

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("123.456", num.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    [Fact]
    public void TokenParser_LeadingDotNumber_ReturnsFloatingPointToken()
    {
        var tokens = Tokenize(".456");

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(".456", num.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    [Fact]
    public void TokenParser_IntegerFollowedByDot_ReturnsIntegerAndSymbol()
    {
        var tokens = Tokenize("123.abc");

        Assert.Equal(3, tokens.Length);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("123", num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
        
        var dot = Assert.IsType<SymbolToken>(tokens[1]);
        Assert.Equal('.', dot.Symbol);
        
        Assert.IsType<IdentToken>(tokens[2]);
    }

    #region Numeric Edge Cases

    [Theory]
    [InlineData("0", NumericType.Integer, "0")]
    [InlineData("1", NumericType.Integer, "1")]
    [InlineData("9", NumericType.Integer, "9")]
    [InlineData("42", NumericType.Integer, "42")]
    [InlineData("999999", NumericType.Integer, "999999")]
    public void TokenParser_SingleDigitAndSimpleIntegers_ReturnsInteger(string input, NumericType expectedType, string expectedContent)
    {
        var tokens = Tokenize(input);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(expectedContent, num.ContentSpan.ToString());
        Assert.Equal(expectedType, num.NumericType);
    }

    [Theory]
    [InlineData("0.0", NumericType.FloatingPoint, "0.0")]
    [InlineData("1.0", NumericType.FloatingPoint, "1.0")]
    [InlineData("0.5", NumericType.FloatingPoint, "0.5")]
    [InlineData("123.456", NumericType.FloatingPoint, "123.456")]
    public void TokenParser_StandardDecimals_ReturnsFloatingPoint(string input, NumericType expectedType, string expectedContent)
    {
        var tokens = Tokenize(input);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(expectedContent, num.ContentSpan.ToString());
        Assert.Equal(expectedType, num.NumericType);
    }

    [Theory]
    [InlineData(".0", NumericType.FloatingPoint, ".0")]
    [InlineData(".5", NumericType.FloatingPoint, ".5")]
    [InlineData(".123", NumericType.FloatingPoint, ".123")]
    [InlineData(".999", NumericType.FloatingPoint, ".999")]
    public void TokenParser_LeadingDotDecimals_ReturnsFloatingPoint(string input, NumericType expectedType, string expectedContent)
    {
        var tokens = Tokenize(input);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(expectedContent, num.ContentSpan.ToString());
        Assert.Equal(expectedType, num.NumericType);
    }

    [Fact]
    public void TokenParser_TrailingDot_ReturnsIntegerAndDotSymbol()
    {
        // "0." should produce Integer(0) + Symbol(.)
        var tokens = Tokenize("0.");

        Assert.Equal(2, tokens.Length);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("0", num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
        
        var dot = Assert.IsType<SymbolToken>(tokens[1]);
        Assert.Equal('.', dot.Symbol);
    }

    [Theory]
    [InlineData("00123", "00123")]
    [InlineData("007", "007")]
    [InlineData("000", "000")]
    public void TokenParser_LeadingZeros_ReturnsIntegerWithLeadingZeros(string input, string expectedContent)
    {
        var tokens = Tokenize(input);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(expectedContent, num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
    }

    [Fact]
    public void TokenParser_MultipleDots_ParsedGreedily()
    {
        // "1.2.3" - tokenizer parses 1.2 as FloatingPoint, then .3 as FloatingPoint
        var tokens = Tokenize("1.2.3");

        Assert.Equal(2, tokens.Length);
        
        var num1 = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("1.2", num1.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num1.NumericType);
        
        var num2 = Assert.IsType<NumericToken>(tokens[1]);
        Assert.Equal(".3", num2.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num2.NumericType);
    }

    [Fact]
    public void TokenParser_ConsecutiveDots_ReturnsNumericAndMultipleDots()
    {
        // "1..2" should produce Integer(1) + Symbol(.) + FloatingPoint(.2)
        var tokens = Tokenize("1..2");

        Assert.Equal(3, tokens.Length);
        
        var num1 = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("1", num1.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num1.NumericType);
        
        var dot = Assert.IsType<SymbolToken>(tokens[1]);
        Assert.Equal('.', dot.Symbol);
        
        var num2 = Assert.IsType<NumericToken>(tokens[2]);
        Assert.Equal(".2", num2.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num2.NumericType);
    }

    [Fact]
    public void TokenParser_NumbersInExpression_ParsedCorrectly()
    {
        var tokens = Tokenize("x = 3.14");

        Assert.Equal(5, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<OperatorToken>(tokens[2]);
        Assert.IsType<WhitespaceToken>(tokens[3]);
        
        var num = Assert.IsType<NumericToken>(tokens[4]);
        Assert.Equal("3.14", num.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    [Fact]
    public void TokenParser_NumberFollowedByIdentifier_SeparateTokens()
    {
        // "123abc" should produce Integer(123) + Ident(abc)
        var tokens = Tokenize("123abc");

        Assert.Equal(2, tokens.Length);
        
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("123", num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
        
        var ident = Assert.IsType<IdentToken>(tokens[1]);
        Assert.Equal("abc", ident.ContentSpan.ToString());
    }

    [Fact]
    public void TokenParser_VeryLongNumber_ParsedAsInteger()
    {
        var longNumber = new string('9', 100);
        var tokens = Tokenize(longNumber);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(longNumber, num.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, num.NumericType);
    }

    [Fact]
    public void TokenParser_VeryLongDecimal_ParsedAsFloatingPoint()
    {
        var longDecimal = new string('9', 50) + "." + new string('1', 50);
        var tokens = Tokenize(longDecimal);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(longDecimal, num.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    #endregion

    #endregion

    #region Comment Tests

    [Fact]
    public void TokenParser_SingleLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tokens = Tokenize("// comment", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("// comment", comment.ContentSpan.ToString());
        Assert.False(comment.IsMultiLine);
    }

    [Fact]
    public void TokenParser_MultiLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* comment */", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("/* comment */", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    [Fact]
    public void TokenParser_UnterminatedMultiLineComment_ReturnsErrorToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* unterminated", options);

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unterminated", error.ErrorMessage);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void TokenParser_UnclosedBlock_ReturnsErrorToken()
    {
        var tokens = Tokenize("{unclosed");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
    }

    [Fact]
    public void TokenParser_UnmatchedCloser_ReturnsErrorToken()
    {
        var tokens = Tokenize("}");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unexpected", error.ErrorMessage);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void ReadOnlyMemoryExtension_Tokenize_Works()
    {
        var tokens = "hello world".AsMemory().Tokenize();

        Assert.Equal(3, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
    }

    [Fact]
    public void StringExtension_TokenizeToTokens_Works()
    {
        var tokens = "hello world".TokenizeToTokens();

        Assert.Equal(3, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
    }

    #endregion
}
