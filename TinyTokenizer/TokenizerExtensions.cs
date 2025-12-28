using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Extension methods for working with token collections.
/// </summary>
public static class TokenizerExtensions
{
    #region Utility Extensions

    /// <summary>
    /// Checks if the token collection contains any error tokens.
    /// </summary>
    /// <param name="tokens">The tokens to check.</param>
    /// <returns>True if any error tokens are present; otherwise, false.</returns>
    public static bool HasErrors(this ImmutableArray<Token> tokens)
    {
        foreach (var token in tokens)
        {
            if (token is ErrorToken)
            {
                return true;
            }

            if (token is SimpleBlock block && block.Children.HasErrors())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all error tokens from the token collection, including nested errors.
    /// </summary>
    /// <param name="tokens">The tokens to search.</param>
    /// <returns>An enumerable of all error tokens.</returns>
    public static IEnumerable<ErrorToken> GetErrors(this ImmutableArray<Token> tokens)
    {
        foreach (var token in tokens)
        {
            if (token is ErrorToken error)
            {
                yield return error;
            }

            if (token is SimpleBlock block)
            {
                foreach (var nestedError in block.Children.GetErrors())
                {
                    yield return nestedError;
                }
            }
        }
    }

    /// <summary>
    /// Gets all tokens of a specific type from the collection, including nested tokens.
    /// </summary>
    /// <typeparam name="T">The token type to filter for.</typeparam>
    /// <param name="tokens">The tokens to search.</param>
    /// <returns>An enumerable of all tokens of the specified type.</returns>
    public static IEnumerable<T> OfTokenType<T>(this ImmutableArray<Token> tokens) where T : Token
    {
        foreach (var token in tokens)
        {
            if (token is T typed)
            {
                yield return typed;
            }

            if (token is SimpleBlock block)
            {
                foreach (var nested in block.Children.OfTokenType<T>())
                {
                    yield return nested;
                }
            }
        }
    }

    #endregion

    #region Pattern Matching Extensions

    /// <summary>
    /// Applies pattern matching to the token array, returning a new array with matched sequences
    /// replaced by composite tokens.
    /// </summary>
    /// <param name="tokens">The input tokens to process.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <returns>A new token array with patterns applied.</returns>
    public static ImmutableArray<Token> ApplyPatterns(
        this ImmutableArray<Token> tokens, 
        params ITokenDefinition[] definitions)
    {
        if (definitions.Length == 0)
            return tokens;

        var matcher = new PatternMatcher(definitions);
        return matcher.Apply(tokens);
    }

    /// <summary>
    /// Applies pattern matching to the token array, returning a new array with matched sequences
    /// replaced by composite tokens.
    /// </summary>
    /// <param name="tokens">The input tokens to process.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <returns>A new token array with patterns applied.</returns>
    public static ImmutableArray<Token> ApplyPatterns(
        this ImmutableArray<Token> tokens, 
        IEnumerable<ITokenDefinition> definitions)
    {
        var matcher = new PatternMatcher(definitions);
        return matcher.Apply(tokens);
    }

    /// <summary>
    /// Applies pattern matching to the token array and returns a diagnostic report.
    /// </summary>
    /// <param name="tokens">The input tokens to process.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <returns>A report containing the output tokens and diagnostic information.</returns>
    public static PatternMatchReport ApplyPatternsWithDiagnostics(
        this ImmutableArray<Token> tokens, 
        params ITokenDefinition[] definitions)
    {
        var matcher = new PatternMatcher(definitions, enableDiagnostics: true);
        return matcher.ApplyWithDiagnostics(tokens);
    }

    /// <summary>
    /// Applies pattern matching to the token array and returns a diagnostic report.
    /// </summary>
    /// <param name="tokens">The input tokens to process.</param>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <returns>A report containing the output tokens and diagnostic information.</returns>
    public static PatternMatchReport ApplyPatternsWithDiagnostics(
        this ImmutableArray<Token> tokens, 
        IEnumerable<ITokenDefinition> definitions)
    {
        var matcher = new PatternMatcher(definitions, enableDiagnostics: true);
        return matcher.ApplyWithDiagnostics(tokens);
    }

    #endregion
}
