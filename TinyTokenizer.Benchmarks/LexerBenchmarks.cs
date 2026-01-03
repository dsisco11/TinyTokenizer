using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for the Lexer (Level 1 tokenization).
/// Tests character classification and simple token generation across various input types.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class LexerBenchmarks
{
    #region Test Data

    /// <summary>
    /// Small input (~50 chars): typical single line of code.
    /// </summary>
    private const string SmallInput = """var result = Calculate(x, y) + offset * 2.5;""";

    /// <summary>
    /// Medium input (~1KB): representative C# method.
    /// </summary>
    private static readonly string MediumInput = GenerateMediumInput();

    /// <summary>
    /// Large input (~100KB): extensive code-like content.
    /// </summary>
    private static readonly string LargeInput = GenerateLargeInput();

    /// <summary>
    /// JSON-like input for variety.
    /// </summary>
    private static readonly string JsonInput = GenerateJsonInput();

    /// <summary>
    /// Whitespace-heavy input to test whitespace scanning.
    /// </summary>
    private static readonly string WhitespaceHeavyInput = GenerateWhitespaceHeavyInput();

    private static string GenerateMediumInput()
    {
        return """
            public static IEnumerable<Token> Tokenize(string input, TokenizerOptions options)
            {
                if (string.IsNullOrEmpty(input))
                    yield break;

                var lexer = new Lexer(options);
                var parser = new TokenParser(options);
                
                foreach (var token in parser.Parse(lexer.Lex(input)))
                {
                    if (token.Type != TokenType.Whitespace)
                    {
                        yield return token;
                    }
                }
            }

            public static async Task<List<Token>> TokenizeAsync(Stream stream, TokenizerOptions options)
            {
                var results = new List<Token>();
                await foreach (var token in stream.TokenizeAsync(options))
                {
                    results.Add(token);
                }
                return results;
            }

            private static bool IsValidIdentifier(ReadOnlySpan<char> span)
            {
                if (span.IsEmpty || !char.IsLetter(span[0]))
                    return false;

                for (int i = 1; i < span.Length; i++)
                {
                    if (!char.IsLetterOrDigit(span[i]) && span[i] != '_')
                        return false;
                }
                return true;
            }
            """;
    }

    private static string GenerateLargeInput()
    {
        var sb = new System.Text.StringBuilder(100_000);
        for (int i = 0; i < 1500; i++)
        {
            sb.AppendLine($"    public void Method{i}(int param{i}, string name{i})");
            sb.AppendLine("    {");
            sb.AppendLine($"        var result = Calculate(param{i}) + offset * {i}.5;");
            sb.AppendLine($"        Console.WriteLine(\"Processing: {{name{i}}}\");");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string GenerateJsonInput()
    {
        var sb = new System.Text.StringBuilder(10_000);
        sb.AppendLine("{");
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"  \"item{i}\": {{");
            sb.AppendLine($"    \"id\": {i},");
            sb.AppendLine($"    \"name\": \"Item {i}\",");
            sb.AppendLine($"    \"price\": {i * 1.5:F2},");
            sb.AppendLine($"    \"active\": {(i % 2 == 0 ? "true" : "false")}");
            sb.Append("  }");
            if (i < 199) sb.AppendLine(",");
            else sb.AppendLine();
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateWhitespaceHeavyInput()
    {
        var sb = new System.Text.StringBuilder(5_000);
        for (int i = 0; i < 100; i++)
        {
            sb.Append("    ");  // 4 spaces
            sb.Append("\t\t");  // 2 tabs
            sb.Append($"identifier{i}");
            sb.Append("     ");  // 5 spaces
            sb.AppendLine();
        }
        return sb.ToString();
    }

    #endregion

    #region Instances

    private Lexer _lexer = null!;

    private ReadOnlyMemory<char> _smallMemory;
    private ReadOnlyMemory<char> _mediumMemory;
    private ReadOnlyMemory<char> _largeMemory;
    private ReadOnlyMemory<char> _jsonMemory;
    private ReadOnlyMemory<char> _whitespaceMemory;

    [GlobalSetup]
    public void Setup()
    {
        _lexer = new Lexer();

        _smallMemory = SmallInput.AsMemory();
        _mediumMemory = MediumInput.AsMemory();
        _largeMemory = LargeInput.AsMemory();
        _jsonMemory = JsonInput.AsMemory();
        _whitespaceMemory = WhitespaceHeavyInput.AsMemory();
    }

    #endregion

    #region Small Input Benchmarks

    [Benchmark]
    [BenchmarkCategory("Small")]
    public int Lex_Small()
    {
        int count = 0;
        foreach (var _ in _lexer.Lex(_smallMemory))
            count++;
        return count;
    }

    #endregion

    #region Medium Input Benchmarks

    [Benchmark]
    [BenchmarkCategory("Medium")]
    public int Lex_Medium()
    {
        int count = 0;
        foreach (var _ in _lexer.Lex(_mediumMemory))
            count++;
        return count;
    }

    #endregion

    #region Large Input Benchmarks

    [Benchmark]
    [BenchmarkCategory("Large")]
    public int Lex_Large()
    {
        int count = 0;
        foreach (var _ in _lexer.Lex(_largeMemory))
            count++;
        return count;
    }

    #endregion

    #region JSON Input Benchmarks

    [Benchmark]
    [BenchmarkCategory("JSON")]
    public int Lex_Json()
    {
        int count = 0;
        foreach (var _ in _lexer.Lex(_jsonMemory))
            count++;
        return count;
    }

    #endregion

    #region Whitespace-Heavy Input Benchmarks

    [Benchmark]
    [BenchmarkCategory("Whitespace")]
    public int Lex_Whitespace()
    {
        int count = 0;
        foreach (var _ in _lexer.Lex(_whitespaceMemory))
            count++;
        return count;
    }

    #endregion

    #region LexToArray Benchmarks (measures full enumeration + allocation)

    [Benchmark]
    [BenchmarkCategory("ToArray")]
    public ImmutableArray<SimpleToken> LexToArray_Large()
    {
        return _lexer.LexToArray(_largeMemory);
    }

    #endregion
}
