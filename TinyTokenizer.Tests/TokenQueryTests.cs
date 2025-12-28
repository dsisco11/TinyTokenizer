using System.Collections.Immutable;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the TokenQuery CSS-like selector system.
/// </summary>
public class TokenQueryTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string input)
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@', '$')
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);
        var lexer = new Lexer(options);
        var parser = new TokenParser(options);
        return parser.ParseToArray(lexer.Lex(input));
    }

    #endregion

    #region Type Query Tests

    [Fact]
    public void Query_Ident_SelectsAllIdentTokens()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Ident.Select(tokens).ToList();

        Assert.All(indices, i => Assert.IsType<IdentToken>(tokens[i]));
    }

    [Fact]
    public void Query_Comment_SelectsAllCommentTokens()
    {
        var tokens = Tokenize("a /* comment */ b // line comment");
        var indices = Query.Comment.Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
        Assert.All(indices, i => Assert.IsType<CommentToken>(tokens[i]));
    }

    [Fact]
    public void Query_String_SelectsAllStringTokens()
    {
        var tokens = Tokenize("a = \"hello\" + 'world'");
        var indices = Query.String.Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
        Assert.All(indices, i => Assert.IsType<StringToken>(tokens[i]));
    }

    [Fact]
    public void Query_Numeric_SelectsAllNumericTokens()
    {
        var tokens = Tokenize("x = 42 + 3.14");
        var indices = Query.Numeric.Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
        Assert.All(indices, i => Assert.IsType<NumericToken>(tokens[i]));
    }

    [Fact]
    public void Query_Whitespace_SelectsAllWhitespaceTokens()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Whitespace.Select(tokens).ToList();

        Assert.True(indices.Count >= 2);
        Assert.All(indices, i => Assert.IsType<WhitespaceToken>(tokens[i]));
    }

    [Fact]
    public void Query_Operator_SelectsAllOperatorTokens()
    {
        var tokens = Tokenize("a == b && c != d");
        var indices = Query.Operator.Select(tokens).ToList();

        Assert.True(indices.Count > 0);
        Assert.All(indices, i => Assert.IsType<OperatorToken>(tokens[i]));
    }

    [Fact]
    public void Query_TaggedIdent_SelectsAllTaggedIdentTokens()
    {
        var tokens = Tokenize("#define @attribute $variable");
        var indices = Query.TaggedIdent.Select(tokens).ToList();

        Assert.Equal(3, indices.Count);
        Assert.All(indices, i => Assert.IsType<TaggedIdentToken>(tokens[i]));
    }

    #endregion

    #region Block Query Tests

    [Fact]
    public void Query_Block_SelectsAllBlocks()
    {
        var tokens = Tokenize("func() [1, 2] {body}");
        var indices = Query.Block().Select(tokens).ToList();

        Assert.Equal(3, indices.Count);
        Assert.All(indices, i => Assert.IsType<SimpleBlock>(tokens[i]));
    }

    [Fact]
    public void Query_BlockWithOpener_SelectsSpecificBlocks()
    {
        var tokens = Tokenize("func() [1, 2] {body}");

        var parenIndices = Query.Block('(').Select(tokens).ToList();
        var bracketIndices = Query.Block('[').Select(tokens).ToList();
        var braceIndices = Query.Block('{').Select(tokens).ToList();

        Assert.Single(parenIndices);
        Assert.Single(bracketIndices);
        Assert.Single(braceIndices);
    }

    [Fact]
    public void Query_ParenBlock_SelectsParenthesisBlocks()
    {
        var tokens = Tokenize("foo() bar[]");
        var indices = Query.ParenBlock.Select(tokens).ToList();

        Assert.Single(indices);
        var block = Assert.IsType<SimpleBlock>(tokens[indices[0]]);
        Assert.Equal('(', block.OpeningDelimiter.FirstChar);
    }

    [Fact]
    public void Query_BraceBlock_SelectsBraceBlocks()
    {
        var tokens = Tokenize("if (x) { y }");
        var indices = Query.BraceBlock.Select(tokens).ToList();

        Assert.Single(indices);
    }

    [Fact]
    public void Query_BracketBlock_SelectsBracketBlocks()
    {
        var tokens = Tokenize("arr[0]");
        var indices = Query.BracketBlock.Select(tokens).ToList();

        Assert.Single(indices);
    }

    #endregion

    #region Index Query Tests

    [Fact]
    public void Query_Index_SelectsSingleToken()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Index(0).Select(tokens).ToList();

        Assert.Single(indices);
        Assert.Equal(0, indices[0]);
    }

    [Fact]
    public void Query_Index_OutOfRange_ReturnsEmpty()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Index(100).Select(tokens).ToList();

        Assert.Empty(indices);
    }

    [Fact]
    public void Query_Range_SelectsTokensInRange()
    {
        var tokens = Tokenize("a b c d e");
        var indices = Query.Range(1, 4).Select(tokens).ToList();

        Assert.Equal(3, indices.Count);
        Assert.Equal([1, 2, 3], indices);
    }

    [Fact]
    public void Query_First_SelectsFirstToken()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.First.Select(tokens).ToList();

        Assert.Single(indices);
        Assert.Equal(0, indices[0]);
    }

    [Fact]
    public void Query_Last_SelectsLastToken()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Last.Select(tokens).ToList();

        Assert.Single(indices);
        Assert.Equal(tokens.Length - 1, indices[0]);
    }

    #endregion

    #region Pseudo-Selector Tests

    [Fact]
    public void First_OnTypeQuery_SelectsFirstMatch()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Ident.First().Select(tokens).ToList();

        Assert.Single(indices);
        Assert.IsType<IdentToken>(tokens[indices[0]]);
    }

    [Fact]
    public void Last_OnTypeQuery_SelectsLastMatch()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Ident.Last().Select(tokens).ToList();

        Assert.Single(indices);
        Assert.IsType<IdentToken>(tokens[indices[0]]);
    }

    [Fact]
    public void Nth_OnTypeQuery_SelectsNthMatch()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Ident.Nth(1).Select(tokens).ToList();

        Assert.Single(indices);
        Assert.IsType<IdentToken>(tokens[indices[0]]);
        Assert.Equal("b", tokens[indices[0]].ContentSpan.ToString());
    }

    [Fact]
    public void All_OnTypeQuery_SelectsAllMatches()
    {
        var tokens = Tokenize("a b c");
        var allIndices = Query.Ident.All().Select(tokens).ToList();
        var directIndices = Query.Ident.Select(tokens).ToList();

        Assert.Equal(directIndices, allIndices);
    }

    #endregion

    #region Filter Tests

    [Fact]
    public void Where_FiltersByPredicate()
    {
        var tokens = Tokenize("foo bar baz");
        var indices = Query.Ident.Where(t => t.ContentSpan.Length == 3).Select(tokens).ToList();

        Assert.Equal(3, indices.Count);
    }

    [Fact]
    public void WithContent_FiltersExactContent()
    {
        var tokens = Tokenize("foo bar foo");
        var indices = Query.Ident.WithContent("foo").Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
        Assert.All(indices, i => Assert.Equal("foo", tokens[i].ContentSpan.ToString()));
    }

    [Fact]
    public void WithContentContaining_FiltersSubstring()
    {
        var tokens = Tokenize("/* TODO: fix */ /* note */ /* TODO: test */");
        var indices = Query.Comment.WithContentContaining("TODO").Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
    }

    [Fact]
    public void WithContentStartingWith_FiltersPrefix()
    {
        var tokens = Tokenize("getValue setValue reset");
        var indices = Query.Ident.WithContentStartingWith("get").Select(tokens).ToList();

        Assert.Single(indices);
        Assert.StartsWith("get", tokens[indices[0]].ContentSpan.ToString());
    }

    [Fact]
    public void WithContentEndingWith_FiltersSuffix()
    {
        var tokens = Tokenize("userId userName itemId");
        var indices = Query.Ident.WithContentEndingWith("Id").Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
    }

    #endregion

    #region Relative Query Tests

    [Fact]
    public void Before_ReturnsRelativeQuery()
    {
        var tokens = Tokenize("a b c");
        var query = Query.Ident.First().Before();

        Assert.IsType<RelativeQuery>(query);
        Assert.Equal(RelativePosition.Before, query.Position);
    }

    [Fact]
    public void After_ReturnsRelativeQuery()
    {
        var tokens = Tokenize("a b c");
        var query = Query.Ident.First().After();

        Assert.IsType<RelativeQuery>(query);
        Assert.Equal(RelativePosition.After, query.Position);
    }

    [Fact]
    public void RelativeQuery_SelectsSameIndicesAsInner()
    {
        var tokens = Tokenize("a b c");
        var innerIndices = Query.Ident.First().Select(tokens).ToList();
        var relativeIndices = Query.Ident.First().Before().Select(tokens).ToList();

        Assert.Equal(innerIndices, relativeIndices);
    }

    #endregion

    #region Composition Tests

    [Fact]
    public void Union_CombinesResults()
    {
        var tokens = Tokenize("a /* comment */ 42");
        var query = Query.Ident | Query.Comment;
        var indices = query.Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
    }

    [Fact]
    public void Union_DeduplicatesIndices()
    {
        var tokens = Tokenize("a b c");
        var query = Query.Ident | Query.Ident;
        var indices = query.Select(tokens).ToList();

        // Should not have duplicate indices
        Assert.Equal(indices.Distinct().Count(), indices.Count);
    }

    [Fact]
    public void Intersection_FiltersResults()
    {
        var tokens = Tokenize("foo bar foobaz");
        var query = Query.Ident & Query.WithContentStartingWith("foo");
        var indices = query.Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
        Assert.All(indices, i => Assert.StartsWith("foo", tokens[i].ContentSpan.ToString()));
    }

    [Fact]
    public void Matches_WorksForSingleToken()
    {
        var identToken = new IdentToken { Content = "test".AsMemory(), Position = 0 };
        var commentToken = new CommentToken { Content = "// comment".AsMemory(), IsMultiLine = false, Position = 0 };

        Assert.True(Query.Ident.Matches(identToken));
        Assert.False(Query.Ident.Matches(commentToken));
        Assert.True(Query.Comment.Matches(commentToken));
    }

    #endregion

    #region Pattern Query Tests

    [Fact]
    public void Query_Pattern_SelectsMatchedPatterns()
    {
        var tokens = Tokenize("func() other() data");
        var matchedTokens = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());
        var indices = Query.Pattern(TokenDefinitions.FunctionCall()).Select(matchedTokens).ToList();

        Assert.Equal(2, indices.Count);
        Assert.All(indices, i => Assert.IsType<FunctionCallToken>(matchedTokens[i]));
    }

    #endregion

    #region Query Factory Tests

    [Fact]
    public void Query_Any_MatchesAllTokens()
    {
        var tokens = Tokenize("a 42 'str'");
        var indices = Query.Any.Select(tokens).ToList();

        Assert.Equal(tokens.Length, indices.Count);
    }

    [Fact]
    public void Query_Of_Type_SelectsSpecificType()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Of.Type<IdentToken>().Select(tokens).ToList();

        Assert.All(indices, i => Assert.IsType<IdentToken>(tokens[i]));
    }

    [Fact]
    public void Query_WithContent_StandaloneWorks()
    {
        var tokens = Tokenize("foo bar foo");
        var indices = Query.WithContent("foo").Select(tokens).ToList();

        Assert.Equal(2, indices.Count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyTokenArray_ReturnsNoMatches()
    {
        var tokens = ImmutableArray<Token>.Empty;
        var indices = Query.Ident.Select(tokens).ToList();

        Assert.Empty(indices);
    }

    [Fact]
    public void NoMatches_ReturnsEmpty()
    {
        var tokens = Tokenize("a b c");
        var indices = Query.Comment.Select(tokens).ToList();

        Assert.Empty(indices);
    }

    [Fact]
    public void ChainedFilters_WorkCorrectly()
    {
        var tokens = Tokenize("fooBar fooBaz barFoo");
        var indices = Query.Ident
            .WithContentStartingWith("foo")
            .WithContentEndingWith("Bar")
            .Select(tokens)
            .ToList();

        Assert.Single(indices);
        Assert.Equal("fooBar", tokens[indices[0]].ContentSpan.ToString());
    }

    #endregion
}
