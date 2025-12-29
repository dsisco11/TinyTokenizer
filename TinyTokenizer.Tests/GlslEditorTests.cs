using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// End-to-end tests for editing GLSL shaders using the SyntaxTree and SyntaxEditor APIs.
/// Demonstrates:
/// - Parsing GLSL with syntax binding for function recognition
/// - Inserting comments and code at various positions
/// - Working with function definitions and bodies
/// </summary>
public class GlslEditorTests
{
    #region Custom Syntax Nodes for GLSL
    
    /// <summary>
    /// A GLSL function definition: type name(params) { body }
    /// Pattern: Ident + Ident + ParenBlock + BraceBlock
    /// Example: void main() { ... }
    /// </summary>
    public sealed class GlslFunctionSyntax : SyntaxNode
    {
        public GlslFunctionSyntax(GreenSyntaxNode green, RedNode? parent, int position)
            : base(green, parent, position) { }
        
        /// <summary>Return type node (e.g., "void", "vec4").</summary>
        public RedLeaf ReturnTypeNode => GetTypedChild<RedLeaf>(0);
        
        /// <summary>Return type as text.</summary>
        public string ReturnType => ReturnTypeNode.Text;
        
        /// <summary>Function name node.</summary>
        public RedLeaf NameNode => GetTypedChild<RedLeaf>(1);
        
        /// <summary>Function name as text.</summary>
        public string Name => NameNode.Text;
        
        /// <summary>Parameter list block (parentheses).</summary>
        public RedBlock Parameters => GetTypedChild<RedBlock>(2);
        
        /// <summary>Function body block (braces).</summary>
        public RedBlock Body => GetTypedChild<RedBlock>(3);
    }
    
    /// <summary>
    /// A GLSL tagged directive: #version, #define, etc.
    /// Pattern: TaggedIdent + rest of line (we just match the tag for now)
    /// </summary>
    public sealed class GlslDirectiveSyntax : SyntaxNode
    {
        public GlslDirectiveSyntax(GreenSyntaxNode green, RedNode? parent, int position)
            : base(green, parent, position) { }
        
        /// <summary>The directive tag node (e.g., "#version").</summary>
        public RedLeaf DirectiveNode => GetTypedChild<RedLeaf>(0);

        public RedLeaf ArgumentsNode => GetTypedChild<RedLeaf>(1);
        
        /// <summary>The directive name without # (e.g., "version").</summary>
        public string Name => DirectiveNode.Text.TrimStart('#');

        /// <summary> The directive arguments as text. </summary>
        public string Arguments => ArgumentsNode.Text;
    }
    
    #endregion
    
    #region Test Schema
    
    /// <summary>
    /// Creates a GLSL schema with function definition recognition.
    /// </summary>
    private static Schema CreateGlslSchema()
    {
        return Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
            .WithOperators(CommonOperators.CFamily)
            .WithTagPrefixes('#', '@')
            // Function definition: type name(params) { body }
            .DefineSyntax(Syntax.Define<GlslFunctionSyntax>("GlslFunction")
                .Match(Query.Ident, Query.Ident, Query.ParenBlock, Query.BraceBlock)
                .WithPriority(10)
                .Build())
            // Directive: #tag ...
            .DefineSyntax(Syntax.Define<GlslDirectiveSyntax>("GlslDirective")
                .Match(Query.TaggedIdent, Query.Any)
                .Build())
            .Build();
    }
    
    /// <summary>
    /// Sample GLSL shader for testing.
    /// </summary>
    private const string SampleShader = @"#version 330 core

uniform sampler2D tex;
in vec2 uv;
out vec4 fragColor;

vec4 foo(vec2 coord) {
    return texture(tex, coord);
}

void main() {
    vec4 color = foo(uv);
}
";
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Finds a function by name in the tree.
    /// </summary>
    private static GlslFunctionSyntax? FindFunction(SyntaxTree tree, string name)
    {
        var walker = new TreeWalker(tree.Root);
        return walker.DescendantsAndSelf()
            .OfType<GlslFunctionSyntax>()
            .FirstOrDefault(f => f.Name == name);
    }
    
    /// <summary>
    /// Finds a tagged directive by name (e.g., "version").
    /// </summary>
    private static RedLeaf? FindDirective(SyntaxTree tree, string directiveName)
    {
        var walker = new TreeWalker(tree.Root);
        return walker.DescendantsAndSelf()
            .OfType<RedLeaf>()
            .FirstOrDefault(n => n.Kind == NodeKind.TaggedIdent && 
                                  n.Text.StartsWith("#" + directiveName));
    }
    
    #endregion
    
    #region Basic Parsing Tests
    
    [Fact]
    public void Parse_RecognizesGlslFunctions()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var fooFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "foo");

        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        var fooFunc = tree.Select(fooFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        
        Assert.NotNull(mainFunc);
        Assert.NotNull(fooFunc);
        Assert.Equal("void", mainFunc!.ReturnType);
        Assert.Equal("vec4", fooFunc!.ReturnType);
    }
    
    [Fact]
    public void Parse_RecognizesVersionDirective()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var versionDirectiveQuery = Query.Syntax<GlslDirectiveSyntax>().Where(d => d.Name == "version");
        var versionDirective = tree.Select(versionDirectiveQuery).FirstOrDefault() as GlslDirectiveSyntax;
        
        Assert.NotNull(versionDirective);
        Assert.Equal("#version 330", versionDirective!.ToString());
    }
    
    #endregion
    
    #region 1. Insert Comment Above main() Method
    
    [Fact]
    public void InsertCommentAboveMainMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFunc = FindFunction(tree, "main");
        Assert.NotNull(mainFunc);
        
        // Create a query that finds the main function by checking the name
        var mainQuery = Query.Kind(mainFunc!.Kind)
            .Where(n => n is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainQuery.Before(), "// Entry point for the fragment shader\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        Assert.Contains("// Entry point for the fragment shader", result);
        Assert.Contains("// Entry point for the fragment shader\nvoid main()", result);
    }
    
    #endregion
    
    #region 2. Insert Sample-From-Texture at Top of main()
    
    [Fact]
    public void InsertSampleFromTextureAtTopOfMain()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFunc = FindFunction(tree, "main");
        Assert.NotNull(mainFunc);
        
        // Insert at the inner start of main's body
        var mainBodyQuery = Query.BraceBlock
            .Where(n => n.Parent is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainBodyQuery.InnerStart(), "\n    vec4 sample = texture(tex, uv);")
            .Commit();
        
        var result = tree.Root.ToString();
        
        Assert.Contains("vec4 sample = texture(tex, uv);", result);
        // Verify it appears after the opening brace of main
        var mainIndex = result.IndexOf("void main()");
        var sampleIndex = result.IndexOf("vec4 sample = texture(tex, uv);");
        Assert.True(sampleIndex > mainIndex, "Sample should be after main declaration");
    }
    
    #endregion
    
    #region 3. Insert Write-to-Out-Buffer at End of main()
    
    [Fact]
    public void InsertWriteToOutBufferAtEndOfMain()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFunc = FindFunction(tree, "main");
        Assert.NotNull(mainFunc);
        
        // Insert at the inner end of main's body (before closing brace)
        var mainBodyQuery = Query.BraceBlock
            .Where(n => n.Parent is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainBodyQuery.InnerEnd(), "\n    fragColor = color;")
            .Commit();
        
        var result = tree.Root.ToString();
        
        Assert.Contains("fragColor = color;", result);
        // Verify it appears before the closing brace of main
        var fragColorIndex = result.IndexOf("fragColor = color;");
        var mainEndIndex = result.LastIndexOf("}"); // Last brace should be main's
        Assert.True(fragColorIndex < mainEndIndex, "fragColor assignment should be before closing brace");
    }
    
    #endregion
    
    #region 4. Insert Comment After main() Method
    
    [Fact]
    public void InsertCommentAfterMainMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFunc = FindFunction(tree, "main");
        Assert.NotNull(mainFunc);
        
        var mainQuery = Query.Kind(mainFunc!.Kind)
            .Where(n => n is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainQuery.After(), "\n// End of main function\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        Assert.Contains("// End of main function", result);
        // Should appear after main's closing brace
        var mainBodyEnd = result.IndexOf("vec4 color = foo(uv);");
        var commentIndex = result.IndexOf("// End of main function");
        Assert.True(commentIndex > mainBodyEnd, "Comment should be after main body");
    }
    
    #endregion
    
    #region 5. Insert Import Below Version Directive
    
    [Fact]
    public void InsertImportBelowVersionDirective()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var versionDirective = FindDirective(tree, "version");
        Assert.NotNull(versionDirective);
        
        // Find the numeric literal after #version (330) and insert after it
        // We need to insert after the entire #version line
        // Since we have #version 330 core, let's find "core" and insert after it
        var coreIdent = new TreeWalker(tree.Root)
            .DescendantsAndSelf()
            .OfType<RedLeaf>()
            .FirstOrDefault(n => n.Text == "core");
        
        Assert.NotNull(coreIdent);
        
        var coreQuery = Query.Ident.WithText("core").First();
        
        tree.CreateEditor()
            .Insert(coreQuery.After(), "\n@import \"my-include.glsl\"")
            .Commit();
        
        var result = tree.Root.ToString();
        
        Assert.Contains("@import \"my-include.glsl\"", result);
        // Verify order: #version comes before @import
        var versionIndex = result.IndexOf("#version");
        var importIndex = result.IndexOf("@import");
        Assert.True(importIndex > versionIndex, "@import should be after #version");
        
        // @import should be before uniform
        var uniformIndex = result.IndexOf("uniform");
        Assert.True(importIndex < uniformIndex, "@import should be before uniform");
    }
    
    #endregion
    
    #region 6. Insert Comment Above foo() Method
    
    [Fact]
    public void InsertCommentAboveFooMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var fooFunc = FindFunction(tree, "foo");
        Assert.NotNull(fooFunc);
        
        var fooQuery = Query.Kind(fooFunc!.Kind)
            .Where(n => n is GlslFunctionSyntax f && f.Name == "foo");
        
        tree.CreateEditor()
            .Insert(fooQuery.Before(), "// Helper function to sample texture\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        Assert.Contains("// Helper function to sample texture", result);
        Assert.Contains("// Helper function to sample texture\nvec4 foo", result);
    }
    
    #endregion
    
    #region Combined Edit Test
    
    [Fact]
    public void ApplyAllEditsInSingleCommit()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFunc = FindFunction(tree, "main");
        var fooFunc = FindFunction(tree, "foo");
        var coreIdent = new TreeWalker(tree.Root)
            .DescendantsAndSelf()
            .OfType<RedLeaf>()
            .FirstOrDefault(n => n.Text == "core");
        
        Assert.NotNull(mainFunc);
        Assert.NotNull(fooFunc);
        Assert.NotNull(coreIdent);
        
        var mainQuery = Query.Kind(mainFunc!.Kind)
            .Where(n => n is GlslFunctionSyntax f && f.Name == "main");
        var fooQuery = Query.Kind(fooFunc!.Kind)
            .Where(n => n is GlslFunctionSyntax f && f.Name == "foo");
        var mainBodyQuery = Query.BraceBlock
            .Where(n => n.Parent is GlslFunctionSyntax f && f.Name == "main");
        var coreQuery = Query.Ident.WithText("core").First();
        
        tree.CreateEditor()
            // 1. Comment above main
            .Insert(mainQuery.Before(), "// Entry point for the fragment shader\n")
            // 2. Sample from texture at top of main
            .Insert(mainBodyQuery.InnerStart(), "\n    vec4 sample = texture(tex, uv);")
            // 3. Write to out buffer at end of main
            .Insert(mainBodyQuery.InnerEnd(), "\n    fragColor = sample;")
            // 4. Comment after main
            .Insert(mainQuery.After(), "\n// End of main function\n")
            // 5. Import below #version
            .Insert(coreQuery.After(), "\n@import \"my-include.glsl\"")
            // 6. Comment above foo
            .Insert(fooQuery.Before(), "// Helper function to sample texture\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Verify all edits were applied
        Assert.Contains("// Entry point for the fragment shader", result);
        Assert.Contains("vec4 sample = texture(tex, uv);", result);
        Assert.Contains("fragColor = sample;", result);
        Assert.Contains("// End of main function", result);
        Assert.Contains("@import \"my-include.glsl\"", result);
        Assert.Contains("// Helper function to sample texture", result);
        
        // Verify order
        var lines = result.Split('\n');
        var versionLineIdx = Array.FindIndex(lines, l => l.Contains("#version"));
        var importLineIdx = Array.FindIndex(lines, l => l.Contains("@import"));
        var fooCommentIdx = Array.FindIndex(lines, l => l.Contains("// Helper function"));
        var mainCommentIdx = Array.FindIndex(lines, l => l.Contains("// Entry point"));
        var endCommentIdx = Array.FindIndex(lines, l => l.Contains("// End of main"));
        
        Assert.True(importLineIdx > versionLineIdx, "@import should be after #version");
        Assert.True(fooCommentIdx > importLineIdx, "foo comment should be after @import");
        Assert.True(mainCommentIdx > fooCommentIdx, "main comment should be after foo comment");
        Assert.True(endCommentIdx > mainCommentIdx, "end comment should be after main comment");
    }
    
    #endregion
    
    #region Undo/Redo Tests
    
    [Fact]
    public void UndoRevertsEdits()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        var originalText = tree.Root.ToString();
        
        var mainFunc = FindFunction(tree, "main");
        var mainQuery = Query.Kind(mainFunc!.Kind)
            .Where(n => n is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainQuery.Before(), "// This comment will be undone\n")
            .Commit();
        
        var modifiedText = tree.Root.ToString();
        Assert.Contains("// This comment will be undone", modifiedText);
        
        // Undo
        Assert.True(tree.Undo());
        
        var undoneText = tree.Root.ToString();
        Assert.Equal(originalText, undoneText);
        Assert.DoesNotContain("// This comment will be undone", undoneText);
    }
    
    #endregion
}
