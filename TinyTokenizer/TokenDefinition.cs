using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Non-generic base interface for token definitions, used by the pattern matcher.
/// </summary>
public interface ITokenDefinition
{
    /// <summary>
    /// Gets the name of this pattern definition.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the pattern alternatives.
    /// </summary>
    ImmutableArray<ImmutableArray<TokenSelector>> Patterns { get; }

    /// <summary>
    /// Gets whether to skip whitespace tokens when matching.
    /// </summary>
    bool SkipWhitespace { get; }

    /// <summary>
    /// Gets the priority of this pattern.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Creates a composite token from the matched tokens.
    /// </summary>
    /// <param name="matchedTokens">The tokens that matched the pattern.</param>
    /// <param name="combinedContent">The combined content of all matched tokens.</param>
    /// <param name="position">The position of the first matched token.</param>
    /// <returns>A new composite token.</returns>
    CompositeToken CreateToken(ImmutableArray<Token> matchedTokens, ReadOnlyMemory<char> combinedContent, int position);
}

/// <summary>
/// Defines a pattern for matching token sequences and creating composite tokens.
/// Multiple patterns can be specified as alternatives (OR logic).
/// Token creation uses parameterless constructor via the new() constraint.
/// </summary>
/// <typeparam name="T">The type of composite token to create when the pattern matches.</typeparam>
public sealed record TokenDefinition<T> : ITokenDefinition where T : CompositeToken, new()
{
    /// <summary>
    /// Gets the name of this pattern definition, used in diagnostics and for the PatternName property.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the pattern alternatives. Each inner array is a sequence of selectors that must match in order.
    /// If any alternative matches, the pattern succeeds (OR logic between alternatives).
    /// </summary>
    public required ImmutableArray<ImmutableArray<TokenSelector>> Patterns { get; init; }

    /// <summary>
    /// Gets whether to skip whitespace tokens when matching the pattern.
    /// Default is true.
    /// </summary>
    public bool SkipWhitespace { get; init; } = true;

    /// <summary>
    /// Gets the priority of this pattern. Higher priority patterns are matched first.
    /// Default is 0.
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <inheritdoc/>
    CompositeToken ITokenDefinition.CreateToken(ImmutableArray<Token> matchedTokens, ReadOnlyMemory<char> combinedContent, int position)
    {
        return CreateToken(matchedTokens, combinedContent, position);
    }

    /// <summary>
    /// Creates a composite token from the matched tokens using the new() constraint.
    /// Token-specific properties are computed from MatchedTokens via property getters.
    /// </summary>
    /// <param name="matchedTokens">The tokens that matched the pattern.</param>
    /// <param name="combinedContent">The combined content of all matched tokens.</param>
    /// <param name="position">The position of the first matched token.</param>
    /// <returns>A new composite token of type T.</returns>
    public T CreateToken(ImmutableArray<Token> matchedTokens, ReadOnlyMemory<char> combinedContent, int position)
    {
        return new T
        {
            Content = combinedContent,
            Position = position,
            MatchedTokens = matchedTokens,
            PatternName = Name
        };
    }
}

/// <summary>
/// Factory methods for creating common token definitions.
/// Token-specific properties are computed from MatchedTokens via property getters.
/// </summary>
public static class TokenDefinitions
{
    /// <summary>
    /// Creates a function call pattern: Ident + ParenBlock
    /// Example: func(a, b)
    /// </summary>
    public static TokenDefinition<FunctionCallToken> FunctionCall() => new()
    {
        Name = "FunctionCall",
        Patterns = [[Match.Ident(), Match.Block('(')]]
    };

    /// <summary>
    /// Creates a property access pattern: Ident + Symbol('.') + Ident
    /// Example: obj.property
    /// </summary>
    public static TokenDefinition<PropertyAccessToken> PropertyAccess() => new()
    {
        Name = "PropertyAccess",
        Patterns = [[Match.Ident(), Match.Symbol('.'), Match.Ident()]]
    };

    /// <summary>
    /// Creates a type annotation pattern: Ident + Symbol(':') + Ident
    /// Example: param: string
    /// </summary>
    public static TokenDefinition<TypeAnnotationToken> TypeAnnotation() => new()
    {
        Name = "TypeAnnotation",
        Patterns = [[Match.Ident(), Match.Symbol(':'), Match.Ident()]]
    };

    /// <summary>
    /// Creates an assignment pattern: Ident + Operator('=') + (any tokens until end)
    /// Example: x = 5
    /// </summary>
    /// <remarks>
    /// Note: This simple pattern only matches single-token values. 
    /// For complex expressions, use a custom pattern.
    /// </remarks>
    public static TokenDefinition<AssignmentToken> Assignment() => new()
    {
        Name = "Assignment",
        Patterns = [[Match.Ident(), Match.Operator("="), Match.Any()]]
    };

    /// <summary>
    /// Creates an array access pattern: Ident + BracketBlock
    /// Example: arr[0]
    /// </summary>
    public static TokenDefinition<ArrayAccessToken> ArrayAccess() => new()
    {
        Name = "ArrayAccess",
        Patterns = [[Match.Ident(), Match.Block('[')]]
    };
}
