using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

[Trait("Category", "Equality")]
public class GreenNodeEqualityTests
{
    [Fact]
    public void GreenLeaf_EquivalentConstructions_AreEqual_AndHashMatches()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        var trailing = ImmutableArray.Create(GreenTrivia.Newline("\n"));

        var a = new GreenLeaf(NodeKind.Ident, "x", leading, trailing);

        var b = new GreenLeaf(NodeKind.Ident, "x")
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(trailing);

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GreenLeaf_CacheAndNonCachePaths_BehaveConsistently()
    {
        // No trivia -> cached
        var cached1 = GreenNodeCache.GetOrCreate(NodeKind.Symbol, "{");
        var cached2 = GreenNodeCache.CreateDelimiter('{');

        Assert.Same(cached1, cached2);
        Assert.True(cached1.Equals(cached2));
        Assert.Equal(cached1.GetHashCode(), cached2.GetHashCode());

        // Trailing space -> cached "with space"
        var spaceTrivia = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        var cachedWithSpace = GreenNodeCache.GetOrCreateWithTrailingSpace(NodeKind.Symbol, "{");
        var viaFactory = GreenNodeCache.Create(NodeKind.Symbol, "{", ImmutableArray<GreenTrivia>.Empty, spaceTrivia);

        Assert.Same(cachedWithSpace, viaFactory);
        Assert.Equal(cachedWithSpace.GetHashCode(), viaFactory.GetHashCode());
        AssertHasWhitespaceButNoNewlineOrComment(cachedWithSpace);
    }

    [Fact]
    public void StructuralSharing_WithMethods_DoNotMutateOriginals()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "x");
        var changed = leaf.WithText("y");

        Assert.Equal("x", leaf.Text);
        Assert.Equal("y", changed.Text);
        Assert.False(leaf.Equals(changed));

        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2));

        var inserted = block.WithInsert(2, ImmutableArray.Create<GreenNode>(new GreenLeaf(NodeKind.Ident, "c")));

        Assert.Equal(4, block.SlotCount); // opener + 2 children + closer
        Assert.Equal(5, inserted.SlotCount); // opener + 3 children + closer

        // Original is unchanged and still shares children
        Assert.Same(child1, block.GetSlot(1));
        Assert.Same(child2, block.GetSlot(2));

        // Inserted block shares originals too
        Assert.Same(child1, inserted.GetSlot(1));
        Assert.Same(child2, inserted.GetSlot(3));

        Assert.False(block.Equals(inserted));
    }

    [Fact]
    public void OnlyContentChanges_CauseInequality()
    {
        // Same kind/text/trivia should be equal (baseline)
        var a = new GreenLeaf(NodeKind.Ident, "x");
        var b = new GreenLeaf(NodeKind.Ident, "x");
        Assert.True(a.Equals(b));

        // Different text -> not equal
        var c = new GreenLeaf(NodeKind.Ident, "y");
        Assert.False(a.Equals(c));

        // Different kind -> not equal
        var d = new GreenLeaf(NodeKind.Numeric, "x");
        Assert.False(a.Equals(d));
    }

    private static void AssertHasWhitespaceButNoNewlineOrComment(GreenLeaf leaf)
    {
        Assert.True((leaf.Flags & GreenNodeFlags.ContainsWhitespaceTrivia) != 0);
        Assert.True((leaf.Flags & GreenNodeFlags.ContainsNewlineTrivia) == 0);
        Assert.True((leaf.Flags & GreenNodeFlags.ContainsCommentTrivia) == 0);
    }
}
