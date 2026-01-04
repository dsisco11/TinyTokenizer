# TinyTokenizer - Copilot Instructions

## Project Focus

**TinyAst** is the primary API — a red-green syntax tree with fluent queries, pattern matching, and undo/redo editing. The low-level tokenization system is a foundation that most users won't interact with directly.

## TinyAst Architecture

The main entry point is `SyntaxTree.Parse()` in [TinyTokenizer/Ast/](../TinyTokenizer/Ast/):

```csharp
using TinyTokenizer.Ast;

var tree = SyntaxTree.Parse("function foo() { return 1; }");

// Query with CSS-like selectors
var idents = tree.Select(Query.AnyIdent);

// Edit with undo/redo
tree.CreateEditor()
    .Replace(Query.Ident("foo"), "bar")
    .Commit();

tree.Undo();
```

### Red-Green Tree Design

TinyAst uses **red-green trees** (like Roslyn):

- **Green nodes** (internal) — Immutable, position-agnostic, shared across edits
  - `GreenNode`, `GreenLeaf`, `GreenBlock` in [Ast/Green*.cs](../TinyTokenizer/Ast/)
- **Red nodes** (public API) — Navigable wrappers with parent references
  - `RedNode`, `RedLeaf`, `RedBlock` in [Ast/Red*.cs](../TinyTokenizer/Ast/)

### Key TinyAst Types

| Type | File | Description |
|------|------|-------------|
| `SyntaxTree` | [SyntaxTree.cs](../TinyTokenizer/Ast/SyntaxTree.cs) | Main entry point, holds root + undo stack |
| `RedNode` | [RedNode.cs](../TinyTokenizer/Ast/RedNode.cs) | Abstract navigable node |
| `RedLeaf` | [RedLeaf.cs](../TinyTokenizer/Ast/RedLeaf.cs) | Terminal token node |
| `RedBlock` | [RedBlock.cs](../TinyTokenizer/Ast/RedBlock.cs) | Block with children |
| `Query` | [Query.cs](../TinyTokenizer/Ast/Query.cs) | Static factory for selectors |
| `SyntaxEditor` | [SyntaxEditor.cs](../TinyTokenizer/Ast/SyntaxEditor.cs) | Batch mutations |
| `Schema` | [Schema.cs](../TinyTokenizer/Ast/Schema.cs) | Unified config + syntax definitions |
| `TreeWalker` | [TreeWalker.cs](../TinyTokenizer/Ast/TreeWalker.cs) | DOM-style traversal |

### Query Combinators

Queries are built with combinators in [Query.cs](../TinyTokenizer/Ast/Query.cs):

```csharp
Query.Ident("main")                              // Named identifier
Query.AnyIdent                                   // Any identifier
Query.AnyIdent.FollowedBy(Query.ParenBlock)      // Function calls
Query.Sequence(Query.AnyIdent, Query.Symbol("."), Query.AnyIdent)  // Property access
Query.AnyIdent.First()                           // Pseudo-selector
query.Before() / query.After()                   // Insertion positions
```

### Syntax Nodes (Pattern Matching)

For typed AST patterns, use `Schema` with syntax node definitions:

```csharp
var tree = SyntaxTree.Parse(source, Schema.Default);
var methods = tree.Match<MethodCallSyntax>();
```

Custom syntax nodes implement `SyntaxNode` and use `INamedNode` / `IBlockContainerNode` interfaces.

## Low-Level Tokenization

The tokenizer uses a **two-level architecture** (most users don't need this directly):

1. **Level 1 - Lexer** ([Lexer.cs](../TinyTokenizer/Lexer.cs)): Stateless character classifier producing `SimpleToken` structs. Uses SIMD-optimized `SearchValues<char>`.

2. **Level 2 - TokenParser** ([TokenParser.cs](../TinyTokenizer/TokenParser.cs)): Combines simple tokens into semantic `Token` records (blocks, strings, comments, operators).

### Token Types
- **SimpleToken** (`readonly record struct`) - Level 1 output
- **Token** (`abstract record`) - Level 2 base, immutable with `ReadOnlyMemory<char>`
- **BlockToken** - Contains `Children` array for nested tokens

### Options Pattern
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
- For TinyAst, test queries and edits against `SyntaxTree`
- For tokenizer, use `TokenizeTwoLevel()` helper
- Assert token types with `Assert.IsType<IdentToken>()` pattern
- Test error recovery by checking for `ErrorToken` / `NodeKind.Error`

```csharp
// TinyAst test pattern
var tree = SyntaxTree.Parse("foo(x)");
var funcs = tree.Select(Query.AnyIdent.FollowedBy(Query.ParenBlock));
Assert.Single(funcs);

// Tokenizer test pattern
var tokens = Tokenize("func(a, b)");
Assert.Single(tokens.OfTokenType<BlockToken>());
```

## Performance Notes

- Green nodes are shared across edits — only affected paths rebuild
- `Lexer` uses `SearchValues<char>` for SIMD-accelerated classification
- `SimpleToken` is a struct to avoid heap allocations during lexing
- Avoid `ToString()` on tokens unless necessary — use `ContentSpan` for comparisons
