using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for RedLeaf covering all properties and methods.
/// </summary>
public class RedLeafTests
{
    #region Basic Properties

    [Fact]
    public void Text_ReturnsGreenText()
    {
        var tree = SyntaxTree.Parse("hello");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal("hello", leaf.Text);
    }

    [Fact]
    public void TextSpan_ReturnsSpanOfText()
    {
        var tree = SyntaxTree.Parse("world");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        var span = leaf.TextSpan;
        Assert.Equal(5, span.Length);
        Assert.Equal("world", span.ToString());
    }

    [Fact]
    public void TextWidth_ReturnsTextLength()
    {
        var tree = SyntaxTree.Parse("test");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(4, leaf.TextWidth);
    }

    [Fact]
    public void Green_ReturnsUnderlyingGreenLeaf()
    {
        var tree = SyntaxTree.Parse("abc");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.NotNull(leaf.Green);
        Assert.IsType<GreenLeaf>(leaf.Green);
        Assert.Equal("abc", leaf.Green.Text);
    }

    #endregion

    #region Trivia Properties

    [Fact]
    public void LeadingTrivia_ReturnsGreenLeadingTrivia()
    {
        // Parse something that creates trivia
        var tree = SyntaxTree.Parse("a b");
        var children = tree.Root.Children.ToList();
        
        // Check any leaf has trivia accessible
        foreach (var child in children.OfType<RedLeaf>())
        {
            // LeadingTrivia should be accessible (may be empty)
            var trivia = child.LeadingTrivia;
            Assert.True(trivia.IsDefault == false || trivia.IsEmpty);
        }
    }

    [Fact]
    public void TrailingTrivia_ReturnsGreenTrailingTrivia()
    {
        var tree = SyntaxTree.Parse("x y");
        var children = tree.Root.Children.ToList();
        
        foreach (var child in children.OfType<RedLeaf>())
        {
            var trivia = child.TrailingTrivia;
            Assert.True(trivia.IsDefault == false || trivia.IsEmpty);
        }
    }

    [Fact]
    public void LeadingTriviaWidth_ReturnsCorrectWidth()
    {
        var tree = SyntaxTree.Parse("abc");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        // First token typically has no leading trivia
        Assert.True(leaf.LeadingTriviaWidth >= 0);
    }

    [Fact]
    public void TrailingTriviaWidth_ReturnsCorrectWidth()
    {
        var tree = SyntaxTree.Parse("abc");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.True(leaf.TrailingTriviaWidth >= 0);
    }

    #endregion

    #region Position Properties

    [Fact]
    public void Position_ReturnsAbsolutePosition()
    {
        var tree = SyntaxTree.Parse("first second");
        var children = tree.Root.Children.OfType<RedLeaf>().ToList();
        
        Assert.True(children.Count >= 2);
        Assert.Equal(0, children[0].Position);
        Assert.True(children[1].Position > children[0].Position);
    }

    [Fact]
    public void TextPosition_AccountsForLeadingTrivia()
    {
        var tree = SyntaxTree.Parse("abc");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        // TextPosition = Position + LeadingTriviaWidth
        Assert.Equal(leaf.Position + leaf.LeadingTriviaWidth, leaf.TextPosition);
    }

    [Fact]
    public void TextEndPosition_EqualsTextPositionPlusTextWidth()
    {
        var tree = SyntaxTree.Parse("hello");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(leaf.TextPosition + leaf.TextWidth, leaf.TextEndPosition);
    }

    [Fact]
    public void FullSpanStart_EqualsPosition()
    {
        var tree = SyntaxTree.Parse("test");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(leaf.Position, leaf.FullSpanStart);
    }

    [Fact]
    public void FullSpanEnd_EqualsEndPosition()
    {
        var tree = SyntaxTree.Parse("test");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(leaf.EndPosition, leaf.FullSpanEnd);
    }

    [Fact]
    public void PositionRelationships_AreConsistent()
    {
        var tree = SyntaxTree.Parse("token");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        
        // Full span starts at Position
        Assert.Equal(leaf.Position, leaf.FullSpanStart);
        
        // Text starts after leading trivia
        Assert.True(leaf.TextPosition >= leaf.FullSpanStart);
        
        // Text ends before trailing trivia
        Assert.True(leaf.TextEndPosition <= leaf.FullSpanEnd);
        
        // Full span ends at EndPosition
        Assert.Equal(leaf.EndPosition, leaf.FullSpanEnd);
    }

    #endregion

    #region GetChild

    [Fact]
    public void GetChild_AlwaysReturnsNull()
    {
        var tree = SyntaxTree.Parse("leaf");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Null(leaf.GetChild(0));
        Assert.Null(leaf.GetChild(1));
        Assert.Null(leaf.GetChild(-1));
        Assert.Null(leaf.GetChild(100));
    }

    [Fact]
    public void Children_IsEmpty()
    {
        var tree = SyntaxTree.Parse("leaf");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Empty(leaf.Children);
    }

    [Fact]
    public void SlotCount_IsZero()
    {
        var tree = SyntaxTree.Parse("leaf");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(0, leaf.SlotCount);
    }

    #endregion

    #region Inherited Properties

    [Fact]
    public void Kind_ReturnsCorrectKind()
    {
        var tree = SyntaxTree.Parse("identifier");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(NodeKind.Ident, leaf.Kind);
    }

    [Fact]
    public void Width_ReturnsFullWidth()
    {
        var tree = SyntaxTree.Parse("test");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.True(leaf.Width >= leaf.TextWidth);
    }

    [Fact]
    public void IsLeaf_ReturnsTrue()
    {
        var tree = SyntaxTree.Parse("leaf");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.True(leaf.IsLeaf);
    }

    [Fact]
    public void IsContainer_ReturnsFalse()
    {
        var tree = SyntaxTree.Parse("leaf");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.False(leaf.IsContainer);
    }

    [Fact]
    public void Parent_ReturnsParentNode()
    {
        var tree = SyntaxTree.Parse("{child}");
        var block = tree.Root.Children.First();
        var leaf = block.Children.First(c => c.Kind == NodeKind.Ident) as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Same(block, leaf.Parent);
    }

    [Fact]
    public void EndPosition_EqualsPositionPlusWidth()
    {
        var tree = SyntaxTree.Parse("test");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(leaf.Position + leaf.Width, leaf.EndPosition);
    }

    #endregion

    #region Different Token Types

    [Fact]
    public void NumericToken_HasCorrectProperties()
    {
        var tree = SyntaxTree.Parse("123");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(NodeKind.Numeric, leaf.Kind);
        Assert.Equal("123", leaf.Text);
    }

    [Fact]
    public void StringToken_HasCorrectProperties()
    {
        var tree = SyntaxTree.Parse("\"hello\"");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(NodeKind.String, leaf.Kind);
        Assert.Contains("hello", leaf.Text);
    }

    [Fact]
    public void OperatorToken_HasCorrectProperties()
    {
        var tree = SyntaxTree.Parse("a + b");
        var ops = tree.Root.Children.OfType<RedLeaf>().Where(l => l.Kind == NodeKind.Operator);
        
        Assert.NotEmpty(ops);
        var op = ops.First();
        Assert.Equal("+", op.Text);
    }

    [Fact]
    public void SymbolToken_HasCorrectProperties()
    {
        var tree = SyntaxTree.Parse("a.b");
        var symbols = tree.Root.Children.OfType<RedLeaf>().Where(l => l.Kind == NodeKind.Symbol);
        
        Assert.NotEmpty(symbols);
        var dot = symbols.First();
        Assert.Equal(".", dot.Text);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SingleCharacterToken()
    {
        var tree = SyntaxTree.Parse("x");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal("x", leaf.Text);
        Assert.Equal(1, leaf.TextWidth);
    }

    [Fact]
    public void LongToken()
    {
        var longText = new string('a', 1000);
        var tree = SyntaxTree.Parse(longText);
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        Assert.Equal(longText, leaf.Text);
        Assert.Equal(1000, leaf.TextWidth);
    }

    [Fact]
    public void MultipleTokensHaveCorrectPositions()
    {
        var tree = SyntaxTree.Parse("one two three");
        var leaves = tree.Root.Children.OfType<RedLeaf>().Where(l => l.Kind == NodeKind.Ident).ToList();
        
        Assert.True(leaves.Count >= 3);
        
        // Positions should be increasing
        for (int i = 1; i < leaves.Count; i++)
        {
            Assert.True(leaves[i].Position > leaves[i - 1].Position);
        }
    }

    [Fact]
    public void NestedLeafHasCorrectPosition()
    {
        var tree = SyntaxTree.Parse("{nested}");
        var block = tree.Root.Children.First();
        var leaf = block.Children.First(c => c.Kind == NodeKind.Ident) as RedLeaf;
        
        Assert.NotNull(leaf);
        // Leaf should be after the opening brace
        Assert.True(leaf.Position > block.Position);
        Assert.True(leaf.EndPosition < block.EndPosition);
    }

    [Fact]
    public void TextSpan_CanBeUsedForComparison()
    {
        var tree = SyntaxTree.Parse("test");
        var leaf = tree.Root.Children.First() as RedLeaf;
        
        Assert.NotNull(leaf);
        var span = leaf.TextSpan;
        
        Assert.True(span.SequenceEqual("test".AsSpan()));
    }

    #endregion
}
