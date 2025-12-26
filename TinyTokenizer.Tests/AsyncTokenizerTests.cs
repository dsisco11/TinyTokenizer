using System.Text;

namespace TinyTokenizer.Tests;

/// <summary>
/// Tests for the async tokenization functionality including AsyncPipeTokenizer,
/// DecodingPipeReader, and async extension methods.
/// </summary>
public class AsyncTokenizerTests
{
    #region Helper Methods

    private static async Task<List<Token>> TokenizeStringAsync(
        string source,
        TokenizerOptions? options = null,
        Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var bytes = encoding.GetBytes(source);
        using var stream = new MemoryStream(bytes);

        var tokens = await stream.TokenizeAsync(options, encoding);
        return [.. tokens];
    }

    private static async Task<List<Token>> TokenizeChunkedAsync(
        string source,
        int chunkSize,
        TokenizerOptions? options = null)
    {
        // Simulate chunked reading by using a throttled stream
        var bytes = Encoding.UTF8.GetBytes(source);
        using var stream = new ChunkedMemoryStream(bytes, chunkSize);

        var tokens = await stream.TokenizeAsync(options);
        return [.. tokens];
    }

    #endregion

    #region Basic Async Tokenization Tests

    [Fact]
    public async Task TokenizeAsync_EmptyStream_ReturnsNoTokens()
    {
        var tokens = await TokenizeStringAsync("");

        Assert.Empty(tokens);
    }

    [Fact]
    public async Task TokenizeAsync_PlainText_ReturnsSingleTextToken()
    {
        var tokens = await TokenizeStringAsync("hello");

        Assert.Single(tokens);
        var token = Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("hello", token.ContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_Whitespace_ReturnsSingleWhitespaceToken()
    {
        var tokens = await TokenizeStringAsync("   ");

        Assert.Single(tokens);
        Assert.IsType<WhitespaceToken>(tokens[0]);
    }

    [Fact]
    public async Task TokenizeAsync_Symbol_ReturnsSingleSymbolToken()
    {
        var tokens = await TokenizeStringAsync("/");

        Assert.Single(tokens);
        var symbol = Assert.IsType<SymbolToken>(tokens[0]);
        Assert.Equal('/', symbol.Symbol);
    }

    [Fact]
    public async Task TokenizeAsync_MixedContent_ReturnsCorrectTokens()
    {
        var tokens = await TokenizeStringAsync("hello world");

        Assert.Equal(3, tokens.Count);
        Assert.IsType<TextToken>(tokens[0]);
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<TextToken>(tokens[2]);
    }

    #endregion

    #region Block Tokenization Tests

    [Fact]
    public async Task TokenizeAsync_SimpleBraceBlock_ReturnsBlockToken()
    {
        var tokens = await TokenizeStringAsync("{content}");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);
        Assert.Equal("{content}", block.FullContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_NestedBlocks_ReturnsRecursiveStructure()
    {
        var tokens = await TokenizeStringAsync("{[()]}");

        Assert.Single(tokens);
        var braceBlock = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Single(braceBlock.Children);

        var bracketBlock = Assert.IsType<BlockToken>(braceBlock.Children[0]);
        Assert.Single(bracketBlock.Children);

        var parenBlock = Assert.IsType<BlockToken>(bracketBlock.Children[0]);
        Assert.Empty(parenBlock.Children);
    }

    [Fact]
    public async Task TokenizeAsync_BlockWithChildren_ParsesNestedContent()
    {
        var tokens = await TokenizeStringAsync("{hello world}");

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(3, block.Children.Length);
    }

    #endregion

    #region String Literal Tests

    [Fact]
    public async Task TokenizeAsync_DoubleQuotedString_ReturnsStringToken()
    {
        var tokens = await TokenizeStringAsync("\"hello world\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("hello world", str.Value.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_SingleQuotedString_ReturnsStringToken()
    {
        var tokens = await TokenizeStringAsync("'hello'");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal('\'', str.Quote);
    }

    [Fact]
    public async Task TokenizeAsync_StringWithEscape_PreservesEscape()
    {
        var tokens = await TokenizeStringAsync("\"hello\\\"world\"");

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"hello\\\"world\"", str.ContentSpan.ToString());
    }

    #endregion

    #region Numeric Tests

    [Fact]
    public async Task TokenizeAsync_Integer_ReturnsNumericToken()
    {
        var tokens = await TokenizeStringAsync("12345");

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(NumericType.Integer, num.NumericType);
    }

    [Fact]
    public async Task TokenizeAsync_FloatingPoint_ReturnsNumericToken()
    {
        var tokens = await TokenizeStringAsync("123.456");

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    #endregion

    #region Comment Tests

    [Fact]
    public async Task TokenizeAsync_SingleLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleSingleLine);
        var tokens = await TokenizeStringAsync("// comment\ncode", options);

        Assert.Equal(3, tokens.Count);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.False(comment.IsMultiLine);
    }

    [Fact]
    public async Task TokenizeAsync_MultiLineComment_ReturnsCommentToken()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        var tokens = await TokenizeStringAsync("/* comment */", options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.True(comment.IsMultiLine);
    }

    #endregion

    #region Chunked Stream Tests (Simulating Network/File I/O)

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_SimpleText_Works()
    {
        // Test with small chunks that split tokens
        var tokens = await TokenizeChunkedAsync("hello world", chunkSize: 3);

        Assert.Equal(3, tokens.Count);
        Assert.IsType<TextToken>(tokens[0]);
        Assert.Equal("hello", tokens[0].ContentSpan.ToString());
        Assert.IsType<WhitespaceToken>(tokens[1]);
        Assert.IsType<TextToken>(tokens[2]);
        Assert.Equal("world", tokens[2].ContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_BlockAcrossChunks_Works()
    {
        // Block split across chunks
        var tokens = await TokenizeChunkedAsync("{content}", chunkSize: 4);

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal("{content}", block.FullContentSpan.ToString());
        Assert.Equal("content", block.InnerContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_StringAcrossChunks_Works()
    {
        // String split across chunks
        var tokens = await TokenizeChunkedAsync("\"hello world\"", chunkSize: 5);

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("hello world", str.Value.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_NestedBlocks_Works()
    {
        // Nested blocks with very small chunks
        var tokens = await TokenizeChunkedAsync("{[()]}", chunkSize: 2);

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);
        Assert.Single(block.Children);
        
        var innerBlock = Assert.IsType<BlockToken>(block.Children[0]);
        Assert.Equal(TokenType.BracketBlock, innerBlock.Type);
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_StringWithEscape_Works()
    {
        // String with escape sequence split across chunks
        var tokens = await TokenizeChunkedAsync("\"hello\\\"world\"", chunkSize: 4);

        Assert.Single(tokens);
        var str = Assert.IsType<StringToken>(tokens[0]);
        Assert.Equal("\"hello\\\"world\"", str.ContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_MultiLineComment_Works()
    {
        var options = TokenizerOptions.Default.WithCommentStyles(CommentStyle.CStyleMultiLine);
        
        // Multi-line comment split across chunks
        var bytes = Encoding.UTF8.GetBytes("/* comment */");
        using var stream = new ChunkedMemoryStream(bytes, chunkSize: 4);
        
        var tokens = await stream.TokenizeAsync(options);

        Assert.Single(tokens);
        var comment = Assert.IsType<CommentToken>(tokens[0]);
        Assert.Equal("/* comment */", comment.ContentSpan.ToString());
        Assert.True(comment.IsMultiLine);
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_MixedContent_Works()
    {
        // Mixed content with small chunks
        var tokens = await TokenizeChunkedAsync("func(a, b) { x = 1; }", chunkSize: 3);

        // The two-level tokenizer produces semantic tokens:
        // - func (text)
        // - (a, b) as a parenthesis block
        // - space (whitespace)  
        // - { x = 1; } as a brace block
        Assert.Equal(4, tokens.Count);
        
        // Find the function name
        Assert.Contains(tokens, t => t is TextToken txt && txt.ContentSpan.ToString() == "func");
        
        // Find the blocks
        var parenBlock = tokens.OfType<BlockToken>().Single(b => b.Type == TokenType.ParenthesisBlock);
        Assert.Equal("(a, b)", parenBlock.ContentSpan.ToString());
        
        var braceBlock = tokens.OfType<BlockToken>().Single(b => b.Type == TokenType.BraceBlock);
        Assert.Equal("{ x = 1; }", braceBlock.ContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_ChunkedStream_Numbers_Works()
    {
        var tokens = await TokenizeChunkedAsync("123.456", chunkSize: 2);

        Assert.Single(tokens);
        var num = Assert.IsType<NumericToken>(tokens[0]);
        Assert.Equal("123.456", num.ContentSpan.ToString());
        Assert.Equal(NumericType.FloatingPoint, num.NumericType);
    }

    #endregion

    #region UTF-8 Encoding Tests

    [Fact]
    public async Task TokenizeAsync_UTF8_BasicText_Works()
    {
        var tokens = await TokenizeStringAsync("hello", encoding: Encoding.UTF8);

        Assert.Single(tokens);
        Assert.Equal("hello", tokens[0].ContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_UTF8_MultiByteCharacters_Works()
    {
        var tokens = await TokenizeStringAsync("héllo wörld", encoding: Encoding.UTF8);

        Assert.Equal(3, tokens.Count);
        Assert.Equal("héllo", tokens[0].ContentSpan.ToString());
        Assert.Equal("wörld", tokens[2].ContentSpan.ToString());
    }

    [Fact]
    public async Task TokenizeAsync_UTF8_Emoji_Works()
    {
        var tokens = await TokenizeStringAsync("hello 🌍 world", encoding: Encoding.UTF8);

        Assert.Equal(5, tokens.Count); // hello, space, emoji, space, world
    }

    [Fact]
    public async Task TokenizeAsync_UTF8_ChineseCharacters_Works()
    {
        var tokens = await TokenizeStringAsync("你好 世界", encoding: Encoding.UTF8);

        Assert.Equal(3, tokens.Count);
        Assert.Equal("你好", tokens[0].ContentSpan.ToString());
        Assert.Equal("世界", tokens[2].ContentSpan.ToString());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task TokenizeAsync_UnexpectedClosingDelimiter_ReturnsErrorToken()
    {
        var tokens = await TokenizeStringAsync("}");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unexpected closing delimiter", error.ErrorMessage);
    }

    [Fact]
    public async Task TokenizeAsync_UnclosedBlock_ReturnsErrorToken()
    {
        var tokens = await TokenizeStringAsync("{");

        Assert.Single(tokens);
        var error = Assert.IsType<ErrorToken>(tokens[0]);
        Assert.Contains("Unclosed block", error.ErrorMessage);
    }

    [Fact]
    public async Task TokenizeAsync_ErrorRecovery_ContinuesFromNextCharacter()
    {
        var tokens = await TokenizeStringAsync("}hello");

        Assert.Equal(2, tokens.Count);
        Assert.IsType<ErrorToken>(tokens[0]);
        Assert.IsType<TextToken>(tokens[1]);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task TokenizeAsync_Cancellation_StopsEnumeration()
    {
        using var cts = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes("hello world foo bar baz");
        using var stream = new MemoryStream(bytes);

        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await stream.TokenizeAsync(cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task TokenizeAsync_WithoutCancellation_CompletesNormally()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(bytes);

        var tokens = await stream.TokenizeAsync();

        Assert.Equal(3, tokens.Length);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public async Task Stream_TokenizeAsync_Extension_Works()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(bytes);

        var tokens = await stream.TokenizeAsync();

        Assert.Equal(3, tokens.Length);
    }

    [Fact]
    public async Task Stream_TokenizeToListAsync_Extension_Works()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(bytes);

        var tokens = await stream.TokenizeToListAsync();

        Assert.Equal(3, tokens.Count);
    }

    [Fact]
    public async Task ByteArray_TokenizeAsync_Extension_Works()
    {
        var bytes = Encoding.UTF8.GetBytes("hello world");

        var tokens = await bytes.TokenizeAsync();

        Assert.Equal(3, tokens.Length);
    }

    [Fact]
    public async Task String_TokenizeAsStreamAsync_Extension_Works()
    {
        var tokens = new List<Token>();
        await foreach (var token in "hello world".TokenizeAsStreamAsync())
        {
            tokens.Add(token);
        }

        Assert.Equal(3, tokens.Count);
    }

    #endregion

    #region Complex Integration Tests

    [Fact]
    public async Task TokenizeAsync_ComplexCode_ParsesCorrectly()
    {
        var code = "function test(a, b) { return a + b; }";

        var tokens = await TokenizeStringAsync(code);

        Assert.True(tokens.Count > 5, $"Expected more than 5 tokens, got {tokens.Count}");
        Assert.Contains(tokens, t => t is TextToken txt && txt.ContentSpan.ToString() == "function");
        Assert.Contains(tokens, t => t is BlockToken { Type: TokenType.ParenthesisBlock });
        Assert.Contains(tokens, t => t is BlockToken { Type: TokenType.BraceBlock });
    }

    [Fact]
    public async Task TokenizeAsync_ComplexCode_Chunked_ParsesCorrectly()
    {
        var code = "function test(a, b) { return a + b; }";

        var tokens = await TokenizeChunkedAsync(code, chunkSize: 5);

        Assert.True(tokens.Count > 5, $"Expected more than 5 tokens, got {tokens.Count}");
        Assert.Contains(tokens, t => t is TextToken txt && txt.ContentSpan.ToString() == "function");
        Assert.Contains(tokens, t => t is BlockToken { Type: TokenType.ParenthesisBlock });
        Assert.Contains(tokens, t => t is BlockToken { Type: TokenType.BraceBlock });
    }

    [Fact]
    public async Task TokenizeAsync_JsonLike_ParsesCorrectly()
    {
        var json = """{"name": "test", "value": 123}""";

        var tokens = await TokenizeStringAsync(json);

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);

        // Should have strings for keys and values
        Assert.Contains(block.Children, t => t is StringToken);
    }

    [Fact]
    public async Task TokenizeAsync_JsonLike_Chunked_ParsesCorrectly()
    {
        var json = """{"name": "test", "value": 123}""";

        var tokens = await TokenizeChunkedAsync(json, chunkSize: 4);

        Assert.Single(tokens);
        var block = Assert.IsType<BlockToken>(tokens[0]);
        Assert.Equal(TokenType.BraceBlock, block.Type);

        // Should have strings for keys and values
        Assert.Contains(block.Children, t => t is StringToken);
    }

    [Fact]
    public async Task TokenizeAsync_LargeInput_CompletesSuccessfully()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            builder.Append($"token{i} ");
        }

        var tokens = await TokenizeStringAsync(builder.ToString());

        // Each token + space = 2 tokens per iteration, minus trailing space handling
        Assert.True(tokens.Count >= 100, $"Expected at least 100 tokens, got {tokens.Count}");
    }

    [Fact]
    public async Task TokenizeAsync_LargeInput_Chunked_CompletesSuccessfully()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            builder.Append($"token{i} ");
        }

        var tokens = await TokenizeChunkedAsync(builder.ToString(), chunkSize: 10);

        // Each token + space = 2 tokens per iteration
        Assert.True(tokens.Count >= 100, $"Expected at least 100 tokens, got {tokens.Count}");
    }

    #endregion
}

/// <summary>
/// A memory stream that returns data in fixed-size chunks to simulate network I/O.
/// </summary>
internal sealed class ChunkedMemoryStream : Stream
{
    private readonly byte[] _data;
    private readonly int _chunkSize;
    private int _position;

    public ChunkedMemoryStream(byte[] data, int chunkSize)
    {
        _data = data;
        _chunkSize = chunkSize;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _data.Length;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _data.Length)
            return 0;

        int bytesToRead = Math.Min(Math.Min(count, _chunkSize), _data.Length - _position);
        Array.Copy(_data, _position, buffer, offset, bytesToRead);
        _position += bytesToRead;
        return bytesToRead;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
