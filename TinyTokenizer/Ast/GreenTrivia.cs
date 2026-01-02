using System.Diagnostics;

namespace TinyTokenizer.Ast;

/// <summary>
/// Represents trivia (whitespace, newlines, comments) attached to a token.
/// Trivia does not affect semantics but preserves formatting information.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal readonly record struct GreenTrivia
{
    /// <summary>The kind of trivia.</summary>
    public TriviaKind Kind { get; }
    
    /// <summary>The text content of the trivia.</summary>
    public string Text { get; }
    
    /// <summary>The character width of this trivia.</summary>
    public int Width => Text.Length;

    /// <summary>
    /// Gets the debugger display string for this trivia.
    /// </summary>
    private string DebuggerDisplay => $"{Kind}[{Width}] \"{Truncate(Text, 20)}\"";

    /// <summary>
    /// Truncates a string to the specified length, replacing quotes with single quotes.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        var escaped = text.Replace('"', '\'').Replace("\n", "\\n").Replace("\r", "\\r");
        if (escaped.Length <= maxLength)
            return escaped;
        return escaped[..maxLength] + "...";
    }

    
    /// <summary>
    /// Creates a new trivia instance.
    /// </summary>
    public GreenTrivia(TriviaKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }
    
    /// <summary>Creates whitespace trivia.</summary>
    public static GreenTrivia Whitespace(string text) => new(TriviaKind.Whitespace, text);
    
    /// <summary>Creates newline trivia.</summary>
    public static GreenTrivia Newline(string text) => new(TriviaKind.Newline, text);
    
    /// <summary>Creates single-line comment trivia.</summary>
    public static GreenTrivia SingleLineComment(string text) => new(TriviaKind.SingleLineComment, text);
    
    /// <summary>Creates multi-line comment trivia.</summary>
    public static GreenTrivia MultiLineComment(string text) => new(TriviaKind.MultiLineComment, text);
}

/// <summary>
/// The kind of trivia. Only whitespace, newlines, and comments are trivia.
/// Directives and other structured content are regular nodes.
/// </summary>
public enum TriviaKind
{
    /// <summary>Whitespace characters (spaces, tabs) excluding newlines.</summary>
    Whitespace,
    
    /// <summary>Newline characters (\n, \r\n).</summary>
    Newline,
    
    /// <summary>Single-line comment (e.g., // comment).</summary>
    SingleLineComment,
    
    /// <summary>Multi-line comment (e.g., /* comment */).</summary>
    MultiLineComment,
}
