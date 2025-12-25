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

            if (token is BlockToken block && block.Children.HasErrors())
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

            if (token is BlockToken block)
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

            if (token is BlockToken block)
            {
                foreach (var nested in block.Children.OfTokenType<T>())
                {
                    yield return nested;
                }
            }
        }
    }

    #endregion
}
