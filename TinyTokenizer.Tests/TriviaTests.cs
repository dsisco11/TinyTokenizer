using System.Collections.Immutable;
using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests verifying Roslyn-compatible trivia ownership rules.
/// 
/// Roslyn trivia model:
/// - Trailing trivia = same-line content after token up to and including the first newline
/// - Leading trivia = content from after previous token's trailing trivia up to current token
/// - First token gets all initial trivia as leading
/// - Last token gets all remaining trivia (to EOF) as trailing
/// - Multi-line comments starting on same line are entirely trailing trivia
/// </summary>
public class TriviaTests
{
    #region Helper Methods

    /// <summary>
    /// Parses source and returns all GreenLeaf nodes in document order.
    /// </summary>
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
        }
        else
        {
            for (int i = 0; i < node.SlotCount; i++)
            {
                var child = node.GetSlot(i);
                if (child != null)
                {
                    CollectLeavesRecursive(child, builder);
                }
            }
        }
    }

    /// <summary>
    /// Gets concatenated text of all trivia pieces.
    /// </summary>
    private static string GetTriviaText(ImmutableArray<GreenTrivia> trivia)
    {
        return string.Concat(trivia.Select(t => t.Text));
    }

    /// <summary>
    /// Finds a leaf by its text content.
    /// </summary>
    private static GreenLeaf? FindLeaf(ImmutableArray<GreenLeaf> leaves, string text)
    {
        return leaves.FirstOrDefault(l => l.Text == text);
    }

    #endregion

    #region Newline Ownership Tests

    /// <summary>
    /// Roslyn rule: Newline after a token is trailing trivia of that token.
    /// The next token starts with empty or subsequent leading trivia.
    /// </summary>
    [Fact]
    public void Newline_IsTrailingTriviaOfPrecedingToken()
    {
        // "a\nb" - newline should be trailing trivia of 'a'
        var leaves = ParseLeaves("a\nb");

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' should have "\n" as trailing trivia
        Assert.Equal("\n", GetTriviaText(tokenA!.TrailingTrivia));
        Assert.Single(tokenA.TrailingTrivia);
        Assert.Equal(TriviaKind.Newline, tokenA.TrailingTrivia[0].Kind);

        // 'b' should have empty leading trivia (newline was consumed by 'a')
        Assert.Empty(tokenB!.LeadingTrivia);
    }

    /// <summary>
    /// Roslyn rule: First newline goes to trailing, subsequent newlines go to next token's leading.
    /// </summary>
    [Fact]
    public void MultipleNewlines_FirstIsTrailingRestAreLeading()
    {
        // "a\n\nb" - first \n is trailing of 'a', second \n is leading of 'b'
        var leaves = ParseLeaves("a\n\nb");

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' has exactly one newline as trailing
        Assert.Equal("\n", GetTriviaText(tokenA!.TrailingTrivia));
        Assert.Single(tokenA.TrailingTrivia);

        // 'b' has exactly one newline as leading
        Assert.Equal("\n", GetTriviaText(tokenB!.LeadingTrivia));
        Assert.Single(tokenB.LeadingTrivia);
    }

    /// <summary>
    /// Roslyn rule: CRLF (\r\n) is treated as a single trivia unit.
    /// </summary>
    [Fact]
    public void CrLf_TreatedAsSingleTriviaUnit()
    {
        // "a\r\nb" - CRLF should be single trailing trivia piece with width 2
        var leaves = ParseLeaves("a\r\nb");

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' has CRLF as single trailing trivia piece
        Assert.Single(tokenA!.TrailingTrivia);
        Assert.Equal("\r\n", tokenA.TrailingTrivia[0].Text);
        Assert.Equal(2, tokenA.TrailingTrivia[0].Width);
        Assert.Equal(TriviaKind.Newline, tokenA.TrailingTrivia[0].Kind);

        // 'b' has empty leading trivia
        Assert.Empty(tokenB!.LeadingTrivia);
    }

    #endregion

    #region Comment Ownership Tests

    /// <summary>
    /// Roslyn rule: Same-line comment (after a token) is trailing trivia, including the newline.
    /// </summary>
    [Fact]
    public void SameLineComment_IsTrailingTriviaIncludingNewline()
    {
        // "a // comment\nb" - space, comment, and newline are all trailing of 'a'
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("a // comment\nb", options);

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' should have " // comment\n" as trailing trivia
        var trailingText = GetTriviaText(tokenA!.TrailingTrivia);
        Assert.Equal(" // comment\n", trailingText);

        // Verify trivia structure: whitespace + comment + newline
        Assert.Equal(3, tokenA.TrailingTrivia.Length);
        Assert.Equal(TriviaKind.Whitespace, tokenA.TrailingTrivia[0].Kind);
        Assert.Equal(" ", tokenA.TrailingTrivia[0].Text);
        Assert.Equal(TriviaKind.SingleLineComment, tokenA.TrailingTrivia[1].Kind);
        Assert.Equal("// comment", tokenA.TrailingTrivia[1].Text);
        Assert.Equal(TriviaKind.Newline, tokenA.TrailingTrivia[2].Kind);
        Assert.Equal("\n", tokenA.TrailingTrivia[2].Text);

        // 'b' should have empty leading trivia
        Assert.Empty(tokenB!.LeadingTrivia);
    }

    /// <summary>
    /// Roslyn rule: Comment on its own line is leading trivia of the next token.
    /// </summary>
    [Fact]
    public void OwnLineComment_IsLeadingTriviaOfNextToken()
    {
        // "a\n// comment\nb" - newline is trailing of 'a', comment+newline is leading of 'b'
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("a\n// comment\nb", options);

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' has just the newline as trailing
        Assert.Equal("\n", GetTriviaText(tokenA!.TrailingTrivia));
        Assert.Single(tokenA.TrailingTrivia);

        // 'b' has the comment and its newline as leading
        var leadingText = GetTriviaText(tokenB!.LeadingTrivia);
        Assert.Equal("// comment\n", leadingText);

        // Verify structure: comment + newline
        Assert.Equal(2, tokenB.LeadingTrivia.Length);
        Assert.Equal(TriviaKind.SingleLineComment, tokenB.LeadingTrivia[0].Kind);
        Assert.Equal("// comment", tokenB.LeadingTrivia[0].Text);
        Assert.Equal(TriviaKind.Newline, tokenB.LeadingTrivia[1].Kind);
    }

    /// <summary>
    /// Roslyn rule: Multi-line comment starting on same line is entirely trailing trivia,
    /// even if the comment contains internal newlines.
    /// </summary>
    [Fact]
    public void MultiLineComment_AttachedToTokenItStartsAfter()
    {
        // "a /* multi\nline */ b" - entire comment is trailing of 'a'
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var leaves = ParseLeaves("a /* multi\nline */ b", options);

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' should have " /* multi\nline */ " as trailing trivia
        var trailingText = GetTriviaText(tokenA!.TrailingTrivia);
        Assert.Equal(" /* multi\nline */ ", trailingText);

        // Verify structure: whitespace + multi-line comment + whitespace
        Assert.Equal(3, tokenA.TrailingTrivia.Length);
        Assert.Equal(TriviaKind.Whitespace, tokenA.TrailingTrivia[0].Kind);
        Assert.Equal(" ", tokenA.TrailingTrivia[0].Text);
        Assert.Equal(TriviaKind.MultiLineComment, tokenA.TrailingTrivia[1].Kind);
        Assert.Equal("/* multi\nline */", tokenA.TrailingTrivia[1].Text);
        Assert.Equal(TriviaKind.Whitespace, tokenA.TrailingTrivia[2].Kind);
        Assert.Equal(" ", tokenA.TrailingTrivia[2].Text);

        // 'b' should have empty leading trivia (comment was consumed by 'a')
        Assert.Empty(tokenB!.LeadingTrivia);
    }

    /// <summary>
    /// Roslyn rule: Multi-line comment on its own line is leading trivia of next token.
    /// </summary>
    [Fact]
    public void MultiLineCommentOnOwnLine_IsLeadingTriviaOfNextToken()
    {
        // "a\n/* comment */\nb" - newline trailing of 'a', comment+newline leading of 'b'
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var leaves = ParseLeaves("a\n/* comment */\nb", options);

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' has just newline as trailing
        Assert.Equal("\n", GetTriviaText(tokenA!.TrailingTrivia));

        // 'b' has comment + newline as leading
        var leadingText = GetTriviaText(tokenB!.LeadingTrivia);
        Assert.Equal("/* comment */\n", leadingText);

        Assert.Equal(2, tokenB.LeadingTrivia.Length);
        Assert.Equal(TriviaKind.MultiLineComment, tokenB.LeadingTrivia[0].Kind);
        Assert.Equal(TriviaKind.Newline, tokenB.LeadingTrivia[1].Kind);
    }

    #endregion

    #region First/Last Token Edge Cases

    /// <summary>
    /// Roslyn rule: First token in source gets ALL leading whitespace/newlines/comments.
    /// </summary>
    [Fact]
    public void FirstToken_GetsAllLeadingTrivia()
    {
        // "  \n  x" - all initial whitespace/newline is leading trivia of 'x'
        var leaves = ParseLeaves("  \n  x");

        Assert.Single(leaves);
        var tokenX = leaves[0];

        Assert.Equal("x", tokenX.Text);
        Assert.Equal("  \n  ", GetTriviaText(tokenX.LeadingTrivia));

        // Verify structure: whitespace + newline + whitespace
        Assert.Equal(3, tokenX.LeadingTrivia.Length);
        Assert.Equal(TriviaKind.Whitespace, tokenX.LeadingTrivia[0].Kind);
        Assert.Equal("  ", tokenX.LeadingTrivia[0].Text);
        Assert.Equal(TriviaKind.Newline, tokenX.LeadingTrivia[1].Kind);
        Assert.Equal("\n", tokenX.LeadingTrivia[1].Text);
        Assert.Equal(TriviaKind.Whitespace, tokenX.LeadingTrivia[2].Kind);
        Assert.Equal("  ", tokenX.LeadingTrivia[2].Text);
    }

    /// <summary>
    /// Roslyn rule: Last token gets all trailing trivia to EOF.
    /// </summary>
    [Fact]
    public void LastToken_GetsAllTrailingTrivia()
    {
        // "x  \n" - all trailing content is trailing trivia of 'x'
        var leaves = ParseLeaves("x  \n");

        Assert.Single(leaves);
        var tokenX = leaves[0];

        Assert.Equal("x", tokenX.Text);
        Assert.Equal("  \n", GetTriviaText(tokenX.TrailingTrivia));

        // Verify structure: whitespace + newline
        Assert.Equal(2, tokenX.TrailingTrivia.Length);
        Assert.Equal(TriviaKind.Whitespace, tokenX.TrailingTrivia[0].Kind);
        Assert.Equal("  ", tokenX.TrailingTrivia[0].Text);
        Assert.Equal(TriviaKind.Newline, tokenX.TrailingTrivia[1].Kind);
        Assert.Equal("\n", tokenX.TrailingTrivia[1].Text);
    }

    /// <summary>
    /// Roslyn rule: First token with leading comment gets all leading trivia.
    /// </summary>
    [Fact]
    public void FirstToken_WithLeadingComment_GetsAllLeadingTrivia()
    {
        // "// header comment\nx" - comment + newline is leading trivia of 'x'
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("// header comment\nx", options);

        Assert.Single(leaves);
        var tokenX = leaves[0];

        Assert.Equal("x", tokenX.Text);
        Assert.Equal("// header comment\n", GetTriviaText(tokenX.LeadingTrivia));

        Assert.Equal(2, tokenX.LeadingTrivia.Length);
        Assert.Equal(TriviaKind.SingleLineComment, tokenX.LeadingTrivia[0].Kind);
        Assert.Equal(TriviaKind.Newline, tokenX.LeadingTrivia[1].Kind);
    }

    /// <summary>
    /// Roslyn rule: Last token with trailing comment gets all trailing trivia to EOF.
    /// </summary>
    [Fact]
    public void LastToken_WithTrailingComment_GetsAllTrailingTrivia()
    {
        // "x // trailing comment" - space + comment is trailing of 'x' (no newline at EOF)
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("x // trailing comment", options);

        Assert.Single(leaves);
        var tokenX = leaves[0];

        Assert.Equal("x", tokenX.Text);
        Assert.Equal(" // trailing comment", GetTriviaText(tokenX.TrailingTrivia));

        Assert.Equal(2, tokenX.TrailingTrivia.Length);
        Assert.Equal(TriviaKind.Whitespace, tokenX.TrailingTrivia[0].Kind);
        Assert.Equal(TriviaKind.SingleLineComment, tokenX.TrailingTrivia[1].Kind);
    }

    #endregion

    #region Whitespace Between Tokens

    /// <summary>
    /// Roslyn rule: Whitespace between tokens on the same line is trailing trivia of the first token.
    /// </summary>
    [Fact]
    public void WhitespaceBetweenTokens_IsTrailingOfFirst()
    {
        // "a   b" - spaces are trailing trivia of 'a'
        var leaves = ParseLeaves("a   b");

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' has "   " as trailing trivia
        Assert.Equal("   ", GetTriviaText(tokenA!.TrailingTrivia));
        Assert.Single(tokenA.TrailingTrivia);
        Assert.Equal(TriviaKind.Whitespace, tokenA.TrailingTrivia[0].Kind);

        // 'b' has empty leading trivia
        Assert.Empty(tokenB!.LeadingTrivia);
    }

    /// <summary>
    /// Verifies whitespace + newline + whitespace is split correctly between tokens.
    /// </summary>
    [Fact]
    public void WhitespaceNewlineWhitespace_SplitsCorrectly()
    {
        // "a  \n  b" - "  \n" is trailing of 'a', "  " is leading of 'b'
        var leaves = ParseLeaves("a  \n  b");

        var tokenA = FindLeaf(leaves, "a");
        var tokenB = FindLeaf(leaves, "b");

        Assert.NotNull(tokenA);
        Assert.NotNull(tokenB);

        // 'a' has "  \n" as trailing (whitespace + newline)
        Assert.Equal("  \n", GetTriviaText(tokenA!.TrailingTrivia));
        Assert.Equal(2, tokenA.TrailingTrivia.Length);
        Assert.Equal(TriviaKind.Whitespace, tokenA.TrailingTrivia[0].Kind);
        Assert.Equal(TriviaKind.Newline, tokenA.TrailingTrivia[1].Kind);

        // 'b' has "  " as leading (indentation)
        Assert.Equal("  ", GetTriviaText(tokenB!.LeadingTrivia));
        Assert.Single(tokenB.LeadingTrivia);
        Assert.Equal(TriviaKind.Whitespace, tokenB.LeadingTrivia[0].Kind);
    }

    #endregion

    #region Empty Source Edge Cases

    [Fact]
    public void EmptySource_ReturnsNoLeaves()
    {
        var leaves = ParseLeaves("");
        Assert.Empty(leaves);
    }

    [Fact]
    public void OnlyWhitespace_ReturnsNodes()
    {
        // Whitespace-only source is converted to nodes to preserve content
        // (important for SyntaxEditor insertions of trivia-only text)
        var leaves = ParseLeaves("   \n\n   ");
        Assert.NotEmpty(leaves);
        // The whitespace and newlines are converted to symbol nodes
        var fullText = string.Concat(leaves.Select(l => l.ToText()));
        Assert.Equal("   \n\n   ", fullText);
    }

    [Fact]
    public void OnlyComment_ReturnsNodes()
    {
        // Comment-only source is converted to nodes to preserve content
        // (important for SyntaxEditor insertions of trivia-only text)
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var leaves = ParseLeaves("// just a comment", options);
        Assert.NotEmpty(leaves);
        Assert.Equal("// just a comment", leaves[0].ToText());
    }

    #endregion
    
    #region Public Trivia API Tests
    
    [Fact]
    public void Trivia_Kind_ReturnsCorrectKind()
    {
        var tree = SyntaxTree.Parse("  x"); // Leading whitespace
        var leaf = tree.Leaves.First();
        
        var trivia = leaf.GetLeadingTrivia().First();
        
        Assert.Equal(TriviaKind.Whitespace, trivia.Kind);
    }
    
    [Fact]
    public void Trivia_Text_ReturnsCorrectText()
    {
        var tree = SyntaxTree.Parse("  x");
        var leaf = tree.Leaves.First();
        
        var trivia = leaf.GetLeadingTrivia().First();
        
        Assert.Equal("  ", trivia.Text);
    }
    
    [Fact]
    public void Trivia_Width_ReturnsCorrectLength()
    {
        var tree = SyntaxTree.Parse("    x"); // 4 spaces
        var leaf = tree.Leaves.First();
        
        var trivia = leaf.GetLeadingTrivia().First();
        
        Assert.Equal(4, trivia.Width);
    }
    
    [Fact]
    public void Trivia_IsWhitespace_ReturnsTrueForWhitespace()
    {
        var tree = SyntaxTree.Parse("  x");
        var leaf = tree.Leaves.First();
        
        var trivia = leaf.GetLeadingTrivia().First();
        
        Assert.True(trivia.IsWhitespace);
        Assert.False(trivia.IsNewline);
        Assert.False(trivia.IsComment);
    }
    
    [Fact]
    public void Trivia_IsNewline_ReturnsTrueForNewline()
    {
        var tree = SyntaxTree.Parse("x\ny");
        var xLeaf = tree.Leaves.First();
        
        var trivia = xLeaf.GetTrailingTrivia().First();
        
        Assert.True(trivia.IsNewline);
        Assert.False(trivia.IsWhitespace);
        Assert.False(trivia.IsComment);
    }
    
    [Fact]
    public void Trivia_IsComment_ReturnsTrueForComments()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tree = SyntaxTree.Parse("x // comment\ny", options);
        var xLeaf = tree.Leaves.First();
        
        var trivia = xLeaf.GetTrailingTrivia().First(t => t.IsComment);
        
        Assert.True(trivia.IsComment);
        Assert.False(trivia.IsWhitespace);
        Assert.False(trivia.IsNewline);
        Assert.Equal(TriviaKind.SingleLineComment, trivia.Kind);
    }
    
    [Fact]
    public void Trivia_ToString_ReturnsText()
    {
        var tree = SyntaxTree.Parse("  x");
        var leaf = tree.Leaves.First();
        
        var trivia = leaf.GetLeadingTrivia().First();
        
        Assert.Equal("  ", trivia.ToString());
    }
    
    [Fact]
    public void Trivia_Equality_WorksCorrectly()
    {
        // Two leaves with identical leading whitespace
        var tree = SyntaxTree.Parse("  x\n  y");
        var leaves = tree.Leaves.ToList();
        
        // Both x and y start with 2 spaces of leading trivia
        var trivia1 = leaves[0].GetLeadingTrivia().First();
        var trivia2 = leaves[1].GetLeadingTrivia().First();
        
        // Same content ("  "), same kind (Whitespace)
        Assert.Equal(TriviaKind.Whitespace, trivia1.Kind);
        Assert.Equal(TriviaKind.Whitespace, trivia2.Kind);
        Assert.Equal(trivia1.Text, trivia2.Text);
        Assert.Equal(trivia1, trivia2);
        Assert.True(trivia1 == trivia2);
        Assert.False(trivia1 != trivia2);
    }
    
    [Fact]
    public void Trivia_Inequality_WorksCorrectly()
    {
        var tree = SyntaxTree.Parse("  x\ny");
        var leaves = tree.Leaves.ToList();
        
        var whitespaceTrivia = leaves[0].GetLeadingTrivia().First();
        var newlineTrivia = leaves[0].GetTrailingTrivia().First();
        
        Assert.NotEqual(whitespaceTrivia, newlineTrivia);
        Assert.False(whitespaceTrivia == newlineTrivia);
        Assert.True(whitespaceTrivia != newlineTrivia);
    }
    
    [Fact]
    public void RedLeaf_GetLeadingTrivia_ReturnsAllLeadingTrivia()
    {
        var tree = SyntaxTree.Parse("  \t x");
        var leaf = tree.Leaves.First();
        
        var leadingTrivia = leaf.GetLeadingTrivia().ToList();
        
        Assert.Single(leadingTrivia); // Consecutive whitespace is combined
        Assert.True(leadingTrivia[0].IsWhitespace);
    }
    
    [Fact]
    public void RedLeaf_GetTrailingTrivia_ReturnsAllTrailingTrivia()
    {
        var tree = SyntaxTree.Parse("x  \t\n");
        var leaf = tree.Leaves.First();
        
        var trailingTrivia = leaf.GetTrailingTrivia().ToList();
        
        // Whitespace + newline
        Assert.Equal(2, trailingTrivia.Count);
        Assert.True(trailingTrivia[0].IsWhitespace);
        Assert.True(trailingTrivia[1].IsNewline);
    }
    
    [Fact]
    public void RedLeaf_HasLeadingTrivia_ReturnsTrueWhenPresent()
    {
        var tree = SyntaxTree.Parse("  x");
        var leaf = tree.Leaves.First();
        
        Assert.True(leaf.HasLeadingTrivia);
    }
    
    [Fact]
    public void RedLeaf_HasLeadingTrivia_ReturnsFalseWhenEmpty()
    {
        var tree = SyntaxTree.Parse("x");
        var leaf = tree.Leaves.First();
        
        Assert.False(leaf.HasLeadingTrivia);
    }
    
    [Fact]
    public void RedLeaf_HasTrailingTrivia_ReturnsTrueWhenPresent()
    {
        var tree = SyntaxTree.Parse("x ");
        var leaf = tree.Leaves.First();
        
        Assert.True(leaf.HasTrailingTrivia);
    }
    
    [Fact]
    public void RedLeaf_HasTrailingTrivia_ReturnsFalseWhenEmpty()
    {
        var tree = SyntaxTree.Parse("x");
        var leaf = tree.Leaves.First();
        
        Assert.False(leaf.HasTrailingTrivia);
    }
    
    [Fact]
    public void RedBlock_GetLeadingTrivia_ReturnsBlockLeadingTrivia()
    {
        var tree = SyntaxTree.Parse("  {x}");
        var block = tree.Root.Children.OfType<RedBlock>().First();
        
        var leadingTrivia = block.GetLeadingTrivia().ToList();
        
        Assert.Single(leadingTrivia);
        Assert.True(leadingTrivia[0].IsWhitespace);
        Assert.Equal("  ", leadingTrivia[0].Text);
    }
    
    [Fact]
    public void RedBlock_GetTrailingTrivia_ReturnsBlockTrailingTrivia()
    {
        var tree = SyntaxTree.Parse("{x}  ");
        var block = tree.Root.Children.OfType<RedBlock>().First();
        
        var trailingTrivia = block.GetTrailingTrivia().ToList();
        
        Assert.Single(trailingTrivia);
        Assert.True(trailingTrivia[0].IsWhitespace);
        Assert.Equal("  ", trailingTrivia[0].Text);
    }
    
    [Fact]
    public void RedBlock_GetInnerTrivia_ReturnsInnerTrivia()
    {
        var tree = SyntaxTree.Parse("{  }"); // Empty block with spaces
        var block = tree.Root.Children.OfType<RedBlock>().First();
        
        var innerTrivia = block.GetInnerTrivia().ToList();
        
        Assert.Single(innerTrivia);
        Assert.True(innerTrivia[0].IsWhitespace);
        Assert.Equal("  ", innerTrivia[0].Text);
    }
    
    [Fact]
    public void RedBlock_HasLeadingTrivia_ReturnsCorrectValue()
    {
        var treeWith = SyntaxTree.Parse("  {x}");
        var treeWithout = SyntaxTree.Parse("{x}");
        
        var blockWith = treeWith.Root.Children.OfType<RedBlock>().First();
        var blockWithout = treeWithout.Root.Children.OfType<RedBlock>().First();
        
        Assert.True(blockWith.HasLeadingTrivia);
        Assert.False(blockWithout.HasLeadingTrivia);
    }
    
    [Fact]
    public void RedBlock_HasTrailingTrivia_ReturnsCorrectValue()
    {
        var treeWith = SyntaxTree.Parse("{x}  ");
        var treeWithout = SyntaxTree.Parse("{x}");
        
        var blockWith = treeWith.Root.Children.OfType<RedBlock>().First();
        var blockWithout = treeWithout.Root.Children.OfType<RedBlock>().First();
        
        Assert.True(blockWith.HasTrailingTrivia);
        Assert.False(blockWithout.HasTrailingTrivia);
    }
    
    [Fact]
    public void RedBlock_HasInnerTrivia_ReturnsCorrectValue()
    {
        var treeWith = SyntaxTree.Parse("{  }");
        var treeWithout = SyntaxTree.Parse("{}");
        
        var blockWith = treeWith.Root.Children.OfType<RedBlock>().First();
        var blockWithout = treeWithout.Root.Children.OfType<RedBlock>().First();
        
        Assert.True(blockWith.HasInnerTrivia);
        Assert.False(blockWithout.HasInnerTrivia);
    }
    
    #endregion
}
