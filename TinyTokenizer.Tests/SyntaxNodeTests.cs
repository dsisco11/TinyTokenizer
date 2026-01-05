using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for GreenSyntaxNode and SyntaxNode classes.
/// </summary>
[Trait("Category", "AST")]
public class SyntaxNodeTests
{
    #region Test Helpers
    
    /// <summary>
    /// Creates a schema with FunctionCallSyntax, ArrayAccessSyntax, and PropertyAccessSyntax definitions.
    /// </summary>
    private static Schema CreateTestSchema()
    {
        return Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .DefineSyntax(Syntax.Define<ArrayAccessSyntax>("ArrayAccess")
                .Match(Query.AnyIdent, Query.BracketBlock)
                .Build())
            .DefineSyntax(Syntax.Define<PropertyAccessSyntax>("PropertyAccess")
                .Match(Query.AnyIdent, Query.Symbol("."), Query.AnyIdent)
                .Build())
            .Build();
    }
    
    #endregion
    
    #region GreenSyntaxNode Tests
    
    [Fact]
    public void GreenSyntaxNode_CalculatesWidthFromChildren()
    {
        // Arrange: "foo" (width 3) + "()" (width 2) = 5
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "foo");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        var kind = NodeKindExtensions.SemanticKind(0);
        
        // Act
        var syntaxNode = new GreenSyntaxNode(kind, nameLeaf, argsBlock);
        
        // Assert
        Assert.Equal(5, syntaxNode.Width); // "foo" + "()"
        Assert.Equal(2, syntaxNode.SlotCount);
    }
    
    [Fact]
    public void GreenSyntaxNode_GetSlot_ReturnsChildren()
    {
        var nameLeaf = new GreenLeaf(NodeKind.Ident, "test");
        var argsBlock = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        var kind = NodeKindExtensions.SemanticKind(0);
        
        var syntaxNode = new GreenSyntaxNode(kind, nameLeaf, argsBlock);
        
        Assert.Same(nameLeaf, syntaxNode.GetSlot(0));
        Assert.Same(argsBlock, syntaxNode.GetSlot(1));
        Assert.Null(syntaxNode.GetSlot(2));
        Assert.Null(syntaxNode.GetSlot(-1));
    }
    
    [Fact]
    public void GreenSyntaxNode_NodeKind_IsSemantic()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "x");
        var kind = NodeKindExtensions.SemanticKind(42);
        var syntaxNode = new GreenSyntaxNode(kind, leaf);
        
        Assert.Equal(kind, syntaxNode.Kind);
        Assert.True(syntaxNode.Kind.IsSemantic());
    }
    
    #endregion
    
    #region SyntaxNode Tests (via Schema binding)
    
    [Fact]
    public void FunctionCallSyntax_ProvidesTypedAccessToChildren()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("doSomething()", schema);
        
        var funcCall = tree.Root.Children.OfType<FunctionCallSyntax>().First();
        
        // Test typed accessors
        Assert.NotNull(funcCall.NameNode);
        Assert.Equal("doSomething", funcCall.Name);
        Assert.NotNull(funcCall.Arguments);
        Assert.Equal('(', funcCall.Arguments.Opener);
    }
    
    [Fact]
    public void ArrayAccessSyntax_ProvidesTypedAccessToChildren()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("arr[0]", schema);
        
        var arrayAccess = tree.Root.Children.OfType<ArrayAccessSyntax>().First();
        
        Assert.Equal("arr", arrayAccess.Target);
        Assert.NotNull(arrayAccess.IndexBlock);
        Assert.Equal('[', arrayAccess.IndexBlock.Opener);
    }
    
    [Fact]
    public void PropertyAccessSyntax_ProvidesTypedAccessToChildren()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("obj.property", schema);
        
        var propAccess = tree.Root.Children.OfType<PropertyAccessSyntax>().First();
        
        Assert.Equal("obj", propAccess.Object);
        Assert.Equal(".", propAccess.DotNode.Text);
        Assert.Equal("property", propAccess.Property);
        Assert.Equal("obj.property", propAccess.FullPath);
    }
    
    [Fact]
    public void SyntaxNode_Children_EnumeratesAllSlots()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("a.b", schema);
        
        var propAccess = tree.Root.Children.OfType<PropertyAccessSyntax>().First();
        var children = propAccess.Children.ToList();
        
        Assert.Equal(3, children.Count);
        Assert.IsType<SyntaxToken>(children[0]);
        Assert.IsType<SyntaxToken>(children[1]);
        Assert.IsType<SyntaxToken>(children[2]);
    }
    
    [Fact]
    public void SyntaxNode_Position_IsCorrect()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("func()", schema);
        
        var funcCall = tree.Root.Children.OfType<FunctionCallSyntax>().First();
        
        Assert.Equal(0, funcCall.Position);
        Assert.Equal(6, funcCall.Width); // "func" + "()"
        Assert.Equal(6, funcCall.EndPosition);
    }
    
    [Fact]
    public void SyntaxNode_ChildPositions_AreCalculatedCorrectly()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("test()", schema);
        
        var funcCall = tree.Root.Children.OfType<FunctionCallSyntax>().First();
        
        Assert.Equal(0, funcCall.NameNode.Position); // First child at syntax node position
        Assert.Equal(4, funcCall.Arguments.Position); // Second child after first (0 + 4)
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
    public void SyntaxNode_ParticipatesInTreeTraversal()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("foo()", schema);
        
        // The tree walker should see the syntax node and its children
        var walker = new TreeWalker(tree.Root);
        var allNodes = walker.DescendantsAndSelf().ToList();
        
        Assert.Contains(allNodes, n => n is FunctionCallSyntax);
        Assert.Contains(allNodes, n => n is SyntaxToken leaf && leaf.Text == "foo");
    }
    
    [Fact]
    public void Query_CanSelectSyntaxNodes()
    {
        var schema = CreateTestSchema();
        var tree = SyntaxTree.Parse("bar()", schema);
        
        var funcDef = schema.GetSyntaxDefinition<FunctionCallSyntax>()!;
        
        // Query by kind should find the syntax node
        var results = Query.Kind(funcDef.Kind).Select(tree).ToList();
        
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
    
    [Fact]
    public void SyntaxBinder_Rebind_DoesNotDoubleWrapSyntaxNodes()
    {
        // Regression test: After edits, rebinding should not double-wrap syntax nodes.
        // The bug manifested as "Expected child at slot 0 to be X, but got SyntaxNode"
        // because GreenSyntaxNode was getting wrapped inside another GreenSyntaxNode.
        
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        // Parse and bind - creates FunctionCallSyntax
        var tree = SyntaxTree.Parse("foo()", schema);
        
        // Verify initial binding worked
        var funcCall = tree.Root.Children.OfType<FunctionCallSyntax>().FirstOrDefault();
        Assert.NotNull(funcCall);
        Assert.Equal("foo", funcCall!.Name);
        
        // Make an edit that triggers rebinding
        tree.CreateEditor()
            .Insert(Query.Syntax<FunctionCallSyntax>().After(), " bar()")
            .Commit();
        
        // After rebinding, the original FunctionCallSyntax should still be valid
        // (not double-wrapped in another syntax node)
        var funcCalls = tree.Root.Children.OfType<FunctionCallSyntax>().ToList();
        
        Assert.Equal(2, funcCalls.Count);
        Assert.Equal("foo", funcCalls[0].Name); // Should not throw "Expected child at slot 0..."
        Assert.Equal("bar", funcCalls[1].Name);
    }
    
    [Fact]
    public void SyntaxBinder_MultipleRebinds_MaintainsCorrectStructure()
    {
        // Test multiple edit/rebind cycles don't corrupt tree structure
        
        var schema = Schema.Create()
            .DefineSyntax(Syntax.Define<FunctionCallSyntax>("FunctionCall")
                .Match(Query.AnyIdent, Query.ParenBlock)
                .Build())
            .Build();
        
        var tree = SyntaxTree.Parse("foo()", schema);
        
        // First edit
        tree.CreateEditor()
            .Insert(Query.Syntax<FunctionCallSyntax>().After(), " bar()")
            .Commit();
        
        // Verify after first edit
        var funcCallsAfterFirst = tree.Root.Children.OfType<FunctionCallSyntax>().ToList();
        Assert.Equal(2, funcCallsAfterFirst.Count);
        Assert.Equal("foo", funcCallsAfterFirst[0].Name);
        Assert.Equal("bar", funcCallsAfterFirst[1].Name);
        
        // Second edit - replace content of first function call
        tree.CreateEditor()
            .Replace(Query.Syntax<FunctionCallSyntax>().First(), "baz()")
            .Commit();
        
        // Verify after second edit
        var funcCallsAfterSecond = tree.Root.Children.OfType<FunctionCallSyntax>().ToList();
        Assert.Equal(2, funcCallsAfterSecond.Count);
        Assert.Equal("baz", funcCallsAfterSecond[0].Name);
        Assert.Equal("bar", funcCallsAfterSecond[1].Name);
        
        // Third edit - insert at beginning
        tree.CreateEditor()
            .Insert(Query.Syntax<FunctionCallSyntax>().First().Before(), "qux() ")
            .Commit();
        
        // Verify after third edit
        var funcCallsAfterThird = tree.Root.Children.OfType<FunctionCallSyntax>().ToList();
        Assert.Equal(3, funcCallsAfterThird.Count);
        Assert.Equal("qux", funcCallsAfterThird[0].Name);
        Assert.Equal("baz", funcCallsAfterThird[1].Name);
        Assert.Equal("bar", funcCallsAfterThird[2].Name);
    }
    
    #endregion
}
