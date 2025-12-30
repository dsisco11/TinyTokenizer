using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the unified Query combinator API.
/// </summary>
public class QueryCombinatorTests
{
    private static readonly Schema DefaultSchema = Schema.Create()
        .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .WithOperators(CommonOperators.CFamily)
        .Build();

    private static SyntaxTree Parse(string source) => SyntaxTree.Parse(source, DefaultSchema);

    #region Sequence Query Tests

    [Fact]
    public void Sequence_MatchesTwoIdentifiers()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident, Query.Ident);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Sequence_FailsIfSecondNotMatch()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident, Query.Ident);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    [Fact]
    public void Sequence_MatchesFunctionCall()
    {
        var tree = Parse("foo(a, b)");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident, Query.ParenBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Sequence_MatchesFourParts()
    {
        // Function definition: type name(params) { body }
        var tree = Parse("void main() {}");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident, Query.Ident, Query.ParenBlock, Query.BraceBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void Sequence_GreenMatching()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        // Get green children via the container interface
        var greenRoot = (GreenContainer)root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.Sequence(Query.Ident, Query.Ident);
        Assert.True(query.TryMatchGreen(greenChildren, 0, out var consumed));
        Assert.Equal(2, consumed);
    }

    #endregion

    #region Optional Query Tests

    [Fact]
    public void Optional_MatchesWhenPresent()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.Optional();
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void Optional_SucceedsWithZeroWhenAbsent()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Ident.Optional();
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Optional_InSequence()
    {
        // Match "foo = bar" or "foo bar"
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident, Query.Operator.Optional(), Query.Ident);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed); // Only 2 because = is optional and not present
    }

    #endregion

    #region Repeat Query Tests

    [Fact]
    public void ZeroOrMore_MatchesZero()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Ident.ZeroOrMore();
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void ZeroOrMore_MatchesMultiple()
    {
        var tree = Parse("foo bar baz 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.ZeroOrMore();
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void OneOrMore_FailsOnZero()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Ident.OneOrMore();
        Assert.False(query.TryMatch(firstNode, out _));
    }

    [Fact]
    public void OneOrMore_MatchesMultiple()
    {
        var tree = Parse("foo bar baz 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.OneOrMore();
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void Exactly_MatchesExactCount()
    {
        var tree = Parse("foo bar baz qux");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.Exactly(2);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Exactly_FailsIfNotEnough()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.Exactly(2);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    [Fact]
    public void Repeat_WithMinMax()
    {
        var tree = Parse("foo bar baz qux quux");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.Repeat(2, 3);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed); // Stops at max
    }

    #endregion

    #region Until Query Tests

    [Fact]
    public void Until_StopsAtTerminator()
    {
        var tree = Parse("foo bar baz;");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Any.Until(Query.Symbol);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed); // foo, bar, baz (not semicolon)
    }

    [Fact]
    public void Until_MatchesZeroIfTerminatorFirst()
    {
        var tree = Parse("; foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Ident.Until(Query.Symbol);
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Until_MatchesAllIfNoTerminator()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.Until(Query.Symbol);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed);
    }

    #endregion

    #region Lookahead Query Tests

    [Fact]
    public void FollowedBy_MatchesWithLookahead()
    {
        var tree = Parse("foo(bar)");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // Match identifier only if followed by paren block (function call)
        var query = Query.Ident.FollowedBy(Query.ParenBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(1, consumed); // Only consumes the identifier
    }

    [Fact]
    public void FollowedBy_FailsWithoutLookahead()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.FollowedBy(Query.ParenBlock);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    [Fact]
    public void NotFollowedBy_MatchesWithoutLookahead()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // Match identifier only if NOT followed by paren block (not a function call)
        var query = Query.Ident.NotFollowedBy(Query.ParenBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void NotFollowedBy_FailsWithLookahead()
    {
        var tree = Parse("foo(bar)");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.NotFollowedBy(Query.ParenBlock);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    #endregion

    #region Union (Alternation) Tests

    [Fact]
    public void Union_MatchesEither()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        
        var query = Query.Ident | Query.Numeric;
        
        Assert.True(query.TryMatch(root.Children.First(), out var c1));
        Assert.Equal(1, c1);
        
        Assert.True(query.TryMatch(root.Children.Skip(1).First(), out var c2));
        Assert.Equal(1, c2);
    }

    [Fact]
    public void Union_InSequence()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident | Query.Numeric, Query.Ident | Query.Numeric);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    #endregion

    #region Complex Pattern Tests

    [Fact]
    public void FunctionDefinition_Pattern()
    {
        var tree = Parse("void main() { return; }");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // type name(params) { body }
        var query = Query.Sequence(Query.Ident, Query.Ident, Query.ParenBlock, Query.BraceBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void MethodChain_Pattern()
    {
        var tree = Parse("foo.bar.baz()");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // identifier followed by .identifier repeated, then optional call
        var dotIdent = Query.Sequence(Query.Symbol.WithText("."), Query.Ident);
        var query = Query.Sequence(Query.Ident, dotIdent.OneOrMore(), Query.ParenBlock.Optional());
        
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        // foo, ., bar, ., baz, ()
        Assert.True(consumed >= 5);
    }

    [Fact]
    public void ArrayAccess_Pattern()
    {
        var tree = Parse("arr[0]");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.Ident, Query.BracketBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    #endregion

    #region Then Extension Method Tests

    [Fact]
    public void Then_CreatesTwoPartSequence()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident.Then(Query.Ident);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Then_ChainsMultipleParts()
    {
        var tree = Parse("void main() {}");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Ident
            .Then(Query.Ident)
            .Then(Query.ParenBlock)
            .Then(Query.BraceBlock);
            
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(4, consumed);
    }

    #endregion
}
