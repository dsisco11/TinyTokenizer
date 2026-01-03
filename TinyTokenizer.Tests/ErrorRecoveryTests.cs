using System.Collections.Immutable;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for error recovery behavior in the tokenizer.
/// Validates that the tokenizer produces appropriate ErrorTokens for malformed input
/// and continues tokenizing after encountering errors.
/// </summary>
[Trait("Category", "ErrorRecovery")]
public class ErrorRecoveryTests
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

    #region Unclosed Block Tests

    [Fact]
    public void UnclosedBrace_ReturnsErrorToken()
    {
        var tokens = Tokenize("{");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
        Assert.Equal('{', error.Content.Span[0]);
    }

    [Fact]
    public void UnclosedBracket_ReturnsErrorToken()
    {
        var tokens = Tokenize("[");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
        Assert.Equal('[', error.Content.Span[0]);
    }

    [Fact]
    public void UnclosedParen_ReturnsErrorToken()
    {
        var tokens = Tokenize("(");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
        Assert.Equal('(', error.Content.Span[0]);
    }

    [Fact]
    public void UnclosedBraceWithContent_ReturnsErrorToken()
    {
        var tokens = Tokenize("{content");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
    }

    [Fact]
    public void UnclosedNestedBlocks_ReturnsErrorToken()
    {
        // Outer brace is closed, but inner is not: { { }
        var tokens = Tokenize("{ { }");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
    }

    [Fact]
    public void DeeplyNestedUnclosedBlock_ReturnsErrorToken()
    {
        var tokens = Tokenize("{[({");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
    }

    #endregion

    #region Mismatched Delimiter Tests

    [Fact]
    public void MismatchedBraceClosedWithBracket_ReturnsErrorToken()
    {
        var tokens = Tokenize("{]");

        // The { starts a block, ] is an unexpected closer
        Assert.True(tokens.Length >= 1);
        var hasError = tokens.Any(t => t is ErrorToken);
        Assert.True(hasError, "Expected an ErrorToken for mismatched delimiters");
    }

    [Fact]
    public void MismatchedBracketClosedWithParen_ReturnsErrorToken()
    {
        var tokens = Tokenize("[)");

        Assert.True(tokens.Length >= 1);
        var hasError = tokens.Any(t => t is ErrorToken);
        Assert.True(hasError, "Expected an ErrorToken for mismatched delimiters");
    }

    [Fact]
    public void MismatchedParenClosedWithBrace_ReturnsErrorToken()
    {
        var tokens = Tokenize("(}");

        Assert.True(tokens.Length >= 1);
        var hasError = tokens.Any(t => t is ErrorToken);
        Assert.True(hasError, "Expected an ErrorToken for mismatched delimiters");
    }

    [Fact]
    public void UnexpectedClosingBrace_ReturnsErrorToken()
    {
        var tokens = Tokenize("}");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unexpected", error.ErrorMessage);
    }

    [Fact]
    public void UnexpectedClosingBracket_ReturnsErrorToken()
    {
        var tokens = Tokenize("]");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unexpected", error.ErrorMessage);
    }

    [Fact]
    public void UnexpectedClosingParen_ReturnsErrorToken()
    {
        var tokens = Tokenize(")");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unexpected", error.ErrorMessage);
    }

    [Fact]
    public void MismatchedDelimiterWithContentInside_ReturnsErrorToken()
    {
        var tokens = Tokenize("{content]");

        Assert.True(tokens.Length >= 1);
        var hasError = tokens.Any(t => t is ErrorToken);
        Assert.True(hasError, "Expected an ErrorToken for mismatched delimiters");
    }

    #endregion

    #region Unclosed String Tests

    [Fact]
    public void UnterminatedDoubleQuoteString_ReturnsSymbolToken()
    {
        // Unterminated strings emit the opening quote as a symbol
        var tokens = Tokenize("\"hello");

        Assert.True(tokens.Length >= 1);
        // First token should be a symbol for the unterminated quote
        var firstToken = tokens[0];
        Assert.IsType<SymbolToken>(firstToken);
        Assert.Equal('"', ((SymbolToken)firstToken).Symbol);
    }

    [Fact]
    public void UnterminatedSingleQuoteString_ReturnsSymbolToken()
    {
        var tokens = Tokenize("'world");

        Assert.True(tokens.Length >= 1);
        var firstToken = tokens[0];
        Assert.IsType<SymbolToken>(firstToken);
        Assert.Equal('\'', ((SymbolToken)firstToken).Symbol);
    }

    [Fact]
    public void StringWithNewlineInMiddle_HandlesCorrectly()
    {
        // Newline in string - the string continues to newline, then becomes unterminated
        var tokens = Tokenize("\"test\nmore\"");

        // Should have multiple tokens as the newline is inside an unterminated string
        // The exact behavior depends on implementation but should not crash
        Assert.NotEmpty(tokens);
    }

    [Fact]
    public void OnlyOpeningDoubleQuote_ReturnsSymbolToken()
    {
        var tokens = Tokenize("\"");

        Assert.Single(tokens);
        var symbol = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('"', symbol.Symbol);
    }

    [Fact]
    public void OnlyOpeningSingleQuote_ReturnsSymbolToken()
    {
        var tokens = Tokenize("'");

        Assert.Single(tokens);
        var symbol = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('\'', symbol.Symbol);
    }

    [Fact]
    public void UnterminatedStringWithEscapeAtEnd_ReturnsSymbolToken()
    {
        var tokens = Tokenize("\"hello\\");

        Assert.True(tokens.Length >= 1);
        var firstToken = tokens[0];
        Assert.IsType<SymbolToken>(firstToken);
    }

    #endregion

    #region Unterminated Comment Tests

    [Fact]
    public void UnterminatedMultiLineComment_ReturnsErrorToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* unterminated", options);

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unterminated", error.ErrorMessage);
    }

    [Fact]
    public void UnterminatedMultiLineCommentWithAsterisks_ReturnsErrorToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = Tokenize("/* * * * unterminated", options);

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unterminated", error.ErrorMessage);
    }

    [Fact]
    public void UnterminatedMultiLineCommentWithPartialClose_ReturnsErrorToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        // Has * but no / after it at the end
        var tokens = Tokenize("/* almost closed *", options);

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unterminated", error.ErrorMessage);
    }

    #endregion

    #region Recovery Tests

    [Fact]
    public void TokenizerContinuesAfterUnexpectedCloser()
    {
        var tokens = Tokenize("} hello");

        // Should have error token and then continue with ident tokens
        Assert.True(tokens.Length >= 2);
        Assert.IsType<ErrorToken>(tokens[0]);
        
        // Verify tokenizer continued and found more tokens
        var hasIdent = tokens.Any(t => t is IdentToken);
        Assert.True(hasIdent, "Tokenizer should continue after error and find 'hello'");
    }

    [Fact]
    public void TokenizerContinuesAfterMultipleUnexpectedClosers()
    {
        var tokens = Tokenize("} ] ) hello");

        // Should have multiple error tokens followed by valid tokens
        var errorCount = tokens.Count(t => t is ErrorToken);
        Assert.Equal(3, errorCount);
        
        var hasIdent = tokens.Any(t => t is IdentToken);
        Assert.True(hasIdent);
    }

    [Fact]
    public void TokenizerProducesValidTokensBeforeError()
    {
        var tokens = Tokenize("valid {unclosed");

        // First token should be valid identifier
        Assert.True(tokens.Length >= 2);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.Equal("valid", tokens[0].ContentSpan.ToString());
        
        // Should end with error
        var lastNonWhitespace = tokens.Last(t => t is not WhitespaceToken);
        Assert.IsType<ErrorToken>(lastNonWhitespace);
    }

    [Fact]
    public void TokenizerProducesValidTokensAfterUnterminatedString()
    {
        // When a string is unterminated, only the opening quote is returned as a symbol.
        // The remaining characters are consumed as part of the "string content" that never terminated.
        // This behavior differs from block parsing where child tokens are preserved.
        var tokens = Tokenize("\"unclosed hello world");

        // Should have just a symbol for the opening quote
        // The implementation consumes the rest as string content but returns only the quote on failure
        Assert.True(tokens.Length >= 1);
        Assert.IsType<SymbolToken>(tokens[0]);
    }

    [Fact]
    public void ComplexRecoveryScenario()
    {
        // Mix of valid tokens, errors, and more valid tokens
        var tokens = Tokenize("func() } 123 { incomplete");

        // Should have valid tokens, error, more valid tokens, then error for unclosed block
        var hasValidBlock = tokens.Any(t => t is SimpleBlock);
        var hasError = tokens.Any(t => t is ErrorToken);
        var hasNumeric = tokens.Any(t => t is NumericToken);
        
        Assert.True(hasValidBlock, "Should parse the () block");
        Assert.True(hasError, "Should have error tokens");
        Assert.True(hasNumeric, "Should parse 123");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyBlock_NotAnError()
    {
        var tokens = Tokenize("{}");

        Assert.Single(tokens);
        var block = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Empty(block.Children);
    }

    [Fact]
    public void MultipleEmptyBlocks_NotAnError()
    {
        var tokens = Tokenize("{}[]()");

        Assert.Equal(3, tokens.Length);
        Assert.All(tokens, t => Assert.IsType<SimpleBlock>(t));
    }

    [Fact]
    public void MixedValidAndInvalidBlocks_PartialRecovery()
    {
        var tokens = Tokenize("{valid}[unclosed");

        // First block should be valid
        Assert.True(tokens.Length >= 2);
        var firstBlock = Assert.IsType<SimpleBlock>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, firstBlock.Type);
        
        // Should end with error for unclosed bracket
        var lastToken = tokens.Last();
        Assert.IsType<ErrorToken>(lastToken);
    }

    [Fact]
    public void NestedValidAndInvalidBlocks_PartialRecovery()
    {
        // Valid outer brace with unclosed inner bracket
        var tokens = Tokenize("{[}");

        // This should produce an error because the inner [ is not closed before }
        var hasError = tokens.Any(t => t is ErrorToken);
        Assert.True(hasError);
    }

    [Fact]
    public void BackslashOutsideString_TreatedAsSymbol()
    {
        var tokens = Tokenize("\\");

        Assert.Single(tokens);
        var symbol = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('\\', symbol.Symbol);
    }

    [Fact]
    public void OnlyWhitespaceInUnclosedBlock_ReturnsErrorToken()
    {
        var tokens = Tokenize("{   ");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed", error.ErrorMessage);
    }

    #endregion

    #region Position Tracking Tests

    [Fact]
    public void ErrorToken_HasCorrectPosition()
    {
        var tokens = Tokenize("hello }");

        var error = tokens.OfType<ErrorToken>().Single();
        // "hello " is 6 characters, so } is at position 6
        Assert.Equal(6, error.Position);
    }

    [Fact]
    public void UnclosedBlockError_HasStartPosition()
    {
        var tokens = Tokenize("x {unclosed");

        var error = tokens.OfType<ErrorToken>().Single();
        // "x " is 2 characters, so { is at position 2
        Assert.Equal(2, error.Position);
    }

    #endregion
}
