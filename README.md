# TinyTokenizer

A high-performance, zero-allocation tokenizer library for .NET 8+ that parses text into abstract tokens using `ReadOnlySpan<char>` and SIMD-optimized `SearchValues<char>` for maximum efficiency.

## Features

- **Zero-allocation parsing** — Uses `ReadOnlySpan<char>` internally for fast, allocation-free text traversal
- **SIMD-optimized** — Uses .NET 8's `SearchValues<char>` for vectorized character matching
- **Two-level architecture** — Lexer (character classification) + TokenParser (semantic parsing)
- **TinyAst** — Red-green tree with structural sharing, fluent queries, and undo/redo support
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
- **Custom tokens (Level 3)** — Pattern-based matching to create composite tokens like function calls, property access, etc.

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

## Custom Tokens (Level 3) — Pattern Matching

TinyTokenizer supports a third level of tokenization that matches token sequences against patterns and creates composite tokens. This is useful for recognizing higher-level constructs like function calls, property access, type annotations, and more.

### Quick Start

```csharp
using TinyTokenizer;

// Parse and apply patterns
var tokens = "obj.method(x, y)".TokenizeToTokens();
var result = tokens.ApplyPatterns(
    TokenDefinitions.PropertyAccess(),
    TokenDefinitions.FunctionCall());

// result contains composite tokens:
// - PropertyAccessToken (obj.method) 
// - or FunctionCallToken depending on pattern priority
```

### Built-in Pattern Definitions

```csharp
// Function call: Ident + ParenBlock  →  func(args)
TokenDefinitions.FunctionCall()

// Property access: Ident + '.' + Ident  →  obj.property
TokenDefinitions.PropertyAccess()

// Type annotation: Ident + ':' + Ident  →  param: string
TokenDefinitions.TypeAnnotation()

// Assignment: Ident + '=' + Any  →  x = value
TokenDefinitions.Assignment()

// Array access: Ident + BracketBlock  →  arr[index]
TokenDefinitions.ArrayAccess()
```

### Custom Pattern Definitions

Create your own patterns using `TokenDefinition<T>` and the `Match` factory:

```csharp
// Define a custom composite token type
public sealed record MethodCallToken : CompositeToken
{
    public ReadOnlySpan<char> ObjectSpan => MatchedTokens[0].ContentSpan;
    public ReadOnlySpan<char> MethodSpan => MatchedTokens[2].ContentSpan;
}

// Create a pattern definition
var methodCall = new TokenDefinition<MethodCallToken>
{
    Name = "MethodCall",
    Patterns = [[Match.Ident(), Match.Symbol('.'), Match.Ident(), Match.Block('(')]],
    SkipWhitespace = true,  // Skip whitespace between tokens (default: true)
    Priority = 10           // Higher priority patterns match first (default: 0)
};

// Apply the pattern
var result = tokens.ApplyPatterns(methodCall);
```

### Match Selectors

The `Match` class provides fluent factory methods for creating token selectors:

```csharp
// Basic type matchers
Match.Any()              // Matches any token
Match.Ident()            // Any identifier
Match.Ident("function")  // Specific identifier
Match.Whitespace()       // Whitespace tokens
Match.Numeric()          // Any number
Match.Numeric(NumericType.Integer)  // Integer only
Match.String()           // Any string literal
Match.String('"')        // Double-quoted strings
Match.Comment()          // Comments
Match.TaggedIdent()      // Any tagged identifier (#define, @attr)
Match.TaggedIdent('@')   // Specific tag prefix

// Operators and symbols
Match.Operator()         // Any operator
Match.Operator("==")     // Specific operator
Match.Symbol('.')        // Specific symbol character

// Blocks
Match.Block()            // Any block type
Match.Block('(')         // Parenthesis block
Match.Block('[')         // Bracket block
Match.Block('{')         // Brace block

// Combinators
Match.AnyOf(Match.Ident(), Match.Numeric())  // OR logic

// Content matchers
Match.ContentStartsWith("_")
Match.ContentEndsWith("Async")
Match.ContentContains("test")
Match.ContentMatches(content => content.Length > 5)
```

### Pattern Alternatives (OR Logic)

Define multiple pattern alternatives that can match:

```csharp
var definition = new TokenDefinition<MyToken>
{
    Name = "FlexiblePattern",
    Patterns =
    [
        // Alternative 1: identifier followed by paren block
        [Match.Ident(), Match.Block('(')],
        // Alternative 2: identifier followed by bracket block
        [Match.Ident(), Match.Block('[')]
    ]
};
```

### Composite Token Properties

Composite tokens provide access to their matched tokens:

```csharp
var funcCall = (FunctionCallToken)result[0];

// Access specific parts computed from MatchedTokens
funcCall.FunctionNameSpan;  // "myFunc"
funcCall.PatternName;       // "FunctionCall"
funcCall.Content;           // Full matched content
funcCall.MatchedTokens;     // All tokens that were matched
funcCall.Children;          // Alias for MatchedTokens
```

### Diagnostics

Get detailed information about pattern matching:

```csharp
var report = tokens.ApplyPatternsWithDiagnostics(
    TokenDefinitions.FunctionCall(),
    TokenDefinitions.PropertyAccess());

// Access results
report.OutputTokens;   // The processed tokens
report.InputTokens;    // Original input

// Inspect match attempts
foreach (var attempt in report.Attempts)
{
    Console.WriteLine($"{attempt.PatternName} at {attempt.TokenIndex}: {attempt.Result}");
    
    foreach (var selector in attempt.SelectorResults)
    {
        Console.WriteLine($"  {selector.SelectorDescription}: {selector.Result}");
    }
}
```

### Recursive Pattern Application

Patterns are automatically applied recursively to block contents:

```csharp
var tokens = "outer(inner())".TokenizeToTokens();
var result = tokens.ApplyPatterns(TokenDefinitions.FunctionCall());

// The outer FunctionCallToken's children also have patterns applied
var outerCall = (FunctionCallToken)result[0];
// Inner function call is also recognized within the block
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

// Traversal
foreach (var leaf in tree.Leaves)
{
    Console.WriteLine($"{leaf.Kind}: {leaf.Text}");
}

// From any node
foreach (var child in node.Children) { }
foreach (var desc in node.DescendantsAndSelf()) { }
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
| `CompositeToken`   | Pattern-matched token sequences       | `FunctionCallToken`, `PropertyAccessToken` |

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

// CompositeToken (base for pattern-matched tokens)
var composite = (CompositeToken)token;
composite.PatternName;    // "FunctionCall" etc.
composite.MatchedTokens;  // ImmutableArray<Token> of matched tokens
composite.Children;       // Alias for MatchedTokens

// FunctionCallToken
var funcCall = (FunctionCallToken)token;
funcCall.FunctionNameSpan; // The function name

// PropertyAccessToken
var propAccess = (PropertyAccessToken)token;
propAccess.TargetSpan;    // "obj" in obj.property
propAccess.MemberSpan;    // "property" in obj.property
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

// Pattern matching extensions on ImmutableArray<Token>
ImmutableArray<Token> ApplyPatterns(this ImmutableArray<Token> tokens, params ITokenDefinition[] definitions)
ImmutableArray<Token> ApplyPatterns(this ImmutableArray<Token> tokens, IEnumerable<ITokenDefinition> definitions)
PatternMatchReport ApplyPatternsWithDiagnostics(this ImmutableArray<Token> tokens, params ITokenDefinition[] definitions)
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

### PatternMatcher

```csharp
public sealed class PatternMatcher
{
    public PatternMatcher(IEnumerable<ITokenDefinition> definitions, bool enableDiagnostics = false);

    public ImmutableArray<Token> Apply(ImmutableArray<Token> tokens);
    public PatternMatchReport ApplyWithDiagnostics(ImmutableArray<Token> tokens);
}
```

### TokenDefinition

```csharp
public sealed record TokenDefinition<T> : ITokenDefinition where T : CompositeToken, new()
{
    public required string Name { get; init; }
    public required ImmutableArray<ImmutableArray<TokenSelector>> Patterns { get; init; }
    public bool SkipWhitespace { get; init; } = true;
    public int Priority { get; init; } = 0;
}
```

### Match (selector factory)

```csharp
public static class Match
{
    public static TokenSelector Any();
    public static TokenSelector Ident();
    public static TokenSelector Ident(string content);
    public static TokenSelector Whitespace();
    public static TokenSelector Symbol(char symbol);
    public static TokenSelector Operator();
    public static TokenSelector Operator(string op);
    public static TokenSelector Numeric();
    public static TokenSelector Numeric(NumericType numericType);
    public static TokenSelector String();
    public static TokenSelector String(char quote);
    public static TokenSelector Comment();
    public static TokenSelector TaggedIdent();
    public static TokenSelector TaggedIdent(char prefix);
    public static TokenSelector Block();
    public static TokenSelector Block(char opener);
    public static TokenSelector AnyOf(params TokenSelector[] selectors);
    public static TokenSelector ContentStartsWith(string prefix);
    public static TokenSelector ContentEndsWith(string suffix);
    public static TokenSelector ContentContains(string substring);
    public static TokenSelector ContentMatches(Func<ReadOnlyMemory<char>, bool> predicate);
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
    TaggedIdent,      // tag prefix + identifier
    Composite         // pattern-matched token sequences
}
```

## Requirements

- .NET 8.0 or later

## License

MIT
