using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Verifies that an enclosing replacement supersedes earlier edits inside its original contents.
/// </summary>
public sealed class SyntaxEditorCancellationTests
{
    #region Public API

    /// <summary>
    /// Verifies that replacing a block cancels an earlier replacement of one of its descendants.
    /// </summary>
    [Fact]
    public void ReplaceDescendantThenEnclosingBlock_LeavesOnlyBlockReplacement()
    {
        // Arrange: the descendant and its enclosing block are both original-tree targets.
        var tree = SyntaxTree.Parse("{alpha}");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));

        // Act: first edit the child, then replace the entire block that contains it.
        tree.CreateEditor()
            .Replace(alpha, "changed")
            .Replace(block, "{final}")
            .Commit();

        // Assert: the enclosing replacement determines all text and structure in the block.
        Assert.Equal("{final}", tree.ToText());
        AssertSingleBlockContent(tree, "final");
    }

    /// <summary>
    /// Verifies that replacing a range cancels an earlier one-to-many replacement inside that range.
    /// </summary>
    [Theory]
    [InlineData("start")]
    [InlineData("two")]
    public void ReplaceNodeWithMultipleNodesThenContainingRange_LeavesOnlyRangeReplacement(string targetText)
    {
        // Arrange: replacing either the first or an interior range node expands its original slot.
        const string source = "{start,one,two,end}";
        var tree = SyntaxTree.Parse(source);
        var target = Assert.Single(tree.Select(Query.Ident(targetText)));
        var containingRange = Query.Between(
            Query.Ident("start"),
            Query.Ident("end"),
            inclusive: true);

        // Act: replace one original node with several nodes, then replace the original full range.
        tree.CreateEditor()
            .Replace(target, "first,second,third")
            .Replace(containingRange, "final")
            .Commit();

        // Assert: no expanded child or untouched range node remains around the final replacement.
        Assert.Equal("{final}", tree.ToText());
        AssertSingleBlockContent(tree, "final");
    }

    /// <summary>
    /// Verifies that replacing a block cancels an earlier removal of one of its descendants.
    /// </summary>
    [Fact]
    public void RemoveDescendantThenEnclosingBlock_LeavesOnlyBlockReplacement()
    {
        // Arrange: the removed identifier is inside the original block target.
        var tree = SyntaxTree.Parse("{alpha}");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));

        // Act: remove the child, then replace its enclosing original block.
        tree.CreateEditor()
            .Remove(alpha)
            .Replace(block, "{final}")
            .Commit();

        // Assert: the block replacement restores only its explicitly supplied contents.
        Assert.Equal("{final}", tree.ToText());
        AssertSingleBlockContent(tree, "final");
    }

    /// <summary>
    /// Verifies that replacing a range cancels an earlier insertion strictly inside it.
    /// </summary>
    [Fact]
    public void InsertInsideRangeThenReplaceRange_LeavesOnlyRangeReplacement()
    {
        // Arrange: the inserted identifier is positioned between the original range endpoints.
        var tree = SyntaxTree.Parse("head one two tail");
        var one = Assert.Single(tree.Select(Query.Ident("one")));
        var range = Query.Between(Query.Ident("one"), Query.Ident("two"), inclusive: true);

        // Act: insert inside the original range, then replace both original endpoint nodes.
        tree.CreateEditor()
            .InsertAfter(one, "injected ")
            .Replace(range, "final")
            .Commit();

        // Assert: neither the inserted identifier nor either original range endpoint survives.
        Assert.Equal("head final tail", tree.ToText());
        Assert.Equal(new[] { "head", "final", "tail" },
            tree.Root.Children.Select(node => Assert.IsType<SyntaxToken>(node).Text));
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Verifies that the tree contains one root block with exactly the requested inner token.
    /// </summary>
    /// <param name="tree">The edited syntax tree.</param>
    /// <param name="expectedContent">The expected text of the block's only inner child.</param>
    private static void AssertSingleBlockContent(SyntaxTree tree, string expectedContent)
    {
        var root = Assert.IsType<SyntaxList>(tree.Root);
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(root.Children));
        var token = Assert.IsType<SyntaxToken>(Assert.Single(block.InnerChildren));
        Assert.Equal(expectedContent, token.Text);
    }

    #endregion
}
