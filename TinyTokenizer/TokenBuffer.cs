using System.Collections.Immutable;

namespace TinyTokenizer;

/// <summary>
/// A mutable token collection with deferred operations.
/// Mutations are queued and applied atomically when <see cref="Commit"/> is called.
/// Designed for future undo/redo support via snapshot storage.
/// </summary>
/// <example>
/// <code>
/// var result = tokens.ToBuffer()
///     .Remove(Query.Comment.All())
///     .Replace(Query.Ident.WithContent("foo"), t => new IdentToken { Content = "bar".AsMemory(), Position = t.Position })
///     .InsertAfter(Query.Ident.WithContent("import").First(), newToken)
///     .Commit()
///     .Tokens;
/// </code>
/// </example>
public sealed class TokenBuffer
{
    #region Fields

    private ImmutableArray<Token> _tokens;
    private readonly List<TokenMutation> _mutations = [];
    private TokenizerOptions _defaultOptions;

    // Reserved for future undo/redo support
    // private readonly Stack<ImmutableArray<Token>> _undoStack = new();
    // private readonly Stack<ImmutableArray<Token>> _redoStack = new();

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="TokenBuffer"/> with the specified tokens.
    /// </summary>
    /// <param name="tokens">The initial token array.</param>
    public TokenBuffer(ImmutableArray<Token> tokens)
    {
        _tokens = tokens;
        _defaultOptions = TokenizerOptions.Default;
    }

    /// <summary>
    /// Initializes a new <see cref="TokenBuffer"/> with the specified tokens and default options.
    /// </summary>
    /// <param name="tokens">The initial token array.</param>
    /// <param name="options">The default tokenizer options for text injection.</param>
    public TokenBuffer(ImmutableArray<Token> tokens, TokenizerOptions options)
    {
        _tokens = tokens;
        _defaultOptions = options;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current committed token array.
    /// </summary>
    public ImmutableArray<Token> Tokens => _tokens;

    /// <summary>
    /// Gets the number of tokens in the current committed state.
    /// </summary>
    public int Count => _tokens.Length;

    /// <summary>
    /// Gets the number of pending mutations.
    /// </summary>
    public int PendingMutationCount => _mutations.Count;

    /// <summary>
    /// Gets whether there are pending mutations to commit.
    /// </summary>
    public bool HasPendingMutations => _mutations.Count > 0;

    /// <summary>
    /// Gets or sets the default tokenizer options for text injection.
    /// </summary>
    public TokenizerOptions DefaultOptions
    {
        get => _defaultOptions;
        set => _defaultOptions = value;
    }

    #endregion

    #region Index-Based Mutations

    /// <summary>
    /// Queues an insertion of a single token at the specified index.
    /// </summary>
    /// <param name="index">The index at which to insert (0 to Count inclusive).</param>
    /// <param name="token">The token to insert.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public TokenBuffer Insert(int index, Token token)
    {
        var mutation = new InsertMutation { Index = index, Token = token };
        mutation.Validate(_tokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    /// <summary>
    /// Queues an insertion of multiple tokens at the specified index.
    /// </summary>
    /// <param name="index">The index at which to insert (0 to Count inclusive).</param>
    /// <param name="tokens">The tokens to insert.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public TokenBuffer InsertRange(int index, IEnumerable<Token> tokens)
    {
        var tokenArray = tokens.ToImmutableArray();
        if (tokenArray.IsEmpty)
            return this;

        var mutation = new InsertRangeMutation { Index = index, Tokens = tokenArray };
        mutation.Validate(_tokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    /// <summary>
    /// Queues the removal of a single token at the specified index.
    /// </summary>
    /// <param name="index">The index of the token to remove.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public TokenBuffer Remove(int index)
    {
        var mutation = new RemoveMutation { Index = index };
        mutation.Validate(_tokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    /// <summary>
    /// Queues the removal of a range of tokens starting at the specified index.
    /// </summary>
    /// <param name="index">The start index of the range to remove.</param>
    /// <param name="count">The number of tokens to remove.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the range is out of bounds.</exception>
    public TokenBuffer RemoveRange(int index, int count)
    {
        var mutation = new RemoveRangeMutation { Index = index, Count = count };
        mutation.Validate(_tokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    /// <summary>
    /// Queues the replacement of a single token at the specified index.
    /// </summary>
    /// <param name="index">The index of the token to replace.</param>
    /// <param name="token">The replacement token.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public TokenBuffer Replace(int index, Token token)
    {
        var mutation = new ReplaceMutation { Index = index, Token = token };
        mutation.Validate(_tokens.Length);
        _mutations.Add(mutation);
        return this;
    }

    #endregion

    #region Query-Based Mutations

    /// <summary>
    /// Queues the removal of all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting tokens to remove.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer Remove(TokenQuery query)
    {
        var indices = query.Select(_tokens).ToList();
        
        // Queue removals in reverse order so indices remain valid
        foreach (var index in indices.OrderByDescending(i => i))
        {
            _mutations.Add(new RemoveMutation { Index = index });
        }
        
        return this;
    }

    /// <summary>
    /// Queues the replacement of all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting tokens to replace.</param>
    /// <param name="replacer">A function that creates a replacement token from the original.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer Replace(TokenQuery query, Func<Token, Token> replacer)
    {
        foreach (var index in query.Select(_tokens))
        {
            var original = _tokens[index];
            var replacement = replacer(original);
            _mutations.Add(new ReplaceMutation { Index = index, Token = replacement });
        }
        
        return this;
    }

    /// <summary>
    /// Queues the replacement of all tokens matching the query with multiple tokens.
    /// </summary>
    /// <param name="query">The query selecting tokens to replace.</param>
    /// <param name="replacer">A function that creates replacement tokens from the original.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer ReplaceWithMany(TokenQuery query, Func<Token, IEnumerable<Token>> replacer)
    {
        foreach (var index in query.Select(_tokens))
        {
            var original = _tokens[index];
            var replacements = replacer(original).ToImmutableArray();
            _mutations.Add(new ReplaceWithRangeMutation { Index = index, Tokens = replacements });
        }
        
        return this;
    }

    /// <summary>
    /// Queues an insertion before all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting target positions.</param>
    /// <param name="token">The token to insert.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertBefore(TokenQuery query, Token token)
    {
        foreach (var index in query.Select(_tokens))
        {
            _mutations.Add(new InsertMutation { Index = index, Token = token });
        }
        
        return this;
    }

    /// <summary>
    /// Queues an insertion after all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting target positions.</param>
    /// <param name="token">The token to insert.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertAfter(TokenQuery query, Token token)
    {
        foreach (var index in query.Select(_tokens))
        {
            // Insert after = insert at index + 1
            _mutations.Add(new InsertMutation { Index = index + 1, Token = token });
        }
        
        return this;
    }

    /// <summary>
    /// Queues insertions before all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting target positions.</param>
    /// <param name="tokens">The tokens to insert.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertRangeBefore(TokenQuery query, IEnumerable<Token> tokens)
    {
        var tokenArray = tokens.ToImmutableArray();
        if (tokenArray.IsEmpty)
            return this;

        foreach (var index in query.Select(_tokens))
        {
            _mutations.Add(new InsertRangeMutation { Index = index, Tokens = tokenArray });
        }
        
        return this;
    }

    /// <summary>
    /// Queues insertions after all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting target positions.</param>
    /// <param name="tokens">The tokens to insert.</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertRangeAfter(TokenQuery query, IEnumerable<Token> tokens)
    {
        var tokenArray = tokens.ToImmutableArray();
        if (tokenArray.IsEmpty)
            return this;

        foreach (var index in query.Select(_tokens))
        {
            _mutations.Add(new InsertRangeMutation { Index = index + 1, Tokens = tokenArray });
        }
        
        return this;
    }

    #endregion

    #region Text Injection

    /// <summary>
    /// Queues an insertion of tokens parsed from text at the specified index.
    /// </summary>
    /// <param name="index">The index at which to insert.</param>
    /// <param name="text">The text to tokenize and insert.</param>
    /// <param name="options">Optional tokenizer options (uses DefaultOptions if null).</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertText(int index, string text, TokenizerOptions? options = null)
    {
        var tokens = TokenizeText(text, options);
        return InsertRange(index, tokens);
    }

    /// <summary>
    /// Queues an insertion of tokens parsed from text before all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting target positions.</param>
    /// <param name="text">The text to tokenize and insert.</param>
    /// <param name="options">Optional tokenizer options (uses DefaultOptions if null).</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertTextBefore(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var tokens = TokenizeText(text, options);
        return InsertRangeBefore(query, tokens);
    }

    /// <summary>
    /// Queues an insertion of tokens parsed from text after all tokens matching the query.
    /// </summary>
    /// <param name="query">The query selecting target positions.</param>
    /// <param name="text">The text to tokenize and insert.</param>
    /// <param name="options">Optional tokenizer options (uses DefaultOptions if null).</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer InsertTextAfter(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var tokens = TokenizeText(text, options);
        return InsertRangeAfter(query, tokens);
    }

    /// <summary>
    /// Queues the replacement of all tokens matching the query with tokens parsed from text.
    /// </summary>
    /// <param name="query">The query selecting tokens to replace.</param>
    /// <param name="text">The text to tokenize and use as replacement.</param>
    /// <param name="options">Optional tokenizer options (uses DefaultOptions if null).</param>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer ReplaceWithText(TokenQuery query, string text, TokenizerOptions? options = null)
    {
        var tokens = TokenizeText(text, options);
        return ReplaceWithMany(query, _ => tokens);
    }

    private ImmutableArray<Token> TokenizeText(string text, TokenizerOptions? options)
    {
        var opts = options ?? _defaultOptions;
        var lexer = new Lexer(opts);
        var parser = new TokenParser(opts);
        return parser.ParseToArray(lexer.Lex(text));
    }

    #endregion

    #region Commit and Preview

    /// <summary>
    /// Returns the result of applying all pending mutations without modifying internal state.
    /// Useful for validation or debugging before committing.
    /// </summary>
    /// <returns>The token array that would result from committing.</returns>
    public ImmutableArray<Token> Preview()
    {
        if (_mutations.Count == 0)
            return _tokens;

        return ApplyMutations(_tokens, _mutations);
    }

    /// <summary>
    /// Applies all pending mutations atomically, updating the internal state.
    /// Clears the mutation queue after successful application.
    /// </summary>
    /// <returns>This buffer for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if mutations conflict or are invalid.</exception>
    public TokenBuffer Commit()
    {
        if (_mutations.Count == 0)
            return this;

        // Future: push current state to undo stack before applying
        // _undoStack.Push(_tokens);
        // _redoStack.Clear();

        _tokens = ApplyMutations(_tokens, _mutations);
        _mutations.Clear();
        
        return this;
    }

    /// <summary>
    /// Discards all pending mutations without applying them.
    /// </summary>
    /// <returns>This buffer for fluent chaining.</returns>
    public TokenBuffer Rollback()
    {
        _mutations.Clear();
        return this;
    }

    #endregion

    #region Mutation Application

    private static ImmutableArray<Token> ApplyMutations(ImmutableArray<Token> tokens, List<TokenMutation> mutations)
    {
        if (mutations.Count == 0)
            return tokens;

        // Group and sort mutations:
        // 1. By index descending (apply from end to start so indices don't shift)
        // 2. By priority ascending (removals before replacements before insertions at same index)
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

    private static void ApplyMutation(ImmutableArray<Token>.Builder tokens, TokenMutation mutation)
    {
        switch (mutation)
        {
            case RemoveMutation remove:
                tokens.RemoveAt(remove.Index);
                break;

            case RemoveRangeMutation removeRange:
                for (int i = 0; i < removeRange.Count; i++)
                {
                    tokens.RemoveAt(removeRange.Index);
                }
                break;

            case ReplaceMutation replace:
                tokens[replace.Index] = replace.Token;
                break;

            case ReplaceWithRangeMutation replaceRange:
                tokens.RemoveAt(replaceRange.Index);
                for (int i = 0; i < replaceRange.Tokens.Length; i++)
                {
                    tokens.Insert(replaceRange.Index + i, replaceRange.Tokens[i]);
                }
                break;

            case InsertMutation insert:
                tokens.Insert(insert.Index, insert.Token);
                break;

            case InsertRangeMutation insertRange:
                for (int i = 0; i < insertRange.Tokens.Length; i++)
                {
                    tokens.Insert(insertRange.Index + i, insertRange.Tokens[i]);
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown mutation type: {mutation.GetType().Name}");
        }
    }

    #endregion
}
