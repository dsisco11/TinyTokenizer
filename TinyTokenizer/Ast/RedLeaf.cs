using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Red node wrapper for leaf tokens.
/// Provides position-aware access to token text and trivia.
/// </summary>
public sealed class RedLeaf : RedNode
{
    /// <summary>
    /// Creates a new red leaf wrapping a green leaf.
    /// </summary>
    public RedLeaf(GreenLeaf green, RedNode? parent, int position)
        : base(green, parent, position)
    {
    }
    
    /// <summary>The underlying green leaf.</summary>
    public new GreenLeaf Green => (GreenLeaf)base.Green;
    
    /// <summary>The token text (excluding trivia).</summary>
    public string Text => Green.Text;
    
    /// <summary>The token text as a span.</summary>
    public ReadOnlySpan<char> TextSpan => Green.Text.AsSpan();
    
    /// <summary>Leading trivia attached to this token.</summary>
    public ImmutableArray<GreenTrivia> LeadingTrivia => Green.LeadingTrivia;
    
    /// <summary>Trailing trivia attached to this token.</summary>
    public ImmutableArray<GreenTrivia> TrailingTrivia => Green.TrailingTrivia;
    
    /// <summary>Width of the token text only.</summary>
    public int TextWidth => Green.TextWidth;
    
    /// <summary>Width of leading trivia.</summary>
    public int LeadingTriviaWidth => Green.LeadingTriviaWidth;
    
    /// <summary>Width of trailing trivia.</summary>
    public int TrailingTriviaWidth => Green.TrailingTriviaWidth;
    
    /// <summary>
    /// Absolute position of the token text (after leading trivia).
    /// Use this for "where does the actual token start".
    /// </summary>
    public int TextPosition => Position + Green.LeadingTriviaWidth;
    
    /// <summary>
    /// End position of the token text (before trailing trivia).
    /// </summary>
    public int TextEndPosition => TextPosition + Green.TextWidth;
    
    /// <summary>
    /// Full span start (same as Position, includes leading trivia).
    /// </summary>
    public int FullSpanStart => Position;
    
    /// <summary>
    /// Full span end (includes trailing trivia).
    /// </summary>
    public int FullSpanEnd => EndPosition;
    
    /// <inheritdoc/>
    public override RedNode? GetChild(int index) => null; // Leaves have no children
}
