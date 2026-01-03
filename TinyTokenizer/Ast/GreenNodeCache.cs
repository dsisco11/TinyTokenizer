using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Cache for common green nodes to maximize structural sharing.
/// Pre-caches well-known tokens (delimiters, operators, keywords).
/// </summary>
internal static class GreenNodeCache
{
    #region Pre-cached Leaves (no trivia)
    
    private static readonly Dictionary<(NodeKind, string), GreenLeaf> _leaves = new()
    {
        // Symbols / Delimiters
        [(NodeKind.Symbol, "{")] = new(NodeKind.Symbol, "{"),
        [(NodeKind.Symbol, "}")] = new(NodeKind.Symbol, "}"),
        [(NodeKind.Symbol, "[")] = new(NodeKind.Symbol, "["),
        [(NodeKind.Symbol, "]")] = new(NodeKind.Symbol, "]"),
        [(NodeKind.Symbol, "(")] = new(NodeKind.Symbol, "("),
        [(NodeKind.Symbol, ")")] = new(NodeKind.Symbol, ")"),
        [(NodeKind.Symbol, ";")] = new(NodeKind.Symbol, ";"),
        [(NodeKind.Symbol, ",")] = new(NodeKind.Symbol, ","),
        [(NodeKind.Symbol, ".")] = new(NodeKind.Symbol, "."),
        [(NodeKind.Symbol, ":")] = new(NodeKind.Symbol, ":"),
        
        // Common Operators
        [(NodeKind.Operator, "=")] = new(NodeKind.Operator, "="),
        [(NodeKind.Operator, "+")] = new(NodeKind.Operator, "+"),
        [(NodeKind.Operator, "-")] = new(NodeKind.Operator, "-"),
        [(NodeKind.Operator, "*")] = new(NodeKind.Operator, "*"),
        [(NodeKind.Operator, "/")] = new(NodeKind.Operator, "/"),
        [(NodeKind.Operator, "%")] = new(NodeKind.Operator, "%"),
        [(NodeKind.Operator, "<")] = new(NodeKind.Operator, "<"),
        [(NodeKind.Operator, ">")] = new(NodeKind.Operator, ">"),
        [(NodeKind.Operator, "!")] = new(NodeKind.Operator, "!"),
        [(NodeKind.Operator, "&")] = new(NodeKind.Operator, "&"),
        [(NodeKind.Operator, "|")] = new(NodeKind.Operator, "|"),
        [(NodeKind.Operator, "^")] = new(NodeKind.Operator, "^"),
        [(NodeKind.Operator, "~")] = new(NodeKind.Operator, "~"),
        [(NodeKind.Operator, "?")] = new(NodeKind.Operator, "?"),
        
        // Multi-char Operators
        [(NodeKind.Operator, "==")] = new(NodeKind.Operator, "=="),
        [(NodeKind.Operator, "!=")] = new(NodeKind.Operator, "!="),
        [(NodeKind.Operator, "<=")] = new(NodeKind.Operator, "<="),
        [(NodeKind.Operator, ">=")] = new(NodeKind.Operator, ">="),
        [(NodeKind.Operator, "&&")] = new(NodeKind.Operator, "&&"),
        [(NodeKind.Operator, "||")] = new(NodeKind.Operator, "||"),
        [(NodeKind.Operator, "++")] = new(NodeKind.Operator, "++"),
        [(NodeKind.Operator, "--")] = new(NodeKind.Operator, "--"),
        [(NodeKind.Operator, "+=")] = new(NodeKind.Operator, "+="),
        [(NodeKind.Operator, "-=")] = new(NodeKind.Operator, "-="),
        [(NodeKind.Operator, "*=")] = new(NodeKind.Operator, "*="),
        [(NodeKind.Operator, "/=")] = new(NodeKind.Operator, "/="),
        [(NodeKind.Operator, "=>")] = new(NodeKind.Operator, "=>"),
        [(NodeKind.Operator, "->")] = new(NodeKind.Operator, "->"),
        [(NodeKind.Operator, "::")] = new(NodeKind.Operator, "::"),
        [(NodeKind.Operator, "??")] = new(NodeKind.Operator, "??"),
        [(NodeKind.Operator, "?.")] = new(NodeKind.Operator, "?."),
        [(NodeKind.Operator, "<<")] = new(NodeKind.Operator, "<<"),
        [(NodeKind.Operator, ">>")] = new(NodeKind.Operator, ">>"),
    };
    
    #endregion
    
    #region Pre-cached Leaves (with trailing space)
    
    private static readonly Dictionary<(NodeKind, string), GreenLeaf> _leavesWithSpace;
    
    static GreenNodeCache()
    {
        // Create variants with trailing space for common tokens
        _leavesWithSpace = new Dictionary<(NodeKind, string), GreenLeaf>();
        var trailingSpace = ImmutableArray.Create(GreenTrivia.Whitespace(" "));
        
        foreach (var kvp in _leaves)
        {
            _leavesWithSpace[kvp.Key] = new GreenLeaf(
                kvp.Key.Item1,
                kvp.Key.Item2,
                ImmutableArray<GreenTrivia>.Empty,
                trailingSpace);
        }
    }
    
    #endregion
    
    #region Pre-cached Trivia
    
    /// <summary>Single space trivia.</summary>
    public static GreenTrivia Space { get; } = GreenTrivia.Whitespace(" ");
    
    /// <summary>Unix newline trivia.</summary>
    public static GreenTrivia NewlineLF { get; } = GreenTrivia.Newline("\n");
    
    /// <summary>Windows newline trivia.</summary>
    public static GreenTrivia NewlineCRLF { get; } = GreenTrivia.Newline("\r\n");
    
    /// <summary>Tab trivia.</summary>
    public static GreenTrivia Tab { get; } = GreenTrivia.Whitespace("\t");
    
    #endregion
    
    #region Factory Methods
    
    /// <summary>
    /// Gets a cached leaf or creates a new one.
    /// Tokens without trivia are cached; tokens with trivia are always new.
    /// </summary>
    public static GreenLeaf GetOrCreate(NodeKind kind, string text)
    {
        if (_leaves.TryGetValue((kind, text), out var cached))
            return cached;
        
        return new GreenLeaf(kind, text);
    }
    
    /// <summary>
    /// Gets a cached leaf with trailing space or creates a new one.
    /// </summary>
    public static GreenLeaf GetOrCreateWithTrailingSpace(NodeKind kind, string text)
    {
        if (_leavesWithSpace.TryGetValue((kind, text), out var cached))
            return cached;
        
        return new GreenLeaf(
            kind,
            text,
            ImmutableArray<GreenTrivia>.Empty,
            ImmutableArray.Create(Space));
    }
    
    /// <summary>
    /// Gets a cached leaf with custom trivia (always creates new instance).
    /// </summary>
    public static GreenLeaf Create(
        NodeKind kind,
        string text,
        ImmutableArray<GreenTrivia> leadingTrivia,
        ImmutableArray<GreenTrivia> trailingTrivia)
    {
        // If no trivia, try cache
        if (leadingTrivia.IsDefaultOrEmpty && trailingTrivia.IsDefaultOrEmpty)
            return GetOrCreate(kind, text);
        
        // If only trailing space, try that cache
        if (leadingTrivia.IsDefaultOrEmpty && 
            trailingTrivia.Length == 1 && 
            trailingTrivia[0].Kind == TriviaKind.Whitespace &&
            trailingTrivia[0].Text == " ")
        {
            return GetOrCreateWithTrailingSpace(kind, text);
        }
        
        return new GreenLeaf(kind, text, leadingTrivia, trailingTrivia);
    }
    
    /// <summary>
    /// Tries to get a cached leaf without trivia.
    /// </summary>
    public static bool TryGetCached(NodeKind kind, string text, out GreenLeaf leaf)
        => _leaves.TryGetValue((kind, text), out leaf!);
    
    /// <summary>
    /// Creates a delimiter leaf (for block opener/closer).
    /// Uses cache for delimiters with no trivia.
    /// </summary>
    public static GreenLeaf CreateDelimiter(
        char delimiter,
        ImmutableArray<GreenTrivia> leadingTrivia = default,
        ImmutableArray<GreenTrivia> trailingTrivia = default)
    {
        var text = delimiter.ToString();
        
        // If no trivia, use cache
        if (leadingTrivia.IsDefaultOrEmpty && trailingTrivia.IsDefaultOrEmpty)
            return GetOrCreate(NodeKind.Symbol, text);
        
        return new GreenLeaf(NodeKind.Symbol, text, leadingTrivia, trailingTrivia);
    }
    
    #endregion
}
