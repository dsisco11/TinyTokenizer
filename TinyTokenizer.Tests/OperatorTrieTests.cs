using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the OperatorTrie class used for O(k) operator matching.
/// </summary>
[Trait("Category", "Operator")]
public class OperatorTrieTests
{
    [Fact]
    public void IsEmpty_EmptyTrie_ReturnsTrue()
    {
        var trie = new OperatorTrie();
        Assert.True(trie.IsEmpty);
    }

    [Fact]
    public void IsEmpty_AfterAdd_ReturnsFalse()
    {
        var trie = new OperatorTrie();
        trie.Add("+");
        Assert.False(trie.IsEmpty);
    }

    [Fact]
    public void TryMatch_EmptyTrie_ReturnsFalse()
    {
        var trie = new OperatorTrie();
        var result = trie.TryMatch("++".AsSpan(), out var matched);
        Assert.False(result);
        Assert.Null(matched);
    }

    [Fact]
    public void TryMatch_EmptyInput_ReturnsFalse()
    {
        var trie = new OperatorTrie();
        trie.Add("+");
        var result = trie.TryMatch(ReadOnlySpan<char>.Empty, out var matched);
        Assert.False(result);
        Assert.Null(matched);
    }

    [Fact]
    public void TryMatch_SingleCharOperator_MatchesExactly()
    {
        var trie = new OperatorTrie();
        trie.Add("+");

        var result = trie.TryMatch("+".AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal("+", matched);
    }

    [Fact]
    public void TryMatch_MultiCharOperator_MatchesExactly()
    {
        var trie = new OperatorTrie();
        trie.Add("==");

        var result = trie.TryMatch("==".AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal("==", matched);
    }

    [Fact]
    public void TryMatch_NoMatch_ReturnsFalse()
    {
        var trie = new OperatorTrie();
        trie.Add("++");

        var result = trie.TryMatch("--".AsSpan(), out var matched);
        Assert.False(result);
        Assert.Null(matched);
    }

    [Fact]
    public void TryMatch_PartialMatch_ReturnsLongestCompleteMatch()
    {
        var trie = new OperatorTrie();
        trie.Add("+");
        trie.Add("++");

        // Input "+" should match "+"
        var result1 = trie.TryMatch("+".AsSpan(), out var matched1);
        Assert.True(result1);
        Assert.Equal("+", matched1);

        // Input "++" should match "++" (greedy)
        var result2 = trie.TryMatch("++".AsSpan(), out var matched2);
        Assert.True(result2);
        Assert.Equal("++", matched2);
    }

    [Fact]
    public void TryMatch_GreedyMatching_ReturnsLongestMatch()
    {
        var trie = new OperatorTrie();
        trie.Add("=");
        trie.Add("==");
        trie.Add("===");

        // Input "===" should match "===" (longest)
        var result = trie.TryMatch("===".AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal("===", matched);
    }

    [Fact]
    public void TryMatch_InputLongerThanOperator_ReturnsMatch()
    {
        var trie = new OperatorTrie();
        trie.Add("==");

        // Input "==>" has "==" prefix, should match "=="
        var result = trie.TryMatch("==>".AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal("==", matched);
    }

    [Fact]
    public void TryMatch_PrefixNotAnOperator_ReturnsLongestCompleteOperator()
    {
        var trie = new OperatorTrie();
        trie.Add("===");
        // Note: "=" is not added

        // Input "=" has no match (we don't have "=" as operator)
        var result = trie.TryMatch("=".AsSpan(), out var matched);
        Assert.False(result);
        Assert.Null(matched);
    }

    [Fact]
    public void TryMatch_MultipleOperators_MatchesCorrectOne()
    {
        var trie = new OperatorTrie();
        trie.Add("+");
        trie.Add("-");
        trie.Add("*");
        trie.Add("/");
        trie.Add("==");
        trie.Add("!=");
        trie.Add("<=");
        trie.Add(">=");

        Assert.True(trie.TryMatch("+".AsSpan(), out var m1) && m1 == "+");
        Assert.True(trie.TryMatch("-".AsSpan(), out var m2) && m2 == "-");
        Assert.True(trie.TryMatch("==".AsSpan(), out var m3) && m3 == "==");
        Assert.True(trie.TryMatch("!=".AsSpan(), out var m4) && m4 == "!=");
        Assert.True(trie.TryMatch("<=".AsSpan(), out var m5) && m5 == "<=");
    }

    [Fact]
    public void TryMatch_OverlappingOperators_GreedyWins()
    {
        var trie = new OperatorTrie();
        trie.Add("<");
        trie.Add("<<");
        trie.Add("<<=");

        // Test greedy matching at each length
        Assert.True(trie.TryMatch("<".AsSpan(), out var m1) && m1 == "<");
        Assert.True(trie.TryMatch("<<".AsSpan(), out var m2) && m2 == "<<");
        Assert.True(trie.TryMatch("<<=".AsSpan(), out var m3) && m3 == "<<=");

        // Input "<<<" should match "<<" (longest complete match)
        Assert.True(trie.TryMatch("<<<".AsSpan(), out var m4) && m4 == "<<");
    }

    [Fact]
    public void Add_NullOrEmpty_DoesNothing()
    {
        var trie = new OperatorTrie();
        trie.Add(null!);
        trie.Add("");
        Assert.True(trie.IsEmpty);
    }

    [Fact]
    public void Add_DuplicateOperator_DoesNotCrash()
    {
        var trie = new OperatorTrie();
        trie.Add("==");
        trie.Add("==");

        var result = trie.TryMatch("==".AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal("==", matched);
    }

    [Fact]
    public void TryMatch_LongOperator_Works()
    {
        var trie = new OperatorTrie();
        trie.Add(">>>=");

        var result = trie.TryMatch(">>>=".AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal(">>>=", matched);
    }

    [Fact]
    public void TryMatch_ManyOperators_AllMatch()
    {
        var trie = new OperatorTrie();
        
        // Add 50+ operators (common C-family operators)
        string[] operators = 
        [
            "+", "-", "*", "/", "%", "^", "&", "|", "~", "!",
            "=", "<", ">", "?", ":", ",", ";", ".",
            "++", "--", "+=", "-=", "*=", "/=", "%=", "^=", "&=", "|=",
            "==", "!=", "<=", ">=", "&&", "||", "<<", ">>",
            "->", "=>", "::", "??", "?.", "?:",
            "<<=", ">>=", "&&=", "||=", "??=",
            "===", "!==", ">>>", ">>>=",
            "..."
        ];

        foreach (var op in operators)
        {
            trie.Add(op);
        }

        // Verify all operators can be matched
        foreach (var op in operators)
        {
            var result = trie.TryMatch(op.AsSpan(), out var matched);
            Assert.True(result, $"Failed to match operator: {op}");
            Assert.Equal(op, matched);
        }
    }

    [Theory]
    [InlineData("+", "+a", "+")]      // Single char, followed by non-operator
    [InlineData("==", "==1", "==")]   // Multi-char, followed by digit
    [InlineData("->", "->foo", "->")] // Arrow, followed by identifier
    public void TryMatch_OperatorAtStartOfInput_MatchesOperator(string op, string input, string expected)
    {
        var trie = new OperatorTrie();
        trie.Add(op);

        var result = trie.TryMatch(input.AsSpan(), out var matched);
        Assert.True(result);
        Assert.Equal(expected, matched);
    }
}
