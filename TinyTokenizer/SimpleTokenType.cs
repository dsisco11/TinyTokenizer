namespace TinyTokenizer;

/// <summary>
/// Defines the types of simple tokens produced by the <see cref="Lexer"/>.
/// These are atomic tokens that never require backtracking or have failure conditions.
/// </summary>
public enum SimpleTokenType
{
    /// <summary>
    /// A sequence of identifier characters (letters, digits, underscores, etc.).
    /// </summary>
    Ident,

    /// <summary>
    /// A sequence of whitespace characters (spaces, tabs) excluding newlines.
    /// </summary>
    Whitespace,

    /// <summary>
    /// A sequence of consecutive digit characters (0-9).
    /// Level 2 (TokenParser) combines Digits + Dot + Digits into NumericToken.
    /// </summary>
    Digits,

    /// <summary>
    /// A single symbol character from the configured symbol set.
    /// </summary>
    Symbol,

    /// <summary>
    /// Dot/period character: .
    /// Level 2 (TokenParser) uses this to detect decimal numbers.
    /// </summary>
    Dot,

    /// <summary>
    /// A newline character or sequence (\n or \r\n).
    /// Separate from Whitespace to enable single-line comment detection.
    /// </summary>
    Newline,

    /// <summary>
    /// Opening brace: {
    /// </summary>
    OpenBrace,

    /// <summary>
    /// Closing brace: }
    /// </summary>
    CloseBrace,

    /// <summary>
    /// Opening bracket: [
    /// </summary>
    OpenBracket,

    /// <summary>
    /// Closing bracket: ]
    /// </summary>
    CloseBracket,

    /// <summary>
    /// Opening parenthesis: (
    /// </summary>
    OpenParen,

    /// <summary>
    /// Closing parenthesis: )
    /// </summary>
    CloseParen,

    /// <summary>
    /// Single quote character: '
    /// </summary>
    SingleQuote,

    /// <summary>
    /// Double quote character: "
    /// </summary>
    DoubleQuote,

    /// <summary>
    /// Backslash character: \
    /// Used for escape sequence detection in Level 2.
    /// </summary>
    Backslash,

    /// <summary>
    /// Forward slash character: /
    /// Used for comment detection (// and /*) in Level 2.
    /// </summary>
    Slash,

    /// <summary>
    /// Asterisk character: *
    /// Used for comment detection (/* and */) in Level 2.
    /// </summary>
    Asterisk,

    /// <summary>
    /// Hash/pound character: #
    /// Used for directive detection in Level 2.
    /// </summary>
    Hash,

    /// <summary>
    /// At sign: @
    /// </summary>
    At,

    /// <summary>
    /// Equals sign: =
    /// </summary>
    Equals,

    /// <summary>
    /// Plus sign: +
    /// </summary>
    Plus,

    /// <summary>
    /// Minus/hyphen: -
    /// </summary>
    Minus,

    /// <summary>
    /// Less than: &lt;
    /// </summary>
    LessThan,

    /// <summary>
    /// Greater than: &gt;
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Pipe/vertical bar: |
    /// </summary>
    Pipe,

    /// <summary>
    /// Ampersand: &amp;
    /// </summary>
    Ampersand,

    /// <summary>
    /// Percent sign: %
    /// </summary>
    Percent,

    /// <summary>
    /// Caret: ^
    /// </summary>
    Caret,

    /// <summary>
    /// Tilde: ~
    /// </summary>
    Tilde,

    /// <summary>
    /// Question mark: ?
    /// </summary>
    Question,

    /// <summary>
    /// Exclamation mark: !
    /// </summary>
    Exclamation,

    /// <summary>
    /// Colon: :
    /// </summary>
    Colon,

    /// <summary>
    /// Comma: ,
    /// </summary>
    Comma,

    /// <summary>
    /// Semicolon: ;
    /// </summary>
    Semicolon,

    /// <summary>
    /// [Obsolete] Use <see cref="Digits"/> instead.
    /// Kept for backwards compatibility.
    /// </summary>
    [Obsolete("Use Digits instead. Numeric is now handled by TokenParser combining Digits + Dot + Digits.")]
    Numeric = Digits,
}
