using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

[Trait("Category", "AST")]
public sealed class SyntaxBinderBlockReconstructionTests
{
    public static TheoryData<string, bool> LineEndingsAndBracePlacements => new()
    {
        { "\n", false },
        { "\n", true },
        { "\r\n", false },
        { "\r\n", true }
    };

    [Theory]
    [MemberData(nameof(LineEndingsAndBracePlacements))]
    public void InitialBinding_RecognizedSyntaxInsideBlock_PreservesSourceAndDelimiters(
        string newline,
        bool braceOnNextLine)
    {
        var source = CreateShader(newline, braceOnNextLine);

        var tree = SyntaxTree.Parse(source, CreateDirectiveSchema());

        Assert.Equal(source, tree.ToText());
        Assert.Equal(2, tree.Select(Query.Syntax<TestDirectiveSyntax>()).Count());
        AssertDelimiterCounts(tree, expectedPairs: 1);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void InitialBinding_ClosingDelimiterComment_PreservesExactTrivia(string newline)
    {
        var source = CreateShader(newline, braceOnNextLine: false) + " // close" + newline;

        var tree = SyntaxTree.Parse(source, CreateDirectiveSchema());

        Assert.Equal(source, tree.ToText());
        Assert.Equal(1, CountOccurrences(tree.ToText(), "// close"));
        AssertDelimiterCounts(tree, expectedPairs: 1);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Rebinding_AfterConcreteChildEdit_PreservesSourceAndDelimiters(string newline)
    {
        var source = CreateShader(newline, braceOnNextLine: false);
        var tree = SyntaxTree.Parse(source, CreateDirectiveSchema());

        tree.CreateEditor()
            .Replace(Query.Ident("value"), "updated")
            .Commit();

        Assert.Equal(source.Replace("value", "updated", StringComparison.Ordinal), tree.ToText());
        AssertDelimiterCounts(tree, expectedPairs: 1);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void InnerEnd_AfterBinding_InsertsBeforeClosingDelimiter(string newline)
    {
        var source = CreateShader(newline, braceOnNextLine: false);
        var insertion = "float inserted;" + newline;
        var expected = source.Insert(source.LastIndexOf('}'), insertion);
        var tree = SyntaxTree.Parse(source, CreateDirectiveSchema());

        tree.CreateEditor()
            .InsertBefore(Query.BraceBlock.First().End(), insertion)
            .Commit();

        Assert.Equal(expected, tree.ToText());
        AssertDelimiterCounts(tree, expectedPairs: 1);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void InitialBinding_RecognizedSyntaxInsideAuthoredNestedBlock_PreservesStructure(string newline)
    {
        var source = "void main() {" + newline
            + "{" + newline
            + "#if FEATURE" + newline
            + "float value;" + newline
            + "#endif" + newline
            + "}" + newline
            + "}";

        var tree = SyntaxTree.Parse(source, CreateDirectiveSchema());

        Assert.Equal(source, tree.ToText());
        Assert.Equal(2, tree.Select(Query.Syntax<TestDirectiveSyntax>()).Count());
        AssertDelimiterCounts(tree, expectedPairs: 2);
    }

    private static Schema CreateDirectiveSchema() => Schema.Create()
        .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .WithTagPrefixes('#')
        .DefineSyntax(Syntax.Define<TestFunctionSyntax>("testFunction")
            .Match(Query.AnyIdent, Query.AnyIdent, Query.ParenBlock, Query.BraceBlock)
            .WithPriority(10)
            .Build())
        .DefineSyntax(Syntax.Define<TestDirectiveSyntax>("testDirective")
            .Match(Query.AnyTaggedIdent, Query.Any.Until(Query.Newline))
            .Build())
        .Build();

    private static string CreateShader(string newline, bool braceOnNextLine)
    {
        var beforeBrace = braceOnNextLine ? newline : " ";
        return "void main()" + beforeBrace + "{" + newline
            + "#if FEATURE" + newline
            + "float value;" + newline
            + "#endif" + newline
            + "}";
    }

    private static void AssertDelimiterCounts(SyntaxTree tree, int expectedPairs)
    {
        Assert.Equal(expectedPairs, CountLeaves(tree.GreenRoot, "{"));
        Assert.Equal(expectedPairs, CountLeaves(tree.GreenRoot, "}"));
    }

    private static int CountLeaves(GreenNode node, string text)
    {
        var count = node is GreenLeaf leaf && leaf.Text == text ? 1 : 0;
        for (var index = 0; index < node.SlotCount; index++)
        {
            if (node.GetSlot(index) is { } child)
                count += CountLeaves(child, text);
        }

        return count;
    }

    private static int CountOccurrences(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private sealed class TestDirectiveSyntax : SyntaxNode
    {
        internal TestDirectiveSyntax(CreationContext context)
            : base(context)
        {
        }
    }

    private sealed class TestFunctionSyntax : SyntaxNode
    {
        internal TestFunctionSyntax(CreationContext context)
            : base(context)
        {
        }
    }
}