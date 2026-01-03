using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for operator matching performance using OperatorTrie.
/// Measures how operator count affects matching performance.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class OperatorMatchingBenchmarks
{
    /// <summary>
    /// Number of operators in the configured set.
    /// Tests scaling behavior of OperatorTrie.
    /// </summary>
    [Params(10, 50, 100)]
    public int OperatorCount { get; set; }

    private TokenizerOptions _options = null!;
    private Lexer _lexer = null!;
    private TokenParser _parser = null!;
    private string _operatorHeavyInput = null!;
    private ReadOnlyMemory<char> _inputMemory;

    /// <summary>
    /// Base operators commonly used in programming languages.
    /// </summary>
    private static readonly string[] BaseOperators =
    [
        // Arithmetic
        "+", "-", "*", "/", "%", "**",
        // Comparison
        "==", "!=", "<", ">", "<=", ">=", "<=>",
        // Logical
        "&&", "||", "!", "and", "or", "not",
        // Bitwise
        "&", "|", "^", "~", "<<", ">>", ">>>",
        // Assignment
        "=", "+=", "-=", "*=", "/=", "%=",
        "&=", "|=", "^=", "<<=", ">>=",
        // Increment/Decrement
        "++", "--",
        // Member access
        ".", "->", "::", "?.", "!.",
        // Null-related
        "??", "??=", "?:",
        // Range and spread
        "..", "...", "..=",
        // Lambda and arrow
        "=>", "->",
        // Other
        "?", ":", ";", ",",
        // Additional operators for scaling
        "@", "#", "$", "@@", "##", "$$",
        "~>", "<~", "<->", "<=>",
        "|>", "<|", ">>>"
    ];

    /// <summary>
    /// Generates additional unique operators for scaling tests.
    /// </summary>
    private static IEnumerable<string> GenerateOperators(int count)
    {
        var operators = new List<string>(BaseOperators.Take(Math.Min(count, BaseOperators.Length)));
        
        // Generate additional operators if needed
        var suffixes = new[] { "+", "-", "*", "/", "=", "!", "?" };
        var prefixes = new[] { "@", "#", "$", "~", "^" };
        
        while (operators.Count < count)
        {
            foreach (var prefix in prefixes)
            {
                if (operators.Count >= count) break;
                foreach (var suffix in suffixes)
                {
                    if (operators.Count >= count) break;
                    var op = $"{prefix}{suffix}";
                    if (!operators.Contains(op))
                    {
                        operators.Add(op);
                    }
                }
            }
            
            // Add longer operators
            foreach (var prefix in prefixes)
            {
                if (operators.Count >= count) break;
                foreach (var suffix in suffixes)
                {
                    if (operators.Count >= count) break;
                    var op = $"{prefix}{prefix}{suffix}";
                    if (!operators.Contains(op))
                    {
                        operators.Add(op);
                    }
                }
            }
        }
        
        return operators.Take(count);
    }

    /// <summary>
    /// Generates input with heavy operator usage.
    /// </summary>
    private static string GenerateOperatorHeavyInput(IEnumerable<string> operators)
    {
        var operatorList = operators.ToList();
        var lines = new List<string>();
        
        // Generate expressions using the operators
        for (int i = 0; i < 100; i++)
        {
            var op = operatorList[i % operatorList.Count];
            lines.Add($"var result{i} = value{i} {op} other{i};");
        }
        
        // Generate chained expressions
        for (int i = 0; i < 50; i++)
        {
            var op1 = operatorList[i % operatorList.Count];
            var op2 = operatorList[(i + 1) % operatorList.Count];
            var op3 = operatorList[(i + 2) % operatorList.Count];
            lines.Add($"var chain{i} = a {op1} b {op2} c {op3} d;");
        }
        
        return string.Join("\n", lines);
    }

    [GlobalSetup]
    public void Setup()
    {
        var operators = GenerateOperators(OperatorCount).ToImmutableHashSet();
        _options = TokenizerOptions.Default.WithOperators(operators);
        _lexer = new Lexer(_options);
        _parser = new TokenParser(_options);
        _operatorHeavyInput = GenerateOperatorHeavyInput(operators);
        _inputMemory = _operatorHeavyInput.AsMemory();
    }

    #region Trie Building Benchmarks

    [Benchmark(Description = "Build OperatorTrie")]
    [BenchmarkCategory("Setup")]
    public TokenParser BuildTrie()
    {
        return new TokenParser(_options);
    }

    #endregion

    #region Operator Matching Benchmarks

    [Benchmark(Description = "Match Operators")]
    [BenchmarkCategory("Matching")]
    public ImmutableArray<Token> MatchOperators()
    {
        var simpleTokens = _lexer.Lex(_inputMemory);
        return _parser.ParseToArray(simpleTokens);
    }

    [Benchmark(Description = "Match Operators (Streaming)")]
    [BenchmarkCategory("Matching")]
    public int MatchOperatorsStreaming()
    {
        var count = 0;
        foreach (var token in _parser.Parse(_lexer.Lex(_inputMemory)))
        {
            if (token is OperatorToken)
            {
                count++;
            }
        }
        return count;
    }

    #endregion

    #region Operator Token Count Benchmarks

    [Benchmark(Description = "Filter Operator Tokens")]
    [BenchmarkCategory("Analysis")]
    public ImmutableArray<Token> FilterOperatorTokens()
    {
        return _parser.Parse(_lexer.Lex(_inputMemory))
            .Where(t => t is OperatorToken)
            .ToImmutableArray();
    }

    #endregion

    #region Comparison: Short vs Long Operators

    /// <summary>
    /// Input with mostly single-character operators.
    /// </summary>
    private static readonly string ShortOperatorInput = string.Join("\n",
        Enumerable.Range(0, 200).Select(i => $"var x{i} = a + b - c * d / e;"));

    /// <summary>
    /// Input with mostly multi-character operators.
    /// </summary>
    private static readonly string LongOperatorInput = string.Join("\n",
        Enumerable.Range(0, 200).Select(i => $"var x{i} = a === b !== c <<= d >>= e;"));

    private static readonly ReadOnlyMemory<char> ShortOperatorInputMemory = ShortOperatorInput.AsMemory();
    private static readonly ReadOnlyMemory<char> LongOperatorInputMemory = LongOperatorInput.AsMemory();

    private static readonly TokenizerOptions MixedOptions = TokenizerOptions.Default
        .WithOperators(CommonOperators.CFamily);

    private static readonly Lexer MixedLexer = new(MixedOptions);
    private static readonly TokenParser MixedParser = new(MixedOptions);

    [Benchmark(Description = "Short Operators (1-2 chars)")]
    [BenchmarkCategory("OperatorLength")]
    public ImmutableArray<Token> MatchShortOperators()
    {
        return MixedParser.ParseToArray(MixedLexer.Lex(ShortOperatorInputMemory));
    }

    [Benchmark(Description = "Long Operators (3+ chars)")]
    [BenchmarkCategory("OperatorLength")]
    public ImmutableArray<Token> MatchLongOperators()
    {
        return MixedParser.ParseToArray(MixedLexer.Lex(LongOperatorInputMemory));
    }

    #endregion
}
