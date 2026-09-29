using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using TinyTokenizer.Ast;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks initial syntax binding and full versus incremental rebinding.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SyntaxBindingBenchmarks
{
    private static readonly Schema BindingSchema = Schema.Create()
        .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
            .Match(Q.AnyIdent, Q.ParenBlock)
            .Build())
        .DefineSyntax(Syntax.Define<ArrayAccessSyntax>("ArrayAccess")
            .Match(Q.AnyIdent, Q.BracketBlock)
            .Build())
        .DefineSyntax(Syntax.Define<PropertyAccessSyntax>("PropertyAccess")
            .Match(Q.AnyIdent, Q.Symbol("."), Q.AnyIdent)
            .Build())
        .Build();

    private SyntaxBinder _binder = null!;
    private GreenNode _unboundRoot = null!;
    private GreenNode _boundRoot = null!;
    private NodePath _incrementalPath;

    [Params(10, 1_000)]
    public int StatementCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var source = GenerateSource(StatementCount);
        var unboundTree = SyntaxTree.Parse(source);
        var boundTree = unboundTree.Bind(BindingSchema);
        var targetIdentifier = $"target{StatementCount / 2}";
        var target = boundTree.Select(Q.Ident(targetIdentifier)).Single();

        _binder = new SyntaxBinder(BindingSchema);
        _unboundRoot = unboundTree.GreenRoot;
        _boundRoot = boundTree.GreenRoot;
        _incrementalPath = NodePath.FromNode(target.Parent!);
    }

    [Benchmark]
    [BenchmarkCategory("Binding")]
    public object BindTree()
    {
        return _binder.Bind(_unboundRoot);
    }

    [Benchmark]
    [BenchmarkCategory("Rebinding", "Incremental")]
    public object RebindIncremental()
    {
        return _binder.BindAtPath(_boundRoot, _incrementalPath);
    }

    [Benchmark]
    [BenchmarkCategory("Rebinding", "Full")]
    public object RebindFromRoot()
    {
        return _binder.Bind(_boundRoot);
    }

    private static string GenerateSource(int statementCount)
    {
        var statements = Enumerable.Range(0, statementCount)
            .Select(index => $"target{index}(items{index}[0].value);");

        return $"rootTarget {{ {string.Join(' ', statements)} }}";
    }
}