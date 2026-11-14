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
    /// Verifies that replacing a node with prebuilt nodes is canceled by a later enclosing replacement.
    /// </summary>
    [Fact]
    public void ReplaceNodeWithPrebuiltNodesThenEnclosingBlock_LeavesOnlyBlockReplacement()
    {
        // Arrange: prebuilt replacement nodes come from a separate syntax tree.
        var tree = SyntaxTree.Parse("{alpha}");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var replacementTree = SyntaxTree.Parse("first,second,third");
        var replacements = replacementTree.Root.Children.ToArray();
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));

        // Act: replace alpha with green nodes, then replace its original enclosing block.
        tree.CreateEditor()
            .Replace(alpha, replacements)
            .Replace(block, "{final}")
            .Commit();

        // Assert: none of the prebuilt nodes leak into the enclosing replacement.
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
    /// Verifies that a query-based range removal is canceled by a later replacement enclosing its range.
    /// </summary>
    [Fact]
    public void RemoveQueryRangeThenEnclosingBlock_LeavesOnlyBlockReplacement()
    {
        // Arrange: the query resolves to multiple original child slots within the enclosing block.
        var tree = SyntaxTree.Parse("{start,one,two,end}");
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));
        var range = Query.Between(Query.Ident("one"), Query.Ident("two"), inclusive: true);

        // Act: remove the original range, then replace the containing original block.
        tree.CreateEditor()
            .Remove(range)
            .Replace(block, "{final}")
            .Commit();

        // Assert: the query-based range removal contributes no text or structure to the result.
        Assert.Equal("{final}", tree.ToText());
        AssertSingleBlockContent(tree, "final");
    }

    /// <summary>
    /// Verifies that a query-based content edit is canceled by a later replacement enclosing its target.
    /// </summary>
    [Fact]
    public void EditQueryTargetThenEnclosingBlock_LeavesOnlyBlockReplacement()
    {
        // Arrange: the content edit and the enclosing block replacement both target the original tree.
        var tree = SyntaxTree.Parse("{alpha}");
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));

        // Act: queue the query-based edit before replacing its containing block.
        tree.CreateEditor()
            .Edit(Query.Ident("alpha"), _ => "changed")
            .Replace(block, "{final}")
            .Commit();

        // Assert: only the explicitly supplied contents of the enclosing block remain.
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

    /// <summary>
    /// Verifies that insertions exactly at either replacement boundary survive cancellation.
    /// </summary>
    [Fact]
    public void InsertAtRangeBoundariesThenReplaceRange_PreservesBothInsertions()
    {
        // Arrange: the insertion points match the start and end of the original selected range.
        var tree = SyntaxTree.Parse("head one two tail");
        var one = Assert.Single(tree.Select(Query.Ident("one")));
        var two = Assert.Single(tree.Select(Query.Ident("two")));
        var range = Query.Between(Query.Ident("one"), Query.Ident("two"), inclusive: true);

        // Act: boundary insertions are outside the replacement's strict interior.
        tree.CreateEditor()
            .InsertBefore(one, "before ")
            .InsertAfter(two, "after ")
            .Replace(range, "final")
            .Commit();

        // Assert: replacing the range preserves content inserted at both original edges.
        Assert.Equal("head before final after tail", tree.ToText());
        Assert.Equal(new[] { "head", "before", "final", "after", "tail" },
            tree.Root.Children.Select(node => Assert.IsType<SyntaxToken>(node).Text));
    }

    /// <summary>
    /// Verifies that cancellation leaves newline and indentation trivia as if only the range replacement ran.
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void InsertInsideTriviaBearingRangeThenReplaceRange_PreservesOriginalTrivia(string newline)
    {
        // Arrange: the selected range and the inner insertion sit among original newline trivia.
        var source = $"head{newline}  one{newline}  two{newline}tail";
        var expectedTree = SyntaxTree.Parse(source);
        var actualTree = SyntaxTree.Parse(source);
        var expectedRange = Query.Between(Query.Ident("one"), Query.Ident("two"), inclusive: true);
        var actualRange = Query.Between(Query.Ident("one"), Query.Ident("two"), inclusive: true);
        var one = Assert.Single(actualTree.Select(Query.Ident("one")));

        // Act: compare the cancellation result with the same replacement applied to the untouched source.
        expectedTree.CreateEditor().Replace(expectedRange, "final").Commit();
        actualTree.CreateEditor()
            .InsertAfter(one, $"injected;{newline}")
            .Replace(actualRange, "final")
            .Commit();

        // Assert: no inserted content leaks through, and the replacement keeps the original trivia.
        Assert.Equal(expectedTree.ToText(), actualTree.ToText());
    }

    /// <summary>
    /// Verifies that cancellation history restores the original tree and replays the pruned batch.
    /// </summary>
    [Fact]
    public void CanceledDescendantEdit_SupportsUndoAndRedo()
    {
        // Arrange: the child edit is superseded by the queued replacement of its original block.
        var tree = SyntaxTree.Parse("{alpha}");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));

        // Act: commit both commands, then traverse the history in both directions.
        tree.CreateEditor()
            .Replace(alpha, "changed")
            .Replace(block, "{final}")
            .Commit();
        var committedText = tree.ToText();
        var undoSucceeded = tree.Undo();
        var textAfterUndo = tree.ToText();
        var redoSucceeded = tree.Redo();

        // Assert: history retains the atomic result of the batch, not the superseded intermediate edit.
        Assert.True(undoSucceeded);
        Assert.Equal("{alpha}", textAfterUndo);
        Assert.True(redoSucceeded);
        Assert.Equal("{final}", committedText);
        Assert.Equal(committedText, tree.ToText());
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
