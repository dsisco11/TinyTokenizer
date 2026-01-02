namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for <see cref="Token"/> IFormattable implementation.
/// </summary>
public class TokenFormattableTests
{
    private static IdentToken CreateToken(string content, int position) =>
        new() { Content = content.AsMemory(), Position = position };

    #region Format Specifier Tests

    [Fact]
    public void ToString_WithNullFormat_ReturnsContent()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString(null, null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToString_WithEmptyFormat_ReturnsContent()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("", null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToString_WithGFormat_ReturnsContent()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("G", null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToString_WithLowercaseGFormat_ReturnsContent()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("g", null);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToString_WithTFormat_ReturnsType()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("T", null);

        Assert.Equal("Ident", result);
    }

    [Fact]
    public void ToString_WithLowercaseTFormat_ReturnsType()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("t", null);

        Assert.Equal("Ident", result);
    }

    [Fact]
    public void ToString_WithPFormat_ReturnsPosition()
    {
        var token = CreateToken("hello", 42);

        var result = token.ToString("P", null);

        Assert.Equal("42", result);
    }

    [Fact]
    public void ToString_WithLowercasePFormat_ReturnsPosition()
    {
        var token = CreateToken("hello", 42);

        var result = token.ToString("p", null);

        Assert.Equal("42", result);
    }

    [Fact]
    public void ToString_WithRFormat_ReturnsRange()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("R", null);

        Assert.Equal("10..15", result);
    }

    [Fact]
    public void ToString_WithLowercaseRFormat_ReturnsRange()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("r", null);

        Assert.Equal("10..15", result);
    }

    [Fact]
    public void ToString_WithDFormat_ReturnsDebugFormat()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("D", null);

        Assert.Equal("Ident[10..15]", result);
    }

    [Fact]
    public void ToString_WithLowercaseDFormat_ReturnsDebugFormat()
    {
        var token = CreateToken("hello", 10);

        var result = token.ToString("d", null);

        Assert.Equal("Ident[10..15]", result);
    }

    [Fact]
    public void ToString_WithUnknownFormat_ThrowsFormatException()
    {
        var token = CreateToken("hello", 10);

        Assert.Throws<FormatException>(() => token.ToString("X", null));
    }

    #endregion

    #region Different Token Types

    [Fact]
    public void ToString_WithSymbolToken_ReturnsCorrectType()
    {
        var token = new SymbolToken { Content = ";".AsMemory(), Position = 5 };

        Assert.Equal("Symbol", token.ToString("T", null));
        Assert.Equal("Symbol[5..6]", token.ToString("D", null));
    }

    [Fact]
    public void ToString_WithOperatorToken_ReturnsCorrectType()
    {
        var token = new OperatorToken { Content = "==".AsMemory(), Position = 0 };

        Assert.Equal("Operator", token.ToString("T", null));
        Assert.Equal("Operator[0..2]", token.ToString("D", null));
    }

    [Fact]
    public void ToString_WithWhitespaceToken_ReturnsCorrectType()
    {
        var token = new WhitespaceToken { Content = "   ".AsMemory(), Position = 100 };

        Assert.Equal("Whitespace", token.ToString("T", null));
        Assert.Equal("100..103", token.ToString("R", null));
    }

    [Fact]
    public void ToString_WithStringToken_ReturnsCorrectType()
    {
        var token = new StringToken { Content = "\"test\"".AsMemory(), Position = 20, Quote = '"' };

        Assert.Equal("String", token.ToString("T", null));
        Assert.Equal("\"test\"", token.ToString("G", null));
    }

    [Fact]
    public void ToString_WithCommentToken_ReturnsCorrectType()
    {
        var token = new CommentToken { Content = "// comment".AsMemory(), Position = 0, IsMultiLine = false };

        Assert.Equal("Comment", token.ToString("T", null));
        Assert.Equal("Comment[0..10]", token.ToString("D", null));
    }

    [Fact]
    public void ToString_WithNumericToken_ReturnsCorrectType()
    {
        var token = new NumericToken { Content = "123.45".AsMemory(), Position = 50, NumericType = NumericType.FloatingPoint };

        Assert.Equal("Numeric", token.ToString("T", null));
        Assert.Equal("50..56", token.ToString("R", null));
    }

    #endregion

    #region String Interpolation Tests

    [Fact]
    public void StringInterpolation_WithFormatSpecifier_Works()
    {
        var token = CreateToken("hello", 10);

        var result = $"{token:D}";

        Assert.Equal("Ident[10..15]", result);
    }

    [Fact]
    public void StringInterpolation_WithDefaultFormat_ReturnsContent()
    {
        var token = CreateToken("hello", 10);

        var result = $"{token}";

        Assert.Equal("hello", result);
    }

    [Fact]
    public void StringFormat_WithFormatSpecifier_Works()
    {
        var token = CreateToken("hello", 10);

        var result = string.Format("{0:T} at {0:P}", token);

        Assert.Equal("Ident at 10", result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToString_WithEmptyContent_ReturnsEmptyString()
    {
        var token = new IdentToken { Content = "".AsMemory(), Position = 0 };

        Assert.Equal("", token.ToString("G", null));
        Assert.Equal("0..0", token.ToString("R", null));
        Assert.Equal("Ident[0..0]", token.ToString("D", null));
    }

    [Fact]
    public void ToString_WithZeroPosition_FormatsCorrectly()
    {
        var token = CreateToken("x", 0);

        Assert.Equal("0", token.ToString("P", null));
        Assert.Equal("0..1", token.ToString("R", null));
    }

    [Fact]
    public void ToString_WithLargePosition_FormatsCorrectly()
    {
        var token = CreateToken("x", 1_000_000);

        Assert.Equal("1000000", token.ToString("P", null));
        Assert.Equal("1000000..1000001", token.ToString("R", null));
    }

    #endregion
}
