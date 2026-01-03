namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for Unicode character handling in the tokenizer.
/// Covers international identifiers, emoji, and invisible characters.
/// </summary>
public class UnicodeTests
{
    #region Helper Methods

    private static Token[] Tokenize(string source, TokenizerOptions? options = null)
    {
        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);
        var simpleTokens = lexer.Lex(source);
        return [.. parser.Parse(simpleTokens)];
    }

    private static SimpleToken[] Lex(string source)
    {
        var lexer = new Lexer();
        return [.. lexer.Lex(source)];
    }

    #endregion

    #region Unicode Identifier Tests

    [Theory]
    [InlineData("変数", "Japanese")]
    [InlineData("変数名", "Japanese multi-char")]
    [InlineData("こんにちは", "Hiragana")]
    [InlineData("カタカナ", "Katakana")]
    public void Tokenize_JapaneseIdentifier_RecognizedAsIdent(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Theory]
    [InlineData("переменная", "Russian")]
    [InlineData("функция", "Russian function")]
    [InlineData("Привет", "Russian greeting")]
    public void Tokenize_CyrillicIdentifier_RecognizedAsIdent(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Theory]
    [InlineData("משתנה", "Hebrew variable")]
    [InlineData("פונקציה", "Hebrew function")]
    public void Tokenize_HebrewIdentifier_RecognizedAsIdent(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Theory]
    [InlineData("المتغير", "Arabic variable")]
    [InlineData("دالة", "Arabic function")]
    public void Tokenize_ArabicIdentifier_RecognizedAsIdent(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Theory]
    [InlineData("变量", "Simplified Chinese")]
    [InlineData("變數", "Traditional Chinese")]
    [InlineData("函数", "Simplified function")]
    public void Tokenize_ChineseIdentifier_RecognizedAsIdent(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Theory]
    [InlineData("αβγ", "Greek lowercase")]
    [InlineData("ΑΒΓ", "Greek uppercase")]
    [InlineData("λfunc", "Greek lambda prefix")]
    public void Tokenize_GreekIdentifier_RecognizedAsIdent(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_MixedScriptIdentifiers_RecognizedSeparately()
    {
        var tokens = Tokenize("hello 変数 world");

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Equal(3, idents.Length);
        Assert.Equal("hello", idents[0].ContentSpan.ToString());
        Assert.Equal("変数", idents[1].ContentSpan.ToString());
        Assert.Equal("world", idents[2].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_UnicodeInExpression_ParsedCorrectly()
    {
        var tokens = Tokenize("変数 = 42");

        // 変数 = 42 produces: IdentToken, WhitespaceToken, OperatorToken, WhitespaceToken, NumericToken
        Assert.Equal(5, tokens.Length);
        Assert.IsType<IdentToken>(tokens[0]);
        Assert.Equal("変数", tokens[0].ContentSpan.ToString());
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<OperatorToken>(tokens[2]);
        Assert.Equal("=", tokens[2].ContentSpan.ToString());
    }

    #endregion

    #region Emoji Tests

    [Theory]
    [InlineData("🚀rocket", "Emoji prefix")]
    [InlineData("🎉celebration", "Party emoji prefix")]
    [InlineData("🔥fire", "Fire emoji prefix")]
    public void Tokenize_EmojiPrefix_HandledAsIdentifier(string input, string description)
    {
        var tokens = Tokenize(input);

        // Emoji should be part of the identifier (not a terminator)
        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Theory]
    [InlineData("var_🎉", "Emoji suffix")]
    [InlineData("rocket🚀", "Rocket suffix")]
    [InlineData("test_🔥_hot", "Emoji middle")]
    public void Tokenize_EmojiSuffix_HandledAsIdentifier(string input, string description)
    {
        var tokens = Tokenize(input);

        // Emoji should be part of the identifier
        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_StandaloneEmoji_RecognizedAsIdentifier()
    {
        var tokens = Tokenize("🚀");

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal("🚀", idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_MultipleEmoji_RecognizedAsSingleIdentifier()
    {
        var tokens = Tokenize("🚀🎉🔥");

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal("🚀🎉🔥", idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_EmojiSeparatedByWhitespace_SeparateIdentifiers()
    {
        var tokens = Tokenize("🚀 🎉");

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Equal(2, idents.Length);
        Assert.Equal("🚀", idents[0].ContentSpan.ToString());
        Assert.Equal("🎉", idents[1].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_EmojiInString_PreservedInStringContent()
    {
        var tokens = Tokenize("\"Hello 🌍 World\"");

        var strings = tokens.OfType<StringToken>().ToArray();
        Assert.Single(strings);
        Assert.Contains("🌍", strings[0].ContentSpan.ToString());
    }

    #endregion

    #region Invisible Character Tests

    [Theory]
    [InlineData("\u200B", "Zero-width space")]
    [InlineData("\u200C", "Zero-width non-joiner")]
    [InlineData("\u200D", "Zero-width joiner")]
    public void Tokenize_ZeroWidthCharacter_HandledWithoutCrash(string input, string description)
    {
        // Should not throw - exact behavior may vary but must be handled
        var tokens = Tokenize(input);
        Assert.NotNull(tokens);
    }

    [Fact]
    public void Tokenize_ByteOrderMark_HandledCorrectly()
    {
        // BOM (\uFEFF) at start of input - commonly seen in UTF-8 files
        var input = "\uFEFFvar x = 1";
        var tokens = Tokenize(input);

        // Should tokenize the content after BOM
        Assert.NotNull(tokens);
        Assert.Contains(tokens, t => t is IdentToken && t.ContentSpan.ToString().Contains("var"));
    }

    [Fact]
    public void Tokenize_ZeroWidthSpaceInIdentifier_IncludedInIdentifier()
    {
        // Zero-width space between letters
        var input = "hello\u200Bworld";
        var tokens = Tokenize(input);

        // The zero-width character is not a terminator, so it should be one identifier
        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_NonBreakingSpace_TreatedAsWhitespace()
    {
        // Non-breaking space (\u00A0) should be treated as whitespace
        var input = "hello\u00A0world";
        var tokens = Tokenize(input);

        // Should produce two identifiers separated by whitespace
        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Equal(2, idents.Length);
        Assert.Equal("hello", idents[0].ContentSpan.ToString());
        Assert.Equal("world", idents[1].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_IdeographicSpace_TreatedAsWhitespace()
    {
        // Ideographic space (\u3000) used in CJK text
        var input = "変数\u3000値";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Equal(2, idents.Length);
        Assert.Equal("変数", idents[0].ContentSpan.ToString());
        Assert.Equal("値", idents[1].ContentSpan.ToString());
    }

    #endregion

    #region Combining Characters and Diacritics

    [Theory]
    [InlineData("café", "Precomposed é")]
    [InlineData("cafe\u0301", "Combining acute accent")]
    [InlineData("naïve", "Precomposed ï")]
    public void Tokenize_AccentedCharacters_RecognizedAsIdentifier(string input, string description)
    {
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
    }

    [Fact]
    public void Tokenize_CombiningCharacterSequence_SingleIdentifier()
    {
        // "e" followed by combining acute accent = é
        var input = "e\u0301";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Tokenize_SurrogatePair_HandledCorrectly()
    {
        // 𝕳𝖊𝖑𝖑𝖔 - Mathematical Fraktur letters (outside BMP)
        var input = "𝕳𝖊𝖑𝖑𝖔";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_MixedBmpAndSurrogatePairs_SingleIdentifier()
    {
        // Mix of BMP characters and surrogate pairs
        var input = "test𝕳𝖊𝖑𝖑𝖔more";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_UnicodeDigits_RecognizedAsIdentifier()
    {
        // Arabic-Indic digits - not ASCII digits, so treated as identifier
        var input = "٠١٢٣٤";  // Arabic-Indic 0-4
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
    }

    [Fact]
    public void Tokenize_FullWidthCharacters_RecognizedAsIdentifier()
    {
        // Full-width Latin letters used in CJK contexts
        var input = "ＡＢＣ";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal(input, idents[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_UnicodeInComment_PreservedCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tokens = Tokenize("// コメント 🚀", options);

        var comments = tokens.OfType<CommentToken>().ToArray();
        Assert.Single(comments);
        Assert.Contains("コメント", comments[0].ContentSpan.ToString());
        Assert.Contains("🚀", comments[0].ContentSpan.ToString());
    }

    [Fact]
    public void Tokenize_UnicodeInBlock_PreservedCorrectly()
    {
        var tokens = Tokenize("{変数}");

        var blocks = tokens.OfType<SimpleBlock>().ToArray();
        Assert.Single(blocks);
        
        var children = blocks[0].Children;
        var idents = children.OfType<IdentToken>().ToArray();
        Assert.Single(idents);
        Assert.Equal("変数", idents[0].ContentSpan.ToString());
    }

    #endregion

    #region Position Tracking with Unicode

    [Fact]
    public void Tokenize_UnicodePositions_TrackCharOffsets()
    {
        // Position should track char offset (UTF-16 code units), not grapheme clusters
        var input = "a 变 b";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Equal(3, idents.Length);
        
        Assert.Equal(0, idents[0].Position); // 'a'
        Assert.Equal(2, idents[1].Position); // '变' at index 2 (after 'a' and space)
        Assert.Equal(4, idents[2].Position); // 'b' at index 4 (after '变' and space)
    }

    [Fact]
    public void Tokenize_SurrogatePairPositions_TrackCodeUnits()
    {
        // Surrogate pairs take 2 UTF-16 code units
        var input = "a 🚀 b";
        var tokens = Tokenize(input);

        var idents = tokens.OfType<IdentToken>().ToArray();
        Assert.Equal(3, idents.Length);
        
        Assert.Equal(0, idents[0].Position); // 'a'
        Assert.Equal(2, idents[1].Position); // '🚀' at index 2
        // '🚀' is a surrogate pair (2 code units), so 'b' is at index 5
        Assert.Equal(5, idents[2].Position);
    }

    #endregion
}
