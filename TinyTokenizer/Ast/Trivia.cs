using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Represents trivia (whitespace, newlines, comments) attached to a token.
/// Trivia does not affect semantics but preserves formatting information.
/// </summary>
/// <remarks>
/// <para>
/// Trivia is attached to tokens (leaves) in the syntax tree. Each token can have
/// leading trivia (before the token text) and trailing trivia (after the token text).
/// </para>
/// <para>
/// Common trivia kinds include:
/// <list type="bullet">
///   <item><description><see cref="TriviaKind.Whitespace"/> - spaces, tabs</description></item>
///   <item><description><see cref="TriviaKind.Newline"/> - line breaks (\n, \r\n)</description></item>
///   <item><description><see cref="TriviaKind.SingleLineComment"/> - e.g., // comment</description></item>
///   <item><description><see cref="TriviaKind.MultiLineComment"/> - e.g., /* comment */</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var tree = SyntaxTree.Parse("  x + y // comment\n");
/// foreach (var leaf in tree.Leaves)
/// {
///     foreach (var trivia in leaf.GetLeadingTrivia())
///     {
///         Console.WriteLine($"{trivia.Kind}: '{trivia.Text}'");
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="TriviaKind"/>
/// <seealso cref="RedLeaf.GetLeadingTrivia"/>
/// <seealso cref="RedLeaf.GetTrailingTrivia"/>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Trivia : IEquatable<Trivia>
{
    private readonly GreenTrivia _green;
    
    /// <summary>
    /// Creates a new trivia instance wrapping the internal green trivia.
    /// </summary>
    internal Trivia(GreenTrivia green)
    {
        _green = green;
    }
    
    /// <summary>
    /// Gets the kind of this trivia.
    /// </summary>
    public TriviaKind Kind => _green.Kind;
    
    /// <summary>
    /// Gets the text content of this trivia.
    /// </summary>
    public string Text => _green.Text;
    
    /// <summary>
    /// Gets the character width (length) of this trivia.
    /// </summary>
    public int Width => _green.Width;
    
    /// <summary>
    /// Gets whether this trivia represents whitespace (spaces, tabs).
    /// </summary>
    public bool IsWhitespace => Kind == TriviaKind.Whitespace;
    
    /// <summary>
    /// Gets whether this trivia represents a newline.
    /// </summary>
    public bool IsNewline => Kind == TriviaKind.Newline;
    
    /// <summary>
    /// Gets whether this trivia represents a comment (single-line or multi-line).
    /// </summary>
    public bool IsComment => Kind == TriviaKind.SingleLineComment || Kind == TriviaKind.MultiLineComment;
    
    /// <summary>
    /// Gets the debugger display string.
    /// </summary>
    private string DebuggerDisplay => $"{Kind}[{Width}] \"{Truncate(Text, 20)}\"";
    
    /// <summary>
    /// Truncates and escapes text for display.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        var escaped = text.Replace('"', '\'').Replace("\n", "\\n").Replace("\r", "\\r");
        if (escaped.Length <= maxLength)
            return escaped;
        return escaped[..maxLength] + "...";
    }
    
    /// <inheritdoc/>
    public override string ToString() => Text;
    
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, Text);
    
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Trivia other && Equals(other);
    
    /// <inheritdoc/>
    public bool Equals(Trivia other) => Kind == other.Kind && Text == other.Text;
    
    /// <summary>
    /// Determines whether two trivia instances are equal.
    /// </summary>
    public static bool operator ==(Trivia left, Trivia right) => left.Equals(right);
    
    /// <summary>
    /// Determines whether two trivia instances are not equal.
    /// </summary>
    public static bool operator !=(Trivia left, Trivia right) => !left.Equals(right);
}
