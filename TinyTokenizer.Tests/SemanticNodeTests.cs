using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

public class SemanticNodeTests
{
    #region NodeKind Extensions
    
    [Fact]
    public void NodeKind_IsLeaf_ReturnsCorrectly()
    {
        Assert.True(NodeKind.Ident.IsLeaf());
        Assert.True(NodeKind.Numeric.IsLeaf());
        Assert.True(NodeKind.String.IsLeaf());
        Assert.False(NodeKind.BraceBlock.IsLeaf());
        Assert.False(NodeKind.Semantic.IsLeaf());
    }
    
    [Fact]
    public void NodeKind_IsContainer_ReturnsCorrectly()
    {
        Assert.False(NodeKind.Ident.IsContainer());
        Assert.True(NodeKind.BraceBlock.IsContainer());
        Assert.True(NodeKind.BracketBlock.IsContainer());
        Assert.True(NodeKind.ParenBlock.IsContainer());
        Assert.False(NodeKind.Semantic.IsContainer());
    }
    
    [Fact]
    public void NodeKind_IsSemantic_ReturnsCorrectly()
    {
        Assert.False(NodeKind.Ident.IsSemantic());
        Assert.False(NodeKind.BraceBlock.IsSemantic());
        Assert.True(NodeKind.Semantic.IsSemantic());
        Assert.True(NodeKindExtensions.SemanticKind(0).IsSemantic());
        Assert.True(NodeKindExtensions.SemanticKind(100).IsSemantic());
    }
    
    [Fact]
    public void NodeKind_SemanticKind_CreatesCorrectValues()
    {
        Assert.Equal((NodeKind)1000, NodeKindExtensions.SemanticKind(0));
        Assert.Equal((NodeKind)1001, NodeKindExtensions.SemanticKind(1));
        Assert.Equal((NodeKind)1005, NodeKindExtensions.SemanticKind(5));
    }
    
    #endregion
    
    #region Sibling Navigation
    
    [Fact]
    public void RedNode_NextSibling_ReturnsCorrectNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        var first = tree.Root.Children.First();
        
        var second = first.NextSibling();
        Assert.NotNull(second);
        Assert.Equal(NodeKind.Ident, second!.Kind);
    }
    
    [Fact]
    public void RedNode_PreviousSibling_ReturnsCorrectNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        var children = tree.Root.Children.ToList();
        var last = children.Last();
        
        var prev = last.PreviousSibling();
        Assert.NotNull(prev);
    }
    
    [Fact]
    public void RedNode_SiblingIndex_ReturnsCorrectIndex()
    {
        var tree = SyntaxTree.Parse("a b c");
        var children = tree.Root.Children.ToList();
        
        Assert.Equal(0, children[0].SiblingIndex);
        Assert.Equal(1, children[1].SiblingIndex);
    }
    
    [Fact]
    public void TreeWalker_FollowingSiblings_EnumeratesCorrectly()
    {
        var tree = SyntaxTree.Parse("a b c d");
        var first = tree.Root.Children.First();
        var walker = new TreeWalker(first);
        var following = walker.FollowingSiblings().ToList();
        Assert.True(following.Count >= 3); // At least b, c, d (may include whitespace)
    }
    
    [Fact]
    public void RedNode_Root_HasNoSiblings()
    {
        var tree = SyntaxTree.Parse("a");
        
        Assert.Equal(-1, tree.Root.SiblingIndex);
        Assert.Null(tree.Root.NextSibling());
        Assert.Null(tree.Root.PreviousSibling());
    }
    
    #endregion
    
    #region NodePattern Matching
    
    [Fact]
    public void QueryPattern_Matches_SingleNode()
    {
        var tree = SyntaxTree.Parse("foo");
        var pattern = new QueryPattern(Query.Ident);
        var node = tree.Root.Children.First();
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Equal(1, match.ConsumedCount);
        Assert.Single(match.Parts);
    }
    
    [Fact]
    public void SequencePattern_Matches_MultipleNodes()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var pattern = NodePattern.Sequence(Query.Ident, Query.ParenBlock);
        var node = tree.Root.Children.First();
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Equal(2, match.ConsumedCount);
        Assert.Equal(2, match.Parts.Length);
    }
    
    [Fact]
    public void SequencePattern_FailsOnPartialMatch()
    {
        var tree = SyntaxTree.Parse("foo bar"); // No paren block
        var pattern = NodePattern.Sequence(Query.Ident, Query.ParenBlock);
        var node = tree.Root.Children.First();
        
        Assert.False(pattern.TryMatch(node, out _));
    }
    
    [Fact]
    public void OptionalPattern_MatchesZeroOccurrences()
    {
        var tree = SyntaxTree.Parse("foo");
        var pattern = NodePattern.Optional(new QueryPattern(Query.ParenBlock));
        var node = tree.Root.Children.First();
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Equal(0, match.ConsumedCount); // Optional didn't match, but that's OK
    }
    
    [Fact]
    public void AlternativePattern_MatchesFirstAlternative()
    {
        var tree = SyntaxTree.Parse("123");
        var pattern = NodePattern.OneOf(
            new QueryPattern(Query.Ident),
            new QueryPattern(Query.Numeric));
        var node = tree.Root.Children.First();
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Single(match.Parts);
    }
    
    [Fact]
    public void PatternBuilder_BuildsSequence()
    {
        var builder = new PatternBuilder();
        var pattern = builder.Ident().ParenBlock().Build();
        
        var tree = SyntaxTree.Parse("foo(x)");
        var node = tree.Root.Children.First();
        
        Assert.True(pattern.TryMatch(node, out var match));
        Assert.Equal(2, match.Parts.Length);
    }
    
    #endregion
    
    #region SemanticNodeDefinition
    
    [Fact]
    public void SemanticNodeDefinition_CanBeBuilt()
    {
        var definition = Semantic.Define<FunctionNameNode>("FunctionName")
            .Match(p => p.Ident().ParenBlock())
            .Create((match, kind) => new FunctionNameNode(match, kind))
            .Build();
        
        Assert.Equal("FunctionName", definition.Name);
        Assert.Single(definition.Patterns);
        Assert.Equal(0, definition.Priority);
    }
    
    [Fact]
    public void SemanticNodeDefinition_WithPriority()
    {
        var definition = Semantic.Define<FunctionNameNode>("FunctionName")
            .Match(p => p.Ident().ParenBlock())
            .Create((match, kind) => new FunctionNameNode(match, kind))
            .WithPriority(10)
            .Build();
        
        Assert.Equal(10, definition.Priority);
    }
    
    [Fact]
    public void SemanticNodeDefinition_MultiplePatterns()
    {
        var definition = Semantic.Define<FunctionNameNode>("FunctionName")
            .Match(p => p.Ident().ParenBlock())
            .Match(p => p.Ident("call").ParenBlock())
            .Create((match, kind) => new FunctionNameNode(match, kind))
            .Build();
        
        Assert.Equal(2, definition.Patterns.Length);
    }
    
    #endregion
    
    #region Schema
    
    [Fact]
    public void Schema_AssignsUniqueNodeKinds()
    {
        var schema = Schema.Create()
            .Define(BuiltInDefinitions.FunctionName)
            .Define(BuiltInDefinitions.ArrayAccess)
            .Build();
        
        var funcKind = schema.GetKind("FunctionName");
        var arrayKind = schema.GetKind("ArrayAccess");
        
        Assert.NotEqual(funcKind, arrayKind);
        Assert.True(funcKind.IsSemantic());
        Assert.True(arrayKind.IsSemantic());
    }
    
    [Fact]
    public void Schema_GetDefinition_ByName()
    {
        var schema = Schema.Create()
            .Define(BuiltInDefinitions.FunctionName)
            .Build();
        
        var def = schema.GetDefinition("FunctionName");
        Assert.NotNull(def);
        Assert.Equal("FunctionName", def!.Name);
    }
    
    [Fact]
    public void Schema_GetDefinition_ByType()
    {
        var schema = Schema.Create()
            .Define(BuiltInDefinitions.FunctionName)
            .Build();
        
        var def = schema.GetDefinition<FunctionNameNode>();
        Assert.NotNull(def);
    }
    
    [Fact]
    public void Schema_Default_HasBuiltInDefinitions()
    {
        var schema = Schema.Default;
        
        Assert.NotNull(schema.GetDefinition("FunctionName"));
        Assert.NotNull(schema.GetDefinition("ArrayAccess"));
        Assert.NotNull(schema.GetDefinition("PropertyAccess"));
        Assert.NotNull(schema.GetDefinition("MethodCall"));
    }
    
    [Fact]
    public void Schema_FromOptions_MigratesTokenizerOptions()
    {
        var options = new TokenizerOptions()
            .WithOperators("++", "--")
            .WithCommentStyles(CommentStyle.CStyleSingleLine);
        
        var schema = Schema.FromOptions(options);
        
        Assert.Contains("++", schema.Operators);
        Assert.Contains("--", schema.Operators);
        Assert.Single(schema.CommentStyles);
    }
    
    #endregion
    
    #region Semantic Matching
    
    [Fact]
    public void Match_FunctionName_FindsCalls()
    {
        var tree = SyntaxTree.Parse("foo(x) bar(y, z)");
        var schema = Schema.Default;
        
        var calls = tree.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Equal(2, calls.Count);
        Assert.Equal("foo", calls[0].Name);
        Assert.Equal("bar", calls[1].Name);
    }
    
    [Fact]
    public void Match_ArrayAccess_FindsIndexers()
    {
        var tree = SyntaxTree.Parse("arr[0] items[i]");
        var schema = Schema.Default;
        
        var accesses = tree.Match<ArrayAccessNode>(schema).ToList();
        
        Assert.Equal(2, accesses.Count);
        Assert.Equal("arr", accesses[0].Target);
        Assert.Equal("items", accesses[1].Target);
    }
    
    [Fact]
    public void Match_PropertyAccess_FindsProperties()
    {
        var tree = SyntaxTree.Parse("obj.prop foo.bar");
        var schema = Schema.Default;
        
        var props = tree.Match<PropertyAccessNode>(schema).ToList();
        
        Assert.Equal(2, props.Count);
        Assert.Equal("obj", props[0].Object);
        Assert.Equal("prop", props[0].Property);
    }
    
    [Fact]
    public void Match_MethodCall_FindsMethods()
    {
        var tree = SyntaxTree.Parse("obj.method(x)");
        var schema = Schema.Default;
        
        var methods = tree.Match<MethodCallNode>(schema).ToList();
        
        Assert.Single(methods);
        Assert.Equal("obj", methods[0].Object);
        Assert.Equal("method", methods[0].Method);
    }
    
    [Fact]
    public void Match_WithContext_PassesContext()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var schema = Schema.Default;
        var context = new SemanticContext { StrictMode = true };
        
        var calls = tree.Match<FunctionNameNode>(schema, context).ToList();
        
        Assert.Single(calls);
    }
    
    [Fact]
    public void MatchAll_ReturnsAllSemanticNodes()
    {
        var tree = SyntaxTree.Parse("foo(x) arr[0]");
        var schema = Schema.Default;
        
        var all = tree.MatchAll(schema).ToList();
        
        Assert.True(all.Count >= 2); // At least function call and array access
    }
    
    [Fact]
    public void Match_NestedInBlock_FindsNested()
    {
        var tree = SyntaxTree.Parse("{ foo(x) }");
        var schema = Schema.Default;
        
        var calls = tree.Match<FunctionNameNode>(schema).ToList();
        
        Assert.Single(calls);
        Assert.Equal("foo", calls[0].Name);
    }
    
    #endregion
    
    #region SemanticNode Properties
    
    [Fact]
    public void FunctionNameNode_Properties()
    {
        var tree = SyntaxTree.Parse("func(a, b)");
        var schema = Schema.Default;
        
        var call = tree.Match<FunctionNameNode>(schema).First();
        
        Assert.Equal("func", call.Name);
        Assert.True(call.Position >= 0);
        Assert.True(call.Width > 0);
        Assert.Equal(1, call.PartCount); // Only captures the ident, not parens
    }
    
    [Fact]
    public void FunctionNameNode_Arguments()
    {
        var tree = SyntaxTree.Parse("func(a, b, c)");
        var schema = Schema.Default;
        
        var call = tree.Match<FunctionNameNode>(schema).First();
        var argsBlock = call.Arguments;
        
        Assert.NotNull(argsBlock);
        Assert.Equal(NodeKind.ParenBlock, argsBlock!.Kind);
    }
    
    [Fact]
    public void SemanticNode_Kind_MatchesSchemaAssignment()
    {
        var tree = SyntaxTree.Parse("foo(x)");
        var schema = Schema.Default;
        
        var call = tree.Match<FunctionNameNode>(schema).First();
        var expectedKind = schema.GetKind("FunctionName");
        
        Assert.Equal(expectedKind, call.Kind);
    }
    
    #endregion
    
    #region SemanticContext
    
    [Fact]
    public void SemanticContext_Services()
    {
        var context = new SemanticContext();
        var service = new TestService();
        
        context.AddService(service);
        
        Assert.Same(service, context.GetService<TestService>());
        Assert.True(context.HasService<TestService>());
        Assert.False(context.HasService<string>());
    }
    
    [Fact]
    public void SemanticContext_GetRequiredService_Throws()
    {
        var context = new SemanticContext();
        
        Assert.Throws<InvalidOperationException>(() => context.GetRequiredService<TestService>());
    }
    
    private class TestService { }
    
    #endregion
    
    #region TreeWalker
    
    [Fact]
    public void TreeWalker_Descendants_EnumeratesAll()
    {
        var tree = SyntaxTree.Parse("a b { c d }");
        var walker = new TreeWalker(tree.Root);
        
        var all = walker.DescendantsAndSelf().ToList();
        
        Assert.True(all.Count > 0);
    }
    
    [Fact]
    public void TreeWalker_WithFilter_FiltersNodes()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        
        var leaves = walker.DescendantsAndSelf().ToList();
        
        Assert.All(leaves, n => Assert.True(n.IsLeaf));
    }
    
    [Fact]
    public void TreeWalker_NextNode_TraversesDepthFirst()
    {
        var tree = SyntaxTree.Parse("a { b }");
        var walker = new TreeWalker(tree.Root);
        
        var nodes = new List<RedNode>();
        while (walker.NextNode() is { } node)
        {
            nodes.Add(node);
        }
        
        Assert.True(nodes.Count > 0);
    }
    
    [Fact]
    public void TreeWalker_CustomFilter_Accepts()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.Ident ? FilterResult.Accept : FilterResult.Skip);
        
        var idents = walker.DescendantsAndSelf().ToList();
        
        Assert.True(idents.Count >= 3);
        Assert.All(idents, n => Assert.Equal(NodeKind.Ident, n.Kind));
    }
    
    #endregion
}
