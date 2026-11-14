namespace TinyTokenizer.Ast;

/// <summary>
/// Removes earlier commands fully superseded by later replacements using original character intervals.
/// </summary>
internal static class EditCancellation
{
    #region Public API

    /// <summary>
    /// Returns retained commands in queue order without modifying the pending queue.
    /// Uses O(n log n) time and O(n) space for n queued commands.
    /// </summary>
    public static IReadOnlyList<PendingEdit> Filter(IReadOnlyList<PendingEdit> edits)
    {
        if (edits.Count < 2)
            return edits;

        // Compress only replacement starts. A prefix query then finds the furthest end
        // belonging to one later replacement whose start satisfies the containment rule.
        var starts = edits.Where(edit => edit.IsReplacement)
            .Select(edit => edit.Position)
            .Distinct()
            .Order()
            .ToArray();
        if (starts.Length == 0)
            return edits;

        var maximumEnds = new int[starts.Length + 1];
        Array.Fill(maximumEnds, -1);
        var retained = new List<PendingEdit>(edits.Count);

        // List order is queue order. Visiting backwards indexes only later replacements.
        // A canceled replacement need not be indexed: its enclosing replacement also
        // contains any earlier command that it could have canceled.
        for (int i = edits.Count - 1; i >= 0; i--)
        {
            var edit = edits[i];
            int startIndex = Array.BinarySearch(starts, edit.Position);
            int prefixCount = startIndex < 0
                ? ~startIndex
                : startIndex + (edit.ConsumesOriginalNodes ? 1 : 0);
            int maximumEnd = GetMaximumEnd(maximumEnds, prefixCount);

            // Point insertions survive both boundaries. Consuming commands require
            // full containment, including equality, rather than merely an intersection.
            bool canceled = edit.ConsumesOriginalNodes
                ? maximumEnd >= edit.EndPosition
                : maximumEnd > edit.Position;
            if (canceled)
                continue;

            retained.Add(edit);
            if (edit.IsReplacement)
                AddEnd(maximumEnds, startIndex + 1, edit.EndPosition);
        }

        retained.Reverse();
        return retained;
    }

    #endregion

    #region Private

    /// <summary>
    /// Gets the maximum replacement end in the first count compressed start positions.
    /// </summary>
    private static int GetMaximumEnd(int[] maximumEnds, int count)
    {
        int maximumEnd = -1;
        // Each Fenwick entry covers a disjoint suffix of the remaining prefix.
        for (int index = count; index > 0; index -= index & -index)
            maximumEnd = Math.Max(maximumEnd, maximumEnds[index]);
        return maximumEnd;
    }

    /// <summary>
    /// Adds an end position at a one-based compressed start index to the Fenwick maxima.
    /// </summary>
    private static void AddEnd(int[] maximumEnds, int index, int endPosition)
    {
        // Maxima only grow as retained later replacements enter the index.
        for (; index < maximumEnds.Length; index += index & -index)
            maximumEnds[index] = Math.Max(maximumEnds[index], endPosition);
    }

    #endregion
}
