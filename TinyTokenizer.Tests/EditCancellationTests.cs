using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Verifies interval containment and queue-order rules used by edit cancellation.
/// </summary>
public sealed class EditCancellationTests
{
    #region Public API

    /// <summary>
    /// Verifies that a covering interval wins even when a later-starting interval has a shorter end.
    /// </summary>
    [Fact]
    public void Filter_UsesMaximumEndAcrossEligibleReplacementStarts()
    {
        // Arrange: the command is contained by the earlier-starting replacement, not the nearer one.
        var command = new CancellationTestEdit("command", 5, 7, consumesNodes: true);
        var coveringReplacement = new CancellationTestEdit("cover", 0, 20, consumesNodes: true, isReplacement: true);
        var nearerShortReplacement = new CancellationTestEdit("near", 4, 6, consumesNodes: true, isReplacement: true);

        // Act: filter the original queue using only commands later than the candidate.
        var retained = EditCancellation.Filter([command, coveringReplacement, nearerShortReplacement]);

        // Assert: the covering command cancels the candidate while both later replacements remain.
        Assert.Equal(new[] { "cover", "near" }, Ids(retained));
    }

    /// <summary>
    /// Verifies that adjacent or overlapping replacements are not combined to claim containment of an earlier command.
    /// </summary>
    [Theory]
    [InlineData(0, 5, 5, 10, 4, 6)]
    [InlineData(0, 6, 3, 9, 2, 8)]
    public void Filter_DoesNotCombineAdjacentOrOverlappingReplacementIntervals(
        int leftStart,
        int leftEnd,
        int rightStart,
        int rightEnd,
        int commandStart,
        int commandEnd)
    {
        // Arrange: the two later ranges jointly cover the command, but neither interval does so by itself.
        var command = new CancellationTestEdit("command", commandStart, commandEnd, consumesNodes: true);
        var leftReplacement = new CancellationTestEdit("left", leftStart, leftEnd, consumesNodes: true, isReplacement: true);
        var rightReplacement = new CancellationTestEdit("right", rightStart, rightEnd, consumesNodes: true, isReplacement: true);

        // Act: filter without applying commands whose partial-overlap execution result is undefined.
        var retained = EditCancellation.Filter([command, leftReplacement, rightReplacement]);

        // Assert: only a single replacement interval can supersede a command.
        Assert.Equal(new[] { "command", "left", "right" }, Ids(retained));
    }

    /// <summary>
    /// Verifies that an equal-span later replacement cancels the earlier queued replacement.
    /// </summary>
    [Fact]
    public void Filter_UsesQueueOrderForEqualSpans()
    {
        // Arrange: both replacements consume the same original interval.
        var earlier = new CancellationTestEdit("earlier", 2, 8, consumesNodes: true, isReplacement: true);
        var later = new CancellationTestEdit("later", 2, 8, consumesNodes: true, isReplacement: true);

        // Act: inspect the commands retained from their original queue order.
        var retained = EditCancellation.Filter([earlier, later]);

        // Assert: the later replacement remains authoritative for the shared interval.
        Assert.Equal(new[] { "later" }, Ids(retained));
    }

    /// <summary>
    /// Verifies that insertions survive at replacement boundaries and are canceled strictly inside.
    /// </summary>
    [Theory]
    [InlineData(2, true)]
    [InlineData(8, true)]
    [InlineData(5, false)]
    public void Filter_UsesStrictContainmentForInsertionPoints(int position, bool survives)
    {
        // Arrange: the replacement covers [2, 8), with the insertion at a boundary or in its interior.
        var insertion = new CancellationTestEdit("insertion", position, position, consumesNodes: false);
        var replacement = new CancellationTestEdit("replacement", 2, 8, consumesNodes: true, isReplacement: true);

        // Act: process the replacement after the insertion in queue order.
        var retained = EditCancellation.Filter([insertion, replacement]);

        // Assert: boundary points are outside the strict interior of the replaced interval.
        Assert.Equal(survives ? new[] { "insertion", "replacement" } : new[] { "replacement" }, Ids(retained));
    }

    /// <summary>
    /// Verifies that an empty-region replacement is treated as an insertion and cannot supersede a command.
    /// </summary>
    [Fact]
    public void Filter_EmptyRegionReplacementIsAnInsertionAndCannotSupersede()
    {
        // Arrange: the real empty-region command is a zero-width location inside the original block.
        var tree = SyntaxTree.Parse("{}");
        var block = Assert.IsType<SyntaxBlock>(Assert.Single(tree.Select(Query.BraceBlock)));
        var emptyRegion = new QueryRegion(block, 1, 1, firstNode: null, position: block.InnerEndPosition);
        var originalCommand = new CancellationTestEdit("command", 0, 2, consumesNodes: true, isReplacement: true);
        var emptyReplacement = new ReplaceRegionEdit(
            emptyRegion,
            "injected",
            ImmutableArray<GreenTrivia>.Empty,
            ImmutableArray<GreenTrivia>.Empty);

        // Act: filter the queue where the zero-width command comes later.
        var retained = EditCancellation.Filter([originalCommand, emptyReplacement]);

        // Assert: a zero-width query replacement is explicitly classified as an insertion.
        Assert.False(emptyReplacement.IsReplacement);
        Assert.False(emptyReplacement.ConsumesOriginalNodes);
        Assert.Equal(new PendingEdit[] { originalCommand, emptyReplacement }, retained);
    }

    /// <summary>
    /// Verifies that a later enclosing replacement still cancels an inner command through a canceled middle replacement.
    /// </summary>
    [Fact]
    public void Filter_ChainedContainmentRetainsOnlyOutermostReplacement()
    {
        // Arrange: each interval is enclosed by the next later replacement.
        var inner = new CancellationTestEdit("inner", 4, 5, consumesNodes: true);
        var middle = new CancellationTestEdit("middle", 2, 8, consumesNodes: true, isReplacement: true);
        var outer = new CancellationTestEdit("outer", 0, 10, consumesNodes: true, isReplacement: true);

        // Act: process nested commands from the end of the queue toward the beginning.
        var retained = EditCancellation.Filter([inner, middle, outer]);

        // Assert: canceled intermediate replacements do not prevent the outer replacement from covering inner edits.
        Assert.Equal(new[] { "outer" }, Ids(retained));
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Projects retained test commands to their stable identifiers.
    /// </summary>
    /// <param name="edits">The retained pending edits.</param>
    /// <returns>The identifiers in original queue order.</returns>
    private static string[] Ids(IReadOnlyList<PendingEdit> edits)
    {
        return edits.Cast<CancellationTestEdit>().Select(edit => edit.Id).ToArray();
    }

    /// <summary>
    /// Minimal command representation for exercising interval filtering without applying edits.
    /// </summary>
    private sealed class CancellationTestEdit : PendingEdit
    {
        /// <summary>
        /// Creates a test command with an explicit original interval and behavior flags.
        /// </summary>
        public CancellationTestEdit(string id, int position, int endPosition, bool consumesNodes, bool isReplacement = false)
        {
            Id = id;
            Position = position;
            EndPosition = endPosition;
            ConsumesOriginalNodes = consumesNodes;
            IsReplacement = isReplacement;
        }

        /// <summary>
        /// Gets the identifier used to distinguish this command in filter assertions.
        /// </summary>
        public string Id { get; }

        /// <inheritdoc/>
        public override int Position { get; }

        /// <inheritdoc/>
        public override int EndPosition { get; }

        /// <inheritdoc/>
        public override bool ConsumesOriginalNodes { get; }

        /// <inheritdoc/>
        public override bool IsReplacement { get; }

        /// <inheritdoc/>
        public override NodePath AffectedPath => NodePath.Root;

        /// <inheritdoc/>
        public override GreenNode Apply(GreenNode root, GreenTreeBuilder builder, TokenizerOptions options)
        {
            throw new NotSupportedException("Filter tests never apply pending edits.");
        }
    }

    #endregion
}
