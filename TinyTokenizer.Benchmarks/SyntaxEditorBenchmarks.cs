using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using TinyTokenizer.Ast;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for SyntaxEditor operations including replacements, insertions,
/// and region resolution on various tree sizes and depths.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SyntaxEditorBenchmarks
{
    #region Test Data

    /// <summary>
    /// Simple expression for single-token replacements.
    /// </summary>
    private const string SimpleExpression = "foo + bar * baz";

    /// <summary>
    /// Empty block for inner replacement benchmarks.
    /// </summary>
    private const string EmptyBlockInput = "function test() { }";

    /// <summary>
    /// Block with content for inner replacement benchmarks.
    /// </summary>
    private const string BlockWithContent = """
        function test() {
            var x = 1;
            var y = 2;
            return x + y;
        }
        """;

    /// <summary>
    /// Multiple functions for multi-position insertions.
    /// </summary>
    private static readonly string MultiFunctionInput = GenerateMultiFunctionInput();

    /// <summary>
    /// Deeply nested blocks for deep tree traversal.
    /// </summary>
    private static readonly string DeeplyNestedInput = GenerateDeeplyNestedInput(20);

    /// <summary>
    /// Very deeply nested blocks for stress testing.
    /// </summary>
    private static readonly string VeryDeeplyNestedInput = GenerateDeeplyNestedInput(50);

    private static readonly Schema DefaultSchema = Schema.Create()
        .AddCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .WithOperators(CommonOperators.CFamily)
        .Build();

    // Pre-parsed trees
    private static readonly SyntaxTree SimpleTree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
    private static readonly SyntaxTree EmptyBlockTree = SyntaxTree.Parse(EmptyBlockInput, DefaultSchema);
    private static readonly SyntaxTree BlockWithContentTree = SyntaxTree.Parse(BlockWithContent, DefaultSchema);
    private static readonly SyntaxTree MultiFunctionTree = SyntaxTree.Parse(MultiFunctionInput, DefaultSchema);
    private static readonly SyntaxTree DeeplyNestedTree = SyntaxTree.Parse(DeeplyNestedInput, DefaultSchema);
    private static readonly SyntaxTree VeryDeeplyNestedTree = SyntaxTree.Parse(VeryDeeplyNestedInput, DefaultSchema);

    private static string GenerateMultiFunctionInput()
    {
        var template = """
            function func{0}(a, b) {{
                return a + b;
            }}

            """;

        return string.Concat(Enumerable.Range(0, 50).Select(i =>
            string.Format(template, i)));
    }

    private static string GenerateDeeplyNestedInput(int depth)
    {
        var open = string.Concat(Enumerable.Repeat("{ ", depth));
        var close = string.Concat(Enumerable.Repeat(" }", depth));
        return $"function deep() {open}x{close}";
    }

    #endregion

    #region Replace - Single Token

    /// <summary>
    /// Replace a single identifier token with another identifier.
    /// Baseline for minimal edit operation.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Replace")]
    public SyntaxTree Replace_SingleToken()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.Ident("foo"), "replaced")
            .Commit();
        return tree;
    }

    /// <summary>
    /// Replace multiple tokens in a single edit batch.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Replace")]
    public SyntaxTree Replace_MultipleTokens()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.Ident("foo"), "a")
            .Replace(Q.Ident("bar"), "b")
            .Replace(Q.Ident("baz"), "c")
            .Commit();
        return tree;
    }

    /// <summary>
    /// Replace all identifiers using a single query.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Replace")]
    public SyntaxTree Replace_AllIdents()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.AnyIdent, "x")
            .Commit();
        return tree;
    }

    #endregion

    #region Replace - Block Inner

    /// <summary>
    /// Replace the inner content of an empty block.
    /// Tests zero-width region handling.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("BlockInner")]
    public SyntaxTree Replace_BlockInner_Empty()
    {
        var tree = SyntaxTree.Parse(EmptyBlockInput, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), "return 42;")
            .Commit();
        return tree;
    }

    /// <summary>
    /// Replace the inner content of a block with existing content.
    /// Tests multi-slot region replacement.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("BlockInner")]
    public SyntaxTree Replace_BlockInner_WithContent()
    {
        var tree = SyntaxTree.Parse(BlockWithContent, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), "return 0;")
            .Commit();
        return tree;
    }

    /// <summary>
    /// Replace inner content with larger replacement text.
    /// Tests tree rebuilding with size change.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("BlockInner")]
    public SyntaxTree Replace_BlockInner_Larger()
    {
        var tree = SyntaxTree.Parse(EmptyBlockInput, DefaultSchema);
        var largeContent = string.Join("\n", Enumerable.Range(0, 20).Select(i => $"var x{i} = {i};"));
        tree.CreateEditor()
            .Replace(Q.BraceBlock.First().Inner(), largeContent)
            .Commit();
        return tree;
    }

    #endregion

    #region Insert - Multiple Positions

    /// <summary>
    /// Insert after a single position.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Insert")]
    public SyntaxTree InsertAfter_SinglePosition()
    {
        var tree = SyntaxTree.Parse(MultiFunctionInput, DefaultSchema);
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.First().Start(), "// inserted\n")
            .Commit();
        return tree;
    }

    /// <summary>
    /// Insert after all function body starts (50 positions).
    /// Tests edit batching with many positions.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Insert")]
    public SyntaxTree InsertAfter_ManyPositions()
    {
        var tree = SyntaxTree.Parse(MultiFunctionInput, DefaultSchema);
        tree.CreateEditor()
            .InsertAfter(Q.BraceBlock.Start(), "// inserted\n")
            .Commit();
        return tree;
    }

    /// <summary>
    /// Insert before all closing braces.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Insert")]
    public SyntaxTree InsertBefore_ManyPositions()
    {
        var tree = SyntaxTree.Parse(MultiFunctionInput, DefaultSchema);
        tree.CreateEditor()
            .InsertBefore(Q.BraceBlock.End(), "\n// end")
            .Commit();
        return tree;
    }

    #endregion

    #region SelectRegions - Deep Trees

    /// <summary>
    /// Select regions in a moderately deep tree (20 levels).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("SelectRegions")]
    public int SelectRegions_DeepTree()
    {
        var count = 0;
        foreach (var _ in ((IRegionQuery)Q.AnyIdent).SelectRegions(DeeplyNestedTree))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Select regions in a very deep tree (50 levels).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("SelectRegions")]
    public int SelectRegions_VeryDeepTree()
    {
        var count = 0;
        foreach (var _ in ((IRegionQuery)Q.AnyIdent).SelectRegions(VeryDeeplyNestedTree))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Select block regions in a deep tree.
    /// Tests BlockNodeQuery optimization.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("SelectRegions")]
    public int SelectRegions_DeepTree_Blocks()
    {
        var count = 0;
        foreach (var _ in ((IRegionQuery)Q.BraceBlock).SelectRegions(DeeplyNestedTree))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Select first match only - tests short-circuit.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("SelectRegions")]
    public int SelectRegions_DeepTree_First()
    {
        var count = 0;
        foreach (var _ in ((IRegionQuery)Q.AnyIdent.First()).SelectRegions(VeryDeeplyNestedTree))
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Edit with Transformer

    /// <summary>
    /// Edit using a transformer function (uppercase identifiers).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Transform")]
    public SyntaxTree Edit_Transform_SingleToken()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        tree.CreateEditor()
            .Edit(Q.AnyIdent, text => text.ToUpperInvariant())
            .Commit();
        return tree;
    }

    /// <summary>
    /// Edit using Replace with RedNode transformer.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Transform")]
    public SyntaxTree Replace_WithNodeTransformer()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.AnyIdent, node => node.ToText().ToUpperInvariant())
            .Commit();
        return tree;
    }

    #endregion

    #region Undo/Redo

    /// <summary>
    /// Perform edit then undo.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("UndoRedo")]
    public SyntaxTree Edit_ThenUndo()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.Ident("foo"), "replaced")
            .Commit();
        tree.Undo();
        return tree;
    }

    /// <summary>
    /// Perform multiple edits then undo all.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("UndoRedo")]
    public SyntaxTree MultipleEdits_ThenUndoAll()
    {
        var tree = SyntaxTree.Parse(SimpleExpression, DefaultSchema);
        
        for (int i = 0; i < 5; i++)
        {
            tree.CreateEditor()
                .Replace(Q.AnyIdent.First(), $"v{i}")
                .Commit();
        }
        
        while (tree.CanUndo)
        {
            tree.Undo();
        }
        
        return tree;
    }

    #endregion
}
