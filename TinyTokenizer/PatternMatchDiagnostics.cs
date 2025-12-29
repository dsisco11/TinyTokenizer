using System.Collections.Immutable;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// The outcome of a pattern match attempt.
/// </summary>
public enum MatchResult
{
    /// <summary>
    /// Pattern matched fully and was applied.
    /// </summary>
    Success,

    /// <summary>
    /// Pattern partially matched but failed before completion.
    /// </summary>
    PartialMatch,

    /// <summary>
    /// First selector didn't match — pattern not applicable here.
    /// </summary>
    NoMatch,

    /// <summary>
    /// Pattern matched but was superseded by a higher-priority pattern.
    /// </summary>
    Superseded
}

/// <summary>
/// Result of matching a single selector against a token.
/// </summary>
public sealed record SelectorMatchResult
{
    /// <summary>
    /// Gets the selector that was tested.
    /// </summary>
    public required TokenSelector Selector { get; init; }

    /// <summary>
    /// Gets the actual token that was tested against.
    /// Null if there was no token available.
    /// </summary>
    public required Token? ActualToken { get; init; }

    /// <summary>
    /// Gets whether the selector matched the token.
    /// </summary>
    public required bool Matched { get; init; }

    /// <summary>
    /// Gets the reason for failure if the match failed.
    /// </summary>
    public required string? FailureReason { get; init; }
}

/// <summary>
/// Represents the result of attempting to match a pattern at a specific position.
/// </summary>
public sealed record PatternMatchAttempt
{
    /// <summary>
    /// Gets the name of the pattern that was attempted.
    /// </summary>
    public required string PatternName { get; init; }

    /// <summary>
    /// Gets the token index where matching started.
    /// </summary>
    public required int TokenIndex { get; init; }

    /// <summary>
    /// Gets the result of the match attempt.
    /// </summary>
    public required MatchResult Result { get; init; }

    /// <summary>
    /// Gets the detailed results for each selector in the pattern.
    /// </summary>
    public required ImmutableArray<SelectorMatchResult> SelectorResults { get; init; }

    /// <summary>
    /// Gets how many selectors matched before failure (for partial matches).
    /// </summary>
    public int MatchedSelectorCount => SelectorResults.Count(r => r.Matched);
}

/// <summary>
/// Complete diagnostic report from pattern matching.
/// </summary>
public sealed record PatternMatchReport
{
    /// <summary>
    /// Gets the input tokens that were processed.
    /// </summary>
    public required ImmutableArray<Token> InputTokens { get; init; }

    /// <summary>
    /// Gets the output tokens after pattern matching.
    /// </summary>
    public required ImmutableArray<Token> OutputTokens { get; init; }

    /// <summary>
    /// Gets all pattern match attempts that were made.
    /// </summary>
    public required ImmutableArray<PatternMatchAttempt> Attempts { get; init; }

    /// <summary>
    /// Gets all attempts that partially matched but failed.
    /// Useful for debugging "almost worked" patterns.
    /// </summary>
    public IEnumerable<PatternMatchAttempt> NearMisses =>
        Attempts.Where(a => a.Result == MatchResult.PartialMatch);

    /// <summary>
    /// Gets patterns that matched successfully.
    /// </summary>
    public IEnumerable<PatternMatchAttempt> Successes =>
        Attempts.Where(a => a.Result == MatchResult.Success);

    /// <summary>
    /// Generates a human-readable diagnostic summary.
    /// </summary>
    /// <returns>A formatted string describing the match results.</returns>
    public string ToSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Input: {InputTokens.Length} tokens → Output: {OutputTokens.Length} tokens");
        sb.AppendLine($"Matches: {Successes.Count()}, Near-misses: {NearMisses.Count()}");

        foreach (var success in Successes)
        {
            sb.AppendLine();
            sb.AppendLine($"✓ Matched: '{success.PatternName}' at token {success.TokenIndex}");
        }

        foreach (var nearMiss in NearMisses)
        {
            sb.AppendLine();
            sb.AppendLine($"✗ Near-miss: '{nearMiss.PatternName}' at token {nearMiss.TokenIndex}");
            sb.AppendLine($"  Matched {nearMiss.MatchedSelectorCount}/{nearMiss.SelectorResults.Length} selectors");

            var failed = nearMiss.SelectorResults.FirstOrDefault(r => !r.Matched);
            if (failed is not null)
            {
                sb.AppendLine($"  Failed at: {failed.Selector.Description}");
                if (failed.ActualToken is not null)
                {
                    sb.AppendLine($"  Actual token: {failed.ActualToken.Type} '{failed.ActualToken.ContentSpan}'");
                }
                sb.AppendLine($"  Reason: {failed.FailureReason}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a detailed diagnostic report including all attempts.
    /// </summary>
    /// <returns>A formatted string with complete match attempt details.</returns>
    public string ToDetailedReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Pattern Match Report ===");
        sb.AppendLine();
        sb.AppendLine($"Input tokens: {InputTokens.Length}");
        sb.AppendLine($"Output tokens: {OutputTokens.Length}");
        sb.AppendLine($"Total attempts: {Attempts.Length}");
        sb.AppendLine($"Successful matches: {Successes.Count()}");
        sb.AppendLine($"Partial matches (near-misses): {NearMisses.Count()}");
        sb.AppendLine();

        sb.AppendLine("--- Input Tokens ---");
        for (int i = 0; i < InputTokens.Length; i++)
        {
            var token = InputTokens[i];
            sb.AppendLine($"  [{i}] {token.Type}: '{TruncateContent(token.ContentSpan, 30)}'");
        }
        sb.AppendLine();

        sb.AppendLine("--- Match Attempts ---");
        foreach (var attempt in Attempts)
        {
            var icon = attempt.Result switch
            {
                MatchResult.Success => "✓",
                MatchResult.PartialMatch => "~",
                MatchResult.NoMatch => "✗",
                MatchResult.Superseded => "○",
                _ => "?"
            };

            sb.AppendLine($"{icon} [{attempt.TokenIndex}] {attempt.PatternName}: {attempt.Result}");
            
            if (attempt.Result == MatchResult.PartialMatch || attempt.Result == MatchResult.Success)
            {
                foreach (var selector in attempt.SelectorResults)
                {
                    var selectorIcon = selector.Matched ? "  ✓" : "  ✗";
                    var tokenDesc = selector.ActualToken is not null 
                        ? $"{selector.ActualToken.Type} '{TruncateContent(selector.ActualToken.ContentSpan, 20)}'"
                        : "(no token)";
                    sb.AppendLine($"{selectorIcon} {selector.Selector.Description} vs {tokenDesc}");
                    if (!selector.Matched && selector.FailureReason is not null)
                    {
                        sb.AppendLine($"      Reason: {selector.FailureReason}");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static string TruncateContent(ReadOnlySpan<char> content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content.ToString().Replace("\n", "\\n").Replace("\r", "\\r");

        return content[..maxLength].ToString().Replace("\n", "\\n").Replace("\r", "\\r") + "...";
    }
}
