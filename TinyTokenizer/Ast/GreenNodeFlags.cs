using System;

namespace TinyTokenizer.Ast;

/// <summary>
/// Bitflags describing trivia and content properties of a green node.
/// Intended for O(1) query checks and subtree pruning.
/// </summary>
/// <remarks>
/// <para>
/// These flags intentionally distinguish between <b>boundary trivia</b> and <b>subtree contains</b>.
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>Boundary flags</b> (<see cref="GreenNodeFlags.HasLeadingNewlineTrivia"/>,
///     <see cref="GreenNodeFlags.HasTrailingNewlineTrivia"/>, etc.) mean the node itself
///     <em>owns</em> trivia on its boundary. For token-centric newline queries, this is the only
///     correct interpretation.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Contains flags</b> (<see cref="GreenNodeFlags.ContainsNewlineTrivia"/>, etc.) mean the
///     trivia exists somewhere within the node's subtree (including children).
///     </description>
///   </item>
/// </list>
/// <para>
/// Important: boundary flags MUST NOT be used to represent "first child has boundary trivia".
/// Container nodes should not automatically inherit boundary flags from their first/last child.
/// </para>
/// </remarks>
[Flags]
internal enum GreenNodeFlags : uint
{
    None = 0,

    // Boundary trivia (owned by this node's boundary)
    //
    // For leaves: these correspond to the leaf's own leading/trailing trivia.
    // For blocks: these correspond to opener leading and closer trailing trivia.
    // For containers/lists: these should remain None unless the container explicitly owns trivia.
    HasLeadingNewlineTrivia = 1u << 0,
    HasTrailingNewlineTrivia = 1u << 1,

    HasLeadingWhitespaceTrivia = 1u << 2,
    HasTrailingWhitespaceTrivia = 1u << 3,

    HasLeadingCommentTrivia = 1u << 4,
    HasTrailingCommentTrivia = 1u << 5,

    // Subtree flags (anywhere within the node's subtree, including children)
    ContainsNewlineTrivia = 1u << 8,
    ContainsWhitespaceTrivia = 1u << 9,
    ContainsCommentTrivia = 1u << 10,

    ContainsErrorNode = 1u << 11,
    ContainsKeyword = 1u << 12,
    ContainsTaggedIdent = 1u << 13,
}

internal static class GreenNodeFlagMasks
{
    public const GreenNodeFlags LeadingBoundary =
        GreenNodeFlags.HasLeadingNewlineTrivia |
        GreenNodeFlags.HasLeadingWhitespaceTrivia |
        GreenNodeFlags.HasLeadingCommentTrivia;

    public const GreenNodeFlags TrailingBoundary =
        GreenNodeFlags.HasTrailingNewlineTrivia |
        GreenNodeFlags.HasTrailingWhitespaceTrivia |
        GreenNodeFlags.HasTrailingCommentTrivia;

    public const GreenNodeFlags Boundary = LeadingBoundary | TrailingBoundary;

    public const GreenNodeFlags Contains =
        GreenNodeFlags.ContainsNewlineTrivia |
        GreenNodeFlags.ContainsWhitespaceTrivia |
        GreenNodeFlags.ContainsCommentTrivia |
        GreenNodeFlags.ContainsErrorNode |
        GreenNodeFlags.ContainsKeyword |
        GreenNodeFlags.ContainsTaggedIdent;
}
