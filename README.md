# TinyTokenizer

A high-performance syntax tree library for .NET 8+ with fluent queries, pattern matching, and undo/redo editing — built on a zero-allocation tokenizer with SIMD optimization.

## Features

- **TinyAst** — Red-green syntax tree with fluent queries, editing, and undo/redo
- **Query API** — CSS-like selectors with combinators, lookahead, and repetition
- **SyntaxEditor** — Batch mutations with atomic commit and full undo/redo support
- **Syntax Nodes** — Pattern-based AST matching (function calls, property access, etc.)
- **Schema System** — Unified configuration for tokenization + syntax node definitions
- **TreeWalker** — DOM-style filtered tree traversal
- **High Performance** — Zero-allocation parsing with SIMD-optimized `SearchValues<char>`
- **Error Recovery** — Gracefully handles malformed input and continues parsing

## Installation

```bash
dotnet add package TinyAst
```

## Quick Start

### TinyAst

```csharp
using TinyTokenizer.Ast;

// Parse source into a syntax tree
var tree = SyntaxTree.Parse("function foo() { return 1; }");

// Query nodes with CSS-like selectors
var idents = tree.Select(Query.AnyIdent);  // [Ident("function"), Ident("foo"), Ident("return")]

// Fluent mutations with undo support
tree.CreateEditor()
    .Replace(Query.Ident("foo"), "bar")
    .Insert(Query.BraceBlock.First().InnerStart(), "console.log('enter');")
    .Commit();

// Undo/redo
tree.Undo();
tree.Redo();
```

### Pattern Matching with Schema

```csharp
var tree = SyntaxTree.Parse("obj.method(x)", Schema.Default);

var methodCalls = tree.Match<MethodCallSyntax>().ToList();  // [MethodCallSyntax { Object="obj", Method="method" }]
```

### Low-Level Tokenization

For scenarios where you don't need a syntax tree:

```csharp
using TinyTokenizer;

var tokens = "func(a, b)".TokenizeToTokens();

// tokens contains:
// - IdentToken("func")
// - BlockToken("(a, b)") with children:
//   - IdentToken("a"), SymbolToken(","), WhitespaceToken(" "), IdentToken("b")
```

## 📚 Documentation

**[Full documentation on the Wiki →](../../wiki)**

### TinyAst

| Topic                                     | Description                |
| ----------------------------------------- | -------------------------- |
| [TinyAst Guide](../../wiki/TinyAst-Guide) | Syntax tree API            |
| [Schema](../../wiki/Schema)               | Unified configuration      |
| [Query API](../../wiki/Query-API)         | CSS-like node selectors    |
| [SyntaxEditor](../../wiki/SyntaxEditor)   | Batch mutations, undo/redo |
| [Syntax Nodes](../../wiki/Syntax-Nodes)   | Pattern-based matching     |
| [TreeWalker](../../wiki/TreeWalker)       | DOM-style traversal        |
| [Trivia](../../wiki/Trivia)               | Whitespace preservation    |

### Low-Level Tokenization

| Topic                                         | Description                     |
| --------------------------------------------- | ------------------------------- |
| [Token Types](../../wiki/Token-Types)         | SimpleToken vs Token, all types |
| [Configuration](../../wiki/Configuration)     | Operators, comments, symbols    |
| [Async Streaming](../../wiki/Async-Streaming) | Stream/PipeReader APIs          |
| [Error Handling](../../wiki/Error-Handling)   | ErrorToken and recovery         |

### Reference

| Topic                                         | Description                       |
| --------------------------------------------- | --------------------------------- |
| [Getting Started](../../wiki/Getting-Started) | Installation and basic usage      |
| [Architecture](../../wiki/Architecture)       | Two-level design, red-green trees |
| [API Reference](../../wiki/API-Reference)     | Types, enums, methods             |

## Requirements

- .NET 8.0 or later
- `CommunityToolkit.HighPerformance` (automatically included)

## License

MIT
