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
    .Replace(Query.Ident("foo"), "bar")                                  // Concise named query
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

// Named queries (with specific text) - NEW concise API!
Query.Ident("main")            // Identifier with text "main"
Query.Symbol(".")              // Dot symbol
Query.Operator("=>")           // Arrow operator
Query.Numeric("42")            // Number literal "42"

// Any-kind queries (match any of that kind)
Query.AnyIdent                 // All identifiers
Query.AnyNumeric               // All numbers
Query.AnyString                // All strings
Query.AnyOperator              // All operators
Query.AnySymbol                // All symbols
Query.AnyTaggedIdent           // All tagged identifiers

// Blocks
Query.BraceBlock               // All { } blocks
Query.BracketBlock             // All [ ] blocks
Query.ParenBlock               // All ( ) blocks
Query.AnyBlock                 // Any block type

// Filters on any-kind queries
Query.AnyIdent.WithText("foo")              // Exact match (same as Query.Ident("foo"))
Query.AnyIdent.WithTextContaining("test")   // Contains
Query.AnyIdent.WithTextStartingWith("_")    // Prefix
Query.AnyIdent.Where(n => n.Width > 5)      // Custom predicate

// Pseudo-selectors
Query.AnyIdent.First()         // First match only
Query.AnyIdent.Last()          // Last match only
Query.AnyIdent.Nth(2)          // Third match (0-indexed)

// Composition
Query.AnyIdent | Query.AnyNumeric    // Union (OR)
Query.AnyIdent & Query.Leaf          // Intersection (AND)

// Sequence combinators
Query.Sequence(Query.AnyIdent, Query.ParenBlock)  // Match ident then paren block
Query.AnyIdent.Then(Query.ParenBlock)             // Fluent chaining

// Repetition combinators
Query.AnyIdent.Optional()        // Match 0 or 1
Query.AnyIdent.ZeroOrMore()      // Match 0+
Query.AnyIdent.OneOrMore()       // Match 1+
Query.AnyIdent.Exactly(3)        // Match exactly 3
Query.AnyIdent.Repeat(2, 5)      // Match 2 to 5
Query.Any.Until(Query.Newline)   // Repeat until terminator (not consumed)

// Lookahead assertions
Query.AnyIdent.FollowedBy(Query.ParenBlock)     // Positive lookahead
Query.Ident.NotFollowedBy(Query.ParenBlock)  // Negative lookahead
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

### Named Node Queries (INamedNode)

Syntax nodes that implement `INamedNode` can be queried by name:

```csharp
// Find functions by name
Query.Syntax<GlslFunctionSyntax>().Named("main")
Query.Syntax<GlslDirectiveSyntax>().Named("version")

// Use with insertion positions
tree.CreateEditor()
    .Insert(Query.Syntax<MyFunctionSyntax>().Named("foo").Before(), "// comment\n")
    .Commit();
```

### Block Container Queries (IBlockContainerNode)

Syntax nodes that implement `IBlockContainerNode` expose named blocks for injection:

```csharp
// Insert into a named block of a syntax node
var mainQuery = Query.Syntax<GlslFunctionSyntax>().Named("main");

tree.CreateEditor()
    .Insert(mainQuery.InnerStart("body"), "\n    // entry")   // Start of body block
    .Insert(mainQuery.InnerEnd("body"), "\n    // exit")     // End of body block
    .Insert(mainQuery.InnerStart("params"), "int x")        // Start of params block
    .Commit();
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

### Common SyntaxEditor Patterns

**Insert before a function/block:**

```csharp
var tree = SyntaxTree.Parse("function {body}");

tree.CreateEditor()
    .Insert(Query.BraceBlock.First().Before(), "/* decorator */ ")
    .Commit();
// Result: "function /* decorator */ {body}"
```

**Insert at the start of a function body (after opening brace):**

```csharp
var tree = SyntaxTree.Parse("function {existing}");

tree.CreateEditor()
    .Insert(Query.BraceBlock.First().InnerStart(), "console.log('enter'); ")
    .Commit();
// Result: "function {console.log('enter'); existing}"
```

**Insert at the end of a function body (before closing brace):**

```csharp
var tree = SyntaxTree.Parse("function {existing}");

tree.CreateEditor()
    .Insert(Query.BraceBlock.First().InnerEnd(), " return result;")
    .Commit();
// Result: "function {existing return result;}"
```

**Insert after a function/block:**

```csharp
var tree = SyntaxTree.Parse("function {body}");

tree.CreateEditor()
    .Insert(Query.BraceBlock.First().After(), " // end of function")
    .Commit();
// Result: "function {body} // end of function"
```

**Multiple insertions in one commit:**

```csharp
var tree = SyntaxTree.Parse("fn {body}");

tree.CreateEditor()
    .Insert(Query.BraceBlock.First().Before(), "/* before */ ")
    .Insert(Query.BraceBlock.First().InnerStart(), "start(); ")
    .Insert(Query.BraceBlock.First().InnerEnd(), " end();")
    .Insert(Query.BraceBlock.First().After(), " /* after */")
    .Commit();
// Result: "fn /* before */ {start(); body end();} /* after */"
```

**Replace multiple occurrences:**

```csharp
var tree = SyntaxTree.Parse("a + b + a");

tree.CreateEditor()
    .Replace(Query.Ident.WithText("a"), "x")  // Replaces ALL 'a' with 'x'
    .Commit();
// Result: "x + b + x"
```

**Remove nodes:**

```csharp
var tree = SyntaxTree.Parse("keep remove keep");

tree.CreateEditor()
    .Remove(Query.Ident.WithText("remove"))
    .Commit();
// Result: "keep  keep"
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

| Type                 | Query Pattern                                                              | Example           |
| -------------------- | -------------------------------------------------------------------------- | ----------------- |
| `FunctionNameNode`   | `Query.Ident.FollowedBy(Query.ParenBlock)`                                 | `foo` in `foo(x)` |
| `ArrayAccessNode`    | `Query.Sequence(Query.Ident, Query.BracketBlock)`                          | `arr[0]`          |
| `PropertyAccessNode` | `Query.Sequence(Query.Ident, Query.Symbol, Query.Ident)`                   | `obj.prop`        |
| `MethodCallNode`     | `Query.Sequence(Query.Ident, Query.Symbol, Query.Ident, Query.ParenBlock)` | `obj.method(x)`   |

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

// 2. Create a definition with pattern using Query combinators
var lambdaDef = Semantic.Define<LambdaNode>("Lambda")
    .Match(Query.Sequence(Query.ParenBlock, Query.Operator.WithText("=>"), Query.BraceBlock))
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

### Query Combinators Reference

| Combinator                      | Description                 | Example                                                 |
| ------------------------------- | --------------------------- | ------------------------------------------------------- |
| `Query.Ident("x")`              | Specific identifier         | `Query.Ident("main")`                                   |
| `Query.Symbol(".")`             | Specific symbol             | `Query.Symbol(".")`                                     |
| `Query.Operator("=>")`          | Specific operator           | `Query.Operator("=>")`                                  |
| `Query.Numeric("42")`           | Specific number             | `Query.Numeric("3.14")`                                 |
| `Query.AnyIdent`                | Any identifier              | `Query.AnyIdent`                                        |
| `Query.AnySymbol`               | Any symbol                  | `Query.AnySymbol`                                       |
| `Query.AnyOperator`             | Any operator                | `Query.AnyOperator`                                     |
| `Query.AnyNumeric`              | Any number literal          | `Query.AnyNumeric`                                      |
| `Query.AnyString`               | Any string literal          | `Query.AnyString`                                       |
| `Query.AnyTaggedIdent`          | Any tagged identifier       | `Query.AnyTaggedIdent`                                  |
| `Query.ParenBlock`              | `( )` block                 | `Query.ParenBlock`                                      |
| `Query.BraceBlock`              | `{ }` block                 | `Query.BraceBlock`                                      |
| `Query.BracketBlock`            | `[ ]` block                 | `Query.BracketBlock`                                    |
| `Query.Any`                     | Any single node             | `Query.Any`                                             |
| `Query.Newline`                 | Node preceded by newline    | `Query.Newline`                                         |
| `Query.Sequence(...)`           | Match A then B then C       | `Query.Sequence(Query.AnyIdent, Query.ParenBlock)`      |
| `a \| b`                        | Match A or B (union)        | `Query.AnyIdent \| Query.AnyNumeric`                    |
| `.Optional()`                   | Match zero or one           | `Query.AnyOperator.Optional()`                          |
| `.ZeroOrMore()`                 | Match zero or more          | `Query.AnyIdent.ZeroOrMore()`                           |
| `.OneOrMore()`                  | Match one or more           | `Query.AnyIdent.OneOrMore()`                            |
| `.Exactly(n)`                   | Match exactly n             | `Query.AnyIdent.Exactly(3)`                             |
| `.Repeat(min, max)`             | Match min to max            | `Query.AnyIdent.Repeat(2, 5)`                           |
| `.Until(terminator)`            | Repeat until terminator     | `Query.Any.Until(Query.Newline)`                        |
| `.FollowedBy(q)`                | Positive lookahead          | `Query.AnyIdent.FollowedBy(Query.ParenBlock)`           |
| `.NotFollowedBy(q)`             | Negative lookahead          | `Query.AnyIdent.NotFollowedBy(Query.ParenBlock)`        |
| `.Then(q)`                      | Fluent sequence             | `Query.AnyIdent.Then(Query.ParenBlock)`                 |

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

| Kind           | Description            | Example            |
| -------------- | ---------------------- | ------------------ |
| `Ident`        | Identifiers            | `foo`, `myVar`     |
| `Whitespace`   | Spaces, tabs, newlines | ` `, `\n`          |
| `Symbol`       | Single characters      | `,`, `;`, `:`      |
| `Operator`     | Multi-char operators   | `==`, `!=`, `=>`   |
| `Numeric`      | Numbers                | `123`, `3.14`      |
| `String`       | Quoted strings         | `"hello"`          |
| `TaggedIdent`  | Prefixed identifiers   | `#define`, `@attr` |
| `BraceBlock`   | Curly braces           | `{ }`              |
| `BracketBlock` | Square brackets        | `[ ]`              |
| `ParenBlock`   | Parentheses            | `( )`              |
| `Error`        | Parse errors           | unmatched `}`      |

## Requirements

- .NET 8.0 or later

## License

MIT
