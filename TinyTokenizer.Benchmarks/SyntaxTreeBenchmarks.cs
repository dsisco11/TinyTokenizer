using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using TinyTokenizer.Ast;
using Q = TinyTokenizer.Ast.Query;

namespace TinyTokenizer.Benchmarks;

/// <summary>
/// Benchmarks for SyntaxTree parsing and SyntaxBinder pattern matching performance.
/// Measures tree construction, red node creation, and semantic binding operations.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SyntaxTreeBenchmarks
{
    #region Test Data

    /// <summary>
    /// Small input (~500 chars): simple function-like structure.
    /// </summary>
    private const string SmallInput = """
        function hello(name, age) {
            var greeting = "Hello, " + name;
            if (age > 18) {
                return greeting + "!";
            }
            return greeting;
        }
        """;

    /// <summary>
    /// Medium input (~5KB): multiple functions and nested structures.
    /// </summary>
    private static readonly string MediumInput = GenerateMediumInput();

    /// <summary>
    /// Large input (~50KB): extensive codebase simulation.
    /// </summary>
    private static readonly string LargeInput = GenerateLargeInput();

    /// <summary>
    /// Deeply nested input to stress block parsing.
    /// </summary>
    private static readonly string DeeplyNestedInput = GenerateDeeplyNestedInput();

    private static readonly Schema DefaultSchema = Schema.Create()
        .AddCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .WithOperators(CommonOperators.CFamily)
        .Build();

    // Pre-parsed trees for traversal benchmarks
    private static readonly SyntaxTree SmallTree = SyntaxTree.Parse(SmallInput, DefaultSchema);
    private static readonly SyntaxTree MediumTree = SyntaxTree.Parse(MediumInput, DefaultSchema);
    private static readonly SyntaxTree LargeTree = SyntaxTree.Parse(LargeInput, DefaultSchema);

    private static string GenerateMediumInput()
    {
        var template = """
            function process{0}(data, options) {{
                // Validate input
                if (data === null || data === undefined) {{
                    throw new Error("Invalid data");
                }}
                
                var result = {{}};
                
                /* Process each item
                   in the collection */
                for (var i = 0; i < data.length; i++) {{
                    var item = data[i];
                    if (item.type === "string") {{
                        result.strings = result.strings || [];
                        result.strings.push(item.value);
                    }} else if (item.type === "number") {{
                        result.numbers = result.numbers || [];
                        result.numbers.push(parseFloat(item.value));
                    }} else {{
                        result.other = result.other || [];
                        result.other.push(item);
                    }}
                }}
                
                return {{
                    data: result,
                    count: data.length,
                    processed: true
                }};
            }}

            """;

        return string.Concat(Enumerable.Range(0, 20).Select(i => 
            string.Format(template, i)));
    }

    private static string GenerateLargeInput()
    {
        var template = """
            class Handler{0} {{
                constructor(config) {{
                    this.config = config;
                    this.cache = {{}};
                    this.listeners = [];
                }}
                
                initialize() {{
                    // Setup phase
                    var self = this;
                    this.listeners.push({{
                        event: "ready",
                        callback: function() {{ self.onReady(); }}
                    }});
                }}
                
                process(input) {{
                    if (!this.validate(input)) {{
                        return {{ error: "Invalid input" }};
                    }}
                    
                    var result = this.transform(input);
                    this.cache[input.id] = result;
                    
                    for (var i = 0; i < this.listeners.length; i++) {{
                        var listener = this.listeners[i];
                        if (listener.event === "process") {{
                            listener.callback(result);
                        }}
                    }}
                    
                    return result;
                }}
                
                validate(input) {{
                    return input !== null && 
                           input !== undefined && 
                           typeof input.id === "string";
                }}
                
                transform(input) {{
                    return {{
                        id: input.id,
                        value: input.value * 2,
                        timestamp: Date.now(),
                        handler: this.config.name
                    }};
                }}
                
                onReady() {{
                    console.log("Handler ready: " + this.config.name);
                }}
            }}

            """;

        return string.Concat(Enumerable.Range(0, 100).Select(i => 
            string.Format(template, i)));
    }

    private static string GenerateDeeplyNestedInput()
    {
        // Creates deeply nested blocks: { { { ... } } }
        const int depth = 50;
        var open = string.Concat(Enumerable.Repeat("{ ", depth));
        var content = "x = 1";
        var close = string.Concat(Enumerable.Repeat(" }", depth));
        return open + content + close;
    }

    #endregion

    #region Tree Parsing Benchmarks

    [Benchmark(Description = "Parse Small (~500 chars)")]
    [BenchmarkCategory("Parsing", "Small")]
    public SyntaxTree ParseSmall()
    {
        return SyntaxTree.Parse(SmallInput, DefaultSchema);
    }

    [Benchmark(Description = "Parse Medium (~5KB)")]
    [BenchmarkCategory("Parsing", "Medium")]
    public SyntaxTree ParseMedium()
    {
        return SyntaxTree.Parse(MediumInput, DefaultSchema);
    }

    [Benchmark(Description = "Parse Large (~50KB)")]
    [BenchmarkCategory("Parsing", "Large")]
    public SyntaxTree ParseLarge()
    {
        return SyntaxTree.Parse(LargeInput, DefaultSchema);
    }

    [Benchmark(Description = "Parse Deeply Nested (50 levels)")]
    [BenchmarkCategory("Parsing", "Nested")]
    public SyntaxTree ParseDeeplyNested()
    {
        return SyntaxTree.Parse(DeeplyNestedInput, DefaultSchema);
    }

    #endregion

    #region Schema Integration Benchmarks

    [Benchmark(Description = "Parse without Schema")]
    [BenchmarkCategory("Schema", "NoSchema")]
    public SyntaxTree ParseWithoutSchema()
    {
        return SyntaxTree.Parse(MediumInput);
    }

    [Benchmark(Description = "Parse with Schema")]
    [BenchmarkCategory("Schema", "WithSchema")]
    public SyntaxTree ParseWithSchema()
    {
        return SyntaxTree.Parse(MediumInput, DefaultSchema);
    }

    #endregion

    #region Red Node Creation Benchmarks

    [Benchmark(Description = "Root Access (lazy red creation)")]
    [BenchmarkCategory("RedNode", "Access")]
    public RedNode AccessRoot()
    {
        var tree = SyntaxTree.Parse(SmallInput, DefaultSchema);
        return tree.Root;
    }

    [Benchmark(Description = "Traverse All Children (Small)")]
    [BenchmarkCategory("RedNode", "Traverse")]
    public int TraverseSmall()
    {
        return CountAllNodes(SmallTree.Root);
    }

    [Benchmark(Description = "Traverse All Children (Medium)")]
    [BenchmarkCategory("RedNode", "Traverse")]
    public int TraverseMedium()
    {
        return CountAllNodes(MediumTree.Root);
    }

    [Benchmark(Description = "Traverse All Children (Large)")]
    [BenchmarkCategory("RedNode", "Traverse")]
    public int TraverseLarge()
    {
        return CountAllNodes(LargeTree.Root);
    }

    private static int CountAllNodes(RedNode node)
    {
        int count = 1;
        foreach (var child in node.Children)
        {
            count += CountAllNodes(child);
        }
        return count;
    }

    #endregion

    #region TreeWalker Benchmarks

    [Benchmark(Description = "TreeWalker Traverse (Small)")]
    [BenchmarkCategory("Walker", "Small")]
    public int WalkSmall()
    {
        var walker = new TreeWalker(SmallTree.Root);
        int count = 0;
        RedNode? node;
        while ((node = walker.NextNode()) != null)
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "TreeWalker Traverse (Medium)")]
    [BenchmarkCategory("Walker", "Medium")]
    public int WalkMedium()
    {
        var walker = new TreeWalker(MediumTree.Root);
        int count = 0;
        RedNode? node;
        while ((node = walker.NextNode()) != null)
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "TreeWalker Traverse (Large)")]
    [BenchmarkCategory("Walker", "Large")]
    public int WalkLarge()
    {
        var walker = new TreeWalker(LargeTree.Root);
        int count = 0;
        RedNode? node;
        while ((node = walker.NextNode()) != null)
        {
            count++;
        }
        return count;
    }

    [Benchmark(Description = "TreeWalker.Descendants (Medium)")]
    [BenchmarkCategory("Walker", "Enumerable")]
    public int DescendantsMedium()
    {
        var walker = new TreeWalker(MediumTree.Root);
        return walker.Descendants().Count();
    }

    [Benchmark(Description = "TreeWalker Filter Leaves Only (Medium)")]
    [BenchmarkCategory("Walker", "Filtering")]
    public int WalkLeavesOnly()
    {
        var walker = new TreeWalker(MediumTree.Root, NodeFilter.Leaves);
        int count = 0;
        RedNode? node;
        while ((node = walker.NextNode()) != null)
        {
            count++;
        }
        return count;
    }

    #endregion

    #region Query Benchmarks

    [Benchmark(Description = "Query.OfType<RedLeaf> (Medium)")]
    [BenchmarkCategory("Query", "Filter")]
    public int QueryLeavesMedium()
    {
        var walker = new TreeWalker(MediumTree.Root);
        return walker.Descendants()
            .OfType<RedLeaf>()
            .Count();
    }

    [Benchmark(Description = "Query.OfType<RedBlock> (Medium)")]
    [BenchmarkCategory("Query", "Filter")]
    public int QueryBlocksMedium()
    {
        var walker = new TreeWalker(MediumTree.Root);
        return walker.Descendants()
            .OfType<RedBlock>()
            .Count();
    }

    [Benchmark(Description = "Find Brace Blocks (Medium)")]
    [BenchmarkCategory("Query", "Find")]
    public int FindBraceBlocksMedium()
    {
        var walker = new TreeWalker(MediumTree.Root);
        return walker.Descendants()
            .OfType<RedBlock>()
            .Count(b => b.Kind == NodeKind.BraceBlock);
    }

    #endregion

    #region SiblingIndex Benchmarks

    [Benchmark(Description = "SiblingIndex Access (cached)")]
    [BenchmarkCategory("Sibling", "Index")]
    public int SiblingIndexAccess()
    {
        int sum = 0;
        foreach (var child in MediumTree.Root.Children)
        {
            sum += child.SiblingIndex;
        }
        return sum;
    }

    [Benchmark(Description = "NextSibling Navigation")]
    [BenchmarkCategory("Sibling", "Navigation")]
    public int NextSiblingNavigation()
    {
        int count = 0;
        var first = MediumTree.Root.Children.FirstOrDefault();
        var current = first;
        while (current != null)
        {
            count++;
            current = current.NextSibling();
        }
        return count;
    }

    [Benchmark(Description = "PreviousSibling Navigation")]
    [BenchmarkCategory("Sibling", "Navigation")]
    public int PreviousSiblingNavigation()
    {
        int count = 0;
        var last = MediumTree.Root.Children.LastOrDefault();
        var current = last;
        while (current != null)
        {
            count++;
            current = current.PreviousSibling();
        }
        return count;
    }

    #endregion

    #region Edit/Mutation Benchmarks

    [Benchmark(Description = "SyntaxEditor Remove")]
    [BenchmarkCategory("Edit", "Remove")]
    public SyntaxTree EditRemove()
    {
        var tree = SyntaxTree.Parse(SmallInput, DefaultSchema);
        tree.CreateEditor()
            .Remove(Q.AnyIdent.First())
            .Commit();
        return tree;
    }

    [Benchmark(Description = "SyntaxEditor Insert")]
    [BenchmarkCategory("Edit", "Insert")]
    public SyntaxTree EditInsert()
    {
        var tree = SyntaxTree.Parse(SmallInput, DefaultSchema);
        tree.CreateEditor()
            .Insert(Q.BraceBlock.First().InnerStart(), "/* inserted */")
            .Commit();
        return tree;
    }

    [Benchmark(Description = "SyntaxEditor Replace")]
    [BenchmarkCategory("Edit", "Replace")]
    public SyntaxTree EditReplace()
    {
        var tree = SyntaxTree.Parse(SmallInput, DefaultSchema);
        tree.CreateEditor()
            .Replace(Q.AnyIdent.WithText("hello"), "goodbye")
            .Commit();
        return tree;
    }

    [Benchmark(Description = "Undo/Redo Cycle")]
    [BenchmarkCategory("Edit", "History")]
    public SyntaxTree UndoRedoCycle()
    {
        var tree = SyntaxTree.Parse(SmallInput, DefaultSchema);
        
        tree.CreateEditor()
            .Remove(Q.AnyIdent.First())
            .Commit();
        
        tree.Undo();
        tree.Redo();
        tree.Undo();
        
        return tree;
    }

    #endregion
}
