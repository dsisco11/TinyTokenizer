# TinyTokenizer Implementation Plan

**Created:** January 2026  
**Based on:** [Codebase Analysis Report](codebase-analysis-2026-01.md)  
**Target Completion:** Q2 2026

---

## Overview

This document outlines a phased approach to implementing improvements identified in the codebase analysis. Each phase is designed to be released independently with minimal breaking changes until Phase 3.

---

## Phase 1: Quick Wins (v0.6.6)

**Timeline:** 1-2 days  
**Breaking Changes:** None  
**Goal:** Low-effort, high-impact improvements

### 1.1 Pre-compute Comment Style Flags

**File:** `TokenParser.cs`  
**Estimated Time:** 30 minutes

#### Current State
```csharp
private Token? TryParseComment(SimpleTokenReader reader, SimpleToken slashToken, SimpleToken nextToken)
{
    var hasCSingleLine = _options.CommentStyles.Any(s => s.Start == "//");
    var hasCMultiLine = _options.CommentStyles.Any(s => s.Start == "/*");
    // ...
}
```

#### Implementation
1. Add private readonly fields:
   ```csharp
   private readonly bool _hasCSingleLineComment;
   private readonly bool _hasCMultiLineComment;
   ```

2. Initialize in constructor:
   ```csharp
   public TokenParser(TokenizerOptions options)
   {
       _options = options;
       _sortedOperators = options.Operators
           .OrderByDescending(op => op.Length)
           .ToImmutableArray();
       
       // Pre-compute comment style flags
       _hasCSingleLineComment = options.CommentStyles.Any(s => s.Start == "//");
       _hasCMultiLineComment = options.CommentStyles.Any(s => s.Start == "/*");
   }
   ```

3. Update `TryParseComment` to use cached fields

#### Acceptance Criteria
- [ ] No LINQ allocation in `TryParseComment`
- [ ] All existing tests pass
- [ ] Benchmark shows measurable improvement for comment-heavy input

---

### 1.2 Add IFormattable to Token

**File:** `Token.cs`  
**Estimated Time:** 1 hour

#### Implementation
1. Add interface to base record:
   ```csharp
   public abstract record Token(
       ReadOnlyMemory<char> Content, 
       TokenType Type, 
       long Position) : IFormattable
   ```

2. Implement `ToString(string?, IFormatProvider?)`:
   ```csharp
   public string ToString(string? format, IFormatProvider? formatProvider)
   {
       return format switch
       {
           null or "" or "G" => ContentSpan.ToString(),
           "T" => Type.ToString(),
           "P" => Position.ToString(),
           "R" => $"{Position}..{Position + Content.Length}",
           "D" => $"{Type}[{Position}..{Position + Content.Length}]",
           _ => ContentSpan.ToString()
       };
   }
   ```

#### Acceptance Criteria
- [ ] `Token` implements `IFormattable`
- [ ] Format specifiers match `RedNode` where applicable
- [ ] Add unit tests for each format specifier
- [ ] XML documentation added

---

### 1.3 Fix AppendToBuffer Inefficiency

**File:** `TokenParser.cs`  
**Estimated Time:** 15 minutes

#### Current State
```csharp
private static void AppendToBuffer(List<char> buffer, ReadOnlyMemory<char> content)
{
    for (int i = 0; i < content.Length; i++)
    {
        buffer.Add(content.Span[i]);
    }
}
```

#### Implementation
Replace with span-based approach:
```csharp
private static void AppendToBuffer(List<char> buffer, ReadOnlyMemory<char> content)
{
    var span = content.Span;
    buffer.EnsureCapacity(buffer.Count + span.Length);
    foreach (var c in span)
        buffer.Add(c);
}
```

> Note: `List<char>` doesn't have `AddRange(ReadOnlySpan<char>)`, so we use `EnsureCapacity` + loop. Full fix in Phase 2 with `ArrayBufferWriter<char>`.

#### Acceptance Criteria
- [ ] Single capacity allocation instead of multiple resizes
- [ ] All parsing tests pass

---

### 1.4 Cache SiblingIndex

**File:** `RedNode.cs`  
**Estimated Time:** 1 hour

#### Current State
```csharp
public int SiblingIndex
{
    get
    {
        if (_parent == null) return -1;
        for (int i = 0; i < _parent.SlotCount; i++)
        {
            if (ReferenceEquals(_parent.GetChild(i), this))
                return i;
        }
        return -1;
    }
}
```

#### Implementation
1. Add field to store cached index:
   ```csharp
   private readonly int _siblingIndex;
   ```

2. Update constructor to accept and store index:
   ```csharp
   internal RedNode(GreenNode green, RedNode? parent, int position, int siblingIndex = -1)
   {
       _green = green;
       _parent = parent;
       _position = position;
       _siblingIndex = siblingIndex;
   }
   ```

3. Update `GetRedChild` to pass index:
   ```csharp
   protected T? GetRedChild<T>(ref T? field, int slot) where T : RedNode
   {
       // ... existing code ...
       var redChild = (T)greenChild.CreateRed(this, childPosition, slot);
       // ...
   }
   ```

4. Update property to return cached value:
   ```csharp
   public int SiblingIndex => _siblingIndex;
   ```

#### Acceptance Criteria
- [ ] `SiblingIndex` is O(1)
- [ ] All navigation tests pass
- [ ] `NextSibling()` and `PreviousSibling()` still work correctly

---

### Phase 1 Release Checklist

- [ ] All four tasks completed
- [ ] Full test suite passes
- [ ] Benchmarks run and results documented
- [ ] CHANGELOG updated
- [ ] Version bumped to 0.6.6
- [ ] NuGet package published

---

## Phase 2: Performance (v0.7.0)

**Timeline:** 1-2 weeks  
**Breaking Changes:** Minor (internal APIs only)  
**Goal:** Significant allocation reduction in hot paths

### 2.1 Replace List\<char\> with ArrayPoolBufferWriter

**Files:** `TokenParser.cs`, `TinyTokenizer.csproj`  
**Estimated Time:** 4 hours

#### Implementation Strategy

1. Add NuGet reference:
   ```xml
   <PackageReference Include="CommunityToolkit.HighPerformance" Version="8.2.2" />
   ```

2. Use `ArrayPoolBufferWriter<char>` from the toolkit:
   ```csharp
   using CommunityToolkit.HighPerformance.Buffers;
   
   // In parsing methods:
   using var buffer = new ArrayPoolBufferWriter<char>(initialCapacity);
   buffer.Write(span);                    // Append span
   buffer.GetSpan(1)[0] = 'c';           // Append single char
   buffer.Advance(1);
   var result = buffer.WrittenMemory;    // Get as Memory<char>
   // Automatically returns to pool on dispose
   ```

3. Update all parsing methods to use `ArrayPoolBufferWriter<char>`:
   - `ParseBlock`
   - `ParseString`
   - `ParseNumericFromDigits`
   - `ParseNumericFromDot`
   - `ParseSingleLineComment`
   - `ParseMultiLineComment`
   - `TryParseOperator`
   - `TryParseTaggedIdent`

4. Ensure proper disposal in all code paths (use `using` statements)

#### Why CommunityToolkit.HighPerformance?
- `ArrayPoolBufferWriter<T>` combines `IBufferWriter<T>` with `ArrayPool` automatically
- Returns buffers to pool on `Dispose()` — no manual pool management
- Battle-tested, widely used in high-performance .NET code
- Zero additional code to maintain

#### Acceptance Criteria
- [ ] Zero `List<char>` usage in `TokenParser`
- [ ] Allocation benchmark shows >50% reduction
- [ ] All parsing tests pass
- [ ] No memory leaks (verify with memory profiler)

---

### 2.2 Build Operator Trie

**Files:** `TokenParser.cs`, new `OperatorTrie.cs`  
**Estimated Time:** 6 hours

#### Implementation Strategy

1. Create `OperatorTrie` class:
   ```csharp
   internal sealed class OperatorTrie
   {
       private readonly TrieNode _root = new();
       
       public void Add(string op) { /* ... */ }
       public string? TryMatch(ReadOnlySpan<char> input) { /* ... */ }
       
       private sealed class TrieNode
       {
           public Dictionary<char, TrieNode>? Children;
           public string? Operator; // non-null if this is end of an operator
       }
   }
   ```

2. Build trie in `TokenParser` constructor:
   ```csharp
   private readonly OperatorTrie _operatorTrie;
   
   public TokenParser(TokenizerOptions options)
   {
       // ...
       _operatorTrie = new OperatorTrie();
       foreach (var op in options.Operators)
           _operatorTrie.Add(op);
   }
   ```

3. Update `TryParseOperator` to use trie lookup

#### Acceptance Criteria
- [ ] Operator matching is O(k) where k = operator length
- [ ] Benchmark with 50+ operators shows improvement
- [ ] Greedy matching still works (longest match first)
- [ ] All operator tests pass

---

### 2.3 Standardize Position Types

**Files:** `SimpleToken.cs`, `Token.cs`  
**Estimated Time:** 2 hours

#### Breaking Change Assessment
- `SimpleToken.Position`: `long` → `int`
- `Token.Position`: `long` → `int`
- Affects: Any code storing or comparing positions

#### Implementation Strategy

1. Update `SimpleToken`:
   ```csharp
   public readonly record struct SimpleToken(
       SimpleTokenType Type,
       ReadOnlyMemory<char> Content,
       int Position)  // Changed from long
   ```

2. Update `Token`:
   ```csharp
   public abstract record Token(
       ReadOnlyMemory<char> Content,
       TokenType Type,
       int Position)  // Changed from long
   ```

3. Update all derived types and usages

4. Add XML doc noting the 2GB file size limit

#### Acceptance Criteria
- [ ] All position types are `int`
- [ ] Documentation notes the practical limit
- [ ] All tests updated and passing
- [ ] Breaking change documented in CHANGELOG

---

### 2.4 Pre-sort Operators in TokenizerOptions

**File:** `TokenizerOptions.cs`  
**Estimated Time:** 1 hour

#### Implementation
Store operators pre-sorted by length descending to avoid sorting in every `TokenParser` instance:

```csharp
public sealed record TokenizerOptions
{
    // Change from ImmutableHashSet to ImmutableArray (already sorted)
    public ImmutableArray<string> Operators { get; init; }
    
    public TokenizerOptions WithOperators(params string[] operators)
    {
        var sorted = operators
            .Distinct()
            .OrderByDescending(op => op.Length)
            .ToImmutableArray();
        return this with { Operators = sorted };
    }
}
```

#### Acceptance Criteria
- [ ] Operators stored pre-sorted
- [ ] `TokenParser` constructor no longer sorts
- [ ] Existing `WithOperators` API unchanged

---

### Phase 2 Release Checklist

- [ ] All four tasks completed
- [ ] Full test suite passes
- [ ] Before/after allocation benchmarks documented
- [ ] Breaking changes documented in CHANGELOG
- [ ] Migration guide written for position type change
- [ ] Version bumped to 0.7.0
- [ ] NuGet package published

---

## Phase 3: API Cleanup (v0.8.0)

**Timeline:** 2-3 weeks  
**Breaking Changes:** Yes (major cleanup)  
**Goal:** Cleaner, more consistent public API

### 3.1 Consolidate Duplicate Parsing Logic

**Files:** `Tokenizer.cs`, `TokenParser.cs`, `GreenLexer.cs`, new `ParsingCore.cs`  
**Estimated Time:** 8 hours

#### Implementation Strategy

1. Create `ParsingCore` internal static class with shared logic:
   ```csharp
   internal static class ParsingCore
   {
       public static ParseResult ParseString(/* ... */) { }
       public static ParseResult ParseBlock(/* ... */) { }
       public static ParseResult ParseComment(/* ... */) { }
       public static ParseResult ParseNumeric(/* ... */) { }
   }
   ```

2. Define `ParseResult` to work with both token types:
   ```csharp
   internal readonly struct ParseResult
   {
       public ReadOnlyMemory<char> Content { get; }
       public int ConsumedTokens { get; }
       public bool Success { get; }
       public string? ErrorMessage { get; }
   }
   ```

3. Refactor all three parsers to use `ParsingCore`

4. Add comprehensive tests for `ParsingCore` directly

#### Acceptance Criteria
- [ ] Single source of truth for parsing logic
- [ ] All three parsers produce identical results for same input
- [ ] Test coverage on `ParsingCore` >90%

---

### 3.2 Address Schema Nullability

**File:** `SyntaxTree.cs`  
**Estimated Time:** 4 hours

#### Implementation Strategy

Option chosen: Add `HasSchema` property + improve documentation

1. Add property:
   ```csharp
   /// <summary>
   /// Whether this tree has an attached schema for semantic matching.
   /// </summary>
   public bool HasSchema => Schema != null;
   ```

2. Add `RequireSchema()` helper:
   ```csharp
   private Schema RequireSchema()
   {
       return Schema ?? throw new InvalidOperationException(
           "This operation requires a schema. Use SyntaxTree.Parse(source, schema) " +
           "or call tree.WithSchema(schema) first.");
   }
   ```

3. Add `WithSchema` method:
   ```csharp
   /// <summary>
   /// Returns a new SyntaxTree with the specified schema attached.
   /// </summary>
   public SyntaxTree WithSchema(Schema schema)
   {
       return new SyntaxTree(_greenRoot, schema);
   }
   ```

4. Update all schema-dependent methods to use `RequireSchema()`

5. Add comprehensive XML documentation

#### Acceptance Criteria
- [ ] `HasSchema` property available
- [ ] `WithSchema` method available
- [ ] Clear error messages when schema is missing
- [ ] XML docs explain the schema requirement

---

### 3.3 Make Trivia Types Public

**Files:** `GreenTrivia.cs`, new `Trivia.cs`  
**Estimated Time:** 3 hours

#### Implementation Strategy

Create public wrapper type:

```csharp
/// <summary>
/// Represents trivia (whitespace, comments) attached to a token.
/// </summary>
public readonly struct Trivia
{
    internal Trivia(GreenTrivia green) => _green = green;
    
    private readonly GreenTrivia _green;
    
    public TriviaKind Kind => _green.Kind;
    public string Text => _green.Text;
    public int Width => _green.Width;
    
    public override string ToString() => Text;
}

public enum TriviaKind
{
    Whitespace,
    Newline,
    SingleLineComment,
    MultiLineComment
}
```

Add to `RedLeaf`:
```csharp
public IEnumerable<Trivia> LeadingTrivia { get; }
public IEnumerable<Trivia> TrailingTrivia { get; }
```

#### Acceptance Criteria
- [ ] `Trivia` and `TriviaKind` are public
- [ ] Accessible via `RedLeaf.LeadingTrivia` / `TrailingTrivia`
- [ ] XML documentation complete
- [ ] Usage examples in docs

---

### 3.4 Comprehensive Documentation Pass

**Files:** All public API files  
**Estimated Time:** 4 hours

#### Checklist

- [ ] `Query.cs` - Document all query factory methods
- [ ] `TreeWalker.cs` - Document traversal options and filters
- [ ] `NodePattern.cs` - Document pattern matching syntax
- [ ] `SyntaxBinder.cs` - Document binding process
- [ ] `Schema.cs` - Document schema configuration
- [ ] `SemanticNode.cs` - Document semantic node creation

#### Documentation Template
```csharp
/// <summary>
/// Brief description of the type/member.
/// </summary>
/// <remarks>
/// Detailed explanation of behavior, edge cases, and usage patterns.
/// </remarks>
/// <example>
/// <code>
/// // Example usage
/// var result = SomeMethod();
/// </code>
/// </example>
/// <seealso cref="RelatedType"/>
```

---

### 3.5 Remove Obsolete APIs

**Files:** `SyntaxTree.cs`  
**Estimated Time:** 30 minutes

#### Implementation
1. Remove `[Obsolete]` attribute and method `ToFullString()`
2. Update any remaining internal usages
3. Document removal in CHANGELOG

---

### Phase 3 Release Checklist

- [ ] All five tasks completed
- [ ] Full test suite passes
- [ ] API documentation complete
- [ ] Breaking changes documented
- [ ] Migration guide for removed APIs
- [ ] Version bumped to 0.8.0
- [ ] NuGet package published

---

## Phase 4: Testing (Ongoing)

**Timeline:** Continuous  
**Goal:** Comprehensive test coverage and performance validation

### 4.1 Error Recovery Test Suite

**File:** New `ErrorRecoveryTests.cs`  
**Estimated Time:** 4 hours

#### Test Cases

```csharp
public class ErrorRecoveryTests
{
    // Unclosed blocks
    [Theory]
    [InlineData("{", "Unclosed block")]
    [InlineData("[", "Unclosed block")]
    [InlineData("(", "Unclosed block")]
    [InlineData("{ { }", "Nested unclosed")]
    public void UnclosedBlock_ProducesErrorToken(string input, string scenario) { }
    
    // Mismatched delimiters
    [Theory]
    [InlineData("{]")]
    [InlineData("[)")]
    [InlineData("(}")]
    public void MismatchedDelimiters_ProducesErrorToken(string input) { }
    
    // Unclosed strings
    [Theory]
    [InlineData("\"hello")]
    [InlineData("'world")]
    [InlineData("\"test\nmore\"")]
    public void UnclosedString_ProducesErrorOrSymbol(string input) { }
    
    // Recovery continues after error
    [Fact]
    public void AfterError_ContinuesTokenizing() { }
}
```

---

### 4.2 Unicode Test Suite

**File:** New `UnicodeTests.cs`  
**Estimated Time:** 3 hours

#### Test Cases

```csharp
public class UnicodeTests
{
    [Theory]
    [InlineData("変数", "Japanese")]
    [InlineData("переменная", "Cyrillic")]
    [InlineData("משתנה", "Hebrew RTL")]
    [InlineData("🚀rocket", "Emoji prefix")]
    [InlineData("var_🎉", "Emoji suffix")]
    public void UnicodeIdentifiers_Recognized(string input, string script) { }
    
    [Theory]
    [InlineData("\u200B", "Zero-width space")]
    [InlineData("\uFEFF", "BOM")]
    public void InvisibleCharacters_Handled(string input, string charType) { }
}
```

---

### 4.3 Numeric Edge Cases

**File:** `LexerParserTests.cs` (extend existing)  
**Estimated Time:** 2 hours

#### Test Cases

```csharp
[Theory]
[InlineData("0", NumericType.Integer)]
[InlineData("0.0", NumericType.FloatingPoint)]
[InlineData(".0", NumericType.FloatingPoint)]
[InlineData("0.", NumericType.Integer)] // Trailing dot is separate
[InlineData("00123", NumericType.Integer)] // Leading zeros
[InlineData("1.2.3", "Multiple dots")] // Should be 1.2 + . + 3
public void NumericEdgeCases(string input, object expected) { }
```

---

### 4.4 Thread Safety Tests

**File:** New `ConcurrencyTests.cs`  
**Estimated Time:** 3 hours

#### Test Cases

```csharp
public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRedNodeAccess_ThreadSafe()
    {
        var tree = SyntaxTree.Parse("func(a, b, c)");
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => 
            {
                var root = tree.Root;
                foreach (var child in root.Children)
                {
                    _ = child.SiblingIndex;
                    _ = child.NextSibling();
                }
            }));
        
        await Task.WhenAll(tasks); // Should not throw
    }
    
    [Fact]
    public async Task ConcurrentTreeParsing_Independent()
    {
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => SyntaxTree.Parse($"var{i} = {i}")));
        
        var trees = await Task.WhenAll(tasks);
        Assert.All(trees, t => Assert.NotNull(t.Root));
    }
}
```

---

### 4.5 Performance Benchmarks

**File:** `TinyTokenizer.Benchmarks/`  
**Estimated Time:** 4 hours

#### New Benchmarks

```csharp
[MemoryDiagnoser]
public class AllocationBenchmarks
{
    private string _smallInput;   // 1 KB
    private string _mediumInput;  // 100 KB
    private string _largeInput;   // 1 MB
    
    [Benchmark]
    public void ParseSmall() => SyntaxTree.Parse(_smallInput);
    
    [Benchmark]
    public void ParseMedium() => SyntaxTree.Parse(_mediumInput);
    
    [Benchmark]
    public void ParseLarge() => SyntaxTree.Parse(_largeInput);
}

[MemoryDiagnoser]
public class OperatorMatchingBenchmarks
{
    [Params(10, 50, 100)]
    public int OperatorCount { get; set; }
    
    [Benchmark]
    public void MatchOperators() { /* ... */ }
}
```

---

## Milestone Summary

| Version | Target Date | Key Deliverables |
|---------|-------------|------------------|
| v0.6.6 | Week 1 | Quick wins: cached flags, IFormattable, buffer fix |
| v0.7.0 | Week 3 | Performance: ArrayPoolBufferWriter (CommunityToolkit), operator trie, position types |
| v0.8.0 | Week 6 | API cleanup: consolidated parsing, schema design, public trivia |
| Ongoing | Continuous | Test coverage expansion |

---

## Dependencies

| Package | Version | Phase | Purpose |
|---------|---------|-------|---------|
| CommunityToolkit.HighPerformance | 8.2.2 | 2.1 | `ArrayPoolBufferWriter<char>` for zero-allocation parsing |

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking change regression | Medium | High | Comprehensive test suite, semantic versioning |
| Performance regression | Low | Medium | Benchmark before/after each phase |
| Memory leak from pooling | Low | High | Memory profiler validation, dispose patterns |
| Thread safety issues | Medium | High | Dedicated concurrency tests |

---

## Success Metrics

1. **Allocation reduction**: >50% fewer allocations in parsing benchmarks
2. **API consistency**: All public types implement `IFormattable` where applicable
3. **Test coverage**: >85% line coverage on core parsing logic
4. **Documentation**: 100% XML doc coverage on public APIs
5. **Performance**: No regression in existing benchmarks
