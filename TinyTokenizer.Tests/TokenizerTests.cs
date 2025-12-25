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
    public void Tokenize_PlainText_ReturnsSingleTextToken()
    {
        var tokens = Tokenize("hello");

        Assert.Single(tokens);
        var token = Assert.IsType<TextToken>(tokens[0]);
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
        Assert.IsType<TextToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<TextToken>(tokens[2]);
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
        Assert.IsType<TextToken>(block.Children[0]);
        Assert.IsType<WhitespaceToken>(block.Children[1]);
        Assert.IsType<TextToken>(block.Children[2]);
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

        var text = Assert.IsType<TextToken>(tokens[0]);
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
        Assert.IsType<TextToken>(tokens[1]);
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
        Assert.IsType<TextToken>(tokens[0]);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.IsType<TextToken>(tokens[2]);
        Assert.IsType<SymbolToken>(tokens[3]);
        Assert.IsType<TextToken>(tokens[4]);
    }

    #endregion

    #region Options Tests

    [Fact]
    public void Tokenize_CustomSymbols_AreRecognized()
    {
        var options = TokenizerOptions.Default.WithSymbols('$', '_');
        var tokens = Tokenize("a$b_c", options);

        Assert.Equal(5, tokens.Length);
        Assert.IsType<TextToken>(tokens[0]);
        Assert.IsType<SymbolToken>(tokens[1]);
        Assert.IsType<TextToken>(tokens[2]);
        Assert.IsType<SymbolToken>(tokens[3]);
        Assert.IsType<TextToken>(tokens[4]);
    }

    [Fact]
    public void Tokenize_RemovedSymbol_TreatedAsText()
    {
        var options = TokenizerOptions.Default.WithoutSymbols('/');
        var tokens = Tokenize("a/b", options);

        // Without / as symbol, "a/b" is one text token
        Assert.Single(tokens);
        Assert.IsType<TextToken>(tokens[0]);
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

    #region Utility Extension Tests

    [Fact]
    public void OfTokenType_ReturnsAllMatchingTokens()
    {
        var tokens = Tokenize("{a} [b] (c)");

        var textTokens = tokens.OfTokenType<TextToken>().ToList();

        Assert.Equal(3, textTokens.Count);
    }

    [Fact]
    public void OfTokenType_IncludesNestedTokens()
    {
        var tokens = Tokenize("{a {b}}");

        var textTokens = tokens.OfTokenType<TextToken>().ToList();

        Assert.Equal(2, textTokens.Count);
    }

    #endregion
}
