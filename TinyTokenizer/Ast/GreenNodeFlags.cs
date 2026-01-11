using System;

namespace TinyTokenizer.Ast;

/// <summary>
/// Bitflags describing trivia and content properties of a green node.
/// Intended for O(1) query checks and subtree pruning.
/// </summary>
[Flags]
internal enum GreenNodeFlags : uint
{
    None = 0,

    // Boundary trivia (left/right edge of the node's text span)
    HasLeadingNewlineTrivia = 1u << 0,
    HasTrailingNewlineTrivia = 1u << 1,

    HasLeadingWhitespaceTrivia = 1u << 2,
    HasTrailingWhitespaceTrivia = 1u << 3,

    HasLeadingCommentTrivia = 1u << 4,
    HasTrailingCommentTrivia = 1u << 5,

    // Subtree flags (anywhere within the node's subtree)
    ContainsNewlineTrivia = 1u << 8,
    ContainsWhitespaceTrivia = 1u << 9,
    ContainsCommentTrivia = 1u << 10,

    ContainsErrorNode = 1u << 11,
    ContainsKeyword = 1u << 12,
    ContainsTaggedIdent = 1u << 13,
}
