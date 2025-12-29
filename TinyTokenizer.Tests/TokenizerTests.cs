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

    /// <summary>
    /// Tokenizes using the two-level architecture (Lexer + TokenParser).
    /// Required for features like directives that need multi-token pattern recognition.
    /// </summary>
    private static ImmutableArray<Token> TokenizeTwoLevel(string source, TokenizerOptions? options = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return ImmutableArray<Token>.Empty;
        }

        options ??= TokenizerOptions.Default;
        var lexer = new Lexer(options);
        var parser = new TokenParser(options);
        var simpleTokens = lexer.Lex(source);
        return parser.ParseToArray(simpleTokens);
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
        var tokens = Tokenize(":");

        Assert.Single(tokens);
        var token = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal(':', token.Symbol);
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
    public void Tokenize_SimpleBraceBlock_ReturnsSimpleBlock()
    {
        var tokens = Tokenize("{content}");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);
        Assert.Equal("{content}", block.ContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
        Assert.Equal('{', block.OpeningDelimiter.FirstChar);
        Assert.Equal('}', block.ClosingDelimiter.FirstChar);
    }

    [Fact]
    public void Tokenize_SimpleBracketBlock_ReturnsSimpleBlock()
    {
        var tokens = Tokenize("[content]");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.BracketBlock, block.Type);
        Assert.Equal("[content]", block.ContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_SimpleParenthesisBlock_ReturnsSimpleBlock()
    {
        var tokens = Tokenize("(content)");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.ParenthesisBlock, block.Type);
        Assert.Equal("(content)", block.ContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_EmptyBlock_ReturnsBlockWithEmptyContent()
    {
        var tokens = Tokenize("{}");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal("{}", block.ContentSpan.ToString());
        Assert.Equal("", block.InnerContentSpan.ToString());
        Assert.Empty(block.Children);
    }

    [Fact]
    public void Tokenize_BlockWithChildren_ReturnsNestedTokens()
    {
        var tokens = Tokenize("{hello world}");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
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

        var braceBlock = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, braceBlock.Type);
        Assert.Single(braceBlock.Children);

        var bracketBlock = Assert.IsType<SimpleBlock>(braceBlock.Children[0]);
        Assert.Equal(TokenType.BracketBlock, bracketBlock.Type);
        Assert.Single(bracketBlock.Children);

        var parenBlock = Assert.IsType<SimpleBlock>(bracketBlock.Children[0]);
        Assert.Equal(TokenType.ParenthesisBlock, parenBlock.Type);
        Assert.Empty(parenBlock.Children);
    }

    [Fact]
    public void Tokenize_SiblingBlocks_ReturnsMultipleSimpleBlocks()
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

        var block = Assert.IsType<SimpleBlock>(tokens[1]);
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
    [InlineData(':')]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('.')]
    [InlineData('@')]
    [InlineData('#')]
    [InlineData('?')]
    [InlineData('^')]
    [InlineData('~')]
    public void Tokenize_DefaultSymbols_AreRecognized(char symbol)
    {
        var tokens = Tokenize(symbol.ToString());

        Assert.Single(tokens);
        var symbolToken = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal(symbol, symbolToken.Symbol);
    }

    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    [InlineData("%")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("=")]
    [InlineData("!")]
    public void Tokenize_DefaultOperators_AreRecognized(string op)
    {
        var tokens = Tokenize(op);

        Assert.Single(tokens);
        var opToken = Assert.IsType<OperatorToken>(tokens[0]);
        Assert.Equal(op, opToken.Operator);
    }

    [Fact]
    public void Tokenize_PathLikeContent_TokenizesCorrectly()
    {
        var tokens = Tokenize("path/to/file");

        Assert.Equal(5, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<OperatorToken>(tokens[1]);  // / is now an operator
        Assert.IsType<IdentToken>(tokens[2]);
        Assert.IsType<OperatorToken>(tokens[3]);
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
        Assert.IsType<OperatorToken>(tokens[1]);  // = is now an operator
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
        Assert.IsType<SimpleBlock>(tokens[3]);
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
    [InlineData("++")]
    [InlineData("--")]
    [InlineData("<<")]
    [InlineData(">>")]
    public void Tokenize_NonDefaultOperators_ReturnsSymbolTokens(string op)
    {
        // Default options (Universal) include ==, !=, &&, ||, <=, >=, +=, -=, *=, /=
        // These operators are NOT in the default set
        var tokens = Tokenize(op);

        // These should NOT be recognized as single operators with default settings
        Assert.True(tokens.Length > 1 || tokens[0] is not OperatorToken);
    }

    [Theory]
    [InlineData("===")]
    [InlineData("!==")]
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
        // Remove += from operators, should become two separate operators (+ and =)
        var options = TokenizerOptions.Default.WithoutOperators("+=");
        var tokens = Tokenize("+=", options);

        // += should now be two separate operators (+ and =)
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, t => Assert.IsType<OperatorToken>(t));
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

    #region Tagged Identifier Token Tests (Two-Level Architecture Only)

    [Fact]
    public void Tokenize_TaggedIdent_WithHashPrefix_ReturnsTaggedIdentToken()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#include", options);

        Assert.Single(tokens);
        var tagged = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal("#include", tagged.ContentSpan.ToString());
        Assert.Equal('#', tagged.Tag);
        Assert.Equal("include", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_WithAtPrefix_ReturnsTaggedIdentToken()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('@');
        var tokens = TokenizeTwoLevel("@Override", options);

        Assert.Single(tokens);
        var tagged = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal("@Override", tagged.ContentSpan.ToString());
        Assert.Equal('@', tagged.Tag);
        Assert.Equal("Override", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_Disabled_ReturnsSymbolAndIdent()
    {
        // Tag prefixes disabled by default
        var tokens = TokenizeTwoLevel("#include");

        Assert.Equal(2, tokens.Length);
        Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('#', ((SymbolToken)tokens[0]).Symbol);
        Assert.IsType<IdentToken>(tokens[1]);
        Assert.Equal("include", tokens[1].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_DoesNotConsumeFollowingTokens()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#define MAX 100", options);

        // Tagged ident only captures "#define", rest are separate tokens
        Assert.Equal(5, tokens.Length);
        var tagged = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal("define", tagged.NameSpan.ToString());
        Assert.IsType<WhitespaceToken>(tokens[1]); // space
        Assert.IsType<IdentToken>(tokens[2]); // MAX
        Assert.IsType<WhitespaceToken>(tokens[3]); // space
        Assert.IsType<NumericToken>(tokens[4]); // 100
    }

    [Fact]
    public void Tokenize_TaggedIdent_Multiple()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#include\n#define", options);

        var tagged = tokens.OfType<TaggedIdentToken>().ToList();
        Assert.Equal(2, tagged.Count);
        Assert.Equal("include", tagged[0].NameSpan.ToString());
        Assert.Equal("define", tagged[1].NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_HashNotFollowedByIdent_ReturnsSymbol()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("# 123", options);

        // # not followed by identifier (there's space), should be symbol
        Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('#', ((SymbolToken)tokens[0]).Symbol);
    }

    [Fact]
    public void Tokenize_TaggedIdent_MidLine_IsRecognized()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        // Tagged idents work anywhere (no line-start requirement)
        var tokens = TokenizeTwoLevel("x #define", options);

        Assert.Equal(3, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]); // x
        Assert.IsType<WhitespaceToken>(tokens[1]); // space
        Assert.IsType<TaggedIdentToken>(tokens[2]); // #define
        Assert.Equal("define", ((TaggedIdentToken)tokens[2]).NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_Position_IsCorrect()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("abc #define", options);

        var tagged = tokens.OfType<TaggedIdentToken>().Single();
        Assert.Equal(4, tagged.Position); // 0-indexed position after "abc "
    }

    [Fact]
    public void Tokenize_TaggedIdent_InBlock_Works()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("{#define}", options);

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        var tagged = block.Children.OfType<TaggedIdentToken>().SingleOrDefault();
        Assert.NotNull(tagged);
        Assert.Equal("define", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_MultiplePrefixes()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#', '@');
        var tokens = TokenizeTwoLevel("#include @Override", options);

        Assert.Equal(3, tokens.Length);
        var hash = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal('#', hash.Tag);
        Assert.Equal("include", hash.NameSpan.ToString());
        
        Assert.IsType<WhitespaceToken>(tokens[1]);
        
        var at = Assert.IsType<TaggedIdentToken>(tokens[2]);
        Assert.Equal('@', at.Tag);
        Assert.Equal("Override", at.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_AtEndOfInput()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("x #define", options);

        Assert.Equal(3, tokens.Length);
        var tagged = Assert.IsType<TaggedIdentToken>(tokens[2]);
        Assert.Equal("define", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_TagAtEndOfInput_NoIdent()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("x #", options);

        // # at end with no following identifier should be symbol
        Assert.Equal(3, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<SymbolToken>(tokens[2]);
        Assert.Equal('#', ((SymbolToken)tokens[2]).Symbol);
    }

    [Fact]
    public void Tokenize_TaggedIdent_TagFollowedByNumber()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#123", options);

        // # followed by number (not identifier) should be symbol + number
        Assert.Equal(2, tokens.Length);
        Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('#', ((SymbolToken)tokens[0]).Symbol);
        Assert.IsType<NumericToken>(tokens[1]);
    }

    [Fact]
    public void Tokenize_TaggedIdent_TagFollowedByAnotherTag()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#', '@');
        var tokens = TokenizeTwoLevel("#@test", options);

        // # followed by @ (not identifier) should be symbol + @test
        Assert.Equal(2, tokens.Length);
        Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('#', ((SymbolToken)tokens[0]).Symbol);
        var tagged = Assert.IsType<TaggedIdentToken>(tokens[1]);
        Assert.Equal('@', tagged.Tag);
        Assert.Equal("test", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_ConsecutiveTags()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#a#b#c", options);

        // Should be three tagged idents back to back
        Assert.Equal(3, tokens.Length);
        Assert.All(tokens, t => Assert.IsType<TaggedIdentToken>(t));
        Assert.Equal("a", ((TaggedIdentToken)tokens[0]).NameSpan.ToString());
        Assert.Equal("b", ((TaggedIdentToken)tokens[1]).NameSpan.ToString());
        Assert.Equal("c", ((TaggedIdentToken)tokens[2]).NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_UnderscoreIdentifier()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#_private #__dunder", options);

        Assert.Equal(3, tokens.Length);
        var first = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal("_private", first.NameSpan.ToString());
        var second = Assert.IsType<TaggedIdentToken>(tokens[2]);
        Assert.Equal("__dunder", second.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_IdentifierWithNumbers()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#var123 #test_456", options);

        Assert.Equal(3, tokens.Length);
        var first = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal("var123", first.NameSpan.ToString());
        var second = Assert.IsType<TaggedIdentToken>(tokens[2]);
        Assert.Equal("test_456", second.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_InsideString_NotRecognized()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("\"#include\"", options);

        // # inside string should not be tagged ident
        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("#include", str.Value.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_AfterOpeningDelimiter()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("(#test)", options);

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        var tagged = block.Children.OfType<TaggedIdentToken>().Single();
        Assert.Equal("test", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_BeforeClosingDelimiter()
    {
        var options = TokenizerOptions.Default.WithTagPrefixes('#');
        var tokens = TokenizeTwoLevel("[x #end]", options);

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        var tagged = block.Children.OfType<TaggedIdentToken>().Single();
        Assert.Equal("end", tagged.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_DollarPrefix()
    {
        // $ works as tag prefix - Lexer automatically treats tag prefixes as symbols
        var options = TokenizerOptions.Default.WithTagPrefixes('$');
        var tokens = TokenizeTwoLevel("$variable $count", options);

        Assert.Equal(3, tokens.Length);
        var first = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal('$', first.Tag);
        Assert.Equal("variable", first.NameSpan.ToString());
        var second = Assert.IsType<TaggedIdentToken>(tokens[2]);
        Assert.Equal("count", second.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_AnyCharacterAsPrefix()
    {
        // Any character can be a tag prefix - even unusual ones
        var options = TokenizerOptions.Default.WithTagPrefixes('~');
        var tokens = TokenizeTwoLevel("~magic ~spell", options);

        Assert.Equal(3, tokens.Length);
        var first = Assert.IsType<TaggedIdentToken>(tokens[0]);
        Assert.Equal('~', first.Tag);
        Assert.Equal("magic", first.NameSpan.ToString());
        var second = Assert.IsType<TaggedIdentToken>(tokens[2]);
        Assert.Equal("spell", second.NameSpan.ToString());
    }

    [Fact]
    public void Tokenize_TaggedIdent_WithAdditionalPrefixes()
    {
        var options = TokenizerOptions.Default
            .WithTagPrefixes('#')
            .WithAdditionalTagPrefixes('@');
        var tokens = TokenizeTwoLevel("#a @b", options);

        var tagged = tokens.OfType<TaggedIdentToken>().ToList();
        Assert.Equal(2, tagged.Count);
        Assert.Equal('#', tagged[0].Tag);
        Assert.Equal('@', tagged[1].Tag);
    }

    [Fact]
    public void Tokenize_TaggedIdent_WithoutPrefixes()
    {
        var options = TokenizerOptions.Default
            .WithTagPrefixes('#', '@')
            .WithoutTagPrefixes('#');
        var tokens = TokenizeTwoLevel("#a @b", options);

        // # should not be recognized, @ should be
        Assert.Equal(4, tokens.Length);
        Assert.IsType<SymbolToken>(tokens[0]); // #
        Assert.IsType<IdentToken>(tokens[1]); // a
        Assert.IsType<WhitespaceToken>(tokens[2]);
        var tagged = Assert.IsType<TaggedIdentToken>(tokens[3]);
        Assert.Equal('@', tagged.Tag);
    }

    [Fact]
    public void Tokenize_TaggedIdent_EmptyPrefixes()
    {
        var options = TokenizerOptions.Default.WithNoTagPrefixes();
        var tokens = TokenizeTwoLevel("#include @test", options);

        // Nothing should be tagged
        var tagged = tokens.OfType<TaggedIdentToken>().ToList();
        Assert.Empty(tagged);
    }

    #endregion

    #region Combined Operator and Tagged Ident Tests (Two-Level Architecture)

    [Fact]
    public void Tokenize_OperatorsAndTaggedIdents_Combined()
    {
        var options = TokenizerOptions.Default
            .WithTagPrefixes('#')
            .WithOperators(CommonOperators.CFamily);
        var tokens = TokenizeTwoLevel("#define EQ == a == b", options);

        var tagged = tokens.OfType<TaggedIdentToken>().Single();
        Assert.Equal("define", tagged.NameSpan.ToString());

        // Two == operators at top level (tagged ident doesn't consume rest of line)
        var operators = tokens.OfType<OperatorToken>().ToList();
        Assert.Equal(2, operators.Count);
        Assert.All(operators, op => Assert.Equal("==", op.Operator));
    }

    #endregion
}
