using System.Collections.Immutable;
using System.Text;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for verifying that deeply nested blocks can roundtrip serialize correctly.
/// </summary>
public class BlockRoundtripTests
{
    #region Helper Methods

    private static ImmutableArray<Token> Tokenize(string source, TokenizerOptions? options = null)
    {
        if (string.IsNullOrEmpty(source))
        {
            return ImmutableArray<Token>.Empty;
        }

        var opts = options ?? TokenizerOptions.Default;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);

        var simpleTokens = lexer.Lex(source);
        return parser.ParseToArray(simpleTokens);
    }

    private static string Roundtrip(string source, TokenizerOptions? options = null)
    {
        var tokens = Tokenize(source, options);
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            sb.Append(token.ToText());
        }
        return sb.ToString();
    }

    private static void AssertRoundtrip(string source, TokenizerOptions? options = null)
    {
        var result = Roundtrip(source, options);
        Assert.Equal(source, result);
    }

    #endregion

    #region Simple Block Roundtrip Tests

    [Fact]
    public void SimpleBlock_BraceBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{content}");
    }

    [Fact]
    public void SimpleBlock_BracketBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("[content]");
    }

    [Fact]
    public void SimpleBlock_ParenthesisBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("(content)");
    }

    [Fact]
    public void SimpleBlock_EmptyBraceBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{}");
    }

    [Fact]
    public void SimpleBlock_EmptyBracketBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("[]");
    }

    [Fact]
    public void SimpleBlock_EmptyParenthesisBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("()");
    }

    [Fact]
    public void SimpleBlock_BlockWithWhitespace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{ content with spaces }");
    }

    #endregion

    #region Two-Level Nesting Tests

    [Fact]
    public void NestedBlocks_TwoLevel_BraceInBrace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{inner}}");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_BracketInBrace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{[inner]}");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_ParenInBrace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{(inner)}");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_BraceInBracket_RoundtripsCorrectly()
    {
        AssertRoundtrip("[{inner}]");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_BraceInParen_RoundtripsCorrectly()
    {
        AssertRoundtrip("({inner})");
    }

    [Fact]
    public void NestedBlocks_TwoLevel_AllThreeTypes_RoundtripsCorrectly()
    {
        AssertRoundtrip("{[()]}");
    }

    #endregion

    #region Three-Level Nesting Tests

    [Fact]
    public void NestedBlocks_ThreeLevel_SameType_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{{innermost}}}");
    }

    [Fact]
    public void NestedBlocks_ThreeLevel_MixedTypes_RoundtripsCorrectly()
    {
        AssertRoundtrip("{[(innermost)]}");
    }

    [Fact]
    public void NestedBlocks_ThreeLevel_WithContent_RoundtripsCorrectly()
    {
        AssertRoundtrip("{outer [middle (inner content) middle] outer}");
    }

    #endregion

    #region Deep Nesting Tests

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DeepNesting_BraceOnly_RoundtripsCorrectly(int depth)
    {
        var opening = new string('{', depth);
        var closing = new string('}', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DeepNesting_BracketOnly_RoundtripsCorrectly(int depth)
    {
        var opening = new string('[', depth);
        var closing = new string(']', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    public void DeepNesting_ParenOnly_RoundtripsCorrectly(int depth)
    {
        var opening = new string('(', depth);
        var closing = new string(')', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(15)]
    [InlineData(30)]
    public void DeepNesting_AlternatingTypes_RoundtripsCorrectly(int depth)
    {
        var sb = new StringBuilder();
        var openers = new[] { '{', '[', '(' };
        var closers = new[] { '}', ']', ')' };

        for (int i = 0; i < depth; i++)
        {
            sb.Append(openers[i % 3]);
        }
        sb.Append("innermost");
        for (int i = depth - 1; i >= 0; i--)
        {
            sb.Append(closers[i % 3]);
        }

        var source = sb.ToString();
        AssertRoundtrip(source);
    }

    #endregion

    #region Complex Structure Tests

    [Fact]
    public void ComplexStructure_SiblingBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a}{b}{c}");
    }

    [Fact]
    public void ComplexStructure_MixedSiblingBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{braces}[brackets](parens)");
    }

    [Fact]
    public void ComplexStructure_NestedSiblings_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{a}{b}}");
    }

    [Fact]
    public void ComplexStructure_MultipleSiblingsAtEachLevel_RoundtripsCorrectly()
    {
        AssertRoundtrip("{outer1 {inner1} {inner2} outer2}");
    }

    [Fact]
    public void ComplexStructure_DeepWithSiblings_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a {b [c (d) c] b} a}");
    }

    [Fact]
    public void ComplexStructure_TreeShape_RoundtripsCorrectly()
    {
        // Tree-like structure with branching at multiple levels
        AssertRoundtrip("{root {left {ll}{lr}} {right {rl}{rr}}}");
    }

    #endregion

    #region Content Within Blocks Tests

    [Fact]
    public void ContentWithinBlocks_Identifiers_RoundtripsCorrectly()
    {
        AssertRoundtrip("{foo bar baz}");
    }

    [Fact]
    public void ContentWithinBlocks_Whitespace_RoundtripsCorrectly()
    {
        AssertRoundtrip("{  \t  \n  }");
    }

    [Fact]
    public void ContentWithinBlocks_Strings_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\"string content\"}");
    }

    [Fact]
    public void ContentWithinBlocks_Numbers_RoundtripsCorrectly()
    {
        AssertRoundtrip("{123 456.789 .5}");
    }

    [Fact]
    public void ContentWithinBlocks_Operators_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default.WithOperators(CommonOperators.CFamily);
        AssertRoundtrip("{a == b && c != d}", options);
    }

    [Fact]
    public void ContentWithinBlocks_Comments_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);
        AssertRoundtrip("{/* comment */}", options);
    }

    [Fact]
    public void ContentWithinBlocks_MixedContent_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleSingleLine);

        AssertRoundtrip("{foo = 123; // comment\n}", options);
    }

    #endregion

    #region Real-World Code Patterns Tests

    [Fact]
    public void RealWorld_FunctionBody_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleSingleLine);

        var source = @"{
    var x = 10;
    return x + 1;
}";
        AssertRoundtrip(source, options);
    }

    [Fact]
    public void RealWorld_NestedFunction_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily);

        var source = @"{
    function inner() {
        return {
            value: 42
        };
    }
}";
        AssertRoundtrip(source, options);
    }

    [Fact]
    public void RealWorld_JsonLikeStructure_RoundtripsCorrectly()
    {
        var source = @"{
    ""name"": ""test"",
    ""items"": [
        { ""id"": 1 },
        { ""id"": 2 }
    ],
    ""nested"": {
        ""deep"": {
            ""value"": 123
        }
    }
}";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_ArrayOfArrays_RoundtripsCorrectly()
    {
        var source = "[[1, 2], [3, 4], [[5, 6], [7, 8]]]";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_FunctionCallChain_RoundtripsCorrectly()
    {
        var source = "foo(bar(baz(qux())))";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_MixedExpression_RoundtripsCorrectly()
    {
        var source = "array[index].method(arg1, arg2).property";
        AssertRoundtrip(source);
    }

    [Fact]
    public void RealWorld_ClassLikeStructure_RoundtripsCorrectly()
    {
        var options = TokenizerOptions.Default
            .WithOperators(CommonOperators.CFamily)
            .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);

        var source = @"{
    /* Constructor */
    constructor(name) {
        this.name = name;
    }

    // Method
    greet() {
        return (""Hello, "" + this.name);
    }

    // Nested class
    Inner {
        value = [1, 2, 3];
    }
}";
        AssertRoundtrip(source, options);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void EdgeCase_EmptyNestedBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{{}}}");
    }

    [Fact]
    public void EdgeCase_BlockFollowedByContent_RoundtripsCorrectly()
    {
        AssertRoundtrip("{block}content");
    }

    [Fact]
    public void EdgeCase_ContentFollowedByBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("content{block}");
    }

    [Fact]
    public void EdgeCase_BlocksWithPunctuation_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a;b;c}");
    }

    [Fact]
    public void EdgeCase_DeepEmptyBlocks_RoundtripsCorrectly()
    {
        AssertRoundtrip("{{{{{}}}}}");;
    }

    [Fact]
    public void EdgeCase_SingleCharContentAtEachLevel_RoundtripsCorrectly()
    {
        AssertRoundtrip("{a{b{c{d}c}b}a}");
    }

    #endregion

    #region IBufferWriter Serialization Tests

    [Fact]
    public void BufferWriter_SimpleBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        var buffer = new System.Buffers.ArrayBufferWriter<char>();
        tokens[0].WriteTo(buffer);

        Assert.Equal("{content}", new string(buffer.WrittenSpan));
    }

    [Fact]
    public void BufferWriter_NestedBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{{inner}}");
        Assert.Single(tokens);

        var buffer = new System.Buffers.ArrayBufferWriter<char>();
        tokens[0].WriteTo(buffer);

        Assert.Equal("{{inner}}", new string(buffer.WrittenSpan));
    }

    #endregion

    #region StringBuilder Serialization Tests

    [Fact]
    public void StringBuilder_SimpleBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        var sb = new StringBuilder();
        tokens[0].WriteTo(sb);

        Assert.Equal("{content}", sb.ToString());
    }

    [Fact]
    public void StringBuilder_DeepNesting_WritesCorrectly()
    {
        var source = "{{{{{innermost}}}}}";
        var tokens = Tokenize(source);
        Assert.Single(tokens);

        var sb = new StringBuilder();
        tokens[0].WriteTo(sb);

        Assert.Equal(source, sb.ToString());
    }

    #endregion

    #region TryWriteTo Tests

    [Fact]
    public void TryWriteTo_SimpleBlock_SucceedsWithAdequateBuffer()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        Span<char> buffer = stackalloc char[50];
        var success = tokens[0].TryWriteTo(buffer, out var written);

        Assert.True(success);
        Assert.Equal("{content}", new string(buffer[..written]));
    }

    [Fact]
    public void TryWriteTo_SimpleBlock_FailsWithSmallBuffer()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        Span<char> buffer = stackalloc char[3]; // Too small
        var success = tokens[0].TryWriteTo(buffer, out var written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    #endregion

    #region TextWriter Serialization Tests

    [Fact]
    public void TextWriter_SimpleBlock_WritesCorrectly()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        using var sw = new StringWriter();
        tokens[0].WriteTo(sw);

        Assert.Equal("{content}", sw.ToString());
    }

    [Fact]
    public void TextWriter_ComplexNesting_WritesCorrectly()
    {
        var source = "{outer {middle [inner (deepest) inner] middle} outer}";
        var tokens = Tokenize(source);

        using var sw = new StringWriter();
        foreach (var token in tokens)
        {
            token.WriteTo(sw);
        }

        Assert.Equal(source, sw.ToString());
    }

    #endregion

    #region TextLength Property Tests

    [Fact]
    public void TextLength_SimpleBlock_ReturnsCorrectLength()
    {
        var tokens = Tokenize("{content}");
        Assert.Single(tokens);

        Assert.Equal(9, tokens[0].TextLength); // "{content}" = 9 chars
    }

    [Fact]
    public void TextLength_NestedBlock_ReturnsCorrectLength()
    {
        var source = "{{inner}}";
        var tokens = Tokenize(source);
        Assert.Single(tokens);

        Assert.Equal(source.Length, tokens[0].TextLength);
    }

    [Fact]
    public void TextLength_DeepNesting_ReturnsCorrectLength()
    {
        var source = "{{{{{innermost}}}}}";
        var tokens = Tokenize(source);
        Assert.Single(tokens);

        Assert.Equal(source.Length, tokens[0].TextLength);
    }

    #endregion

    #region Special Characters Within Blocks Tests

    [Fact]
    public void SpecialChars_StringWithBracesInBlock_RoundtripsCorrectly()
    {
        // String containing braces should not affect block parsing
        AssertRoundtrip("{\"{}[]()\"}");
    }

    [Fact]
    public void SpecialChars_EscapedQuoteInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\"hello\\\"world\"}");
    }

    [Fact]
    public void SpecialChars_NewlinesInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\n\n\n}");
    }

    [Fact]
    public void SpecialChars_TabsInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{\t\t\t}");
    }

    [Fact]
    public void SpecialChars_MixedWhitespaceInBlock_RoundtripsCorrectly()
    {
        AssertRoundtrip("{ \t \n \r\n }");
    }

    #endregion

    #region Stress Tests

    [Fact]
    public void Stress_VeryDeepNesting_RoundtripsCorrectly()
    {
        const int depth = 100;
        var opening = new string('{', depth);
        var closing = new string('}', depth);
        var source = $"{opening}innermost{closing}";

        AssertRoundtrip(source);
    }

    [Fact]
    public void Stress_WideTree_RoundtripsCorrectly()
    {
        // Create a block with many siblings at each level
        var sb = new StringBuilder();
        sb.Append('{');
        for (int i = 0; i < 50; i++)
        {
            sb.Append($"{{child{i}}}");
        }
        sb.Append('}');

        var source = sb.ToString();
        AssertRoundtrip(source);
    }

    [Fact]
    public void Stress_LargeContent_RoundtripsCorrectly()
    {
        var content = new string('x', 10000);
        var source = $"{{{content}}}";

        AssertRoundtrip(source);
    }

    [Fact]
    public void Stress_ManyNestedBlocksWithContent_RoundtripsCorrectly()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 20; i++)
        {
            sb.Append('{');
            sb.Append($"level{i} ");
        }
        sb.Append("innermost");
        for (int i = 19; i >= 0; i--)
        {
            sb.Append($" level{i}");
            sb.Append('}');
        }

        var source = sb.ToString();
        AssertRoundtrip(source);
    }

    #endregion

    #region GLSL Real-World Tests

    private static TokenizerOptions GlslOptions => TokenizerOptions.Default
        .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .WithOperators(CommonOperators.CFamily)
        .WithTagPrefixes('#', '@');

    [Fact]
    public void Glsl_SimpleForLoop_RoundtripsCorrectly()
    {
        var source = @"void main() {
    for (int i = 0; i < 10; i++) {
        color += vec4(1.0);
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_NestedForLoops_RoundtripsCorrectly()
    {
        var source = @"void processMatrix() {
    for (int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            mat[i][j] = i * 4 + j;
        }
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_TripleNestedLoops_RoundtripsCorrectly()
    {
        var source = @"void process3D() {
    for (int x = 0; x < width; x++) {
        for (int y = 0; y < height; y++) {
            for (int z = 0; z < depth; z++) {
                float value = texture(volumeTex, vec3(x, y, z) / resolution).r;
                sum += value;
            }
        }
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_MultipleVariableDeclarations_RoundtripsCorrectly()
    {
        var source = @"void main() {
    float x = 1.0;
    float y = 2.0;
    float z = 3.0;
    vec3 position = vec3(x, y, z);
    vec4 color = vec4(position, 1.0);
    mat4 transform = mat4(1.0);
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_StructWithFields_RoundtripsCorrectly()
    {
        var source = @"struct Material {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    float shininess;
};

struct Light {
    vec3 position;
    vec3 color;
    float intensity;
};";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_NestedStructs_RoundtripsCorrectly()
    {
        var source = @"struct Inner {
    float value;
};

struct Outer {
    Inner data[4];
    int count;
};

void main() {
    Outer obj;
    for (int i = 0; i < 4; i++) {
        obj.data[i].value = float(i);
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_FieldAccessChain_RoundtripsCorrectly()
    {
        var source = @"void main() {
    float val = object.material.properties.ambient.r;
    object.transform.matrix[0][0] = 1.0;
    output.color.rgb = input.color.rgb * material.diffuse.rgb;
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_ComplexIndentation_RoundtripsCorrectly()
    {
        var source = @"#version 330 core

uniform sampler2D tex;
in vec2 uv;
out vec4 fragColor;

void main() {
    vec4 color = vec4(0.0);
    
    // Nested conditionals with loops
    if (uv.x > 0.5) {
        for (int i = 0; i < 4; i++) {
            if (i % 2 == 0) {
                color += texture(tex, uv + vec2(float(i) * 0.1, 0.0));
            } else {
                color -= texture(tex, uv - vec2(float(i) * 0.1, 0.0));
            }
        }
    }
    
    fragColor = color;
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_MixedCommentsAndCode_RoundtripsCorrectly()
    {
        var source = @"#version 330 core

/* 
 * Multi-line comment
 * describing the shader
 */

// Single line comment
uniform float time;

void main() {
    // Loop with inline comment
    for (int i = 0; i < 10; i++) { // iterate 10 times
        /* process each iteration */
        value += float(i);
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_WhileLoopWithBreakContinue_RoundtripsCorrectly()
    {
        var source = @"void search() {
    int i = 0;
    while (i < 100) {
        if (data[i] == target) {
            result = i;
            break;
        }
        if (data[i] < 0) {
            i++;
            continue;
        }
        process(data[i]);
        i++;
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_SwitchStatement_RoundtripsCorrectly()
    {
        var source = @"void selectColor(int mode) {
    switch (mode) {
        case 0:
            color = vec4(1.0, 0.0, 0.0, 1.0);
            break;
        case 1:
            color = vec4(0.0, 1.0, 0.0, 1.0);
            break;
        case 2:
            color = vec4(0.0, 0.0, 1.0, 1.0);
            break;
        default:
            color = vec4(1.0);
            break;
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_ArrayInitializersAndAccess_RoundtripsCorrectly()
    {
        var source = @"void main() {
    float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    vec2 offsets[9] = vec2[](
        vec2(-1.0, -1.0), vec2(0.0, -1.0), vec2(1.0, -1.0),
        vec2(-1.0,  0.0), vec2(0.0,  0.0), vec2(1.0,  0.0),
        vec2(-1.0,  1.0), vec2(0.0,  1.0), vec2(1.0,  1.0)
    );
    
    for (int i = 0; i < 9; i++) {
        sum += texture(tex, uv + offsets[i] * texelSize) * weights[i % 5];
    }
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_FunctionCallsWithNestedParens_RoundtripsCorrectly()
    {
        var source = @"void main() {
    float result = max(min(pow(abs(sin(time * 2.0)), 2.0), 1.0), 0.0);
    vec3 normal = normalize(cross(dFdx(position), dFdy(position)));
    float attenuation = 1.0 / (constant + linear * distance + quadratic * (distance * distance));
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_CompleteFragmentShader_RoundtripsCorrectly()
    {
        var source = @"#version 330 core

// Input from vertex shader
in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoords;

// Output
out vec4 FragColor;

// Uniforms
uniform sampler2D diffuseMap;
uniform sampler2D specularMap;
uniform vec3 lightPos;
uniform vec3 viewPos;
uniform vec3 lightColor;

struct Material {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    float shininess;
};

uniform Material material;

void main() {
    // Ambient
    vec3 ambient = lightColor * material.ambient * texture(diffuseMap, TexCoords).rgb;
    
    // Diffuse
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = lightColor * (diff * material.diffuse) * texture(diffuseMap, TexCoords).rgb;
    
    // Specular
    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
    vec3 specular = lightColor * (spec * material.specular) * texture(specularMap, TexCoords).rgb;
    
    // Combine
    vec3 result = ambient + diffuse + specular;
    FragColor = vec4(result, 1.0);
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_MultipleFunctionsWithSharedState_RoundtripsCorrectly()
    {
        var source = @"#version 330 core

// Shared state
vec3 globalLight = vec3(0.0);
float totalIntensity = 0.0;

void addPointLight(vec3 pos, vec3 color, float intensity) {
    for (int i = 0; i < 4; i++) {
        float falloff = 1.0 / (1.0 + float(i) * 0.1);
        globalLight += color * intensity * falloff;
        totalIntensity += intensity * falloff;
    }
}

void addDirectionalLight(vec3 dir, vec3 color) {
    float factor = max(dot(normal, -dir), 0.0);
    globalLight += color * factor;
}

void main() {
    // Reset
    globalLight = vec3(0.0);
    totalIntensity = 0.0;
    
    // Add lights
    for (int i = 0; i < numLights; i++) {
        if (lights[i].type == 0) {
            addPointLight(lights[i].position, lights[i].color, lights[i].intensity);
        } else {
            addDirectionalLight(lights[i].direction, lights[i].color);
        }
    }
    
    // Output
    fragColor = vec4(globalLight / max(totalIntensity, 1.0), 1.0);
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_MacroDirectives_RoundtripsCorrectly()
    {
        var source = @"#version 330 core
#define PI 3.14159265359
#define TWO_PI (PI * 2.0)
#define SAMPLE_COUNT 16

void main() {
    float angle = 0.0;
    vec3 result = vec3(0.0);
    
    for (int i = 0; i < SAMPLE_COUNT; i++) {
        angle = TWO_PI * float(i) / float(SAMPLE_COUNT);
        vec2 offset = vec2(cos(angle), sin(angle)) * radius;
        result += texture(tex, uv + offset).rgb;
    }
    
    result /= float(SAMPLE_COUNT);
    fragColor = vec4(result, 1.0);
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_TernaryOperatorsNested_RoundtripsCorrectly()
    {
        var source = @"void main() {
    float x = condition1 ? value1 : (condition2 ? value2 : value3);
    vec3 color = isDay 
        ? (isSunny ? sunColor : cloudColor)
        : (isMoonVisible ? moonColor : starColor);
    int index = a > b ? (b > c ? 2 : 1) : (a > c ? 1 : 0);
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_BlankLinesPreserved_RoundtripsCorrectly()
    {
        var source = @"#version 330 core


uniform float time;


void helper() {

    // Some code
    
}


void main() {
    
    vec4 color = vec4(0.0);
    
    
    color.r = sin(time);
    
    
    fragColor = color;
    
}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_TabsAndSpacesMixed_RoundtripsCorrectly()
    {
        var source = "void main() {\n\tvec4 color = vec4(0.0);\n    for (int i = 0; i < 4; i++) {\n\t    color += texture(tex, uv);\n    \t}\n\tfragColor = color;\n}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_WindowsLineEndings_RoundtripsCorrectly()
    {
        var source = "void main() {\r\n    for (int i = 0; i < 4; i++) {\r\n        color += vec4(1.0);\r\n    }\r\n}";
        AssertRoundtrip(source, GlslOptions);
    }

    [Fact]
    public void Glsl_MixedLineEndings_RoundtripsCorrectly()
    {
        var source = "void main() {\n    // Unix line\r\n    // Windows line\r    // Old Mac line\n}";
        AssertRoundtrip(source, GlslOptions);
    }

    #endregion
}
