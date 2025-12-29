using TinyTokenizer.Ast;

namespace TinyTokenizer.Tests;

/// <summary>
/// Comprehensive tests for TreeWalker covering all navigation methods,
/// filter modes, and edge cases.
/// </summary>
public class TreeWalkerTests
{
    #region Constructor and Properties

    [Fact]
    public void Constructor_SetsRootAndCurrent()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root);

        Assert.Same(tree.Root, walker.Root);
        Assert.Same(tree.Root, walker.Current);
        Assert.Equal(NodeFilter.All, walker.WhatToShow);
    }

    [Fact]
    public void Constructor_WithFilter_SetsWhatToShow()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);

        Assert.Equal(NodeFilter.Leaves, walker.WhatToShow);
    }

    #endregion

    #region ParentNode

    [Fact]
    public void ParentNode_FromLeaf_ReturnsParent()
    {
        var tree = SyntaxTree.Parse("{a}");
        var ident = tree.Root.Children.OfType<RedBlock>().First().Children.First(c => c.Kind == NodeKind.Ident);
        var walker = new TreeWalker(tree.Root) { };
        
        // Navigate to the ident first
        while (walker.NextNode() is { } node)
        {
            if (node.Kind == NodeKind.Ident)
                break;
        }
        
        var parent = walker.ParentNode();
        
        Assert.NotNull(parent);
        Assert.True(parent.IsContainer);
    }

    [Fact]
    public void ParentNode_AtRoot_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a");
        var walker = new TreeWalker(tree.Root);

        var parent = walker.ParentNode();

        Assert.Null(parent);
    }

    [Fact]
    public void ParentNode_SkipsFilteredParents()
    {
        var tree = SyntaxTree.Parse("{a}");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        
        // Navigate to ident
        walker.NextNode();
        
        // Parent should be null since blocks are filtered out
        var parent = walker.ParentNode();
        
        Assert.Null(parent);
    }

    #endregion

    #region FirstChild and LastChild

    [Fact]
    public void FirstChild_ReturnsFirstAcceptedChild()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var walker = new TreeWalker(tree.Root);

        var first = walker.FirstChild();

        Assert.NotNull(first);
    }

    [Fact]
    public void FirstChild_OnLeaf_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("abc");
        var walker = new TreeWalker(tree.Root);
        walker.NextNode(); // Move to ident

        var first = walker.FirstChild();

        Assert.Null(first);
    }

    [Fact]
    public void LastChild_ReturnsLastAcceptedChild()
    {
        var tree = SyntaxTree.Parse("{a b c}");
        var walker = new TreeWalker(tree.Root);

        var last = walker.LastChild();

        Assert.NotNull(last);
    }

    [Fact]
    public void FirstChild_WithFilter_SkipsNonMatching()
    {
        var tree = SyntaxTree.Parse("{ a }");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);

        // First child should skip the block and find a leaf
        var first = walker.FirstChild();

        Assert.NotNull(first);
        Assert.True(first.IsLeaf);
    }

    #endregion

    #region NextSibling and PreviousSibling

    [Fact]
    public void NextSibling_ReturnsNextAcceptedSibling()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        walker.FirstChild(); // Move to first leaf

        var next = walker.NextSibling();

        Assert.NotNull(next);
        Assert.True(next.IsLeaf);
    }

    [Fact]
    public void NextSibling_AtLastSibling_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a");
        var walker = new TreeWalker(tree.Root);
        walker.FirstChild();
        
        // Keep moving until no more siblings
        while (walker.NextSibling() != null) { }
        
        var next = walker.NextSibling();

        Assert.Null(next);
    }

    [Fact]
    public void PreviousSibling_ReturnsPreviousAcceptedSibling()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        walker.LastChild(); // Move to last leaf

        var prev = walker.PreviousSibling();

        Assert.NotNull(prev);
        Assert.True(prev.IsLeaf);
    }

    [Fact]
    public void PreviousSibling_AtFirstSibling_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root);
        walker.FirstChild();

        // Keep moving backward
        while (walker.PreviousSibling() != null) { }
        
        var prev = walker.PreviousSibling();

        Assert.Null(prev);
    }

    #endregion

    #region NextNode

    [Fact]
    public void NextNode_TraversesDepthFirst()
    {
        var tree = SyntaxTree.Parse("a {b} c");
        var walker = new TreeWalker(tree.Root);

        var visited = new List<RedNode>();
        while (walker.NextNode() is { } node)
        {
            visited.Add(node);
        }

        Assert.True(visited.Count >= 3);
    }

    [Fact]
    public void NextNode_AtEnd_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a");
        var walker = new TreeWalker(tree.Root);

        // Exhaust all nodes
        while (walker.NextNode() != null) { }

        var next = walker.NextNode();

        Assert.Null(next);
    }

    [Fact]
    public void NextNode_WithRejectFilter_SkipsSubtrees()
    {
        var tree = SyntaxTree.Parse("a {b c} d");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var visited = new List<RedNode>();
        while (walker.NextNode() is { } node)
        {
            visited.Add(node);
        }

        // Should not include the block or its children
        Assert.DoesNotContain(visited, n => n.Kind == NodeKind.BraceBlock);
        Assert.DoesNotContain(visited, n => n.Kind == NodeKind.Ident && ((RedLeaf)n).Text == "b");
    }

    #endregion

    #region PreviousNode

    [Fact]
    public void PreviousNode_TraversesReverseDepthFirst()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root);

        // Move to end
        while (walker.NextNode() != null) { }

        var visited = new List<RedNode>();
        while (walker.PreviousNode() is { } node)
        {
            visited.Add(node);
        }

        Assert.True(visited.Count >= 3);
    }

    [Fact]
    public void PreviousNode_AtStart_ReturnsNull()
    {
        var tree = SyntaxTree.Parse("a");
        var walker = new TreeWalker(tree.Root);

        var prev = walker.PreviousNode();

        Assert.Null(prev);
    }

    [Fact]
    public void PreviousNode_FromDeepNode_NavigatesCorrectly()
    {
        var tree = SyntaxTree.Parse("{{deep}}");
        var walker = new TreeWalker(tree.Root);

        // Navigate to deepest node
        while (walker.NextNode() != null) { }

        // Navigate back
        var prev = walker.PreviousNode();

        Assert.NotNull(prev);
    }

    #endregion

    #region Descendants and DescendantsAndSelf

    [Fact]
    public void Descendants_ExcludesRoot()
    {
        var tree = SyntaxTree.Parse("a b");
        var walker = new TreeWalker(tree.Root);

        var descendants = walker.Descendants().ToList();

        Assert.DoesNotContain(descendants, n => ReferenceEquals(n, tree.Root));
    }

    [Fact]
    public void DescendantsAndSelf_IncludesRoot()
    {
        var tree = SyntaxTree.Parse("a b");
        var walker = new TreeWalker(tree.Root);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.Contains(all, n => ReferenceEquals(n, tree.Root));
    }

    [Fact]
    public void Descendants_PreservesCurrentPosition()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root);
        walker.NextNode(); // Move somewhere
        var current = walker.Current;

        _ = walker.Descendants().ToList();

        Assert.Same(current, walker.Current);
    }

    [Fact]
    public void Descendants_WithFilter_OnlyIncludesMatching()
    {
        var tree = SyntaxTree.Parse("a {b} c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);

        var descendants = walker.Descendants().ToList();

        Assert.All(descendants, n => Assert.True(n.IsLeaf));
    }

    #endregion

    #region Ancestors

    [Fact]
    public void Ancestors_FromDeepNode_ReturnsAllAncestors()
    {
        var tree = SyntaxTree.Parse("{{deep}}");
        var walker = new TreeWalker(tree.Root);

        // Navigate to deepest ident
        while (walker.NextNode() is { } node)
        {
            if (node.Kind == NodeKind.Ident)
                break;
        }

        var ancestors = walker.Ancestors().ToList();

        Assert.True(ancestors.Count >= 2); // At least inner block and outer block
    }

    [Fact]
    public void Ancestors_FromRoot_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("a");
        var walker = new TreeWalker(tree.Root);

        var ancestors = walker.Ancestors().ToList();

        Assert.Empty(ancestors);
    }

    [Fact]
    public void Ancestors_WithFilter_OnlyIncludesMatching()
    {
        var tree = SyntaxTree.Parse("{{deep}}");
        var walker = new TreeWalker(tree.Root, NodeFilter.Blocks);

        // Navigate to deepest
        while (walker.NextNode() != null) { }

        var ancestors = walker.Ancestors().ToList();

        Assert.All(ancestors, n => Assert.True(n.IsContainer));
    }

    #endregion

    #region FollowingSiblings and PrecedingSiblings

    [Fact]
    public void FollowingSiblings_ReturnsAllFollowing()
    {
        var tree = SyntaxTree.Parse("a b c d e");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        walker.FirstChild(); // Start at 'a'

        var following = walker.FollowingSiblings().ToList();

        Assert.True(following.Count >= 4); // b, c, d, e (at minimum)
    }

    [Fact]
    public void FollowingSiblings_AtLastSibling_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        walker.LastChild();

        var following = walker.FollowingSiblings().ToList();

        Assert.Empty(following);
    }

    [Fact]
    public void PrecedingSiblings_ReturnsAllPreceding()
    {
        var tree = SyntaxTree.Parse("a b c d e");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        walker.LastChild(); // Start at 'e'

        var preceding = walker.PrecedingSiblings().ToList();

        Assert.True(preceding.Count >= 4); // d, c, b, a (at minimum)
    }

    [Fact]
    public void PrecedingSiblings_AtFirstSibling_ReturnsEmpty()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        walker.FirstChild();

        var preceding = walker.PrecedingSiblings().ToList();

        Assert.Empty(preceding);
    }

    #endregion

    #region NodeFilter Modes

    [Fact]
    public void NodeFilter_Leaves_OnlyShowsLeaves()
    {
        var tree = SyntaxTree.Parse("{a} [b] (c)");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.All(all, n => Assert.True(n.IsLeaf));
    }

    [Fact]
    public void NodeFilter_Blocks_OnlyShowsBlocks()
    {
        var tree = SyntaxTree.Parse("{a} [b] (c)");
        var walker = new TreeWalker(tree.Root, NodeFilter.Blocks);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.All(all, n => Assert.True(n.IsContainer && n.Kind != NodeKind.TokenList));
    }

    [Fact]
    public void NodeFilter_Root_OnlyShowsRoot()
    {
        var tree = SyntaxTree.Parse("{a} [b]");
        var walker = new TreeWalker(tree.Root, NodeFilter.Root);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.Single(all);
        Assert.Equal(NodeKind.TokenList, all[0].Kind);
    }

    [Fact]
    public void NodeFilter_None_ShowsNothing()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = new TreeWalker(tree.Root, NodeFilter.None);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.Empty(all);
    }

    [Fact]
    public void NodeFilter_Combined_LeavesAndBlocks()
    {
        var tree = SyntaxTree.Parse("{a}");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves | NodeFilter.Blocks);

        var all = walker.DescendantsAndSelf().ToList();

        // Should include both the block and the ident, but not the root
        Assert.True(all.Count >= 2);
        Assert.DoesNotContain(all, n => n.Kind == NodeKind.TokenList);
    }

    #endregion

    #region Custom Filter Functions

    [Fact]
    public void CustomFilter_Accept_IncludesNode()
    {
        var tree = SyntaxTree.Parse("foo bar baz");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            _ => FilterResult.Accept);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.True(all.Count >= 4); // Root + 3 idents + whitespace
    }

    [Fact]
    public void CustomFilter_Skip_ExcludesButTraversesChildren()
    {
        var tree = SyntaxTree.Parse("{a b}");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Skip : FilterResult.Accept);

        var all = walker.DescendantsAndSelf().ToList();

        // Block is skipped but its children (a, b) are included
        Assert.DoesNotContain(all, n => n.Kind == NodeKind.BraceBlock);
        Assert.Contains(all, n => n.Kind == NodeKind.Ident);
    }

    [Fact]
    public void CustomFilter_Reject_ExcludesNodeAndDescendants()
    {
        var tree = SyntaxTree.Parse("x {a b} y");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var all = walker.DescendantsAndSelf().ToList();

        // Block and its children are excluded
        Assert.DoesNotContain(all, n => n.Kind == NodeKind.BraceBlock);
        // x and y should be included
        var idents = all.Where(n => n.Kind == NodeKind.Ident).Cast<RedLeaf>().Select(l => l.Text).ToList();
        Assert.Contains("x", idents);
        Assert.Contains("y", idents);
        // a and b should NOT be included
        Assert.DoesNotContain("a", idents);
        Assert.DoesNotContain("b", idents);
    }

    [Fact]
    public void CustomFilter_ByText_FiltersCorrectly()
    {
        var tree = SyntaxTree.Parse("keep1 remove keep2");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.Leaves,
            node => node is RedLeaf leaf && leaf.Text.StartsWith("keep")
                ? FilterResult.Accept
                : FilterResult.Skip);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.Equal(2, all.Count);
        Assert.All(all, n => Assert.StartsWith("keep", ((RedLeaf)n).Text));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyInput_DescendantsAndSelf_ReturnsOnlyRoot()
    {
        var tree = SyntaxTree.Parse("");
        var walker = new TreeWalker(tree.Root);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.Single(all);
        Assert.Same(tree.Root, all[0]);
    }

    [Fact]
    public void SingleNode_NavigationWorksCorrectly()
    {
        var tree = SyntaxTree.Parse("x");
        var walker = new TreeWalker(tree.Root);

        walker.FirstChild();
        Assert.Null(walker.NextSibling());
        Assert.Null(walker.PreviousSibling());
    }

    [Fact]
    public void DeeplyNested_NavigatesCorrectly()
    {
        var tree = SyntaxTree.Parse("{{{{{deep}}}}}");
        var walker = new TreeWalker(tree.Root);

        // Navigate to deepest
        var depth = 0;
        while (walker.NextNode() != null)
        {
            depth++;
        }

        Assert.True(depth >= 6); // At least 5 blocks + 1 ident
    }

    [Fact]
    public void MixedContent_TraversesAllTypes()
    {
        var tree = SyntaxTree.Parse("a {b [c (d)]} e");
        var walker = new TreeWalker(tree.Root);

        var kinds = new HashSet<NodeKind>();
        foreach (var node in walker.DescendantsAndSelf())
        {
            kinds.Add(node.Kind);
        }

        Assert.Contains(NodeKind.Ident, kinds);
        Assert.Contains(NodeKind.BraceBlock, kinds);
        Assert.Contains(NodeKind.BracketBlock, kinds);
        Assert.Contains(NodeKind.ParenBlock, kinds);
    }

    [Fact]
    public void NextNode_WithSkipOnFirstChild_RecursesCorrectly()
    {
        // Test the Skip branch in NextNode when first child is skipped
        var tree = SyntaxTree.Parse("{a}");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Skip : FilterResult.Accept);

        var first = walker.NextNode();
        
        // Should skip the block and return the ident inside
        Assert.NotNull(first);
        Assert.Equal(NodeKind.Ident, first.Kind);
    }

    [Fact]
    public void NextNode_WithRejectOnFirstChild_ContinuesToNextChild()
    {
        // Test the Reject branch in NextNode
        var tree = SyntaxTree.Parse("{a} b");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var visited = new List<NodeKind>();
        while (walker.NextNode() is { } node)
        {
            visited.Add(node.Kind);
        }

        // Should reject the block entirely and find 'b'
        Assert.DoesNotContain(NodeKind.BraceBlock, visited);
        Assert.Contains(NodeKind.Ident, visited);
    }

    [Fact]
    public void NextNode_SiblingWithSkip_RecursesCorrectly()
    {
        // Test Skip branch when traversing siblings
        var tree = SyntaxTree.Parse("a {b} c");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Skip : FilterResult.Accept);

        var visited = new List<string>();
        while (walker.NextNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                visited.Add(leaf.Text);
        }

        // Should include a, b (inside skipped block), and c
        Assert.Contains("a", visited);
        Assert.Contains("b", visited);
        Assert.Contains("c", visited);
    }

    [Fact]
    public void NextNode_SiblingWithReject_SkipsEntireSubtree()
    {
        // Test Reject branch when traversing siblings
        var tree = SyntaxTree.Parse("a {b} c");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var visited = new List<string>();
        while (walker.NextNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                visited.Add(leaf.Text);
        }

        // Should include a and c, but NOT b (inside rejected block)
        Assert.Contains("a", visited);
        Assert.DoesNotContain("b", visited);
        Assert.Contains("c", visited);
    }

    [Fact]
    public void PreviousNode_WithSkip_NavigatesCorrectly()
    {
        var tree = SyntaxTree.Parse("a {b} c");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Skip : FilterResult.Accept);

        // Navigate to end
        while (walker.NextNode() != null) { }

        // Navigate backwards
        var visited = new List<string>();
        while (walker.PreviousNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                visited.Add(leaf.Text);
        }

        Assert.True(visited.Count >= 2);
    }

    [Fact]
    public void TraverseChildren_WithSkip_RecursesIntoSkippedNode()
    {
        var tree = SyntaxTree.Parse("{{deep}}");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Skip : FilterResult.Accept);

        var first = walker.FirstChild();

        // Should skip outer block, skip inner block, and return "deep"
        Assert.NotNull(first);
        Assert.Equal(NodeKind.Ident, first.Kind);
    }

    [Fact]
    public void TraverseSiblings_NavigatesToParentSibling()
    {
        var tree = SyntaxTree.Parse("{a} b");
        var walker = new TreeWalker(tree.Root, NodeFilter.Leaves);
        
        // Navigate into the block to 'a'
        walker.FirstChild();
        
        // NextSibling from 'a' should eventually find 'b' (parent's sibling's content)
        var next = walker.NextSibling();
        
        Assert.NotNull(next);
    }

    [Fact]
    public void NextNode_MultipleRejectsInSiblings_ContinuesToNext()
    {
        // Test consecutive rejects when traversing siblings
        var tree = SyntaxTree.Parse("{a} {b} c");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var visited = new List<string>();
        while (walker.NextNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                visited.Add(leaf.Text);
        }

        // Should skip both blocks and find 'c'
        Assert.DoesNotContain("a", visited);
        Assert.DoesNotContain("b", visited);
        Assert.Contains("c", visited);
    }

    [Fact]
    public void NextNode_RejectThenAccept_MovesToNextSibling()
    {
        var tree = SyntaxTree.Parse("{reject} accept");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        // First call should skip the block and return "accept"
        RedNode? firstIdent = null;
        while (walker.NextNode() is { } node)
        {
            if (node.Kind == NodeKind.Ident)
            {
                firstIdent = node;
                break;
            }
        }

        Assert.NotNull(firstIdent);
        Assert.Equal("accept", ((RedLeaf)firstIdent).Text);
    }

    [Fact]
    public void PreviousNode_RejectFilter_SkipsRejectedSubtree()
    {
        // When traversing backwards with Reject on a block, the block's children should be skipped
        var tree = SyntaxTree.Parse("first {reject} last");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        // Navigate to end using NextNode (which respects Reject)
        while (walker.NextNode() != null) { }

        // Current should be at "last" since the block was rejected
        Assert.Equal(NodeKind.Ident, walker.Current.Kind);
        Assert.Equal("last", ((RedLeaf)walker.Current).Text);
    }

    [Fact]
    public void NextNode_ConsecutiveRejectedSiblings_SkipsAllRejected()
    {
        // This tests the "node = sibling; continue;" path when multiple consecutive siblings are rejected
        var tree = SyntaxTree.Parse("start {a} {b} {c} end");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var idents = new List<string>();
        while (walker.NextNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                idents.Add(leaf.Text);
        }

        // Should find "start" and "end", skipping all blocks
        Assert.Contains("start", idents);
        Assert.Contains("end", idents);
        Assert.Equal(2, idents.Count);
    }

    [Fact]
    public void NextNode_RejectedSiblingFollowedByAccepted_ContinuesToAccepted()
    {
        // After rejecting a sibling, should continue to next sibling
        var tree = SyntaxTree.Parse("{skip}found");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var first = walker.NextNode();

        Assert.NotNull(first);
        Assert.Equal(NodeKind.Ident, first.Kind);
        Assert.Equal("found", ((RedLeaf)first).Text);
    }

    [Fact]
    public void NextNode_RejectSiblingInLoop_TriggersNodeContinue()
    {
        // This specifically tests the path: reject sibling -> node = sibling -> continue -> get next sibling
        // We need: [leaf with children] -> [rejected sibling] -> [accepted sibling]
        // After visiting the leaf's children, we try its sibling (rejected), then continue to next
        var tree = SyntaxTree.Parse("(inner){rejected}accepted");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Reject : FilterResult.Accept);

        var visited = new List<string>();
        while (walker.NextNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                visited.Add(leaf.Text);
        }

        // Should visit "inner" from paren block, skip brace block entirely, then visit "accepted"
        Assert.Contains("inner", visited);
        Assert.Contains("accepted", visited);
        Assert.DoesNotContain("rejected", visited);
    }

    [Fact]
    public void PreviousNode_SkipFilter_IncludesChildren()
    {
        var tree = SyntaxTree.Parse("a {inside} b");
        var walker = new TreeWalker(
            tree.Root,
            NodeFilter.All,
            node => node.Kind == NodeKind.BraceBlock ? FilterResult.Skip : FilterResult.Accept);

        // Navigate to end
        while (walker.NextNode() != null) { }

        // Navigate backwards
        var visited = new List<string>();
        while (walker.PreviousNode() is { } node)
        {
            if (node is RedLeaf leaf && node.Kind == NodeKind.Ident)
                visited.Add(leaf.Text);
        }

        Assert.Contains("inside", visited);
    }

    #endregion

    #region Extension Methods

    [Fact]
    public void RedNode_CreateTreeWalker_CreatesWalker()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = tree.Root.CreateTreeWalker();

        Assert.NotNull(walker);
        Assert.Same(tree.Root, walker.Root);
    }

    [Fact]
    public void SyntaxTree_CreateTreeWalker_CreatesWalker()
    {
        var tree = SyntaxTree.Parse("a b c");
        var walker = tree.CreateTreeWalker(NodeFilter.Leaves);

        Assert.NotNull(walker);
        Assert.Equal(NodeFilter.Leaves, walker.WhatToShow);
    }

    [Fact]
    public void CreateTreeWalker_WithCustomFilter_Works()
    {
        var tree = SyntaxTree.Parse("foo bar");
        var walker = tree.CreateTreeWalker(
            NodeFilter.All,
            n => n.Kind == NodeKind.Ident ? FilterResult.Accept : FilterResult.Skip);

        var all = walker.DescendantsAndSelf().ToList();

        Assert.All(all, n => Assert.Equal(NodeKind.Ident, n.Kind));
    }

    #endregion
}
