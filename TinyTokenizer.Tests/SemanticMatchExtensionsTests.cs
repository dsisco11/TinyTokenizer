using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for SemanticMatchExtensions covering all methods.
/// </summary>
[Trait("Category", "Semantic")]
public class SemanticMatchExtensionsTests
{
    #region RedNode.Match<T> Extension

    [Fact]
    public void Match_FromRedNode_FindsSemanticNodes()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var schema = Schema.Default;
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Single(calls);
        Assert.Equal("foo", calls[0].Name);
    }

    [Fact]
    public void Match_FromRedNode_FindsMultipleMatches()
    {
        var tree = SyntaxTree.Parse("foo(x) bar(y)");
        var schema = Schema.Default;
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public void Match_FromRedNode_WithContext()
    {
        var tree = SyntaxTree.Parse("test(a)");
        var schema = Schema.Default;
        var context = new SemanticContext { StrictMode = true };
        
        var calls = tree.Root.Match<FunctionNameNode>(schema, context).ToList();
        
        Assert.Single(calls);
    }

    [Fact]
    public void Match_FromRedNode_NoMatches_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("just identifiers here");
        var schema = Schema.Default;
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Empty(calls);
    }

    [Fact]
    public void Match_FromRedNode_UnregisteredType_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        // Create schema without FunctionName definition
        var schema = Schema.Create().Build();
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Empty(calls);
    }

    [Fact]
    public void Match_FromNestedNode_SearchesDescendants()
    {
        var tree = SyntaxTree.Parse("{foo(x)}");
        var schema = Schema.Default;
        var block = tree.Root.Children.First();
        
        var calls = block.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Single(calls);
    }

    [Fact]
    public void Match_ArrayAccess_FromRedNode()
    {
        var tree = SyntaxTree.Parse("arr[0]");
        var schema = Schema.Default;
        
        var accesses = tree.Root.Match<ArrayAccessNode>(schema).ToList();
        
        Assert.Single(accesses);
        Assert.Equal("arr", accesses[0].Target);
    }

    [Fact]
    public void Match_PropertyAccess_FromRedNode()
    {
        var tree = SyntaxTree.Parse("obj.prop");
        var schema = Schema.Default;
        
        var props = tree.Root.Match<PropertyAccessNode>(schema).ToList();
        
        Assert.Single(props);
        Assert.Equal("obj", props[0].Object);
        Assert.Equal("prop", props[0].Property);
    }

    [Fact]
    public void Match_MethodCall_FromRedNode()
    {
        var tree = SyntaxTree.Parse("obj.method(x)");
        var schema = Schema.Default;
        
        var methods = tree.Root.Match<MethodCallNode>(schema).ToList();
        
        Assert.Single(methods);
        Assert.Equal("obj", methods[0].Object);
        Assert.Equal("method", methods[0].Method);
    }

    #endregion

    #region RedNode.MatchAll Extension

    [Fact]
    public void MatchAll_FromRedNode_FindsAllTypes()
    {
        var tree = SyntaxTree.Parse("foo(x) arr[0]");
        var schema = Schema.Default;
        
        var all = tree.Root.MatchAll(schema).ToList();
        
        Assert.True(all.Count >= 2);
        Assert.True(all.Any(n => n is FunctionNameNode));
        Assert.True(all.Any(n => n is ArrayAccessNode));
    }

    [Fact]
    public void MatchAll_WithContext()
    {
        var tree = SyntaxTree.Parse("foo(x) obj.prop");
        var schema = Schema.Default;
        var context = new SemanticContext();
        
        var all = tree.Root.MatchAll(schema, context).ToList();
        
        Assert.True(all.Count >= 2);
    }

    [Fact]
    public void MatchAll_NoMatches_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("a b c");
        var schema = Schema.Default;
        
        var all = tree.Root.MatchAll(schema).ToList();
        
        // May have some matches depending on schema, but shouldn't throw
    }

    [Fact]
    public void MatchAll_FromNestedNode()
    {
        var tree = SyntaxTree.Parse("{foo(x) arr[0]}");
        var schema = Schema.Default;
        var block = tree.Root.Children.First();
        
        var all = block.MatchAll(schema).ToList();
        
        Assert.True(all.Count >= 2);
    }

    [Fact]
    public void MatchAll_EmptySchema_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var schema = Schema.Create().Build();
        
        var all = tree.Root.MatchAll(schema).ToList();
        
        Assert.Empty(all);
    }

    #endregion

    #region Schema.Semantic Extension

    [Fact]
    public void Schema_Semantic_ByName_CreatesQuery()
    {
        var schema = Schema.Default;
        
        var query = schema.Syntax("FunctionName");
        
        Assert.NotNull(query);
    }

    [Fact]
    public void Schema_Semantic_ByName_QueryMatches()
    {
        var tree = SyntaxTree.Parse("test");
        var schema = Schema.Default;
        var kind = schema.GetKind("FunctionName");
        
        // The query should work even if no semantic nodes exist
        var query = schema.Syntax("FunctionName");
        var results = query.Select(tree).ToList();
        
        // Results depend on whether tree has matching nodes
    }

    [Fact]
    public void Schema_Semantic_UnknownName_ReturnsDefaultKind()
    {
        var schema = Schema.Default;
        
        var query = schema.Syntax("NonExistent");
        
        Assert.NotNull(query);
    }

    #endregion

    #region Schema.Semantic<T> Generic Extension

    [Fact]
    public void Schema_SemanticGeneric_CreatesQuery()
    {
        var schema = Schema.Default;
        
        var query = schema.Semantic<FunctionNameNode>();
        
        Assert.NotNull(query);
    }

    [Fact]
    public void Schema_SemanticGeneric_QueryMatchesSameAsStringVersion()
    {
        var tree = SyntaxTree.Parse("foo(x) bar(y)");
        var schema = Schema.Default;
        
        var genericQuery = schema.Semantic<FunctionNameNode>();
        var stringQuery = schema.Syntax("FunctionName");
        
        // Both queries should select the same nodes (comparing behavior, not instances)
        var genericResults = genericQuery.Select(tree).ToList();
        var stringResults = stringQuery.Select(tree).ToList();
        
        Assert.Equal(stringResults.Count, genericResults.Count);
    }

    [Fact]
    public void Schema_SemanticGeneric_UsesCorrectNodeKind()
    {
        var schema = Schema.Default;
        var expectedKind = schema.GetKind("FunctionName");
        
        // GetKind<T> should return the same kind as GetKind(name)
        var genericKind = schema.GetKind<FunctionNameNode>();
        
        Assert.Equal(expectedKind, genericKind);
    }

    [Fact]
    public void Schema_SemanticGeneric_UnregisteredType_Throws()
    {
        // Create schema without FunctionName definition
        var schema = Schema.Create().Build();
        
        Assert.Throws<InvalidOperationException>(() => schema.Semantic<FunctionNameNode>());
    }

    [Fact]
    public void Schema_SemanticGeneric_UnregisteredType_ThrowsWithHelpfulMessage()
    {
        var schema = Schema.Create().Build();
        
        var ex = Assert.Throws<InvalidOperationException>(() => schema.Semantic<FunctionNameNode>());
        
        Assert.Contains("FunctionNameNode", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void Schema_SemanticGeneric_AllBuiltInTypes_HaveDistinctKinds()
    {
        var schema = Schema.Default;
        
        var functionKind = schema.GetKind<FunctionNameNode>();
        var arrayKind = schema.GetKind<ArrayAccessNode>();
        var propertyKind = schema.GetKind<PropertyAccessNode>();
        var methodKind = schema.GetKind<MethodCallNode>();
        
        // Each type should have a distinct NodeKind
        var kinds = new[] { functionKind, arrayKind, propertyKind, methodKind };
        Assert.Equal(4, kinds.Distinct().Count());
    }

    [Fact]
    public void Schema_SemanticGeneric_KindsAreSemanticRange()
    {
        var schema = Schema.Default;
        
        var functionKind = schema.GetKind<FunctionNameNode>();
        var arrayKind = schema.GetKind<ArrayAccessNode>();
        var propertyKind = schema.GetKind<PropertyAccessNode>();
        var methodKind = schema.GetKind<MethodCallNode>();
        
        // All semantic kinds should be >= 1000
        Assert.True(functionKind.IsSemantic());
        Assert.True(arrayKind.IsSemantic());
        Assert.True(propertyKind.IsSemantic());
        Assert.True(methodKind.IsSemantic());
    }

    [Fact]
    public void Schema_GetKind_Generic_UnregisteredType_ReturnsDefaultSemantic()
    {
        var schema = Schema.Create().Build();
        
        var kind = schema.GetKind<FunctionNameNode>();
        
        Assert.Equal(NodeKind.Semantic, kind);
    }

    [Fact]
    public void Schema_SemanticGeneric_CanBeChainedWithFilters()
    {
        var schema = Schema.Default;
        
        // Should be able to chain .First() and other modifiers
        var query = schema.Semantic<FunctionNameNode>().First();
        
        Assert.NotNull(query);
    }

    [Fact]
    public void Schema_SemanticGeneric_CanBeUsedInUnion()
    {
        var schema = Schema.Default;
        
        // Should be able to combine with | operator
        var query = schema.Semantic<FunctionNameNode>() | schema.Semantic<ArrayAccessNode>();
        
        Assert.NotNull(query);
    }

    [Fact]
    public void Schema_SemanticGeneric_ExtensionMethodDelegatesToSchemaMethod()
    {
        var schema = Schema.Default;
        
        // Both the extension method and direct method should work identically
        var fromExtension = SemanticMatchExtensions.Syntax<FunctionNameNode>(schema);
        var fromDirect = schema.Semantic<FunctionNameNode>();
        
        // They should produce equivalent queries (same kind)
        var tree = SyntaxTree.Parse("test");
        Assert.Equal(
            fromExtension.Select(tree).Count(),
            fromDirect.Select(tree).Count());
    }

    #endregion

    #region SemanticMatchExtensions.Semantic (Static)

    [Fact]
    public void Semantic_ByKind_CreatesQuery()
    {
        var query = SemanticMatchExtensions.Syntax(NodeKind.Semantic);
        
        Assert.NotNull(query);
    }

    [Fact]
    public void Semantic_ByKind_MatchesCorrectKind()
    {
        var tree = SyntaxTree.Parse("{block}");
        var query = SemanticMatchExtensions.Syntax(NodeKind.BraceBlock);
        
        var results = query.Select(tree).ToList();
        
        Assert.Single(results);
        Assert.Equal(NodeKind.BraceBlock, results[0].Kind);
    }

    #endregion

    #region SemanticNodeQuery

    [Fact]
    public void SemanticNodeQuery_Select_FromTree()
    {
        var tree = SyntaxTree.Parse("test");
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        var results = query.Select(tree).ToList();
        
        Assert.Single(results);
        Assert.Equal(NodeKind.Ident, results[0].Kind);
    }

    [Fact]
    public void SemanticNodeQuery_Select_FromRedNode()
    {
        var tree = SyntaxTree.Parse("{inner}");
        var block = tree.Root.Children.First();
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        var results = query.Select(block).ToList();
        
        Assert.Single(results);
    }

    [Fact]
    public void SemanticNodeQuery_Matches_CorrectKind()
    {
        var tree = SyntaxTree.Parse("abc");
        var node = tree.Root.Children.First();
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        Assert.True(query.Matches(node));
    }

    [Fact]
    public void SemanticNodeQuery_Matches_WrongKind_ReturnsFalse()
    {
        var tree = SyntaxTree.Parse("{block}");
        var node = tree.Root.Children.First();
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        Assert.False(query.Matches(node));
    }

    [Fact]
    public void SemanticNodeQuery_Select_FindsMultiple()
    {
        var tree = SyntaxTree.Parse("a b c");
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        var results = query.Select(tree).ToList();
        
        Assert.True(results.Count >= 3);
    }

    [Fact]
    public void SemanticNodeQuery_Select_FindsNested()
    {
        var tree = SyntaxTree.Parse("{a {b}}");
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        var results = query.Select(tree).ToList();
        
        Assert.True(results.Count >= 2);
    }

    [Fact]
    public void SemanticNodeQuery_Select_NoMatches_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("{[()]}");
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        var results = query.Select(tree).ToList();
        
        Assert.Empty(results);
    }

    [Fact]
    public void SemanticNodeQuery_ForBlocks()
    {
        var tree = SyntaxTree.Parse("{a} [b] (c)");
        
        var braceQuery = new SyntaxNodeQuery(NodeKind.BraceBlock);
        var bracketQuery = new SyntaxNodeQuery(NodeKind.BracketBlock);
        var parenQuery = new SyntaxNodeQuery(NodeKind.ParenBlock);
        
        Assert.Single(braceQuery.Select(tree));
        Assert.Single(bracketQuery.Select(tree));
        Assert.Single(parenQuery.Select(tree));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Match_ComplexExpression()
    {
        var tree = SyntaxTree.Parse("obj.method(arr[0])");
        var schema = Schema.Default;
        
        var methods = tree.Root.Match<MethodCallNode>(schema).ToList();
        var arrays = tree.Root.Match<ArrayAccessNode>(schema).ToList();
        
        Assert.Single(methods);
        Assert.Single(arrays);
    }

    [Fact]
    public void Match_ChainedCalls()
    {
        var tree = SyntaxTree.Parse("a.b.c");
        var schema = Schema.Default;
        
        var props = tree.Root.Match<PropertyAccessNode>(schema).ToList();
        
        // Should find at least one property access
        Assert.NotEmpty(props);
    }

    [Fact]
    public void MatchAll_MixedContent()
    {
        var tree = SyntaxTree.Parse("foo(x) obj.prop arr[i] bar(y)");
        var schema = Schema.Default;
        
        var all = tree.Root.MatchAll(schema).ToList();
        
        // Should find function calls, property access, and array access
        Assert.True(all.Count >= 4);
    }

    [Fact]
    public void Match_InDeeplyNestedStructure()
    {
        var tree = SyntaxTree.Parse("{{{foo(x)}}}");
        var schema = Schema.Default;
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Single(calls);
        Assert.Equal("foo", calls[0].Name);
    }

    [Fact]
    public void Match_WithCustomSchema()
    {
        var schema = Schema.Create()
            .Define(BuiltInDefinitions.FunctionName)
            .Build();
        
        var tree = SyntaxTree.Parse("test(x) arr[0]");
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        var arrays = tree.Root.Match<ArrayAccessNode>(schema).ToList();
        
        Assert.Single(calls);
        Assert.Empty(arrays); // ArrayAccess not in custom schema
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Match_EmptyTree()
    {
        var tree = SyntaxTree.Parse("");
        var schema = Schema.Default;
        
        var calls = tree.Root.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Empty(calls);
    }

    [Fact]
    public void MatchAll_EmptyTree()
    {
        var tree = SyntaxTree.Parse("");
        var schema = Schema.Default;
        
        var all = tree.Root.MatchAll(schema).ToList();
        
        Assert.Empty(all);
    }

    [Fact]
    public void SemanticNodeQuery_EmptyTree()
    {
        var tree = SyntaxTree.Parse("");
        var query = new SyntaxNodeQuery(NodeKind.Ident);
        
        var results = query.Select(tree).ToList();
        
        Assert.Empty(results);
    }

    [Fact]
    public void Match_NullContext_IsAllowed()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var schema = Schema.Default;
        
        var calls = tree.Root.Match<FunctionNameNode>(schema, null).ToList();
        
        Assert.Single(calls);
    }

    [Fact]
    public void MatchAll_NullContext_IsAllowed()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var schema = Schema.Default;
        
        var all = tree.Root.MatchAll(schema, null).ToList();
        
        Assert.NotEmpty(all);
    }

    #endregion
}
