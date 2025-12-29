# TinyTokenizer

A high-performance, zero-allocation tokenizer library for .NET 8+ that parses text into abstract tokens using `ReadOnlySpan<char>` and SIMD-optimized `SearchValues<char>` for maximum efficiency.

## Features

- **Zero-allocation parsing** — Uses `ReadOnlySpan<char>` internally for fast, allocation-free text traversal
- **SIMD-optimized** — Uses .NET 8's `SearchValues<char>` for vectorized character matching
- **Two-level architecture** — Lexer (character classification) + TokenParser (semantic parsing)
- **TinyAst** — Red-green tree with structural sharing, fluent queries, and undo/redo support
- **Schema system** — Unified configuration for tokenization + semantic node definitions
- **Semantic nodes** — Pattern-based AST matching for function calls, property access, etc.
- **TreeWalker** — DOM-style filtered tree traversal inspired by W3C TreeWalker
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

### NodeKind Enum

```csharp
public enum NodeKind
{
    Ident,        // Identifiers
    Whitespace,   // Spaces, tabs, newlines
    Symbol,       // Single characters like , ; :
    Operator,     // Multi-char operators like == !=
    Numeric,      // Numbers (integer and floating-point)
    String,       // Quoted strings
    TaggedIdent,  // #define, @attribute, $var
    BraceBlock,   // { }
    BracketBlock, // [ ]
    ParenBlock,   // ( )
    Error,        // Parsing errors
}
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

### SyntaxEditor Methods

```csharp
public sealed class SyntaxEditor
{
    // Insert text at resolved positions
    SyntaxEditor Insert(InsertionQuery query, string text);

    // Remove all matched nodes
    SyntaxEditor Remove(NodeQuery query);

    // Replace matched nodes with text
    SyntaxEditor Replace(NodeQuery query, string text);
    SyntaxEditor Replace(NodeQuery query, Func<RedNode, string> replacer);

    // Apply or discard
    void Commit();
    void Rollback();
}
```

### Navigation

```csharp
var tree = SyntaxTree.Parse("{ a + b }");

// Positional lookup
var node = tree.FindNodeAt(3);      // Deepest node at position
var leaf = tree.FindLeafAt(3);      // Leaf at position

// Traversal with TreeWalker
var walker = tree.CreateTreeWalker();
foreach (var node in walker.DescendantsAndSelf())
{
    Console.WriteLine($"{node.Kind}: {node.Position}");
}

// From any node
foreach (var child in node.Children) { }
var sibling = node.NextSibling();
var parent = node.Parent;
```

### Node Properties

```csharp
// All nodes
int position = node.Position;       // Start position in source
int endPosition = node.EndPosition; // End position
int width = node.Width;             // Character count
NodeKind kind = node.Kind;

// Leaf nodes
string text = leaf.Text;            // The token text

// Block nodes
char opener = block.Opener;         // '{', '[', or '('
char closer = block.Closer;         // '}', ']', or ')'
int childCount = block.ChildCount;
```

### Undo/Redo

The `SyntaxTree` maintains history automatically:

```csharp
var tree = SyntaxTree.Parse("original");

tree.CreateEditor()
    .Replace(Query.Ident.First(), "modified")
    .Commit();

// tree.ToFullString() == "modified"

tree.Undo();
// tree.ToFullString() == "original"

tree.Redo();
// tree.ToFullString() == "modified"

// Check availability
if (tree.CanUndo) { }
if (tree.CanRedo) { }

// Clear history
tree.ClearHistory();
```

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

// FilterResult options:
// - Accept: Include node in results
// - Skip: Skip node, continue to children  
// - Reject: Skip node and all descendants

var idents = walker.DescendantsAndSelf().ToList();
```

### Navigation Methods

```csharp
var walker = tree.CreateTreeWalker();

// Cursor-based navigation (mutates walker.Current)
walker.NextNode();        // Next in document order
walker.PreviousNode();    // Previous in document order
walker.ParentNode();      // Move to parent
walker.FirstChild();      // Move to first child
walker.LastChild();       // Move to last child
walker.NextSibling();     // Move to next sibling
walker.PreviousSibling(); // Move to previous sibling

// Enumeration (does not mutate Current)
walker.Descendants();       // All descendants
walker.DescendantsAndSelf(); // Root + all descendants
walker.Ancestors();         // Ancestors to root
walker.FollowingSiblings(); // Siblings after current
walker.PrecedingSiblings(); // Siblings before current
```

### NodeFilter Flags

```csharp
[Flags]
public enum NodeFilter
{
    None = 0,
    Leaves = 1 << 0,   // Leaf nodes (idents, operators, etc.)
    Blocks = 1 << 1,   // Block nodes ({ }, [ ], ( ))
    Root = 1 << 2,     // The root list node
    All = Leaves | Blocks | Root
}
```

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

### FunctionNameNode

Captures just the function name (not the arguments):

```csharp
var tree = SyntaxTree.Parse("calculate(a, b)", Schema.Default);
var func = tree.Match<FunctionNameNode>().First();

func.Name;       // "calculate"
func.NameNode;   // The RedLeaf for the identifier
func.Arguments;  // The following ParenBlock (via sibling navigation)
```

### ArrayAccessNode

```csharp
var tree = SyntaxTree.Parse("items[0]", Schema.Default);
var access = tree.Match<ArrayAccessNode>().First();

access.Target;      // "items"
access.IndexBlock;  // The [0] block
```

### PropertyAccessNode

```csharp
var tree = SyntaxTree.Parse("user.name", Schema.Default);
var prop = tree.Match<PropertyAccessNode>().First();

prop.Object;    // "user"
prop.Property;  // "name"
```

### MethodCallNode

```csharp
var tree = SyntaxTree.Parse("list.add(item)", Schema.Default);
var method = tree.Match<MethodCallNode>().First();

method.Object;     // "list"
method.Method;     // "add"
method.Arguments;  // The (item) block
```

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

### Pattern Builder API

The fluent pattern builder provides matchers for AST nodes:

```csharp
var pattern = new PatternBuilder()
    .Ident()                    // Match identifier
    .Symbol(".")                // Match dot symbol
    .Ident()                    // Match identifier
    .ParenBlock()               // Match ( ) block
    .Build();

// Available matchers:
builder.Ident()                 // Any identifier
builder.Ident("specific")       // Specific identifier text
builder.Operator("==")          // Specific operator
builder.Symbol(".")             // Specific symbol
builder.ParenBlock()            // ( ) block
builder.BraceBlock()            // { } block
builder.BracketBlock()          // [ ] block
builder.AnyBlock()              // Any block type
builder.Numeric()               // Number literal
builder.String()                // String literal
builder.Any()                   // Any single node
```

### Pattern Combinators

```csharp
// Sequence: A then B then C
var seq = NodePattern.Sequence(Query.Ident, Query.ParenBlock);

// Alternatives: A or B
var alt = NodePattern.OneOf(pattern1, pattern2);

// Optional: zero or one
var opt = NodePattern.Optional(pattern);

// Repetition
var zeroOrMore = NodePattern.ZeroOrMore(pattern);
var oneOrMore = NodePattern.OneOrMore(pattern);
var exactly3 = NodePattern.Repeat(pattern, 3, 3);

// Lookahead (match A only if followed by B, B not consumed)
var lookahead = new LookaheadPattern(
    new QueryPattern(Query.Ident),      // Match and capture
    new QueryPattern(Query.ParenBlock)  // Must follow (not captured)
);
```

### SemanticContext

Pass context to semantic node factories:

```csharp
using Microsoft.Extensions.Logging;

var context = new SemanticContext
{
    Tree = tree,
    Logger = loggerFactory.CreateLogger("Semantic"),
    StrictMode = true
};

// Register services for dependency injection
context.AddService(mySymbolTable);
context.AddService(myTypeChecker);

// Use in matching
var nodes = tree.Match<FunctionNameNode>(context);

// Access services in node factory
var symbolTable = context.GetService<ISymbolTable>();
var required = context.GetRequiredService<ITypeChecker>(); // throws if missing
```

## Token Types

| Type               | Description                           | Example                        |
| ------------------ | ------------------------------------- | ------------------------------ |
| `IdentToken`       | Identifier/text content               | `hello`, `func`, `_name`       |
| `WhitespaceToken`  | Spaces, tabs, newlines                | ` `, `\t`, `\n`                |
| `SymbolToken`      | Configurable symbol characters        | `/`, `:`, `,`, `;`             |
| `OperatorToken`    | Multi-character operators             | `==`, `!=`, `&&`, `\|\|`, `->` |
| `TaggedIdentToken` | Tag prefix + identifier               | `#define`, `@Override`, `$var` |
| `NumericToken`     | Integer or floating-point numbers     | `123`, `3.14`, `.5`            |
| `StringToken`      | Quoted string literals                | `"hello"`, `'c'`               |
| `CommentToken`     | Single or multi-line comments         | `// comment`, `/* block */`    |
| `BlockToken`       | Declaration blocks with delimiters    | `{...}`, `[...]`, `(...)`      |
| `ErrorToken`       | Parsing errors (unmatched delimiters) | `}` without opening `{`        |

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
CommonOperators.Universal   // +, -, *, /, %, ==, !=, <, >, <=, >=, &&, ||, !, =, +=, -=, *=, /=
CommonOperators.CFamily     // Universal + ++, --, &, |, ^, ~, <<, >>, ->, ::, etc.
CommonOperators.JavaScript  // CFamily + ===, !==, =>, ?., ??, ??=, **
CommonOperators.Python      // Universal + //, **, ->, :=, @, &, |, ^, ~
CommonOperators.Sql         // Universal + <>, ::

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

### Schema

```csharp
public sealed class Schema
{
    // Properties (tokenization settings)
    ImmutableHashSet<char> Symbols { get; }
    ImmutableArray<string> Operators { get; }
    ImmutableHashSet<char> TagPrefixes { get; }
    ImmutableArray<CommentStyle> CommentStyles { get; }
    
    // Semantic definitions
    ImmutableArray<ISemanticNodeDefinition> SortedDefinitions { get; }
    
    // Factory methods
    static SchemaBuilder Create();
    static Schema FromOptions(TokenizerOptions options);
    static Schema Default { get; }
    
    // Query
    SemanticNodeDefinition<T>? GetDefinition<T>() where T : SemanticNode;
    NodeKind GetKind(string definitionName);
    TokenizerOptions ToTokenizerOptions();
}

public sealed class SchemaBuilder
{
    SchemaBuilder WithSymbols(params char[] symbols);
    SchemaBuilder WithOperators(params string[] operators);
    SchemaBuilder WithOperators(IEnumerable<string> operators);
    SchemaBuilder WithCommentStyles(params CommentStyle[] styles);
    SchemaBuilder WithTagPrefixes(params char[] prefixes);
    SchemaBuilder Define<T>(SemanticNodeDefinition<T> definition) where T : SemanticNode;
    Schema Build();
}
```

### TreeWalker

```csharp
public sealed class TreeWalker
{
    // Constructor
    TreeWalker(RedNode root, NodeFilter whatToShow = NodeFilter.All, 
               Func<RedNode, FilterResult>? filter = null);
    
    // Properties
    RedNode Root { get; }
    RedNode Current { get; }
    NodeFilter WhatToShow { get; }
    
    // Cursor navigation (mutates Current)
    RedNode? ParentNode();
    RedNode? FirstChild();
    RedNode? LastChild();
    RedNode? NextSibling();
    RedNode? PreviousSibling();
    RedNode? NextNode();
    RedNode? PreviousNode();
    
    // Enumeration
    IEnumerable<RedNode> Descendants();
    IEnumerable<RedNode> DescendantsAndSelf();
    IEnumerable<RedNode> Ancestors();
    IEnumerable<RedNode> FollowingSiblings();
    IEnumerable<RedNode> PrecedingSiblings();
}

[Flags]
public enum NodeFilter
{
    None = 0,
    Leaves = 1 << 0,
    Blocks = 1 << 1,
    Root = 1 << 2,
    All = Leaves | Blocks | Root
}

public enum FilterResult
{
    Accept,  // Include node
    Skip,    // Skip node, continue to children
    Reject   // Skip node and all descendants
}
```

### SemanticNode

```csharp
public abstract class SemanticNode
{
    // Properties
    NodeKind Kind { get; }
    int Position { get; }
    int Width { get; }
    int PartCount { get; }
    
    // Access matched parts
    T Part<T>(int index) where T : RedNode;
}

// Built-in semantic nodes
public sealed class FunctionNameNode : SemanticNode
{
    RedLeaf NameNode { get; }
    string Name { get; }
    RedBlock? Arguments { get; }  // Via sibling navigation
}

public sealed class ArrayAccessNode : SemanticNode
{
    string Target { get; }
    RedBlock IndexBlock { get; }
}

public sealed class PropertyAccessNode : SemanticNode
{
    string Object { get; }
    string Property { get; }
}

public sealed class MethodCallNode : SemanticNode
{
    string Object { get; }
    string Method { get; }
    RedBlock Arguments { get; }
}
```

### SemanticNodeDefinition

```csharp
public sealed class SemanticNodeDefinition<T> where T : SemanticNode
{
    string Name { get; }
    ImmutableArray<NodePattern> Patterns { get; }
    int Priority { get; }
    
    T? TryCreate(NodeMatch match, SemanticContext? context);
}

// Fluent builder
public static class Semantic
{
    static SemanticNodeDefinitionBuilder<T> Define<T>(string name) where T : SemanticNode;
}

public sealed class SemanticNodeDefinitionBuilder<T>
{
    SemanticNodeDefinitionBuilder<T> Match(Func<PatternBuilder, NodePattern> configure);
    SemanticNodeDefinitionBuilder<T> Match(NodePattern pattern);
    SemanticNodeDefinitionBuilder<T> Create(Func<NodeMatch, NodeKind, T> factory);
    SemanticNodeDefinitionBuilder<T> WithPriority(int priority);
    SemanticNodeDefinition<T> Build();
}
```

### SemanticContext

```csharp
public class SemanticContext
{
    SyntaxTree? Tree { get; init; }
    ILogger Logger { get; init; }  // Defaults to NullLogger.Instance
    bool StrictMode { get; init; }
    
    SemanticContext AddService<T>(T service) where T : class;
    T? GetService<T>() where T : class;
    T GetRequiredService<T>() where T : class;  // Throws if missing
    bool HasService<T>() where T : class;
}
```

### SyntaxTree (Semantic Matching)

```csharp
public class SyntaxTree
{
    // Parse with schema
    static SyntaxTree Parse(string source, Schema schema);
    static SyntaxTree Parse(ReadOnlyMemory<char> source, Schema schema);
    
    // Attached schema
    Schema? Schema { get; }
    
    // Semantic matching
    IEnumerable<T> Match<T>(SemanticContext? context = null) where T : SemanticNode;
    IEnumerable<T> Match<T>(Schema schema, SemanticContext? context = null) where T : SemanticNode;
    IEnumerable<SemanticNode> MatchAll(SemanticContext? context = null);
    IEnumerable<SemanticNode> MatchAll(Schema schema, SemanticContext? context = null);
    
    // TreeWalker creation
    TreeWalker CreateTreeWalker(NodeFilter whatToShow = NodeFilter.All,
                                Func<RedNode, FilterResult>? filter = null);
}
```

## Requirements

- .NET 8.0 or later

## License

MIT
