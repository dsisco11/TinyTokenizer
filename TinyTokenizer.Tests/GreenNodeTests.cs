using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for GreenNode, GreenLeaf, and GreenBlock covering
/// all properties, methods, structural sharing, and edge cases.
/// </summary>
public class GreenNodeTests
{
    #region GreenNode Base Properties

    [Fact]
    public void GreenNode_IsContainer_TrueForBlocks()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        Assert.True(block.IsContainer);
    }

    [Fact]
    public void GreenNode_IsLeaf_TrueForLeaves()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        
        Assert.True(leaf.IsLeaf);
        Assert.False(leaf.IsContainer);
    }

    [Fact]
    public void GreenNode_IsContainer_FalseForLeaves()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        
        Assert.False(leaf.IsContainer);
    }

    [Fact]
    public void GreenNode_GetSlotOffset_ComputesCorrectly()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "aaa"); // width 3
        var child2 = new GreenLeaf(NodeKind.Ident, "bb");  // width 2
        var child3 = new GreenLeaf(NodeKind.Ident, "c");   // width 1
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        // Offset 0: after opener '{' (1 char)
        Assert.Equal(1, block.GetSlotOffset(0));
        // Offset 1: after child1 (1 + 3 = 4)
        Assert.Equal(4, block.GetSlotOffset(1));
        // Offset 2: after child2 (1 + 3 + 2 = 6)
        Assert.Equal(6, block.GetSlotOffset(2));
    }

    #endregion

    #region GreenLeaf Properties

    [Fact]
    public void GreenLeaf_Kind_ReturnsCorrectKind()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        
        Assert.Equal(NodeKind.Ident, leaf.Kind);
    }

    [Fact]
    public void GreenLeaf_Text_ReturnsText()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "hello");
        
        Assert.Equal("hello", leaf.Text);
    }

    [Fact]
    public void GreenLeaf_Width_EqualsTextLengthWithoutTrivia()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "hello");
        
        Assert.Equal(5, leaf.Width);
        Assert.Equal(5, leaf.TextWidth);
    }

    [Fact]
    public void GreenLeaf_Width_IncludesTrivia()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        var trailing = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        var leaf = new GreenLeaf(NodeKind.Ident, "foo", leading, trailing);
        
        Assert.Equal(6, leaf.Width); // 2 + 3 + 1
        Assert.Equal(3, leaf.TextWidth);
    }

    [Fact]
    public void GreenLeaf_LeadingTriviaWidth_ComputedCorrectly()
    {
        var leading = ImmutableArray.Create(
            GreenTrivia.Whitespace("  "),
            GreenTrivia.Whitespace("\t"));
        var leaf = new GreenLeaf(NodeKind.Ident, "x", leading);
        
        Assert.Equal(3, leaf.LeadingTriviaWidth); // 2 + 1
    }

    [Fact]
    public void GreenLeaf_TrailingTriviaWidth_ComputedCorrectly()
    {
        var trailing = ImmutableArray.Create(GreenTrivia.Whitespace("   "));
        var leaf = new GreenLeaf(NodeKind.Ident, "x", default, trailing);
        
        Assert.Equal(3, leaf.TrailingTriviaWidth);
    }

    [Fact]
    public void GreenLeaf_TextOffset_EqualsLeadingTriviaWidth()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace("    "));
        var leaf = new GreenLeaf(NodeKind.Ident, "test", leading);
        
        Assert.Equal(4, leaf.TextOffset);
        Assert.Equal(leaf.LeadingTriviaWidth, leaf.TextOffset);
    }

    [Fact]
    public void GreenLeaf_SlotCount_IsZero()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        
        Assert.Equal(0, leaf.SlotCount);
    }

    [Fact]
    public void GreenLeaf_GetSlot_ReturnsNull()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        
        Assert.Null(leaf.GetSlot(0));
        Assert.Null(leaf.GetSlot(1));
        Assert.Null(leaf.GetSlot(-1));
    }

    [Fact]
    public void GreenLeaf_DefaultTrivia_IsEmpty()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "test");
        
        Assert.Empty(leaf.LeadingTrivia);
        Assert.Empty(leaf.TrailingTrivia);
    }

    #endregion

    #region GreenLeaf Mutations

    [Fact]
    public void GreenLeaf_WithLeadingTrivia_CreatesNewLeaf()
    {
        var original = new GreenLeaf(NodeKind.Ident, "test");
        var trivia = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        
        var modified = original.WithLeadingTrivia(trivia);
        
        Assert.NotSame(original, modified);
        Assert.Equal("test", modified.Text);
        Assert.Single(modified.LeadingTrivia);
        Assert.Empty(original.LeadingTrivia); // Original unchanged
    }

    [Fact]
    public void GreenLeaf_WithTrailingTrivia_CreatesNewLeaf()
    {
        var original = new GreenLeaf(NodeKind.Ident, "test");
        var trivia = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        
        var modified = original.WithTrailingTrivia(trivia);
        
        Assert.NotSame(original, modified);
        Assert.Equal("test", modified.Text);
        Assert.Single(modified.TrailingTrivia);
    }

    [Fact]
    public void GreenLeaf_WithText_PreservesTrivia()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        var trailing = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        var original = new GreenLeaf(NodeKind.Ident, "old", leading, trailing);
        
        var modified = original.WithText("new");
        
        Assert.Equal("new", modified.Text);
        Assert.Equal(leading.Length, modified.LeadingTrivia.Length);
        Assert.Equal(trailing.Length, modified.TrailingTrivia.Length);
    }

    [Fact]
    public void GreenLeaf_WithText_UpdatesWidth()
    {
        var original = new GreenLeaf(NodeKind.Ident, "short");
        var modified = original.WithText("muchlonger");
        
        Assert.Equal(5, original.Width);
        Assert.Equal(10, modified.Width);
    }

    #endregion

    #region GreenLeaf CreateRed

    [Fact]
    public void GreenLeaf_CreateRed_ReturnsRedLeaf()
    {
        var green = new GreenLeaf(NodeKind.Ident, "test");
        
        var red = green.CreateRed(null, 0);
        
        Assert.IsType<RedLeaf>(red);
        Assert.Equal(0, red.Position);
        Assert.Null(red.Parent);
    }

    [Fact]
    public void GreenLeaf_CreateRed_WithPosition()
    {
        var green = new GreenLeaf(NodeKind.Ident, "test");
        
        var red = green.CreateRed(null, 42);
        
        Assert.Equal(42, red.Position);
    }

    #endregion

    #region GreenBlock Properties

    [Fact]
    public void GreenBlock_Kind_CorrectForBraces()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        Assert.Equal(NodeKind.BraceBlock, block.Kind);
    }

    [Fact]
    public void GreenBlock_Kind_CorrectForBrackets()
    {
        var block = GreenBlock.Create('[', ImmutableArray<GreenNode>.Empty);
        
        Assert.Equal(NodeKind.BracketBlock, block.Kind);
    }

    [Fact]
    public void GreenBlock_Kind_CorrectForParens()
    {
        var block = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        Assert.Equal(NodeKind.ParenBlock, block.Kind);
    }

    [Fact]
    public void GreenBlock_OpenerCloser_BraceBlock()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        Assert.Equal('{', block.Opener);
        Assert.Equal('}', block.Closer);
    }

    [Fact]
    public void GreenBlock_OpenerCloser_BracketBlock()
    {
        var block = GreenBlock.Create('[', ImmutableArray<GreenNode>.Empty);
        
        Assert.Equal('[', block.Opener);
        Assert.Equal(']', block.Closer);
    }

    [Fact]
    public void GreenBlock_OpenerCloser_ParenBlock()
    {
        var block = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        
        Assert.Equal('(', block.Opener);
        Assert.Equal(')', block.Closer);
    }

    [Fact]
    public void GreenBlock_Width_EmptyBlock()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        // Width = opener(1) + closer(1) = 2
        Assert.Equal(2, block.Width);
    }

    [Fact]
    public void GreenBlock_Width_WithChildren()
    {
        var children = ImmutableArray.Create<GreenNode>(
            new GreenLeaf(NodeKind.Ident, "abc")); // width 3
        var block = GreenBlock.Create('{', children);
        
        // Width = opener(1) + children(3) + closer(1) = 5
        Assert.Equal(5, block.Width);
    }

    [Fact]
    public void GreenBlock_Width_WithTrivia()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        var trailing = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty, leading, closerTrailingTrivia: trailing);
        
        // Width = leading(1) + opener(1) + closer(1) + trailing(2) = 5
        Assert.Equal(5, block.Width);
    }

    [Fact]
    public void GreenBlock_SlotCount_ReturnsChildCount()
    {
        var children = ImmutableArray.Create<GreenNode>(
            new GreenLeaf(NodeKind.Ident, "a"),
            new GreenLeaf(NodeKind.Ident, "b"),
            new GreenLeaf(NodeKind.Ident, "c"));
        var block = GreenBlock.Create('{', children);
        
        Assert.Equal(3, block.SlotCount);
    }

    [Fact]
    public void GreenBlock_GetSlot_ReturnsChild()
    {
        var child = new GreenLeaf(NodeKind.Ident, "test");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        Assert.Same(child, block.GetSlot(0));
    }

    [Fact]
    public void GreenBlock_GetSlot_OutOfRange_ReturnsNull()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        Assert.Null(block.GetSlot(0));
        Assert.Null(block.GetSlot(-1));
        Assert.Null(block.GetSlot(100));
    }

    [Fact]
    public void GreenBlock_TriviaWidth_Computed()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        var trailing = ImmutableArray.Create(GreenTrivia.Whitespace("   "));
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty, leading, closerTrailingTrivia: trailing);
        
        Assert.Equal(2, block.LeadingTriviaWidth);
        Assert.Equal(3, block.TrailingTriviaWidth);
    }

    #endregion

    #region GreenBlock GetSlotOffset

    [Fact]
    public void GreenBlock_GetSlotOffset_SmallBlock()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "aa");  // width 2
        var child2 = new GreenLeaf(NodeKind.Ident, "bbb"); // width 3
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2));
        
        // slot 0: after opener = 1
        Assert.Equal(1, block.GetSlotOffset(0));
        // slot 1: after opener + child1 = 1 + 2 = 3
        Assert.Equal(3, block.GetSlotOffset(1));
    }

    [Fact]
    public void GreenBlock_GetSlotOffset_WithLeadingTrivia()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace("  ")); // width 2
        var child = new GreenLeaf(NodeKind.Ident, "x");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child), leading);
        
        // slot 0: leading trivia(2) + opener(1) = 3
        Assert.Equal(3, block.GetSlotOffset(0));
    }

    [Fact]
    public void GreenBlock_GetSlotOffset_LargeBlock_UsesPrecomputed()
    {
        // Create block with 10+ children to trigger precomputed offsets
        var children = new List<GreenNode>();
        for (int i = 0; i < 15; i++)
        {
            children.Add(new GreenLeaf(NodeKind.Ident, new string('x', i + 1)));
        }
        var block = GreenBlock.Create('{', children.ToImmutableArray());
        
        // Verify offsets are computed correctly
        int expectedOffset = 1; // After opener
        for (int i = 0; i < 15; i++)
        {
            Assert.Equal(expectedOffset, block.GetSlotOffset(i));
            expectedOffset += children[i].Width;
        }
    }

    #endregion

    #region GreenBlock Structural Sharing Mutations

    [Fact]
    public void GreenBlock_WithSlot_ReplacesChild()
    {
        var original = new GreenLeaf(NodeKind.Ident, "old");
        var replacement = new GreenLeaf(NodeKind.Ident, "new");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(original));
        
        var modified = block.WithSlot(0, replacement);
        
        Assert.NotSame(block, modified);
        Assert.Same(replacement, modified.GetSlot(0));
        Assert.Same(original, block.GetSlot(0)); // Original unchanged
    }

    [Fact]
    public void GreenBlock_WithSlot_SharesUnchangedChildren()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var replacement = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        var modified = block.WithSlot(1, replacement);
        
        // child1 and child3 should be shared
        Assert.Same(child1, modified.GetSlot(0));
        Assert.Same(replacement, modified.GetSlot(1));
        Assert.Same(child3, modified.GetSlot(2));
    }

    [Fact]
    public void GreenBlock_WithSlot_InvalidIndex_Throws()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var child = new GreenLeaf(NodeKind.Ident, "x");
        
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithSlot(0, child));
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithSlot(-1, child));
    }

    [Fact]
    public void GreenBlock_WithInsert_InsertsAtIndex()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var inserted = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2));
        
        var modified = block.WithInsert(1, ImmutableArray.Create<GreenNode>(inserted));
        
        Assert.Equal(3, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(0));
        Assert.Same(inserted, modified.GetSlot(1));
        Assert.Same(child2, modified.GetSlot(2));
    }

    [Fact]
    public void GreenBlock_WithInsert_AtStart()
    {
        var child = new GreenLeaf(NodeKind.Ident, "a");
        var inserted = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        var modified = block.WithInsert(0, ImmutableArray.Create<GreenNode>(inserted));
        
        Assert.Same(inserted, modified.GetSlot(0));
        Assert.Same(child, modified.GetSlot(1));
    }

    [Fact]
    public void GreenBlock_WithInsert_AtEnd()
    {
        var child = new GreenLeaf(NodeKind.Ident, "a");
        var inserted = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        var modified = block.WithInsert(1, ImmutableArray.Create<GreenNode>(inserted));
        
        Assert.Same(child, modified.GetSlot(0));
        Assert.Same(inserted, modified.GetSlot(1));
    }

    [Fact]
    public void GreenBlock_WithInsert_MultipleNodes()
    {
        var original = new GreenLeaf(NodeKind.Ident, "a");
        var insert1 = new GreenLeaf(NodeKind.Ident, "X");
        var insert2 = new GreenLeaf(NodeKind.Ident, "Y");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(original));
        
        var modified = block.WithInsert(0, ImmutableArray.Create<GreenNode>(insert1, insert2));
        
        Assert.Equal(3, modified.SlotCount);
        Assert.Same(insert1, modified.GetSlot(0));
        Assert.Same(insert2, modified.GetSlot(1));
        Assert.Same(original, modified.GetSlot(2));
    }

    [Fact]
    public void GreenBlock_WithInsert_InvalidIndex_Throws()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var child = new GreenLeaf(NodeKind.Ident, "x");
        
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithInsert(-1, ImmutableArray.Create<GreenNode>(child)));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithInsert(10, ImmutableArray.Create<GreenNode>(child)));
    }

    [Fact]
    public void GreenBlock_WithRemove_RemovesChildren()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        var modified = block.WithRemove(1, 1);
        
        Assert.Equal(2, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(0));
        Assert.Same(child3, modified.GetSlot(1));
    }

    [Fact]
    public void GreenBlock_WithRemove_MultipleChildren()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var child4 = new GreenLeaf(NodeKind.Ident, "d");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3, child4));
        
        var modified = block.WithRemove(1, 2); // Remove b and c
        
        Assert.Equal(2, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(0));
        Assert.Same(child4, modified.GetSlot(1));
    }

    [Fact]
    public void GreenBlock_WithRemove_InvalidRange_Throws()
    {
        var child = new GreenLeaf(NodeKind.Ident, "a");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithRemove(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithRemove(0, 5));
    }

    [Fact]
    public void GreenBlock_WithReplace_ReplacesRange()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var replacement = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        var modified = block.WithReplace(1, 1, ImmutableArray.Create<GreenNode>(replacement));
        
        Assert.Equal(3, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(0));
        Assert.Same(replacement, modified.GetSlot(1));
        Assert.Same(child3, modified.GetSlot(2));
    }

    [Fact]
    public void GreenBlock_WithReplace_ExpandsRange()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var repl1 = new GreenLeaf(NodeKind.Ident, "X");
        var repl2 = new GreenLeaf(NodeKind.Ident, "Y");
        var repl3 = new GreenLeaf(NodeKind.Ident, "Z");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2));
        
        // Replace 1 child with 3
        var modified = block.WithReplace(1, 1, ImmutableArray.Create<GreenNode>(repl1, repl2, repl3));
        
        Assert.Equal(4, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(0));
        Assert.Same(repl1, modified.GetSlot(1));
        Assert.Same(repl2, modified.GetSlot(2));
        Assert.Same(repl3, modified.GetSlot(3));
    }

    [Fact]
    public void GreenBlock_WithReplace_ContractsRange()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var replacement = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        // Replace 2 children with 1
        var modified = block.WithReplace(0, 2, ImmutableArray.Create<GreenNode>(replacement));
        
        Assert.Equal(2, modified.SlotCount);
        Assert.Same(replacement, modified.GetSlot(0));
        Assert.Same(child3, modified.GetSlot(1));
    }

    [Fact]
    public void GreenBlock_WithReplace_InvalidRange_Throws()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var child = new GreenLeaf(NodeKind.Ident, "x");
        
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithReplace(-1, 1, ImmutableArray.Create<GreenNode>(child)));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithReplace(0, 5, ImmutableArray.Create<GreenNode>(child)));
    }

    [Fact]
    public void GreenBlock_WithLeadingTrivia_CreatesNewBlock()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var trivia = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        
        var modified = block.WithLeadingTrivia(trivia);
        
        Assert.NotSame(block, modified);
        Assert.Single(modified.LeadingTrivia);
        Assert.Empty(block.LeadingTrivia);
    }

    [Fact]
    public void GreenBlock_WithTrailingTrivia_CreatesNewBlock()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var trivia = ImmutableArray.Create(GreenTrivia.Whitespace("  "));
        
        var modified = block.WithTrailingTrivia(trivia);
        
        Assert.NotSame(block, modified);
        Assert.Single(modified.TrailingTrivia);
    }

    #endregion

    #region GreenBlock CreateRed

    [Fact]
    public void GreenBlock_CreateRed_ReturnsRedBlock()
    {
        var green = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        var red = green.CreateRed(null, 0);
        
        Assert.IsType<RedBlock>(red);
    }

    [Fact]
    public void GreenBlock_CreateRed_WithPosition()
    {
        var green = GreenBlock.Create('[', ImmutableArray<GreenNode>.Empty);
        
        var red = green.CreateRed(null, 100);
        
        Assert.Equal(100, red.Position);
    }

    #endregion

    #region GreenBlock Invalid Opener

    [Fact]
    public void GreenBlock_InvalidOpener_Throws()
    {
        Assert.Throws<ArgumentException>(() => 
            GreenBlock.Create('x', ImmutableArray<GreenNode>.Empty));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GreenLeaf_EmptyText()
    {
        var leaf = new GreenLeaf(NodeKind.Ident, "");
        
        Assert.Equal(0, leaf.Width);
        Assert.Equal(0, leaf.TextWidth);
        Assert.Equal("", leaf.Text);
    }

    [Fact]
    public void GreenBlock_NestedBlocks()
    {
        var inner = GreenBlock.Create('(', ImmutableArray<GreenNode>.Empty);
        var outer = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(inner));
        
        Assert.Equal(4, outer.Width); // { + ( + ) + }
        Assert.Same(inner, outer.GetSlot(0));
    }

    [Fact]
    public void GreenBlock_DeeplyNested()
    {
        GreenNode current = new GreenLeaf(NodeKind.Ident, "x");
        for (int i = 0; i < 5; i++)
        {
            current = GreenBlock.Create('{', ImmutableArray.Create(current));
        }
        
        // Each layer adds 2 chars for delimiters
        Assert.Equal(1 + 5 * 2, current.Width); // x + 5 * {}
    }

    [Fact]
    public void GreenLeaf_AllNodeKinds()
    {
        var kinds = new[] { NodeKind.Ident, NodeKind.Numeric, NodeKind.String, NodeKind.Operator, NodeKind.Symbol };
        
        foreach (var kind in kinds)
        {
            var leaf = new GreenLeaf(kind, "test");
            Assert.Equal(kind, leaf.Kind);
            Assert.True(leaf.IsLeaf);
        }
    }

    [Fact]
    public void GreenBlock_DefaultTrivia_IsEmpty()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        Assert.Empty(block.LeadingTrivia);
        Assert.Empty(block.TrailingTrivia);
        Assert.Equal(0, block.LeadingTriviaWidth);
        Assert.Equal(0, block.TrailingTriviaWidth);
    }

    #endregion
}
