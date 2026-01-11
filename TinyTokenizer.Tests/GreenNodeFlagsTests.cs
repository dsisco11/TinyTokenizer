using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

[Trait("Category", "Flags")]
public class GreenNodeFlagsTests
{
    #region Helpers

    private static ImmutableArray<GreenLeaf> ParseLeaves(string source, TokenizerOptions? options = null)
    {
        var lexer = new GreenLexer(options ?? TokenizerOptions.Default);
        var nodes = lexer.ParseToGreenNodes(source);
        return CollectLeaves(nodes);
    }

    private static ImmutableArray<GreenLeaf> CollectLeaves(ImmutableArray<GreenNode> nodes)
    {
        var builder = ImmutableArray.CreateBuilder<GreenLeaf>();
        foreach (var node in nodes)
        {
            CollectLeavesRecursive(node, builder);
        }
        return builder.ToImmutable();
    }

    private static void CollectLeavesRecursive(GreenNode node, ImmutableArray<GreenLeaf>.Builder builder)
    {
        if (node is GreenLeaf leaf)
        {
            builder.Add(leaf);
            return;
        }

        for (int i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child != null)
                CollectLeavesRecursive(child, builder);
        }
    }

    private static GreenLeaf FindLeaf(ImmutableArray<GreenLeaf> leaves, string text) =>
        Assert.Single(leaves.Where(l => l.Text == text));

    private static void AssertHas(GreenNodeFlags flags, GreenNodeFlags expected)
    {
        Assert.True((flags & expected) == expected, $"Expected flags to include {expected} but was {flags}");
    }

    private static void AssertNotHas(GreenNodeFlags flags, GreenNodeFlags unexpected)
    {
        Assert.True((flags & unexpected) == 0, $"Expected flags to NOT include {unexpected} but was {flags}");
    }

    #endregion

    [Fact]
    public void GreenLeaf_Flags_ReflectLeadingAndTrailingTriviaKinds()
    {
        var leading = ImmutableArray.Create(
            GreenTrivia.Whitespace("  "),
            GreenTrivia.SingleLineComment("// c"),
            GreenTrivia.Newline("\n"));

        var trailing = ImmutableArray.Create(
            GreenTrivia.Whitespace("\t"),
            GreenTrivia.MultiLineComment("/* m */"));

        var leaf = new GreenLeaf(NodeKind.Ident, "x", leading, trailing);

        AssertHas(leaf.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertHas(leaf.Flags, GreenNodeFlags.HasLeadingCommentTrivia);
        AssertHas(leaf.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);

        AssertHas(leaf.Flags, GreenNodeFlags.HasTrailingWhitespaceTrivia);
        AssertHas(leaf.Flags, GreenNodeFlags.HasTrailingCommentTrivia);
        AssertNotHas(leaf.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);

        AssertHas(leaf.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
        AssertHas(leaf.Flags, GreenNodeFlags.ContainsCommentTrivia);
        AssertHas(leaf.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void GreenLeaf_Flags_IncludeKindDerivedSubtreeBits()
    {
        var keywordKind = NodeKindExtensions.KeywordKind(0);

        var keywordLeaf = new GreenLeaf(keywordKind, "if");
        AssertHas(keywordLeaf.Flags, GreenNodeFlags.ContainsKeyword);
        AssertNotHas(keywordLeaf.Flags, GreenNodeFlagMasks.Boundary | GreenNodeFlags.ContainsWhitespaceTrivia | GreenNodeFlags.ContainsCommentTrivia | GreenNodeFlags.ContainsNewlineTrivia);

        var taggedLeaf = new GreenLeaf(NodeKind.TaggedIdent, "#define");
        AssertHas(taggedLeaf.Flags, GreenNodeFlags.ContainsTaggedIdent);

        var errorLeaf = new GreenLeaf(NodeKind.Error, "<error>");
        AssertHas(errorLeaf.Flags, GreenNodeFlags.ContainsErrorNode);

        var normalLeaf = new GreenLeaf(NodeKind.Ident, "x");
        Assert.Equal(GreenNodeFlags.None, normalLeaf.Flags);
    }

    [Fact]
    public void GreenBlock_Flags_AggregateContains_AndUseOpenerLeadingPlusCloserTrailingAsBoundary()
    {
        var opener = new GreenLeaf(
            NodeKind.Symbol,
            "{",
            leadingTrivia: ImmutableArray.Create(GreenTrivia.Newline("\n")),
            trailingTrivia: ImmutableArray.Create(GreenTrivia.Whitespace(" ")));

        var closer = new GreenLeaf(
            NodeKind.Symbol,
            "}",
            leadingTrivia: ImmutableArray.Create(GreenTrivia.Newline("\n")),
            trailingTrivia: ImmutableArray.Create(GreenTrivia.Whitespace(" ")));

        var inner = new GreenLeaf(
            NodeKind.Ident,
            "x",
            trailingTrivia: ImmutableArray.Create(GreenTrivia.SingleLineComment("// c")));

        var block = new GreenBlock(opener, closer, ImmutableArray.Create<GreenNode>(inner));

        // Boundary behavior
        AssertHas(block.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);
        AssertHas(block.Flags, GreenNodeFlags.HasTrailingWhitespaceTrivia);

        // Not from opener trailing / closer leading
        AssertNotHas(block.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertNotHas(block.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);

        // Subtree contains aggregation
        AssertHas(block.Flags, GreenNodeFlags.ContainsNewlineTrivia);
        AssertHas(block.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
        AssertHas(block.Flags, GreenNodeFlags.ContainsCommentTrivia);
    }

    [Fact]
    public void GreenList_Flags_UseFirstLeadingAndLastTrailingAsBoundary()
    {
        var first = new GreenLeaf(NodeKind.Ident, "a", leadingTrivia: ImmutableArray.Create(GreenTrivia.Whitespace("  ")));
        var middle = new GreenLeaf(NodeKind.Ident, "b", trailingTrivia: ImmutableArray.Create(GreenTrivia.SingleLineComment("// c")));
        var last = new GreenLeaf(NodeKind.Ident, "c", trailingTrivia: ImmutableArray.Create(GreenTrivia.Newline("\n")));

        var list = new GreenList(ImmutableArray.Create<GreenNode>(first, middle, last));

        AssertHas(list.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertHas(list.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);

        AssertHas(list.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
        AssertHas(list.Flags, GreenNodeFlags.ContainsCommentTrivia);
        AssertHas(list.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void GreenSyntaxNode_Flags_AggregateLikeList()
    {
        var kind = NodeKindExtensions.SemanticKind(0);

        var first = new GreenLeaf(NodeKind.Ident, "a", leadingTrivia: ImmutableArray.Create(GreenTrivia.Whitespace(" ")));
        var last = new GreenLeaf(NodeKind.Ident, "b", trailingTrivia: ImmutableArray.Create(GreenTrivia.Newline("\n")));

        var node = new GreenSyntaxNode(kind, first, last);

        AssertHas(node.Flags, GreenNodeFlags.HasLeadingWhitespaceTrivia);
        AssertHas(node.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertHas(node.Flags, GreenNodeFlags.ContainsWhitespaceTrivia);
        AssertHas(node.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }

    [Fact]
    public void ParsedNewlineOwnership_ReflectedInFlags_CommonCase()
    {
        var leaves = ParseLeaves("a\nb");

        var a = FindLeaf(leaves, "a");
        var b = FindLeaf(leaves, "b");

        AssertHas(a.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertHas(a.Flags, GreenNodeFlags.ContainsNewlineTrivia);

        AssertNotHas(b.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);
    }

    [Fact]
    public void ParsedSameLineCommentOwnership_ReflectedInFlags()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("a // c\nb", options);

        var a = FindLeaf(leaves, "a");
        var b = FindLeaf(leaves, "b");

        AssertHas(a.Flags, GreenNodeFlags.HasTrailingCommentTrivia);
        AssertHas(a.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);
        AssertHas(a.Flags, GreenNodeFlags.ContainsCommentTrivia);
        AssertHas(a.Flags, GreenNodeFlags.ContainsNewlineTrivia);

        Assert.Empty(b.LeadingTrivia);
        AssertNotHas(b.Flags, GreenNodeFlags.HasLeadingCommentTrivia | GreenNodeFlags.HasLeadingNewlineTrivia);
    }

    [Fact]
    public void ParsedOwnLineCommentOwnership_ReflectedInFlags()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("a\n// c\nb", options);

        var a = FindLeaf(leaves, "a");
        var b = FindLeaf(leaves, "b");

        AssertHas(a.Flags, GreenNodeFlags.HasTrailingNewlineTrivia);

        AssertHas(b.Flags, GreenNodeFlags.HasLeadingCommentTrivia);
        AssertHas(b.Flags, GreenNodeFlags.HasLeadingNewlineTrivia);
        AssertHas(b.Flags, GreenNodeFlags.ContainsCommentTrivia);
        AssertHas(b.Flags, GreenNodeFlags.ContainsNewlineTrivia);
    }
}
