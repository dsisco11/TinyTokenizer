# TinyTokenizer

A high-performance, zero-allocation tokenizer library for .NET 8+ with SIMD-optimized character matching.

## Features

- **High performance** — Zero-allocation parsing with SIMD-optimized `SearchValues<char>`
- **TinyAst** — Red-green syntax tree with fluent queries, editing, and undo/redo
- **Schema system** — Unified configuration for tokenization + semantic node definitions
- **Semantic nodes** — Pattern-based AST matching (function calls, property access, etc.)
- **TreeWalker** — DOM-style filtered tree traversal
- **Async streaming** — Tokenize from `Stream` and `PipeReader` with `IAsyncEnumerable`
- **Full tokenization** — Blocks, strings, numbers, comments, operators, tagged identifiers
- **Error recovery** — Gracefully handles malformed input and continues parsing

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

## TinyAst — Syntax Tree API

TinyTokenizer includes a syntax tree for efficient AST manipulation with fluent queries and undo/redo support.

### Quick Start

```csharp
using TinyTokenizer.Ast;

// Parse source into a syntax tree
var tree = SyntaxTree.Parse("function foo() { return 1; }");

// Query nodes
var idents = tree.Leaves.Where(l => l.Kind == NodeKind.Ident);

// Fluent mutations with undo support
tree.CreateEditor()
    .Replace(Query.Ident.WithText("foo"), "bar")
    .Insert(Query.BraceBlock.First().InnerStart(), "console.log('enter');")
    .Commit();

// Undo/redo
tree.Undo();
tree.Redo();
```

### Querying with NodeQuery

The `Query` static class provides a fluent CSS-like selector API:

```csharp
using TinyTokenizer.Ast;

// By kind
Query.Ident                    // All identifiers
Query.Numeric                  // All numbers
Query.String                   // All strings
Query.Operator                 // All operators

// Blocks
Query.BraceBlock               // All { } blocks
Query.BracketBlock             // All [ ] blocks
Query.ParenBlock               // All ( ) blocks
Query.AnyBlock                 // Any block type

// Filters
Query.Ident.WithText("foo")              // Exact match
Query.Ident.WithTextContaining("test")   // Contains
Query.Ident.WithTextStartingWith("_")    // Prefix
Query.Ident.Where(n => n.Width > 5)      // Custom predicate

// Pseudo-selectors
Query.Ident.First()            // First match only
Query.Ident.Last()             // Last match only
Query.Ident.Nth(2)             // Third match (0-indexed)

// Composition
Query.Ident | Query.Numeric    // Union (OR)
Query.Ident & Query.Leaf       // Intersection (AND)
```

### Insertion Positions

Queries resolve to insertion points with position modifiers:

```csharp
// Relative to matched node
Query.Ident.First().Before()   // Insert before first ident
Query.Ident.First().After()    // Insert after first ident

// Inside blocks
Query.BraceBlock.First().InnerStart()  // After opening {
Query.BraceBlock.First().InnerEnd()    // Before closing }
```

### SyntaxEditor

The `SyntaxEditor` provides batched mutations with atomic commit:

```csharp
var tree = SyntaxTree.Parse("a + b");

var editor = tree.CreateEditor();

// Queue multiple edits
editor
    .Replace(Query.Ident.WithText("a"), "x")
    .Replace(Query.Ident.WithText("b"), "y")
    .Insert(Query.Operator.First().Before(), "(")
    .Insert(Query.Operator.First().After(), ")");

// Apply atomically (supports undo)
editor.Commit();

// Or discard
editor.Rollback();
```

The editor supports `Insert`, `Remove`, and `Replace` operations. All changes can be undone with `tree.Undo()` and redone with `tree.Redo()`.

## Schema — Unified Configuration

The `Schema` class provides unified configuration for both tokenization and semantic node definitions.

### Quick Start

```csharp
using TinyTokenizer.Ast;

// Create a schema with tokenization settings and semantic definitions
var schema = Schema.Create()
    .WithOperators(CommonOperators.JavaScript)
    .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
    .Define(BuiltInDefinitions.FunctionName)
    .Define(BuiltInDefinitions.ArrayAccess)
    .Define(BuiltInDefinitions.PropertyAccess)
    .Define(BuiltInDefinitions.MethodCall)
    .Build();

// Parse with schema
var tree = SyntaxTree.Parse("obj.method(x)", schema);

// Match semantic nodes using the attached schema
var methods = tree.Match<MethodCallNode>().ToList();
```

### Built-in Schema

```csharp
// Schema.Default includes:
// - CommonOperators.Universal
// - C-style single and multi-line comments
// - FunctionName, ArrayAccess, PropertyAccess, MethodCall definitions
var tree = SyntaxTree.Parse(source, Schema.Default);
```

### Converting from TokenizerOptions

```csharp
// Create schema from existing TokenizerOptions
var options = TokenizerOptions.Default
    .WithOperators(CommonOperators.CFamily)
    .WithCommentStyles(CommentStyle.CStyleSingleLine);

var schema = Schema.FromOptions(options);
```

## TreeWalker — DOM-Style Traversal

The `TreeWalker` provides filtered tree traversal similar to the W3C DOM TreeWalker specification.

### Basic Usage

```csharp
var tree = SyntaxTree.Parse("foo { bar(x) }");

// Create walker from tree or node
var walker = tree.CreateTreeWalker();
// or: var walker = new TreeWalker(tree.Root);

// Enumerate all descendants
foreach (var node in walker.DescendantsAndSelf())
{
    Console.WriteLine($"{node.Kind} at {node.Position}");
}
```

### Filtered Traversal

```csharp
// Filter by node type using NodeFilter flags
var leafWalker = new TreeWalker(tree.Root, NodeFilter.Leaves);
foreach (var leaf in leafWalker.DescendantsAndSelf())
{
    // Only leaf nodes (identifiers, operators, etc.)
}

var blockWalker = new TreeWalker(tree.Root, NodeFilter.Blocks);
foreach (var block in blockWalker.DescendantsAndSelf())
{
    // Only block nodes ({ }, [ ], ( ))
}
```

### Custom Filter Functions

```csharp
// Use FilterResult for fine-grained control
var walker = new TreeWalker(
    tree.Root,
    NodeFilter.All,
    node => node.Kind == NodeKind.Ident 
        ? FilterResult.Accept    // Include this node
        : FilterResult.Skip);    // Skip node, but check children

var idents = walker.DescendantsAndSelf().ToList();
```

The walker also provides cursor-based navigation (`NextNode`, `ParentNode`, `FirstChild`, etc.) and enumeration methods (`Descendants`, `Ancestors`, `FollowingSiblings`).

## Semantic Nodes — AST Pattern Matching

Semantic nodes provide a way to match structural patterns in the AST and create typed wrapper objects.

### Quick Start

```csharp
using TinyTokenizer.Ast;

// Parse with schema
var tree = SyntaxTree.Parse("foo(x) + bar.baz", Schema.Default);

// Find all function names (identifiers followed by parentheses)
var funcNames = tree.Match<FunctionNameNode>().ToList();
// funcNames[0].Name == "foo"

// Find all property accesses
var props = tree.Match<PropertyAccessNode>().ToList();
// props[0].Object == "bar", props[0].Property == "baz"
```

### Built-in Semantic Nodes

| Type | Pattern | Example |
|------|---------|---------|
| `FunctionNameNode` | `Ident(?=ParenBlock)` | `foo` in `foo(x)` |
| `ArrayAccessNode` | `Ident + BracketBlock` | `arr[0]` |
| `PropertyAccessNode` | `Ident + "." + Ident` | `obj.prop` |
| `MethodCallNode` | `Ident + "." + Ident + ParenBlock` | `obj.method(x)` |

### Custom Semantic Nodes

Define your own semantic node types:

```csharp
// 1. Define the node class
public sealed class LambdaNode : SemanticNode
{
    public LambdaNode(NodeMatch match, NodeKind kind) : base(match, kind) { }
    
    public RedBlock Parameters => Part<RedBlock>(0);
    public RedBlock Body => Part<RedBlock>(2);
}

// 2. Create a definition with pattern
var lambdaDef = Semantic.Define<LambdaNode>("Lambda")
    .Match(p => p.ParenBlock().Operator("=>").BraceBlock())
    .Create((match, kind) => new LambdaNode(match, kind))
    .WithPriority(15)
    .Build();

// 3. Add to schema
var schema = Schema.Create()
    .WithOperators(CommonOperators.JavaScript)
    .Define(lambdaDef)
    .Build();

// 4. Match
var tree = SyntaxTree.Parse("(x) => { return x; }", schema);
var lambdas = tree.Match<LambdaNode>().ToList();
```

### Pattern Builder Matchers

| Matcher | Description |
|---------|-------------|
| `Ident()` | Any identifier |
| `Ident("name")` | Specific identifier |
| `Operator("==")` | Specific operator |
| `Symbol(".")` | Specific symbol |
| `ParenBlock()` | `( )` block |
| `BraceBlock()` | `{ }` block |
| `BracketBlock()` | `[ ]` block |
| `Numeric()` | Number literal |
| `String()` | String literal |
| `Any()` | Any single node |
| `Sequence(...)` | Match A then B then C |
| `OneOf(...)` | Match A or B |
| `Optional(...)` | Match zero or one |
| `ZeroOrMore(...)` | Match zero or more |
| `OneOrMore(...)` | Match one or more |
| `LookaheadPattern` | Match A only if followed by B |

## Async Tokenization

```csharp
// From Stream
await using var stream = File.OpenRead("source.txt");
var tokens = await stream.TokenizeAsync();

// Streaming with IAsyncEnumerable
await foreach (var token in stream.TokenizeStreamingAsync())
{
    Console.WriteLine(token);
}
```

Also supports `PipeReader` and custom encoding options.

## Error Handling

The tokenizer produces `ErrorToken` for malformed input and continues parsing:

```csharp
var tree = SyntaxTree.Parse("}hello{");

// Query for error nodes
var errors = tree.Root.Children
    .Where(n => n.Kind == NodeKind.Error)
    .Cast<RedLeaf>();

foreach (var error in errors)
{
    Console.WriteLine($"Error at {error.Position}: {error.Text}");
}
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

### Core Types

```csharp
// Parse source into syntax tree (recommended)
var tree = SyntaxTree.Parse(source, Schema.Default);

// Low-level tokenization (if needed)
var tokens = source.TokenizeToTokens(options);

// Async streaming from files
await foreach (var token in stream.TokenizeStreamingAsync()) { }
```

### Schema Configuration

```csharp
// Built-in operator sets
CommonOperators.Universal    // Basic: ==, !=, &&, ||, etc.
CommonOperators.CFamily      // C/C++: ++, --, ->, ::, etc.
CommonOperators.JavaScript   // JS: ===, =>, ?., ??, etc.

// Built-in comment styles
CommentStyle.CStyleSingleLine   // //
CommentStyle.CStyleMultiLine    // /* */
CommentStyle.HashSingleLine     // #

// Create custom schema
var schema = Schema.Create()
    .WithOperators(CommonOperators.JavaScript)
    .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
    .WithTagPrefixes('#', '@')
    .Define(BuiltInDefinitions.FunctionName)
    .Build();
```

### NodeKind Values

| Kind | Description | Example |
|------|-------------|---------|
| `Ident` | Identifiers | `foo`, `myVar` |
| `Whitespace` | Spaces, tabs, newlines | ` `, `\n` |
| `Symbol` | Single characters | `,`, `;`, `:` |
| `Operator` | Multi-char operators | `==`, `!=`, `=>` |
| `Numeric` | Numbers | `123`, `3.14` |
| `String` | Quoted strings | `"hello"` |
| `TaggedIdent` | Prefixed identifiers | `#define`, `@attr` |
| `BraceBlock` | Curly braces | `{ }` |
| `BracketBlock` | Square brackets | `[ ]` |
| `ParenBlock` | Parentheses | `( )` |
| `Error` | Parse errors | unmatched `}` |

## Requirements

- .NET 8.0 or later

## License

MIT
