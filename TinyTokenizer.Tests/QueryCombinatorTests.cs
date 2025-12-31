using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the unified Query combinator API.
/// </summary>
public class QueryCombinatorTests
{
    private static readonly Schema DefaultSchema = Schema.Create()
        .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .WithOperators(CommonOperators.CFamily)
        .Build();

    private static SyntaxTree Parse(string source) => SyntaxTree.Parse(source, DefaultSchema);

    #region Sequence Query Tests

    [Fact]
    public void Sequence_MatchesTwoIdentifiers()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent, Query.AnyIdent);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Sequence_FailsIfSecondNotMatch()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent, Query.AnyIdent);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    [Fact]
    public void Sequence_MatchesFunctionCall()
    {
        var tree = Parse("foo(a, b)");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent, Query.ParenBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Sequence_MatchesFourParts()
    {
        // Function definition: type name(params) { body }
        var tree = Parse("void main() {}");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent, Query.AnyIdent, Query.ParenBlock, Query.BraceBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void Sequence_GreenMatching()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        // Get green children via the container interface
        var greenRoot = (GreenContainer)root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.Sequence(Query.AnyIdent, Query.AnyIdent);
        var greenQuery = (IGreenNodeQuery)query;
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed));
        Assert.Equal(2, consumed);
    }

    #endregion

    #region Optional Query Tests

    [Fact]
    public void Optional_MatchesWhenPresent()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.Optional();
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void Optional_SucceedsWithZeroWhenAbsent()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyIdent.Optional();
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Optional_InSequence()
    {
        // Match "foo = bar" or "foo bar"
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent, Query.AnyOperator.Optional(), Query.AnyIdent);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed); // Only 2 because = is optional and not present
    }

    #endregion

    #region Repeat Query Tests

    [Fact]
    public void ZeroOrMore_MatchesZero()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyIdent.ZeroOrMore();
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void ZeroOrMore_MatchesMultiple()
    {
        var tree = Parse("foo bar baz 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.ZeroOrMore();
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void OneOrMore_FailsOnZero()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyIdent.OneOrMore();
        Assert.False(query.TryMatch(firstNode, out _));
    }

    [Fact]
    public void OneOrMore_MatchesMultiple()
    {
        var tree = Parse("foo bar baz 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.OneOrMore();
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void Exactly_MatchesExactCount()
    {
        var tree = Parse("foo bar baz qux");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.Exactly(2);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Exactly_FailsIfNotEnough()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.Exactly(2);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    [Fact]
    public void Repeat_WithMinMax()
    {
        var tree = Parse("foo bar baz qux quux");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.Repeat(2, 3);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed); // Stops at max
    }

    #endregion

    #region Until Query Tests

    [Fact]
    public void Until_StopsAtTerminator()
    {
        var tree = Parse("foo bar baz;");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Any.Until(Query.AnySymbol);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed); // foo, bar, baz (not semicolon)
    }

    [Fact]
    public void Until_MatchesZeroIfTerminatorFirst()
    {
        var tree = Parse("; foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyIdent.Until(Query.AnySymbol);
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Until_MatchesAllIfNoTerminator()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.Until(Query.AnySymbol);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(3, consumed);
    }

    #endregion

    #region Lookahead Query Tests

    [Fact]
    public void FollowedBy_MatchesWithLookahead()
    {
        var tree = Parse("foo(bar)");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // Match identifier only if followed by paren block (function call)
        var query = Query.AnyIdent.FollowedBy(Query.ParenBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(1, consumed); // Only consumes the identifier
    }

    [Fact]
    public void FollowedBy_FailsWithoutLookahead()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.FollowedBy(Query.ParenBlock);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    [Fact]
    public void NotFollowedBy_MatchesWithoutLookahead()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // Match identifier only if NOT followed by paren block (not a function call)
        var query = Query.AnyIdent.NotFollowedBy(Query.ParenBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void NotFollowedBy_FailsWithLookahead()
    {
        var tree = Parse("foo(bar)");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.NotFollowedBy(Query.ParenBlock);
        Assert.False(query.TryMatch(firstIdent, out _));
    }

    #endregion

    #region Union (Alternation) Tests

    [Fact]
    public void Union_MatchesEither()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        
        var query = Query.AnyIdent | Query.AnyNumeric;
        
        Assert.True(query.TryMatch(root.Children.First(), out var c1));
        Assert.Equal(1, c1);
        
        Assert.True(query.TryMatch(root.Children.Skip(1).First(), out var c2));
        Assert.Equal(1, c2);
    }

    [Fact]
    public void Union_InSequence()
    {
        var tree = Parse("foo 123");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent | Query.AnyNumeric, Query.AnyIdent | Query.AnyNumeric);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    #endregion

    #region Complex Pattern Tests

    [Fact]
    public void FunctionDefinition_Pattern()
    {
        var tree = Parse("void main() { return; }");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // type name(params) { body }
        var query = Query.Sequence(Query.AnyIdent, Query.AnyIdent, Query.ParenBlock, Query.BraceBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void MethodChain_Pattern()
    {
        var tree = Parse("foo.bar.baz()");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        // identifier followed by .identifier repeated, then optional call
        var dotIdent = Query.Sequence(Query.AnySymbol.WithText("."), Query.AnyIdent);
        var query = Query.Sequence(Query.AnyIdent, dotIdent.OneOrMore(), Query.ParenBlock.Optional());
        
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        // foo, ., bar, ., baz, ()
        Assert.True(consumed >= 5);
    }

    [Fact]
    public void ArrayAccess_Pattern()
    {
        var tree = Parse("arr[0]");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.Sequence(Query.AnyIdent, Query.BracketBlock);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    #endregion

    #region Then Extension Method Tests

    [Fact]
    public void Then_CreatesTwoPartSequence()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent.Then(Query.AnyIdent);
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void Then_ChainsMultipleParts()
    {
        var tree = Parse("void main() {}");
        var root = tree.Root;
        var firstIdent = root.Children.First();
        
        var query = Query.AnyIdent
            .Then(Query.AnyIdent)
            .Then(Query.ParenBlock)
            .Then(Query.BraceBlock);
            
        Assert.True(query.TryMatch(firstIdent, out var consumed));
        Assert.Equal(4, consumed);
    }

    #endregion
    
    #region TaggedIdent Text Constraint Tests

    [Fact]
    public void TaggedIdent_WithTextConstraint_GreenMatching()
    {
        // Create schema with tag prefixes
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .WithTagPrefixes('#', '@')
            .Build();
        
        var tree = SyntaxTree.Parse("@import \"file.txt\"", schema);
        var root = tree.Root;
        var greenRoot = (GreenContainer)root.Green;
        var greenChildren = greenRoot.Children;
        
        // Verify we have the expected children
        Assert.True(greenChildren.Length >= 2, $"Expected at least 2 children, got {greenChildren.Length}");
        var firstChild = greenChildren[0];
        Assert.Equal(NodeKind.TaggedIdent, firstChild.Kind);
        
        // Verify text is "@import"
        var leaf = Assert.IsType<GreenLeaf>(firstChild);
        Assert.Equal("@import", leaf.Text);
        
        // Test that Query.TaggedIdent("@import") matches at green level
        var matchingQuery = Query.TaggedIdent("@import");
        var greenQuery = (IGreenNodeQuery)matchingQuery;
        Assert.True(greenQuery.MatchesGreen(firstChild), "Query.TaggedIdent(\"@import\") should match @import");
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed1));
        Assert.Equal(1, consumed1);
        
        // Test that Query.TaggedIdent("#version") does NOT match @import
        var nonMatchingQuery = Query.TaggedIdent("#version");
        var greenQuery2 = (IGreenNodeQuery)nonMatchingQuery;
        Assert.False(greenQuery2.MatchesGreen(firstChild), "Query.TaggedIdent(\"#version\") should NOT match @import");
        Assert.False(greenQuery2.TryMatchGreen(greenChildren, 0, out _));
    }

    [Fact]
    public void MultipleTaggedIdentPatterns_DifferentTextConstraints()
    {
        // Create schema with tag prefixes
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .WithTagPrefixes('#', '@')
            .Build();
        
        var tree = SyntaxTree.Parse("#version 330\n@import \"file.txt\"", schema);
        var root = tree.Root;
        var greenRoot = (GreenContainer)root.Green;
        var greenChildren = greenRoot.Children;
        
        // Find the @import node
        GreenLeaf? importNode = null;
        GreenLeaf? versionNode = null;
        foreach (var child in greenChildren)
        {
            if (child is GreenLeaf leaf && leaf.Kind == NodeKind.TaggedIdent)
            {
                if (leaf.Text == "@import")
                    importNode = leaf;
                else if (leaf.Text == "#version")
                    versionNode = leaf;
            }
        }
        
        Assert.NotNull(versionNode);
        Assert.NotNull(importNode);
        
        var importQuery = Query.TaggedIdent("@import");
        var versionQuery = Query.TaggedIdent("#version");
        var anyTaggedQuery = Query.AnyTaggedIdent;
        
        // importQuery should match @import but not #version
        Assert.True(((IGreenNodeQuery)importQuery).MatchesGreen(importNode!));
        Assert.False(((IGreenNodeQuery)importQuery).MatchesGreen(versionNode!));
        
        // versionQuery should match #version but not @import
        Assert.True(((IGreenNodeQuery)versionQuery).MatchesGreen(versionNode!));
        Assert.False(((IGreenNodeQuery)versionQuery).MatchesGreen(importNode!));
        
        // anyTaggedQuery should match both
        Assert.True(((IGreenNodeQuery)anyTaggedQuery).MatchesGreen(importNode!));
        Assert.True(((IGreenNodeQuery)anyTaggedQuery).MatchesGreen(versionNode!));
    }

    [Fact]
    public void TaggedIdent_SyntaxBinding_DistinguishesByText()
    {
        // Create schema with two syntax definitions using different TaggedIdent patterns
        var schema = Schema.Create()
            .WithCommentStyles(CommentStyle.CStyleSingleLine)
            .WithTagPrefixes('#', '@')
            .DefineSyntax(Syntax.Define<ImportSyntax>("Import")
                .Match(Query.TaggedIdent("@import"), Query.Any.Until(Query.Newline))
                .WithPriority(1)
                .Build())
            .DefineSyntax(Syntax.Define<DirectiveSyntax>("Directive")
                .Match(Query.AnyTaggedIdent, Query.Any.Until(Query.Newline))
                .Build())
            .Build();
        
        var tree = SyntaxTree.Parse("#version 330\n@import \"file.txt\"", schema);
        
        // Find all ImportSyntax nodes - should only match @import
        var imports = tree.Select(Query.Syntax<ImportSyntax>()).ToList();
        var directivesList = tree.Select(Query.Syntax<DirectiveSyntax>()).ToList();
        
        // Should have exactly 1 import and 1 directive
        Assert.Single(imports);
        Assert.Single(directivesList);
        
        // Verify content
        var importNode = imports[0] as ImportSyntax;
        Assert.NotNull(importNode);
        Assert.Contains("@import", importNode!.ToString());
        
        var directiveNode = directivesList[0] as DirectiveSyntax;
        Assert.NotNull(directiveNode);
        Assert.Contains("#version", directiveNode!.ToString());
    }
    
    private static string DumpGreenTree(GreenNode node, int indent)
    {
        var sb = new System.Text.StringBuilder();
        var prefix = new string(' ', indent * 2);
        
        // Show trivia info for leaves
        string triviaInfo = "";
        string textContent = "";
        if (node is GreenLeaf leaf)
        {
            textContent = leaf.Text.Replace("\n", "\\n").Replace("\r", "\\r");
            var leadingNewlines = leaf.LeadingTrivia.Count(t => t.Kind == TriviaKind.Newline);
            var trailingNewlines = leaf.TrailingTrivia.Count(t => t.Kind == TriviaKind.Newline);
            if (leadingNewlines > 0 || trailingNewlines > 0)
                triviaInfo = $" [lead:{leadingNewlines}NL, trail:{trailingNewlines}NL]";
        }
        else
        {
            textContent = node.ToText().Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        if (textContent.Length > 30) textContent = textContent[..30] + "...";
        
        sb.AppendLine($"{prefix}{node.ToString("D", null)}{triviaInfo}: \"{textContent}\"");
        
        for (int i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child != null)
                sb.Append(DumpGreenTree(child, indent + 1));
        }
        
        return sb.ToString();
    }
    
    /// <summary>Test syntax node for @import directives.</summary>
    public sealed class ImportSyntax : SyntaxNode
    {
        public ImportSyntax(CreationContext context) : base(context) { }
    }
    
    /// <summary>Test syntax node for general # directives.</summary>
    public sealed class DirectiveSyntax : SyntaxNode
    {
        public DirectiveSyntax(CreationContext context) : base(context) { }
    }

    #endregion
}
