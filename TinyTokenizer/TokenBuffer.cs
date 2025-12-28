using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// A mutable token collection with deferred operations.
/// Stores Level 1 SimpleTokens as the source of truth and provides Level 2 Tokens as a computed view.
/// Mutations are queued and applied atomically when <see cref="Commit"/> is called.
/// </summary>
/// <remarks>
/// This architecture enables natural mutations inside blocks (like function bodies) because
/// blocks only exist at Level 2 — at Level 1, everything is a flat sequence of SimpleTokens.
/// </remarks>
/// <example>
/// <code>
/// var buffer = "function foo() { return 1; }".ToTokenBuffer();
/// 
/// // Insert inside a block (after the opening brace)
/// buffer
///     .InsertIntoBlockStart(Query.BraceBlock.First(), "console.log('enter');")
///     .Commit();
/// </code>
/// </example>
public sealed class TokenBuffer
{
    #region Fields

    private ImmutableArray<SimpleToken> _simpleTokens;
    private ImmutableArray<Token>? _cachedTokens;
    private readonly List<SimpleTokenMutation> _mutations = [];
    private TokenizerOptions _defaultOptions;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="TokenBuffer"/> with the specified SimpleTokens.
    /// </summary>
    /// <param name="simpleTokens">The initial SimpleToken array.</param>
    /// <param name="options">The tokenizer options for Level 2 parsing and text injection.</param>
    public TokenBuffer(ImmutableArray<SimpleToken> simpleTokens, TokenizerOptions options)
    {
        _simpleTokens = simpleTokens;
        _defaultOptions = options;
        _cachedTokens = null;
    }

    /// <summary>
    /// Initializes a new <see cref="TokenBuffer"/> with the specified SimpleTokens and default options.
    /// </summary>
    /// <param name="simpleTokens">The initial SimpleToken array.</param>
    public TokenBuffer(ImmutableArray<SimpleToken> simpleTokens)
        : this(simpleTokens, TokenizerOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="TokenBuffer"/> from Level 2 tokens.
    /// Re-lexes the content to obtain SimpleTokens.
    /// </summary>
    /// <param name="tokens">The Level 2 tokens to convert.</param>
    /// <param name="options">The tokenizer options.</param>
    public TokenBuffer(ImmutableArray<Token> tokens, TokenizerOptions options)
    {
        _defaultOptions = options;
        
        // Reconstruct source text from tokens and re-lex
        var sourceText = string.Concat(tokens.Select(t => t.ContentSpan.ToString()));
        var lexer = new Lexer(options);
        _simpleTokens = lexer.LexToArray(sourceText);
        _cachedTokens = null;
    }

    /// <summary>
    /// Initializes a new <see cref="TokenBuffer"/> from Level 2 tokens with default options.
    /// </summary>
    /// <param name="tokens">The Level 2 tokens to convert.</param>
    public TokenBuffer(ImmutableArray<Token> tokens)
        : this(tokens, TokenizerOptions.Default)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current Level 1 SimpleTokens (source of truth).
    /// </summary>
    public ImmutableArray<SimpleToken> SimpleTokens => _simpleTokens;

    /// <summary>
    /// Gets the current Level 2 tokens (computed view, cached).
    /// </summary>
    public ImmutableArray<Token> Tokens => _cachedTokens ??= ComputeLevel2Tokens();

    /// <summary>
    /// Gets the number of SimpleTokens in the current committed state.
    /// </summary>
    public int SimpleTokenCount => _simpleTokens.Length;

    /// <summary>
    /// Gets the number of Level 2 tokens in the current committed state.
    /// </summary>
    public int Count => Tokens.Length;

    /// <summary>
    /// Gets the number of pending mutations.
    /// </summary>
    public int PendingMutationCount => _mutations.Count;

    /// <summary>
    /// Gets whether there are pending mutations to commit.
    /// </summary>
    public bool HasPendingMutations => _mutations.Count > 0;

    /// <summary>
    /// Gets or sets the default tokenizer options for text injection and Level 2 parsing.
    /// </summary>
    public TokenizerOptions DefaultOptions
    {
        get => _defaultOptions;
        set
        {
            _defaultOptions = value;
            InvalidateLevel2Cache();
        }
    }

    #endregion

    #region Level 2 Computation

    private ImmutableArray<Token> ComputeLevel2Tokens()
    {
        if (_simpleTokens.IsEmpty)
            return ImmutableArray<Token>.Empty;

        var parser = new TokenParser(_defaultOptions);
        return parser.ParseToArray(_simpleTokens);
    }

    private void InvalidateLevel2Cache()
    {
        _cachedTokens = null;
    }

    #endregion

    #region Position Mapping (Level 2 → Level 1)

    /// <summary>
    /// Finds the SimpleToken index corresponding to a Level 2 token's position.
    /// </summary>
    private int FindSimpleTokenIndex(Token token)
    {
        var targetPosition = token.Position;
        
        for (int i = 0; i < _simpleTokens.Length; i++)
        {
            if (_simpleTokens[i].Position == targetPosition)
                return i;
        }
        
        throw new InvalidOperationException($"Could not find SimpleToken at position {targetPosition}");
    }

    /// <summary>
    /// Finds the SimpleToken index of a block's opening delimiter.
    /// </summary>
    private int FindBlockOpeningIndex(SimpleBlock block)
    {
        return FindSimpleTokenIndex(block);
    }

    /// <summary>
    /// Finds the SimpleToken index of a block's closing delimiter.
    /// </summary>
    private int FindBlockClosingIndex(SimpleBlock block)
    {
        var closingPosition = block.ClosingDelimiter.Position;
        
        for (int i = 0; i < _simpleTokens.Length; i++)
        {
            if (_simpleTokens[i].Position == closingPosition)
                return i;
        }
        
        throw new InvalidOperationException($"Could not find closing delimiter at position {closingPosition}");
    }

    /// <summary>
    /// Finds the range of SimpleToken indices that comprise a Level 2 token.
    /// For simple tokens, this is a single index. For blocks, this spans from opener to closer.
    /// </summary>
    private (int Start, int End) FindSimpleTokenRange(Token token)
    {
        if (token is SimpleBlock block)
        {
            var start = FindBlockOpeningIndex(block);
            var end = FindBlockClosingIndex(block);
            return (start, end);
        }
        else
        {
            var index = FindSimpleTokenIndex(token);
            return (index, index);
        }
    }

    #endregion

    #region Index-Based Mutations (Level 1)

    /// <summary>
    /// Queues an insertion of SimpleTokens at the specified Level 1 index.
    /// </summary>
    public TokenBuffer InsertSimpleTokens(int index, IEnumerable<SimpleToken> tokens)
    {
        var tokenArray = tokens.ToImmutableArray();
        if (tokenArray.IsEmpty)
            return this;

        var mutation = new InsertSimpleTokensMutation { Index = index, Tokens = tokenArray };
        mutation.Validate(_simpleTokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    /// <summary>
    /// Queues the removal of a SimpleToken at the specified Level 1 index.
    /// </summary>
    public TokenBuffer RemoveSimpleToken(int index)
    {
        var mutation = new RemoveSimpleTokenMutation { Index = index };
        mutation.Validate(_simpleTokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    /// <summary>
    /// Queues the removal of a range of SimpleTokens at the specified Level 1 index.
    /// </summary>
    public TokenBuffer RemoveSimpleTokenRange(int index, int count)
    {
        var mutation = new RemoveSimpleTokenRangeMutation { Index = index, Count = count };
        mutation.Validate(_simpleTokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    #endregion

    #region Query-Based Mutations (Level 2 → Level 1)

    /// <summary>
    /// Queues the removal of all Level 2 tokens matching the query.
    /// </summary>
    public TokenBuffer Remove(TokenQuery query)
    {
        var tokens = Tokens;
        var indices = query.Select(tokens).OrderByDescending(i => i).ToList();
        
        foreach (var index in indices)
        {
            var token = tokens[index];
            var (start, end) = FindSimpleTokenRange(token);
            _mutations.Add(new RemoveSimpleTokenRangeMutation { Index = start, Count = end - start + 1 });
        }
        
        return this;
    }

    /// <summary>
    /// Queues the replacement of all Level 2 tokens matching the query.
    /// </summary>
    public TokenBuffer Replace(TokenQuery query, Func<Token, string> textReplacer)
    {
        var tokens = Tokens;
        
        foreach (var index in query.Select(tokens))
        {
            var original = tokens[index];
            var replacementText = textReplacer(original);
            var replacementTokens = LexText(replacementText);
            var (start, end) = FindSimpleTokenRange(original);
            
            _mutations.Add(new ReplaceSimpleTokensMutation
            {
                Index = start,
                RemoveCount = end - start + 1,
                Tokens = replacementTokens
            });
        }
        
        return this;
    }

    /// <summary>
    /// Queues the replacement of all Level 2 tokens matching the query with a token-based replacer.
    /// The replacement token's content is used as the replacement text.
    /// </summary>
    public TokenBuffer Replace(TokenQuery query, Func<Token, Token> replacer)
    {
        return Replace(query, t => replacer(t).ContentSpan.ToString());
    }

    /// <summary>
    /// Queues an insertion before all Level 2 tokens matching the query.
    /// </summary>
    public TokenBuffer InsertBefore(TokenQuery query, Token token)
    {
        return InsertTextBefore(query, token.ContentSpan.ToString());
    }

    /// <summary>
    /// Queues an insertion after all Level 2 tokens matching the query.
    /// </summary>
    public TokenBuffer InsertAfter(TokenQuery query, Token token)
    {
        return InsertTextAfter(query, token.ContentSpan.ToString());
    }

    #endregion

    #region Text Injection

    /// <summary>
    /// Queues an insertion of text (lexed to SimpleTokens) at the specified Level 1 index.
    /// </summary>
    public TokenBuffer InsertText(int index, string text, TokenizerOptions? options = null)
    {
        var simpleTokens = LexText(text, options);
        return InsertSimpleTokens(index, simpleTokens);
    }

    /// <summary>
    /// Queues an insertion of text before all Level 2 tokens matching the query.
    /// </summary>
    public TokenBuffer InsertTextBefore(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var simpleTokens = LexText(text, options);
        var tokens = Tokens;
        
        foreach (var index in query.Select(tokens))
        {
            var token = tokens[index];
            var simpleIndex = FindSimpleTokenIndex(token);
            _mutations.Add(new InsertSimpleTokensMutation { Index = simpleIndex, Tokens = simpleTokens });
        }
        
        return this;
    }

    /// <summary>
    /// Queues an insertion of text after all Level 2 tokens matching the query.
    /// </summary>
    public TokenBuffer InsertTextAfter(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var simpleTokens = LexText(text, options);
        var tokens = Tokens;
        
        foreach (var index in query.Select(tokens))
        {
            var token = tokens[index];
            var (_, end) = FindSimpleTokenRange(token);
            _mutations.Add(new InsertSimpleTokensMutation { Index = end + 1, Tokens = simpleTokens });
        }
        
        return this;
    }

    /// <summary>
    /// Queues the replacement of all Level 2 tokens matching the query with tokenized text.
    /// </summary>
    public TokenBuffer ReplaceWithText(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        return Replace(query, _ => text);
    }

    #endregion

    #region Block Mutations

    /// <summary>
    /// Queues an insertion of text at the start of a block's content (after the opening delimiter).
    /// </summary>
    /// <param name="query">A query that selects SimpleBlock tokens.</param>
    /// <param name="text">The text to insert.</param>
    /// <param name="options">Optional tokenizer options.</param>
    public TokenBuffer InsertIntoBlockStart(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var simpleTokens = LexText(text, options);
        var tokens = Tokens;
        
        foreach (var index in query.Select(tokens))
        {
            var token = tokens[index];
            if (token is not SimpleBlock block)
            {
                throw new InvalidOperationException($"InsertIntoBlockStart requires a SimpleBlock, got {token.GetType().Name}");
            }
            
            // Insert after the opening delimiter
            var openingIndex = FindBlockOpeningIndex(block);
            _mutations.Add(new InsertSimpleTokensMutation { Index = openingIndex + 1, Tokens = simpleTokens });
        }
        
        return this;
    }

    /// <summary>
    /// Queues an insertion of text at the end of a block's content (before the closing delimiter).
    /// </summary>
    /// <param name="query">A query that selects SimpleBlock tokens.</param>
    /// <param name="text">The text to insert.</param>
    /// <param name="options">Optional tokenizer options.</param>
    public TokenBuffer InsertIntoBlockEnd(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var simpleTokens = LexText(text, options);
        var tokens = Tokens;
        
        foreach (var index in query.Select(tokens))
        {
            var token = tokens[index];
            if (token is not SimpleBlock block)
            {
                throw new InvalidOperationException($"InsertIntoBlockEnd requires a SimpleBlock, got {token.GetType().Name}");
            }
            
            // Insert before the closing delimiter
            var closingIndex = FindBlockClosingIndex(block);
            _mutations.Add(new InsertSimpleTokensMutation { Index = closingIndex, Tokens = simpleTokens });
        }
        
        return this;
    }

    #endregion

    #region Lexing Helpers

    private ImmutableArray<SimpleToken> LexText(string text, TokenizerOptions? options = null)
    {
        var opts = options ?? _defaultOptions;
        var lexer = new Lexer(opts);
        return lexer.LexToArray(text);
    }

    #endregion

    #region Commit and Preview

    /// <summary>
    /// Returns the Level 2 tokens that would result from applying all pending mutations.
    /// Does not modify internal state.
    /// </summary>
    public ImmutableArray<Token> Preview()
    {
        if (_mutations.Count == 0)
            return Tokens;

        var previewSimpleTokens = ApplyMutations(_simpleTokens, _mutations);
        var parser = new TokenParser(_defaultOptions);
        return parser.ParseToArray(previewSimpleTokens);
    }

    /// <summary>
    /// Returns the SimpleTokens that would result from applying all pending mutations.
    /// Does not modify internal state.
    /// </summary>
    public ImmutableArray<SimpleToken> PreviewSimpleTokens()
    {
        if (_mutations.Count == 0)
            return _simpleTokens;

        return ApplyMutations(_simpleTokens, _mutations);
    }

    /// <summary>
    /// Applies all pending mutations atomically, updating the internal state.
    /// Clears the mutation queue and invalidates the Level 2 cache.
    /// </summary>
    public TokenBuffer Commit()
    {
        if (_mutations.Count == 0)
            return this;

        _simpleTokens = ApplyMutations(_simpleTokens, _mutations);
        _mutations.Clear();
        InvalidateLevel2Cache();
        
        return this;
    }

    /// <summary>
    /// Discards all pending mutations without applying them.
    /// </summary>
    public TokenBuffer Rollback()
    {
        _mutations.Clear();
        return this;
    }

    #endregion

    #region Mutation Application

    private static ImmutableArray<SimpleToken> ApplyMutations(
        ImmutableArray<SimpleToken> tokens,
        List<SimpleTokenMutation> mutations)
    {
        if (mutations.Count == 0)
            return tokens;

        // Sort mutations by index descending, then by priority ascending
        var sortedMutations = mutations
            .OrderByDescending(m => m.Index)
            .ThenBy(m => m.Priority)
            .ToList();

        var result = tokens.ToBuilder();

        foreach (var mutation in sortedMutations)
        {
            ApplyMutation(result, mutation);
        }

        return result.ToImmutable();
    }

    private static void ApplyMutation(ImmutableArray<SimpleToken>.Builder tokens, SimpleTokenMutation mutation)
    {
        switch (mutation)
        {
            case RemoveSimpleTokenMutation remove:
                tokens.RemoveAt(remove.Index);
                break;

            case RemoveSimpleTokenRangeMutation removeRange:
                for (int i = 0; i < removeRange.Count; i++)
                {
                    tokens.RemoveAt(removeRange.Index);
                }
                break;

            case ReplaceSimpleTokensMutation replace:
                // Remove old tokens
                for (int i = 0; i < replace.RemoveCount; i++)
                {
                    tokens.RemoveAt(replace.Index);
                }
                // Insert new tokens
                for (int i = 0; i < replace.Tokens.Length; i++)
                {
                    tokens.Insert(replace.Index + i, replace.Tokens[i]);
                }
                break;

            case InsertSimpleTokensMutation insert:
                for (int i = 0; i < insert.Tokens.Length; i++)
                {
                    tokens.Insert(insert.Index + i, insert.Tokens[i]);
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown mutation type: {mutation.GetType().Name}");
        }
    }

    #endregion

    #region Compatibility Methods (Legacy Token-based API)

    /// <summary>
    /// Queues an insertion of a token at the specified Level 2 index.
    /// The token's content is lexed and inserted at the corresponding Level 1 position.
    /// </summary>
    [Obsolete("Use InsertText or InsertTextBefore/After with queries for better control")]
    public TokenBuffer Insert(int index, Token token)
    {
        var tokens = Tokens;
        if (index < 0 || index > tokens.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var text = token.ContentSpan.ToString();
        var simpleTokens = LexText(text);

        if (index == tokens.Length)
        {
            // Insert at end
            _mutations.Add(new InsertSimpleTokensMutation { Index = _simpleTokens.Length, Tokens = simpleTokens });
        }
        else
        {
            var targetToken = tokens[index];
            var simpleIndex = FindSimpleTokenIndex(targetToken);
            _mutations.Add(new InsertSimpleTokensMutation { Index = simpleIndex, Tokens = simpleTokens });
        }

        return this;
    }

    /// <summary>
    /// Queues the removal of a token at the specified Level 2 index.
    /// </summary>
    public TokenBuffer Remove(int index)
    {
        var tokens = Tokens;
        if (index < 0 || index >= tokens.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var token = tokens[index];
        var (start, end) = FindSimpleTokenRange(token);
        _mutations.Add(new RemoveSimpleTokenRangeMutation { Index = start, Count = end - start + 1 });
        
        return this;
    }

    /// <summary>
    /// Queues the removal of a range of tokens starting at the specified Level 2 index.
    /// </summary>
    public TokenBuffer RemoveRange(int index, int count)
    {
        var tokens = Tokens;
        if (index < 0 || index >= tokens.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index + count > tokens.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        // Remove each token's SimpleToken range (in reverse order for correct indices)
        for (int i = count - 1; i >= 0; i--)
        {
            var token = tokens[index + i];
            var (start, end) = FindSimpleTokenRange(token);
            _mutations.Add(new RemoveSimpleTokenRangeMutation { Index = start, Count = end - start + 1 });
        }
        
        return this;
    }

    /// <summary>
    /// Queues the replacement of a token at the specified Level 2 index.
    /// </summary>
    public TokenBuffer Replace(int index, Token replacement)
    {
        var tokens = Tokens;
        if (index < 0 || index >= tokens.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var original = tokens[index];
        var (start, end) = FindSimpleTokenRange(original);
        var replacementTokens = LexText(replacement.ContentSpan.ToString());
        
        _mutations.Add(new ReplaceSimpleTokensMutation
        {
            Index = start,
            RemoveCount = end - start + 1,
            Tokens = replacementTokens
        });
        
        return this;
    }

    /// <summary>
    /// Queues insertions of multiple tokens at the specified Level 2 index.
    /// </summary>
    [Obsolete("Use InsertText for better control")]
    public TokenBuffer InsertRange(int index, IEnumerable<Token> tokensToInsert)
    {
        var text = string.Concat(tokensToInsert.Select(t => t.ContentSpan.ToString()));
        var tokens = Tokens;
        
        if (index < 0 || index > tokens.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var simpleTokens = LexText(text);

        if (index == tokens.Length)
        {
            _mutations.Add(new InsertSimpleTokensMutation { Index = _simpleTokens.Length, Tokens = simpleTokens });
        }
        else
        {
            var targetToken = tokens[index];
            var simpleIndex = FindSimpleTokenIndex(targetToken);
            _mutations.Add(new InsertSimpleTokensMutation { Index = simpleIndex, Tokens = simpleTokens });
        }

        return this;
    }

    /// <summary>
    /// Queues insertions before all tokens matching the query.
    /// </summary>
    public TokenBuffer InsertRangeBefore(TokenQuery query, IEnumerable<Token> tokensToInsert)
    {
        var text = string.Concat(tokensToInsert.Select(t => t.ContentSpan.ToString()));
        return InsertTextBefore(query, text);
    }

    /// <summary>
    /// Queues insertions after all tokens matching the query.
    /// </summary>
    public TokenBuffer InsertRangeAfter(TokenQuery query, IEnumerable<Token> tokensToInsert)
    {
        var text = string.Concat(tokensToInsert.Select(t => t.ContentSpan.ToString()));
        return InsertTextAfter(query, text);
    }

    /// <summary>
    /// Queues replacement of tokens with multiple tokens.
    /// </summary>
    public TokenBuffer ReplaceWithMany(TokenQuery query, Func<Token, IEnumerable<Token>> replacer)
    {
        return Replace(query, t => string.Concat(replacer(t).Select(r => r.ContentSpan.ToString())));
    }

    #endregion
}
