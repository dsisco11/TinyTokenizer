using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for TokenParser parsing performance.
/// Measures allocation and throughput after ArrayPoolBufferWriter optimization.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TokenParserBenchmarks
{
    #region Test Data

    /// <summary>
    /// Small input with strings and blocks (~100 chars).
    /// </summary>
    private const string SmallInput = """
        func("hello", 'x') { a = 1 + 2; b = 3.14; }
        """;

    /// <summary>
    /// Medium input with various token types (~2KB).
    /// </summary>
    private static readonly string MediumInput = GenerateMediumInput();

    /// <summary>
    /// Large input with extensive parsing (~50KB).
    /// </summary>
    private static readonly string LargeInput = GenerateLargeInput();

    /// <summary>
    /// Comment-heavy input to test comment parsing.
    /// </summary>
    private static readonly string CommentHeavyInput = GenerateCommentHeavyInput();

    /// <summary>
    /// String-heavy input to test string parsing.
    /// </summary>
    private static readonly string StringHeavyInput = GenerateStringHeavyInput();

    /// <summary>
    /// Nested blocks input to test block parsing.
    /// </summary>
    private static readonly string NestedBlocksInput = GenerateNestedBlocksInput();

    /// <summary>
    /// Operator-heavy input to test operator matching.
    /// </summary>
    private static readonly string OperatorHeavyInput = GenerateOperatorHeavyInput();

    private static string GenerateMediumInput()
    {
        return """
            function parseData(input, options) {
                // Single-line comment
                var result = {};
                
                /* Multi-line
                   comment */
                for (var i = 0; i < input.length; i++) {
                    var item = input[i];
                    if (item.type === "string") {
                        result.strings = result.strings || [];
                        result.strings.push(item.value);
                    } else if (item.type === "number") {
                        result.numbers = result.numbers || [];
                        result.numbers.push(parseFloat(item.value));
                    }
                }
                
                return {
                    data: result,
                    count: input.length,
                    processed: true,
                    timestamp: Date.now()
                };
            }
            
            class DataProcessor {
                constructor(config) {
                    this.config = config;
                    this.cache = new Map();
                }
                
                process(data) {
                    var key = JSON.stringify(data);
                    if (this.cache.has(key)) {
                        return this.cache.get(key);
                    }
                    
                    var result = this.transform(data);
                    this.cache.set(key, result);
                    return result;
                }
                
                transform(data) {
                    return data.map(item => ({
                        id: item.id,
                        name: item.name.toUpperCase(),
                        value: item.value * 2
                    }));
                }
            }
            """;
    }

    private static string GenerateLargeInput()
    {
        var sb = new System.Text.StringBuilder(50_000);
        for (int i = 0; i < 500; i++)
        {
            sb.AppendLine($"function process{i}(data, options) {{");
            sb.AppendLine($"    // Process item {i}");
            sb.AppendLine($"    var result = calculate(data.value{i}, options.factor);");
            sb.AppendLine($"    if (result > {i * 10}) {{");
            sb.AppendLine($"        return {{ success: true, value: result, id: \"{i}\" }};");
            sb.AppendLine("    }");
            sb.AppendLine($"    /* Fallback for item {i} */");
            sb.AppendLine($"    return {{ success: false, error: \"Below threshold\" }};");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateCommentHeavyInput()
    {
        var sb = new System.Text.StringBuilder(10_000);
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"// Comment line {i}: This is a single-line comment");
            sb.AppendLine($"var x{i} = {i};");
            sb.AppendLine($"/* Multi-line comment {i}");
            sb.AppendLine($"   with multiple lines");
            sb.AppendLine($"   explaining the code */");
            sb.AppendLine($"var y{i} = x{i} + 1;");
        }
        return sb.ToString();
    }

    private static string GenerateStringHeavyInput()
    {
        var sb = new System.Text.StringBuilder(10_000);
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"var str{i} = \"String value {i} with some content\";");
            sb.AppendLine($"var char{i} = 'A';");
            sb.AppendLine($"var escaped{i} = \"Line with \\\"escaped\\\" quotes and \\n newlines\";");
            sb.AppendLine($"var mixed{i} = concat(str{i}, 'B', \"suffix\");");
        }
        return sb.ToString();
    }

    private static string GenerateNestedBlocksInput()
    {
        var sb = new System.Text.StringBuilder(10_000);
        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($"function nested{i}() {{");
            sb.AppendLine($"    var arr = [1, 2, [3, 4, [5, 6]]];");
            sb.AppendLine($"    var obj = {{ a: {{ b: {{ c: {i} }} }} }};");
            sb.AppendLine($"    call(fn(x, y), (a, b) => {{ return a + b; }});");
            sb.AppendLine($"    return [obj, arr, (x) => x * 2];");
            sb.AppendLine("}");
        }
        return sb.ToString();
    }

    private static string GenerateOperatorHeavyInput()
    {
        var sb = new System.Text.StringBuilder(10_000);
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"var a{i} = x{i} + y{i} - z{i} * w{i} / v{i};");
            sb.AppendLine($"var b{i} = a{i} >= 0 && a{i} <= 100 || a{i} == -1;");
            sb.AppendLine($"var c{i} = b{i} ? a{i}++ : a{i}--;");
            sb.AppendLine($"var d{i} = ~a{i} | b{i} & c{i} ^ {i};");
            sb.AppendLine($"var e{i} = a{i} << 2 >> 1 >>> 0;");
            sb.AppendLine($"a{i} += b{i}; c{i} -= d{i}; e{i} *= 2;");
        }
        return sb.ToString();
    }

    #endregion

    #region Shared State

    private Lexer _lexer = null!;
    private TokenParser _parser = null!;
    private TokenizerOptions _options = null!;

    // Pre-lexed tokens for isolating parser performance
    private ImmutableArray<SimpleToken> _smallTokens;
    private ImmutableArray<SimpleToken> _mediumTokens;
    private ImmutableArray<SimpleToken> _largeTokens;
    private ImmutableArray<SimpleToken> _commentTokens;
    private ImmutableArray<SimpleToken> _stringTokens;
    private ImmutableArray<SimpleToken> _blockTokens;
    private ImmutableArray<SimpleToken> _operatorTokens;

    [GlobalSetup]
    public void Setup()
    {
        _options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);
        _lexer = new Lexer(_options);
        _parser = new TokenParser(_options);

        // Pre-lex inputs for parser-only benchmarks
        _smallTokens = _lexer.LexToArray(SmallInput);
        _mediumTokens = _lexer.LexToArray(MediumInput);
        _largeTokens = _lexer.LexToArray(LargeInput);
        _commentTokens = _lexer.LexToArray(CommentHeavyInput);
        _stringTokens = _lexer.LexToArray(StringHeavyInput);
        _blockTokens = _lexer.LexToArray(NestedBlocksInput);
        _operatorTokens = _lexer.LexToArray(OperatorHeavyInput);
    }

    #endregion

    #region Full Pipeline Benchmarks (Lex + Parse)

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FullPipeline")]
    public int ParseSmall()
    {
        var tokens = _parser.ParseToArray(_lexer.Lex(SmallInput));
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("FullPipeline")]
    public int ParseMedium()
    {
        var tokens = _parser.ParseToArray(_lexer.Lex(MediumInput));
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("FullPipeline")]
    public int ParseLarge()
    {
        var tokens = _parser.ParseToArray(_lexer.Lex(LargeInput));
        return tokens.Length;
    }

    #endregion

    #region Parser-Only Benchmarks (Pre-lexed input)

    [Benchmark]
    [BenchmarkCategory("ParserOnly")]
    public int ParserOnly_Small()
    {
        var tokens = _parser.ParseToArray(_smallTokens);
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("ParserOnly")]
    public int ParserOnly_Medium()
    {
        var tokens = _parser.ParseToArray(_mediumTokens);
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("ParserOnly")]
    public int ParserOnly_Large()
    {
        var tokens = _parser.ParseToArray(_largeTokens);
        return tokens.Length;
    }

    #endregion

    #region Feature-Specific Benchmarks

    [Benchmark]
    [BenchmarkCategory("Comments")]
    public int ParseComments()
    {
        var tokens = _parser.ParseToArray(_commentTokens);
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Strings")]
    public int ParseStrings()
    {
        var tokens = _parser.ParseToArray(_stringTokens);
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Blocks")]
    public int ParseBlocks()
    {
        var tokens = _parser.ParseToArray(_blockTokens);
        return tokens.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Operators")]
    public int ParseOperators()
    {
        var tokens = _parser.ParseToArray(_operatorTokens);
        return tokens.Length;
    }

    #endregion
}
