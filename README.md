# TinyTokenizer

A high-performance, zero-allocation tokenizer library for .NET 8+ with SIMD-optimized character matching.

## Features

- **High performance** — Zero-allocation parsing with SIMD-optimized `SearchValues<char>`
- **TinyAst** — Red-green syntax tree with fluent queries, editing, and undo/redo
- **Schema system** — Unified configuration for tokenization + syntax node definitions
- **Syntax nodes** — Pattern-based AST matching (function calls, property access, etc.)
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

// Boundary assertions
Query.BOF                      // Beginning of file (first token at root)
Query.EOF                      // End of file (last token at root)

// Filters (use .WithText* when you need to filter an any-kind query)
Query.AnyIdent.WithText("foo")              // Same as Query.Ident("foo") but on any-kind
Query.AnyIdent.WithTextContaining("test")   // Contains substring
Query.AnyIdent.WithTextStartingWith("_")    // Starts with prefix
Query.AnyIdent.Where(n => n.Width > 5)      // Custom predicate

// Pseudo-selectors
Query.AnyIdent.First()         // First match only
Query.AnyIdent.Last()          // Last match only
Query.AnyIdent.Nth(2)          // Third match (0-indexed)

// Composition
Query.AnyIdent | Query.AnyNumeric    // Union (OR)
Query.AnyIdent & Query.Leaf          // Intersection (AND)
Query.AnyOf(Query.AnyIdent, Query.AnyNumeric, Query.AnyString)  // Variadic OR
Query.NoneOf(Query.Ident("if"), Query.Ident("else"))           // Match when none match

// Negation (zero-width)
Query.Not(Query.Ident("if"))         // Negative lookahead assertion
Query.AnyIdent.Not()                 // Fluent syntax

// Content matching
Query.Between(Query.Operator("<"), Query.Operator(">"))  // Match between delimiters

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

// Navigation queries (zero-width)
Query.Sibling(1)                       // Next sibling exists
Query.Sibling(-1)                      // Previous sibling exists
Query.Sibling(1, Query.AnyIdent)       // Next sibling is identifier
Query.AnyIdent.NextSibling()           // Fluent: check next sibling
Query.AnyIdent.PreviousSibling()       // Fluent: check previous sibling
Query.Parent(Query.BraceBlock)         // Parent is a brace block
Query.Ancestor(Query.BraceBlock)       // Any ancestor is a brace block
Query.BraceBlock.AsParent()            // Fluent: match nodes with this parent
Query.BraceBlock.AsAncestor()          // Fluent: match nodes with this ancestor

// Exact node reference (when you have a specific RedNode)
Query.Exact(myRedNode)           // Match this specific node instance
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

The `SyntaxEditor` provides batched mutations with atomic commit. You can work directly with `RedNode` references or use query-based selection.

**Working with RedNode references (preferred for known nodes):**

```csharp
var tree = SyntaxTree.Parse("a + b");
var idents = tree.Select(Query.AnyIdent).ToList();

tree.CreateEditor()
    .Replace(idents[0], "x")                 // Replace specific node
    .Replace(idents[1], "y")                 // Replace another node
    .InsertBefore(idents[0], "(")            // Insert before node
    .InsertAfter(idents[1], ")")             // Insert after node
    .Commit();
// Result: "(x + y)"
```

**Working with queries (for pattern matching):**

```csharp
var tree = SyntaxTree.Parse("a + b");

tree.CreateEditor()
    .Replace(Query.Ident("a"), "x")          // Replace by query
    .Replace(Query.Ident("b"), "y")
    .Insert(Query.AnyOperator.First().Before(), "(")
    .Insert(Query.AnyOperator.First().After(), ")")
    .Commit();
// Result: "(x + y)"
```

**Batch operations on multiple nodes:**

```csharp
var tree = SyntaxTree.Parse("a b c");
var idents = tree.Select(Query.AnyIdent);

tree.CreateEditor()
    .Replace(idents, "X")                    // Replace all at once
    .Commit();
// Result: "X X X"
```

### Common SyntaxEditor Patterns

**Insert around a node (using RedNode directly):**

```csharp
var tree = SyntaxTree.Parse("function {body}");
var block = tree.Select(Query.BraceBlock.First()).Single();

tree.CreateEditor()
    .InsertBefore(block, "/* decorator */ ")
    .InsertAfter(block, " // end")
    .Commit();
// Result: "function /* decorator */ {body} // end"
```

**Insert inside blocks (using queries for position):**

```csharp
var tree = SyntaxTree.Parse("function {existing}");

tree.CreateEditor()
    .Insert(Query.BraceBlock.First().InnerStart(), "console.log('enter'); ")
    .Insert(Query.BraceBlock.First().InnerEnd(), " return result;")
    .Commit();
// Result: "function {console.log('enter'); existing return result;}"
```

**Replace with transformation:**

```csharp
var tree = SyntaxTree.Parse("hello world");
var idents = tree.Select(Query.AnyIdent);

tree.CreateEditor()
    .Replace(idents, n => ((RedLeaf)n).Text.ToUpper())
    .Commit();
// Result: "HELLO WORLD"
```

**Edit content (preserves trivia automatically):**

The `Edit` methods transform node content while automatically preserving surrounding whitespace and comments. Unlike `Replace`, the transformer receives only the content string (without trivia):

```csharp
var tree = SyntaxTree.Parse("  hello   world  ");

// Edit: transformer receives "hello" and "world" (no trivia)
tree.CreateEditor()
    .Edit(Query.AnyIdent, content => content.ToUpper())
    .Commit();
// Result: "  HELLO   WORLD  " (whitespace preserved)

// Works with any transformation
tree.CreateEditor()
    .Edit(Query.AnyNumeric, content => (int.Parse(content) * 2).ToString())
    .Commit();

// Query-based or node-based
var node = tree.Select(Query.Ident("foo")).Single();
tree.CreateEditor()
    .Edit(node, content => $"[{content}]")
    .Commit();
```

**Edit vs Replace:**
- `Replace(query, node => ...)` — transformer receives full `RedNode`, you handle trivia
- `Edit(query, content => ...)` — transformer receives content string only, trivia auto-preserved

**Replace with another node:**

```csharp
var tree = SyntaxTree.Parse("old");
var oldNode = tree.Select(Query.Ident("old")).Single();

// From another tree
var sourceTree = SyntaxTree.Parse("new");
var newNode = sourceTree.Select(Query.Ident("new")).Single();

tree.CreateEditor()
    .Replace(oldNode, newNode)               // Copy node from another tree
    .Commit();
// Result: "new"
```

**Remove specific nodes:**

```csharp
var tree = SyntaxTree.Parse("keep remove keep");
var toRemove = tree.Select(Query.Ident("remove")).Single();

tree.CreateEditor()
    .Remove(toRemove)                        // Remove specific node
    .Commit();
// Result: "keep  keep"
```

**Batch operations with multiple queries:**

```csharp
var tree = SyntaxTree.Parse("foo bar baz");
var queries = new[] { Query.Ident("foo"), Query.Ident("baz") };

tree.CreateEditor()
    .Replace(queries, "X")                   // Replace all matches
    .Commit();
// Result: "X bar X"
```

**Use Query.Exact for precise targeting:**

```csharp
var tree = SyntaxTree.Parse("a b c");
var bNode = tree.Select(Query.Ident("b")).Single();

// Use Query.Exact when you need a query but have a specific node
tree.CreateEditor()
    .Replace(Query.Exact(bNode), "X")        // Same as .Replace(bNode, "X")
    .Commit();
// Result: "a X c"
```

The editor supports `Insert`, `InsertBefore`, `InsertAfter`, `Remove`, `Replace`, and `Edit` operations. All changes can be undone with `tree.Undo()` and redone with `tree.Redo()`.

## Schema — Unified Configuration

The `Schema` class provides unified configuration for both tokenization and syntax node definitions.

### Quick Start

```csharp
using TinyTokenizer.Ast;

// Create a schema with tokenization settings and syntax definitions
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

// Match syntax nodes using the attached schema
var methods = tree.Match<MethodCallSyntax>().ToList();

// Check if tree has a schema attached
if (tree.HasSchema)
{
    // Safe to use schema-dependent operations
}

// Attach schema to an existing tree
var boundTree = tree.WithSchema(schema);
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

## Trivia — Whitespace and Comments

Trivia (whitespace, newlines, and comments) attached to tokens is accessible via `RedLeaf` nodes:

```csharp
var tree = SyntaxTree.Parse("x = 1; // comment\n");
var leaf = tree.Leaves.First();

// Access leading trivia (before the token)
foreach (var trivia in leaf.LeadingTrivia)
{
    Console.WriteLine($"{trivia.Kind}: '{trivia.Text}'");
}

// Access trailing trivia (after the token, same line)
foreach (var trivia in leaf.TrailingTrivia)
{
    Console.WriteLine($"{trivia.Kind}: '{trivia.Text}'");
}
```

### TriviaKind Values

| Kind                 | Description              | Example    |
| -------------------- | ------------------------ | ---------- |
| `Whitespace`         | Spaces and tabs          | `   `      |
| `Newline`            | Line endings             | `\n`       |
| `SingleLineComment`  | Single-line comments     | `// ...`   |
| `MultiLineComment`   | Multi-line comments      | `/* ... */`|

## Syntax Nodes — AST Pattern Matching

Syntax nodes provide a way to match structural patterns in the AST and create typed wrapper objects.

### Quick Start

```csharp
using TinyTokenizer.Ast;

// Parse with schema
var tree = SyntaxTree.Parse("foo(x) + bar.baz", Schema.Default);

// Find all function calls (identifiers followed by parentheses)
var funcCalls = tree.Match<FunctionCallSyntax>().ToList();
// funcCalls[0].Name == "foo"

// Find all property accesses
var props = tree.Match<PropertyAccessSyntax>().ToList();
// props[0].Object == "bar", props[0].Property == "baz"
```

### Built-in Syntax Nodes

| Type                   | Query Pattern                                                              | Example         |
| ---------------------- | -------------------------------------------------------------------------- | --------------- |
| `FunctionCallSyntax`   | `Query.Ident.FollowedBy(Query.ParenBlock)`                                 | `foo(x)`        |
| `ArrayAccessSyntax`    | `Query.Sequence(Query.Ident, Query.BracketBlock)`                          | `arr[0]`        |
| `PropertyAccessSyntax` | `Query.Sequence(Query.Ident, Query.Symbol, Query.Ident)`                   | `obj.prop`      |
| `MethodCallSyntax`     | `Query.Sequence(Query.Ident, Query.Symbol, Query.Ident, Query.ParenBlock)` | `obj.method(x)` |

### Custom Syntax Nodes

Define your own syntax node types:

```csharp
// 1. Define the node class
public sealed class LambdaSyntax : SyntaxNode
{
    // Constructor receives an opaque CreationContext - just pass it to base
    protected LambdaSyntax(CreationContext context)
        : base(context) { }

    // Access child nodes by index (determined by the pattern)
    public RedBlock Parameters => GetTypedChild<RedBlock>(0);
    public RedLeaf Arrow => GetTypedChild<RedLeaf>(1);
    public RedBlock Body => GetTypedChild<RedBlock>(2);
}

// 2. Create a definition with pattern using Query combinators
var lambdaDef = Syntax.Define<LambdaSyntax>("Lambda")
    .Match(Query.Sequence(Query.ParenBlock, Query.Operator("=>"), Query.BraceBlock))
    .WithPriority(15)
    .Build();

// 3. Add to schema
var schema = Schema.Create()
    .WithOperators(CommonOperators.JavaScript)
    .DefineSyntax(lambdaDef)
    .Build();

// 4. Match
var tree = SyntaxTree.Parse("(x) => { return x; }", schema);
var lambdas = tree.Match<LambdaSyntax>().ToList();
```

### INamedNode and IBlockContainerNode

Implement these interfaces for enhanced querying capabilities:

```csharp
// A function syntax node with named access and block containers
public sealed class FunctionSyntax : SyntaxNode, INamedNode, IBlockContainerNode
{
    protected FunctionSyntax(CreationContext context)
        : base(context) { }

    // INamedNode - enables Query.Syntax<T>().Named("foo")
    public string Name => GetTypedChild<RedLeaf>(1).Text;

    // IBlockContainerNode - enables .InnerStart("body"), .InnerEnd("params")
    public IReadOnlyList<string> BlockNames => ["body", "params"];
    
    public RedBlock GetBlock(string? name = null) => name switch
    {
        null or "body" => GetTypedChild<RedBlock>(3),  // { }
        "params" => GetTypedChild<RedBlock>(2),        // ( )
        _ => throw new ArgumentException($"Unknown block: {name}")
    };
}

// Usage with fluent queries
var mainQuery = Query.Syntax<FunctionSyntax>().Named("main");

tree.CreateEditor()
    .Insert(mainQuery.Before(), "// Entry point\n")
    .Insert(mainQuery.InnerStart("body"), "\n    console.log('enter');")
    .Insert(mainQuery.InnerEnd("body"), "\n    console.log('exit');")
    .Commit();
```

### Query Combinators Reference

| Combinator             | Description              | Example                                            |
| ---------------------- | ------------------------ | -------------------------------------------------- |
| `Query.Ident("x")`     | Specific identifier      | `Query.Ident("main")`                              |
| `Query.Symbol(".")`    | Specific symbol          | `Query.Symbol(".")`                                |
| `Query.Operator("=>")` | Specific operator        | `Query.Operator("=>")`                             |
| `Query.Numeric("42")`  | Specific number          | `Query.Numeric("3.14")`                            |
| `Query.AnyIdent`       | Any identifier           | `Query.AnyIdent`                                   |
| `Query.AnySymbol`      | Any symbol               | `Query.AnySymbol`                                  |
| `Query.AnyOperator`    | Any operator             | `Query.AnyOperator`                                |
| `Query.AnyNumeric`     | Any number literal       | `Query.AnyNumeric`                                 |
| `Query.AnyString`      | Any string literal       | `Query.AnyString`                                  |
| `Query.AnyTaggedIdent` | Any tagged identifier    | `Query.AnyTaggedIdent`                             |
| `Query.ParenBlock`     | `( )` block              | `Query.ParenBlock`                                 |
| `Query.BraceBlock`     | `{ }` block              | `Query.BraceBlock`                                 |
| `Query.BracketBlock`   | `[ ]` block              | `Query.BracketBlock`                               |
| `Query.Any`            | Any single node          | `Query.Any`                                        |
| `Query.Newline`        | Node preceded by newline | `Query.Newline`                                    |
| `Query.Sequence(...)`  | Match A then B then C    | `Query.Sequence(Query.AnyIdent, Query.ParenBlock)` |
| `a \| b`               | Match A or B (union)     | `Query.AnyIdent \| Query.AnyNumeric`               |
| `.Optional()`          | Match zero or one        | `Query.AnyOperator.Optional()`                     |
| `.ZeroOrMore()`        | Match zero or more       | `Query.AnyIdent.ZeroOrMore()`                      |
| `.OneOrMore()`         | Match one or more        | `Query.AnyIdent.OneOrMore()`                       |
| `.Exactly(n)`          | Match exactly n          | `Query.AnyIdent.Exactly(3)`                        |
| `.Repeat(min, max)`    | Match min to max         | `Query.AnyIdent.Repeat(2, 5)`                      |
| `.Until(terminator)`   | Repeat until terminator  | `Query.Any.Until(Query.Newline)`                   |
| `.FollowedBy(q)`       | Positive lookahead       | `Query.AnyIdent.FollowedBy(Query.ParenBlock)`      |
| `.NotFollowedBy(q)`    | Negative lookahead       | `Query.AnyIdent.NotFollowedBy(Query.ParenBlock)`   |
| `.Then(q)`             | Fluent sequence          | `Query.AnyIdent.Then(Query.ParenBlock)`            |
| `Query.Exact(node)`    | Exact node reference     | `Query.Exact(myRedNode)`                           |

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

## API Reference

### Core Types

```csharp
// Parse source into syntax tree (recommended)
var tree = SyntaxTree.Parse(source, Schema.Default);

// Low-level tokenization (if needed)
var tokens = source.TokenizeToTokens(options);

// Async streaming from files
await foreach (var token in stream.TokenizeStreamingAsync()) { }

// Token formatting (IFormattable)
var token = tokens.First();
$"{token:G}"  // Content (default)
$"{token:T}"  // Type name
$"{token:P}"  // Position
$"{token:R}"  // Range (start..end)
$"{token:D}"  // Debug format: Type[start..end]
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
- `CommunityToolkit.HighPerformance` (automatically included)

### Limitations

- Maximum file size: ~2GB (positions are stored as `int`)

## License

MIT
