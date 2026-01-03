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
    {
        Kind = kind;
        Text = text;
        LeadingTrivia = leadingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : leadingTrivia;
        TrailingTrivia = trailingTrivia.IsDefault ? ImmutableArray<GreenTrivia>.Empty : trailingTrivia;
        
        LeadingTriviaWidth = ComputeTriviaWidth(LeadingTrivia);
        TrailingTriviaWidth = ComputeTriviaWidth(TrailingTrivia);
        _width = LeadingTriviaWidth + Text.Length + TrailingTriviaWidth;
    }
    
    /// <inheritdoc/>
    public override GreenNode? GetSlot(int index) => null;
    
    /// <inheritdoc/>
    public override RedNode CreateRed(RedNode? parent, int position)
        => new RedLeaf(this, parent, position);
    
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
    
    private static int ComputeTriviaWidth(ImmutableArray<GreenTrivia> trivia)
    {
        int width = 0;
        foreach (var t in trivia)
            width += t.Width;
        return width;
    }
}
