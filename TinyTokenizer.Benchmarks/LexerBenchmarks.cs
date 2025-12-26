using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for comparing the optimized Lexer (SearchValues) against
/// a baseline implementation using ImmutableHashSet.
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

    private Lexer _optimizedLexer = null!;
    private BaselineLexer _baselineLexer = null!;

    private ReadOnlyMemory<char> _smallMemory;
    private ReadOnlyMemory<char> _mediumMemory;
    private ReadOnlyMemory<char> _largeMemory;
    private ReadOnlyMemory<char> _jsonMemory;
    private ReadOnlyMemory<char> _whitespaceMemory;

    [GlobalSetup]
    public void Setup()
    {
        _optimizedLexer = new Lexer();
        _baselineLexer = new BaselineLexer();

        _smallMemory = SmallInput.AsMemory();
        _mediumMemory = MediumInput.AsMemory();
        _largeMemory = LargeInput.AsMemory();
        _jsonMemory = JsonInput.AsMemory();
        _whitespaceMemory = WhitespaceHeavyInput.AsMemory();
    }

    #endregion

    #region Small Input Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Small")]
    public int Baseline_Small()
    {
        int count = 0;
        foreach (var _ in _baselineLexer.Lex(_smallMemory))
            count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Small")]
    public int Optimized_Small()
    {
        int count = 0;
        foreach (var _ in _optimizedLexer.Lex(_smallMemory))
            count++;
        return count;
    }

    #endregion

    #region Medium Input Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Medium")]
    public int Baseline_Medium()
    {
        int count = 0;
        foreach (var _ in _baselineLexer.Lex(_mediumMemory))
            count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Medium")]
    public int Optimized_Medium()
    {
        int count = 0;
        foreach (var _ in _optimizedLexer.Lex(_mediumMemory))
            count++;
        return count;
    }

    #endregion

    #region Large Input Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Large")]
    public int Baseline_Large()
    {
        int count = 0;
        foreach (var _ in _baselineLexer.Lex(_largeMemory))
            count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Large")]
    public int Optimized_Large()
    {
        int count = 0;
        foreach (var _ in _optimizedLexer.Lex(_largeMemory))
            count++;
        return count;
    }

    #endregion

    #region JSON Input Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("JSON")]
    public int Baseline_Json()
    {
        int count = 0;
        foreach (var _ in _baselineLexer.Lex(_jsonMemory))
            count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("JSON")]
    public int Optimized_Json()
    {
        int count = 0;
        foreach (var _ in _optimizedLexer.Lex(_jsonMemory))
            count++;
        return count;
    }

    #endregion

    #region Whitespace-Heavy Input Benchmarks

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Whitespace")]
    public int Baseline_Whitespace()
    {
        int count = 0;
        foreach (var _ in _baselineLexer.Lex(_whitespaceMemory))
            count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Whitespace")]
    public int Optimized_Whitespace()
    {
        int count = 0;
        foreach (var _ in _optimizedLexer.Lex(_whitespaceMemory))
            count++;
        return count;
    }

    #endregion

    #region LexToArray Benchmarks (measures full enumeration + allocation)

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ToArray")]
    public ImmutableArray<SimpleToken> Baseline_ToArray_Large()
    {
        return _baselineLexer.LexToArray(_largeMemory);
    }

    [Benchmark]
    [BenchmarkCategory("ToArray")]
    public ImmutableArray<SimpleToken> Optimized_ToArray_Large()
    {
        return _optimizedLexer.LexToArray(_largeMemory);
    }

    #endregion
}

#region Baseline Lexer (Original Implementation)

/// <summary>
/// The original Lexer implementation using ImmutableHashSet for comparison.
/// </summary>
public sealed class BaselineLexer
{
    private static readonly ImmutableHashSet<char> DefaultSymbols = ImmutableHashSet.Create(
        '/', ':', ',', ';', '=', '+', '-', '*', '<', '>', '!', '&', '|', '.', '@', '#', '?', '%', '^', '~', '\\'
    );

    private readonly ImmutableHashSet<char> _symbols = DefaultSymbols;

    public IEnumerable<SimpleToken> Lex(ReadOnlyMemory<char> input)
    {
        if (input.IsEmpty)
            yield break;

        int position = 0;
        int length = input.Length;

        while (position < length)
        {
            char c = input.Span[position];
            int start = position;

            // Single-character tokens with dedicated types
            var singleCharType = ClassifySingleChar(c);
            if (singleCharType.HasValue)
            {
                position++;
                yield return new SimpleToken(singleCharType.Value, input.Slice(start, 1), start);
                continue;
            }

            // Newline (handle \r\n as single token)
            if (c == '\n')
            {
                position++;
                yield return new SimpleToken(SimpleTokenType.Newline, input.Slice(start, 1), start);
                continue;
            }
            if (c == '\r')
            {
                position++;
                int tokenLength = 1;
                if (position < length && input.Span[position] == '\n')
                {
                    position++;
                    tokenLength = 2;
                }
                yield return new SimpleToken(SimpleTokenType.Newline, input.Slice(start, tokenLength), start);
                continue;
            }

            // Whitespace (excluding newlines)
            if (char.IsWhiteSpace(c))
            {
                while (position < length)
                {
                    char current = input.Span[position];
                    if (!char.IsWhiteSpace(current) || current == '\n' || current == '\r')
                        break;
                    position++;
                }
                yield return new SimpleToken(SimpleTokenType.Whitespace, input.Slice(start, position - start), start);
                continue;
            }

            // Digits (consecutive digit characters only)
            if (char.IsDigit(c))
            {
                while (position < length && char.IsDigit(input.Span[position]))
                {
                    position++;
                }
                yield return new SimpleToken(SimpleTokenType.Digits, input.Slice(start, position - start), start);
                continue;
            }

            // Symbol (from configured set, excluding special chars handled above)
            if (_symbols.Contains(c))
            {
                position++;
                yield return new SimpleToken(SimpleTokenType.Symbol, input.Slice(start, 1), start);
                continue;
            }

            // Identifier (everything else that forms identifier-like content)
            while (position < length)
            {
                char current = input.Span[position];
                if (char.IsWhiteSpace(current) ||
                    ClassifySingleChar(current).HasValue ||
                    _symbols.Contains(current))
                {
                    break;
                }
                position++;
            }

            if (position > start)
            {
                yield return new SimpleToken(SimpleTokenType.Ident, input.Slice(start, position - start), start);
            }
        }
    }

    public ImmutableArray<SimpleToken> LexToArray(ReadOnlyMemory<char> input)
    {
        return [.. Lex(input)];
    }

    private static SimpleTokenType? ClassifySingleChar(char c)
    {
        return c switch
        {
            '{' => SimpleTokenType.OpenBrace,
            '}' => SimpleTokenType.CloseBrace,
            '[' => SimpleTokenType.OpenBracket,
            ']' => SimpleTokenType.CloseBracket,
            '(' => SimpleTokenType.OpenParen,
            ')' => SimpleTokenType.CloseParen,
            '\'' => SimpleTokenType.SingleQuote,
            '"' => SimpleTokenType.DoubleQuote,
            '\\' => SimpleTokenType.Backslash,
            '/' => SimpleTokenType.Slash,
            '*' => SimpleTokenType.Asterisk,
            '.' => SimpleTokenType.Dot,
            _ => null
        };
    }
}

#endregion
