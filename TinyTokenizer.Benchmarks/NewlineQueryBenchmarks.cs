using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using TinyTokenizer.Ast;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for token-centric newline detection via Query.Newline.
/// Measures the cost of scanning a newline-heavy tree and matching nodes
/// that follow a newline (current leading newline OR previous sibling trailing newline).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class NewlineQueryBenchmarks
{
    [Params(1_000, 10_000)]
    public int Lines { get; set; }

    private SyntaxTree _tree = null!;

    [GlobalSetup]
    public void Setup()
    {
        var source = GenerateNewlineHeavyInput(Lines);
        _tree = SyntaxTree.Parse(source);
    }

    [Benchmark(Description = "Select(Query.Newline) - Count")]
    [BenchmarkCategory("Query", "Newline")]
    public int SelectNewline_Count()
    {
        int count = 0;
        foreach (var _ in _tree.Select(Q.Newline))
            count++;
        return count;
    }

    [Benchmark(Description = "Select(Query.Newline.First()) - First match")]
    [BenchmarkCategory("Query", "Newline")]
    public int SelectNewline_First()
    {
        foreach (var node in _tree.Select(Q.Newline.First()))
            return node.Position;
        return -1;
    }

    private static string GenerateNewlineHeavyInput(int lines)
    {
        // Intentionally mixes: trailing-newline ownership (end-of-line) and
        // leading-newline ownership (own-line comments become leading trivia).
        var builder = new StringBuilder(capacity: lines * 32);

        for (int i = 0; i < lines; i++)
        {
            builder.Append("x");
            builder.Append(i);
            builder.Append(" = ");
            builder.Append(i);
            builder.Append(";\n");

            if ((i & 7) == 0)
            {
                builder.Append("// comment\n");
            }
        }

        return builder.ToString();
    }
}
