using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// Matches token sequences against pattern definitions and creates composite tokens.
/// Level 3 of the three-level tokenizer architecture.
/// </summary>
public sealed class PatternMatcher
{
    private readonly ImmutableArray<ITokenDefinition> _definitions;
    private readonly bool _enableDiagnostics;
    private List<PatternMatchAttempt>? _attempts;

    /// <summary>
    /// Initializes a new instance of <see cref="PatternMatcher"/> with the specified definitions.
    /// </summary>
    /// <param name="definitions">The pattern definitions to match against.</param>
    /// <param name="enableDiagnostics">Whether to collect diagnostic information about match attempts.</param>
    public PatternMatcher(IEnumerable<ITokenDefinition> definitions, bool enableDiagnostics = false)
    {
        // Sort by priority (higher first), then by pattern length (longer first for greedy matching)
        _definitions = definitions
            .OrderByDescending(d => d.Priority)
            .ThenByDescending(d => d.Patterns.Max(p => p.Length))
            .ToImmutableArray();
        _enableDiagnostics = enableDiagnostics;
    }

    /// <summary>
    /// Applies pattern matching to the token array, returning a new array with matched sequences
    /// replaced by composite tokens.
    /// </summary>
    /// <param name="tokens">The input tokens to process.</param>
    /// <returns>A new token array with patterns applied.</returns>
    public ImmutableArray<Token> Apply(ImmutableArray<Token> tokens)
    {
        if (_enableDiagnostics)
        {
            _attempts = new List<PatternMatchAttempt>();
        }

        var result = ApplyInternal(tokens);
        
        return result;
    }

    /// <summary>
    /// Applies pattern matching and returns a diagnostic report.
    /// </summary>
    /// <param name="tokens">The input tokens to process.</param>
    /// <returns>A report containing the output tokens and diagnostic information.</returns>
    public PatternMatchReport ApplyWithDiagnostics(ImmutableArray<Token> tokens)
    {
        _attempts = new List<PatternMatchAttempt>();
        
        var output = ApplyInternal(tokens);
        
        return new PatternMatchReport
        {
            InputTokens = tokens,
            OutputTokens = output,
            Attempts = [.. _attempts]
        };
    }

    private ImmutableArray<Token> ApplyInternal(ImmutableArray<Token> tokens)
    {
        if (tokens.IsEmpty || _definitions.IsEmpty)
            return tokens;

        var result = ImmutableArray.CreateBuilder<Token>();
        int position = 0;

        while (position < tokens.Length)
        {
            var matchResult = TryMatchAtPosition(tokens, position);

            if (matchResult.HasValue)
            {
                var (compositeToken, tokensConsumed) = matchResult.Value;
                result.Add(compositeToken);
                position += tokensConsumed;
            }
            else
            {
                // No match - process token (recursively apply to blocks)
                var token = tokens[position];
                if (token is SimpleBlock blockToken)
                {
                    // Recursively apply patterns to block children
                    var processedChildren = ApplyInternal(blockToken.Children);
                    if (!processedChildren.SequenceEqual(blockToken.Children))
                    {
                        token = blockToken with { Children = processedChildren };
                    }
                }
                result.Add(token);
                position++;
            }
        }

        return result.ToImmutable();
    }

    private (CompositeToken Token, int TokensConsumed)? TryMatchAtPosition(ImmutableArray<Token> tokens, int startPosition)
    {
        foreach (var definition in _definitions)
        {
            foreach (var pattern in definition.Patterns)
            {
                var matchResult = TryMatchPattern(tokens, startPosition, pattern, definition.SkipWhitespace);
                
                if (_enableDiagnostics && _attempts is not null)
                {
                    _attempts.Add(new PatternMatchAttempt
                    {
                        PatternName = definition.Name,
                        TokenIndex = startPosition,
                        Result = matchResult.Result,
                        SelectorResults = matchResult.SelectorResults
                    });
                }

                if (matchResult.Result == MatchResult.Success)
                {
                    var matchedTokens = matchResult.MatchedTokens;
                    var combinedContent = CombineContent(matchedTokens);
                    var position = matchedTokens[0].Position;
                    
                    var compositeToken = definition.CreateToken(matchedTokens, combinedContent, position);
                    return (compositeToken, matchResult.TokensConsumed);
                }
            }
        }

        return null;
    }

    private PatternMatchResult TryMatchPattern(
        ImmutableArray<Token> tokens, 
        int startPosition, 
        ImmutableArray<TokenSelector> pattern,
        bool skipWhitespace)
    {
        var matchedTokens = ImmutableArray.CreateBuilder<Token>();
        var selectorResults = ImmutableArray.CreateBuilder<SelectorMatchResult>();
        int tokenIndex = startPosition;
        int selectorIndex = 0;

        while (selectorIndex < pattern.Length)
        {
            // Skip whitespace if configured
            while (skipWhitespace && tokenIndex < tokens.Length && tokens[tokenIndex] is WhitespaceToken)
            {
                matchedTokens.Add(tokens[tokenIndex]);
                tokenIndex++;
            }

            // Check if we ran out of tokens
            if (tokenIndex >= tokens.Length)
            {
                selectorResults.Add(new SelectorMatchResult
                {
                    Selector = pattern[selectorIndex],
                    ActualToken = null,
                    Matched = false,
                    FailureReason = "Ran out of tokens"
                });

                return new PatternMatchResult
                {
                    Result = selectorIndex == 0 ? MatchResult.NoMatch : MatchResult.PartialMatch,
                    SelectorResults = selectorResults.ToImmutable(),
                    MatchedTokens = matchedTokens.ToImmutable(),
                    TokensConsumed = tokenIndex - startPosition
                };
            }

            var currentToken = tokens[tokenIndex];
            var currentSelector = pattern[selectorIndex];
            bool matches = currentSelector.Matches(currentToken);

            selectorResults.Add(new SelectorMatchResult
            {
                Selector = currentSelector,
                ActualToken = currentToken,
                Matched = matches,
                FailureReason = matches ? null : GetFailureReason(currentSelector, currentToken)
            });

            if (!matches)
            {
                return new PatternMatchResult
                {
                    Result = selectorIndex == 0 ? MatchResult.NoMatch : MatchResult.PartialMatch,
                    SelectorResults = selectorResults.ToImmutable(),
                    MatchedTokens = matchedTokens.ToImmutable(),
                    TokensConsumed = tokenIndex - startPosition
                };
            }

            matchedTokens.Add(currentToken);
            tokenIndex++;
            selectorIndex++;
        }

        // All selectors matched!
        return new PatternMatchResult
        {
            Result = MatchResult.Success,
            SelectorResults = selectorResults.ToImmutable(),
            MatchedTokens = matchedTokens.ToImmutable(),
            TokensConsumed = tokenIndex - startPosition
        };
    }

    private static string GetFailureReason(TokenSelector selector, Token actualToken)
    {
        // Use the selector's Description and actual token type for failure message
        return selector switch
        {
            IdentSelector { ExactContent: not null } identSel => 
                $"Expected Ident '{identSel.ExactContent}', got '{actualToken.ContentSpan}'",
            IdentSelector => 
                $"Expected IdentToken, got {actualToken.GetType().Name}",
            SymbolSelector symbolSel when actualToken is SymbolToken symbolToken => 
                $"Expected symbol '{symbolSel.Symbol}', got '{symbolToken.Symbol}'",
            SymbolSelector symbolSel => 
                $"Expected SymbolToken '{symbolSel.Symbol}', got {actualToken.GetType().Name}",
            OperatorSelector { Operator: not null } opSel when actualToken is OperatorToken opToken => 
                $"Expected operator '{opSel.Operator}', got '{opToken.Operator}'",
            OperatorSelector { Operator: not null } opSel => 
                $"Expected OperatorToken '{opSel.Operator}', got {actualToken.GetType().Name}",
            OperatorSelector => 
                $"Expected OperatorToken, got {actualToken.GetType().Name}",
            SimpleBlockSelector { BlockOpener: not null } blockSel when actualToken is SimpleBlock block => 
                $"Expected block '{blockSel.BlockOpener.Value}', got '{block.OpeningDelimiter.FirstChar}'",
            SimpleBlockSelector { BlockOpener: not null } blockSel => 
                $"Expected SimpleBlock '{blockSel.BlockOpener.Value}', got {actualToken.GetType().Name}",
            SimpleBlockSelector => 
                $"Expected SimpleBlock, got {actualToken.GetType().Name}",
            _ => $"Expected {selector.Description}, got {actualToken.GetType().Name}"
        };
    }

    private static ReadOnlyMemory<char> CombineContent(ImmutableArray<Token> tokens)
    {
        if (tokens.IsEmpty)
            return ReadOnlyMemory<char>.Empty;

        if (tokens.Length == 1)
            return tokens[0].Content;

        // Calculate total length
        int totalLength = 0;
        foreach (var token in tokens)
        {
            totalLength += token.Content.Length;
        }

        // Build combined content
        var buffer = new char[totalLength];
        int offset = 0;
        foreach (var token in tokens)
        {
            token.Content.Span.CopyTo(buffer.AsSpan(offset));
            offset += token.Content.Length;
        }

        return buffer.AsMemory();
    }

    private readonly struct PatternMatchResult
    {
        public required MatchResult Result { get; init; }
        public required ImmutableArray<SelectorMatchResult> SelectorResults { get; init; }
        public required ImmutableArray<Token> MatchedTokens { get; init; }
        public required int TokensConsumed { get; init; }
    }
}
