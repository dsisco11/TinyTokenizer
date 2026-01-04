using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for keyword recognition via Schema.
/// </summary>
public class KeywordTests
{
    #region Schema Configuration
    
    [Fact]
    public void Schema_DefineKeywordCategory_RegistersKeywords()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .Build();
        
        Assert.True(schema.HasKeywords);
        Assert.Equal(1, schema.KeywordCategories.Length);
        Assert.Equal("Types", schema.KeywordCategories[0].Name);
    }
    
    [Fact]
    public void Schema_DefineKeywords_WithPredefinedCategory_RegistersKeywords()
    {
        var typeKeywords = new KeywordCategory("Types", "int", "float", "double", "void");
        
        var schema = Schema.Create()
            .DefineKeywords(typeKeywords)
            .Build();
        
        Assert.True(schema.HasKeywords);
        Assert.NotNull(schema.GetKeywordKind("int"));
        Assert.NotNull(schema.GetKeywordKind("float"));
    }
    
    [Fact]
    public void Schema_GetKeywordKind_ReturnsCorrectKind()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .DefineKeywordCategory("Control", "if", "else")
            .Build();
        
        // Each keyword gets a unique NodeKind in range 1000-99999
        var intKind = schema.GetKeywordKind("int");
        var floatKind = schema.GetKeywordKind("float");
        var ifKind = schema.GetKeywordKind("if");
        
        Assert.NotNull(intKind);
        Assert.NotNull(floatKind);
        Assert.NotNull(ifKind);
        
        Assert.True(intKind!.Value.IsKeyword());
        Assert.True(floatKind!.Value.IsKeyword());
        Assert.True(ifKind!.Value.IsKeyword());
        
        // Different keywords have different kinds
        Assert.NotEqual(intKind, floatKind);
        Assert.NotEqual(intKind, ifKind);
    }
    
    [Fact]
    public void Schema_GetKeywordKind_ReturnsNullForNonKeyword()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        Assert.Null(schema.GetKeywordKind("myVariable"));
        Assert.Null(schema.GetKeywordKind("notAKeyword"));
    }
    
    [Fact]
    public void Schema_GetKeywordInfo_ReturnsKeywordMetadata()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        var intKind = schema.GetKeywordKind("int")!.Value;
        var info = schema.GetKeywordInfo(intKind);
        
        Assert.NotNull(info);
        Assert.Equal("Types", info.Category);
        Assert.Equal("int", info.Keyword);
        Assert.Equal(intKind, info.Kind);
    }
    
    [Fact]
    public void Schema_GetKeywordsInCategory_ReturnsAllKindsInCategory()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "double")
            .DefineKeywordCategory("Control", "if", "else")
            .Build();
        
        var typeKinds = schema.GetKeywordsInCategory("Types");
        var controlKinds = schema.GetKeywordsInCategory("Control");
        
        Assert.Equal(3, typeKinds.Length);
        Assert.Equal(2, controlKinds.Length);
    }
    
    #endregion
    
    #region Case Sensitivity
    
    [Fact]
    public void Schema_CaseSensitiveKeywords_OnlyMatchExactCase()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", caseSensitive: true, "Int", "Float")
            .Build();
        
        Assert.NotNull(schema.GetKeywordKind("Int"));
        Assert.Null(schema.GetKeywordKind("int"));
        Assert.Null(schema.GetKeywordKind("INT"));
    }
    
    [Fact]
    public void Schema_CaseInsensitiveKeywords_MatchAnyCase()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("SqlKeywords", caseSensitive: false, "SELECT", "FROM", "WHERE")
            .Build();
        
        Assert.NotNull(schema.GetKeywordKind("SELECT"));
        Assert.NotNull(schema.GetKeywordKind("select"));
        Assert.NotNull(schema.GetKeywordKind("Select"));
        
        // All should map to the same kind
        var upperKind = schema.GetKeywordKind("SELECT");
        var lowerKind = schema.GetKeywordKind("select");
        var mixedKind = schema.GetKeywordKind("Select");
        
        Assert.Equal(upperKind, lowerKind);
        Assert.Equal(upperKind, mixedKind);
    }
    
    #endregion
    
    #region Parsing with Keywords
    
    [Fact]
    public void SyntaxTree_Parse_WithSchema_RecognizesKeywords()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .DefineKeywordCategory("Control", "if", "return")
            .Build();
        
        var tree = SyntaxTree.Parse("int x = 42;", schema);
        
        // First token should be keyword 'int'
        var firstLeaf = tree.Leaves.First();
        Assert.True(firstLeaf.Kind.IsKeyword());
        Assert.Equal("int", firstLeaf.Text);
    }
    
    [Fact]
    public void SyntaxTree_Parse_NonKeywordIdentifier_RemainsIdent()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        var tree = SyntaxTree.Parse("myVariable = 42;", schema);
        
        // 'myVariable' is not a keyword
        var firstLeaf = tree.Leaves.First();
        Assert.Equal(NodeKind.Ident, firstLeaf.Kind);
        Assert.Equal("myVariable", firstLeaf.Text);
    }
    
    [Fact]
    public void SyntaxTree_Parse_MixedKeywordsAndIdents()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .DefineKeywordCategory("Control", "if", "else", "return")
            .Build();
        
        var tree = SyntaxTree.Parse("int foo() { return 42; }", schema);
        
        var leaves = tree.Leaves.ToList();
        
        // 'int' is a keyword
        Assert.True(leaves[0].Kind.IsKeyword());
        Assert.Equal("int", leaves[0].Text);
        
        // 'foo' is an identifier
        Assert.Equal(NodeKind.Ident, leaves[1].Kind);
        Assert.Equal("foo", leaves[1].Text);
        
        // 'return' is a keyword
        var returnLeaf = leaves.First(l => l.Text == "return");
        Assert.True(returnLeaf.Kind.IsKeyword());
    }
    
    #endregion
    
    #region Query Support
    
    [Fact]
    public void Query_AnyKeyword_MatchesKeywords()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        var tree = SyntaxTree.Parse("int x = 42;", schema);
        
        var keywords = tree.Select(Query.AnyKeyword).ToList();
        
        Assert.Single(keywords);
        Assert.Equal("int", ((RedLeaf)keywords[0]).Text);
    }
    
    [Fact]
    public void Query_Keyword_MatchesSpecificKeyword()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        var tree = SyntaxTree.Parse("int x; float y;", schema);
        
        var intKeywords = tree.Select(Query.Keyword("int")).ToList();
        var floatKeywords = tree.Select(Query.Keyword("float")).ToList();
        
        Assert.Single(intKeywords);
        Assert.Single(floatKeywords);
    }
    
    [Fact]
    public void Query_KeywordCategory_MatchesAllKeywordsInCategory()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .DefineKeywordCategory("Control", "if", "else")
            .Build();
        
        var tree = SyntaxTree.Parse("int foo(); void bar(); if (x) {} else {}", schema);
        
        // Use schema-aware query with KeywordCategory
        var typeKeywords = tree.Select(Query.KeywordCategory("Types")).ToList();
        var controlKeywords = tree.Select(Query.KeywordCategory("Control")).ToList();
        
        Assert.Equal(2, typeKeywords.Count); // int, void
        Assert.Equal(2, controlKeywords.Count); // if, else
    }
    
    #endregion
    
    #region NodeKind Extensions
    
    [Fact]
    public void NodeKind_IsKeyword_ReturnsTrueForKeywordKinds()
    {
        var kind = NodeKindExtensions.KeywordKind(0);
        Assert.True(kind.IsKeyword());
        
        kind = NodeKindExtensions.KeywordKind(100);
        Assert.True(kind.IsKeyword());
        
        kind = NodeKindExtensions.KeywordKind(1499); // Max keyword (500 + 1499 = 1999)
        Assert.True(kind.IsKeyword());
    }
    
    [Fact]
    public void NodeKind_IsKeyword_ReturnsFalseForNonKeywordKinds()
    {
        Assert.False(NodeKind.Ident.IsKeyword());
        Assert.False(NodeKind.Operator.IsKeyword());
        Assert.False(NodeKind.BraceBlock.IsKeyword());
        Assert.False(NodeKind.Semantic.IsKeyword());
        Assert.False(NodeKindExtensions.SemanticKind(0).IsKeyword());
    }
    
    [Fact]
    public void NodeKind_KeywordKind_CreatesCorrectValues()
    {
        // Keywords start at 500
        Assert.Equal((NodeKind)500, NodeKindExtensions.KeywordKind(0));
        Assert.Equal((NodeKind)501, NodeKindExtensions.KeywordKind(1));
        Assert.Equal((NodeKind)600, NodeKindExtensions.KeywordKind(100));
    }
    
    #endregion
    
    #region CommonKeywords Presets
    
    [Fact]
    public void CommonKeywords_CTypes_ContainsExpectedKeywords()
    {
        Assert.Contains("int", CommonKeywords.CTypes.Words);
        Assert.Contains("float", CommonKeywords.CTypes.Words);
        Assert.Contains("double", CommonKeywords.CTypes.Words);
        Assert.Contains("void", CommonKeywords.CTypes.Words);
        Assert.Contains("char", CommonKeywords.CTypes.Words);
    }
    
    [Fact]
    public void CommonKeywords_ControlFlow_ContainsExpectedKeywords()
    {
        Assert.Contains("if", CommonKeywords.ControlFlow.Words);
        Assert.Contains("else", CommonKeywords.ControlFlow.Words);
        Assert.Contains("while", CommonKeywords.ControlFlow.Words);
        Assert.Contains("for", CommonKeywords.ControlFlow.Words);
        Assert.Contains("return", CommonKeywords.ControlFlow.Words);
    }
    
    [Fact]
    public void Schema_WithCommonKeywords_Works()
    {
        var schema = Schema.Create()
            .DefineKeywords(CommonKeywords.CTypes)
            .DefineKeywords(CommonKeywords.ControlFlow)
            .Build();
        
        var tree = SyntaxTree.Parse("int main() { if (x) return 0; }", schema);
        
        var keywords = tree.Select(Query.AnyKeyword).ToList();
        Assert.Equal(3, keywords.Count); // int, if, return
    }
    
    #endregion
    
    #region Round-Trip Serialization
    
    [Fact]
    public void RoundTrip_WithKeywords_PreservesSource()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .DefineKeywordCategory("Control", "if", "else", "return")
            .Build();
        
        var source = "int foo() { return 42; }";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
    }
    
    [Fact]
    public void RoundTrip_WithKeywords_PreservesWhitespace()
    {
        var schema = Schema.Create()
            .DefineKeywords(CommonKeywords.CTypes)
            .DefineKeywords(CommonKeywords.ControlFlow)
            .Build();
        
        var source = @"int   main()
{
    if (x > 0)
        return   x;
    else
        return   0;
}";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
    }
    
    [Fact]
    public void RoundTrip_WithCaseInsensitiveKeywords_PreservesOriginalCase()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Sql", caseSensitive: false, "SELECT", "FROM", "WHERE")
            .Build();
        
        // Parse with mixed case - should preserve original casing
        var source = "SELECT * FROM table WHERE x = 1";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
        
        // Keywords should still be recognized
        var keywords = tree.Select(Query.AnyKeyword).ToList();
        Assert.Equal(3, keywords.Count);
    }
    
    [Fact]
    public void RoundTrip_WithCaseInsensitiveKeywords_PreservesLowerCase()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Sql", caseSensitive: false, "SELECT", "FROM", "WHERE")
            .Build();
        
        var source = "select * from table where x = 1";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
        
        var keywords = tree.Select(Query.AnyKeyword).ToList();
        Assert.Equal(3, keywords.Count);
    }
    
    [Fact]
    public void RoundTrip_MixedKeywordsAndIdentifiers_PreservesAll()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        var source = "int myInt = 42; float myFloat = 3.14;";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
        
        // Verify keywords are recognized
        var keywords = tree.Select(Query.AnyKeyword).Cast<RedLeaf>().ToList();
        Assert.Equal(2, keywords.Count);
        Assert.Equal("int", keywords[0].Text);
        Assert.Equal("float", keywords[1].Text);
        
        // Verify identifiers are still identifiers
        var idents = tree.Select(Query.AnyIdent).Cast<RedLeaf>().ToList();
        Assert.Contains(idents, i => i.Text == "myInt");
        Assert.Contains(idents, i => i.Text == "myFloat");
    }
    
    [Fact]
    public void RoundTrip_WithNestedBlocks_AndKeywords_PreservesStructure()
    {
        var schema = Schema.Create()
            .DefineKeywords(CommonKeywords.CTypes)
            .DefineKeywords(CommonKeywords.ControlFlow)
            .Build();
        
        var source = @"void test() {
    if (a) {
        while (b) {
            return c;
        }
    }
}";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
        
        // Verify structure is correct
        var keywords = tree.Select(Query.AnyKeyword).Cast<RedLeaf>().ToList();
        Assert.Equal(4, keywords.Count); // void, if, while, return
    }
    
    [Fact]
    public void RoundTrip_WithComments_AndKeywords_PreservesBoth()
    {
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
            .DefineKeywords(CommonKeywords.CTypes)
            .DefineKeywords(CommonKeywords.ControlFlow)
            .Build();
        
        var source = @"// Returns the sum
int add(int a, int b) {
    /* simple addition */
    return a + b;
}";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
    }
    
    [Fact]
    public void RoundTrip_WithOperators_AndKeywords_PreservesBoth()
    {
        var schema = Schema.Create()
            .WithOperators(CommonOperators.CFamily)
            .DefineKeywords(CommonKeywords.CTypes)
            .DefineKeywords(CommonKeywords.ControlFlow)
            .Build();
        
        var source = "int x = a++ + --b; if (x >= 0 && y <= 10) return x;";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
    }
    
    [Fact]
    public void RoundTrip_MultipleCategories_PreservesAll()
    {
        var schema = Schema.Create()
            .DefineKeywords(CommonKeywords.CTypes)
            .DefineKeywords(CommonKeywords.ControlFlow)
            .DefineKeywords(CommonKeywords.CppModifiers)
            .DefineKeywords(CommonKeywords.CppMemory)
            .Build();
        
        var source = "public static int GetValue() { if (this == nullptr) return 0; else return value; }";
        var tree = SyntaxTree.Parse(source, schema);
        
        Assert.Equal(source, tree.ToText());
    }
    
    #endregion
}
