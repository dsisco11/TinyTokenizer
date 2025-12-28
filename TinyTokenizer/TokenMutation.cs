using System.Collections.Immutable;

namespace TinyTokenizer;

#region Base Mutation

/// <summary>
/// Abstract base for deferred token mutations.
/// Mutations are queued and applied atomically when <see cref="TokenBuffer.Commit"/> is called.
/// </summary>
public abstract record TokenMutation
{
    /// <summary>
    /// Gets the index in the token array where this mutation applies.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the priority for ordering mutations at the same index.
    /// Lower values are applied first. Removals have lower priority than insertions.
    /// </summary>
    public abstract int Priority { get; }

    /// <summary>
    /// Validates the mutation against the current token array length.
    /// </summary>
    /// <param name="tokenCount">The current number of tokens.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the index is invalid.</exception>
    public abstract void Validate(int tokenCount);
}

#endregion

#region Concrete Mutations

/// <summary>
/// Represents an insertion of a single token at a specific index.
/// Tokens at and after the index are shifted right.
/// </summary>
public sealed record InsertMutation : TokenMutation
{
    /// <summary>
    /// Gets the token to insert.
    /// </summary>
    public required Token Token { get; init; }

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
/// Represents an insertion of multiple tokens at a specific index.
/// Tokens at and after the index are shifted right.
/// </summary>
public sealed record InsertRangeMutation : TokenMutation
{
    /// <summary>
    /// Gets the tokens to insert.
    /// </summary>
    public required ImmutableArray<Token> Tokens { get; init; }

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
/// Represents the removal of a single token at a specific index.
/// </summary>
public sealed record RemoveMutation : TokenMutation
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
/// Represents the removal of a range of tokens starting at a specific index.
/// </summary>
public sealed record RemoveRangeMutation : TokenMutation
{
    /// <summary>
    /// Gets the number of tokens to remove.
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
/// Represents the replacement of a single token at a specific index.
/// </summary>
public sealed record ReplaceMutation : TokenMutation
{
    /// <summary>
    /// Gets the replacement token.
    /// </summary>
    public required Token Token { get; init; }

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
    }
}

/// <summary>
/// Represents the replacement of a single token with multiple tokens.
/// </summary>
public sealed record ReplaceWithRangeMutation : TokenMutation
{
    /// <summary>
    /// Gets the replacement tokens.
    /// </summary>
    public required ImmutableArray<Token> Tokens { get; init; }

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
    }
}

#endregion
