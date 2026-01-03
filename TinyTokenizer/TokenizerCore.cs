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
    /// Gets the closing delimiter character for an opening delimiter.
    /// </summary>
    /// <param name="opener">The opening delimiter character.</param>
    /// <returns>The corresponding closing delimiter character.</returns>
    /// <exception cref="ArgumentException">Thrown when the opener is not a valid opening delimiter.</exception>
    public static char GetClosingDelimiter(char opener) => opener switch
    {
        '{' => '}',
        '[' => ']',
        '(' => ')',
        _ => throw new ArgumentException($"Invalid opening delimiter: '{opener}'", nameof(opener))
    };

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

    /// <summary>
    /// Gets the SimpleTokenType for an opening delimiter character.
    /// </summary>
    /// <param name="opener">The opening delimiter character.</param>
    /// <returns>The corresponding SimpleTokenType.</returns>
    public static SimpleTokenType GetOpeningDelimiterType(char opener) => opener switch
    {
        '{' => SimpleTokenType.OpenBrace,
        '[' => SimpleTokenType.OpenBracket,
        '(' => SimpleTokenType.OpenParen,
        _ => throw new ArgumentException($"Invalid opening delimiter: '{opener}'", nameof(opener))
    };

    /// <summary>
    /// Gets the SimpleTokenType for a closing delimiter character.
    /// </summary>
    /// <param name="closer">The closing delimiter character.</param>
    /// <returns>The corresponding SimpleTokenType.</returns>
    public static SimpleTokenType GetClosingDelimiterType(char closer) => closer switch
    {
        '}' => SimpleTokenType.CloseBrace,
        ']' => SimpleTokenType.CloseBracket,
        ')' => SimpleTokenType.CloseParen,
        _ => throw new ArgumentException($"Invalid closing delimiter: '{closer}'", nameof(closer))
    };

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

    #region SimpleToken Type Classification

    /// <summary>
    /// Checks if a SimpleTokenType represents a character that could be part of an operator.
    /// </summary>
    /// <param name="type">The SimpleTokenType to check.</param>
    /// <returns>True if the token type can be part of an operator.</returns>
    public static bool IsOperatorCapableToken(SimpleTokenType type)
    {
        return type is SimpleTokenType.Symbol
            or SimpleTokenType.Equals
            or SimpleTokenType.Plus
            or SimpleTokenType.Minus
            or SimpleTokenType.LessThan
            or SimpleTokenType.GreaterThan
            or SimpleTokenType.Pipe
            or SimpleTokenType.Ampersand
            or SimpleTokenType.Percent
            or SimpleTokenType.Caret
            or SimpleTokenType.Tilde
            or SimpleTokenType.Question
            or SimpleTokenType.Exclamation
            or SimpleTokenType.Colon
            or SimpleTokenType.At;
    }

    /// <summary>
    /// Gets the character representation of a SimpleTokenType for operator matching.
    /// Returns null for token types that don't map to a single character.
    /// </summary>
    /// <param name="type">The SimpleTokenType to convert.</param>
    /// <returns>The corresponding character, or null if not applicable.</returns>
    public static char? GetOperatorChar(SimpleTokenType type)
    {
        return type switch
        {
            SimpleTokenType.Equals => '=',
            SimpleTokenType.Plus => '+',
            SimpleTokenType.Minus => '-',
            SimpleTokenType.LessThan => '<',
            SimpleTokenType.GreaterThan => '>',
            SimpleTokenType.Pipe => '|',
            SimpleTokenType.Ampersand => '&',
            SimpleTokenType.Percent => '%',
            SimpleTokenType.Caret => '^',
            SimpleTokenType.Tilde => '~',
            SimpleTokenType.Question => '?',
            SimpleTokenType.Exclamation => '!',
            SimpleTokenType.Colon => ':',
            SimpleTokenType.Hash => '#',
            SimpleTokenType.At => '@',
            SimpleTokenType.Slash => '/',
            SimpleTokenType.Asterisk => '*',
            SimpleTokenType.Dot => '.',
            SimpleTokenType.Comma => ',',
            SimpleTokenType.Semicolon => ';',
            _ => null
        };
    }

    /// <summary>
    /// Gets the closing delimiter SimpleTokenType for an opening delimiter SimpleTokenType.
    /// </summary>
    /// <param name="opener">The opening delimiter token type.</param>
    /// <returns>The corresponding closing delimiter token type.</returns>
    public static SimpleTokenType GetMatchingCloser(SimpleTokenType opener)
    {
        return opener switch
        {
            SimpleTokenType.OpenBrace => SimpleTokenType.CloseBrace,
            SimpleTokenType.OpenBracket => SimpleTokenType.CloseBracket,
            SimpleTokenType.OpenParen => SimpleTokenType.CloseParen,
            _ => throw new ArgumentException($"Not an opening delimiter: {opener}", nameof(opener))
        };
    }

    /// <summary>
    /// Gets the TokenType for a block opened by the given SimpleTokenType.
    /// </summary>
    /// <param name="opener">The opening delimiter token type.</param>
    /// <returns>The corresponding block TokenType.</returns>
    public static TokenType GetBlockTokenType(SimpleTokenType opener)
    {
        return opener switch
        {
            SimpleTokenType.OpenBrace => TokenType.BraceBlock,
            SimpleTokenType.OpenBracket => TokenType.BracketBlock,
            SimpleTokenType.OpenParen => TokenType.ParenthesisBlock,
            _ => throw new ArgumentException($"Not an opening delimiter: {opener}", nameof(opener))
        };
    }

    /// <summary>
    /// Checks if a SimpleTokenType represents an opening delimiter.
    /// </summary>
    public static bool IsOpeningDelimiter(SimpleTokenType type)
    {
        return type is SimpleTokenType.OpenBrace or SimpleTokenType.OpenBracket or SimpleTokenType.OpenParen;
    }

    /// <summary>
    /// Checks if a SimpleTokenType represents a closing delimiter.
    /// </summary>
    public static bool IsClosingDelimiter(SimpleTokenType type)
    {
        return type is SimpleTokenType.CloseBrace or SimpleTokenType.CloseBracket or SimpleTokenType.CloseParen;
    }

    #endregion
}
