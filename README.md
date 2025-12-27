# TinyTokenizer

A high-performance, zero-allocation tokenizer library for .NET 8+ that parses text into abstract tokens using `ReadOnlySpan<char>` and SIMD-optimized `SearchValues<char>` for maximum efficiency.

## Features

- **Zero-allocation parsing** — Uses `ReadOnlySpan<char>` internally for fast, allocation-free text traversal
- **SIMD-optimized** — Uses .NET 8's `SearchValues<char>` for vectorized character matching
- **Two-level architecture** — Lexer (character classification) + TokenParser (semantic parsing)
- **Async streaming** — Tokenize `Stream` and `PipeReader` sources with `IAsyncEnumerable<Token>`
- **Recursive declaration blocks** — Automatically parses nested `{}`, `[]`, and `()` blocks with child tokens
- **String literals** — Supports single and double-quoted strings with escape sequences
- **Numeric literals** — Parses integers and floating-point numbers
- **Comment support** — Configurable single-line and multi-line comment styles
- **Operators** — Configurable multi-character operators with greedy matching
- **Tagged identifiers** — Configurable prefix characters for patterns like `#define`, `@attribute`, `$variable`
- **Configurable symbols** — Define which characters are recognized as symbol tokens
- **Immutable tokens** — All token types are immutable record classes
- **Error recovery** — Gracefully handles malformed input with `ErrorToken` and continues parsing

## Installation

```bash
dotnet add package TinyTokenizer
```

Or add a reference to the `TinyTokenizer` project in your solution.

## Quick Start

```csharp
using TinyTokenizer;

// Simple tokenization
var tokens = "func(a, b)".TokenizeToTokens();

// tokens contains:
// - IdentToken("func")
// - BlockToken("(a, b)") with children:
//   - IdentToken("a")
//   - SymbolToken(",")
//   - WhitespaceToken(" ")
//   - IdentToken("b")

// With options
var options = TokenizerOptions.Default
    .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine);

var tokens = "x = 42; // comment".TokenizeToTokens(options);
```

## Architecture

TinyTokenizer uses a two-level tokenization architecture:

### Level 1: Lexer

The `Lexer` is a stateless character classifier that never backtracks and never fails. It produces `SimpleToken` instances representing atomic character sequences:

- `Ident` — identifier characters
- `Whitespace` — spaces, tabs (excluding newlines)
- `Newline` — `\n` or `\r\n` (separate for single-line comment detection)
- `Digits` — consecutive digit characters
- `Symbol` — configured symbol characters
- `Dot`, `Slash`, `Asterisk`, `Backslash` — special characters for parsing
- `OpenBrace/CloseBrace`, `OpenBracket/CloseBracket`, `OpenParen/CloseParen`
- `SingleQuote`, `DoubleQuote`

### Level 2: TokenParser

The `TokenParser` combines simple tokens into semantic tokens:

- **Block parsing** — recursive nesting of `{}`, `[]`, `()`
- **String literals** — quoted strings with escape sequences
- **Numeric literals** — integers and floating-point numbers
- **Comments** — single-line and multi-line
- **Error recovery** — produces `ErrorToken` for malformed input

```csharp
// Using two-level API directly
var lexer = new Lexer(options);
var parser = new TokenParser(options);

var simpleTokens = lexer.Lex(source);
var tokens = parser.ParseToArray(simpleTokens);
```

## Token Types

| Type              | Description                           | Example                       |
| ----------------- | ------------------------------------- | ----------------------------- |
| `IdentToken`      | Identifier/text content               | `hello`, `func`, `_name`      |
| `WhitespaceToken` | Spaces, tabs, newlines                | ` `, `\t`, `\n`               |
| `SymbolToken`     | Configurable symbol characters        | `/`, `:`, `,`, `;`            |
| `OperatorToken`   | Multi-character operators             | `==`, `!=`, `&&`, `\|\|`, `->` |
| `TaggedIdentToken`| Tag prefix + identifier               | `#define`, `@Override`, `$var`|
| `NumericToken`    | Integer or floating-point numbers     | `123`, `3.14`, `.5`           |
| `StringToken`     | Quoted string literals                | `"hello"`, `'c'`              |
| `CommentToken`    | Single or multi-line comments         | `// comment`, `/* block */`   |
| `BlockToken`      | Declaration blocks with delimiters    | `{...}`, `[...]`, `(...)`     |
| `ErrorToken`      | Parsing errors (unmatched delimiters) | `}` without opening `{`       |

### Token Properties

```csharp
// All tokens have Position tracking
var token = tokens[0];
long position = token.Position;  // Character offset in source

// NumericToken
var num = (NumericToken)token;
num.NumericType;  // NumericType.Integer or NumericType.FloatingPoint

// StringToken
var str = (StringToken)token;
str.Quote;  // '"' or '\''
str.Value;  // Content without quotes (ReadOnlySpan<char>)

// CommentToken
var comment = (CommentToken)token;
comment.IsMultiLine;  // true for /* */, false for //

// BlockToken
var block = (BlockToken)token;
block.FullContent;       // "{inner content}" (includes delimiters)
block.InnerContent;      // "inner content" (excludes delimiters)
block.Children;          // ImmutableArray<Token> of parsed inner tokens
block.OpeningDelimiter;  // '{'
block.ClosingDelimiter;  // '}'

// OperatorToken
var op = (OperatorToken)token;
op.Operator;  // "==" or "!=" etc. (string)

// TaggedIdentToken
var tagged = (TaggedIdentToken)token;
tagged.Tag;      // '#' or '@' or '$' etc.
tagged.NameSpan; // "define" or "Override" (ReadOnlySpan<char>)
```

## Async Tokenization

Tokenize streams and pipes asynchronously:

```csharp
using TinyTokenizer;

// From Stream
await using var stream = File.OpenRead("source.txt");
var tokens = await stream.TokenizeAsync();

// Streaming with IAsyncEnumerable
await foreach (var token in stream.TokenizeStreamingAsync())
{
    Console.WriteLine(token);
}

// From PipeReader
var pipeReader = PipeReader.Create(stream);
var tokens = await pipeReader.TokenizeAsync();

// With custom encoding
var tokens = await stream.TokenizeAsync(
    options: TokenizerOptions.Default,
    encoding: Encoding.UTF8,
    leaveOpen: false,
    cancellationToken: ct);
```

## Configuration

### Symbols

```csharp
// Default symbols: / : , ; = + - * < > ! & | . @ # ? % ^ ~ \
var options = TokenizerOptions.Default;

// Add custom symbols
options = options.WithAdditionalSymbols('$', '_');

// Remove symbols (they become part of identifier tokens)
options = options.WithoutSymbols('/');

// Replace entire symbol set
options = options.WithSymbols(':', ',', ';');
```

### Comment Styles

```csharp
// Built-in comment styles
CommentStyle.CStyleSingleLine   // //
CommentStyle.CStyleMultiLine    // /* */
CommentStyle.HashSingleLine     // #
CommentStyle.SqlSingleLine      // --
CommentStyle.HtmlComment        // <!-- -->

// Configure tokenizer with comments
var options = TokenizerOptions.Default
    .WithCommentStyles(
        CommentStyle.CStyleSingleLine,
        CommentStyle.CStyleMultiLine);

// Add additional comment styles
options = options.WithAdditionalCommentStyles(CommentStyle.HashSingleLine);

// Custom comment style
var customComment = new CommentStyle("REM", null);  // Single-line ending at newline
var blockComment = new CommentStyle("(*", "*)");    // Multi-line Pascal-style
```

### Operators

```csharp
// Built-in operator sets
CommonOperators.Universal   // ==, !=, &&, ||
CommonOperators.CFamily     // ==, !=, <=, >=, &&, ||, ++, --, <<, >>, ->, etc.
CommonOperators.Comparison  // ==, !=, <, >, <=, >=
CommonOperators.Logical     // &&, ||, !
CommonOperators.Assignment  // =, +=, -=, *=, /=, etc.

// Configure operators (uses greedy matching - longest operator first)
var options = TokenizerOptions.Default
    .WithOperators(CommonOperators.CFamily);

// Add custom operators
options = options.WithAdditionalOperators("<=>", "??", "?.");

// Remove specific operators
options = options.WithoutOperators("++", "--");

// No operators (all symbols emit individually)
options = options.WithNoOperators();
```

### Tagged Identifiers

Tagged identifiers recognize patterns like `#define`, `@attribute`, or `$variable`:

```csharp
// Enable C-style preprocessor tags
var options = TokenizerOptions.Default.WithTagPrefixes('#');
var tokens = "#include #define".TokenizeToTokens(options);
// tokens: TaggedIdentToken("#include"), WhitespaceToken, TaggedIdentToken("#define")

// Enable Java/C# style annotations
var options = TokenizerOptions.Default.WithTagPrefixes('@');
var tokens = "@Override @NotNull".TokenizeToTokens(options);

// Enable shell/PHP style variables
var options = TokenizerOptions.Default.WithTagPrefixes('$');
var tokens = "$name $count".TokenizeToTokens(options);

// Multiple prefixes for multi-language support
var options = TokenizerOptions.Default.WithTagPrefixes('#', '@', '$');

// Add/remove prefixes
options = options.WithAdditionalTagPrefixes('~');
options = options.WithoutTagPrefixes('#');
options = options.WithNoTagPrefixes();  // Disable all
```

**Note:** Tag prefix characters are automatically treated as symbols by the Lexer, so any character can be used as a tag prefix.

## Nested Blocks

Declaration blocks are parsed recursively:

```csharp
var tokens = "{outer [inner (deepest)]}".TokenizeToTokens();

var braceBlock = (BlockToken)tokens[0];                  // {outer [inner (deepest)]}
var bracketBlock = (BlockToken)braceBlock.Children[2];   // [inner (deepest)]
var parenBlock = (BlockToken)bracketBlock.Children[2];   // (deepest)
```

## Error Handling

The tokenizer produces `ErrorToken` for malformed input and continues parsing:

```csharp
var tokens = "}hello{".TokenizeToTokens();

// tokens contains:
// - ErrorToken("}", "Unexpected closing delimiter '}'", position: 0)
// - IdentToken("hello")
// - ErrorToken("{", "Unclosed block starting with '{'", position: 6)

// Check for errors
if (tokens.HasErrors())
{
    foreach (var error in tokens.GetErrors())
    {
        Console.WriteLine($"Error at {error.Position}: {error.ErrorMessage}");
    }
}
```

## Utility Extensions

Extensions on `ImmutableArray<Token>` for common operations:

```csharp
// Check if any errors exist (including nested)
bool hasErrors = tokens.HasErrors();

// Get all errors (including nested)
IEnumerable<ErrorToken> errors = tokens.GetErrors();

// Get all tokens of a specific type (including nested)
IEnumerable<IdentToken> idents = tokens.OfTokenType<IdentToken>();
IEnumerable<BlockToken> blocks = tokens.OfTokenType<BlockToken>();
IEnumerable<NumericToken> numbers = tokens.OfTokenType<NumericToken>();
```

## Benchmarks

Performance comparison of the optimized `SearchValues<char>` implementation vs the baseline `ImmutableHashSet<char>`:

| Input Size        | Baseline | Optimized | Speedup   |
| ----------------- | -------- | --------- | --------- |
| Small (~50 chars) | 377 ns   | 245 ns    | **1.54x** |
| Medium (~1KB)     | 6,866 ns | 3,020 ns  | **2.27x** |
| Large (~100KB)    | 1,907 μs | 781 μs    | **2.44x** |
| JSON (~10KB)      | 130 μs   | 87 μs     | **1.51x** |
| Whitespace-heavy  | 9,808 ns | 3,661 ns  | **2.68x** |

Run benchmarks yourself:

```bash
dotnet run -c Release --project TinyTokenizer.Benchmarks -- --filter "*"
```

## API Reference

### Extension Methods

```csharp
// String extensions
ImmutableArray<Token> TokenizeToTokens(this string source, TokenizerOptions? options = null)

// ReadOnlyMemory<char> extensions
ImmutableArray<Token> Tokenize(this ReadOnlyMemory<char> source, TokenizerOptions? options = null)

// Stream extensions
Task<ImmutableArray<Token>> TokenizeAsync(this Stream, TokenizerOptions?, Encoding?, bool leaveOpen, CancellationToken)
IAsyncEnumerable<Token> TokenizeStreamingAsync(this Stream, TokenizerOptions?, Encoding?, bool leaveOpen, CancellationToken)

// PipeReader extensions
Task<ImmutableArray<Token>> TokenizeAsync(this PipeReader, TokenizerOptions?, Encoding?, CancellationToken)
IAsyncEnumerable<Token> TokenizeStreamingAsync(this PipeReader, TokenizerOptions?, Encoding?, CancellationToken)
```

### Tokenizer (ref struct)

```csharp
public ref struct Tokenizer
{
    public Tokenizer(ReadOnlyMemory<char> source, TokenizerOptions? options = null);
    public ImmutableArray<Token> Tokenize();
}
```

### Lexer

```csharp
public sealed class Lexer
{
    public Lexer();
    public Lexer(ImmutableHashSet<char> symbols);
    public Lexer(TokenizerOptions options);

    public IEnumerable<SimpleToken> Lex(ReadOnlyMemory<char> input);
    public IEnumerable<SimpleToken> Lex(string input);
    public ImmutableArray<SimpleToken> LexToArray(ReadOnlyMemory<char> input);
}
```

### TokenParser

```csharp
public sealed class TokenParser
{
    public TokenParser();
    public TokenParser(TokenizerOptions options);

    public IEnumerable<Token> Parse(IEnumerable<SimpleToken> simpleTokens);
    public ImmutableArray<Token> ParseToArray(IEnumerable<SimpleToken> simpleTokens);
}
```

### Token (abstract record)

```csharp
public abstract record Token(ReadOnlyMemory<char> Content, TokenType Type, long Position)
{
    public ReadOnlySpan<char> ContentSpan { get; }
}
```

### TokenType (enum)

```csharp
public enum TokenType
{
    BraceBlock,       // { }
    BracketBlock,     // [ ]
    ParenthesisBlock, // ( )
    Symbol,           // configurable characters
    Ident,            // identifiers
    Whitespace,       // spaces, tabs, newlines
    Numeric,          // numbers
    String,           // quoted strings
    Comment,          // comments
    Error,            // parsing errors
    Operator,         // multi-character operators
    TaggedIdent       // tag prefix + identifier
}
```

## Requirements

- .NET 8.0 or later

## License

MIT
