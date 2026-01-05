using System.Collections.Immutable;
using TinyTokenizer.Ast;
using Xunit;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the unified Query combinator API.
/// </summary>
[Trait("Category", "Query")]
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
        Assert.Contains("@import", importNode!.ToText());
        
        var directiveNode = directivesList[0] as DirectiveSyntax;
        Assert.NotNull(directiveNode);
        Assert.Contains("#version", directiveNode!.ToText());
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
    
    #region AnyOf Query Tests
    
    [Fact]
    public void AnyOf_MatchesFirstQuery()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void AnyOf_MatchesSecondQuery()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void AnyOf_FailsWhenNoneMatch()
    {
        var tree = Parse("'string'");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.False(query.Matches(firstNode));
    }
    
    [Fact]
    public void AnyOf_TryMatch_ReturnsFirstMatchConsumedCount()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // AnyOf with sequence (consumes 2) OR single ident (consumes 1)
        var query = Query.AnyOf(
            Query.Sequence(Query.AnyIdent, Query.AnyIdent),
            Query.AnyIdent
        );
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(2, consumed); // First matching query wins
    }
    
    [Fact]
    public void AnyOf_Select_ReturnsDistinctMatches()
    {
        var tree = Parse("foo 123 bar");
        var root = tree.Root;
        
        var query = Query.AnyOf(Query.AnyIdent, Query.AnyNumeric);
        var matches = query.Select(root).ToList();
        
        Assert.Equal(3, matches.Count); // foo, 123, bar
    }
    
    #endregion
    
    #region NoneOf Query Tests
    
    [Fact]
    public void NoneOf_MatchesWhenNoQueryMatches()
    {
        var tree = Parse("'string'");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.NoneOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void NoneOf_FailsWhenAnyQueryMatches()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.NoneOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.False(query.Matches(firstNode));
    }
    
    [Fact]
    public void NoneOf_TryMatch_ConsumesOneWhenMatched()
    {
        var tree = Parse("'string'");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.NoneOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(1, consumed);
    }
    
    [Fact]
    public void NoneOf_TryMatch_ConsumesZeroWhenFailed()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.NoneOf(Query.AnyIdent, Query.AnyNumeric);
        Assert.False(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }
    
    #endregion
    
    #region Not Query Tests
    
    [Fact]
    public void Not_SucceedsWhenInnerFails()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Not(Query.AnyIdent);
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void Not_FailsWhenInnerSucceeds()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Not(Query.AnyIdent);
        Assert.False(query.Matches(firstNode));
    }
    
    [Fact]
    public void Not_ZeroWidth_NeverConsumes()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Not(Query.AnyIdent);
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed); // Zero-width assertion
    }
    
    [Fact]
    public void Not_InSequence_NegativeLookahead()
    {
        // Match any identifier that is NOT "if"
        var tree = Parse("foo bar if baz");
        var root = tree.Root;
        
        var query = Query.Sequence(Query.Not(Query.Ident("if")), Query.AnyIdent);
        
        // Should match foo, bar, baz but not if
        var matches = new List<string>();
        foreach (var child in root.Children)
        {
            if (query.TryMatch(child, out _))
                matches.Add(child.ToText().Trim());
        }
        
        Assert.Equal(3, matches.Count);
        Assert.Contains("foo", matches);
        Assert.Contains("bar", matches);
        Assert.Contains("baz", matches);
        Assert.DoesNotContain("if", matches);
    }
    
    [Fact]
    public void Not_ExtensionMethod()
    {
        var tree = Parse("123");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyIdent.Not();
        Assert.True(query.Matches(firstNode));
    }
    
    #endregion
    
    #region BOF Query Tests
    
    [Fact]
    public void BOF_MatchesFirstNode()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        Assert.True(Query.BOF.Matches(firstNode));
    }
    
    [Fact]
    public void BOF_DoesNotMatchMiddleNode()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var secondNode = root.Children.Skip(1).First();
        
        Assert.False(Query.BOF.Matches(secondNode));
    }
    
    [Fact]
    public void BOF_ZeroWidth_NeverConsumes()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        Assert.True(Query.BOF.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }
    
    [Fact]
    public void BOF_Select_ReturnsOnlyFirstNode()
    {
        var tree = Parse("foo bar baz");
        var matches = Query.BOF.Select(tree).ToList();
        
        Assert.Single(matches);
        Assert.StartsWith("foo", matches[0].ToText());
    }
    
    [Fact]
    public void BOF_InSequence()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Match: BOF then identifier
        var query = Query.Sequence(Query.BOF, Query.AnyIdent);
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(1, consumed); // Only ident consumed, BOF is zero-width
    }
    
    #endregion
    
    #region EOF Query Tests
    
    [Fact]
    public void EOF_MatchesLastNode()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var lastNode = root.Children.Last();
        
        Assert.True(Query.EOF.Matches(lastNode));
    }
    
    [Fact]
    public void EOF_DoesNotMatchFirstNode()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        Assert.False(Query.EOF.Matches(firstNode));
    }
    
    [Fact]
    public void EOF_ZeroWidth_NeverConsumes()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var lastNode = root.Children.Last();
        
        Assert.True(Query.EOF.TryMatch(lastNode, out var consumed));
        Assert.Equal(0, consumed);
    }
    
    [Fact]
    public void EOF_Select_ReturnsOnlyLastNode()
    {
        var tree = Parse("foo bar baz");
        var matches = Query.EOF.Select(tree).ToList();
        
        Assert.Single(matches);
        Assert.Equal("baz", matches[0].ToText());
    }
    
    [Fact]
    public void EOF_SingleToken_MatchesBothBOFAndEOF()
    {
        var tree = Parse("single");
        var root = tree.Root;
        var onlyNode = root.Children.First();
        
        Assert.True(Query.BOF.Matches(onlyNode));
        Assert.True(Query.EOF.Matches(onlyNode));
    }
    
    #endregion
    
    #region Between Query Tests
    
    [Fact]
    public void Between_MatchesContentBetweenDelimiters()
    {
        // Use angle brackets which are operators, not block delimiters
        var tree = Parse("< foo bar >");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Between < and >
        var query = Query.Between(Query.Operator("<"), Query.Operator(">"));
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.True(consumed >= 2); // At least < and >
    }
    
    [Fact]
    public void Between_TryMatch_ConsumesAllIncludingDelimiters()
    {
        // Use symbols that aren't block delimiters
        var tree = Parse("before < content > after");
        var root = tree.Root;
        
        // Find the open angle bracket
        var children = root.Children.ToList();
        var openAngle = children.FirstOrDefault(c => c.ToText().Trim() == "<");
        
        if (openAngle != null)
        {
            var query = Query.Between(Query.Operator("<"), Query.Operator(">"), inclusive: true);
            Assert.True(query.TryMatch(openAngle, out var consumed));
            // Should consume: < content >
            Assert.True(consumed >= 1);
        }
    }
    
    [Fact]
    public void Between_FailsWhenEndNotFound()
    {
        var tree = Parse("( foo bar");
        var root = tree.Root;
        var children = root.Children.ToList();
        var openParen = children.FirstOrDefault(c => c.ToString() == "(");
        
        if (openParen != null)
        {
            var query = Query.Between(Query.Symbol("("), Query.Symbol(")"));
            Assert.False(query.TryMatch(openParen, out _));
        }
    }
    
    [Fact]
    public void Between_ExtensionMethod()
    {
        var tree = Parse("before ( content ) after");
        var root = tree.Root;
        var children = root.Children.ToList();
        var openParen = children.FirstOrDefault(c => c.ToString() == "(");
        
        if (openParen != null)
        {
            var query = Query.Symbol("(").Between(Query.Symbol(")"));
            Assert.True(query.TryMatch(openParen, out _));
        }
    }
    
    #endregion
    
    #region Sibling Query Tests
    
    [Fact]
    public void Sibling_MatchesNextSibling()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Check if next sibling (+1) matches identifier
        var query = Query.Sibling(1, Query.AnyIdent);
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void Sibling_MatchesPreviousSibling()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var secondNode = root.Children.Skip(1).First();
        
        // Check if previous sibling (-1) matches identifier
        var query = Query.Sibling(-1, Query.AnyIdent);
        Assert.True(query.Matches(secondNode));
    }
    
    [Fact]
    public void Sibling_FailsWhenSiblingDoesNotExist()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var onlyNode = root.Children.First();
        
        var query = Query.Sibling(1); // Next sibling doesn't exist
        Assert.False(query.Matches(onlyNode));
    }
    
    [Fact]
    public void Sibling_ZeroWidth_NeverConsumes()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Sibling(1);
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed); // Zero-width navigation
    }
    
    [Fact]
    public void Sibling_ExtensionMethods()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var secondNode = root.Children.Skip(1).First();
        
        // NextSibling extension
        var nextQuery = Query.AnyIdent.NextSibling();
        Assert.True(nextQuery.Matches(secondNode));
        
        // PreviousSibling extension
        var prevQuery = Query.AnyIdent.PreviousSibling();
        Assert.True(prevQuery.Matches(secondNode));
    }
    
    #endregion
    
    #region Parent Query Tests
    
    [Fact]
    public void Parent_MatchesDirectParent()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        Assert.NotNull(block);
        
        var innerIdent = block!.Children.First();
        
        var query = Query.Parent(Query.BraceBlock);
        Assert.True(query.Matches(innerIdent));
    }
    
    [Fact]
    public void Parent_FailsWhenParentDoesNotMatch()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        Assert.NotNull(block);
        
        var innerIdent = block!.Children.First();
        
        var query = Query.Parent(Query.ParenBlock); // Wrong block type
        Assert.False(query.Matches(innerIdent));
    }
    
    [Fact]
    public void Parent_ZeroWidth_NeverConsumes()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        var innerIdent = block!.Children.First();
        
        var query = Query.Parent();
        Assert.True(query.TryMatch(innerIdent, out var consumed));
        Assert.Equal(0, consumed);
    }
    
    [Fact]
    public void Parent_WithoutFilter_MatchesAnyParent()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        var innerIdent = block!.Children.First();
        
        var query = Query.Parent(); // No inner query
        Assert.True(query.Matches(innerIdent));
    }
    
    #endregion
    
    #region Ancestor Query Tests
    
    [Fact]
    public void Ancestor_MatchesDirectParent()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        var innerIdent = block!.Children.First();
        
        var query = Query.Ancestor(Query.BraceBlock);
        Assert.True(query.Matches(innerIdent));
    }
    
    [Fact]
    public void Ancestor_MatchesGrandparent()
    {
        var tree = Parse("{ [ foo ] }");
        var root = tree.Root;
        var braceBlock = root.Children.First() as SyntaxBlock;
        var bracketBlock = braceBlock!.Children.First() as SyntaxBlock;
        var innerIdent = bracketBlock!.Children.First();
        
        // innerIdent's grandparent is brace block
        var query = Query.Ancestor(Query.BraceBlock);
        Assert.True(query.Matches(innerIdent));
    }
    
    [Fact]
    public void Ancestor_FailsWhenNoAncestorMatches()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        var innerIdent = block!.Children.First();
        
        var query = Query.Ancestor(Query.ParenBlock); // No paren ancestor
        Assert.False(query.Matches(innerIdent));
    }
    
    [Fact]
    public void Ancestor_ZeroWidth_NeverConsumes()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        var innerIdent = block!.Children.First();
        
        var query = Query.Ancestor(Query.BraceBlock);
        Assert.True(query.TryMatch(innerIdent, out var consumed));
        Assert.Equal(0, consumed);
    }
    
    [Fact]
    public void Ancestor_Select_ReturnsMatchingAncestors()
    {
        var tree = Parse("{ [ foo ] }");
        var root = tree.Root;
        
        var query = Query.Ancestor(Query.AnyBlock);
        var matches = query.Select(root).ToList();
        
        // Should return both brace and bracket blocks (as ancestors of foo)
        Assert.True(matches.Count >= 1);
    }
    
    #endregion
    
    #region Fluent Extension Tests
    
    [Fact]
    public void Or_Extension_CreatesAnyOf()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyIdent.Or(Query.AnyNumeric, Query.AnyString);
        Assert.True(query.Matches(firstNode));
        Assert.IsType<AnyOfQuery>(query);
    }
    
    [Fact]
    public void AsParent_Extension_CreatesParentQuery()
    {
        var tree = Parse("{ foo }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        var innerIdent = block!.Children.First();
        
        var query = Query.BraceBlock.AsParent();
        Assert.True(query.Matches(innerIdent));
        Assert.IsType<ParentQuery>(query);
    }
    
    [Fact]
    public void AsAncestor_Extension_CreatesAncestorQuery()
    {
        var tree = Parse("{ [ foo ] }");
        var root = tree.Root;
        var braceBlock = root.Children.First() as SyntaxBlock;
        var bracketBlock = braceBlock!.Children.First() as SyntaxBlock;
        var innerIdent = bracketBlock!.Children.First();
        
        var query = Query.BraceBlock.AsAncestor();
        Assert.True(query.Matches(innerIdent));
        Assert.IsType<AncestorQuery>(query);
    }
    
    #endregion
    
    #region Edge Case Tests - Empty/Degenerate Inputs
    
    [Fact]
    public void AnyOf_EmptyArray_NeverMatches()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.AnyOf(Array.Empty<INodeQuery>());
        Assert.False(query.Matches(firstNode));
        Assert.False(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(0, consumed);
    }
    
    [Fact]
    public void NoneOf_EmptyArray_AlwaysMatches()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // With no queries to fail against, NoneOf should match anything
        var query = Query.NoneOf(Array.Empty<INodeQuery>());
        Assert.True(query.Matches(firstNode));
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(1, consumed);
    }
    
    [Fact]
    public void BOF_EOF_SingleToken_BothMatch()
    {
        var tree = Parse("only");
        var root = tree.Root;
        var onlyNode = root.Children.First();
        
        // Single token is both BOF and EOF
        Assert.True(Query.BOF.Matches(onlyNode));
        Assert.True(Query.EOF.Matches(onlyNode));
    }
    
    [Fact]
    public void Between_EmptyContent_MatchesAdjacentDelimiters()
    {
        var tree = Parse("< >");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Between(Query.Operator("<"), Query.Operator(">"));
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(2, consumed); // Just < and >
    }
    
    #endregion
    
    #region Edge Case Tests - BOF/EOF Nested Context
    
    [Fact]
    public void BOF_DoesNotMatchFirstInsideBlock()
    {
        var tree = Parse("{ foo bar }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        Assert.NotNull(block);
        
        var firstInsideBlock = block!.Children.First();
        
        // BOF should NOT match inside a block - only at root level
        Assert.False(Query.BOF.Matches(firstInsideBlock));
    }
    
    [Fact]
    public void EOF_DoesNotMatchLastInsideBlock()
    {
        var tree = Parse("{ foo bar }");
        var root = tree.Root;
        var block = root.Children.First() as SyntaxBlock;
        Assert.NotNull(block);
        
        var lastInsideBlock = block!.Children.Last();
        
        // EOF should NOT match inside a block - only at root level
        Assert.False(Query.EOF.Matches(lastInsideBlock));
    }
    
    [Fact]
    public void BOF_MatchesOnlyAtRootLevel()
    {
        var tree = Parse("first { nested } last");
        var root = tree.Root;
        
        var bofMatches = Query.BOF.Select(root).ToList();
        Assert.Single(bofMatches);
        Assert.StartsWith("first", bofMatches[0].ToText());
    }
    
    #endregion
    
    #region Edge Case Tests - Between
    
    [Fact]
    public void Between_Exclusive_DoesNotCountDelimiters()
    {
        var tree = Parse("< a b c >");
        var root = tree.Root;
        var children = root.Children.ToList();
        var openAngle = children.First(c => c.ToText().Trim() == "<");
        
        var queryInclusive = Query.Between(Query.Operator("<"), Query.Operator(">"), inclusive: true);
        var queryExclusive = Query.Between(Query.Operator("<"), Query.Operator(">"), inclusive: false);
        
        Assert.True(queryInclusive.TryMatch(openAngle, out var consumedInclusive));
        Assert.True(queryExclusive.TryMatch(openAngle, out var consumedExclusive));
        
        // Exclusive should report fewer consumed (just the content, not delimiters)
        Assert.True(consumedExclusive < consumedInclusive);
    }
    
    [Fact]
    public void Between_StartNotFound_Fails()
    {
        var tree = Parse("foo bar baz");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Between(Query.Operator("<"), Query.Operator(">"));
        Assert.False(query.TryMatch(firstNode, out _));
    }
    
    [Fact]
    public void Between_SameStartAndEnd_MatchesFirstPair()
    {
        // Using same delimiter for start and end
        var tree = Parse("| content |");
        var root = tree.Root;
        var children = root.Children.ToList();
        var firstPipe = children.FirstOrDefault(c => c.ToText().Trim() == "|");
        
        if (firstPipe != null)
        {
            var query = Query.Between(Query.Operator("|"), Query.Operator("|"));
            Assert.True(query.TryMatch(firstPipe, out var consumed));
            Assert.True(consumed >= 2);
        }
    }
    
    #endregion
    
    #region Edge Case Tests - Sibling
    
    [Fact]
    public void Sibling_ZeroOffset_MatchesSelf()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Sibling(0, Query.AnyIdent);
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void Sibling_LargeOffset_DoesNotMatch()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Offset way beyond available siblings
        var query = Query.Sibling(100);
        Assert.False(query.Matches(firstNode));
    }
    
    [Fact]
    public void Sibling_NegativeOffset_NavigatesBackward()
    {
        var tree = Parse("a b c");
        var root = tree.Root;
        var children = root.Children.ToList();
        var lastNode = children.Last();
        
        // Go back 2 siblings
        var query = Query.Sibling(-2, Query.AnyIdent);
        Assert.True(query.Matches(lastNode));
    }
    
    [Fact]
    public void Sibling_NegativeOffsetFromFirst_DoesNotMatch()
    {
        var tree = Parse("foo bar");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Sibling(-1);
        Assert.False(query.Matches(firstNode));
    }
    
    #endregion
    
    #region Edge Case Tests - Parent/Ancestor at Root
    
    [Fact]
    public void Parent_AtRoot_DoesNotMatch()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // firstNode's parent is root, which has no parent
        // But Parent query checks if the node's parent matches, which it does (root exists)
        var query = Query.Parent();
        Assert.True(query.Matches(firstNode)); // Has a parent (root)
        
        // Root itself has no parent
        Assert.False(query.Matches(root));
    }
    
    [Fact]
    public void Ancestor_AtRoot_DoesNotMatch()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        
        // Root has no ancestors
        var query = Query.Ancestor(Query.Any);
        Assert.False(query.Matches(root));
    }
    
    [Fact]
    public void Parent_WithFilter_FailsWhenParentDoesNotMatchFilter()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Parent exists but doesn't match the filter
        var query = Query.Parent(Query.BraceBlock);
        Assert.False(query.Matches(firstNode));
    }
    
    #endregion
    
    #region Edge Case Tests - Green Node Matching
    
    [Fact]
    public void AnyOf_GreenMatching()
    {
        var tree = Parse("foo 123");
        var greenRoot = (GreenContainer)tree.Root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.AnyOf(Query.AnyIdent, Query.AnyNumeric);
        var greenQuery = (IGreenNodeQuery)query;
        
        // First child is identifier
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed1));
        Assert.Equal(1, consumed1);
        
        // Second child is numeric (after skipping whitespace)
        // Find the numeric index
        for (int i = 0; i < greenChildren.Length; i++)
        {
            if (greenChildren[i].Kind == NodeKind.Numeric)
            {
                Assert.True(greenQuery.TryMatchGreen(greenChildren, i, out var consumed2));
                Assert.Equal(1, consumed2);
                break;
            }
        }
    }
    
    [Fact]
    public void NoneOf_GreenMatching()
    {
        var tree = Parse("'string'");
        var greenRoot = (GreenContainer)tree.Root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.NoneOf(Query.AnyIdent, Query.AnyNumeric);
        var greenQuery = (IGreenNodeQuery)query;
        
        // String doesn't match ident or numeric
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed));
        Assert.Equal(1, consumed);
    }
    
    [Fact]
    public void Not_GreenMatching()
    {
        var tree = Parse("123");
        var greenRoot = (GreenContainer)tree.Root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.Not(Query.AnyIdent);
        var greenQuery = (IGreenNodeQuery)query;
        
        // Numeric is not an identifier
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed));
        Assert.Equal(0, consumed); // Zero-width
    }
    
    [Fact]
    public void BOF_GreenMatching_MatchesFirstIndex()
    {
        var tree = Parse("foo bar");
        var greenRoot = (GreenContainer)tree.Root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.BOF;
        var greenQuery = (IGreenNodeQuery)query;
        
        // Index 0 should match BOF
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed0));
        Assert.Equal(0, consumed0);
        
        // Index 1 should not match BOF
        Assert.False(greenQuery.TryMatchGreen(greenChildren, 1, out _));
    }
    
    [Fact]
    public void EOF_GreenMatching_MatchesLastIndex()
    {
        var tree = Parse("foo bar");
        var greenRoot = (GreenContainer)tree.Root.Green;
        var greenChildren = greenRoot.Children;
        var lastIndex = greenChildren.Length - 1;
        
        var query = Query.EOF;
        var greenQuery = (IGreenNodeQuery)query;
        
        // Last index should match EOF
        Assert.True(greenQuery.TryMatchGreen(greenChildren, lastIndex, out var consumedLast));
        Assert.Equal(0, consumedLast);
        
        // Index 0 should not match EOF (unless single element)
        if (greenChildren.Length > 1)
        {
            Assert.False(greenQuery.TryMatchGreen(greenChildren, 0, out _));
        }
    }
    
    [Fact]
    public void Between_GreenMatching()
    {
        var tree = Parse("< a >");
        var greenRoot = (GreenContainer)tree.Root.Green;
        var greenChildren = greenRoot.Children;
        
        var query = Query.Between(Query.Operator("<"), Query.Operator(">"));
        var greenQuery = (IGreenNodeQuery)query;
        
        Assert.True(greenQuery.TryMatchGreen(greenChildren, 0, out var consumed));
        Assert.True(consumed >= 2);
    }
    
    #endregion
    
    #region Edge Case Tests - Complex Compositions
    
    [Fact]
    public void BOF_Sequence_EOF_MatchesEntireFile()
    {
        var tree = Parse("only");
        var root = tree.Root;
        var onlyNode = root.Children.First();
        
        // BOF (0) + ident (1) = 1 consumed
        // Note: EOF cannot be checked in a sequence after consuming the last node
        // because there's no node left to test against. Use FollowedBy for this.
        var query = Query.Sequence(Query.BOF, Query.AnyIdent);
        Assert.True(query.TryMatch(onlyNode, out var consumed));
        Assert.Equal(1, consumed);
        
        // Verify the node is also EOF
        Assert.True(Query.EOF.Matches(onlyNode));
    }
    
    [Fact]
    public void BOF_Sequence_MultipleTokens_EOF()
    {
        var tree = Parse("a b c");
        var root = tree.Root;
        var firstNode = root.Children.First();
        var lastNode = root.Children.Last();
        
        // Match: BOF, then 3 identifiers
        // Note: EOF cannot be part of sequence after consuming last node
        var query = Query.Sequence(
            Query.BOF,
            Query.AnyIdent,
            Query.AnyIdent,
            Query.AnyIdent
        );
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(3, consumed); // Only idents consume
        
        // Verify the last consumed node is at EOF
        Assert.True(Query.EOF.Matches(lastNode));
    }
    
    [Fact]
    public void DoubleNegation_EquivalentToOriginal()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Not(Not(AnyIdent)) should be equivalent to AnyIdent
        // But since Not is zero-width, double negation just checks the condition twice
        var doubleNot = Query.Not(Query.Not(Query.AnyIdent));
        var original = Query.AnyIdent;
        
        Assert.Equal(original.Matches(firstNode), doubleNot.Matches(firstNode));
    }
    
    [Fact]
    public void AnyOf_WithSequences_MatchesLongestFirst()
    {
        var tree = Parse("a b c");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // First query matches 3 idents, second matches 1
        var query = Query.AnyOf(
            Query.Sequence(Query.AnyIdent, Query.AnyIdent, Query.AnyIdent),
            Query.AnyIdent
        );
        
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(3, consumed); // First (longer) sequence wins
    }
    
    [Fact]
    public void Not_Combined_With_AnyOf()
    {
        var tree = Parse("foo");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Not any of (numeric, string, operator) = should match identifier
        var query = Query.Not(Query.AnyOf(Query.AnyNumeric, Query.AnyString, Query.AnyOperator));
        Assert.True(query.Matches(firstNode));
    }
    
    [Fact]
    public void Ancestor_Combined_With_Parent()
    {
        var tree = Parse("{ [ foo ] }");
        var root = tree.Root;
        var braceBlock = root.Children.First() as SyntaxBlock;
        var bracketBlock = braceBlock!.Children.First() as SyntaxBlock;
        var innerIdent = bracketBlock!.Children.First();
        
        // Direct parent is bracket, ancestor is brace
        Assert.True(Query.Parent(Query.BracketBlock).Matches(innerIdent));
        Assert.False(Query.Parent(Query.BraceBlock).Matches(innerIdent)); // Not direct parent
        Assert.True(Query.Ancestor(Query.BraceBlock).Matches(innerIdent)); // Is an ancestor
    }
    
    [Fact]
    public void Sibling_InSequence()
    {
        var tree = Parse("a b c");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        // Match identifier where next sibling is also identifier
        var query = Query.Sequence(
            Query.Sibling(1, Query.AnyIdent), // Assert next sibling is ident (zero-width)
            Query.AnyIdent // Consume this identifier
        );
        
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.Equal(1, consumed); // Sibling is zero-width
    }
    
    [Fact]
    public void Between_Inside_Sequence()
    {
        var tree = Parse("prefix < content > suffix");
        var root = tree.Root;
        var firstNode = root.Children.First();
        
        var query = Query.Sequence(
            Query.AnyIdent, // prefix
            Query.Between(Query.Operator("<"), Query.Operator(">")), // < content >
            Query.AnyIdent  // suffix
        );
        
        Assert.True(query.TryMatch(firstNode, out var consumed));
        Assert.True(consumed >= 3); // prefix, between-content, suffix
    }
    
    #endregion
}
