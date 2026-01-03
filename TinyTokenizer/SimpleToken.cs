using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

namespace TinyTokenizer;

/// <summary>
/// Represents an atomic token produced by the <see cref="Lexer"/>.
/// Simple tokens never require backtracking and have no failure conditions.
/// </summary>
/// <param name="Type">The type of this token.</param>
/// <param name="Content">The character content of this token.</param>
/// <param name="Position">The absolute position in the source where this token starts. Limited to ~2GB file size.</param>
/// <remarks>
/// Position uses <c>int</c> rather than <c>long</c> for memory efficiency and alignment with
/// .NET's string and array length limits. This supports files up to ~2GB in size.
/// </remarks>
public readonly record struct SimpleToken(
    SimpleTokenType Type,
    ReadOnlyMemory<char> Content,
    int Position
) : ITextSerializable,
    IParsable<SimpleToken>,
    ISpanParsable<SimpleToken>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    #region Properties

    /// <summary>
    /// Gets the content as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public ReadOnlySpan<char> ContentSpan => Content.Span;

    /// <summary>
    /// Gets the length of the content.
    /// </summary>
    public int Length => Content.Length;

    /// <summary>
    /// Gets whether this token represents a single character.
    /// </summary>
    public bool IsSingleChar => Content.Length == 1;

    /// <summary>
    /// Gets the first character of the content, or '\0' if empty.
    /// </summary>
    public char FirstChar => Content.Length > 0 ? Content.Span[0] : '\0';

    #endregion

    #region Factory Methods

    /// <summary>
    /// Creates a simple token from a single character.
    /// </summary>
    public static SimpleToken FromChar(SimpleTokenType type, char c, int position)
    {
        return new SimpleToken(type, new[] { c }.AsMemory(), position);
    }

    /// <summary>
    /// Creates a simple token from a string.
    /// </summary>
    public static SimpleToken FromString(SimpleTokenType type, string content, int position)
    {
        return new SimpleToken(type, content.AsMemory(), position);
    }

    #endregion

    #region IParsable<SimpleToken>

    /// <summary>
    /// Parses a string into a <see cref="SimpleToken"/>.
    /// Format: "Type:Content@Position" (e.g., "Text:hello@0")
    /// </summary>
    public static SimpleToken Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"Invalid SimpleToken format: '{s}'");
        }
        
        return result;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="SimpleToken"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SimpleToken result)
    {
        result = default;
        
        if (string.IsNullOrEmpty(s))
            return false;

        return TryParse(s.AsSpan(), provider, out result);
    }

    #endregion

    #region ISpanParsable<SimpleToken>

    /// <summary>
    /// Parses a span into a <see cref="SimpleToken"/>.
    /// </summary>
    public static SimpleToken Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"Invalid SimpleToken format: '{s.ToString()}'");
        }
        
        return result;
    }

    /// <summary>
    /// Tries to parse a span into a <see cref="SimpleToken"/>.
    /// Format: "Type:Content@Position" (e.g., "Text:hello@0")
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SimpleToken result)
    {
        result = default;
        
        if (s.IsEmpty)
            return false;

        // Find the first colon (Type separator)
        int colonIndex = s.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        // Find the @ symbol (Position separator)
        int atIndex = s.LastIndexOf('@');
        if (atIndex <= colonIndex)
            return false;

        // Parse type
        var typeSpan = s[..colonIndex];
        if (!Enum.TryParse<SimpleTokenType>(typeSpan.ToString(), ignoreCase: true, out var type))
            return false;

        // Parse position
        var positionSpan = s[(atIndex + 1)..];
        if (!int.TryParse(positionSpan, NumberStyles.Integer, provider, out var position))
            return false;

        // Extract content (between colon and @)
        var contentSpan = s[(colonIndex + 1)..atIndex];
        var content = contentSpan.ToString();

        result = new SimpleToken(type, content.AsMemory(), position);
        return true;
    }

    #endregion

    #region IFormattable

    /// <summary>
    /// Formats the token as a string.
    /// </summary>
    /// <param name="format">
    /// Format string:
    /// - null or "D": Debug format "Type@Position"
    /// - "G": General format "Type:Content@Position"
    /// - "S": Short format "Type@Position"
    /// - "C": Content only
    /// - "T": Type only
    /// </param>
    /// <param name="formatProvider">Format provider.</param>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return format?.ToUpperInvariant() switch
        {
            null or "" or "D" => $"{Type}@{Position}",
            "G" => $"{Type}:{Content}@{Position}",
            "S" => $"{Type}@{Position}",
            "C" => Content.ToString(),
            "T" => Type.ToString(),
            _ => throw new FormatException($"Unknown format string: '{format}'")
        };
    }

    /// <summary>
    /// Returns a debug representation of this token.
    /// Use <see cref="ToText"/> to get the serialized text content.
    /// </summary>
    public override string ToString() => ToString(null, null);

    #endregion

    #region ITextSerializable

    /// <inheritdoc />
    public void WriteTo(IBufferWriter<char> writer)
    {
        var span = writer.GetSpan(Content.Length);
        Content.Span.CopyTo(span);
        writer.Advance(Content.Length);
    }

    /// <inheritdoc />
    public string ToText() => new(Content.Span);

    /// <inheritdoc />
    public void WriteTo(StringBuilder builder) => builder.Append(Content.Span);

    /// <inheritdoc />
    public void WriteTo(TextWriter writer) => writer.Write(Content.Span);

    /// <inheritdoc />
    public bool TryWriteTo(Span<char> destination, out int charsWritten)
    {
        if (Content.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        Content.Span.CopyTo(destination);
        charsWritten = Content.Length;
        return true;
    }

    /// <inheritdoc />
    public int TextLength => Content.Length;

    #endregion

    #region ISpanFormattable

    /// <summary>
    /// Tries to format the token into a span.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;
        
        var formatStr = format.IsEmpty ? "G" : format.ToString().ToUpperInvariant();
        
        string result = formatStr switch
        {
            "G" => $"{Type}:{Content}@{Position}",
            "S" => $"{Type}@{Position}",
            "C" => Content.ToString(),
            "T" => Type.ToString(),
            _ => throw new FormatException($"Unknown format string: '{formatStr}'")
        };
        
        if (result.Length > destination.Length)
            return false;
        
        result.AsSpan().CopyTo(destination);
        charsWritten = result.Length;
        return true;
    }

    #endregion

    #region IUtf8SpanFormattable

    /// <summary>
    /// Tries to format the token into a UTF-8 byte span.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        bytesWritten = 0;
        
        var formatStr = format.IsEmpty ? "G" : format.ToString().ToUpperInvariant();
        
        string result = formatStr switch
        {
            "G" => $"{Type}:{Content}@{Position}",
            "S" => $"{Type}@{Position}",
            "C" => Content.ToString(),
            "T" => Type.ToString(),
            _ => throw new FormatException($"Unknown format string: '{formatStr}'")
        };
        
        var byteCount = Encoding.UTF8.GetByteCount(result);
        if (byteCount > utf8Destination.Length)
            return false;
        
        bytesWritten = Encoding.UTF8.GetBytes(result, utf8Destination);
        return true;
    }

    #endregion

    #region Operators and Equality

    /// <summary>
    /// Checks if this token's content equals the specified character.
    /// </summary>
    public bool ContentEquals(char c)
    {
        return Content.Length == 1 && Content.Span[0] == c;
    }

    /// <summary>
    /// Checks if this token's content equals the specified string.
    /// </summary>
    public bool ContentEquals(ReadOnlySpan<char> other)
    {
        return Content.Span.SequenceEqual(other);
    }

    /// <summary>
    /// Checks if this token's content equals the specified string.
    /// </summary>
    public bool ContentEquals(string other)
    {
        return ContentEquals(other.AsSpan());
    }

    #endregion
}
