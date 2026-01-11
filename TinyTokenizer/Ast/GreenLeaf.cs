using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace TinyTokenizer.Ast;

/// <summary>
/// Green node for leaf tokens (identifiers, operators, strings, etc.).
/// Stores the token text and attached trivia. Has no children.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed record GreenLeaf : GreenNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Width}] \"{Truncate(Text, 20)}\"";

    private readonly int _width;
    
    /// <inheritdoc/>
    public override NodeKind Kind { get; }
    
    /// <summary>The text content of this token (excluding trivia).</summary>
    public string Text { get; }
    
    /// <summary>Trivia appearing before this token.</summary>
    public ImmutableArray<GreenTrivia> LeadingTrivia { get; }
    
    /// <summary>Trivia appearing after this token.</summary>
    public ImmutableArray<GreenTrivia> TrailingTrivia { get; }
    
    /// <inheritdoc/>
    public override int Width => _width;
    
    /// <inheritdoc/>
    public override int SlotCount => 0;
    
    /// <summary>Width of the token text only (excluding trivia).</summary>
    public int TextWidth => Text.Length;
    
    /// <summary>Total width of leading trivia.</summary>
    public int LeadingTriviaWidth { get; }
    
    /// <summary>Total width of trailing trivia.</summary>
    public int TrailingTriviaWidth { get; }
    
    /// <summary>Offset from node start to the actual token text.</summary>
    public int TextOffset => LeadingTriviaWidth;
    
    /// <summary>
    /// Creates a new leaf node with optional trivia.
    /// </summary>
    public GreenLeaf(
        NodeKind kind,
        string text,
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default)
        : this(
            kind,
            text,
            leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia,
            trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia,
            Compute(kind,
                text,
                leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia,
                trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia))
    {
    }

    private GreenLeaf(
        NodeKind kind,
        string text,
        ImmutableArray<GreenTrivia> leadingTrivia,
        ImmutableArray<GreenTrivia> trailingTrivia,
        LeafComputed computed)
        : base(computed.Flags)
    {
        Kind = kind;
        Text = text;
        LeadingTrivia = leadingTrivia;
        TrailingTrivia = trailingTrivia;

        LeadingTriviaWidth = computed.LeadingTriviaWidth;
        TrailingTriviaWidth = computed.TrailingTriviaWidth;
        _width = computed.Width;
    }
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index) => null;
    
    /// <inheritdoc/>
    public override SyntaxNode CreateRed(SyntaxNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        => new SyntaxToken(this, parent, position, siblingIndex, tree);
    
    /// <inheritdoc/>
    public override void WriteTo(IBufferWriter<char> writer)
    {
        foreach (var trivia in LeadingTrivia)
        {
            var span = writer.GetSpan(trivia.Width);
            trivia.Text.AsSpan().CopyTo(span);
            writer.Advance(trivia.Width);
        }
        
        var textSpan = writer.GetSpan(Text.Length);
        Text.AsSpan().CopyTo(textSpan);
        writer.Advance(Text.Length);
        
        foreach (var trivia in TrailingTrivia)
        {
            var span = writer.GetSpan(trivia.Width);
            trivia.Text.AsSpan().CopyTo(span);
            writer.Advance(trivia.Width);
        }
    }
    
    /// <summary>
    /// Creates a new leaf with different leading trivia.
    /// </summary>
    public GreenLeaf WithLeadingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Kind, Text, trivia, TrailingTrivia);
    
    /// <summary>
    /// Creates a new leaf with different trailing trivia.
    /// </summary>
    public GreenLeaf WithTrailingTrivia(ImmutableArray<GreenTrivia> trivia)
        => new(Kind, Text, LeadingTrivia, trivia);
    
    /// <summary>
    /// Creates a new leaf with different text (preserving trivia).
    /// </summary>
    public GreenLeaf WithText(string text)
        => new(Kind, text, LeadingTrivia, TrailingTrivia);

    private static LeafComputed Compute(
        NodeKind kind,
        string text,
        ImmutableArray<GreenTrivia> leadingTrivia,
        ImmutableArray<GreenTrivia> trailingTrivia)
    {
        int leadingWidth = ComputeTriviaWidthAndFlags(
            leadingTrivia,
            isLeading: true,
            out var leadingBoundaryFlags,
            out var leadingContainsFlags);

        int trailingWidth = ComputeTriviaWidthAndFlags(
            trailingTrivia,
            isLeading: false,
            out var trailingBoundaryFlags,
            out var trailingContainsFlags);

        int width = leadingWidth + text.Length + trailingWidth;

        // Subtree flags based on node kind
        var kindFlags = GreenNodeFlags.None;
        if (kind == NodeKind.Error)
            kindFlags |= GreenNodeFlags.ContainsErrorNode;
        if (kind == NodeKind.TaggedIdent)
            kindFlags |= GreenNodeFlags.ContainsTaggedIdent;
        if (kind.IsKeyword())
            kindFlags |= GreenNodeFlags.ContainsKeyword;

        var flags = leadingBoundaryFlags | trailingBoundaryFlags | leadingContainsFlags | trailingContainsFlags | kindFlags;
        return new LeafComputed(leadingWidth, trailingWidth, width, flags);
    }

    private readonly record struct LeafComputed(
        int LeadingTriviaWidth,
        int TrailingTriviaWidth,
        int Width,
        GreenNodeFlags Flags);
    
    private static int ComputeTriviaWidthAndFlags(
        ImmutableArray<GreenTrivia> trivia,
        bool isLeading,
        out GreenNodeFlags boundaryFlags,
        out GreenNodeFlags containsFlags)
    {
        boundaryFlags = GreenNodeFlags.None;
        containsFlags = GreenNodeFlags.None;

        int width = 0;
        bool hasNewline = false;
        bool hasWhitespace = false;
        bool hasComment = false;

        foreach (var t in trivia)
        {
            width += t.Width;
            switch (t.Kind)
            {
                case TriviaKind.Newline:
                    hasNewline = true;
                    break;
                case TriviaKind.Whitespace:
                    hasWhitespace = true;
                    break;
                case TriviaKind.SingleLineComment:
                case TriviaKind.MultiLineComment:
                    hasComment = true;
                    break;
            }
        }

        if (hasNewline)
        {
            boundaryFlags |= isLeading ? GreenNodeFlags.HasLeadingNewlineTrivia : GreenNodeFlags.HasTrailingNewlineTrivia;
            containsFlags |= GreenNodeFlags.ContainsNewlineTrivia;
        }

        if (hasWhitespace)
        {
            boundaryFlags |= isLeading ? GreenNodeFlags.HasLeadingWhitespaceTrivia : GreenNodeFlags.HasTrailingWhitespaceTrivia;
            containsFlags |= GreenNodeFlags.ContainsWhitespaceTrivia;
        }

        if (hasComment)
        {
            boundaryFlags |= isLeading ? GreenNodeFlags.HasLeadingCommentTrivia : GreenNodeFlags.HasTrailingCommentTrivia;
            containsFlags |= GreenNodeFlags.ContainsCommentTrivia;
        }

        return width;
    }
}
