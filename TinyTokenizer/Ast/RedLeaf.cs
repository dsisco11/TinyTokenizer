using System.Collections.Immutable;
using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Red node wrapper for leaf tokens.
/// Provides position-aware access to token text and trivia.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class RedLeaf : SyntaxNode
{
    /// <inheritdoc/>
    protected override string DebuggerDisplay =>
        $"{Kind}[{Position}..{EndPosition}] \"{Truncate(Text, 20)}\"";

    /// <summary>
    /// Creates a new red leaf wrapping a green leaf.
    /// </summary>
    internal RedLeaf(GreenLeaf green, SyntaxNode? parent, int position, int siblingIndex = -1, SyntaxTree? tree = null)
        : base(green, parent, position, siblingIndex, tree)
    {
    }
    
    /// <summary>The underlying green leaf.</summary>
    internal new GreenLeaf Green => (GreenLeaf)base.Green;
    
    /// <summary>The token text (excluding trivia).</summary>
    public string Text => Green.Text;
    
    /// <summary>The token text as a span.</summary>
    public ReadOnlySpan<char> TextSpan => Green.Text.AsSpan();
    
    /// <summary>Leading trivia attached to this token (internal access).</summary>
    internal ImmutableArray<GreenTrivia> GreenLeadingTrivia => Green.LeadingTrivia;
    
    /// <summary>Trailing trivia attached to this token (internal access).</summary>
    internal ImmutableArray<GreenTrivia> GreenTrailingTrivia => Green.TrailingTrivia;
    
    /// <summary>
    /// Gets the leading trivia attached to this token.
    /// Leading trivia appears before the token text (e.g., indentation, comments on previous line).
    /// </summary>
    /// <returns>An enumerable of trivia items before this token.</returns>
    /// <example>
    /// <code>
    /// var tree = SyntaxTree.Parse("  x + y");
    /// var xToken = tree.Leaves.First();
    /// foreach (var trivia in xToken.GetLeadingTrivia())
    /// {
    ///     Console.WriteLine($"{trivia.Kind}: '{trivia.Text}'"); // Whitespace: '  '
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="GetTrailingTrivia"/>
    /// <seealso cref="HasLeadingTrivia"/>
    public IEnumerable<Trivia> GetLeadingTrivia()
    {
        foreach (var green in Green.LeadingTrivia)
        {
            yield return new Trivia(green);
        }
    }
    
    /// <summary>
    /// Gets the trailing trivia attached to this token.
    /// Trailing trivia appears after the token text (e.g., end-of-line comments).
    /// </summary>
    /// <returns>An enumerable of trivia items after this token.</returns>
    /// <example>
    /// <code>
    /// var tree = SyntaxTree.Parse("x // comment\n");
    /// var xToken = tree.Leaves.First();
    /// foreach (var trivia in xToken.GetTrailingTrivia())
    /// {
    ///     Console.WriteLine($"{trivia.Kind}: '{trivia.Text}'");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="GetLeadingTrivia"/>
    /// <seealso cref="HasTrailingTrivia"/>
    public IEnumerable<Trivia> GetTrailingTrivia()
    {
        foreach (var green in Green.TrailingTrivia)
        {
            yield return new Trivia(green);
        }
    }
    
    /// <summary>
    /// Gets whether this token has any leading trivia.
    /// </summary>
    public bool HasLeadingTrivia => !Green.LeadingTrivia.IsEmpty;
    
    /// <summary>
    /// Gets whether this token has any trailing trivia.
    /// </summary>
    public bool HasTrailingTrivia => !Green.TrailingTrivia.IsEmpty;
    
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
    public override SyntaxNode? GetChild(int index) => null; // Leaves have no children
}
