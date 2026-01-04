using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for RedNode covering all properties, methods, and edge cases.
/// </summary>
[Trait("Category", "AST")]
[Trait("Category", "RedNode")]
public class RedNodeTests
{
    #region Basic Properties

    [Fact]
    public void Position_ReturnsCorrectOffset()
    {
        var tree = SyntaxTree.Parse("abc def");
        var children = tree.Root.Children.ToList();
        
        Assert.Equal(0, children[0].Position); // "abc" starts at 0
    }

    [Fact]
    public void Width_ReturnsNodeWidth()
    {
        var tree = SyntaxTree.Parse("hello");
        var ident = tree.Root.Children.First();
        
        Assert.Equal(5, ident.Width);
    }

    [Fact]
    public void EndPosition_ReturnsPositionPlusWidth()
    {
        var tree = SyntaxTree.Parse("hello");
        var ident = tree.Root.Children.First();
        
        Assert.Equal(0, ident.Position);
        Assert.Equal(5, ident.Width);
        Assert.Equal(5, ident.EndPosition);
    }

    [Fact]
    public void Kind_ReturnsCorrectNodeKind()
    {
        var tree = SyntaxTree.Parse("{a}");
        var block = tree.Root.Children.First();
        
        Assert.Equal(NodeKind.BraceBlock, block.Kind);
    }

    [Fact]
    public void SlotCount_ReturnsChildCount()
    {
        var tree = SyntaxTree.Parse("{a}");
        var block = tree.Root.Children.First();
        
        Assert.True(block.SlotCount > 0);
    }

    [Fact]
    public void IsContainer_TrueForBlocks()
    {
        var tree = SyntaxTree.Parse("{a}");
        var block = tree.Root.Children.First();
        
        Assert.True(block.IsContainer);
    }

    [Fact]
    public void IsLeaf_TrueForIdents()
    {
        var tree = SyntaxTree.Parse("abc");
        var ident = tree.Root.Children.First();
        
        Assert.True(ident.IsLeaf);
    }

    [Fact]
    public void IsLeaf_FalseForBlocks()
    {
        var tree = SyntaxTree.Parse("{a}");
        var block = tree.Root.Children.First();
        
        Assert.False(block.IsLeaf);
    }

    [Fact]
    public void Green_ReturnsUnderlyingGreenNode()
    {
        var tree = SyntaxTree.Parse("abc");
        var ident = tree.Root.Children.First();
        
        Assert.NotNull(ident.Green);
        Assert.Equal(ident.Kind, ident.Green.Kind);
        Assert.Equal(ident.Width, ident.Green.Width);
    }

    [Fact]
    public void Parent_ReturnsParentNode()
    {
        var tree = SyntaxTree.Parse("{child}");
        var block = tree.Root.Children.First();
        var child = block.Children.First(c => c.Kind == NodeKind.Ident);
        
        Assert.NotNull(child.Parent);
        Assert.Same(block, child.Parent);
    }

    [Fact]
    public void Parent_NullForRoot()
    {
        var tree = SyntaxTree.Parse("abc");
        
        Assert.Null(tree.Root.Parent);
    }

    #endregion

    #region Children Enumeration

    [Fact]
    public void Children_EnumeratesAllChildren()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var block = tree.Root.Children.First();
        var children = block.Children.ToList();
        
        Assert.True(children.Count >= 3); // At least a, b, c
    }

    [Fact]
    public void Children_EmptyForLeaf()
    {
        var tree = SyntaxTree.Parse("abc");
        var ident = tree.Root.Children.First();
        
        Assert.Empty(ident.Children);
    }

    [Fact]
    public void Children_CanBeEnumeratedMultipleTimes()
    {
        var tree = SyntaxTree.Parse("{a b}");
        var block = tree.Root.Children.First();
        
        var first = block.Children.ToList();
        var second = block.Children.ToList();
        
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public void GetChild_ReturnsSameInstanceOnRepeatedCalls()
    {
        var tree = SyntaxTree.Parse("{child}");
        var block = tree.Root.Children.First();
        
        var child1 = block.GetChild(0);
        var child2 = block.GetChild(0);
        
        Assert.Same(child1, child2);
    }

    [Fact]
    public void GetChild_OutOfRange_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("{a}");
        var block = tree.Root.Children.First();
        
        var outOfRange = block.GetChild(999);
        
        Assert.Null(outOfRange);
    }

    #endregion

    #region Root Property

    [Fact]
    public void Root_ReturnsRootFromDeepNode()
    {
        var tree = SyntaxTree.Parse("{{{{deep}}}}");
        
        // Navigate to deepest ident
        RedNode current = tree.Root;
        while (current.Children.Any())
        {
            var firstChild = current.Children.First();
            if (firstChild.IsLeaf)
            {
                current = firstChild;
                break;
            }
            current = firstChild;
        }
        
        Assert.Same(tree.Root, current.Root);
    }

    [Fact]
    public void Root_ReturnsSelfForRootNode()
    {
        var tree = SyntaxTree.Parse("abc");
        
        Assert.Same(tree.Root, tree.Root.Root);
    }

    #endregion

    #region FindNodeAt

    [Fact]
    public void FindNodeAt_ReturnsDeepestContainingNode()
    {
        var tree = SyntaxTree.Parse("{abc}");
        
        // Position 1 should be inside "abc" which is inside the block
        var found = tree.Root.FindNodeAt(1);
        
        Assert.NotNull(found);
        Assert.Equal(NodeKind.Ident, found.Kind);
    }

    [Fact]
    public void FindNodeAt_AtBlockDelimiter_ReturnsBlock()
    {
        var tree = SyntaxTree.Parse("{abc}");
        
        // Position 0 is the opening brace
        var found = tree.Root.FindNodeAt(0);
        
        Assert.NotNull(found);
    }

    [Fact]
    public void FindNodeAt_BeforeStart_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var found = tree.Root.FindNodeAt(-1);
        
        Assert.Null(found);
    }

    [Fact]
    public void FindNodeAt_AtEnd_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        // EndPosition is exclusive, so position 3 is out of range
        var found = tree.Root.FindNodeAt(3);
        
        Assert.Null(found);
    }

    [Fact]
    public void FindNodeAt_PastEnd_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var found = tree.Root.FindNodeAt(100);
        
        Assert.Null(found);
    }

    [Fact]
    public void FindNodeAt_InNestedBlock_ReturnsDeepest()
    {
        var tree = SyntaxTree.Parse("{[inner]}");
        
        // Position 2 is inside "inner"
        var found = tree.Root.FindNodeAt(2);
        
        Assert.NotNull(found);
    }

    [Fact]
    public void FindNodeAt_OnRootDirectly_ReturnsNode()
    {
        var tree = SyntaxTree.Parse("a b c");
        
        var found = tree.Root.FindNodeAt(0);
        
        Assert.NotNull(found);
    }

    #endregion

    #region FindLeafAt

    [Fact]
    public void FindLeafAt_ReturnsLeafNode()
    {
        var tree = SyntaxTree.Parse("{abc}");
        
        var leaf = tree.Root.FindLeafAt(2);
        
        Assert.NotNull(leaf);
        Assert.True(leaf.IsLeaf);
    }

    [Fact]
    public void FindLeafAt_InNestedStructure_ReturnsDeepestLeaf()
    {
        var tree = SyntaxTree.Parse("{[nested]}");
        
        var leaf = tree.Root.FindLeafAt(2);
        
        Assert.NotNull(leaf);
        Assert.True(leaf.IsLeaf);
        Assert.Equal(NodeKind.Ident, leaf.Kind);
    }

    [Fact]
    public void FindLeafAt_OutOfRange_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var leaf = tree.Root.FindLeafAt(100);
        
        Assert.Null(leaf);
    }

    [Fact]
    public void FindLeafAt_AtBlockDelimiter_ReturnsBlockOrLeaf()
    {
        var tree = SyntaxTree.Parse("{a}");
        
        // Position 0 is the block delimiter - FindLeafAt should handle this
        var result = tree.Root.FindLeafAt(0);
        
        // The result depends on implementation - should not throw
        Assert.NotNull(result);
    }

    [Fact]
    public void FindLeafAt_EmptyBlock_ReturnsBlockOrNull()
    {
        var tree = SyntaxTree.Parse("{}");
        
        // Position 1 is inside empty block
        var result = tree.Root.FindLeafAt(1);
        
        // Should handle gracefully (may return block or null depending on impl)
    }

    #endregion

    #region Sibling Navigation

    [Fact]
    public void SiblingIndex_ReturnsCorrectIndex()
    {
        var tree = SyntaxTree.Parse("a b c");
        var children = tree.Root.Children.ToList();
        
        for (int i = 0; i < children.Count; i++)
        {
            Assert.Equal(i, children[i].SiblingIndex);
        }
    }

    [Fact]
    public void SiblingIndex_RootReturnsMinusOne()
    {
        var tree = SyntaxTree.Parse("abc");
        
        Assert.Equal(-1, tree.Root.SiblingIndex);
    }

    [Fact]
    public void NextSibling_ReturnsNextChild()
    {
        var tree = SyntaxTree.Parse("a b c");
        var first = tree.Root.Children.First();
        
        var next = first.NextSibling();
        
        Assert.NotNull(next);
        Assert.Equal(1, next.SiblingIndex);
    }

    [Fact]
    public void NextSibling_LastChild_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a b c");
        var last = tree.Root.Children.Last();
        
        var next = last.NextSibling();
        
        Assert.Null(next);
    }

    [Fact]
    public void NextSibling_Root_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var next = tree.Root.NextSibling();
        
        Assert.Null(next);
    }

    [Fact]
    public void PreviousSibling_ReturnsPreviousChild()
    {
        var tree = SyntaxTree.Parse("a b c");
        var children = tree.Root.Children.ToList();
        var second = children[1];
        
        var prev = second.PreviousSibling();
        
        Assert.NotNull(prev);
        Assert.Equal(0, prev.SiblingIndex);
    }

    [Fact]
    public void PreviousSibling_FirstChild_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a b c");
        var first = tree.Root.Children.First();
        
        var prev = first.PreviousSibling();
        
        Assert.Null(prev);
    }

    [Fact]
    public void PreviousSibling_Root_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        
        var prev = tree.Root.PreviousSibling();
        
        Assert.Null(prev);
    }

    [Fact]
    public void NextSibling_ChainTraversal()
    {
        var tree = SyntaxTree.Parse("a b c d e");
        var current = tree.Root.Children.First();
        var count = 1;
        
        while (current.NextSibling() is { } next)
        {
            current = next;
            count++;
        }
        
        Assert.True(count >= 5); // At least a, b, c, d, e
    }

    [Fact]
    public void PreviousSibling_ChainTraversal()
    {
        var tree = SyntaxTree.Parse("a b c d e");
        var current = tree.Root.Children.Last();
        var count = 1;
        
        while (current.PreviousSibling() is { } prev)
        {
            current = prev;
            count++;
        }
        
        Assert.True(count >= 5); // At least e, d, c, b, a
    }

    #endregion

    #region Position Calculations

    [Fact]
    public void Position_ChildPositionsAreCorrect()
    {
        var tree = SyntaxTree.Parse("abc def ghi");
        var children = tree.Root.Children.ToList();
        
        // First child starts at 0
        Assert.Equal(0, children[0].Position);
        
        // Each child's position should be >= previous child's end
        for (int i = 1; i < children.Count; i++)
        {
            Assert.True(children[i].Position >= children[i - 1].EndPosition);
        }
    }

    [Fact]
    public void Position_NestedBlocksHaveCorrectPositions()
    {
        var tree = SyntaxTree.Parse("{inner}");
        var block = tree.Root.Children.First();
        var inner = block.Children.First(c => c.Kind == NodeKind.Ident);
        
        // Inner ident should be after the opening brace
        Assert.True(inner.Position > block.Position);
        Assert.True(inner.EndPosition < block.EndPosition);
    }

    [Fact]
    public void EndPosition_EqualsPositionPlusWidth()
    {
        var tree = SyntaxTree.Parse("test");
        var ident = tree.Root.Children.First();
        
        Assert.Equal(ident.Position + ident.Width, ident.EndPosition);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptySource_RootHasNoChildren()
    {
        var tree = SyntaxTree.Parse("");
        
        Assert.Empty(tree.Root.Children);
    }

    [Fact]
    public void SingleCharacter_ParsesCorrectly()
    {
        var tree = SyntaxTree.Parse("x");
        var children = tree.Root.Children.ToList();
        
        Assert.Single(children);
        Assert.Equal(1, children[0].Width);
    }

    [Fact]
    public void DeeplyNested_AllPositionsValid()
    {
        var tree = SyntaxTree.Parse("{{{{{a}}}}}");
        
        void ValidatePositions(RedNode node)
        {
            Assert.True(node.Position >= 0);
            Assert.True(node.Width >= 0);
            Assert.Equal(node.Position + node.Width, node.EndPosition);
            
            foreach (var child in node.Children)
            {
                Assert.True(child.Position >= node.Position);
                Assert.True(child.EndPosition <= node.EndPosition);
                ValidatePositions(child);
            }
        }
        
        ValidatePositions(tree.Root);
    }

    [Fact]
    public void MultipleBlocks_IndependentPositions()
    {
        var tree = SyntaxTree.Parse("{a}{b}{c}");
        var blocks = tree.Root.Children.Where(c => c.IsContainer).ToList();
        
        Assert.True(blocks.Count >= 3);
        
        for (int i = 1; i < blocks.Count; i++)
        {
            Assert.True(blocks[i].Position >= blocks[i - 1].EndPosition);
        }
    }

    #endregion

    #region RedLeaf Specific

    [Fact]
    public void RedLeaf_Text_ReturnsContent()
    {
        var tree = SyntaxTree.Parse("hello");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal("hello", leaf.Text);
    }

    [Fact]
    public void RedLeaf_HasNoChildren()
    {
        var tree = SyntaxTree.Parse("abc");
        var leaf = tree.Root.Children.First();
        
        Assert.True(leaf.IsLeaf);
        Assert.Equal(0, leaf.SlotCount);
    }

    #endregion

    #region RedBlock Specific

    [Fact]
    public void RedBlock_Opener_ReturnsCorrectCharacter()
    {
        var tree = SyntaxTree.Parse("{test}");
        var block = tree.Root.Children.First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal('{', block.Opener);
    }

    [Fact]
    public void RedBlock_Closer_ReturnsCorrectCharacter()
    {
        var tree = SyntaxTree.Parse("[test]");
        var block = tree.Root.Children.First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal(']', block.Closer);
    }

    [Fact]
    public void RedBlock_ParenBlock_HasCorrectDelimiters()
    {
        var tree = SyntaxTree.Parse("(test)");
        var block = tree.Root.Children.First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.Equal('(', block.Opener);
        Assert.Equal(')', block.Closer);
    }

    [Fact]
    public void RedBlock_ChildCount_ReturnsCorrectCount()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var block = tree.Root.Children.First() as RedBlock;
        
        Assert.NotNull(block);
        Assert.True(block.ChildCount >= 3);
    }

    #endregion

    #region FindLeafAt Edge Cases

    [Fact]
    public void FindLeafAt_AtContainerBoundary_HandlesCorrectly()
    {
        // Test the branch where deeperChild == null in FindLeafAt
        var tree = SyntaxTree.Parse("{}");
        
        // Position 0 is at the '{' - block has no ident children
        var result = tree.Root.FindLeafAt(0);
        
        // Should return the block itself since there's no leaf at that position
        Assert.NotNull(result);
    }

    [Fact]
    public void FindLeafAt_BetweenChildren_ReturnsClosest()
    {
        var tree = SyntaxTree.Parse("a b");
        
        // Position 1 is the space between 'a' and 'b' - depends on tokenization
        var result = tree.Root.FindLeafAt(1);
        
        Assert.NotNull(result);
    }

    [Fact]
    public void FindLeafAt_InBlockWithWhitespaceOnly()
    {
        var tree = SyntaxTree.Parse("{   }");
        
        // Position in the whitespace area
        var result = tree.Root.FindLeafAt(2);
        
        // Should handle gracefully
    }

    [Fact]
    public void FindLeafAt_OnBlockDelimiter_ReturnsAppropriateNode()
    {
        var tree = SyntaxTree.Parse("{abc}");
        
        // Test at each position
        for (int i = 0; i < 5; i++)
        {
            var result = tree.Root.FindLeafAt(i);
            Assert.NotNull(result);
        }
    }

    #endregion
}
