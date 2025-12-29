using System.Collections.Immutable;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the PatternMatcher system (Level 3 tokenization).
/// </summary>
public class PatternMatcherTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string input)
    {
        var tokenizer = new Tokenizer(input.AsMemory(), TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@', '$'));
        return tokenizer.Tokenize();
    }

    private static ImmutableArray<Token> TokenizeTwoLevel(string input)
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@', '$');
        var lexer = new Lexer(options);
        var parser = new TokenParser(options);
        return parser.ParseToArray(lexer.Lex(input));
    }

    #endregion

    #region FunctionCall Pattern Tests

    [Fact]
    public void FunctionCall_SimpleCall_MatchesPattern()
    {
        var tokens = Tokenize("func()");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        var funcCall = Assert.IsType<FunctionCallToken>(result[0]);
        Assert.Equal("func", funcCall.FunctionNameSpan.ToString());
        Assert.Equal("FunctionCall", funcCall.PatternName);
    }

    [Fact]
    public void FunctionCall_WithArguments_MatchesPattern()
    {
        var tokens = Tokenize("print(x, y)");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        var funcCall = Assert.IsType<FunctionCallToken>(result[0]);
        Assert.Equal("print", funcCall.FunctionNameSpan.ToString());
        Assert.Equal("print(x, y)", funcCall.ContentSpan.ToString());
    }

    [Fact]
    public void FunctionCall_WithWhitespaceBetween_MatchesPattern()
    {
        var tokens = Tokenize("func ()");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        // Should match because SkipWhitespace is true by default
        Assert.Single(result);
        var funcCall = Assert.IsType<FunctionCallToken>(result[0]);
        Assert.Equal("func", funcCall.FunctionNameSpan.ToString());
    }

    [Fact]
    public void FunctionCall_MultipleCalls_MatchesAll()
    {
        var tokens = Tokenize("foo()bar()");  // No whitespace between calls
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Equal(2, result.Length);
        var funcCall1 = Assert.IsType<FunctionCallToken>(result[0]);
        var funcCall2 = Assert.IsType<FunctionCallToken>(result[1]);
        Assert.Equal("foo", funcCall1.FunctionNameSpan.ToString());
        Assert.Equal("bar", funcCall2.FunctionNameSpan.ToString());
    }

    [Fact]
    public void FunctionCall_NestedCalls_MatchesOuter()
    {
        var tokens = Tokenize("outer(inner())");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        var funcCall = Assert.IsType<FunctionCallToken>(result[0]);
        Assert.Equal("outer", funcCall.FunctionNameSpan.ToString());
    }

    [Fact]
    public void FunctionCall_NoMatch_LeavesTokensUnchanged()
    {
        var tokens = Tokenize("identifier");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        Assert.IsType<IdentToken>(result[0]);
    }

    #endregion

    #region PropertyAccess Pattern Tests

    [Fact]
    public void PropertyAccess_SimpleAccess_MatchesPattern()
    {
        var tokens = TokenizeTwoLevel("obj.property");
        var result = tokens.ApplyPatterns(TokenDefinitions.PropertyAccess());

        Assert.Single(result);
        var propAccess = Assert.IsType<PropertyAccessToken>(result[0]);
        Assert.Equal("obj", propAccess.TargetSpan.ToString());
        Assert.Equal("property", propAccess.MemberSpan.ToString());
    }

    [Fact]
    public void PropertyAccess_ChainedAccess_MatchesFirst()
    {
        var tokens = TokenizeTwoLevel("a.b.c");
        var result = tokens.ApplyPatterns(TokenDefinitions.PropertyAccess());

        // First pass matches "a.b", leaves ".c"
        Assert.Equal(3, result.Length);
        var propAccess = Assert.IsType<PropertyAccessToken>(result[0]);
        Assert.Equal("a", propAccess.TargetSpan.ToString());
        Assert.Equal("b", propAccess.MemberSpan.ToString());
    }

    [Fact]
    public void PropertyAccess_WithWhitespace_MatchesPattern()
    {
        var tokens = TokenizeTwoLevel("obj . member");
        var result = tokens.ApplyPatterns(TokenDefinitions.PropertyAccess());

        Assert.Single(result);
        var propAccess = Assert.IsType<PropertyAccessToken>(result[0]);
        Assert.Equal("obj", propAccess.TargetSpan.ToString());
        Assert.Equal("member", propAccess.MemberSpan.ToString());
    }

    #endregion

    #region TypeAnnotation Pattern Tests

    [Fact]
    public void TypeAnnotation_SimpleAnnotation_MatchesPattern()
    {
        var tokens = TokenizeTwoLevel("param: string");
        var result = tokens.ApplyPatterns(TokenDefinitions.TypeAnnotation());

        Assert.Single(result);
        var typeAnnotation = Assert.IsType<TypeAnnotationToken>(result[0]);
        Assert.Equal("param", typeAnnotation.NameSpan.ToString());
        Assert.Equal("string", typeAnnotation.TypeNameSpan.ToString());
    }

    [Fact]
    public void TypeAnnotation_MultipleAnnotations_MatchesAll()
    {
        var tokens = TokenizeTwoLevel("x: int, y: float");
        var result = tokens.ApplyPatterns(TokenDefinitions.TypeAnnotation());

        // x: int, [comma] [space] y: float
        var typeAnnotations = result.OfType<TypeAnnotationToken>().ToList();
        Assert.Equal(2, typeAnnotations.Count);
        Assert.Equal("x", typeAnnotations[0].NameSpan.ToString());
        Assert.Equal("y", typeAnnotations[1].NameSpan.ToString());
    }

    #endregion

    #region Assignment Pattern Tests

    [Fact]
    public void Assignment_SimpleAssignment_MatchesPattern()
    {
        var tokens = TokenizeTwoLevel("x = 5");
        var result = tokens.ApplyPatterns(TokenDefinitions.Assignment());

        // Assignment pattern matches ident + op + any (3 tokens)
        // Remaining tokens are not consumed
        var assignments = result.OfType<AssignmentToken>().ToList();
        Assert.Single(assignments);
        Assert.Equal("x", assignments[0].TargetSpan.ToString());
    }

    [Fact]
    public void Assignment_IdentifierValue_MatchesPattern()
    {
        var tokens = TokenizeTwoLevel("result = value");
        var result = tokens.ApplyPatterns(TokenDefinitions.Assignment());

        Assert.Single(result);
        var assignment = Assert.IsType<AssignmentToken>(result[0]);
        Assert.Equal("result", assignment.TargetSpan.ToString());
    }

    #endregion

    #region ArrayAccess Pattern Tests

    [Fact]
    public void ArrayAccess_SimpleAccess_MatchesPattern()
    {
        var tokens = Tokenize("arr[0]");
        var result = tokens.ApplyPatterns(TokenDefinitions.ArrayAccess());

        Assert.Single(result);
        var arrayAccess = Assert.IsType<ArrayAccessToken>(result[0]);
        Assert.Equal("arr", arrayAccess.TargetSpan.ToString());
        Assert.NotNull(arrayAccess.IndexBlock);
        Assert.Equal("[0]", arrayAccess.IndexBlock!.ContentSpan.ToString());
    }

    [Fact]
    public void ArrayAccess_WithExpression_MatchesPattern()
    {
        var tokens = Tokenize("matrix[i + 1]");
        var result = tokens.ApplyPatterns(TokenDefinitions.ArrayAccess());

        Assert.Single(result);
        var arrayAccess = Assert.IsType<ArrayAccessToken>(result[0]);
        Assert.Equal("matrix", arrayAccess.TargetSpan.ToString());
    }

    #endregion

    #region Multiple Pattern Tests

    [Fact]
    public void MultiplePatterns_FunctionAndProperty_BothMatch()
    {
        var tokens = TokenizeTwoLevel("obj.method()");
        var result = tokens.ApplyPatterns(
            TokenDefinitions.PropertyAccess(),
            TokenDefinitions.FunctionCall());

        // PropertyAccess matches first (obj.method), then FunctionCall cannot match
        // because PropertyAccess already consumed "obj.method"
        Assert.Equal(2, result.Length);
        Assert.IsType<PropertyAccessToken>(result[0]);
        Assert.IsType<SimpleBlock>(result[1]); // The () block remains
    }

    [Fact]
    public void MultiplePatterns_Priority_HigherPriorityFirst()
    {
        // Create custom definitions with different priorities
        var lowPriority = new TokenDefinition<FunctionCallToken>
        {
            Name = "LowPriorityFunc",
            Patterns = [[Match.Ident(), Match.Block('(')]],
            Priority = 0
        };

        var highPriority = new TokenDefinition<FunctionCallToken>
        {
            Name = "HighPriorityFunc",
            Patterns = [[Match.Ident(), Match.Block('(')]],
            Priority = 10
        };

        var tokens = Tokenize("func()");
        var result = tokens.ApplyPatterns(lowPriority, highPriority);

        Assert.Single(result);
        var funcCall = Assert.IsType<FunctionCallToken>(result[0]);
        Assert.Equal("HighPriorityFunc", funcCall.PatternName);
    }

    [Fact]
    public void MultiplePatterns_AlternativePatterns_MatchesFirst()
    {
        // Pattern with alternatives (OR logic)
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "FunctionOrMethod",
            Patterns = 
            [
                [Match.Ident(), Match.Block('(')],  // func()
                [Match.Ident(), Match.Symbol('.'), Match.Ident(), Match.Block('(')] // obj.method()
            ]
        };

        var tokens = Tokenize("func()");
        var result = tokens.ApplyPatterns(definition);

        Assert.Single(result);
        Assert.IsType<FunctionCallToken>(result[0]);
    }

    #endregion

    #region Recursive Block Processing Tests

    [Fact]
    public void RecursiveProcessing_NestedBlocks_AppliesPatterns()
    {
        var tokens = Tokenize("{ func() }");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        var block = Assert.IsType<SimpleBlock>(result[0]);
        
        // Inside the block, func() should be matched
        var children = block.Children.Where(t => t is not WhitespaceToken).ToList();
        Assert.Single(children);
        Assert.IsType<FunctionCallToken>(children[0]);
    }

    [Fact]
    public void RecursiveProcessing_DeeplyNested_AppliesPatterns()
    {
        var tokens = Tokenize("{ [ func() ] }");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        var braceBlock = Assert.IsType<SimpleBlock>(result[0]);
        
        var bracketBlock = braceBlock.Children
            .Where(t => t is SimpleBlock)
            .Cast<SimpleBlock>()
            .First();
        
        var funcCall = bracketBlock.Children
            .Where(t => t is FunctionCallToken)
            .First();
        
        Assert.IsType<FunctionCallToken>(funcCall);
    }

    #endregion

    #region Diagnostics Tests

    [Fact]
    public void ApplyWithDiagnostics_ReturnsMatchAttempts()
    {
        var matcher = new PatternMatcher([TokenDefinitions.FunctionCall()], enableDiagnostics: true);
        var tokens = Tokenize("func()");
        
        var report = matcher.ApplyWithDiagnostics(tokens);

        Assert.NotEmpty(report.Attempts);
        Assert.Contains(report.Attempts, a => a.Result == MatchResult.Success);
        Assert.Equal("FunctionCall", report.Attempts.First(a => a.Result == MatchResult.Success).PatternName);
    }

    [Fact]
    public void ApplyWithDiagnostics_FailedMatch_RecordsReason()
    {
        var matcher = new PatternMatcher([TokenDefinitions.FunctionCall()], enableDiagnostics: true);
        var tokens = Tokenize("identifier");
        
        var report = matcher.ApplyWithDiagnostics(tokens);

        Assert.NotEmpty(report.Attempts);
        // When first selector (Ident) matches but second (Block) doesn't exist,
        // it's a PartialMatch, not NoMatch
        Assert.Contains(report.Attempts, a => a.Result == MatchResult.PartialMatch);
    }

    [Fact]
    public void ApplyWithDiagnostics_PartialMatch_RecordsAttempt()
    {
        var matcher = new PatternMatcher([TokenDefinitions.PropertyAccess()], enableDiagnostics: true);
        var tokens = TokenizeTwoLevel("obj.");  // Incomplete property access
        
        var report = matcher.ApplyWithDiagnostics(tokens);

        Assert.NotEmpty(report.Attempts);
        Assert.Contains(report.Attempts, a => a.Result == MatchResult.PartialMatch);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyTokens_ReturnsEmpty()
    {
        var tokens = ImmutableArray<Token>.Empty;
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Empty(result);
    }

    [Fact]
    public void NoDefinitions_ReturnsOriginalTokens()
    {
        var tokens = Tokenize("func()");
        var result = tokens.ApplyPatterns();

        Assert.Equal(tokens.Length, result.Length);
        Assert.IsType<IdentToken>(result[0]);
    }

    [Fact]
    public void WhitespaceSkipping_Disabled_RequiresExactMatch()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "ExactFunctionCall",
            Patterns = [[Match.Ident(), Match.Block('(')]],
            SkipWhitespace = false  // Disable whitespace skipping
        };

        var tokens = Tokenize("func ()");  // Whitespace between ident and paren
        var result = tokens.ApplyPatterns(definition);

        // Should NOT match because whitespace is not skipped
        Assert.Equal(3, result.Length);  // ident + whitespace + block
        Assert.IsType<IdentToken>(result[0]);
        Assert.IsType<WhitespaceToken>(result[1]);
        Assert.IsType<SimpleBlock>(result[2]);
    }

    [Fact]
    public void PatternMatcher_PreservesTokenPositions()
    {
        var tokens = Tokenize("func()");
        var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

        Assert.Single(result);
        var funcCall = Assert.IsType<FunctionCallToken>(result[0]);
        Assert.Equal(0, funcCall.Position);
    }

    #endregion

    #region Custom TokenSelector Tests

    [Fact]
    public void CustomSelector_AnyOf_MatchesAlternatives()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "MultiBlockCall",
            Patterns = [[Match.Ident(), Match.AnyOf(Match.Block('('), Match.Block('['))]]
        };

        var tokens1 = Tokenize("func()");
        var result1 = tokens1.ApplyPatterns(definition);
        Assert.Single(result1);
        Assert.IsType<FunctionCallToken>(result1[0]);

        var tokens2 = Tokenize("arr[]");
        var result2 = tokens2.ApplyPatterns(definition);
        Assert.Single(result2);
        Assert.IsType<FunctionCallToken>(result2[0]);
    }

    [Fact]
    public void CustomSelector_ContentStartsWith_Matches()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "PrefixedCall",
            Patterns = [[Match.ContentStartsWith("get"), Match.Block('(')]]
        };

        var tokens = Tokenize("getValue()");
        var result = tokens.ApplyPatterns(definition);

        Assert.Single(result);
        Assert.IsType<FunctionCallToken>(result[0]);
    }

    [Fact]
    public void CustomSelector_ContentEndsWith_Matches()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "SuffixedCall",
            Patterns = [[Match.ContentEndsWith("Async"), Match.Block('(')]]
        };

        var tokens = Tokenize("loadDataAsync()");
        var result = tokens.ApplyPatterns(definition);

        Assert.Single(result);
        Assert.IsType<FunctionCallToken>(result[0]);
    }

    [Fact]
    public void CustomSelector_ContentContains_Matches()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "ContainsCall",
            Patterns = [[Match.ContentContains("Data"), Match.Block('(')]]
        };

        var tokens = Tokenize("processDataHandler()");
        var result = tokens.ApplyPatterns(definition);

        Assert.Single(result);
        Assert.IsType<FunctionCallToken>(result[0]);
    }

    [Fact]
    public void CustomSelector_ContentPredicate_Matches()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "PredicateCall",
            Patterns = [[Match.ContentMatches(c => c.Length > 5), Match.Block('(')]]
        };

        var tokens = Tokenize("longFunction()");
        var result = tokens.ApplyPatterns(definition);

        Assert.Single(result);
        Assert.IsType<FunctionCallToken>(result[0]);

        var tokensShort = Tokenize("fn()");
        var resultShort = tokensShort.ApplyPatterns(definition);

        // Should NOT match because "fn" is only 2 chars
        Assert.Equal(2, resultShort.Length);
        Assert.IsType<IdentToken>(resultShort[0]);
    }

    [Fact]
    public void CustomSelector_ExactIdent_Matches()
    {
        var definition = new TokenDefinition<FunctionCallToken>
        {
            Name = "SpecificCall",
            Patterns = [[Match.Ident("main"), Match.Block('(')]]
        };

        var tokensMatch = Tokenize("main()");
        var resultMatch = tokensMatch.ApplyPatterns(definition);
        Assert.Single(resultMatch);
        Assert.IsType<FunctionCallToken>(resultMatch[0]);

        var tokensNoMatch = Tokenize("other()");
        var resultNoMatch = tokensNoMatch.ApplyPatterns(definition);
        Assert.Equal(2, resultNoMatch.Length);
        Assert.IsType<IdentToken>(resultNoMatch[0]);
    }

    #endregion

    #region Complex Expression Tests

    [Fact]
    public void ComplexExpression_MixedPatterns_MatchesCorrectly()
    {
        var tokens = TokenizeTwoLevel("x = func()");
        var result = tokens.ApplyPatterns(
            TokenDefinitions.Assignment(),
            TokenDefinitions.FunctionCall());

        // Assignment matches first: x = func
        // Wait, this only matches x = func (ident = any)
        // The () is left over
        Assert.Equal(2, result.Length);
        Assert.IsType<AssignmentToken>(result[0]);
        Assert.IsType<SimpleBlock>(result[1]);
    }

    [Fact]
    public void ComplexExpression_MethodChain_PartialMatch()
    {
        var tokens = TokenizeTwoLevel("a.b().c");
        var result = tokens.ApplyPatterns(
            TokenDefinitions.PropertyAccess(),
            TokenDefinitions.FunctionCall());

        // Pattern matching is greedy, left-to-right
        // PropertyAccess matches a.b first
        var propAccess = Assert.IsType<PropertyAccessToken>(result[0]);
        Assert.Equal("a", propAccess.TargetSpan.ToString());
    }

    #endregion
}
