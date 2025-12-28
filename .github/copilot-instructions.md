# TinyTokenizer - Copilot Instructions

## Architecture Overview

TinyTokenizer uses a **two-level tokenization architecture**:

1. **Level 1 - Lexer** ([Lexer.cs](../TinyTokenizer/Lexer.cs)): Stateless character classifier producing `SimpleToken` structs. Never backtracks, never fails. Uses SIMD-optimized `SearchValues<char>` for vectorized matching.

2. **Level 2 - TokenParser** ([TokenParser.cs](../TinyTokenizer/TokenParser.cs)): Combines simple tokens into semantic `Token` records (blocks, strings, comments, operators, tagged identifiers). Handles recursive block parsing and error recovery.

There's also a legacy `Tokenizer` ref struct that combines both levels internally - prefer the two-level API for new features.

## Key Patterns

### Token Types
- **SimpleToken** (`readonly record struct`) - Level 1 output, position-tracked atomic units
- **Token** (`abstract record`) - Level 2 base class, immutable with `ReadOnlyMemory<char>` content
- **BlockToken** - Contains `Children` array for recursive nested tokens (`{}`, `[]`, `()`)

### Zero-Allocation Design
```csharp
// Content uses ReadOnlyMemory<char> to reference source without copying
public abstract record Token(ReadOnlyMemory<char> Content, TokenType Type, long Position);

// Access spans for processing without allocation
ReadOnlySpan<char> span = token.ContentSpan;
```

### Options Pattern
Configure via `TokenizerOptions` with fluent `With*` methods:
```csharp
var options = TokenizerOptions.Default
    .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
    .WithOperators(CommonOperators.CFamily)
    .WithTagPrefixes('#', '@', '$');
```

## Build & Test Commands

```powershell
# Build all projects
dotnet build

# Run tests
dotnet test TinyTokenizer.Tests

# Run benchmarks (Release mode required)
dotnet run -c Release --project TinyTokenizer.Benchmarks
```

## Testing Conventions

- Test files mirror source structure in [TinyTokenizer.Tests/](../TinyTokenizer.Tests/)
- Use `TokenizeTwoLevel()` helper for features requiring multi-token pattern recognition
- Assert token types with `Assert.IsType<IdentToken>()` pattern
- Test error recovery by checking for `ErrorToken` in results

```csharp
// Standard test pattern
var tokens = Tokenize("func(a, b)");
Assert.Single(tokens.OfTokenType<BlockToken>());
```

## Extension Points

- **Custom symbols**: `TokenizerOptions.Symbols` - characters recognized as `SymbolToken`
- **Custom operators**: `TokenizerOptions.Operators` - multi-char sequences for `OperatorToken`
- **Tag prefixes**: `TokenizerOptions.TagPrefixes` - prefix chars for `TaggedIdentToken` (`#define`, `@attr`)
- **Comment styles**: Use predefined `CommentStyle.CStyleSingleLine` or create custom `new CommentStyle("--")`

## Async Streaming

For large inputs, use `IAsyncEnumerable` streaming via extension methods:
```csharp
await foreach (var token in stream.TokenizeStreamingAsync(options))
{
    // Process tokens as they arrive
}
```

## Performance Notes

- `Lexer` uses `SearchValues<char>` for SIMD-accelerated character classification
- Operators sorted by length descending for greedy matching
- `SimpleToken` is a struct to avoid heap allocations during lexing
- Avoid `ToString()` on tokens unless necessary - use `ContentSpan` for comparisons
