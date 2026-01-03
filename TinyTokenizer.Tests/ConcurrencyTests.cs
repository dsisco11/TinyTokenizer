using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for thread safety of the tokenizer and syntax tree operations.
/// Verifies concurrent access doesn't cause exceptions or data corruption.
/// </summary>
[Trait("Category", "Concurrency")]
public class ConcurrencyTests
{
    #region Constants

    private const int ConcurrentTaskCount = 100;
    private const int IterationsPerTask = 50;

    #endregion

    #region Helper Methods

    private static SyntaxTree ParseTree(string source)
    {
        return SyntaxTree.Parse(source, TokenizerOptions.Default);
    }

    private static string GenerateTestSource(int index)
    {
        return $@"
function test{index}() {{
    var x{index} = {index};
    var y{index} = x{index} + {index * 2};
    return y{index};
}}
";
    }

    /// <summary>
    /// Recursively enumerate all descendants of a node.
    /// </summary>
    private static IEnumerable<RedNode> GetAllDescendants(RedNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in GetAllDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    #endregion

    #region Concurrent RedNode Access Tests

    [Fact]
    [Trait("Category", "RedNode")]
    public async Task ConcurrentRedNodeAccess_MultipleTasksAccessingChildren_NoExceptions()
    {
        // Arrange - parse tree once
        var source = @"
{
    first { inner1 inner2 }
    second { nested { deep } }
    third { a b c d e }
}
";
        var tree = ParseTree(source);
        var root = tree.Root;

        // Act - spawn many tasks accessing Children concurrently
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    // Access children at various levels
                    var children = root.Children.ToList();
                    foreach (var child in children)
                    {
                        var grandchildren = child.Children.ToList();
                        var slotCount = child.SlotCount;
                        var width = child.Width;
                        var position = child.Position;
                    }
                }
            }))
            .ToArray();

        // Assert - no exceptions thrown
        await Task.WhenAll(tasks);
    }

    [Fact]
    [Trait("Category", "RedNode")]
    public async Task ConcurrentRedNodeAccess_SiblingIndexAccess_NoExceptions()
    {
        // Arrange
        var source = "{ a b c d e f g h i j }";
        var tree = ParseTree(source);
        var root = tree.Root;

        // Act - spawn tasks accessing SiblingIndex
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    foreach (var child in root.Children)
                    {
                        var index = child.SiblingIndex;
                    }
                }
            }))
            .ToArray();

        // Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    [Trait("Category", "RedNode")]
    public async Task ConcurrentRedNodeAccess_NextAndPreviousSibling_NoExceptions()
    {
        // Arrange
        var source = "{ first second third fourth fifth }";
        var tree = ParseTree(source);
        var root = tree.Root;

        // Act - spawn tasks navigating siblings
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    var children = root.Children.ToList();
                    foreach (var child in children)
                    {
                        var next = child.NextSibling();
                        var prev = child.PreviousSibling();
                    }
                }
            }))
            .ToArray();

        // Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    [Trait("Category", "RedNode")]
    public async Task ConcurrentRedNodeAccess_ParentNavigation_NoExceptions()
    {
        // Arrange
        var source = "{ outer { middle { inner } } }";
        var tree = ParseTree(source);

        // Act - spawn tasks navigating parent chain
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    // Navigate to deepest node and back up
                    var node = tree.Root;
                    while (node.Children.Any())
                    {
                        node = node.Children.First();
                    }

                    // Walk back up to root
                    while (node.Parent != null)
                    {
                        node = node.Parent;
                    }
                }
            }))
            .ToArray();

        // Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    [Trait("Category", "RedNode")]
    public async Task ConcurrentRedNodeAccess_DescendantsEnumeration_NoExceptions()
    {
        // Arrange
        var source = @"
{
    level1 {
        level2a { leaf1 leaf2 }
        level2b { leaf3 leaf4 }
    }
}
";
        var tree = ParseTree(source);

        // Act - spawn tasks enumerating descendants
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    var descendants = GetAllDescendants(tree.Root).ToList();
                    Assert.NotEmpty(descendants);
                }
            }))
            .ToArray();

        // Assert
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Concurrent Tree Parsing Tests

    [Fact]
    [Trait("Category", "SyntaxTree")]
    public async Task ConcurrentTreeParsing_DifferentInputs_AllTreesValid()
    {
        // Act - spawn tasks each parsing different input
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(i => Task.Run(() =>
            {
                var source = GenerateTestSource(i);
                var tree = ParseTree(source);
                
                // Verify tree is valid
                Assert.NotNull(tree.Root);
                Assert.True(tree.Width > 0);
                
                return tree;
            }))
            .ToArray();

        // Await all and verify results
        var trees = await Task.WhenAll(tasks);
        
        Assert.Equal(ConcurrentTaskCount, trees.Length);
        Assert.All(trees, tree =>
        {
            Assert.NotNull(tree);
            Assert.NotNull(tree.Root);
        });
    }

    [Fact]
    [Trait("Category", "SyntaxTree")]
    public async Task ConcurrentTreeParsing_SameInput_ConsistentResults()
    {
        // Arrange
        const string source = "{ identifier = 42; }";

        // Act - parse same input concurrently
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() => ParseTree(source)))
            .ToArray();

        var trees = await Task.WhenAll(tasks);

        // Assert - all trees should have same structure
        var firstWidth = trees[0].Width;
        var firstChildCount = trees[0].Root.Children.Count();

        Assert.All(trees, tree =>
        {
            Assert.Equal(firstWidth, tree.Width);
            Assert.Equal(firstChildCount, tree.Root.Children.Count());
        });
    }

    [Fact]
    [Trait("Category", "SyntaxTree")]
    public async Task ConcurrentTreeParsing_LargeInput_NoExceptions()
    {
        // Arrange - generate large input
        var largeSource = string.Join("\n", Enumerable.Range(0, 100)
            .Select(i => $"block{i} {{ item{i} = {i}; }}"));

        // Act
        var tasks = Enumerable.Range(0, 50) // Fewer tasks for large input
            .Select(_ => Task.Run(() => ParseTree(largeSource)))
            .ToArray();

        var trees = await Task.WhenAll(tasks);

        // Assert
        Assert.All(trees, tree => Assert.NotNull(tree.Root));
    }

    #endregion

    #region Concurrent Tokenizer Tests

    [Fact]
    [Trait("Category", "Tokenizer")]
    public async Task ConcurrentTokenizing_SharedLexerAndParser_NoExceptions()
    {
        // Arrange - create shared lexer and parser
        var options = TokenizerOptions.Default;
        var lexer = new Lexer(options);
        var parser = new TokenParser(options);

        // Act - concurrent tokenization with shared instances
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(i => Task.Run(() =>
            {
                var source = $"token{i} = {i} + {i * 2};";
                var simpleTokens = lexer.Lex(source);
                var tokens = parser.Parse(simpleTokens).ToList();
                return tokens;
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(ConcurrentTaskCount, results.Length);
        Assert.All(results, tokens => Assert.NotEmpty(tokens));
    }

    [Fact]
    [Trait("Category", "Tokenizer")]
    public async Task ConcurrentTokenizing_SeparateInstances_NoExceptions()
    {
        // Act - each task creates its own lexer/parser
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(i => Task.Run(() =>
            {
                var options = TokenizerOptions.Default;
                var lexer = new Lexer(options);
                var parser = new TokenParser(options);
                
                var source = $"func{i}(arg{i}) {{ return {i}; }}";
                var simpleTokens = lexer.Lex(source);
                var tokens = parser.Parse(simpleTokens).ToList();
                return tokens;
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, tokens => Assert.NotEmpty(tokens));
    }

    #endregion

    #region Concurrent Query Tests

    [Fact]
    [Trait("Category", "Query")]
    public async Task ConcurrentQueries_SameTree_NoExceptions()
    {
        // Arrange
        var source = @"
{
    block1 { a b c }
    block2 { d e f }
    block3 { g h i }
}
";
        var tree = ParseTree(source);

        // Act - run queries concurrently on the same tree
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    // Various tree queries
                    var children = tree.Root.Children.ToList();
                    var descendants = GetAllDescendants(tree.Root).ToList();
                    var leaves = descendants.Where(n => n.IsLeaf).ToList();
                    
                    Assert.NotEmpty(children);
                    Assert.NotEmpty(descendants);
                }
            }))
            .ToArray();

        // Assert
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Stress Tests

    [Fact]
    [Trait("Category", "Stress")]
    public async Task StressTest_RapidParseAndQuery_NoExceptions()
    {
        // Act - rapid fire parsing and querying
        var tasks = Enumerable.Range(0, ConcurrentTaskCount)
            .Select(i => Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var source = $"{{ item_{i}_{j} = {i * j}; }}";
                    var tree = ParseTree(source);
                    
                    // Immediately query the tree
                    var children = tree.Root.Children.ToList();
                    var descendants = GetAllDescendants(tree.Root).ToList();
                    var width = tree.Width;
                }
            }))
            .ToArray();

        // Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    [Trait("Category", "Stress")]
    public async Task StressTest_MixedOperations_NoExceptions()
    {
        // Arrange
        var sharedTree = ParseTree("{ shared content here }");

        // Act - mix of operations
        var tasks = new List<Task>();
        
        // Some tasks parse new trees
        tasks.AddRange(Enumerable.Range(0, ConcurrentTaskCount / 3)
            .Select(i => Task.Run(() =>
            {
                var tree = ParseTree($"{{ new{i} }}");
                var children = tree.Root.Children.ToList();
            })));
        
        // Some tasks query shared tree
        tasks.AddRange(Enumerable.Range(0, ConcurrentTaskCount / 3)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    var children = sharedTree.Root.Children.ToList();
                    var descendants = GetAllDescendants(sharedTree.Root).ToList();
                }
            })));
        
        // Some tasks navigate shared tree
        tasks.AddRange(Enumerable.Range(0, ConcurrentTaskCount / 3)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < IterationsPerTask; i++)
                {
                    foreach (var child in sharedTree.Root.Children)
                    {
                        var index = child.SiblingIndex;
                        var next = child.NextSibling();
                        var parent = child.Parent;
                    }
                }
            })));

        // Assert
        await Task.WhenAll(tasks);
    }

    #endregion
}
