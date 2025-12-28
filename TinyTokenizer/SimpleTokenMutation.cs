using System.Collections.Immutable;

namespace TinyTokenizer;

#region Base Mutation

/// <summary>
/// Abstract base for deferred SimpleToken mutations.
/// Mutations are queued and applied atomically when <see cref="TokenBuffer.Commit"/> is called.
/// </summary>
public abstract record SimpleTokenMutation
{
    /// <summary>
    /// Gets the index in the SimpleToken array where this mutation applies.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the priority for ordering mutations at the same index.
    /// Lower values are applied first. Removals have lower priority than insertions.
    /// </summary>
    public abstract int Priority { get; }

    /// <summary>
    /// Validates the mutation against the current SimpleToken array length.
    /// </summary>
    /// <param name="tokenCount">The current number of SimpleTokens.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the index is invalid.</exception>
    public abstract void Validate(int tokenCount);
}

#endregion

#region Concrete Mutations

/// <summary>
/// Represents an insertion of SimpleTokens at a specific index.
/// Tokens at and after the index are shifted right.
/// </summary>
public sealed record InsertSimpleTokensMutation : SimpleTokenMutation
{
    /// <summary>
    /// Gets the SimpleTokens to insert.
    /// </summary>
    public required ImmutableArray<SimpleToken> Tokens { get; init; }

    /// <inheritdoc/>
    public override int Priority => 10; // Insertions after removals

    /// <inheritdoc/>
    public override void Validate(int tokenCount)
    {
        if (Index < 0 || Index > tokenCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Index),
                Index,
                $"Insert index must be between 0 and {tokenCount} (inclusive).");
        }
    }
}

/// <summary>
/// Represents the removal of a single SimpleToken at a specific index.
/// </summary>
public sealed record RemoveSimpleTokenMutation : SimpleTokenMutation
{
    /// <inheritdoc/>
    public override int Priority => 0; // Removals before insertions

    /// <inheritdoc/>
    public override void Validate(int tokenCount)
    {
        if (Index < 0 || Index >= tokenCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Index),
                Index,
                $"Remove index must be between 0 and {tokenCount - 1} (inclusive).");
        }
    }
}

/// <summary>
/// Represents the removal of a range of SimpleTokens starting at a specific index.
/// </summary>
public sealed record RemoveSimpleTokenRangeMutation : SimpleTokenMutation
{
    /// <summary>
    /// Gets the number of SimpleTokens to remove.
    /// </summary>
    public required int Count { get; init; }

    /// <inheritdoc/>
    public override int Priority => 0; // Removals before insertions

    /// <inheritdoc/>
    public override void Validate(int tokenCount)
    {
        if (Index < 0 || Index >= tokenCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Index),
                Index,
                $"Remove range start index must be between 0 and {tokenCount - 1} (inclusive).");
        }

        if (Count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Count),
                Count,
                "Remove count must be non-negative.");
        }

        if (Index + Count > tokenCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Count),
                Count,
                $"Remove range exceeds token array bounds. Index: {Index}, Count: {Count}, TokenCount: {tokenCount}.");
        }
    }
}

/// <summary>
/// Represents the replacement of SimpleTokens at a specific index with new SimpleTokens.
/// </summary>
public sealed record ReplaceSimpleTokensMutation : SimpleTokenMutation
{
    /// <summary>
    /// Gets the number of SimpleTokens to replace (remove).
    /// </summary>
    public required int RemoveCount { get; init; }

    /// <summary>
    /// Gets the replacement SimpleTokens.
    /// </summary>
    public required ImmutableArray<SimpleToken> Tokens { get; init; }

    /// <inheritdoc/>
    public override int Priority => 5; // Between removals and insertions

    /// <inheritdoc/>
    public override void Validate(int tokenCount)
    {
        if (Index < 0 || Index >= tokenCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Index),
                Index,
                $"Replace index must be between 0 and {tokenCount - 1} (inclusive).");
        }

        if (RemoveCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RemoveCount),
                RemoveCount,
                "Replace remove count must be at least 1.");
        }

        if (Index + RemoveCount > tokenCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RemoveCount),
                RemoveCount,
                $"Replace range exceeds token array bounds. Index: {Index}, RemoveCount: {RemoveCount}, TokenCount: {tokenCount}.");
        }
    }
}

#endregion
