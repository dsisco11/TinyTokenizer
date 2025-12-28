namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for TokenSelector types and Match factory methods.
/// </summary>
public class TokenSelectorTests
{
    #region Helper Methods
    
    private static Token CreateIdentToken(string content) =>
        new IdentToken { Content = content.AsMemory(), Position = 0 };
    
    private static Token CreateWhitespaceToken(string content = " ") =>
        new WhitespaceToken { Content = content.AsMemory(), Position = 0 };
    
    private static Token CreateSymbolToken(char symbol) =>
        new SymbolToken { Content = symbol.ToString().AsMemory(), Position = 0 };
    
    private static Token CreateOperatorToken(string op) =>
        new OperatorToken { Content = op.AsMemory(), Position = 0 };
    
    private static Token CreateNumericToken(string value, NumericType type) =>
        new NumericToken { Content = value.AsMemory(), NumericType = type, Position = 0 };
    
    private static Token CreateStringToken(string content, char quote) =>
        new StringToken { Content = content.AsMemory(), Quote = quote, Position = 0 };
    
    private static Token CreateCommentToken(string content) =>
        new CommentToken { Content = content.AsMemory(), IsMultiLine = false, Position = 0 };
    
    private static Token CreateTaggedIdentToken(string content, char tag) =>
        new TaggedIdentToken { Content = content.AsMemory(), Tag = tag, Name = content.AsMemory()[1..], Position = 0 };
    
    #endregion
    
    #region AnySelector Tests
    
    [Fact]
    public void AnySelector_Matches_AnyToken()
    {
        var selector = Match.Any();
        
        Assert.True(selector.Matches(CreateIdentToken("test")));
        Assert.True(selector.Matches(CreateWhitespaceToken()));
        Assert.True(selector.Matches(CreateSymbolToken(':')));
        Assert.True(selector.Matches(CreateNumericToken("123", NumericType.Integer)));
    }
    
    [Fact]
    public void AnySelector_Description_IsAny()
    {
        var selector = Match.Any();
        Assert.Equal("Any", selector.Description);
    }
    
    #endregion
    
    #region IdentSelector Tests
    
    [Fact]
    public void IdentSelector_Matches_AnyIdent()
    {
        var selector = Match.Ident();
        
        Assert.True(selector.Matches(CreateIdentToken("hello")));
        Assert.True(selector.Matches(CreateIdentToken("world")));
        Assert.False(selector.Matches(CreateWhitespaceToken()));
        Assert.False(selector.Matches(CreateSymbolToken(':')));
    }
    
    [Fact]
    public void IdentSelector_WithContent_MatchesExact()
    {
        var selector = Match.Ident("test");
        
        Assert.True(selector.Matches(CreateIdentToken("test")));
        Assert.False(selector.Matches(CreateIdentToken("other")));
        Assert.False(selector.Matches(CreateIdentToken("TEST")));
    }
    
    [Fact]
    public void IdentSelector_Description_IsCorrect()
    {
        Assert.Equal("Ident", Match.Ident().Description);
        Assert.Equal("Ident 'foo'", Match.Ident("foo").Description);
    }
    
    #endregion
    
    #region WhitespaceSelector Tests
    
    [Fact]
    public void WhitespaceSelector_Matches_Whitespace()
    {
        var selector = Match.Whitespace();
        
        Assert.True(selector.Matches(CreateWhitespaceToken(" ")));
        Assert.True(selector.Matches(CreateWhitespaceToken("\t")));
        Assert.False(selector.Matches(CreateIdentToken("test")));
    }
    
    [Fact]
    public void WhitespaceSelector_Description_IsWhitespace()
    {
        Assert.Equal("Whitespace", Match.Whitespace().Description);
    }
    
    #endregion
    
    #region SymbolSelector Tests
    
    [Fact]
    public void SymbolSelector_Matches_SpecificSymbol()
    {
        var selector = Match.Symbol(':');
        
        Assert.True(selector.Matches(CreateSymbolToken(':')));
        Assert.False(selector.Matches(CreateSymbolToken(';')));
        Assert.False(selector.Matches(CreateIdentToken(":")));
    }
    
    [Fact]
    public void SymbolSelector_Description_IsCorrect()
    {
        Assert.Equal("Symbol ':'", Match.Symbol(':').Description);
    }
    
    #endregion
    
    #region OperatorSelector Tests
    
    [Fact]
    public void OperatorSelector_Matches_AnyOperator()
    {
        var selector = Match.Operator();
        
        Assert.True(selector.Matches(CreateOperatorToken("==")));
        Assert.True(selector.Matches(CreateOperatorToken("!=")));
        Assert.False(selector.Matches(CreateIdentToken("==")));
    }
    
    [Fact]
    public void OperatorSelector_WithOperator_MatchesExact()
    {
        var selector = Match.Operator("==");
        
        Assert.True(selector.Matches(CreateOperatorToken("==")));
        Assert.False(selector.Matches(CreateOperatorToken("!=")));
    }
    
    [Fact]
    public void OperatorSelector_Description_IsCorrect()
    {
        Assert.Equal("Operator", Match.Operator().Description);
        Assert.Equal("Operator '=='", Match.Operator("==").Description);
    }
    
    #endregion
    
    #region NumericSelector Tests
    
    [Fact]
    public void NumericSelector_Matches_AnyNumeric()
    {
        var selector = Match.Numeric();
        
        Assert.True(selector.Matches(CreateNumericToken("123", NumericType.Integer)));
        Assert.True(selector.Matches(CreateNumericToken("3.14", NumericType.FloatingPoint)));
        Assert.False(selector.Matches(CreateIdentToken("123")));
    }
    
    [Fact]
    public void NumericSelector_WithType_MatchesSpecificType()
    {
        var intSelector = Match.Numeric(NumericType.Integer);
        var floatSelector = Match.Numeric(NumericType.FloatingPoint);
        
        var intToken = CreateNumericToken("123", NumericType.Integer);
        var floatToken = CreateNumericToken("3.14", NumericType.FloatingPoint);
        
        Assert.True(intSelector.Matches(intToken));
        Assert.False(intSelector.Matches(floatToken));
        
        Assert.True(floatSelector.Matches(floatToken));
        Assert.False(floatSelector.Matches(intToken));
    }
    
    [Fact]
    public void NumericSelector_Description_IsCorrect()
    {
        Assert.Equal("Numeric", Match.Numeric().Description);
        Assert.Equal("Numeric (Integer)", Match.Numeric(NumericType.Integer).Description);
        Assert.Equal("Numeric (FloatingPoint)", Match.Numeric(NumericType.FloatingPoint).Description);
    }
    
    #endregion
    
    #region StringSelector Tests
    
    [Fact]
    public void StringSelector_Matches_AnyString()
    {
        var selector = Match.String();
        
        Assert.True(selector.Matches(CreateStringToken("\"hello\"", '"')));
        Assert.True(selector.Matches(CreateStringToken("'c'", '\'')));
        Assert.False(selector.Matches(CreateIdentToken("hello")));
    }
    
    [Fact]
    public void StringSelector_WithQuote_MatchesSpecificQuote()
    {
        var doubleQuoteSelector = Match.String('"');
        var singleQuoteSelector = Match.String('\'');
        
        var doubleQuoted = CreateStringToken("\"hello\"", '"');
        var singleQuoted = CreateStringToken("'c'", '\'');
        
        Assert.True(doubleQuoteSelector.Matches(doubleQuoted));
        Assert.False(doubleQuoteSelector.Matches(singleQuoted));
        
        Assert.True(singleQuoteSelector.Matches(singleQuoted));
        Assert.False(singleQuoteSelector.Matches(doubleQuoted));
    }
    
    [Fact]
    public void StringSelector_Description_IsCorrect()
    {
        Assert.Equal("String", Match.String().Description);
        Assert.Equal("String (\")", Match.String('"').Description);
    }
    
    #endregion
    
    #region CommentSelector Tests
    
    [Fact]
    public void CommentSelector_Matches_Comments()
    {
        var selector = Match.Comment();
        
        Assert.True(selector.Matches(CreateCommentToken("// comment")));
        Assert.False(selector.Matches(CreateIdentToken("comment")));
    }
    
    [Fact]
    public void CommentSelector_Description_IsComment()
    {
        Assert.Equal("Comment", Match.Comment().Description);
    }
    
    #endregion
    
    #region TaggedIdentSelector Tests
    
    [Fact]
    public void TaggedIdentSelector_Matches_AnyTaggedIdent()
    {
        var selector = Match.TaggedIdent();
        
        Assert.True(selector.Matches(CreateTaggedIdentToken("#define", '#')));
        Assert.True(selector.Matches(CreateTaggedIdentToken("@attr", '@')));
        Assert.False(selector.Matches(CreateIdentToken("define")));
    }
    
    [Fact]
    public void TaggedIdentSelector_WithPrefix_MatchesSpecificPrefix()
    {
        var hashSelector = Match.TaggedIdent('#');
        var atSelector = Match.TaggedIdent('@');
        
        var hashToken = CreateTaggedIdentToken("#define", '#');
        var atToken = CreateTaggedIdentToken("@attr", '@');
        
        Assert.True(hashSelector.Matches(hashToken));
        Assert.False(hashSelector.Matches(atToken));
        
        Assert.True(atSelector.Matches(atToken));
        Assert.False(atSelector.Matches(hashToken));
    }
    
    [Fact]
    public void TaggedIdentSelector_Description_IsCorrect()
    {
        Assert.Equal("TaggedIdent", Match.TaggedIdent().Description);
        Assert.Equal("TaggedIdent '#'", Match.TaggedIdent('#').Description);
    }
    
    #endregion
    
    #region SimpleBlockSelector Tests
    
    [Fact]
    public void SimpleBlockSelector_Matches_AnyBlock()
    {
        var selector = Match.Block();
        var tokens = "{ } [ ] ( )".TokenizeToTokens();
        
        var blocks = tokens.Where(t => t.Type == TokenType.BraceBlock 
                                     || t.Type == TokenType.BracketBlock 
                                     || t.Type == TokenType.ParenthesisBlock);
        
        foreach (var block in blocks)
        {
            Assert.True(selector.Matches(block));
        }
        
        Assert.False(selector.Matches(CreateIdentToken("block")));
    }
    
    [Fact]
    public void SimpleBlockSelector_WithOpener_MatchesSpecificBlock()
    {
        var braceSelector = Match.Block('{');
        var bracketSelector = Match.Block('[');
        var parenSelector = Match.Block('(');
        
        var tokens = "{a} [b] (c)".TokenizeToTokens();
        var braceBlock = tokens.First(t => t.Type == TokenType.BraceBlock);
        var bracketBlock = tokens.First(t => t.Type == TokenType.BracketBlock);
        var parenBlock = tokens.First(t => t.Type == TokenType.ParenthesisBlock);
        
        Assert.True(braceSelector.Matches(braceBlock));
        Assert.False(braceSelector.Matches(bracketBlock));
        Assert.False(braceSelector.Matches(parenBlock));
        
        Assert.True(bracketSelector.Matches(bracketBlock));
        Assert.True(parenSelector.Matches(parenBlock));
    }
    
    [Fact]
    public void SimpleBlockSelector_Description_IsCorrect()
    {
        Assert.Equal("Block", Match.Block().Description);
        Assert.Equal("Block '{'", Match.Block('{').Description);
    }
    
    #endregion
    
    #region AnyOfSelector Tests
    
    [Fact]
    public void AnyOfSelector_Matches_Alternatives()
    {
        var selector = Match.AnyOf(Match.Ident(), Match.Numeric());
        
        Assert.True(selector.Matches(CreateIdentToken("test")));
        Assert.True(selector.Matches(CreateNumericToken("123", NumericType.Integer)));
        Assert.False(selector.Matches(CreateWhitespaceToken()));
    }
    
    [Fact]
    public void AnyOfSelector_EmptyAlternatives_ReturnsFalse()
    {
        var selector = new AnyOfSelector { Alternatives = [] };
        
        Assert.False(selector.Matches(CreateIdentToken("test")));
    }
    
    [Fact]
    public void AnyOfSelector_Description_IsCorrect()
    {
        var selector = Match.AnyOf(Match.Ident(), Match.Numeric());
        Assert.Equal("AnyOf(Ident | Numeric)", selector.Description);
    }
    
    #endregion
    
    #region ContentSelector Tests
    
    [Fact]
    public void ContentSelector_Prefix_MatchesStartsWith()
    {
        var selector = Match.ContentStartsWith("get");
        
        Assert.True(selector.Matches(CreateIdentToken("getValue")));
        Assert.True(selector.Matches(CreateIdentToken("getData")));
        Assert.False(selector.Matches(CreateIdentToken("setValue")));
    }
    
    [Fact]
    public void ContentSelector_Suffix_MatchesEndsWith()
    {
        var selector = Match.ContentEndsWith("Async");
        
        Assert.True(selector.Matches(CreateIdentToken("loadAsync")));
        Assert.True(selector.Matches(CreateIdentToken("saveAsync")));
        Assert.False(selector.Matches(CreateIdentToken("loadSync")));
    }
    
    [Fact]
    public void ContentSelector_Contains_MatchesSubstring()
    {
        var selector = Match.ContentContains("Data");
        
        Assert.True(selector.Matches(CreateIdentToken("loadData")));
        Assert.True(selector.Matches(CreateIdentToken("DataLoader")));
        Assert.True(selector.Matches(CreateIdentToken("processDataHandler")));
        Assert.False(selector.Matches(CreateIdentToken("loadInfo")));
    }
    
    [Fact]
    public void ContentSelector_Predicate_MatchesCustomLogic()
    {
        var selector = Match.ContentMatches(content => content.Length > 5);
        
        Assert.True(selector.Matches(CreateIdentToken("longname")));
        Assert.False(selector.Matches(CreateIdentToken("short")));
        Assert.False(selector.Matches(CreateIdentToken("abc")));
    }
    
    [Fact]
    public void ContentSelector_MultipleConditions_AllMustMatch()
    {
        var selector = new ContentSelector 
        { 
            Prefix = "get", 
            Suffix = "Async",
            Contains = "Data"
        };
        
        Assert.True(selector.Matches(CreateIdentToken("getDataAsync")));
        Assert.False(selector.Matches(CreateIdentToken("getValueAsync"))); // No "Data"
        Assert.False(selector.Matches(CreateIdentToken("setDataAsync"))); // Wrong prefix
        Assert.False(selector.Matches(CreateIdentToken("getDataSync"))); // Wrong suffix
    }
    
    [Fact]
    public void ContentSelector_Description_IsCorrect()
    {
        Assert.Contains("starts with 'get'", Match.ContentStartsWith("get").Description);
        Assert.Contains("ends with 'Async'", Match.ContentEndsWith("Async").Description);
        Assert.Contains("contains 'Data'", Match.ContentContains("Data").Description);
        Assert.Contains("matches predicate", Match.ContentMatches(_ => true).Description);
    }
    
    [Fact]
    public void ContentSelector_EmptyDescription_WhenNoConditions()
    {
        var selector = new ContentSelector();
        Assert.Equal("Content", selector.Description);
    }
    
    #endregion
}
