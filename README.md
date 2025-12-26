# TinyTokenizer

A high-performance, zero-allocation tokenizer library for .NET that parses text into abstract tokens using `ReadOnlySpan<char>` for maximum efficiency.

## Features

- **Zero-allocation parsing** — Uses `ReadOnlySpan<char>` internally for fast, allocation-free text traversal
- **Recursive declaration blocks** — Automatically parses nested `{}`, `[]`, and `()` blocks with child tokens
- **Configurable symbols** — Define which characters are recognized as symbol tokens
- **Immutable tokens** — All token types are immutable record classes
- **Error recovery** — Gracefully handles malformed input with `ErrorToken` and continues parsing

## Installation

Add a reference to the `TinyTokenizer` project or include the source files in your solution.

## Quick Start

```csharp
using TinyTokenizer;

// Create a tokenizer with source text
var tokenizer = new Tokenizer("func(a, b)".AsMemory());
var tokens = tokenizer.Tokenize();

// tokens contains:
// - IdentToken("func")
// - BlockToken("(a, b)") with children:
//   - IdentToken("a")
//   - SymbolToken(",")
//   - WhitespaceToken(" ")
//   - IdentToken("b")
```

## Token Types

| Type | Description | Example |
|------|-------------|---------|
| `IdentToken` | Identifier/text content | `hello`, `func`, `123` |
| `WhitespaceToken` | Spaces, tabs, newlines | ` `, `\t`, `\n` |
| `SymbolToken` | Configurable symbol characters | `/`, `:`, `,`, `;` |
| `BlockToken` | Declaration blocks with delimiters | `{...}`, `[...]`, `(...)` |
| `ErrorToken` | Parsing errors (unmatched delimiters) | `}` without opening `{` |

### BlockToken Properties

```csharp
var tokenizer = new Tokenizer("{inner content}".AsMemory());
var tokens = tokenizer.Tokenize();
var block = (BlockToken)tokens[0];

block.FullContent;      // "{inner content}" (includes delimiters)
block.InnerContent;     // "inner content" (excludes delimiters)
block.Children;         // ImmutableArray<Token> of parsed inner tokens
block.OpeningDelimiter; // '{'
block.ClosingDelimiter; // '}'
block.Type;             // TokenType.BraceBlock
```

## Configuration

Customize the tokenizer with `TokenizerOptions`:

```csharp
// Default symbols: / : , ; = + - * < > ! & | . @ # ? % ^ ~ \
var options = TokenizerOptions.Default;

// Add custom symbols
options = TokenizerOptions.Default.WithAdditionalSymbols('$', '_');

// Remove symbols (they become part of text tokens)
options = TokenizerOptions.Default.WithoutSymbols('/');

// Replace entire symbol set
options = TokenizerOptions.Default.WithSymbols(':', ',', ';');

// Use with tokenizer
var tokenizer = new Tokenizer(source.AsMemory(), options);
```

## Nested Blocks

Declaration blocks are parsed recursively:

```csharp
var tokenizer = new Tokenizer("{outer [inner (deepest)]}".AsMemory());
var tokens = tokenizer.Tokenize();

var braceBlock = (BlockToken)tokens[0];           // {outer [inner (deepest)]}
var bracketBlock = (BlockToken)braceBlock.Children[2];  // [inner (deepest)]
var parenBlock = (BlockToken)bracketBlock.Children[2];  // (deepest)
```

## Error Handling

The tokenizer produces `ErrorToken` for malformed input and continues parsing:

```csharp
var tokenizer = new Tokenizer("}hello{".AsMemory());
var tokens = tokenizer.Tokenize();

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
IEnumerable<IdentToken> IdentTokens = tokens.OfTokenType<IdentToken>();
IEnumerable<BlockToken> blocks = tokens.OfTokenType<BlockToken>();
```

## API Reference

### Tokenizer (ref struct)

```csharp
// Constructor
public Tokenizer(ReadOnlyMemory<char> source, TokenizerOptions? options = null)

// Tokenize the source
public ImmutableArray<Token> Tokenize()
```

### Token (abstract record)

```csharp
public abstract record Token(ReadOnlyMemory<char> Content, TokenType Type)
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
    Text,             // plain text
    Whitespace,       // spaces, tabs, newlines
    Error             // parsing errors
}
```

## Requirements

- .NET 8.0 or later

## License

MIT
