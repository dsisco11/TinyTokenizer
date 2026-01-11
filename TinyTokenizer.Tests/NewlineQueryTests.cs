using System.Linq;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

[Trait("Category", "Query")]
public sealed class NewlineQueryTests
{
    [Fact]
    public void Newline_MatchesNodeWithLeadingTriviaNewline_TopLevelFirstSibling()
    {
        var tree = SyntaxTree.Parse("\nfoo");
        var foo = tree.Root.Children.OfType<SyntaxToken>().First(n => n.Kind == NodeKind.Ident);

        Assert.True(Query.Newline.Matches(foo));
        Assert.False(Query.NotNewline.Matches(foo));
    }

    [Fact]
    public void Newline_MatchesNodeAfterNewlineViaPreviousSiblingTrailingTrivia_TopLevel()
    {
        var tree = SyntaxTree.Parse("x\ny");
        var idents = tree.Root.Children.OfType<SyntaxToken>().Where(n => n.Kind == NodeKind.Ident).ToList();

        Assert.Equal(2, idents.Count);
        Assert.False(Query.Newline.Matches(idents[0]));
        Assert.True(Query.Newline.Matches(idents[1]));
    }

    [Fact]
    public void Newline_MatchesFirstInnerNodeAfterOpenerViaPreviousSiblingTrailingTrivia_InBlock()
    {
        var tree = SyntaxTree.Parse("{\na}");
        var block = tree.Root.Children.OfType<SyntaxBlock>().Single();
        var a = block.InnerChildren.OfType<SyntaxToken>().Single(n => n.Kind == NodeKind.Ident);

        Assert.True(Query.Newline.Matches(a));
        Assert.False(Query.NotNewline.Matches(a));
    }

    [Fact]
    public void Newline_MatchesInnerNodeAfterNewlineBetweenSiblings_InBlock()
    {
        var tree = SyntaxTree.Parse("{a\nb}");
        var block = tree.Root.Children.OfType<SyntaxBlock>().Single();
        var idents = block.InnerChildren.OfType<SyntaxToken>().Where(n => n.Kind == NodeKind.Ident).ToList();

        Assert.Equal(2, idents.Count);
        Assert.False(Query.Newline.Matches(idents[0]));
        Assert.True(Query.Newline.Matches(idents[1]));
    }

    [Fact]
    public void NotNewline_IsExactNegationOfNewline_ForIdentifiersInSameTree()
    {
        var tree = SyntaxTree.Parse("a b\nc\n\nd");

        var allIdents = tree.Select(Query.AnyIdent).ToList();
        var newlineIdents = tree.Select(Query.AnyIdent & Query.Newline).ToList();
        var notNewlineIdents = tree.Select(Query.AnyIdent & Query.NotNewline).ToList();

        Assert.All(allIdents, n => Assert.True(newlineIdents.Contains(n) ^ notNewlineIdents.Contains(n)));
        Assert.Empty(newlineIdents.Intersect(notNewlineIdents));
        Assert.Equal(allIdents.Count, newlineIdents.Count + notNewlineIdents.Count);
    }

    [Fact]
    public void Newline_DoesNotThrow_OnEmptyBlockOrEmptyTree()
    {
        var emptyTree = SyntaxTree.Parse(string.Empty);
        Assert.Empty(emptyTree.Select(Query.Newline));

        var emptyBlockTree = SyntaxTree.Parse("{}");
        Assert.Empty(emptyBlockTree.Select(Query.Newline));
    }

    [Fact]
    public void Newline_MatchesCloserAfterNewlineInEmptyBlock()
    {
        var tree = SyntaxTree.Parse("{\n}");
        var block = tree.Root.Children.OfType<SyntaxBlock>().Single();

        Assert.True(Query.Newline.Matches(block.CloserNode));
        Assert.False(Query.Newline.Matches(block.OpenerNode));
    }
}
