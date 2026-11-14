using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Regression tests for edits targeting original siblings at the same boundary.
/// </summary>
public sealed class SyntaxEditorAdjacentMutationTests
{
    #region Public API

    #region Replacement And Insertion Boundaries

    /// <summary>
    /// Verifies that replacing the node after an insertion boundary still targets the original node.
    /// </summary>
    [Theory]
    [InlineData(false, false, "\n", "alpha\nint injected;\n\n\n")]
    [InlineData(false, true, "\n", "alpha\nint injected;\n\n\n")]
    [InlineData(true, false, "\n", "alpha\nint injected;\n\n\n")]
    [InlineData(true, true, "\n", "alpha\nint injected;\n\n\n")]
    [InlineData(false, false, "\r\n", "alpha\r\nint injected;\r\n\r\n\r\n")]
    [InlineData(false, true, "\r\n", "alpha\r\nint injected;\r\n\r\n\r\n")]
    [InlineData(true, false, "\r\n", "alpha\r\nint injected;\r\n\r\n\r\n")]
    [InlineData(true, true, "\r\n", "alpha\r\nint injected;\r\n\r\n\r\n")]
    public void ReplaceAndInsertAtAdjacentBoundary_TargetOriginalNode(
        bool useQueries,
        bool insertFirst,
        string lineEnding,
        string expected)
    {
        // Arrange: alpha ends exactly where beta starts, so both queued edits share a position.
        var tree = SyntaxTree.Parse($"alpha{lineEnding}beta{lineEnding}");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        Assert.Equal(beta.Position, alpha.EndPosition);
        var editor = tree.CreateEditor();

        // Act: reverse the queue order while keeping both targets bound to the original tree.
        if (insertFirst)
        {
            QueueInsertion(editor, alpha, useQueries, $"int injected;{lineEnding}");
            QueueReplacement(editor, beta, useQueries, lineEnding);
        }
        else
        {
            QueueReplacement(editor, beta, useQueries, lineEnding);
            QueueInsertion(editor, alpha, useQueries, $"int injected;{lineEnding}");
        }

        editor.Commit();

        // Assert: replacing beta must preserve the inserted statement and all original boundary trivia.
        Assert.Equal(expected, tree.ToText());
    }

    /// <summary>
    /// Verifies that same-line spaces attached to the preceding token stay before inserted text.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ReplaceAndInsertAtWhitespaceBoundary_PreserveTrivia(bool useQueries, bool insertFirst)
    {
        // Arrange: same-line spaces belong to alpha, and beta begins at alpha's end position.
        var tree = SyntaxTree.Parse("alpha   beta\n");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        Assert.Equal(beta.Position, alpha.EndPosition);
        var editor = tree.CreateEditor();

        // Act: insert at alpha's original end and replace beta in either call order.
        if (insertFirst)
        {
            QueueInsertion(editor, alpha, useQueries, "int injected;\n");
            QueueReplacement(editor, beta, useQueries, "\n");
        }
        else
        {
            QueueReplacement(editor, beta, useQueries, "\n");
            QueueInsertion(editor, alpha, useQueries, "int injected;\n");
        }

        editor.Commit();

        // Assert: alpha's spaces, inserted text, replacement newline, and beta's newline remain ordered.
        Assert.Equal("alpha   int injected;\n\n\n", tree.ToText());
    }

    /// <summary>
    /// Verifies that two same-boundary insertions retain their queue order when a sibling is also replaced.
    /// </summary>
    [Fact]
    public void MultipleInsertionsAndReplacementAtSameBoundary_PreserveQueueOrder()
    {
        // Arrange: both inserted snippets and the replacement target share the alpha/beta boundary.
        var tree = SyntaxTree.Parse("alpha\nbeta\n");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));

        // Act: queue two insertions in order, followed by replacement of the original beta.
        tree.CreateEditor()
            .InsertAfter(alpha, "first;\n")
            .InsertAfter(alpha, "second;\n")
            .Replace(beta, "X")
            .Commit();

        // Assert: queue order of same-boundary insertions is retained around the replacement.
        Assert.Equal("alpha\nfirst;\nsecond;\nX\n", tree.ToText());
    }

    #endregion

    #region Removal And Multi-Node Boundaries

    /// <summary>
    /// Verifies that removing the following node does not remove a same-boundary insertion.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RemoveAndInsertAfterAdjacentNode_TargetOriginalNode(bool useQueries, bool insertFirst)
    {
        // Arrange: removing beta and inserting after alpha share the original slot before beta.
        var tree = SyntaxTree.Parse("alpha\nbeta\n");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        Assert.Equal(beta.Position, alpha.EndPosition);
        var editor = tree.CreateEditor();

        // Act: queue the same edits in either order; removal consumes beta's trailing newline too.
        if (insertFirst)
        {
            QueueInsertion(editor, alpha, useQueries, "int injected;\n");
            QueueRemoval(editor, beta, useQueries);
        }
        else
        {
            QueueRemoval(editor, beta, useQueries);
            QueueInsertion(editor, alpha, useQueries, "int injected;\n");
        }

        editor.Commit();

        // Assert: beta and its trailing newline are removed while the inserted text survives.
        Assert.Equal("alpha\nint injected;\n", tree.ToText());
    }

    /// <summary>
    /// Verifies that an insertion before the node after a removed sibling remains correctly positioned.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RemoveAndInsertBeforeNextNode_KeepSeparateBoundaries(bool useQueries, bool insertFirst)
    {
        // Arrange: this insertion is at beta's start, while alpha is removed at an earlier position.
        var tree = SyntaxTree.Parse("alpha\nbeta\n");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        Assert.True(alpha.Position < beta.Position);
        var editor = tree.CreateEditor();

        // Act: apply edits in either call order at their distinct original boundaries.
        if (insertFirst)
        {
            QueueInsertionBefore(editor, beta, useQueries, "int injected;\n");
            QueueRemoval(editor, alpha, useQueries);
        }
        else
        {
            QueueRemoval(editor, alpha, useQueries);
            QueueInsertionBefore(editor, beta, useQueries, "int injected;\n");
        }

        editor.Commit();

        // Assert: deleting alpha also removes its newline; beta retains its own trailing newline.
        Assert.Equal("int injected;\nbeta\n", tree.ToText());
    }

    /// <summary>
    /// Verifies that insertion at a query region's leading boundary preserves the original multi-slot target.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceQueryRegionAndInsertAtItsLeadingBoundary_KeepOriginalRegion(bool insertFirst)
    {
        // Arrange: beta and gamma form a two-node region immediately after alpha.
        var tree = SyntaxTree.Parse("alpha\nbeta\ngamma\n");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        var editor = tree.CreateEditor();
        var region = Query.Between(Query.Ident("beta"), Query.Ident("gamma"), inclusive: true);

        // Act: replace the original two-slot region and insert at its leading boundary.
        if (insertFirst)
        {
            editor.InsertAfter(alpha, "int injected;\n");
            editor.Replace(region, "X");
        }
        else
        {
            editor.Replace(region, "X");
            editor.InsertAfter(alpha, "int injected;\n");
        }

        editor.Commit();

        // Assert: the inserted statement and the replacement for both original nodes are retained.
        Assert.Equal("alpha\nint injected;\nX\n", tree.ToText());
    }

    /// <summary>
    /// Verifies that a nested parent path remains valid when an insertion and replacement meet at a boundary.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceAndInsertAtNestedBoundary_TargetOriginalNode(bool insertFirst)
    {
        // Arrange: both target nodes are children of the same nested block.
        var tree = SyntaxTree.Parse("{alpha\nbeta\n}");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        Assert.Equal(beta.Position, alpha.EndPosition);
        var editor = tree.CreateEditor();

        // Act: edit two original children at their shared position in either call order.
        if (insertFirst)
        {
            editor.InsertAfter(alpha, "int injected;\n");
            editor.Replace(beta, "X");
        }
        else
        {
            editor.Replace(beta, "X");
            editor.InsertAfter(alpha, "int injected;\n");
        }

        editor.Commit();

        // Assert: the nested replacement and insertion both survive.
        Assert.Equal("{alpha\nint injected;\nX\n}", tree.ToText());
    }

    #endregion

    #region Undo, Redo, And Prebuilt Node Inputs

    /// <summary>
    /// Verifies that a successful adjacent batch is a single undo step and can be redone.
    /// </summary>
    [Fact]
    public void ReplaceAndInsertAtAdjacentBoundary_CommitIsOneUndoStep()
    {
        // Arrange: capture the original tree text before the combined mutation.
        const string original = "alpha\nbeta\n";
        const string expected = "alpha\nint injected;\nX\n\n";
        var tree = SyntaxTree.Parse(original);
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        var editor = tree.CreateEditor();

        // Act: commit both edits together, then undo and redo the commit.
        editor
            .InsertAfter(alpha, "int injected;\n")
            .Replace(beta, "X\n")
            .Commit();

        var committedText = tree.ToText();
        var pendingEditCount = editor.PendingEditCount;
        var undone = tree.Undo();
        var undoText = tree.ToText();
        var undoAvailabilityAfterBatch = tree.CanUndo;
        var redone = tree.Redo();

        // Assert: history stores the whole batch as one reversible tree mutation.
        Assert.Equal(expected, committedText);
        Assert.Equal(0, pendingEditCount);
        Assert.True(undone);
        Assert.Equal(original, undoText);
        Assert.False(undoAvailabilityAfterBatch);
        Assert.True(redone);
        Assert.Equal(expected, tree.ToText());
    }

    /// <summary>
    /// Verifies that the node-based prebuilt replacement and insertion overloads share original targets.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceNodesAndInsertNodesAtAdjacentBoundary_TargetOriginalNode(bool insertFirst)
    {
        // Arrange: materialize nodes independently, then target alpha and beta in the source tree.
        var tree = SyntaxTree.Parse("alpha\nbeta\n");
        var alpha = Assert.Single(tree.Select(Query.Ident("alpha")));
        var beta = Assert.Single(tree.Select(Query.Ident("beta")));
        var insertedNodes = SyntaxTree.Parse("int injected;\n").Root.Children.ToArray();
        var replacementNodes = SyntaxTree.Parse("X\n").Root.Children.ToArray();
        var editor = tree.CreateEditor();

        // Act: prebuilt node edits at the shared original boundary execute in either queue order.
        if (insertFirst)
        {
            editor.InsertAfter(alpha, insertedNodes);
            editor.Replace(beta, replacementNodes);
        }
        else
        {
            editor.Replace(beta, replacementNodes);
            editor.InsertAfter(alpha, insertedNodes);
        }

        editor.Commit();

        // Assert: both the inserted nodes and replacement target survive; original trailing trivia remains.
        Assert.Equal("alpha\nint injected;\nX\n\n", tree.ToText());
    }

    #endregion

    #endregion

    #region Private Helpers

    /// <summary>
    /// Queues an insertion using either the SyntaxNode or query overload.
    /// </summary>
    private static void QueueInsertion(SyntaxEditor editor, SyntaxNode alpha, bool useQueries, string text)
    {
        if (useQueries)
        {
            editor.InsertAfter(Query.Ident("alpha"), text);
        }
        else
        {
            editor.InsertAfter(alpha, text);
        }
    }

    /// <summary>
    /// Queues a replacement using either the SyntaxNode or query overload.
    /// </summary>
    private static void QueueReplacement(SyntaxEditor editor, SyntaxNode beta, bool useQueries, string text)
    {
        if (useQueries)
        {
            editor.Replace(Query.Ident("beta"), text);
        }
        else
        {
            editor.Replace(beta, text);
        }
    }

    /// <summary>
    /// Queues removal using either the SyntaxNode or query overload.
    /// </summary>
    private static void QueueRemoval(SyntaxEditor editor, SyntaxNode node, bool useQueries)
    {
        if (useQueries)
        {
            editor.Remove(Query.Kind(node.Kind).WithText(((SyntaxToken)node).Text));
        }
        else
        {
            editor.Remove(node);
        }
    }

    /// <summary>
    /// Queues an insertion before a node using either the SyntaxNode or query overload.
    /// </summary>
    private static void QueueInsertionBefore(SyntaxEditor editor, SyntaxNode node, bool useQueries, string text)
    {
        if (useQueries)
        {
            editor.InsertBefore(Query.Kind(node.Kind).WithText(((SyntaxToken)node).Text), text);
        }
        else
        {
            editor.InsertBefore(node, text);
        }
    }

    #endregion
}
