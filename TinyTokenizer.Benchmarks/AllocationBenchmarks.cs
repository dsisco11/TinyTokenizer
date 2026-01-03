using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks focused on memory allocation patterns during parsing.
/// Measures allocation efficiency across different input sizes.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class AllocationBenchmarks
{
    #region Test Data

    /// <summary>
    /// Small input (~1 KB): typical code snippet.
    /// </summary>
    private static readonly string SmallInput = GenerateInput(1);

    /// <summary>
    /// Medium input (~100 KB): substantial source file.
    /// </summary>
    private static readonly string MediumInput = GenerateInput(100);

    /// <summary>
    /// Large input (~1 MB): very large source file.
    /// </summary>
    private static readonly string LargeInput = GenerateInput(1000);

    private static readonly ReadOnlyMemory<char> SmallInputMemory = SmallInput.AsMemory();
    private static readonly ReadOnlyMemory<char> MediumInputMemory = MediumInput.AsMemory();
    private static readonly ReadOnlyMemory<char> LargeInputMemory = LargeInput.AsMemory();

    private static readonly Lexer DefaultLexer = new();
    private static readonly TokenParser DefaultParser = new();

    private static readonly TokenizerOptions OptionsWithOperators = TokenizerOptions.Default
        .WithOperators(CommonOperators.CFamily);

    private static readonly Lexer LexerWithOperators = new(OptionsWithOperators);
    private static readonly TokenParser ParserWithOperators = new(OptionsWithOperators);

    /// <summary>
    /// Generates input of approximately the specified size in KB.
    /// Creates realistic code-like content with varied token types.
    /// </summary>
    private static string GenerateInput(int targetKilobytes)
    {
        var template = """
            function processData(input, options) {
                // Comment explaining the function
                var result = { items: [], count: 0 };
                
                /* Multi-line comment
                   with details */
                for (var i = 0; i < input.length; i++) {
                    var item = input[i];
                    if (item.type === "string") {
                        result.items.push({
                            value: item.value,
                            index: i,
                            processed: true
                        });
                    } else if (item.type === "number") {
                        result.count += parseFloat(item.value);
                    }
                    
                    // Handle special cases
                    switch (item.category) {
                        case "alpha":
                            handleAlpha(item);
                            break;
                        case "beta":
                            handleBeta(item);
                            break;
                        default:
                            handleDefault(item);
                    }
                }
                
                return {
                    data: result,
                    total: input.length,
                    timestamp: Date.now(),
                    valid: result.count >= 0 && result.items.length > 0
                };
            }

            """;

        var targetBytes = targetKilobytes * 1024;
        var repetitions = Math.Max(1, targetBytes / template.Length);
        
        return string.Concat(Enumerable.Range(0, repetitions).Select(i => 
            template.Replace("processData", $"processData{i}")));
    }

    #endregion

    #region Lexer Allocation Benchmarks

    [Benchmark(Description = "Lex 1KB")]
    [BenchmarkCategory("Lexer", "Small")]
    public ImmutableArray<SimpleToken> LexSmall()
    {
        return DefaultLexer.LexToArray(SmallInputMemory);
    }

    [Benchmark(Description = "Lex 100KB")]
    [BenchmarkCategory("Lexer", "Medium")]
    public ImmutableArray<SimpleToken> LexMedium()
    {
        return DefaultLexer.LexToArray(MediumInputMemory);
    }

    [Benchmark(Description = "Lex 1MB")]
    [BenchmarkCategory("Lexer", "Large")]
    public ImmutableArray<SimpleToken> LexLarge()
    {
        return DefaultLexer.LexToArray(LargeInputMemory);
    }

    #endregion

    #region Parser Allocation Benchmarks

    [Benchmark(Description = "Parse 1KB")]
    [BenchmarkCategory("Parser", "Small")]
    public ImmutableArray<Token> ParseSmall()
    {
        var simpleTokens = DefaultLexer.Lex(SmallInputMemory);
        return DefaultParser.ParseToArray(simpleTokens);
    }

    [Benchmark(Description = "Parse 100KB")]
    [BenchmarkCategory("Parser", "Medium")]
    public ImmutableArray<Token> ParseMedium()
    {
        var simpleTokens = DefaultLexer.Lex(MediumInputMemory);
        return DefaultParser.ParseToArray(simpleTokens);
    }

    [Benchmark(Description = "Parse 1MB")]
    [BenchmarkCategory("Parser", "Large")]
    public ImmutableArray<Token> ParseLarge()
    {
        var simpleTokens = DefaultLexer.Lex(LargeInputMemory);
        return DefaultParser.ParseToArray(simpleTokens);
    }

    #endregion

    #region Full Pipeline Allocation Benchmarks

    [Benchmark(Description = "Full Pipeline 1KB")]
    [BenchmarkCategory("Pipeline", "Small")]
    public ImmutableArray<Token> FullPipelineSmall()
    {
        var simpleTokens = LexerWithOperators.Lex(SmallInputMemory);
        return ParserWithOperators.ParseToArray(simpleTokens);
    }

    [Benchmark(Description = "Full Pipeline 100KB")]
    [BenchmarkCategory("Pipeline", "Medium")]
    public ImmutableArray<Token> FullPipelineMedium()
    {
        var simpleTokens = LexerWithOperators.Lex(MediumInputMemory);
        return ParserWithOperators.ParseToArray(simpleTokens);
    }

    [Benchmark(Description = "Full Pipeline 1MB")]
    [BenchmarkCategory("Pipeline", "Large")]
    public ImmutableArray<Token> FullPipelineLarge()
    {
        var simpleTokens = LexerWithOperators.Lex(LargeInputMemory);
        return ParserWithOperators.ParseToArray(simpleTokens);
    }

    #endregion

    #region Streaming vs Array Comparison

    [Benchmark(Description = "Streaming Parse 1KB")]
    [BenchmarkCategory("Streaming", "Small")]
    public int StreamingParseSmall()
    {
        var count = 0;
        foreach (var token in DefaultParser.Parse(DefaultLexer.Lex(SmallInputMemory)))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Streaming Parse 100KB")]
    [BenchmarkCategory("Streaming", "Medium")]
    public int StreamingParseMedium()
    {
        var count = 0;
        foreach (var token in DefaultParser.Parse(DefaultLexer.Lex(MediumInputMemory)))
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "Streaming Parse 1MB")]
    [BenchmarkCategory("Streaming", "Large")]
    public int StreamingParseLarge()
    {
        var count = 0;
        foreach (var token in DefaultParser.Parse(DefaultLexer.Lex(LargeInputMemory)))
        {
            count++;
        }
        return count;
    }

    #endregion
}
