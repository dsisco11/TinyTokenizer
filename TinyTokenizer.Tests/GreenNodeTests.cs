using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for GreenNode, GreenLeaf, and GreenBlock covering
/// all properties, methods, structural sharing, and edge cases.
/// </summary>
[Trait("Category", "AST")]
[Trait("Category", "GreenNode")]
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
        
        // Slot 0: opener '{' at offset 0
        Assert.Equal(0, block.GetSlotOffset(0));
        // Slot 1: first inner child after opener (1)
        Assert.Equal(1, block.GetSlotOffset(1));
        // Slot 2: after child1 (1 + 3 = 4)
        Assert.Equal(4, block.GetSlotOffset(2));
        // Slot 3: after child2 (1 + 3 + 2 = 6)
        Assert.Equal(6, block.GetSlotOffset(3));
        // Slot 4: closer after all children (1 + 3 + 2 + 1 = 7)
        Assert.Equal(7, block.GetSlotOffset(4));
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
        
        Assert.IsType<SyntaxToken>(red);
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
        
        // SlotCount = opener + 3 inner children + closer = 5
        Assert.Equal(5, block.SlotCount);
        // InnerChildren should still be 3
        Assert.Equal(3, block.InnerChildren.Length);
    }

    [Fact]
    public void GreenBlock_GetSlot_ReturnsChild()
    {
        var child = new GreenLeaf(NodeKind.Ident, "test");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        // Slot 0 is opener, slot 1 is first inner child, slot 2 is closer
        Assert.Equal(NodeKind.Symbol, block.GetSlot(0)!.Kind); // opener
        Assert.Same(child, block.GetSlot(1)); // inner child
        Assert.Equal(NodeKind.Symbol, block.GetSlot(2)!.Kind); // closer
    }

    [Fact]
    public void GreenBlock_GetSlot_OutOfRange_ReturnsNull()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        
        // Empty block has 2 slots: opener (0) and closer (1)
        Assert.NotNull(block.GetSlot(0)); // opener
        Assert.NotNull(block.GetSlot(1)); // closer
        Assert.Null(block.GetSlot(2));    // out of range
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
        
        // slot 0: opener at offset 0
        Assert.Equal(0, block.GetSlotOffset(0));
        // slot 1: first child after opener = 1
        Assert.Equal(1, block.GetSlotOffset(1));
        // slot 2: after opener + child1 = 1 + 2 = 3
        Assert.Equal(3, block.GetSlotOffset(2));
        // slot 3: closer after all = 1 + 2 + 3 = 6
        Assert.Equal(6, block.GetSlotOffset(3));
    }

    [Fact]
    public void GreenBlock_GetSlotOffset_WithLeadingTrivia()
    {
        var leading = ImmutableArray.Create(GreenTrivia.Whitespace("  ")); // width 2
        var child = new GreenLeaf(NodeKind.Ident, "x");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child), leading);
        
        // slot 0: opener at offset 0 (trivia is part of opener's width)
        Assert.Equal(0, block.GetSlotOffset(0));
        // slot 1: first child after opener = leading trivia(2) + opener(1) = 3
        Assert.Equal(3, block.GetSlotOffset(1));
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
        
        // Slot 0 is opener at offset 0
        Assert.Equal(0, block.GetSlotOffset(0));
        
        // Verify inner child offsets are computed correctly (slots 1..15)
        int expectedOffset = 1; // After opener
        for (int i = 0; i < 15; i++)
        {
            Assert.Equal(expectedOffset, block.GetSlotOffset(i + 1)); // +1 because slot 0 is opener
            expectedOffset += children[i].Width;
        }
        
        // Slot 16 is closer
        Assert.Equal(expectedOffset, block.GetSlotOffset(16));
    }

    #endregion

    #region GreenBlock Structural Sharing Mutations

    [Fact]
    public void GreenBlock_WithSlot_ReplacesChild()
    {
        var original = new GreenLeaf(NodeKind.Ident, "old");
        var replacement = new GreenLeaf(NodeKind.Ident, "new");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(original));
        
        // Replace slot 1 (first inner child)
        var modified = block.WithSlot(1, replacement);
        
        Assert.NotSame(block, modified);
        Assert.Same(replacement, modified.GetSlot(1));
        Assert.Same(original, block.GetSlot(1)); // Original unchanged
    }

    [Fact]
    public void GreenBlock_WithSlot_SharesUnchangedChildren()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var replacement = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        // Replace slot 2 (second inner child)
        var modified = block.WithSlot(2, replacement);
        
        // child1 and child3 should be shared (at slots 1 and 3)
        Assert.Same(child1, modified.GetSlot(1));
        Assert.Same(replacement, modified.GetSlot(2));
        Assert.Same(child3, modified.GetSlot(3));
    }

    [Fact]
    public void GreenBlock_WithSlot_InvalidIndex_Throws()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var child = new GreenLeaf(NodeKind.Ident, "x");
        
        // Empty block has 2 slots: opener (0) and closer (1)
        // Slot 0 can only be replaced with a valid opener leaf
        // Slot 2+ is out of range
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithSlot(2, child));
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithSlot(-1, child));
    }

    [Fact]
    public void GreenBlock_WithInsert_InsertsAtIndex()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var inserted = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2));
        
        // Insert at slot 2 (between a and b)
        var modified = block.WithInsert(2, ImmutableArray.Create<GreenNode>(inserted));
        
        // 5 slots: opener + 3 inner + closer
        Assert.Equal(5, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(1));
        Assert.Same(inserted, modified.GetSlot(2));
        Assert.Same(child2, modified.GetSlot(3));
    }

    [Fact]
    public void GreenBlock_WithInsert_AtStart()
    {
        var child = new GreenLeaf(NodeKind.Ident, "a");
        var inserted = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        // Insert at slot 1 (after opener, before first inner child)
        var modified = block.WithInsert(1, ImmutableArray.Create<GreenNode>(inserted));
        
        Assert.Same(inserted, modified.GetSlot(1)); // inserted at slot 1
        Assert.Same(child, modified.GetSlot(2));    // original moved to slot 2
    }

    [Fact]
    public void GreenBlock_WithInsert_AtEnd()
    {
        var child = new GreenLeaf(NodeKind.Ident, "a");
        var inserted = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        // Insert at slot 2 (after last inner child, before closer)
        var modified = block.WithInsert(2, ImmutableArray.Create<GreenNode>(inserted));
        
        Assert.Same(child, modified.GetSlot(1));    // original at slot 1
        Assert.Same(inserted, modified.GetSlot(2)); // inserted at slot 2
    }

    [Fact]
    public void GreenBlock_WithInsert_MultipleNodes()
    {
        var original = new GreenLeaf(NodeKind.Ident, "a");
        var insert1 = new GreenLeaf(NodeKind.Ident, "X");
        var insert2 = new GreenLeaf(NodeKind.Ident, "Y");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(original));
        
        // Insert at slot 1 (start of inner content)
        var modified = block.WithInsert(1, ImmutableArray.Create<GreenNode>(insert1, insert2));
        
        // 5 slots: opener + 3 inner + closer
        Assert.Equal(5, modified.SlotCount);
        Assert.Same(insert1, modified.GetSlot(1));
        Assert.Same(insert2, modified.GetSlot(2));
        Assert.Same(original, modified.GetSlot(3));
    }

    [Fact]
    public void GreenBlock_WithInsert_InvalidIndex_Throws()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var child = new GreenLeaf(NodeKind.Ident, "x");
        
        // Cannot insert at slot 0 (opener) or past slot 1 (closer)
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithInsert(0, ImmutableArray.Create<GreenNode>(child)));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithInsert(-1, ImmutableArray.Create<GreenNode>(child)));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithInsert(10, ImmutableArray.Create<GreenNode>(child)));
        
        // Valid: insert at slot 1 (the only valid position for empty block)
        var modified = block.WithInsert(1, ImmutableArray.Create<GreenNode>(child));
        Assert.Equal(3, modified.SlotCount); // opener + 1 inner + closer
    }

    [Fact]
    public void GreenBlock_WithRemove_RemovesChildren()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        // Remove slot 2 (second inner child 'b')
        var modified = block.WithRemove(2, 1);
        
        // 4 slots remaining: opener + 2 inner + closer
        Assert.Equal(4, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(1)); // 'a' at slot 1
        Assert.Same(child3, modified.GetSlot(2)); // 'c' moved to slot 2
    }

    [Fact]
    public void GreenBlock_WithRemove_MultipleChildren()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var child4 = new GreenLeaf(NodeKind.Ident, "d");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3, child4));
        
        // Remove slots 2-3 (b and c, inner indices 1-2)
        var modified = block.WithRemove(2, 2);
        
        // 4 slots remaining: opener + 2 inner + closer
        Assert.Equal(4, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(1)); // 'a' at slot 1
        Assert.Same(child4, modified.GetSlot(2)); // 'd' moved to slot 2
    }

    [Fact]
    public void GreenBlock_WithRemove_InvalidRange_Throws()
    {
        var child = new GreenLeaf(NodeKind.Ident, "a");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child));
        
        // Cannot remove opener (slot 0)
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithRemove(0, 1));
        // Cannot remove with negative index
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithRemove(-1, 1));
        // Cannot remove past inner children (would affect closer)
        Assert.Throws<ArgumentOutOfRangeException>(() => block.WithRemove(1, 5));
    }

    [Fact]
    public void GreenBlock_WithReplace_ReplacesRange()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var replacement = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        // Replace slot 2 (second inner child 'b')
        var modified = block.WithReplace(2, 1, ImmutableArray.Create<GreenNode>(replacement));
        
        // 5 slots: opener + 3 inner + closer
        Assert.Equal(5, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(1));
        Assert.Same(replacement, modified.GetSlot(2));
        Assert.Same(child3, modified.GetSlot(3));
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
        
        // Replace slot 2 (second inner child 'b') with 3 nodes
        var modified = block.WithReplace(2, 1, ImmutableArray.Create<GreenNode>(repl1, repl2, repl3));
        
        // 6 slots: opener + 4 inner + closer
        Assert.Equal(6, modified.SlotCount);
        Assert.Same(child1, modified.GetSlot(1));
        Assert.Same(repl1, modified.GetSlot(2));
        Assert.Same(repl2, modified.GetSlot(3));
        Assert.Same(repl3, modified.GetSlot(4));
    }

    [Fact]
    public void GreenBlock_WithReplace_ContractsRange()
    {
        var child1 = new GreenLeaf(NodeKind.Ident, "a");
        var child2 = new GreenLeaf(NodeKind.Ident, "b");
        var child3 = new GreenLeaf(NodeKind.Ident, "c");
        var replacement = new GreenLeaf(NodeKind.Ident, "X");
        var block = GreenBlock.Create('{', ImmutableArray.Create<GreenNode>(child1, child2, child3));
        
        // Replace slots 1-2 (a and b) with 1 node
        var modified = block.WithReplace(1, 2, ImmutableArray.Create<GreenNode>(replacement));
        
        // 4 slots: opener + 2 inner + closer
        Assert.Equal(4, modified.SlotCount);
        Assert.Same(replacement, modified.GetSlot(1));
        Assert.Same(child3, modified.GetSlot(2));
    }

    [Fact]
    public void GreenBlock_WithReplace_InvalidRange_Throws()
    {
        var block = GreenBlock.Create('{', ImmutableArray<GreenNode>.Empty);
        var child = new GreenLeaf(NodeKind.Ident, "x");
        
        // Cannot replace with negative index
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithReplace(-1, 1, ImmutableArray.Create<GreenNode>(child)));
        // Cannot replace range that exceeds total slots
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            block.WithReplace(1, 5, ImmutableArray.Create<GreenNode>(child)));
        // Cannot partially replace starting at opener without replacing entire block
        Assert.Throws<ArgumentException>(() => 
            block.WithReplace(0, 1, ImmutableArray.Create<GreenNode>(child)));
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
        
        Assert.IsType<SyntaxBlock>(red);
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
        // Inner block is at slot 1 (slot 0 is opener)
        Assert.Same(inner, outer.GetSlot(1));
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
