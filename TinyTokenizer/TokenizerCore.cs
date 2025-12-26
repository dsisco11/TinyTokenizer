using System.Collections.Frozen;
using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Static helper class containing shared delimiter mappings and character classification
/// utilities used by both synchronous and asynchronous tokenizers.
/// </summary>
public static class TokenizerCore
{
    #region Delimiter Mappings

    /// <summary>
    /// Maps opening delimiters to their corresponding closing delimiters.
    /// </summary>
    public static readonly FrozenDictionary<char, char> OpenToClose = new Dictionary<char, char>
    {
        ['{'] = '}',
        ['['] = ']',
        ['('] = ')'
    }.ToFrozenDictionary();

    /// <summary>
    /// Maps closing delimiters to their corresponding opening delimiters.
    /// </summary>
    public static readonly FrozenDictionary<char, char> CloseToOpen = new Dictionary<char, char>
    {
        ['}'] = '{',
        [']'] = '[',
        [')'] = '('
    }.ToFrozenDictionary();

    /// <summary>
    /// Maps opening delimiters to their corresponding token types.
    /// </summary>
    public static readonly FrozenDictionary<char, TokenType> OpenToTokenType = new Dictionary<char, TokenType>
    {
        ['{'] = TokenType.BraceBlock,
        ['['] = TokenType.BracketBlock,
        ['('] = TokenType.ParenthesisBlock
    }.ToFrozenDictionary();

    /// <summary>
    /// Set of closing delimiter characters.
    /// </summary>
    public static readonly FrozenSet<char> ClosingDelimiters = new HashSet<char> { '}', ']', ')' }.ToFrozenSet();

    /// <summary>
    /// Set of opening delimiter characters.
    /// </summary>
    public static readonly FrozenSet<char> OpeningDelimiters = new HashSet<char> { '{', '[', '(' }.ToFrozenSet();

    /// <summary>
    /// Characters that can start a string literal.
    /// </summary>
    public static readonly FrozenSet<char> StringQuotes = new HashSet<char> { '"', '\'' }.ToFrozenSet();

    #endregion

    #region Character Classification

    /// <summary>
    /// Determines if a character is an opening delimiter.
    /// </summary>
    public static bool IsOpeningDelimiter(char c) => OpeningDelimiters.Contains(c);

    /// <summary>
    /// Determines if a character is a closing delimiter.
    /// </summary>
    public static bool IsClosingDelimiter(char c) => ClosingDelimiters.Contains(c);

    /// <summary>
    /// Determines if a character is a quote character that starts a string literal.
    /// </summary>
    public static bool IsStringQuote(char c) => StringQuotes.Contains(c);

    /// <summary>
    /// Gets the closing delimiter for an opening delimiter.
    /// </summary>
    /// <param name="opener">The opening delimiter character.</param>
    /// <param name="closer">The corresponding closing delimiter, if found.</param>
    /// <returns>True if the opener is a valid opening delimiter.</returns>
    public static bool TryGetClosingDelimiter(char opener, out char closer)
    {
        return OpenToClose.TryGetValue(opener, out closer);
    }

    /// <summary>
    /// Gets the token type for an opening delimiter.
    /// </summary>
    /// <param name="opener">The opening delimiter character.</param>
    /// <param name="tokenType">The corresponding token type, if found.</param>
    /// <returns>True if the opener is a valid opening delimiter.</returns>
    public static bool TryGetBlockTokenType(char opener, out TokenType tokenType)
    {
        return OpenToTokenType.TryGetValue(opener, out tokenType);
    }

    #endregion

    #region Pattern Matching Helpers

    /// <summary>
    /// Checks if the given span matches a string pattern at the specified position.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <param name="position">The position to check at.</param>
    /// <param name="pattern">The pattern to match.</param>
    /// <returns>True if the pattern matches at the position.</returns>
    public static bool MatchesAt(ReadOnlySpan<char> span, int position, ReadOnlySpan<char> pattern)
    {
        if (position + pattern.Length > span.Length)
        {
            return false;
        }

        return span.Slice(position, pattern.Length).SequenceEqual(pattern);
    }

    /// <summary>
    /// Tries to match any comment style at the current position.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <param name="position">The position to check at.</param>
    /// <param name="commentStyles">The comment styles to try matching.</param>
    /// <returns>The matched comment style, or null if none match.</returns>
    public static CommentStyle? TryMatchCommentStart(
        ReadOnlySpan<char> span,
        int position,
        ImmutableArray<CommentStyle> commentStyles)
    {
        foreach (var style in commentStyles)
        {
            if (MatchesAt(span, position, style.Start.AsSpan()))
            {
                return style;
            }
        }
        return null;
    }

    #endregion

    #region Token Content Helpers

    /// <summary>
    /// Size threshold above which we use MemoryPool instead of ArrayPool.
    /// </summary>
    public const int LargeTokenThreshold = 4096;

    #endregion
}
