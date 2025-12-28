using System.Collections.Immutable;

namespace TinyTokenizer;

#region Base Selector

/// <summary>
/// Abstract base class for token selectors used in pattern matching.
/// Each derived selector type provides specialized matching logic.
/// </summary>
public abstract record TokenSelector
{
    /// <summary>
    /// Tests whether the given token matches this selector.
    /// </summary>
    /// <param name="token">The token to test.</param>
    /// <returns>True if the token matches this selector's criteria.</returns>
    public abstract bool Matches(Token token);

    /// <summary>
    /// Gets a description of what this selector matches, for diagnostics.
    /// </summary>
    public abstract string Description { get; }
}

#endregion

#region Specialized Selectors

/// <summary>
/// Matches any token.
/// </summary>
public sealed record AnySelector : TokenSelector
{
    /// <inheritdoc/>
    public override bool Matches(Token token) => token is not null;

    /// <inheritdoc/>
    public override string Description => "Any";
}

/// <summary>
/// Matches IdentToken, optionally with exact content.
/// </summary>
public sealed record IdentSelector : TokenSelector
{
    /// <summary>
    /// Gets the exact content to match, or null to match any IdentToken.
    /// </summary>
    public string? ExactContent { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token.Type != TokenType.Ident)
            return false;

        if (ExactContent is not null)
            return token.ContentSpan.SequenceEqual(ExactContent.AsSpan());

        return true;
    }

    /// <inheritdoc/>
    public override string Description => ExactContent is not null 
        ? $"Ident '{ExactContent}'" 
        : "Ident";
}

/// <summary>
/// Matches WhitespaceToken.
/// </summary>
public sealed record WhitespaceSelector : TokenSelector
{
    /// <inheritdoc/>
    public override bool Matches(Token token) => token.Type == TokenType.Whitespace;

    /// <inheritdoc/>
    public override string Description => "Whitespace";
}

/// <summary>
/// Matches SymbolToken with a specific character.
/// </summary>
public sealed record SymbolSelector : TokenSelector
{
    /// <summary>
    /// Gets the symbol character to match.
    /// </summary>
    public required char Symbol { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        return token.Type == TokenType.Symbol && ((SymbolToken)token).Symbol == Symbol;
    }

    /// <inheritdoc/>
    public override string Description => $"Symbol '{Symbol}'";
}

/// <summary>
/// Matches OperatorToken, optionally with a specific operator string.
/// </summary>
public sealed record OperatorSelector : TokenSelector
{
    /// <summary>
    /// Gets the operator string to match, or null to match any OperatorToken.
    /// </summary>
    public string? Operator { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token.Type != TokenType.Operator)
            return false;

        if (Operator is not null)
            return ((OperatorToken)token).Operator == Operator;

        return true;
    }

    /// <inheritdoc/>
    public override string Description => Operator is not null 
        ? $"Operator '{Operator}'" 
        : "Operator";
}

/// <summary>
/// Matches NumericToken, optionally with a specific numeric type.
/// </summary>
public sealed record NumericSelector : TokenSelector
{
    /// <summary>
    /// Gets the numeric type to match, or null to match any NumericToken.
    /// </summary>
    public NumericType? NumericType { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token.Type != TokenType.Numeric)
            return false;

        if (NumericType.HasValue)
            return ((NumericToken)token).NumericType == NumericType.Value;

        return true;
    }

    /// <inheritdoc/>
    public override string Description => NumericType.HasValue 
        ? $"Numeric ({NumericType.Value})" 
        : "Numeric";
}

/// <summary>
/// Matches StringToken, optionally with a specific quote character.
/// </summary>
public sealed record StringSelector : TokenSelector
{
    /// <summary>
    /// Gets the quote character to match, or null to match any StringToken.
    /// </summary>
    public char? Quote { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token.Type != TokenType.String)
            return false;

        if (Quote.HasValue)
            return ((StringToken)token).Quote == Quote.Value;

        return true;
    }

    /// <inheritdoc/>
    public override string Description => Quote.HasValue 
        ? $"String ({Quote.Value})" 
        : "String";
}

/// <summary>
/// Matches CommentToken.
/// </summary>
public sealed record CommentSelector : TokenSelector
{
    /// <inheritdoc/>
    public override bool Matches(Token token) => token.Type == TokenType.Comment;

    /// <inheritdoc/>
    public override string Description => "Comment";
}

/// <summary>
/// Matches TaggedIdentToken, optionally with a specific tag prefix.
/// </summary>
public sealed record TaggedIdentSelector : TokenSelector
{
    /// <summary>
    /// Gets the tag prefix character to match, or null to match any TaggedIdentToken.
    /// </summary>
    public char? TagPrefix { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token.Type != TokenType.TaggedIdent)
            return false;

        if (TagPrefix.HasValue)
            return ((TaggedIdentToken)token).Tag == TagPrefix.Value;

        return true;
    }

    /// <inheritdoc/>
    public override string Description => TagPrefix.HasValue 
        ? $"TaggedIdent '{TagPrefix.Value}'" 
        : "TaggedIdent";
}

/// <summary>
/// Matches SimpleBlock, optionally with a specific opening delimiter.
/// </summary>
public sealed record SimpleBlockSelector : TokenSelector
{
    /// <summary>
    /// Gets the opening delimiter character to match, or null to match any SimpleBlock.
    /// </summary>
    public char? BlockOpener { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        // SimpleBlock.Type returns BraceBlock, BracketBlock, or ParenthesisBlock
        var type = token.Type;
        if (type != TokenType.BraceBlock && type != TokenType.BracketBlock && type != TokenType.ParenthesisBlock)
            return false;

        if (BlockOpener.HasValue)
            return ((SimpleBlock)token).OpeningDelimiter.FirstChar == BlockOpener.Value;

        return true;
    }

    /// <inheritdoc/>
    public override string Description => BlockOpener.HasValue 
        ? $"Block '{BlockOpener.Value}'" 
        : "Block";
}

/// <summary>
/// Matches if any of the alternative selectors match (OR logic).
/// </summary>
public sealed record AnyOfSelector : TokenSelector
{
    /// <summary>
    /// Gets the alternative selectors. If any matches, the overall match succeeds.
    /// </summary>
    public required ImmutableArray<TokenSelector> Alternatives { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token is null || Alternatives.IsEmpty)
            return false;

        return Alternatives.Any(alt => alt.Matches(token));
    }

    /// <inheritdoc/>
    public override string Description => 
        $"AnyOf({string.Join(" | ", Alternatives.Select(a => a.Description))})";
}

/// <summary>
/// Matches tokens based on content criteria (prefix, suffix, contains, or predicate).
/// </summary>
public sealed record ContentSelector : TokenSelector
{
    /// <summary>
    /// Gets the content prefix to match (case-sensitive).
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets the content suffix to match (case-sensitive).
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Gets the substring the content must contain (case-sensitive).
    /// </summary>
    public string? Contains { get; init; }

    /// <summary>
    /// Gets a custom predicate for content matching.
    /// </summary>
    public Func<ReadOnlyMemory<char>, bool>? Predicate { get; init; }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token is null)
            return false;

        if (Prefix is not null && !token.ContentSpan.StartsWith(Prefix.AsSpan()))
            return false;

        if (Suffix is not null && !token.ContentSpan.EndsWith(Suffix.AsSpan()))
            return false;

        if (Contains is not null && !token.ContentSpan.Contains(Contains.AsSpan(), StringComparison.Ordinal))
            return false;

        if (Predicate is not null && !Predicate(token.Content))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public override string Description
    {
        get
        {
            var parts = new List<string>();
            if (Prefix is not null) parts.Add($"starts with '{Prefix}'");
            if (Suffix is not null) parts.Add($"ends with '{Suffix}'");
            if (Contains is not null) parts.Add($"contains '{Contains}'");
            if (Predicate is not null) parts.Add("matches predicate");
            return parts.Count > 0 ? $"Content({string.Join(", ", parts)})" : "Content";
        }
    }
}

#endregion

#region Match Factory

/// <summary>
/// Factory class providing fluent methods for creating <see cref="TokenSelector"/> instances.
/// </summary>
public static class Match
{
    /// <summary>
    /// Creates a selector that matches any token.
    /// </summary>
    public static TokenSelector Any() => new AnySelector();

    /// <summary>
    /// Creates a selector that matches any IdentToken.
    /// </summary>
    public static TokenSelector Ident() => new IdentSelector();

    /// <summary>
    /// Creates a selector that matches an IdentToken with exact content.
    /// </summary>
    /// <param name="content">The exact content to match.</param>
    public static TokenSelector Ident(string content) => new IdentSelector { ExactContent = content };

    /// <summary>
    /// Creates a selector that matches any WhitespaceToken.
    /// </summary>
    public static TokenSelector Whitespace() => new WhitespaceSelector();

    /// <summary>
    /// Creates a selector that matches a SymbolToken with the specified character.
    /// </summary>
    /// <param name="symbol">The symbol character to match.</param>
    public static TokenSelector Symbol(char symbol) => new SymbolSelector { Symbol = symbol };

    /// <summary>
    /// Creates a selector that matches an OperatorToken with the specified operator.
    /// </summary>
    /// <param name="op">The operator string to match.</param>
    public static TokenSelector Operator(string op) => new OperatorSelector { Operator = op };

    /// <summary>
    /// Creates a selector that matches any OperatorToken.
    /// </summary>
    public static TokenSelector Operator() => new OperatorSelector();

    /// <summary>
    /// Creates a selector that matches any NumericToken.
    /// </summary>
    public static TokenSelector Numeric() => new NumericSelector();

    /// <summary>
    /// Creates a selector that matches a NumericToken with the specified numeric type.
    /// </summary>
    /// <param name="numericType">The numeric type to match.</param>
    public static TokenSelector Numeric(NumericType numericType) => new NumericSelector { NumericType = numericType };

    /// <summary>
    /// Creates a selector that matches any StringToken.
    /// </summary>
    public static TokenSelector String() => new StringSelector();

    /// <summary>
    /// Creates a selector that matches a StringToken with the specified quote character.
    /// </summary>
    /// <param name="quote">The quote character to match.</param>
    public static TokenSelector String(char quote) => new StringSelector { Quote = quote };

    /// <summary>
    /// Creates a selector that matches any CommentToken.
    /// </summary>
    public static TokenSelector Comment() => new CommentSelector();

    /// <summary>
    /// Creates a selector that matches any TaggedIdentToken.
    /// </summary>
    public static TokenSelector TaggedIdent() => new TaggedIdentSelector();

    /// <summary>
    /// Creates a selector that matches a TaggedIdentToken with the specified prefix.
    /// </summary>
    /// <param name="prefix">The tag prefix character to match.</param>
    public static TokenSelector TaggedIdent(char prefix) => new TaggedIdentSelector { TagPrefix = prefix };

    /// <summary>
    /// Creates a selector that matches any SimpleBlock.
    /// </summary>
    public static TokenSelector Block() => new SimpleBlockSelector();

    /// <summary>
    /// Creates a selector that matches a SimpleBlock with the specified opening delimiter.
    /// </summary>
    /// <param name="opener">The opening delimiter character to match.</param>
    public static TokenSelector Block(char opener) => new SimpleBlockSelector { BlockOpener = opener };

    /// <summary>
    /// Creates a selector that matches any of the specified selectors (OR logic).
    /// </summary>
    /// <param name="selectors">The selectors to match against.</param>
    public static TokenSelector AnyOf(params TokenSelector[] selectors) => new AnyOfSelector { Alternatives = [.. selectors] };

    /// <summary>
    /// Creates a selector that matches tokens with content starting with the specified prefix.
    /// </summary>
    /// <param name="prefix">The content prefix to match.</param>
    public static TokenSelector ContentStartsWith(string prefix) => new ContentSelector { Prefix = prefix };

    /// <summary>
    /// Creates a selector that matches tokens with content ending with the specified suffix.
    /// </summary>
    /// <param name="suffix">The content suffix to match.</param>
    public static TokenSelector ContentEndsWith(string suffix) => new ContentSelector { Suffix = suffix };

    /// <summary>
    /// Creates a selector that matches tokens with content containing the specified substring.
    /// </summary>
    /// <param name="substring">The substring to match.</param>
    public static TokenSelector ContentContains(string substring) => new ContentSelector { Contains = substring };

    /// <summary>
    /// Creates a selector that matches tokens satisfying the specified content predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test content against.</param>
    public static TokenSelector ContentMatches(Func<ReadOnlyMemory<char>, bool> predicate) => new ContentSelector { Predicate = predicate };
}

#endregion
