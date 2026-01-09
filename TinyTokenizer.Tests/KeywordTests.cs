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
        Assert.Equal("int", ((SyntaxToken)keywords[0]).Text);
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
        var keywords = tree.Select(Query.AnyKeyword).Cast<SyntaxToken>().ToList();
        Assert.Equal(2, keywords.Count);
        Assert.Equal("int", keywords[0].Text);
        Assert.Equal("float", keywords[1].Text);
        
        // Verify identifiers are still identifiers
        var idents = tree.Select(Query.AnyIdent).Cast<SyntaxToken>().ToList();
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
        var keywords = tree.Select(Query.AnyKeyword).Cast<SyntaxToken>().ToList();
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
    
    #region Query.Keyword Bug Tests
    
    /// <summary>
    /// BUG: Query.Keyword("int") incorrectly matches any keyword in the same category.
    /// When searching for "int", it should only match "int" tokens, not "float" or "void".
    /// </summary>
    [Fact]
    public void Query_Keyword_ShouldOnlyMatchExactKeyword_NotOthersInSameCategory()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .Build();
        
        // Parse source with multiple type keywords
        var tree = SyntaxTree.Parse("int a; float b; void c;", schema);
        
        // Query for only "int" keyword
        var intKeywords = tree.Select(Query.Keyword("int")).ToList();
        
        // Should only find 1 match (the "int" keyword), not 3 (all type keywords)
        Assert.Single(intKeywords);
        
        // Verify the matched node is actually "int"
        var matchedText = ((SyntaxToken)intKeywords[0]).Text;
        Assert.Equal("int", matchedText);
    }
    
    /// <summary>
    /// BUG: Query.Keyword should not match keywords from other categories.
    /// This is a stricter test to ensure cross-category matching doesn't occur.
    /// </summary>
    [Fact]
    public void Query_Keyword_ShouldNotMatchKeywordsFromOtherCategories()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .DefineKeywordCategory("Control", "if", "else", "for")
            .Build();
        
        // Parse source with keywords from both categories
        var tree = SyntaxTree.Parse("int x; if (x) { float y; } else { for (;;) {} }", schema);
        
        // Query for "int" - should only match "int", not any control flow keywords
        var intMatches = tree.Select(Query.Keyword("int")).ToList();
        Assert.Single(intMatches);
        Assert.Equal("int", ((SyntaxToken)intMatches[0]).Text);
        
        // Query for "if" - should only match "if"
        var ifMatches = tree.Select(Query.Keyword("if")).ToList();
        Assert.Single(ifMatches);
        Assert.Equal("if", ((SyntaxToken)ifMatches[0]).Text);
    }
    
    /// <summary>
    /// Verifies that querying for a non-existent keyword returns no results.
    /// </summary>
    [Fact]
    public void Query_Keyword_NonExistentKeyword_ReturnsEmpty()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float")
            .Build();
        
        var tree = SyntaxTree.Parse("int x; float y;", schema);
        
        // Query for a keyword that doesn't exist in the schema
        var matches = tree.Select(Query.Keyword("double")).ToList();
        
        Assert.Empty(matches);
    }
    
    /// <summary>
    /// Tests Query.Keyword selection - ensure it only matches the specific keyword even when
    /// multiple keywords in the same category exist.
    /// </summary>
    [Fact]
    public void Query_Keyword_First_ShouldOnlyMatchSpecificKeyword()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .Build();
        
        // "float" appears first in source, but we query for "int"
        var tree = SyntaxTree.Parse("float a; int b; void c;", schema);
        
        // Query for "int" - should only match "int", not the first keyword ("float")
        var matches = tree.Select(Query.Keyword("int")).ToList();
        
        Assert.Single(matches);
        Assert.Equal("int", ((SyntaxToken)matches[0]).Text);
    }
    
    /// <summary>
    /// Tests Query.Keyword with SyntaxEditor Replace operation.
    /// Ensures replacing "int" doesn't accidentally replace other keywords in the same category.
    /// </summary>
    [Fact]
    public void Query_Keyword_WithSyntaxEditor_ShouldOnlyReplaceSpecificKeyword()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("Types", "int", "float", "void")
            .Build();
        
        var tree = SyntaxTree.Parse("int a; float b; void c;", schema);
        
        // Verify tree has schema
        Assert.True(tree.HasSchema);
        Assert.Same(schema, tree.Schema);
        
        // Verify keyword resolution works
        var kwQuery = Query.Keyword("int");
        var matches = kwQuery.Select(tree).ToList();
        Assert.Single(matches); // Should find one "int" keyword
        
        // Replace only "int" keywords
        tree.CreateEditor()
            .Replace(Query.Keyword("int"), "long")
            .Commit();
        
        // Should only replace "int" -> "long", leaving "float" and "void" unchanged
        var result = tree.ToText();
        Assert.Equal("long a; float b; void c;", result);
        
        // Verify "float" and "void" are still present as keywords
        var floatMatches = tree.Select(Query.Keyword("float")).ToList();
        var voidMatches = tree.Select(Query.Keyword("void")).ToList();
        Assert.Single(floatMatches);
        Assert.Single(voidMatches);
    }
    
    #endregion
    
    #region Query.Keyword in Syntax Definition Bug
    
    /// <summary>
    /// BUG: Query.Keyword("uniform") in a syntax definition pattern matches ANY keyword 
    /// in the same category, not just "uniform".
    /// 
    /// This causes "in vec3 pos;" to be incorrectly matched as a uniform declaration
    /// when "in" and "uniform" are in the same keyword category (GlslStorageQualifiers).
    /// </summary>
    [Fact]
    public void Query_Keyword_InSyntaxDefinition_ShouldOnlyMatchSpecificKeyword()
    {
        // Define a syntax node for GLSL uniform declarations
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer", "shared",
                "attribute", "varying")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4", "mat4", "float", "int")
            .DefineSyntax(Syntax.Define<GlUniformTestNode>("glUniform")
                .Match(
                    Query.Keyword("uniform"),                      // The "uniform" keyword specifically
                    Query.AnyOf(Query.AnyKeyword, Query.AnyIdent), // Type (keyword or custom type)
                    Query.AnyIdent,                                // Name
                    Query.BracketBlock.Optional(),                 // Optional array [size]
                    Query.Symbol(";")
                )
                .WithPriority(15)
                .Build())
            .Build();
        
        // Parse source that has "in" declaration (NOT uniform)
        var tree = SyntaxTree.Parse("in vec3 pos;", schema);
        
        // Query for uniform nodes - should find NONE because "in" is not "uniform"
        var uniformNodes = tree.Select(Query.Syntax<GlUniformTestNode>()).ToList();
        
        // BUG: This fails because Query.Keyword("uniform") incorrectly matches "in"
        // since they're in the same category
        Assert.Empty(uniformNodes);
    }
    
    /// <summary>
    /// Verifies that Query.Keyword("uniform") correctly matches actual uniform declarations.
    /// </summary>
    [Fact]
    public void Query_Keyword_InSyntaxDefinition_MatchesCorrectKeyword()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer", "shared")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4", "mat4", "float", "int")
            .DefineSyntax(Syntax.Define<GlUniformTestNode>("glUniform")
                .Match(
                    Query.Keyword("uniform"),
                    Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
                    Query.AnyIdent,
                    Query.BracketBlock.Optional(),
                    Query.Symbol(";")
                )
                .WithPriority(15)
                .Build())
            .Build();
        
        // Parse source with actual uniform declaration
        var tree = SyntaxTree.Parse("uniform mat4 modelView;", schema);
        
        // Should find exactly one uniform node
        var uniformNodes = tree.Select(Query.Syntax<GlUniformTestNode>()).ToList();
        Assert.Single(uniformNodes);
    }
    
    /// <summary>
    /// Verifies that different keywords in the same category are distinguished correctly.
    /// </summary>
    [Fact]
    public void Query_Keyword_InSyntaxDefinition_DistinguishesSameCategoryKeywords()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4", "mat4", "float", "int")
            .DefineSyntax(Syntax.Define<GlUniformTestNode>("glUniform")
                .Match(
                    Query.Keyword("uniform"),
                    Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
                    Query.AnyIdent,
                    Query.Symbol(";")
                )
                .WithPriority(15)
                .Build())
            .DefineSyntax(Syntax.Define<GlInputTestNode>("glInput")
                .Match(
                    Query.Keyword("in"),
                    Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
                    Query.AnyIdent,
                    Query.Symbol(";")
                )
                .WithPriority(15)
                .Build())
            .Build();
        
        // Parse source with both uniform and in declarations
        var tree = SyntaxTree.Parse("uniform mat4 model; in vec3 pos; out vec4 color;", schema);
        
        // Should find exactly one uniform node
        var uniformNodes = tree.Select(Query.Syntax<GlUniformTestNode>()).ToList();
        Assert.Single(uniformNodes);
        
        // Should find exactly one input node  
        var inputNodes = tree.Select(Query.Syntax<GlInputTestNode>()).ToList();
        Assert.Single(inputNodes);
        
        // "out vec4 color;" should match neither (no syntax for "out" defined)
    }
    
    /// <summary>
    /// Diagnostic test to verify SpecificKeywordQuery resolution.
    /// </summary>
    [Fact]
    public void Query_Keyword_ResolutionDiagnostic()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer")
            .Build();
        
        // Create keyword queries
        var uniformQuery = Query.Keyword("uniform");
        var inQuery = Query.Keyword("in");
        
        // Before resolution
        Assert.False(uniformQuery.IsResolved);
        Assert.False(inQuery.IsResolved);
        
        // Resolve
        uniformQuery.ResolveWithSchema(schema);
        inQuery.ResolveWithSchema(schema);
        
        // After resolution
        Assert.True(uniformQuery.IsResolved);
        Assert.True(inQuery.IsResolved);
        
        // Get the expected kinds
        var uniformKind = schema.GetKeywordKind("uniform");
        var inKind = schema.GetKeywordKind("in");
        
        Assert.NotNull(uniformKind);
        Assert.NotNull(inKind);
        Assert.NotEqual(uniformKind, inKind); // Different keywords should have different kinds
        
        // Parse source with "in" keyword
        var tree = SyntaxTree.Parse("in vec3 pos;", schema);
        var firstLeaf = tree.Leaves.First();
        
        // The "in" token should have kind = inKind, not uniformKind
        Assert.Equal(inKind, firstLeaf.Kind);
        Assert.NotEqual(uniformKind, firstLeaf.Kind);
        
        // Test green matching
        var inGreen = tree.GreenRoot.GetSlot(0)!;
        Assert.True(inQuery.MatchesGreen(inGreen));
        Assert.False(uniformQuery.MatchesGreen(inGreen)); // Should NOT match!
    }
    
    /// <summary>
    /// Diagnostic test to verify SpecificKeywordQuery resolution inside SequenceQuery.
    /// </summary>
    [Fact]
    public void Query_Keyword_SequenceResolutionDiagnostic()
    {
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4")
            .Build();
        
        // Create a sequence query mimicking the syntax definition
        var uniformKwQuery = Query.Keyword("uniform");
        var seqQuery = Query.Sequence(
            uniformKwQuery,
            Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
            Query.AnyIdent,
            Query.Symbol(";")
        );
        
        // Before resolution
        Assert.False(uniformKwQuery.IsResolved);
        Assert.False(((ISchemaResolvableQuery)seqQuery).IsResolved);
        
        // Resolve the sequence (as SyntaxBinder does)
        ((ISchemaResolvableQuery)seqQuery).ResolveWithSchema(schema);
        
        // After resolution - the nested keyword query should be resolved
        Assert.True(uniformKwQuery.IsResolved);
        Assert.True(((ISchemaResolvableQuery)seqQuery).IsResolved);
        
        // Parse source with "in" keyword
        var tree = SyntaxTree.Parse("in vec3 pos;", schema);
        
        // Get the green children
        var greenChildren = new List<GreenNode>();
        for (int i = 0; i < tree.GreenRoot.SlotCount; i++)
        {
            var slot = tree.GreenRoot.GetSlot(i);
            if (slot != null)
                greenChildren.Add(slot);
        }
        
        // Test if SequenceQuery matches at position 0 (it should NOT)
        var greenQuery = (IGreenNodeQuery)seqQuery;
        var matches = greenQuery.TryMatchGreen(greenChildren, 0, out var consumed);
        
        Assert.False(matches); // Should NOT match because "in" != "uniform"
    }
    
    /// <summary>
    /// Diagnostic test to verify the full SyntaxDefinition flow.
    /// </summary>
    [Fact]
    public void Query_Keyword_SyntaxDefinitionFlowDiagnostic()
    {
        // Build syntax definition BEFORE the schema is fully built
        var syntaxDef = Syntax.Define<GlUniformTestNode>("glUniform")
            .Match(
                Query.Keyword("uniform"),
                Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
                Query.AnyIdent,
                Query.Symbol(";")
            )
            .WithPriority(15)
            .Build();
        
        // Get the pattern from the definition
        var pattern = syntaxDef.Patterns[0];
        Assert.IsType<SequenceQuery>(pattern);
        var seqQuery = (SequenceQuery)pattern;
        
        // The first part should be a SpecificKeywordQuery
        Assert.IsType<SpecificKeywordQuery>(seqQuery.Parts[0]);
        var kwQuery = (SpecificKeywordQuery)seqQuery.Parts[0];
        
        // Before schema is built, the query should NOT be resolved
        Assert.False(kwQuery.IsResolved);
        
        // Now build the schema with the syntax definition
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4")
            .DefineSyntax(syntaxDef)
            .Build();
        
        // The schema should NOT have resolved the query yet
        // (resolution happens during binding, not during schema construction)
        Assert.False(kwQuery.IsResolved);
        
        // Now manually resolve as SyntaxBinder would
        ((ISchemaResolvableQuery)seqQuery).ResolveWithSchema(schema);
        
        // NOW it should be resolved
        Assert.True(kwQuery.IsResolved);
        
        // Parse and test
        var tree = SyntaxTree.Parse("in vec3 pos;", schema);
        var greenChildren = new List<GreenNode>();
        for (int i = 0; i < tree.GreenRoot.SlotCount; i++)
        {
            var slot = tree.GreenRoot.GetSlot(i);
            if (slot != null)
                greenChildren.Add(slot);
        }
        
        var greenQuery = (IGreenNodeQuery)seqQuery;
        var matches = greenQuery.TryMatchGreen(greenChildren, 0, out var consumed);
        
        Assert.False(matches); // "in" != "uniform"
    }
    
    /// <summary>
    /// Diagnostic test to check if SyntaxBinder correctly resolves queries.
    /// </summary>
    [Fact]
    public void Query_Keyword_SyntaxBinderResolutionDiagnostic()
    {
        // Build syntax definition
        var syntaxDef = Syntax.Define<GlUniformTestNode>("glUniform")
            .Match(
                Query.Keyword("uniform"),
                Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
                Query.AnyIdent,
                Query.Symbol(";")
            )
            .WithPriority(15)
            .Build();
        
        // Get the pattern and keyword query
        var pattern = (SequenceQuery)syntaxDef.Patterns[0];
        var kwQuery = (SpecificKeywordQuery)pattern.Parts[0];
        
        // Before parsing
        Assert.False(kwQuery.IsResolved);
        
        // Now build schema and parse
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4")
            .DefineSyntax(syntaxDef)
            .Build();
        
        // Parse - this triggers SyntaxBinder internally
        var tree = SyntaxTree.Parse("in vec3 pos;", schema);
        
        // After parsing, the query SHOULD be resolved by SyntaxBinder
        Assert.True(kwQuery.IsResolved); // THIS IS THE KEY ASSERTION
        
        // If resolved, then matching should work correctly
        // Query for uniform nodes - should find NONE
        var uniformNodes = tree.Select(Query.Syntax<GlUniformTestNode>()).ToList();
        Assert.Empty(uniformNodes);
    }
    
    /// <summary>
    /// Test to verify that SyntaxNodeDefinition.WithKind preserves query references.
    /// </summary>
    [Fact]
    public void SyntaxNodeDefinition_WithKind_PreservesQueryReferences()
    {
        var kwQuery = Query.Keyword("uniform");
        var syntaxDef = Syntax.Define<GlUniformTestNode>("glUniform")
            .Match(kwQuery, Query.AnyIdent, Query.Symbol(";"))
            .Build();
        
        var newDef = syntaxDef.WithKind(NodeKind.Semantic);
        
        // ImmutableArray is a struct - check if the content is equal
        Assert.Equal(syntaxDef.Patterns.Length, newDef.Patterns.Length);
        
        // The query INSIDE the array should be the SAME instance
        var origSeq = syntaxDef.Patterns[0];
        var newSeq = newDef.Patterns[0];
        Assert.Same(origSeq, newSeq); // THIS IS THE KEY - are the query instances shared?
        
        // Check the keyword query is shared
        var origSeqTyped = (SequenceQuery)origSeq;
        var origKw = origSeqTyped.Parts[0];
        Assert.Same(kwQuery, origKw); // Verify it's the same as what we created
    }
    
    /// <summary>
    /// Detailed diagnostic: Check if SyntaxBinder resolves queries when definitions are created inline.
    /// </summary>
    [Fact]
    public void Query_Keyword_InlineDefinition_ResolutionDiagnostic()
    {
        // This mimics the exact pattern from the failing tests - inline definition
        var schema = Schema.Create()
            .DefineKeywordCategory("GlslStorageQualifiers", 
                "const", "in", "out", "inout", "uniform", "buffer")
            .DefineKeywordCategory("GlslTypes",
                "vec2", "vec3", "vec4")
            .DefineSyntax(Syntax.Define<GlUniformTestNode>("glUniform")
                .Match(
                    Query.Keyword("uniform"),
                    Query.AnyOf(Query.AnyKeyword, Query.AnyIdent),
                    Query.AnyIdent,
                    Query.Symbol(";")
                )
                .WithPriority(15)
                .Build())
            .Build();
        
        // Get the pattern from the schema (after WithKind transformation)
        var syntaxDef = schema.GetSyntaxDefinition<GlUniformTestNode>();
        Assert.NotNull(syntaxDef);
        
        var pattern = syntaxDef.Patterns[0];
        Assert.IsType<SequenceQuery>(pattern);
        var seqQuery = (SequenceQuery)pattern;
        
        var firstPart = seqQuery.Parts[0];
        Assert.IsType<SpecificKeywordQuery>(firstPart);
        var kwQuery = (SpecificKeywordQuery)firstPart;
        
        // BEFORE parsing, the keyword query should NOT be resolved yet
        // (resolution happens during SyntaxBinder.TryMatchDefinition)
        // But wait - if we're checking AFTER schema.Build(), the queries
        // shouldn't be resolved yet since we haven't parsed anything
        
        // Record the initial state
        var wasResolvedBeforeParse = kwQuery.IsResolved;
        
        // Now parse - this should trigger SyntaxBinder which should resolve the queries
        var tree = SyntaxTree.Parse("in vec3 pos;", schema);
        
        // AFTER parsing, the keyword query SHOULD be resolved
        var isResolvedAfterParse = kwQuery.IsResolved;
        
        // Output for debugging
        Assert.False(wasResolvedBeforeParse); // Should not be resolved before parse
        Assert.True(isResolvedAfterParse);    // SHOULD be resolved after parse
        
        // If resolved correctly, matching should work
        var uniformNodes = tree.Select(Query.Syntax<GlUniformTestNode>()).ToList();
        Assert.Empty(uniformNodes); // "in" != "uniform"
    }
    
    #endregion
}

#region Test Syntax Nodes for Keyword Bug Tests

/// <summary>Test syntax node for GLSL uniform declarations.</summary>
public sealed class GlUniformTestNode : SyntaxNode
{
    internal GlUniformTestNode(CreationContext context) : base(context) { }
}

/// <summary>Test syntax node for GLSL input declarations.</summary>
public sealed class GlInputTestNode : SyntaxNode
{
    internal GlInputTestNode(CreationContext context) : base(context) { }
}

#endregion
