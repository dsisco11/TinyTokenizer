using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;
using Xunit.Abstractions;

namespace TinyTokenizer.E2ETests;

/// <summary>
/// End-to-end tests for editing GLSL shaders using the SyntaxTree and SyntaxEditor APIs.
/// Demonstrates:
/// - Parsing GLSL with syntax binding for function recognition
/// - Inserting comments and code at various positions
/// - Working with function definitions and bodies
/// </summary>
public class GlslEditorTests
{
    private readonly ITestOutputHelper _output;

    public GlslEditorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Helpers
    
    /// <summary>
    /// Normalizes line endings to \n for cross-platform test assertions.
    /// </summary>
    private static string NormalizeLineEndings(string text) => text.ReplaceLineEndings("\n");
    
    #endregion
    
    #region Custom Syntax Nodes for GLSL
    
    /// <summary>
    /// A GLSL function definition: type name(params) { body }
    /// Pattern: Ident + Ident + ParenBlock + BraceBlock
    /// Example: void main() { ... }
    /// </summary>
    public sealed class GlFunctionNode : SyntaxNode, INamedNode, IBlockContainerNode
    {
        public GlFunctionNode(CreationContext context)
            : base(context) { }
        
        /// <summary>Return type node (e.g., "void", "vec4").</summary>
        public SyntaxToken ReturnTypeNode => GetTypedChild<SyntaxToken>(0);
        
        /// <summary>Return type as text.</summary>
        public string ReturnType => ReturnTypeNode.Text;
        
        /// <summary>Function name node.</summary>
        public SyntaxToken NameNode => GetTypedChild<SyntaxToken>(1);
        
        /// <summary>Function name as text.</summary>
        public string Name => NameNode.Text;
        
        /// <summary>Parameter list block (parentheses).</summary>
        public SyntaxBlock Parameters => GetTypedChild<SyntaxBlock>(2);
        
        /// <summary>Function body block (braces).</summary>
        public SyntaxBlock Body => GetTypedChild<SyntaxBlock>(3);
        
        #region IBlockContainerNode
        
        /// <inheritdoc/>
        public IReadOnlyList<string> BlockNames => ["body", "params"];
        
        /// <inheritdoc/>
        public SyntaxBlock GetBlock(string? name = null) => name switch
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
    public sealed class GlDirectiveNode : SyntaxNode, INamedNode
    {
        public GlDirectiveNode(CreationContext context)
            : base(context) { }
        
        /// <summary>The directive tag node (e.g., "#version").</summary>
        public SyntaxToken DirectiveNode => GetTypedChild<SyntaxToken>(0);
        
        /// <summary>The directive name without # (e.g., "version").</summary>
        public string Name => DirectiveNode.Text.TrimStart('#');
        
        /// <summary>
        /// Gets all children after the directive tag (the arguments).
        /// </summary>
        public IEnumerable<SyntaxNode> Arguments => Children.Skip(1);
        
        /// <summary>
        /// Gets the arguments as a string.
        /// </summary>
        public string ArgumentsText => string.Concat(Arguments.Select(a => a.ToText()));
    }
    
    /// <summary>
    /// A GLSL tagged directive: #version, #define, etc.
    /// Pattern: TaggedIdent + rest of line
    /// </summary>
    public sealed class GlImportNode : SyntaxNode, INamedNode
    {
        public GlImportNode(CreationContext context)
            : base(context) { }
        
        /// <summary>The Import tag node (e.g., "#version").</summary>
        public SyntaxToken ImportNode => GetTypedChild<SyntaxToken>(0);
        
        /// <summary>The Import name without # (e.g., "version").</summary>
        public string Name => ImportNode.Text.TrimStart('@');
        
        /// <summary>
        /// Gets the filename node (the second child).
        /// </summary>
        public SyntaxToken? FilenameNode => GetChild(1) is SyntaxToken leaf && leaf.Kind == NodeKind.String ? leaf : null;

        public string? Filename => FilenameNode?.ToString();
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
            .DefineSyntax(Syntax.Define<GlFunctionNode>("glFunction")
                .Match(Query.AnyIdent, Query.AnyIdent, Query.ParenBlock, Query.BraceBlock)
                .WithPriority(10)
                .Build())
            // Import: @import "filename"
            .DefineSyntax(Syntax.Define<GlImportNode>("glImport")
                .Match(Query.TaggedIdent("@import"), Query.Any.Until(Query.Newline))
                .WithPriority(1)
                .Build())
            // Directive: #tag followed by tokens until newline
            .DefineSyntax(Syntax.Define<GlDirectiveNode>("glDirective")
                // .Match(Query.TaggedIdent("version"), Query.Any.Until(Query.Newline))
                .Match(Query.AnyTaggedIdent, Query.Any.Until(Query.Newline))
                .Build())
            .Build();
    }
    
    /// <summary>
    /// Sample GLSL shader for testing.
    /// </summary>
    private const string SampleShader = @"#version 330 core

@import ""my-include.glsl""

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
    public void DumpTokenTree()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        // Use "S" format for full structure dump with trivia info
        var dump = tree.Root.ToString("S", null);
        // Dump text to xunit output for visual verification
        // (In real tests, we would assert specific structure)
        _output.WriteLine(dump);
        Assert.NotNull(dump);
    }
    
    [Fact]
    public void Parse_RecognizesGlslFunctions()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var mainFuncQuery = Query.Syntax<GlFunctionNode>().Named("main");
        var fooFuncQuery = Query.Syntax<GlFunctionNode>().Named("foo");

        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlFunctionNode;
        var fooFunc = tree.Select(fooFuncQuery).FirstOrDefault() as GlFunctionNode;
        
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
        
        var versionDirectiveQuery = Query.Syntax<GlDirectiveNode>().Named("version");
        var versionDirective = tree.Select(versionDirectiveQuery).FirstOrDefault() as GlDirectiveNode;
        
        Assert.NotNull(versionDirective);
        // Roslyn-style: ToString() includes trailing trivia (space before next token)
        Assert.Equal("#version 330 core\n", NormalizeLineEndings(versionDirective!.ToText()));
    }

    [Fact]
    public void Parse_RecognizesImportDirective()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        var importDirectiveQuery = Query.Syntax<GlImportNode>().Named("import");
        var importDirective = tree.Select(importDirectiveQuery).FirstOrDefault() as GlImportNode;
        Assert.NotNull(importDirective);
        // Roslyn-style: ToString() includes trailing trivia (space before next token)
        Assert.Equal("\n@import \"my-include.glsl\"\n", NormalizeLineEndings(importDirective!.ToText()));
    }
    
    #endregion
    
    #region 1. Insert Comment Above Method
    
    [Fact]
    public void InsertCommentAboveMainMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Create a query that finds the main function by checking the name
        var mainFuncQuery = Query.Syntax<GlFunctionNode>().Named("main");
        var mainFunc = tree.Select(mainFuncQuery).FirstOrDefault() as GlFunctionNode;
        Assert.NotNull(mainFunc);
        
        
        tree.CreateEditor()
            .InsertBefore(mainFuncQuery, "// Entry point for the fragment shader\r\n")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Roslyn-style: insertion goes BEFORE target's leading trivia.
        // The blank line (leading trivia of 'void') stays with 'void', so comment appears before the blank line.
        Assert.Contains("// Entry point for the fragment shader\n\nvoid main()", result);
    }

    #endregion
    
    #region 2. Insert Sample-From-Texture at Top of main()
    
    [Fact]
    public void InsertSampleFromTextureAtTopOfMain()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use INamedNode + IBlockContainerNode APIs to locate injection point
        tree.CreateEditor()
            .InsertAfter(Query.Syntax<GlFunctionNode>().Named("main").InnerStart("body"), "\n    vec4 sample = texture(tex, uv);")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Verify insertion appears at inner start of main's body (after opener's trailing trivia)
        // Original: {\n    vec4 color... -> After insert: {\n    \n    vec4 sample...vec4 color
        Assert.Contains("void main() {\n    \n    vec4 sample = texture(tex, uv);", result);
    }
    
    #endregion
    
    #region 3. Insert Write-to-Out-Buffer at End of main()
    
    [Fact]
    public void InsertWriteToOutBufferAtEndOfMain()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use INamedNode + IBlockContainerNode APIs to locate injection point
        tree.CreateEditor()
            .InsertBefore(Query.Syntax<GlFunctionNode>().Named("main").InnerEnd("body"), "\n    fragColor = color;")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
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
        
        var mainQuery = Query.Syntax<GlFunctionNode>().Named("main");
        var mainFunc = tree.Select(mainQuery).FirstOrDefault() as GlFunctionNode;
        Assert.NotNull(mainFunc);
        
        tree.CreateEditor()
            .InsertAfter(mainQuery, "\n// End of main function\n")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Verify insertion appears after main's closing brace
        // Block trailing trivia (newline after }) comes before the inserted content
        Assert.Contains("}\n\n// End of main function\n", result);
    }
    
    #endregion
    
    #region 5. Insert Import Below Version Directive
    
    [Fact]
    public void InsertImportBelowVersionDirective()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use INamedNode API to find the #version directive and insert after it
        tree.CreateEditor()
            .InsertAfter(Query.Syntax<GlDirectiveNode>().Named("version"), "\n@import \"my-include.glsl\"")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Verify insertion appears after #version directive
        // The sample already contains @import, so we now have TWO imports
        Assert.Matches(@"core\s*@import \""my-include\.glsl\""\s*@import", result);
    }
    
    #endregion
    
    #region 6. Insert Comment Above foo() Method
    
    [Fact]
    public void InsertCommentAboveFooMethod()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use INamedNode API to find foo function by name
        tree.CreateEditor()
            .InsertBefore(Query.Syntax<GlFunctionNode>().Named("foo"), "/* foo comment */\n")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Roslyn-style: insertion goes BEFORE target's leading trivia.
        // The existing comment is leading trivia of 'vec4', so our comment appears before it.
        Assert.Contains("/* foo comment */\n\n// A helper function to sample a texture\nvec4 foo", result);
    }
    
    #endregion
    
    #region Combined Edit Test
    
    [Fact]
    public void ApplyAllEditsInSingleCommit()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use INamedNode + IBlockContainerNode APIs for all injection points
        var mainQuery = Query.Syntax<GlFunctionNode>().Named("main");
        var fooQuery = Query.Syntax<GlFunctionNode>().Named("foo");
        var versionQuery = Query.Syntax<GlDirectiveNode>().Named("version");
        
        tree.CreateEditor()
            // 1. Comment above main
            .InsertBefore(mainQuery, "// Entry point for the fragment shader\n")
            // 2. Sample from texture at top of main body
            .InsertAfter(mainQuery.InnerStart("body"), "\n    vec4 sample = texture(tex, uv);")
            // 3. Write to out buffer at end of main body
            .InsertBefore(mainQuery.InnerEnd("body"), "\n    fragColor = sample;")
            // 4. Comment after main
            .InsertAfter(mainQuery, "\n// End of main function\n")
            // 5. Import below #version directive
            .InsertAfter(versionQuery, "\n@import \"my-include.glsl\"")
            // 6. Comment above foo
            .InsertBefore(fooQuery, "/* foo comment */\n")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Verify all edits with their surrounding context using patterns that match the sequence
        // 1. Import after "core" - sample already has @import so now we have two
        Assert.Matches(@"core\s*@import \""my-include\.glsl\""\s*@import", result);
        // 2. Comment above foo (before existing comment/leading trivia)
        Assert.Contains("/* foo comment */\n\n// A helper function to sample a texture\nvec4 foo", result);
        // 3. Comment above main (before leading trivia)
        Assert.Contains("// Entry point for the fragment shader\n\nvoid main()", result);
        // 4. Sample at inner start of main body (opener's trailing trivia preserved: newline + indent)
        Assert.Contains("void main() {\n    \n    vec4 sample = texture(tex, uv);", result);
        // 5. fragColor at inner end of main body (shows context: last stmt + inserted + close brace)
        Assert.Matches(@"foo\(uv\);\s*fragColor = sample;\s*}", result);
        // 6. Comment after main (shows closing brace + trailing trivia + inserted content)
        Assert.Contains("}\n\n// End of main function\n", result);
    }
    
    #endregion
    
    #region Undo/Redo Tests
    
    [Fact]
    public void UndoRevertsEdits()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        var originalText = tree.Root.ToText();
        
        var mainQuery = Query.Syntax<GlFunctionNode>().Named("main");
        var mainFunc = tree.Select(mainQuery).FirstOrDefault() as GlFunctionNode;
        Assert.NotNull(mainFunc);
        
        tree.CreateEditor()
            .InsertBefore(mainQuery, "// This comment will be undone\n")
            .Commit();
        
        var modifiedText = tree.Root.ToText();
        Assert.Contains("// This comment will be undone", modifiedText);
        
        // Undo
        Assert.True(tree.Undo());
        
        var undoneText = tree.Root.ToText();
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
        var mainFunc = Query.Syntax<GlFunctionNode>().Named("main")
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
        
        var nonExistent = Query.Syntax<GlFunctionNode>().Named("nonexistent")
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
        var mainQuery = Query.Syntax<GlFunctionNode>().Named("main");
        
        tree.CreateEditor()
            .InsertAfter(mainQuery.InnerStart("body"), "\n    // Body start")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        // With new GreenBlock design, opener's trailing trivia (newline + indent) is preserved
        Assert.Contains("void main() {\n    \n    // Body start", result);
    }
    
    [Fact]
    public void InnerEnd_WithDefaultBlock_InsertsIntoBody()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // Use the new .InnerEnd() extension method (defaults to "body" as first block)
        var mainQuery = Query.Syntax<GlFunctionNode>().Named("main");
        
        tree.CreateEditor()
            .InsertBefore(mainQuery.InnerEnd(), "\n    // Body end")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        Assert.Matches(@"// Body end\s*}", result);
    }
    
    [Fact]
    public void DirectiveSyntax_CapturesFullLine()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        // The directive should capture "#version 330 core"
        var versionDirective = Query.Syntax<GlDirectiveNode>().Named("version")
            .SelectTyped(tree)
            .FirstOrDefault();
        
        Assert.NotNull(versionDirective);
        Assert.Equal("version", versionDirective!.Name);
        
        // The arguments should contain "330" and "core"
        var argsText = versionDirective.ArgumentsText;
        Assert.Contains("330", argsText);
        Assert.Contains("core", argsText);
    }

    [Fact]
    public void CanCommentOutImportDirective()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(SampleShader, schema);
        
        var importQuery = Query.Syntax<GlImportNode>().Named("import");
        var importNode = tree.Select(importQuery).FirstOrDefault() as GlImportNode;
        Assert.NotNull(importNode);
        
        // Comment out the import directive by inserting '//' before it
        tree.CreateEditor()
            .Edit(importQuery, str => $"// {str}")
            .Commit();
        
        var result = NormalizeLineEndings(tree.Root.ToText());
        
        // Verify that the import directive is now commented out
        // Note: The blank line between #version and @import is preserved (leading trivia)
        Assert.Contains("#version 330 core\n\n// @import \"my-include.glsl\"", NormalizeLineEndings(result));
    }

    [Fact]
    public void InsertNewImport_CanQueryGlImportNodeAfterwards()
    {
        // Use a shader without any imports initially
        const string shaderWithoutImport = @"#version 330 core

uniform sampler2D tex;

void main() {
    vec4 color = texture(tex, vec2(0.0));
}
";
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(shaderWithoutImport, schema);
        
        // Verify no imports exist before insertion
        var importQuery = Query.Syntax<GlImportNode>();
        var importsBefore = tree.Select(importQuery).ToList();
        Assert.Empty(importsBefore);
        
        // Insert a new import after the #version directive
        var versionQuery = Query.Syntax<GlDirectiveNode>().Named("version");
        tree.CreateEditor()
            .InsertAfter(versionQuery, "\n@import \"utils.glsl\"")
            .Commit();
        
        // The inserted text is present in serialized content
        var serialized = NormalizeLineEndings(tree.Root.ToText());
        Assert.Contains("@import \"utils.glsl\"", serialized);
        
        // Incremental rebinding happens automatically in Commit(),
        // so the new import is immediately queryable without re-parsing
        var importsAfter = tree.Select(importQuery).Cast<GlImportNode>().ToList();
        Assert.Single(importsAfter);
        
        var insertedImport = importsAfter[0];
        Assert.Equal("import", insertedImport.Name);
        // Filename includes trailing trivia via ToString(), so check the node's text value
        Assert.NotNull(insertedImport.FilenameNode);
        Assert.Equal("\"utils.glsl\"", insertedImport.FilenameNode!.Text);
    }
    
    #endregion
    
    #region Trivia Preservation Tests - Complex Nested Structures
    
    /// <summary>
    /// Complex shader with triple-nested for loops for testing trivia preservation.
    /// Uses 4-space indentation consistently.
    /// </summary>
    private const string ComplexNestedShader = @"#version 330 core

// Uniforms
uniform sampler2D tex;
uniform float time;
uniform vec3 lightDir;

// Input/output
in vec2 uv;
out vec4 fragColor;

// Process a single sample with nested loops
vec4 processPixel(vec2 coord) {
    vec4 result = vec4(0.0);
    
    // Triple nested loop for blur kernel
    for (int x = -2; x <= 2; x++) {
        for (int y = -2; y <= 2; y++) {
            for (int c = 0; c < 4; c++) {
                vec2 offset = vec2(float(x), float(y)) * 0.01;
                vec4 sample = texture(tex, coord + offset);
                result[c] += sample[c] * 0.04;
            }
        }
    }
    
    return result;
}

// Calculate lighting with conditionals
float calculateLight(vec3 normal) {
    float intensity = 0.0;
    
    if (dot(normal, lightDir) > 0.0) {
        intensity = dot(normal, lightDir);
        
        // Specular highlight
        if (intensity > 0.8) {
            intensity += pow(intensity, 32.0);
        }
    } else {
        intensity = 0.1; // Ambient
    }
    
    return intensity;
}

void main() {
    vec4 color = processPixel(uv);
    float light = calculateLight(vec3(0.0, 0.0, 1.0));
    fragColor = color * light;
}
";

    /// <summary>
    /// Test that parsing and serializing a shader preserves exact 4-space indentation
    /// in triple-nested for loops. This test will FAIL if the tokenizer corrupts whitespace.
    /// </summary>
    [Fact]
    public void TriviaPreservation_TripleNestedLoop_FourSpaceIndentationPreserved()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        var result = NormalizeLineEndings(tree.ToText());
        
        // These are the EXPECTED patterns with exact 4-space-per-level indentation.
        // If ANY of these fail, there's a bug in trivia handling.
        
        // Level 0 (inside function): exactly 4 spaces
        Assert.Contains("\n    // Triple nested loop for blur kernel\n", result);
        Assert.Contains("\n    for (int x = -2; x <= 2; x++) {\n", result);
        
        // Level 1: exactly 8 spaces (4+4)
        Assert.Contains("\n        for (int y = -2; y <= 2; y++) {\n", result);
        
        // Level 2: exactly 12 spaces (4+4+4)
        Assert.Contains("\n            for (int c = 0; c < 4; c++) {\n", result);
        
        // Level 3: exactly 16 spaces (4+4+4+4)
        Assert.Contains("\n                vec2 offset = vec2(float(x), float(y)) * 0.01;\n", result);
        Assert.Contains("\n                vec4 sample = texture(tex, coord + offset);\n", result);
        Assert.Contains("\n                result[c] += sample[c] * 0.04;\n", result);
    }
    
    /// <summary>
    /// Test that nested if/else conditionals preserve exact indentation.
    /// </summary>
    [Fact]
    public void TriviaPreservation_NestedConditionals_FourSpaceIndentationPreserved()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        var result = NormalizeLineEndings(tree.ToText());
        
        // Level 0 (function body): 4 spaces
        Assert.Contains("\n    if (dot(normal, lightDir) > 0.0) {\n", result);
        
        // Level 1 (inside if): 8 spaces
        Assert.Contains("\n        intensity = dot(normal, lightDir);\n", result);
        Assert.Contains("\n        // Specular highlight\n", result);
        Assert.Contains("\n        if (intensity > 0.8) {\n", result);
        
        // Level 2 (nested if): 12 spaces
        Assert.Contains("\n            intensity += pow(intensity, 32.0);\n", result);
        
        // The else block: "    } else {" with 4 spaces before the closing brace
        Assert.Contains("\n    } else {\n", result);
        
        // Inside else: 8 spaces, with inline comment preserved
        Assert.Contains("\n        intensity = 0.1; // Ambient\n", result);
    }
    
    /// <summary>
    /// Test that blank lines between functions are preserved exactly (one blank line = two newlines).
    /// </summary>
    [Fact]
    public void TriviaPreservation_BlankLinesBetweenFunctions_ExactlyOneBlankLine()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        var result = NormalizeLineEndings(tree.ToText());
        
        // Between processPixel and calculateLight: exactly one blank line
        Assert.Contains("}\n\n// Calculate lighting with conditionals\n", result);
        
        // Between calculateLight and main: exactly one blank line
        Assert.Contains("}\n\nvoid main() {\n", result);
    }
    
    /// <summary>
    /// Test that inline comments (statement followed by // comment) are preserved.
    /// </summary>
    [Fact]
    public void TriviaPreservation_InlineComment_SpacingPreserved()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        var result = NormalizeLineEndings(tree.ToText());
        
        // The inline comment must have: 8 spaces indent, statement, semicolon, space, //, space, comment
        Assert.Contains("\n        intensity = 0.1; // Ambient\n", result);
    }
    
    /// <summary>
    /// Test that global declarations (uniforms, in/out) have no indentation.
    /// </summary>
    [Fact]
    public void TriviaPreservation_GlobalDeclarations_NoIndentation()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        var result = NormalizeLineEndings(tree.ToText());
        
        // Uniforms should have no leading spaces (start at column 0)
        Assert.Contains("\nuniform sampler2D tex;\n", result);
        Assert.Contains("\nuniform float time;\n", result);
        Assert.Contains("\nuniform vec3 lightDir;\n", result);
        
        // In/out should have no leading spaces
        Assert.Contains("\nin vec2 uv;\n", result);
        Assert.Contains("\nout vec4 fragColor;\n", result);
    }
    
    /// <summary>
    /// Test that 4-level deep nesting (20 spaces for innermost) is preserved.
    /// </summary>
    [Fact]
    public void TriviaPreservation_FourLevelNesting_TwentySpacesInnermost()
    {
        const string deeplyNestedShader = @"void deepNest() {
    // Level 1
    for (int a = 0; a < 2; a++) {
        // Level 2
        for (int b = 0; b < 2; b++) {
            // Level 3
            for (int c = 0; c < 2; c++) {
                // Level 4
                for (int d = 0; d < 2; d++) {
                    // Innermost
                    result += a * b * c * d;
                }
            }
        }
    }
}
";
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(deeplyNestedShader, schema);
        var result = NormalizeLineEndings(tree.ToText());
        
        // Level 1: 4 spaces
        Assert.Contains("\n    // Level 1\n", result);
        Assert.Contains("\n    for (int a = 0; a < 2; a++) {\n", result);
        
        // Level 2: 8 spaces
        Assert.Contains("\n        // Level 2\n", result);
        Assert.Contains("\n        for (int b = 0; b < 2; b++) {\n", result);
        
        // Level 3: 12 spaces
        Assert.Contains("\n            // Level 3\n", result);
        Assert.Contains("\n            for (int c = 0; c < 2; c++) {\n", result);
        
        // Level 4: 16 spaces
        Assert.Contains("\n                // Level 4\n", result);
        Assert.Contains("\n                for (int d = 0; d < 2; d++) {\n", result);
        
        // Innermost: 20 spaces (5 levels deep)
        Assert.Contains("\n                    // Innermost\n", result);
        Assert.Contains("\n                    result += a * b * c * d;\n", result);
    }
    
    /// <summary>
    /// Test that after making edits, unmodified sections still have exact indentation.
    /// </summary>
    [Fact]
    public void TriviaPreservation_AfterEdits_UnmodifiedSectionsKeepExactIndentation()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        
        // Make edits to calculateLight
        var calcLightQuery = Query.Syntax<GlFunctionNode>().Named("calculateLight");
        tree.CreateEditor()
            .InsertAfter(calcLightQuery.InnerStart("body"), "\n    // INJECTED")
            .Commit();
        
        var result = NormalizeLineEndings(tree.ToText());
        
        // The edit should be there
        Assert.Contains("// INJECTED", result);
        
        // processPixel's nested loops should still have EXACT indentation
        Assert.Contains("\n    for (int x = -2; x <= 2; x++) {\n", result);
        Assert.Contains("\n        for (int y = -2; y <= 2; y++) {\n", result);
        Assert.Contains("\n            for (int c = 0; c < 4; c++) {\n", result);
        Assert.Contains("\n                vec2 offset = vec2(float(x), float(y)) * 0.01;\n", result);
        
        // main function should still have exact 4-space indentation
        Assert.Contains("\n    vec4 color = processPixel(uv);\n", result);
        Assert.Contains("\n    float light = calculateLight(vec3(0.0, 0.0, 1.0));\n", result);
        Assert.Contains("\n    fragColor = color * light;\n", result);
    }
    
    /// <summary>
    /// Test that undo restores the EXACT original text including all whitespace.
    /// </summary>
    [Fact]
    public void TriviaPreservation_UndoRestoresExactOriginal()
    {
        var schema = CreateGlslSchema();
        var tree = SyntaxTree.Parse(ComplexNestedShader, schema);
        var original = tree.ToText();
        
        // Make edits
        var mainQuery = Query.Syntax<GlFunctionNode>().Named("main");
        tree.CreateEditor()
            .InsertBefore(mainQuery, "// Comment\n")
            .InsertAfter(mainQuery.InnerStart("body"), "\n    // Start")
            .InsertBefore(mainQuery.InnerEnd("body"), "\n    // End")
            .Commit();
        
        // Verify edits were applied
        var edited = tree.ToText();
        Assert.Contains("// Comment", edited);
        Assert.Contains("// Start", edited);
        Assert.Contains("// End", edited);
        
        // Undo
        Assert.True(tree.Undo());
        
        // Should be byte-for-byte identical to original
        var afterUndo = tree.ToText();
        Assert.Equal(original, afterUndo);
    }
    
    #endregion
    
    #region Multi-Commit Edit Scenarios
    
    /// <summary>
    /// Tests multiple sequential edit/commit cycles with re-querying syntax nodes between mutations.
    /// 
    /// Scenario:
    /// 1. Parse shader with #version directive
    /// 2. Insert @import after #version, commit
    /// 3. Find and Edit the import in-place (comment it out + inject fake code), commit
    /// 4. Re-find #version and insert #define after it, commit
    /// 
    /// Verifies that syntax nodes can be reliably re-queried after multiple tree mutations.
    /// </summary>
    [Fact]
    public void MultiCommit_EditImportThenInsertDefine_MaintainsCorrectTreeStructure()
    {
        var schema = CreateGlslSchema();
        
        // Minimal shader with #version directive
        const string minimalShader = @"#version 330 core

void main() {
    gl_FragColor = vec4(1.0);
}
";
        
        var tree = SyntaxTree.Parse(minimalShader, schema);
        _output.WriteLine("=== Initial tree ===");
        _output.WriteLine(tree.ToText());
        
        // Diagnostic: Verify initial #version is found
        var versionQuery = Query.Syntax<GlDirectiveNode>().Named("version");
        var initialVersion = tree.Select(versionQuery).FirstOrDefault() as GlDirectiveNode;
        Assert.NotNull(initialVersion);
        _output.WriteLine($"Initial #version found: '{NormalizeLineEndings(initialVersion!.ToText())}'");
        
        // === STEP 1: Insert @import after #version ===
        tree.CreateEditor()
            .InsertAfter(versionQuery, "\n@import \"utils.glsl\"")
            .Commit();
        
        _output.WriteLine("\n=== After inserting @import ===");
        _output.WriteLine(tree.ToText());
        
        // Diagnostic: Verify @import was inserted and can be found
        var importQuery = Query.Syntax<GlImportNode>().Named("import");
        var importNode = tree.Select(importQuery).FirstOrDefault() as GlImportNode;
        Assert.NotNull(importNode);
        _output.WriteLine($"@import found: '{NormalizeLineEndings(importNode!.ToText())}'");
        
        // === STEP 2: Edit the import in-place (comment it out + inject fake code) ===
        tree.CreateEditor()
            .Edit(importQuery, content => $"// {content}\nfloat fake = 1.0;")
            .Commit();
        
        _output.WriteLine("\n=== After editing @import (commented out + fake code) ===");
        _output.WriteLine(tree.ToText());
        
        // Diagnostic: Verify the edit was applied
        var treeTextAfterEdit = NormalizeLineEndings(tree.ToText());
        Assert.Contains("// ", treeTextAfterEdit); // Should contain comment marker
        Assert.Contains("float fake = 1.0;", treeTextAfterEdit); // Should contain injected code
        
        // === STEP 3: Re-find #version and insert #define after it ===
        // This is where the bug manifests - the version directive should still be findable
        var versionAfterMutations = tree.Select(versionQuery).FirstOrDefault() as GlDirectiveNode;
        
        // Diagnostic checkpoint: Assert version is still findable
        Assert.NotNull(versionAfterMutations);
        _output.WriteLine($"\n#version after mutations: '{NormalizeLineEndings(versionAfterMutations!.ToText())}'");
        
        tree.CreateEditor()
            .InsertAfter(versionQuery, "\n#define DEBUG 1")
            .Commit();
        
        _output.WriteLine("\n=== After inserting #define ===");
        _output.WriteLine(tree.ToText());
        
        // === FINAL ASSERTIONS ===
        var finalText = NormalizeLineEndings(tree.ToText());
        
        // Verify all modifications are present in correct order
        Assert.Contains("#version 330 core", finalText);
        Assert.Contains("#define DEBUG 1", finalText);
        Assert.Contains("// ", finalText); // Commented import
        Assert.Contains("float fake = 1.0;", finalText);
        Assert.Contains("void main()", finalText);
        
        // Verify order: #version should come before #define which should come before the commented import
        var versionIndex = finalText.IndexOf("#version 330 core");
        var defineIndex = finalText.IndexOf("#define DEBUG 1");
        var commentedImportIndex = finalText.IndexOf("// ");
        var fakeCodeIndex = finalText.IndexOf("float fake = 1.0;");
        
        Assert.True(versionIndex < defineIndex, 
            $"#version (index {versionIndex}) should come before #define (index {defineIndex})");
        Assert.True(defineIndex < commentedImportIndex, 
            $"#define (index {defineIndex}) should come before commented import (index {commentedImportIndex})");
        Assert.True(commentedImportIndex < fakeCodeIndex, 
            $"Commented import (index {commentedImportIndex}) should come before fake code (index {fakeCodeIndex})");
        
        _output.WriteLine("\n=== Test passed: All mutations applied in correct order ===");
    }
    
    #endregion
}
