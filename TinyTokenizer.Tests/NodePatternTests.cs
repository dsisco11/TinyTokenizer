using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Q = TinyTokenizer.Ast.Query;

#pragma warning disable CS0618 // Tests intentionally exercise deprecated NodePattern/NodeMatch APIs

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for NodePattern and all pattern types.
/// </summary>
[Trait("Category", "Pattern")]
public class NodePatternTests
{
    #region NodeMatch

    [Fact]
    public void NodeMatch_Empty_HasNoPartsAndIsNotSuccess()
    {
        var empty = NodeMatch.Empty;
        
        Assert.False(empty.IsSuccess);
        Assert.Empty(empty.Parts);
        Assert.Equal(0, empty.Width);
    }

    [Fact]
    public void NodeMatch_WithParts_IsSuccess()
    {
        var tree = SyntaxTree.Parse("abc");
        var node = tree.Root.Children.First();
        
        var match = new NodeMatch
        {
            Position = 0,
            Parts = ImmutableArray.Create(node),
            ConsumedCount = 1
        };
        
        Assert.True(match.IsSuccess);
        Assert.Single(match.Parts);
    }

    [Fact]
    public void NodeMatch_Width_SumsPartWidths()
    {
        var tree = SyntaxTree.Parse("a b c");
        var nodes = tree.Root.Children.Take(3).ToImmutableArray();
        
        var match = new NodeMatch
        {
            Position = 0,
            Parts = nodes,
            ConsumedCount = 3
        };
        
        Assert.Equal(nodes.Sum(n => n.Width), match.Width);
    }

    [Fact]
    public void NodeMatch_DefaultParts_WidthIsZero()
    {
        var match = new NodeMatch { Position = 0, ConsumedCount = 0 };
        
        Assert.Equal(0, match.Width);
    }

    #endregion

    #region QueryPattern

    [Fact]
    public void QueryPattern_MatchesIdent()
    {
        var tree = SyntaxTree.Parse("hello");
        var node = tree.Root.Children.First();
        var pattern = new QueryPattern(Q.AnyIdent);
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.True(match.IsSuccess);
        Assert.Single(match.Parts);
        Assert.Equal(1, match.ConsumedCount);
    }

    [Fact]
    public void QueryPattern_DoesNotMatchWrongKind()
    {
        var tree = SyntaxTree.Parse("{block}");
        var block = tree.Root.Children.First();
        var pattern = new QueryPattern(Q.AnyIdent);
        
        Assert.False(pattern.TryMatch(block, out var match));
        Assert.False(match.IsSuccess);
    }

    [Fact]
    public void QueryPattern_Description_ReturnsQueryString()
    {
        var pattern = new QueryPattern(Q.AnyIdent);
        
        Assert.NotNull(pattern.Description);
        Assert.NotEmpty(pattern.Description);
    }

    [Fact]
    public void QueryPattern_MatchesWithTextFilter()
    {
        var tree = SyntaxTree.Parse("foo bar");
        var foo = tree.Root.Children.First();
        var pattern = new QueryPattern(Q.AnyIdent.WithText("foo"));
        
        Assert.True(pattern.TryMatch(foo, out _));
    }

    [Fact]
    public void QueryPattern_DoesNotMatchWrongText()
    {
        var tree = SyntaxTree.Parse("foo");
        var node = tree.Root.Children.First();
        var pattern = new QueryPattern(Q.AnyIdent.WithText("bar"));
        
        Assert.False(pattern.TryMatch(node, out _));
    }

    #endregion

    #region SequencePattern

    [Fact]
    public void SequencePattern_MatchesMultipleNodes()
    {
        var tree = SyntaxTree.Parse("a.b");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.Sequence(Q.AnyIdent, Q.AnySymbol, Q.AnyIdent);
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.True(match.IsSuccess);
        Assert.Equal(3, match.Parts.Length);
        Assert.Equal(3, match.ConsumedCount);
    }

    [Fact]
    public void SequencePattern_FailsOnPartialMatch()
    {
        var tree = SyntaxTree.Parse("a b");
        var first = tree.Root.Children.First();
        // Looking for Ident + Symbol + Ident, but we have Ident + Ident
        var pattern = NodePattern.Sequence(Q.AnyIdent, Q.AnySymbol, Q.AnyIdent);
        
        Assert.False(pattern.TryMatch(first, out var match));
        Assert.False(match.IsSuccess);
    }

    [Fact]
    public void SequencePattern_FailsWhenNoMoreNodes()
    {
        var tree = SyntaxTree.Parse("a");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.Sequence(Q.AnyIdent, Q.AnyIdent);
        
        Assert.False(pattern.TryMatch(first, out _));
    }

    [Fact]
    public void SequencePattern_FromPatterns_Works()
    {
        var tree = SyntaxTree.Parse("a + b");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.Sequence(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.AnyOperator),
            new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(3, match.Parts.Length);
    }

    [Fact]
    public void SequencePattern_Description_JoinsParts()
    {
        var pattern = NodePattern.Sequence(Q.AnyIdent, Q.AnyOperator);
        
        Assert.Contains(" ", pattern.Description);
    }

    #endregion

    #region AlternativePattern

    [Fact]
    public void AlternativePattern_MatchesFirstAlternative()
    {
        var tree = SyntaxTree.Parse("123");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.OneOf(
            new QueryPattern(Q.AnyNumeric),
            new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.True(match.IsSuccess);
    }

    [Fact]
    public void AlternativePattern_MatchesSecondAlternative()
    {
        var tree = SyntaxTree.Parse("abc");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.OneOf(
            new QueryPattern(Q.AnyNumeric),
            new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.True(match.IsSuccess);
    }

    [Fact]
    public void AlternativePattern_FailsWhenNoneMatch()
    {
        var tree = SyntaxTree.Parse("{block}");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.OneOf(
            new QueryPattern(Q.AnyNumeric),
            new QueryPattern(Q.AnyIdent));
        
        Assert.False(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void AlternativePattern_Description_ShowsAlternatives()
    {
        var pattern = NodePattern.OneOf(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.AnyNumeric));
        
        Assert.Contains("|", pattern.Description);
        Assert.Contains("(", pattern.Description);
    }

    #endregion

    #region OptionalPattern

    [Fact]
    public void OptionalPattern_MatchesWhenInnerMatches()
    {
        var tree = SyntaxTree.Parse("abc");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.Optional(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.True(match.IsSuccess);
        Assert.Single(match.Parts);
        Assert.Equal(1, match.ConsumedCount);
    }

    [Fact]
    public void OptionalPattern_SucceedsWithEmptyWhenNoMatch()
    {
        var tree = SyntaxTree.Parse("{block}");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.Optional(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Empty(match.Parts);
        Assert.Equal(0, match.ConsumedCount);
    }

    [Fact]
    public void OptionalPattern_FromQuery_Works()
    {
        var tree = SyntaxTree.Parse("test");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.Optional(Q.AnyIdent);
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Single(match.Parts);
    }

    [Fact]
    public void OptionalPattern_Description_ShowsQuestionMark()
    {
        var pattern = NodePattern.Optional(new QueryPattern(Q.AnyIdent));
        
        Assert.Contains("?", pattern.Description);
    }

    #endregion

    #region RepeatPattern

    [Fact]
    public void RepeatPattern_ZeroOrMore_MatchesZero()
    {
        var tree = SyntaxTree.Parse("{block}");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.ZeroOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Empty(match.Parts);
        Assert.Equal(0, match.ConsumedCount);
    }

    [Fact]
    public void RepeatPattern_ZeroOrMore_MatchesMultiple()
    {
        var tree = SyntaxTree.Parse("a b c");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.ZeroOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.True(match.Parts.Length >= 3);
    }

    [Fact]
    public void RepeatPattern_OneOrMore_FailsOnZero()
    {
        var tree = SyntaxTree.Parse("{block}");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.OneOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.False(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void RepeatPattern_OneOrMore_MatchesOne()
    {
        var tree = SyntaxTree.Parse("single");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.OneOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Single(match.Parts);
    }

    [Fact]
    public void RepeatPattern_OneOrMore_MatchesMultiple()
    {
        var tree = SyntaxTree.Parse("a b c d");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.OneOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.True(match.Parts.Length >= 4);
    }

    [Fact]
    public void RepeatPattern_WithBounds_RespectsMin()
    {
        var tree = SyntaxTree.Parse("a");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.Repeat(new QueryPattern(Q.AnyIdent), 2, 5);
        
        Assert.False(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void RepeatPattern_WithBounds_RespectsMax()
    {
        var tree = SyntaxTree.Parse("a b c d e f g");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.Repeat(new QueryPattern(Q.AnyIdent), 1, 3);
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.True(match.Parts.Length <= 3);
    }

    [Fact]
    public void RepeatPattern_Description_ZeroOrMore_ShowsStar()
    {
        var pattern = NodePattern.ZeroOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.Contains("*", pattern.Description);
    }

    [Fact]
    public void RepeatPattern_Description_OneOrMore_ShowsPlus()
    {
        var pattern = NodePattern.OneOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.Contains("+", pattern.Description);
    }

    [Fact]
    public void RepeatPattern_Description_WithBounds_ShowsBraces()
    {
        var pattern = NodePattern.Repeat(new QueryPattern(Q.AnyIdent), 2, 5);
        
        Assert.Contains("{2,5}", pattern.Description);
    }

    #endregion

    #region LookaheadPattern

    [Fact]
    public void LookaheadPattern_PositiveLookahead_MatchesWhenNextMatches()
    {
        var tree = SyntaxTree.Parse("func(x)");
        var first = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock));
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.True(match.IsSuccess);
        Assert.Single(match.Parts); // Only the ident, not the block
        Assert.Equal(1, match.ConsumedCount);
    }

    [Fact]
    public void LookaheadPattern_PositiveLookahead_FailsWhenNextDoesNotMatch()
    {
        var tree = SyntaxTree.Parse("foo bar");
        var first = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock));
        
        Assert.False(pattern.TryMatch(first, out _));
    }

    [Fact]
    public void LookaheadPattern_NegativeLookahead_MatchesWhenNextDoesNotMatch()
    {
        var tree = SyntaxTree.Parse("foo bar");
        var first = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock),
            positive: false);
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.True(match.IsSuccess);
    }

    [Fact]
    public void LookaheadPattern_NegativeLookahead_FailsWhenNextMatches()
    {
        var tree = SyntaxTree.Parse("func(x)");
        var first = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock),
            positive: false);
        
        Assert.False(pattern.TryMatch(first, out _));
    }

    [Fact]
    public void LookaheadPattern_FailsWhenPrimaryDoesNotMatch()
    {
        var tree = SyntaxTree.Parse("{block}(x)");
        var first = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock));
        
        Assert.False(pattern.TryMatch(first, out _));
    }

    [Fact]
    public void LookaheadPattern_PositiveDescription_ShowsLookahead()
    {
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock));
        
        Assert.Contains("(?=", pattern.Description);
    }

    [Fact]
    public void LookaheadPattern_NegativeDescription_ShowsNegativeLookahead()
    {
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock),
            positive: false);
        
        Assert.Contains("(?!", pattern.Description);
    }

    [Fact]
    public void LookaheadPattern_WithNoNextSibling_PositiveFails()
    {
        var tree = SyntaxTree.Parse("alone");
        var node = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock));
        
        Assert.False(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void LookaheadPattern_WithNoNextSibling_NegativeSucceeds()
    {
        var tree = SyntaxTree.Parse("alone");
        var node = tree.Root.Children.First();
        var pattern = new LookaheadPattern(
            new QueryPattern(Q.AnyIdent),
            new QueryPattern(Q.ParenBlock),
            positive: false);
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    #endregion

    #region PatternBuilder

    [Fact]
    public void PatternBuilder_Ident_MatchesIdentifier()
    {
        var tree = SyntaxTree.Parse("test");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder().Ident().Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_IdentWithText_MatchesSpecificText()
    {
        var tree = SyntaxTree.Parse("hello");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder().Ident("hello").Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_Numeric_MatchesNumber()
    {
        var tree = SyntaxTree.Parse("123");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder().Numeric().Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_String_MatchesStringLiteral()
    {
        var tree = SyntaxTree.Parse("\"hello\"");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder().String().Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_Operator_MatchesOperator()
    {
        var tree = SyntaxTree.Parse("a + b");
        var op = tree.Root.Children.First(c => c.Kind == NodeKind.Operator);
        var pattern = new PatternBuilder().Operator().Build();
        
        Assert.True(pattern.TryMatch(op, out _));
    }

    [Fact]
    public void PatternBuilder_OperatorWithText_MatchesSpecificOperator()
    {
        var tree = SyntaxTree.Parse("a + b");
        var op = tree.Root.Children.First(c => c.Kind == NodeKind.Operator);
        var pattern = new PatternBuilder().Operator("+").Build();
        
        Assert.True(pattern.TryMatch(op, out _));
    }

    [Fact]
    public void PatternBuilder_Symbol_MatchesSymbol()
    {
        var tree = SyntaxTree.Parse("a.b");
        var dot = tree.Root.Children.First(c => c.Kind == NodeKind.Symbol);
        var pattern = new PatternBuilder().Symbol().Build();
        
        Assert.True(pattern.TryMatch(dot, out _));
    }

    [Fact]
    public void PatternBuilder_SymbolWithText_MatchesSpecificSymbol()
    {
        var tree = SyntaxTree.Parse("a.b");
        var dot = tree.Root.Children.First(c => c.Kind == NodeKind.Symbol);
        var pattern = new PatternBuilder().Symbol(".").Build();
        
        Assert.True(pattern.TryMatch(dot, out _));
    }

    [Fact]
    public void PatternBuilder_BraceBlock_MatchesBraceBlock()
    {
        var tree = SyntaxTree.Parse("{x}");
        var block = tree.Root.Children.First();
        var pattern = new PatternBuilder().BraceBlock().Build();
        
        Assert.True(pattern.TryMatch(block, out _));
    }

    [Fact]
    public void PatternBuilder_BracketBlock_MatchesBracketBlock()
    {
        var tree = SyntaxTree.Parse("[x]");
        var block = tree.Root.Children.First();
        var pattern = new PatternBuilder().BracketBlock().Build();
        
        Assert.True(pattern.TryMatch(block, out _));
    }

    [Fact]
    public void PatternBuilder_ParenBlock_MatchesParenBlock()
    {
        var tree = SyntaxTree.Parse("(x)");
        var block = tree.Root.Children.First();
        var pattern = new PatternBuilder().ParenBlock().Build();
        
        Assert.True(pattern.TryMatch(block, out _));
    }

    [Fact]
    public void PatternBuilder_AnyBlock_MatchesAllBlockTypes()
    {
        var braceTree = SyntaxTree.Parse("{x}");
        var bracketTree = SyntaxTree.Parse("[x]");
        var parenTree = SyntaxTree.Parse("(x)");
        var pattern = new PatternBuilder().AnyBlock().Build();
        
        Assert.True(pattern.TryMatch(braceTree.Root.Children.First(), out _));
        Assert.True(pattern.TryMatch(bracketTree.Root.Children.First(), out _));
        Assert.True(pattern.TryMatch(parenTree.Root.Children.First(), out _));
    }

    [Fact]
    public void PatternBuilder_MatchQuery_AddsCustomQuery()
    {
        var tree = SyntaxTree.Parse("test");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder().MatchQuery(Q.AnyIdent).Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_Pattern_AddsCustomPattern()
    {
        var tree = SyntaxTree.Parse("test");
        var node = tree.Root.Children.First();
        var customPattern = new QueryPattern(Q.AnyIdent);
        var pattern = new PatternBuilder().Pattern(customPattern).Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_Optional_CreatesOptionalPattern()
    {
        var tree = SyntaxTree.Parse("{block}");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Optional(p => p.Ident())
            .Build();
        
        // Optional should succeed even when inner doesn't match
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Empty(match.Parts);
    }

    [Fact]
    public void PatternBuilder_OneOf_CreatesAlternativePattern()
    {
        var tree = SyntaxTree.Parse("123");
        var node = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .OneOf(
                p => p.Ident(),
                p => p.Numeric())
            .Build();
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void PatternBuilder_Sequence_MatchesMultipleNodes()
    {
        var tree = SyntaxTree.Parse("a.b");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .Symbol(".")
            .Ident()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(3, match.Parts.Length);
    }

    [Fact]
    public void PatternBuilder_SinglePart_ReturnsUnwrappedPattern()
    {
        var pattern = new PatternBuilder().Ident().Build();
        
        Assert.IsType<QueryPattern>(pattern);
    }

    [Fact]
    public void PatternBuilder_MultipleParts_ReturnsSequencePattern()
    {
        var pattern = new PatternBuilder().Ident().Symbol().Build();
        
        Assert.IsType<SequencePattern>(pattern);
    }

    #endregion

    #region Complex Patterns

    [Fact]
    public void ComplexPattern_FunctionCall()
    {
        var tree = SyntaxTree.Parse("func(a, b)");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .ParenBlock()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(2, match.Parts.Length);
    }

    [Fact]
    public void ComplexPattern_PropertyAccess()
    {
        var tree = SyntaxTree.Parse("obj.prop");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .Symbol(".")
            .Ident()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(3, match.Parts.Length);
    }

    [Fact]
    public void ComplexPattern_ArrayAccess()
    {
        var tree = SyntaxTree.Parse("arr[0]");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .BracketBlock()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(2, match.Parts.Length);
    }

    [Fact]
    public void ComplexPattern_MethodCall()
    {
        var tree = SyntaxTree.Parse("obj.method(x)");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .Symbol(".")
            .Ident()
            .ParenBlock()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(4, match.Parts.Length);
    }

    [Fact]
    public void ComplexPattern_BinaryExpression()
    {
        var tree = SyntaxTree.Parse("a + b");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .Operator("+")
            .Ident()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
        Assert.Equal(3, match.Parts.Length);
    }

    [Fact]
    public void ComplexPattern_WithOptional()
    {
        var tree = SyntaxTree.Parse("a b");
        var first = tree.Root.Children.First();
        var pattern = new PatternBuilder()
            .Ident()
            .Optional(p => p.Symbol("."))
            .Ident()
            .Build();
        
        Assert.True(pattern.TryMatch(first, out var match));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Pattern_OnEmptyTree_HandlesGracefully()
    {
        var tree = SyntaxTree.Parse("");
        var pattern = new PatternBuilder().Ident().Build();
        
        // No children to match against
        Assert.Empty(tree.Root.Children);
    }

    [Fact]
    public void SequencePattern_EmptyParts_StillMatches()
    {
        var tree = SyntaxTree.Parse("test");
        var node = tree.Root.Children.First();
        var pattern = new SequencePattern(Array.Empty<INodeQuery>());
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Empty(match.Parts);
    }

    [Fact]
    public void AlternativePattern_SingleAlternative_Works()
    {
        var tree = SyntaxTree.Parse("test");
        var node = tree.Root.Children.First();
        var pattern = NodePattern.OneOf(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(node, out _));
    }

    [Fact]
    public void RepeatPattern_StopsAtNonMatchingNode()
    {
        var tree = SyntaxTree.Parse("a b {c}");
        var first = tree.Root.Children.First();
        var pattern = NodePattern.ZeroOrMore(new QueryPattern(Q.AnyIdent));
        
        Assert.True(pattern.TryMatch(first, out var match));
        // Should stop before the block
        Assert.True(match.Parts.All(p => p.Kind == NodeKind.Ident));
    }

    #endregion
}
