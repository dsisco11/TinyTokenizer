using System.Collections.Immutable;

namespace TinyTokenizer;

#region Base Query

/// <summary>
/// Abstract base class for CSS-like token selectors.
/// Queries select tokens by type, content, position, or pattern matching.
/// </summary>
public abstract record TokenQuery
{
    /// <summary>
    /// Selects all token indices that match this query.
    /// </summary>
    /// <param name="tokens">The token array to search.</param>
    /// <returns>Indices of matching tokens.</returns>
    public abstract IEnumerable<int> Select(ImmutableArray<Token> tokens);

    /// <summary>
    /// Tests whether a single token matches this query's criteria.
    /// </summary>
    /// <param name="token">The token to test.</param>
    /// <returns>True if the token matches.</returns>
    public abstract bool Matches(Token token);

    #region Pseudo-Selectors

    /// <summary>
    /// Returns a query that selects only the first matching token.
    /// </summary>
    public TokenQuery First() => new FirstQuery(this);

    /// <summary>
    /// Returns a query that selects only the last matching token.
    /// </summary>
    public TokenQuery Last() => new LastQuery(this);

    /// <summary>
    /// Returns a query that selects only the nth matching token (0-based).
    /// </summary>
    /// <param name="n">The zero-based index of the match to select.</param>
    public TokenQuery Nth(int n) => new NthQuery(this, n);

    /// <summary>
    /// Returns a query that selects all matching tokens.
    /// This is the default behavior but provided for fluent readability.
    /// </summary>
    public TokenQuery All() => this;

    #endregion

    #region Filters

    /// <summary>
    /// Adds a predicate filter to this query.
    /// </summary>
    /// <param name="predicate">The predicate that tokens must satisfy.</param>
    public TokenQuery Where(Func<Token, bool> predicate) => new PredicateQuery(this, predicate);

    /// <summary>
    /// Filters to tokens with exact content match.
    /// </summary>
    /// <param name="content">The exact content to match.</param>
    public TokenQuery WithContent(string content) => 
        new PredicateQuery(this, t => t.ContentSpan.SequenceEqual(content.AsSpan()));

    /// <summary>
    /// Filters to tokens whose content contains the specified substring.
    /// </summary>
    /// <param name="substring">The substring to search for.</param>
    public TokenQuery WithContentContaining(string substring) => 
        new PredicateQuery(this, t => t.ContentSpan.Contains(substring.AsSpan(), StringComparison.Ordinal));

    /// <summary>
    /// Filters to tokens whose content starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The prefix to match.</param>
    public TokenQuery WithContentStartingWith(string prefix) => 
        new PredicateQuery(this, t => t.ContentSpan.StartsWith(prefix.AsSpan()));

    /// <summary>
    /// Filters to tokens whose content ends with the specified suffix.
    /// </summary>
    /// <param name="suffix">The suffix to match.</param>
    public TokenQuery WithContentEndingWith(string suffix) => 
        new PredicateQuery(this, t => t.ContentSpan.EndsWith(suffix.AsSpan()));

    #endregion

    #region Relative Combinators

    /// <summary>
    /// Returns a query that targets the position before each matched token.
    /// Used with InsertBefore operations.
    /// </summary>
    public RelativeQuery Before() => new RelativeQuery(this, RelativePosition.Before);

    /// <summary>
    /// Returns a query that targets the position after each matched token.
    /// Used with InsertAfter operations.
    /// </summary>
    public RelativeQuery After() => new RelativeQuery(this, RelativePosition.After);

    #endregion

    #region Composition Operators

    /// <summary>
    /// Creates a union query that matches tokens satisfying either query.
    /// </summary>
    public static TokenQuery operator |(TokenQuery left, TokenQuery right) => 
        new UnionQuery(left, right);

    /// <summary>
    /// Creates an intersection query that matches tokens satisfying both queries.
    /// </summary>
    public static TokenQuery operator &(TokenQuery left, TokenQuery right) => 
        new IntersectionQuery(left, right);

    #endregion
}

#endregion

#region Relative Position

/// <summary>
/// Specifies a position relative to a matched token.
/// </summary>
public enum RelativePosition
{
    /// <summary>Position before the matched token.</summary>
    Before,
    /// <summary>Position after the matched token.</summary>
    After
}

/// <summary>
/// A query that wraps another query and specifies a relative position.
/// Used for InsertBefore/InsertAfter operations.
/// </summary>
public sealed record RelativeQuery : TokenQuery
{
    /// <summary>
    /// Gets the underlying query.
    /// </summary>
    public TokenQuery InnerQuery { get; }

    /// <summary>
    /// Gets the relative position.
    /// </summary>
    public RelativePosition Position { get; }

    internal RelativeQuery(TokenQuery inner, RelativePosition position)
    {
        InnerQuery = inner;
        Position = position;
    }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens) => 
        InnerQuery.Select(tokens);

    /// <inheritdoc/>
    public override bool Matches(Token token) => InnerQuery.Matches(token);
}

#endregion

#region Type Queries

/// <summary>
/// Matches tokens of a specific type.
/// </summary>
/// <typeparam name="T">The token type to match.</typeparam>
public sealed record TypeQuery<T> : TokenQuery where T : Token
{
    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] is T)
                yield return i;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => token is T;
}

/// <summary>
/// Matches any token.
/// </summary>
public sealed record AnyQuery : TokenQuery
{
    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
            yield return i;
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => token is not null;
}

/// <summary>
/// Matches SimpleBlock tokens with a specific opener character.
/// </summary>
public sealed record BlockQuery : TokenQuery
{
    /// <summary>
    /// Gets the opener character to match, or null to match any block.
    /// </summary>
    public char? Opener { get; init; }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (Matches(tokens[i]))
                yield return i;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        if (token is not SimpleBlock block)
            return false;

        if (Opener is null)
            return true;

        return block.OpeningDelimiter.FirstChar == Opener.Value;
    }
}

#endregion

#region Index Queries

/// <summary>
/// Matches a token at a specific index.
/// </summary>
public sealed record IndexQuery : TokenQuery
{
    /// <summary>
    /// Gets the index to match.
    /// </summary>
    public required int Index { get; init; }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        if (Index >= 0 && Index < tokens.Length)
            yield return Index;
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => true; // Index-based, always matches the token at that position
}

/// <summary>
/// Matches tokens within a range of indices.
/// </summary>
public sealed record RangeQuery : TokenQuery
{
    /// <summary>
    /// Gets the start index (inclusive).
    /// </summary>
    public required int Start { get; init; }

    /// <summary>
    /// Gets the end index (exclusive).
    /// </summary>
    public required int End { get; init; }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        int start = Math.Max(0, Start);
        int end = Math.Min(tokens.Length, End);

        for (int i = start; i < end; i++)
            yield return i;
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => true; // Range-based, always matches tokens in range
}

/// <summary>
/// Selects the first token in the array.
/// </summary>
public sealed record FirstIndexQuery : TokenQuery
{
    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        if (tokens.Length > 0)
            yield return 0;
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => true;
}

/// <summary>
/// Selects the last token in the array.
/// </summary>
public sealed record LastIndexQuery : TokenQuery
{
    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        if (tokens.Length > 0)
            yield return tokens.Length - 1;
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => true;
}

#endregion

#region Pseudo-Selector Queries

/// <summary>
/// Wraps a query to select only the first match.
/// </summary>
public sealed record FirstQuery : TokenQuery
{
    private readonly TokenQuery _inner;

    internal FirstQuery(TokenQuery inner) => _inner = inner;

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        foreach (var index in _inner.Select(tokens))
        {
            yield return index;
            yield break;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => _inner.Matches(token);
}

/// <summary>
/// Wraps a query to select only the last match.
/// </summary>
public sealed record LastQuery : TokenQuery
{
    private readonly TokenQuery _inner;

    internal LastQuery(TokenQuery inner) => _inner = inner;

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        int? lastIndex = null;
        foreach (var index in _inner.Select(tokens))
        {
            lastIndex = index;
        }

        if (lastIndex.HasValue)
            yield return lastIndex.Value;
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => _inner.Matches(token);
}

/// <summary>
/// Wraps a query to select only the nth match (0-based).
/// </summary>
public sealed record NthQuery : TokenQuery
{
    private readonly TokenQuery _inner;
    private readonly int _n;

    internal NthQuery(TokenQuery inner, int n)
    {
        _inner = inner;
        _n = n;
    }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        int count = 0;
        foreach (var index in _inner.Select(tokens))
        {
            if (count == _n)
            {
                yield return index;
                yield break;
            }
            count++;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => _inner.Matches(token);
}

#endregion

#region Filter Queries

/// <summary>
/// Wraps a query with an additional predicate filter.
/// </summary>
public sealed record PredicateQuery : TokenQuery
{
    private readonly TokenQuery _inner;
    private readonly Func<Token, bool> _predicate;

    internal PredicateQuery(TokenQuery inner, Func<Token, bool> predicate)
    {
        _inner = inner;
        _predicate = predicate;
    }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        foreach (var index in _inner.Select(tokens))
        {
            if (_predicate(tokens[index]))
                yield return index;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => _inner.Matches(token) && _predicate(token);
}

#endregion

#region Pattern Query

/// <summary>
/// Matches tokens that would be matched by a pattern definition.
/// Bridges the TokenQuery system with the PatternMatcher system.
/// </summary>
public sealed record PatternQuery : TokenQuery
{
    private readonly ITokenDefinition _definition;

    internal PatternQuery(ITokenDefinition definition)
    {
        _definition = definition;
    }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        // Apply the pattern and find which indices got matched
        var matcher = new PatternMatcher([_definition]);
        var result = matcher.Apply(tokens);

        // Find CompositeTokens that match our pattern name
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] is CompositeToken composite && composite.PatternName == _definition.Name)
            {
                yield return i;
            }
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token)
    {
        // For single token matching, check if it's already a matched composite
        return token is CompositeToken composite && composite.PatternName == _definition.Name;
    }
}

#endregion

#region Composition Queries

/// <summary>
/// Matches tokens that satisfy either of two queries (OR logic).
/// </summary>
public sealed record UnionQuery : TokenQuery
{
    private readonly TokenQuery _left;
    private readonly TokenQuery _right;

    internal UnionQuery(TokenQuery left, TokenQuery right)
    {
        _left = left;
        _right = right;
    }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        var seen = new HashSet<int>();

        foreach (var index in _left.Select(tokens))
        {
            if (seen.Add(index))
                yield return index;
        }

        foreach (var index in _right.Select(tokens))
        {
            if (seen.Add(index))
                yield return index;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => _left.Matches(token) || _right.Matches(token);
}

/// <summary>
/// Matches tokens that satisfy both queries (AND logic).
/// </summary>
public sealed record IntersectionQuery : TokenQuery
{
    private readonly TokenQuery _left;
    private readonly TokenQuery _right;

    internal IntersectionQuery(TokenQuery left, TokenQuery right)
    {
        _left = left;
        _right = right;
    }

    /// <inheritdoc/>
    public override IEnumerable<int> Select(ImmutableArray<Token> tokens)
    {
        var leftMatches = new HashSet<int>(_left.Select(tokens));

        foreach (var index in _right.Select(tokens))
        {
            if (leftMatches.Contains(index))
                yield return index;
        }
    }

    /// <inheritdoc/>
    public override bool Matches(Token token) => _left.Matches(token) && _right.Matches(token);
}

#endregion
