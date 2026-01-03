using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for GreenSyntaxNode and RedSyntaxNode classes.
/// </summary>
public class SyntaxNodeTests
{
    #region GreenSyntaxNode Tests
    
    [Fact]
    public void GreenSyntaxNode_CalculatesWidthFromChildren()
    {
        // Arrange: "foo" (width 3) + "()" (width 2) = 5
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "foo");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        // Act
        var syntaxNode = new GreenSyntaxNode(
            NodeKind.Semantic, 
            typeof(FunctionCallSyntax),
            nameLeaf, argsBlock);
        
        // Assert
        Assert.Equal(5, syntaxNode.Width); // "foo" + "()"
        Assert.Equal(2, syntaxNode.SlotCount);
    }
    
    [Fact]
    public void GreenSyntaxNode_GetSlot_ReturnsChildren()
    {
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "test");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        var syntaxNode = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(FunctionCallSyntax),
            nameLeaf, argsBlock);
        
        Assert.Same(nameLeaf, syntaxNode.GetSlot(0));
        Assert.Same(argsBlock, syntaxNode.GetSlot(1));
        Assert.Null(syntaxNode.GetSlot(2));
        Assert.Null(syntaxNode.GetSlot(-1));
    }
    
    [Fact]
    public void GreenSyntaxNode_StoresRedType()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "x");
        var syntaxNode = new GreenSyntaxNode(NodeKind.Semantic, typeof(FunctionCallSyntax), leaf);
        
        Assert.Equal(typeof(FunctionCallSyntax), syntaxNode.RedType);
    }
    
    [Fact]
    public void GreenSyntaxNode_CreatesCorrectRedNode()
    {
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "myFunc");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(FunctionCallSyntax),
            nameLeaf, argsBlock);
        
        var redNode = greenSyntax.CreateRed(null, 0);
        
        Assert.IsType<FunctionCallSyntax>(redNode);
        var funcCall = (FunctionCallSyntax)redNode;
        Assert.Equal("myFunc", funcCall.Name);
    }
    
    #endregion
    
    #region RedSyntaxNode Tests
    
    [Fact]
    public void FunctionCallSyntax_ProvidesTypedAccessToChildren()
    {
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "doSomething");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(FunctionCallSyntax),
            nameLeaf, argsBlock);
        
        var funcCall = (FunctionCallSyntax)greenSyntax.CreateRed(null, 0);
        
        // Test typed accessors
        Assert.NotNull(funcCall.NameNode);
        Assert.Equal("doSomething", funcCall.Name);
        Assert.NotNull(funcCall.Arguments);
        Assert.Equal('(', funcCall.Arguments.Opener);
    }
    
    [Fact]
    public void ArrayAccessSyntax_ProvidesTypedAccessToChildren()
    {
        var targetLeaf = new GreenLeaf(NodeKind.Ident, "arr");
        var indexContent = new GreenLeaf(NodeKind.Numeric, "0");
        var indexBlock = GreenBlock.Create('[', ImmutableArray.Create<GreenNode>(indexContent));
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(ArrayAccessSyntax),
            targetLeaf, indexBlock);
        
        var arrayAccess = (ArrayAccessSyntax)greenSyntax.CreateRed(null, 0);
        
        Assert.Equal("arr", arrayAccess.Target);
        Assert.NotNull(arrayAccess.IndexBlock);
        Assert.Equal('[', arrayAccess.IndexBlock.Opener);
    }
    
    [Fact]
    public void PropertyAccessSyntax_ProvidesTypedAccessToChildren()
    {
        var objLeaf = new GreenLeaf(NodeKind.Ident, "obj");
        var dotLeaf = new GreenLeaf(NodeKind.Symbol, ".");
        var propLeaf = new GreenLeaf(NodeKind.Ident, "property");
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(PropertyAccessSyntax),
            objLeaf, dotLeaf, propLeaf);
        
        var propAccess = (PropertyAccessSyntax)greenSyntax.CreateRed(null, 0);
        
        Assert.Equal("obj", propAccess.Object);
        Assert.Equal(".", propAccess.DotNode.Text);
        Assert.Equal("property", propAccess.Property);
        Assert.Equal("obj.property", propAccess.FullPath);
    }
    
    [Fact]
    public void RedSyntaxNode_Children_EnumeratesAllSlots()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Symbol, ".");
        var child3 = new GreenLeaf(NodeKind.Ident, "b");
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(PropertyAccessSyntax),
            child1, child2, child3);
        
        var redSyntax = (SyntaxNode)greenSyntax.CreateRed(null, 0);
        var children = redSyntax.Children.ToList();
        
        Assert.Equal(3, children.Count);
        Assert.IsType<RedLeaf>(children[0]);
        Assert.IsType<RedLeaf>(children[1]);
        Assert.IsType<RedLeaf>(children[2]);
    }
    
    [Fact]
    public void RedSyntaxNode_Position_IsCorrect()
    {
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "func");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(FunctionCallSyntax),
            nameLeaf, argsBlock);
        
        var funcCall = (FunctionCallSyntax)greenSyntax.CreateRed(null, 10);
        
        Assert.Equal(10, funcCall.Position);
        Assert.Equal(6, funcCall.Width); // "func" + "()"
        Assert.Equal(16, funcCall.EndPosition);
    }
    
    [Fact]
    public void RedSyntaxNode_ChildPositions_AreCalculatedCorrectly()
    {
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "test"); // width 4
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty); // width 2
        
        var greenSyntax = new GreenSyntaxNode(
            NodeKind.Semantic,
            typeof(FunctionCallSyntax),
            nameLeaf, argsBlock);
        
        var funcCall = (FunctionCallSyntax)greenSyntax.CreateRed(null, 100);
        
        Assert.Equal(100, funcCall.NameNode.Position); // First child at syntax node position
        Assert.Equal(104, funcCall.Arguments.Position); // Second child after first (100 + 4)
    }
    
    #endregion
    
    #region SyntaxBinder Tests
    
    [Fact]
    public void SyntaxBinder_WithNoDefinitions_ReturnsUnmodifiedTree()
    {
        var binder = new SyntaxBinder(ImmutableArray<SyntaxNodeDefinition>.Empty);
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        var list = new GreenList(ImmutableArray.Create<GreenNode>(leaf));
        
        var result = binder.Bind(list);
        
        Assert.Same(list, result);
    }
    
    [Fact]
    public void SyntaxNodeDefinitionBuilder_CreatesDefinition()
    {
        var definition = Syntax.Define<FunctionCallSyntax>("FunctionCall")
            .Match(Query.AnyIdent, Query.ParenBlock)
            .WithPriority(10)
            .Build();
        
        Assert.Equal("FunctionCall", definition.Name);
        Assert.Equal(typeof(FunctionCallSyntax), definition.RedType);
        Assert.Equal(10, definition.Priority);
        Assert.Single(definition.Patterns);
    }
    
    #endregion
    
    #region Integration Tests
    
    [Fact]
    public void GreenSyntaxNode_ParticipatesInTreeTraversal()
    {
        // Build a tree with a GreenSyntaxNode
        var funcName = new GreenLeaf(NodeKind.Ident, "foo");
        var args = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        var funcCallGreen = new GreenSyntaxNode(NodeKind.Semantic, typeof(FunctionCallSyntax), funcName, args);
        
        var root = new GreenList(ImmutableArray.Create<GreenNode>(funcCallGreen));
        var redRoot = (RedList)root.CreateRed(null, 0);
        
        // The tree walker should see the syntax node and its children
        var walker = new TreeWalker(redRoot);
        var allNodes = walker.DescendantsAndSelf().ToList();
        
        Assert.Contains(allNodes, n => n is FunctionCallSyntax);
        Assert.Contains(allNodes, n => n is RedLeaf leaf && leaf.Text == "foo");
    }
    
    [Fact]
    public void Query_CanSelectSyntaxNodes()
    {
        // Build a tree with a syntax node
        var funcName = new GreenLeaf(NodeKind.Ident, "bar");
        var args = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        var syntaxKind = NodeKindExtensions.SemanticKind(0);
        var funcCallGreen = new GreenSyntaxNode(syntaxKind, typeof(FunctionCallSyntax), funcName, args);
        
        var root = new GreenList(ImmutableArray.Create<GreenNode>(funcCallGreen));
        
        // Create SyntaxTree from source, then manually test the green structure
        var redRoot = root.CreateRed(null, 0);
        
        // Query by kind should find the syntax node
        var results = Query.Kind(syntaxKind).Select(redRoot).ToList();
        
        Assert.Single(results);
        Assert.IsType<FunctionCallSyntax>(results[0]);
    }
    
    #endregion
    
    #region Schema Integration Tests
    
    [Fact]
    public void Schema_DefineSyntax_RegistersDefinition()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        Assert.Single(schema.SyntaxDefinitions);
        Assert.Equal("FunctionCall", schema.SyntaxDefinitions[0].Name);
        Assert.Equal(typeof(FunctionCallSyntax), schema.SyntaxDefinitions[0].RedType);
    }
    
    [Fact]
    public void Schema_DefineSyntax_AssignsNodeKind()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .DefineSyntax(Syntax.Define<ArrayAccessSyntax>("ArrayAccess")
                .Match(Query.AnyIdent, Query.BracketBlock)
                .Build())
            .Build();
        
        var funcDef = schema.SyntaxDefinitions[0];
        var arrDef = schema.SyntaxDefinitions[1];
        
        // Each definition should get a unique kind
        Assert.NotEqual(funcDef.Kind, arrDef.Kind);
        Assert.True(funcDef.Kind.IsSemantic());
        Assert.True(arrDef.Kind.IsSemantic());
    }
    
    [Fact]
    public void Schema_GetSyntaxDefinition_ByName()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .DefineSyntax(Syntax.Define<ArrayAccessSyntax>("ArrayAccess")
                .Match(Query.AnyIdent, Query.BracketBlock)
                .Build())
            .Build();
        
        var funcDef = schema.GetSyntaxDefinition("FunctionCall");
        var arrDef = schema.GetSyntaxDefinition("ArrayAccess");
        
        Assert.NotNull(funcDef);
        Assert.NotNull(arrDef);
        Assert.Equal(typeof(FunctionCallSyntax), funcDef!.RedType);
        Assert.Equal(typeof(ArrayAccessSyntax), arrDef!.RedType);
    }
    
    [Fact]
    public void Schema_GetSyntaxDefinition_ByType()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        var definition = schema.GetSyntaxDefinition<FunctionCallSyntax>();
        
        Assert.NotNull(definition);
        Assert.Equal("FunctionCall", definition!.Name);
    }
    
    [Fact]
    public void Schema_DefineSyntax_WithBuilderAction()
    {
        var schema = Schema.Create()
            .DefineSyntax<FunctionCallSyntax>("FunctionCall", builder => builder
                .Match(Query.AnyIdent, Query.ParenBlock)
                .WithPriority(10))
            .Build();
        
        var definition = schema.SyntaxDefinitions[0];
        Assert.Equal("FunctionCall", definition.Name);
        Assert.Equal(10, definition.Priority);
    }
    
    [Fact]
    public void SyntaxBinder_WithSchemaDefinitions_CanBind()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        var binder = new SyntaxBinder(schema);
        
        // Verify binder was created with schema definitions
        Assert.NotNull(binder);
    }
    
    #endregion
    
    #region End-to-End Integration Tests
    
    [Fact]
    public void ParseAndBind_RecognizesFunctionCalls()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        var tree = SyntaxTree.ParseAndBind("foo() bar(x)", schema);
        
        // Find all function calls
        var funcCalls = tree.Root.Children
            .Where(n => n is FunctionCallSyntax)
            .Cast<FunctionCallSyntax>()
            .ToList();
        
        Assert.Equal(2, funcCalls.Count);
        Assert.Equal("foo", funcCalls[0].Name);
        Assert.Equal("bar", funcCalls[1].Name);
    }
    
    [Fact]
    public void ParseAndBind_RecognizesNestedPatterns()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        var tree = SyntaxTree.ParseAndBind("outer(inner())", schema);
        
        // Find outer function call
        var outerCall = tree.Root.Children
            .OfType<FunctionCallSyntax>()
            .FirstOrDefault();
        
        Assert.NotNull(outerCall);
        Assert.Equal("outer", outerCall!.Name);
        
        // Find inner function call in arguments
        var innerCall = outerCall.Arguments.Children
            .OfType<FunctionCallSyntax>()
            .FirstOrDefault();
        
        Assert.NotNull(innerCall);
        Assert.Equal("inner", innerCall!.Name);
    }
    
    [Fact]
    public void Tree_Parse_AutoBindsWhenSchemaHasDefinitions()
    {
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        // Parse auto-binds when schema has definitions
        var tree = SyntaxTree.Parse("foo()", schema);
        var funcCall = tree.Root.Children.OfType<FunctionCallSyntax>().FirstOrDefault();
        
        Assert.NotNull(funcCall);
        Assert.Equal("foo", funcCall!.Name);
    }
    
    [Fact]
    public void Tree_Parse_NoAutoBindWhenNoDefinitions()
    {
        var schema = Schema.Create().Build(); // No syntax definitions
        
        var tree = SyntaxTree.Parse("foo()", schema);
        
        // Should not contain FunctionCallSyntax since no definitions
        Assert.DoesNotContain(tree.Root.Children, n => n is FunctionCallSyntax);
    }
    
    [Fact]
    public void Tree_Bind_CanReapplyBindingExplicitly()
    {
        var schemaWithoutDefs = Schema.Create().Build();
        var schemaWithDefs = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        // Parse without definitions
        var tree = SyntaxTree.Parse("foo()", schemaWithoutDefs);
        Assert.DoesNotContain(tree.Root.Children, n => n is FunctionCallSyntax);
        
        // Bind with a different schema that has definitions
        var boundTree = tree.Bind(schemaWithDefs);
        var funcCall = boundTree.Root.Children.OfType<FunctionCallSyntax>().FirstOrDefault();
        
        Assert.NotNull(funcCall);
        Assert.Equal("foo", funcCall!.Name);
    }
    
    #endregion
}
