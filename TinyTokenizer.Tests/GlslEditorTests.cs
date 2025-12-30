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
    public sealed class GlslFunctionSyntax : SyntaxNode, INamedNode, IBlockContainerNode
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
        
        #region IBlockContainerNode
        
        /// <inheritdoc/>
        public IReadOnlyList<string> BlockNames => ["body", "params"];
        
        /// <inheritdoc/>
        public RedBlock GetBlock(string? name = null) => name switch
        {
            null or "body" => Body,
            "params" => Parameters,
            _ => throw new ArgumentException($"Unknown block name: {name}. Valid names are: {string.Join(", ", BlockNames)}")
        };
        
        #endregion
    }
    
    /// <summary>
    /// A GLSL tagged directive: #version, #define, etc.
    /// Pattern: TaggedIdent + rest of line
    /// </summary>
    public sealed class GlslDirectiveSyntax : SyntaxNode, INamedNode
    {
        public GlslDirectiveSyntax(GreenSyntaxNode green, RedNode? parent, int position)
            : base(green, parent, position) { }
        
        /// <summary>The directive tag node (e.g., "#version").</summary>
        public RedLeaf DirectiveNode => GetTypedChild<RedLeaf>(0);
        
        /// <summary>The directive name without # (e.g., "version").</summary>
        public string Name => DirectiveNode.Text.TrimStart('#');
        
        /// <summary>
        /// Gets all children after the directive tag (the arguments).
        /// </summary>
        public IEnumerable<RedNode> Arguments => Children.Skip(1);
        
        /// <summary>
        /// Gets the arguments as a string.
        /// </summary>
        public string ArgumentsText => string.Concat(Arguments.Select(a => a.ToString()));
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
            // Directive: #tag followed by tokens until newline
            .DefineSyntax(Syntax.Define<GlslDirectiveSyntax>("GlslDirective")
                .Match(Query.TaggedIdent, Query.Any.Until(Query.Newline))
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

// A helper function to sample a texture
vec4 foo(vec2 coord) {
    return texture(tex, coord);
}

void main() {
    vec4 color = foo(uv);
}
";
    
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
        // Roslyn-style: ToString() includes trailing trivia (space before next token)
        Assert.StartsWith("#version 330", versionDirective!.ToString());
    }
    
    #endregion
    
    #region 1. Insert Comment Above Method
    
    [Fact]
    public void InsertCommentAboveMainMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Create a query that finds the main function by checking the name
        var mainFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        Assert.NotNull(mainFunc);
        
        
        tree.CreateEditor()
            .Insert(mainFuncQuery.Before(), "// Entry point for the fragment shader\r\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Roslyn-style: insertion goes BEFORE target's leading trivia.
        // The blank line (leading trivia of 'void') stays with 'void', so comment appears before the blank line.
        Assert.Contains("// Entry point for the fragment shader\r\n\r\nvoid main()", result);
    }

    #endregion
    
    #region 2. Insert Sample-From-Texture at Top of main()
    
    [Fact]
    public void InsertSampleFromTextureAtTopOfMain()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        Assert.NotNull(mainFunc);
        
        // Insert at the inner start of main's body
        var mainBodyQuery = Query.BraceBlock
            .Where(n => n.Parent is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainBodyQuery.InnerStart(), "\n    vec4 sample = texture(tex, uv);")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Verify insertion appears at inner start of main's body (after opening brace)
        Assert.Contains("void main() {\n    vec4 sample = texture(tex, uv);", result);
    }
    
    #endregion
    
    #region 3. Insert Write-to-Out-Buffer at End of main()
    
    [Fact]
    public void InsertWriteToOutBufferAtEndOfMain()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        Assert.NotNull(mainFunc);
        
        // Insert at the inner end of main's body (before closing brace)
        var mainBodyQuery = Query.BraceBlock
            .Where(n => n.Parent is GlslFunctionSyntax f && f.Name == "main");
        
        tree.CreateEditor()
            .Insert(mainBodyQuery.InnerEnd(), "\n    fragColor = color;")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Verify insertion appears at inner end of main's body, showing context (last statement + inserted + closing brace)
        // The exact whitespace may vary, so we check the key sequence
        Assert.Matches(@"foo\(uv\);\s*fragColor = color;\s*}", result);
    }
    
    #endregion
    
    #region 4. Insert Comment After main() Method
    
    [Fact]
    public void InsertCommentAfterMainMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var mainFunc = tree.Select(mainQuery).FirstOrDefault() as GlslFunctionSyntax;
        Assert.NotNull(mainFunc);
        
        tree.CreateEditor()
            .Insert(mainQuery.After(), "\n// End of main function\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Verify insertion appears after main's closing brace
        Assert.Contains("}\n// End of main function\n", result);
    }
    
    #endregion
    
    #region 5. Insert Import Below Version Directive
    
    [Fact]
    public void InsertImportBelowVersionDirective()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var versionDirectiveQuery = Query.Syntax<GlslDirectiveSyntax>().Where(d => d.Name == "version");
        var versionDirective = tree.Select(versionDirectiveQuery).FirstOrDefault() as GlslDirectiveSyntax;
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
        
        // Verify insertion appears after "core" and before "uniform" (the content + surrounding context)
        Assert.Matches(@"core\s*@import \""my-include\.glsl\""\s*uniform", result);
    }
    
    #endregion
    
    #region 6. Insert Comment Above foo() Method
    
    [Fact]
    public void InsertCommentAboveFooMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var fooFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "foo");
        var fooFunc = tree.Select(fooFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        Assert.NotNull(fooFunc);
        
        var fooQuery = Query.Syntax<GlslFunctionSyntax>().Where(n => n.Name == "foo");
        
        tree.CreateEditor()
            .Insert(fooQuery.Before(), "/* foo comment */\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Roslyn-style: insertion goes BEFORE target's leading trivia.
        // The existing comment is leading trivia of 'vec4', so our comment appears before it.
        Assert.Contains("/* foo comment */\n\r\n// A helper function to sample a texture\r\nvec4 foo", result);
    }
    
    #endregion
    
    #region Combined Edit Test
    
    [Fact]
    public void ApplyAllEditsInSingleCommit()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        
        var fooFuncQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "foo");
        var fooFunc = tree.Select(fooFuncQuery).FirstOrDefault() as GlslFunctionSyntax;
        var coreIdent = new TreeWalker(tree.Root)
            .DescendantsAndSelf()
            .OfType<RedLeaf>()
            .FirstOrDefault(n => n.Text == "core");
        
        Assert.NotNull(mainFunc);
        Assert.NotNull(fooFunc);
        Assert.NotNull(coreIdent);
        
        var mainQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var fooQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "foo");
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
            .Insert(fooQuery.Before(), "/* foo comment */\n")
            .Commit();
        
        var result = tree.Root.ToString();
        
        // Verify all edits with their surrounding context using patterns that match the sequence
        // 1. Import after "core" and before "uniform"
        Assert.Matches(@"core\s*@import \""my-include\.glsl\""\s*uniform", result);
        // 2. Comment above foo (before existing comment/leading trivia)
        Assert.Contains("/* foo comment */\n\r\n// A helper function to sample a texture\r\nvec4 foo", result);
        // 3. Comment above main (before leading trivia)
        Assert.Contains("// Entry point for the fragment shader\n\r\nvoid main()", result);
        // 4. Sample at inner start of main body  
        Assert.Contains("void main() {\n    vec4 sample = texture(tex, uv);", result);
        // 5. fragColor at inner end of main body (shows context: last stmt + inserted + close brace)
        Assert.Matches(@"foo\(uv\);\s*fragColor = sample;\s*}", result);
        // 6. Comment after main (shows closing brace + inserted content)
        Assert.Contains("}\n// End of main function\n", result);
    }
    
    #endregion
    
    #region Undo/Redo Tests
    
    [Fact]
    public void UndoRevertsEdits()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        var originalText = tree.Root.ToString();
        
        var mainQuery = Query.Syntax<GlslFunctionSyntax>().Where(f => f.Name == "main");
        var mainFunc = tree.Select(mainQuery).FirstOrDefault() as GlslFunctionSyntax;
        Assert.NotNull(mainFunc);
        
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
    
    #region New API Tests (INamedNode, IBlockContainerNode)
    
    [Fact]
    public void Named_FindsFunctionByName()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use the new .Named() extension method
        var mainFunc = Query.Syntax<GlslFunctionSyntax>().Named("main")
            .SelectTyped(tree)
            .FirstOrDefault();
        
        Assert.NotNull(mainFunc);
        Assert.Equal("main", mainFunc!.Name);
        Assert.Equal("void", mainFunc.ReturnType);
    }
    
    [Fact]
    public void Named_ReturnsEmptyForNonExistentName()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var nonExistent = Query.Syntax<GlslFunctionSyntax>().Named("nonexistent")
            .SelectTyped(tree)
            .FirstOrDefault();
        
        Assert.Null(nonExistent);
    }
    
    [Fact]
    public void InnerStart_WithBlockName_InsertsIntoBody()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use the new .InnerStart("body") extension method
        var mainQuery = Query.Syntax<GlslFunctionSyntax>().Named("main");
        
        tree.CreateEditor()
            .Insert(mainQuery.InnerStart("body"), "\n    // Body start")
            .Commit();
        
        var result = tree.Root.ToString();
        Assert.Contains("void main() {\n    // Body start", result);
    }
    
    [Fact]
    public void InnerEnd_WithDefaultBlock_InsertsIntoBody()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use the new .InnerEnd() extension method (defaults to "body" as first block)
        var mainQuery = Query.Syntax<GlslFunctionSyntax>().Named("main");
        
        tree.CreateEditor()
            .Insert(mainQuery.InnerEnd(), "\n    // Body end")
            .Commit();
        
        var result = tree.Root.ToString();
        Assert.Matches(@"// Body end\s*}", result);
    }
    
    [Fact]
    public void DirectiveSyntax_CapturesFullLine()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // The directive should capture "#version 330 core"
        var versionDirective = Query.Syntax<GlslDirectiveSyntax>().Named("version")
            .SelectTyped(tree)
            .FirstOrDefault();
        
        Assert.NotNull(versionDirective);
        Assert.Equal("version", versionDirective!.Name);
        
        // The arguments should contain "330" and "core"
        var argsText = versionDirective.ArgumentsText;
        Assert.Contains("330", argsText);
        Assert.Contains("core", argsText);
    }
    
    #endregion
}
