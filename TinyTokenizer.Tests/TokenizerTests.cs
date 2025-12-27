using System.Collections.Immutable;

namespace TinyTokenizer.Tests;

public class TokenizerTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string source, TokenizerOptions? options = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return ImmutableArray<Token>.Empty;
        }

        var tokenizer = new Tokenizer(source.AsMemory(), options);
        return tokenizer.Tokenize();
    }

    #endregion

    #region Basic Tokenization Tests

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmptyArray()
    {
        var tokens = Tokenize("");

        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_PlainText_ReturnsSingleIdentToken()
    {
        var tokens = Tokenize("hello");

        Assert.Single(tokens);
        var token = Assert.IsType<IdentToken>(tokens[0]);
        Assert.Equal("hello", token.ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_Whitespace_ReturnsSingleWhitespaceToken()
    {
        var tokens = Tokenize("   ");

        Assert.Single(tokens);
        var token = Assert.IsType<WhitespaceToken>(tokens[0]);
        Assert.Equal("   ", token.ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_Symbol_ReturnsSingleSymbolToken()
    {
        var tokens = Tokenize("/");

        Assert.Single(tokens);
        var token = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('/', token.Symbol);
    }

    [Fact]
    public void Tokenize_TextWithWhitespace_ReturnsMixedTokens()
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
    public void Tokenize_SimpleBraceBlock_ReturnsBlockToken()
    {
        var tokens = Tokenize("{content}");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);
        Assert.Equal("{content}", block.FullContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
        Assert.Equal('{', block.OpeningDelimiter);
        Assert.Equal('}', block.ClosingDelimiter);
    }

    [Fact]
    public void Tokenize_SimpleBracketBlock_ReturnsBlockToken()
    {
        var tokens = Tokenize("[content]");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BracketBlock, block.Type);
        Assert.Equal("[content]", block.FullContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_SimpleParenthesisBlock_ReturnsBlockToken()
    {
        var tokens = Tokenize("(content)");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.ParenthesisBlock, block.Type);
        Assert.Equal("(content)", block.FullContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_EmptyBlock_ReturnsBlockWithEmptyContent()
    {
        var tokens = Tokenize("{}");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal("{}", block.FullContentSpan.ToString());
        Assert.Equal("", block.InnerContentSpan.ToString());
        Assert.Empty(block.Children);
    }

    [Fact]
    public void Tokenize_BlockWithChildren_ReturnsNestedTokens()
    {
        var tokens = Tokenize("{hello world}");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(3, block.Children.Length);
        Assert.IsType<IdentToken>(block.Children[0]);
        Assert.IsType<WhitespaceToken>(block.Children[1]);
        Assert.IsType<IdentToken>(block.Children[2]);
    }

    #endregion

    #region Nested Block Tests

    [Fact]
    public void Tokenize_NestedBlocks_ReturnsRecursiveStructure()
    {
        var tokens = Tokenize("{[()]}");

        Assert.Single(tokens);

        var braceBlock = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, braceBlock.Type);
        Assert.Single(braceBlock.Children);

        var bracketBlock = Assert.IsType<BlockToken>(braceBlock.Children[0]);
        Assert.Equal(TokenType.BracketBlock, bracketBlock.Type);
        Assert.Single(bracketBlock.Children);

        var parenBlock = Assert.IsType<BlockToken>(bracketBlock.Children[0]);
        Assert.Equal(TokenType.ParenthesisBlock, parenBlock.Type);
        Assert.Empty(parenBlock.Children);
    }

    [Fact]
    public void Tokenize_SiblingBlocks_ReturnsMultipleBlockTokens()
    {
        var tokens = Tokenize("{}[]()");

        Assert.Equal(3, tokens.Length);
        Assert.Equal(TokenType.BraceBlock, tokens[0].Type);
        Assert.Equal(TokenType.BracketBlock, tokens[1].Type);
        Assert.Equal(TokenType.ParenthesisBlock, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_MixedContent_ReturnsCorrectStructure()
    {
        var tokens = Tokenize("func(a, b)");

        Assert.Equal(2, tokens.Length);

        var text = Assert.IsType<IdentToken>(tokens[0]);
        Assert.Equal("func", text.ContentSpan.ToString());

        var block = Assert.IsType<BlockToken>(tokens[1]);
        Assert.Equal(TokenType.ParenthesisBlock, block.Type);

        // Inside: a, b (text, symbol, whitespace, text)
        Assert.Equal(4, block.Children.Length);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Tokenize_UnexpectedClosingDelimiter_ReturnsErrorToken()
    {
        var tokens = Tokenize("}");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unexpected closing delimiter", error.ErrorMessage);
        Assert.Equal(0, error.Position);
    }

    [Fact]
    public void Tokenize_UnclosedBlock_ReturnsErrorToken()
    {
        var tokens = Tokenize("{");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed block", error.ErrorMessage);
    }

    [Fact]
    public void Tokenize_MismatchedDelimiters_ReturnsErrorAndContinues()
    {
        var tokens = Tokenize("{]");

        // Should get error for unexpected ] and error for unclosed {
        Assert.True(tokens.HasErrors());
    }

    [Fact]
    public void Tokenize_ErrorRecovery_ContinuesFromNextCharacter()
    {
        var tokens = Tokenize("}hello");

        Assert.Equal(2, tokens.Length);
        Assert.IsType<ErrorToken>(tokens[0]);
        Assert.IsType<IdentToken>(tokens[1]);
        Assert.Equal("hello", tokens[1].ContentSpan.ToString());
    }

    [Fact]
    public void HasErrors_WithErrors_ReturnsTrue()
    {
        var tokens = Tokenize("{");

        Assert.True(tokens.HasErrors());
    }

    [Fact]
    public void HasErrors_NoErrors_ReturnsFalse()
    {
        var tokens = Tokenize("{hello}");

        Assert.False(tokens.HasErrors());
    }

    [Fact]
    public void GetErrors_ReturnsAllErrors()
    {
        var tokens = Tokenize("} { ]");

        var errors = tokens.GetErrors().ToList();
        Assert.True(errors.Count >= 2);
    }

    #endregion

    #region Symbol Tests

    [Theory]
    [InlineData('/')]
    [InlineData(':')]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('=')]
    [InlineData('+')]
    [InlineData('-')]
    [InlineData('*')]
    [InlineData('<')]
    [InlineData('>')]
    [InlineData('.')]
    [InlineData('@')]
    [InlineData('#')]
    public void Tokenize_DefaultSymbols_AreRecognized(char symbol)
    {
        var tokens = Tokenize(symbol.ToString());

        Assert.Single(tokens);
        var symbolToken = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal(symbol, symbolToken.Symbol);
    }

    [Fact]
    public void Tokenize_PathLikeContent_TokenizesCorrectly()
    {
        var tokens = Tokenize("path/to/file");

        Assert.Equal(5, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
        Assert.IsType<SymbolToken>(tokens[3]);
        Assert.IsType<IdentToken>(tokens[4]);
    }

    #endregion

    #region Options Tests

    [Fact]
    public void Tokenize_CustomSymbols_AreRecognized()
    {
        var options = TokenizerOptions.Default.WithSymbols('$', '_');
        var tokens = Tokenize("a$b_c", options);

        Assert.Equal(5, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
        Assert.IsType<SymbolToken>(tokens[3]);
        Assert.IsType<IdentToken>(tokens[4]);
    }

    [Fact]
    public void Tokenize_RemovedSymbol_TreatedAsText()
    {
        var options = TokenizerOptions.Default.WithoutSymbols('/');
        var tokens = Tokenize("a/b", options);

        // Without / as symbol, "a/b" is one text token
        Assert.Single(tokens);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.Equal("a/b", tokens[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_AdditionalSymbol_IsRecognized()
    {
        var options = TokenizerOptions.Default.WithAdditionalSymbols('$');
        var tokens = Tokenize("a$b", options);

        Assert.Equal(3, tokens.Length);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.Equal('$', ((SymbolToken)tokens[1]).Symbol);
    }

    #endregion

    #region Numeric Token Tests

    [Fact]
    public void Tokenize_IntegerLiteral_ReturnsNumericToken()
    {
        var tokens = Tokenize("123");

        Assert.Single(tokens);
        var numeric = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("123", numeric.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, numeric.NumericType);
    }

    [Fact]
    public void Tokenize_FloatingPointLiteral_ReturnsNumericToken()
    {
        var tokens = Tokenize("3.14");

        Assert.Single(tokens);
        var numeric = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("3.14", numeric.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, numeric.NumericType);
    }

    [Fact]
    public void Tokenize_FloatingPointStartingWithDot_ReturnsNumericToken()
    {
        var tokens = Tokenize(".5");

        Assert.Single(tokens);
        var numeric = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(".5", numeric.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, numeric.NumericType);
    }

    [Fact]
    public void Tokenize_IntegerFollowedByDotWithoutDigit_ReturnsIntegerAndSymbol()
    {
        var tokens = Tokenize("123.");

        Assert.Equal(2, tokens.Length);
        var numeric = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("123", numeric.ContentSpan.ToString());
        Assert.Equal(NumericType.Integer, numeric.NumericType);
        Assert.IsType<SymbolToken>(tokens[1]);
    }

    [Fact]
    public void Tokenize_MultipleNumericLiterals_ReturnsMultipleTokens()
    {
        var tokens = Tokenize("1 2.5 3");

        Assert.Equal(5, tokens.Length);
        var num1 = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(NumericType.Integer, num1.NumericType);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        var num2 = Assert.IsType<NumericToken>(tokens[2]);
        Assert.Equal(NumericType.FloatingPoint, num2.NumericType);
        Assert.IsType<WhitespaceToken>(tokens[3]);
        var num3 = Assert.IsType<NumericToken>(tokens[4]);
        Assert.Equal(NumericType.Integer, num3.NumericType);
    }

    #endregion

    #region String Token Tests

    [Fact]
    public void Tokenize_DoubleQuotedString_ReturnsStringToken()
    {
        var tokens = Tokenize("\"hello\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"hello\"", str.ContentSpan.ToString());
        Assert.Equal('"', str.Quote);
        Assert.Equal("hello", str.Value.ToString());
    }

    [Fact]
    public void Tokenize_SingleQuotedString_ReturnsStringToken()
    {
        var tokens = Tokenize("'hello'");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("'hello'", str.ContentSpan.ToString());
        Assert.Equal('\'', str.Quote);
        Assert.Equal("hello", str.Value.ToString());
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsStringTokenWithEmptyValue()
    {
        var tokens = Tokenize("\"\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"\"", str.ContentSpan.ToString());
        Assert.True(str.Value.IsEmpty);
    }

    [Fact]
    public void Tokenize_StringWithEscapedQuote_IncludesEscapeSequence()
    {
        var tokens = Tokenize("\"say \\\"hi\\\"\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"say \\\"hi\\\"\"", str.ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_StringWithEscapedBackslash_IncludesEscapeSequence()
    {
        var tokens = Tokenize("\"path\\\\file\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"path\\\\file\"", str.ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_UnterminatedString_ReturnsSymbolAndContinues()
    {
        var tokens = Tokenize("\"hello");

        // Should emit the quote as a symbol, then continue with "hello" as text
        Assert.Equal(2, tokens.Length);
        var symbol = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('"', symbol.Symbol);
        var text = Assert.IsType<IdentToken>(tokens[1]);
        Assert.Equal("hello", text.ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_UnterminatedSingleQuotedString_ReturnsSymbolAndContinues()
    {
        var tokens = Tokenize("'hello");

        Assert.Equal(2, tokens.Length);
        var symbol = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('\'', symbol.Symbol);
        var text = Assert.IsType<IdentToken>(tokens[1]);
        Assert.Equal("hello", text.ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_MixedStringAndText_ReturnsCorrectTokens()
    {
        var tokens = Tokenize("name=\"value\"");

        Assert.Equal(3, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<SymbolToken>(tokens[1]);
        var str = Assert.IsType<StringToken>(tokens[2]);
        Assert.Equal("value", str.Value.ToString());
    }

    [Fact]
    public void Tokenize_StringWithSpaces_PreservesSpaces()
    {
        var tokens = Tokenize("\"hello world\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("hello world", str.Value.ToString());
    }

    [Fact]
    public void Tokenize_StringWithNumbers_PreservesNumbers()
    {
        var tokens = Tokenize("\"value is 42\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("value is 42", str.Value.ToString());
    }

    #endregion

    #region Comment Token Tests

    [Fact]
    public void Tokenize_SingleLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tokens = Tokenize("// this is a comment", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("// this is a comment", comment.ContentSpan.ToString());
        Assert.False(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_SingleLineComment_StopsAtNewline()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tokens = Tokenize("// comment\ncode", options);

        Assert.Equal(3, tokens.Length);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("// comment", comment.ContentSpan.ToString());
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
    }

    [Fact]
    public void Tokenize_MultiLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* this is a comment */", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("/* this is a comment */", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_MultiLineComment_SpansMultipleLines()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* line 1\nline 2 */", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("/* line 1\nline 2 */", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_UnterminatedMultiLineComment_ConsumesToEnd()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* unclosed comment", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("/* unclosed comment", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_HashComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.HashSingleLine);
        var tokens = Tokenize("# python comment", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("# python comment", comment.ContentSpan.ToString());
        Assert.False(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_SqlComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.SqlSingleLine);
        var tokens = Tokenize("-- sql comment", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("-- sql comment", comment.ContentSpan.ToString());
        Assert.False(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_HtmlComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.HtmlComment);
        var tokens = Tokenize("<!-- html comment -->", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("<!-- html comment -->", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    [Fact]
    public void Tokenize_MultipleCommentStyles_RecognizesBoth()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(
            CommentStyle.CStyleSingleLine, 
            CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("// single\n/* multi */", options);

        Assert.Equal(3, tokens.Length);
        var single = Assert.IsType<CommentToken>(tokens[0]);
        Assert.False(single.IsMultiLine);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        var multi = Assert.IsType<CommentToken>(tokens[2]);
        Assert.True(multi.IsMultiLine);
    }

    [Fact]
    public void Tokenize_CommentBeforeCode_ReturnsCorrectOrder()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tokens = Tokenize("// comment\nfunc()", options);

        Assert.Equal(4, tokens.Length);
        Assert.IsType<CommentToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<IdentToken>(tokens[2]);
        Assert.IsType<BlockToken>(tokens[3]);
    }

    [Fact]
    public void Tokenize_NoCommentStylesConfigured_TreatsAsSymbols()
    {
        // Default options have no comment styles
        var tokens = Tokenize("// not a comment");

        // Should be parsed as symbols and text, not as a comment
        Assert.True(tokens.Length > 1);
        Assert.DoesNotContain(tokens, t => t is CommentToken);
    }

    [Fact]
    public void Tokenize_CustomCommentStyle_IsRecognized()
    {
        var customStyle = new CommentStyle("REM ");
        var options = TokenizerOptions.Default.WithCommentStyles(customStyle);
        var tokens = Tokenize("REM this is a batch comment", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("REM this is a batch comment", comment.ContentSpan.ToString());
    }

    #endregion

    #region Utility Extension Tests

    [Fact]
    public void OfTokenType_ReturnsAllMatchingTokens()
    {
        var tokens = Tokenize("{a} [b] (c)");

        var IdentTokens = tokens.OfTokenType<IdentToken>().ToList();

        Assert.Equal(3, IdentTokens.Count);
    }

    [Fact]
    public void OfTokenType_IncludesNestedTokens()
    {
        var tokens = Tokenize("{a {b}}");

        var IdentTokens = tokens.OfTokenType<IdentToken>().ToList();

        Assert.Equal(2, IdentTokens.Count);
    }

    #endregion

    #region Operator Token Tests

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData("&&")]
    [InlineData("||")]
    public void Tokenize_DefaultOperators_ReturnsOperatorToken(string op)
    {
        var tokens = Tokenize(op);

        Assert.Single(tokens);
        var opToken = Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal(op, opToken.Operator);
    }

    [Theory]
    [InlineData("===")]
    [InlineData("!==")]
    [InlineData("<=")]
    [InlineData(">=")]
    [InlineData("++")]
    [InlineData("--")]
    public void Tokenize_NonDefaultOperators_ReturnsSymbolTokens(string op)
    {
        // Default options only include ==, !=, &&, ||
        var tokens = Tokenize(op);

        // These should NOT be recognized as single operators with default settings
        Assert.True(tokens.Length > 1 || tokens[0] is not OperatorToken);
    }

    [Theory]
    [InlineData("===")]
    [InlineData("!==")]
    [InlineData("<=")]
    [InlineData(">=")]
    [InlineData("++")]
    [InlineData("--")]
    [InlineData("=>")]
    [InlineData("?.")]
    [InlineData("??")]
    public void Tokenize_JavaScriptOperators_WithConfig_ReturnsOperatorToken(string op)
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.JavaScript);
        var tokens = Tokenize(op, options);

        Assert.Single(tokens);
        var opToken = Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal(op, opToken.Operator);
    }

    [Fact]
    public void Tokenize_OperatorInExpression_TokenizesCorrectly()
    {
        var tokens = Tokenize("a == b");

        Assert.Equal(5, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);      // a
        Assert.IsType<WhitespaceToken>(tokens[1]); // space
        Assert.IsType<OperatorToken>(tokens[2]);   // ==
        Assert.IsType<WhitespaceToken>(tokens[3]); // space
        Assert.IsType<IdentToken>(tokens[4]);      // b
    }

    [Fact]
    public void Tokenize_MultipleOperators_TokenizesCorrectly()
    {
        var tokens = Tokenize("a && b || c");

        var opTokens = tokens.Where(t => t is OperatorToken).ToList();
        Assert.Equal(2, opTokens.Count);
        Assert.Equal("&&", ((OperatorToken)opTokens[0]).Operator);
        Assert.Equal("||", ((OperatorToken)opTokens[1]).Operator);
    }

    [Fact]
    public void Tokenize_OverlappingOperators_MatchesLongestFirst_TripleEquals()
    {
        // === should match as === not == + = when configured
        var options = TokenizerOptions.Default.WithOperators("===", "==", "=");
        var tokens = Tokenize("===", options);

        Assert.Single(tokens);
        var opToken = Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal("===", opToken.Operator);
    }

    [Fact]
    public void Tokenize_OverlappingOperators_MatchesLongestFirst_StrictNotEquals()
    {
        // !== should match as !== not != + = when configured
        var options = TokenizerOptions.Default.WithOperators("!==", "!=", "!");
        var tokens = Tokenize("!==", options);

        Assert.Single(tokens);
        var opToken = Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal("!==", opToken.Operator);
    }

    [Fact]
    public void Tokenize_OverlappingOperators_DoubleEqualsVsTripleEquals()
    {
        // When only == is configured, === should be == + =
        var options = TokenizerOptions.Default.WithOperators("==");
        var tokens = Tokenize("===", options);

        Assert.Equal(2, tokens.Length);
        Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal("==", ((OperatorToken)tokens[0]).Operator);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.Equal('=', ((SymbolToken)tokens[1]).Symbol);
    }

    [Fact]
    public void Tokenize_OverlappingOperators_NotEqualsVsStrictNotEquals()
    {
        // When only != is configured, !== should be != + =
        var options = TokenizerOptions.Default.WithOperators("!=");
        var tokens = Tokenize("!==", options);

        Assert.Equal(2, tokens.Length);
        Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal("!=", ((OperatorToken)tokens[0]).Operator);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.Equal('=', ((SymbolToken)tokens[1]).Symbol);
    }

    [Fact]
    public void Tokenize_WithNoOperators_AllSymbolsAreSymbolTokens()
    {
        var options = TokenizerOptions.Default.WithNoOperators();
        var tokens = Tokenize("== != && ||", options);

        var symbolTokens = tokens.Where(t => t is SymbolToken).ToList();
        Assert.Equal(8, symbolTokens.Count); // Each char is a separate symbol
    }

    [Fact]
    public void Tokenize_WithAdditionalOperators_RecognizesNewOperator()
    {
        var options = TokenizerOptions.Default.WithAdditionalOperators("<=", ">=");
        var tokens = Tokenize("a <= b >= c", options);

        var opTokens = tokens.Where(t => t is OperatorToken).ToList();
        Assert.Equal(2, opTokens.Count);
        Assert.Equal("<=", ((OperatorToken)opTokens[0]).Operator);
        Assert.Equal(">=", ((OperatorToken)opTokens[1]).Operator);
    }

    [Fact]
    public void Tokenize_WithoutOperators_RemovesOperator()
    {
        var options = TokenizerOptions.Default.WithoutOperators("==");
        var tokens = Tokenize("==", options);

        // == should now be two separate symbols
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, t => Assert.IsType<SymbolToken>(t));
    }

    [Fact]
    public void Tokenize_CFamilyOperators_WithConfig()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        
        var testCases = new[] { "==", "!=", "<=", ">=", "++", "--", "+=", "-=", "<<", ">>" };
        foreach (var op in testCases)
        {
            var tokens = Tokenize(op, options);
            Assert.Single(tokens);
            var opToken = Assert.IsType<OperatorToken>(tokens[0]);
            Assert.Equal(op, opToken.Operator);
        }
    }

    [Fact]
    public void Tokenize_OperatorPosition_IsCorrect()
    {
        var tokens = Tokenize("abc==def");

        var opToken = tokens.OfType<OperatorToken>().Single();
        Assert.Equal(3, opToken.Position); // 0-indexed position after "abc"
    }

    #endregion

    #region Directive Token Tests

    [Fact]
    public void Tokenize_Directive_WithDirectivesEnabled_ReturnsDirectiveToken()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("#include", options);

        Assert.Single(tokens);
        var directive = Assert.IsType<DirectiveToken>(tokens[0]);
        Assert.Equal("#include", directive.ContentSpan.ToString());
        Assert.Equal("include", directive.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_Directive_WithDirectivesDisabled_ReturnsSymbolAndIdent()
    {
        // Directives disabled by default
        var tokens = Tokenize("#include");

        Assert.Equal(2, tokens.Length);
        Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('#', ((SymbolToken)tokens[0]).Symbol);
        Assert.IsType<IdentToken>(tokens[1]);
        Assert.Equal("include", tokens[1].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_Directive_WithArguments()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("#define MAX 100", options);

        Assert.Single(tokens);
        var directive = Assert.IsType<DirectiveToken>(tokens[0]);
        Assert.Equal("define", directive.NameSpan.ToString());
        Assert.Equal(4, directive.Arguments.Length); // space, MAX, space, 100
    }

    [Fact]
    public void Tokenize_Directive_WithStringArgument()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("#include \"header.h\"", options);

        Assert.Single(tokens);
        var directive = Assert.IsType<DirectiveToken>(tokens[0]);
        Assert.Equal("include", directive.NameSpan.ToString());
        
        var stringArg = directive.Arguments.OfType<StringToken>().SingleOrDefault();
        Assert.NotNull(stringArg);
        Assert.Equal("header.h", stringArg.Value.ToString());
    }

    [Fact]
    public void Tokenize_Directive_EndsAtNewline()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("#define X\nint main", options);

        Assert.Equal(5, tokens.Length);
        Assert.IsType<DirectiveToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]); // newline
        Assert.IsType<IdentToken>(tokens[2]); // int
        Assert.IsType<WhitespaceToken>(tokens[3]); // space
        Assert.IsType<IdentToken>(tokens[4]); // main
    }

    [Fact]
    public void Tokenize_Directive_MultipleDirectives()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("#include\n#define", options);

        var directives = tokens.OfType<DirectiveToken>().ToList();
        Assert.Equal(2, directives.Count);
        Assert.Equal("include", directives[0].NameSpan.ToString());
        Assert.Equal("define", directives[1].NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_Directive_WithWhitespaceBetweenHashAndName()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("#  include", options);

        Assert.Single(tokens);
        var directive = Assert.IsType<DirectiveToken>(tokens[0]);
        Assert.Equal("include", directive.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_HashNotFollowedByIdent_ReturnsSymbol()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("# 123", options);

        // # not followed by identifier, should be symbol
        Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('#', ((SymbolToken)tokens[0]).Symbol);
    }

    [Fact]
    public void Tokenize_DirectivePosition_IsCorrect()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        var tokens = Tokenize("abc #define xyz", options);

        var directive = tokens.OfType<DirectiveToken>().Single();
        Assert.Equal(4, directive.Position); // 0-indexed position after "abc "
    }

    [Fact]
    public void Tokenize_DirectiveInBlock_Works()
    {
        var options = TokenizerOptions.Default.WithDirectives();
        // Directive needs a newline to terminate, otherwise it consumes the closing brace
        var tokens = Tokenize("{#define\n}", options);

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        var directive = block.Children.OfType<DirectiveToken>().SingleOrDefault();
        Assert.NotNull(directive);
        Assert.Equal("define", directive.NameSpan.ToString());
    }

    #endregion

    #region Combined Operator and Directive Tests

    [Fact]
    public void Tokenize_OperatorsAndDirectives_Combined()
    {
        var options = TokenizerOptions.Default
            .WithDirectives()
            .WithOperators(CommonOperators.CFamily);
        var tokens = Tokenize("#define EQ ==\na == b", options);

        var directive = tokens.OfType<DirectiveToken>().Single();
        Assert.Equal("define", directive.NameSpan.ToString());

        // One operator in directive args (nested), one in the expression (top-level)
        var topLevelOperators = tokens.OfType<OperatorToken>().ToList();
        Assert.Single(topLevelOperators);
        Assert.Equal("==", topLevelOperators[0].Operator);

        // Check directive has operator in its arguments
        var directiveOperators = directive.Arguments.OfType<OperatorToken>().ToList();
        Assert.Single(directiveOperators);
        Assert.Equal("==", directiveOperators[0].Operator);
    }

    #endregion
}
