using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Lexer that produces GreenNodes directly from source text.
/// Optimized path that skips intermediate Token allocations.
/// Handles trivia attachment and recursive block parsing.
/// </summary>
internal sealed class GreenLexer
{
    private readonly TokenizerOptions _options;
    private readonly Lexer _charLexer;
    private readonly OperatorTrie _operatorTrie;
    private readonly ImmutableDictionary<string, NodeKind>? _keywordsCaseSensitive;
    private readonly ImmutableDictionary<string, NodeKind>? _keywordsCaseInsensitive;
    
    /// <summary>
    /// Creates a new GreenLexer with default options.
    /// </summary>
    public GreenLexer() : this(TokenizerOptions.Default)
    {
    }
    
    /// <summary>
    /// Creates a new GreenLexer with the specified options.
    /// </summary>
    public GreenLexer(TokenizerOptions options) : this(options, null, null)
    {
    }
    
    /// <summary>
    /// Creates a new GreenLexer with the specified options and keyword dictionaries.
    /// </summary>
    /// <param name="options">Tokenizer options.</param>
    /// <param name="keywordsCaseSensitive">Case-sensitive keyword lookup dictionary. If null, no case-sensitive keywords.</param>
    /// <param name="keywordsCaseInsensitive">Case-insensitive keyword lookup dictionary. If null, no case-insensitive keywords.</param>
    public GreenLexer(
        TokenizerOptions options, 
        ImmutableDictionary<string, NodeKind>? keywordsCaseSensitive,
        ImmutableDictionary<string, NodeKind>? keywordsCaseInsensitive)
    {
        _options = options;
        _charLexer = new Lexer(options);
        _keywordsCaseSensitive = keywordsCaseSensitive;
        _keywordsCaseInsensitive = keywordsCaseInsensitive;
        // Build operator trie for O(k) greedy matching
        _operatorTrie = new OperatorTrie();
        foreach (var op in options.Operators)
        {
            _operatorTrie.Add(op);
        }
    }
    
    #region Public API
    
    /// <summary>
    /// Parses source text into a SyntaxTree.
    /// </summary>
    public SyntaxTree Parse(string source)
    {
        return Parse(source.AsMemory());
    }
    
    /// <summary>
    /// Parses source text into a SyntaxTree.
    /// </summary>
    public SyntaxTree Parse(ReadOnlyMemory<char> source)
    {
        var simpleTokens = _charLexer.LexToArray(source);
        var greenNodes = ParseTokens(simpleTokens);
        var root = new GreenList(greenNodes);
        return new SyntaxTree(root);
    }
    
    /// <summary>
    /// Parses source text into green nodes (without wrapping in SyntaxTree).
    /// </summary>
    public ImmutableArray<GreenNode> ParseToGreenNodes(string source)
    {
        return ParseToGreenNodes(source.AsMemory());
    }
    
    /// <summary>
    /// Parses source text into green nodes (without wrapping in SyntaxTree).
    /// </summary>
    public ImmutableArray<GreenNode> ParseToGreenNodes(ReadOnlyMemory<char> source)
    {
        var simpleTokens = _charLexer.LexToArray(source);
        return ParseTokens(simpleTokens);
    }
    
    #endregion
    
    #region Token Parsing
    
    private ImmutableArray<GreenNode> ParseTokens(ImmutableArray<SimpleToken> tokens)
    {
        if (tokens.IsEmpty)
            return ImmutableArray<GreenNode>.Empty;
        
        var builder = ImmutableArray.CreateBuilder<GreenNode>();
        var reader = new TokenReader(tokens);
        
        // First token gets all initial trivia as leading
        var leadingTrivia = CollectLeadingTrivia(ref reader);
        
        while (reader.HasMore)
        {
            // Parse the main node with leading trivia
            var node = ParseNode(ref reader, null, leadingTrivia);
            if (node != null)
            {
                // Collect trailing trivia (same-line content up to and including newline)
                var trailingTrivia = CollectTrailingTrivia(ref reader);
                node = AttachTrailingTrivia(node, trailingTrivia);
                builder.Add(node);
            }
            
            // Collect leading trivia for next token (newlines from prev line + indentation)
            leadingTrivia = CollectLeadingTrivia(ref reader);
        }
        
        // EOF: any remaining trivia (collected as leadingTrivia) goes to last node as trailing
        if (!leadingTrivia.IsEmpty)
        {
            if (builder.Count > 0)
            {
                var last = builder[^1];
                builder[^1] = AttachTrailingTrivia(last, leadingTrivia);
            }
            else
            {
                // Only trivia, no actual nodes - convert trivia to standalone nodes
                // This handles cases like parsing just "// comment" where the comment
                // is treated as leading trivia but there's no subsequent token.
                foreach (var trivia in leadingTrivia)
                {
                    // Create a leaf node from the trivia content
                    var triviaNode = new GreenLeaf(NodeKind.Symbol, trivia.Text, 
                        ImmutableArray<GreenTrivia>.Empty, ImmutableArray<GreenTrivia>.Empty);
                    builder.Add(triviaNode);
                }
            }
        }
        
        return builder.ToImmutable();
    }
    
    private GreenNode? ParseNode(
        ref TokenReader reader,
        SimpleTokenType? expectedCloser,
        ImmutableArray<GreenTrivia> leadingTrivia)
    {
        if (!reader.TryPeek(out var token))
            return null;
        
        // Check for expected closing delimiter
        if (expectedCloser.HasValue && token.Type == expectedCloser.Value)
            return null;
        
        // Check for unexpected closing delimiter
        if (TokenizerCore.IsClosingDelimiter(token.Type) && token.Type != expectedCloser)
        {
            reader.Advance();
            // Leading-only trivia model: no trailing trivia
            return new GreenLeaf(NodeKind.Error, token.Content.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
        }
        
        // Opening delimiter - parse block
        if (TokenizerCore.IsOpeningDelimiter(token.Type))
        {
            return ParseBlock(ref reader, leadingTrivia);
        }
        
        // Quote - parse string
        if (token.Type == SimpleTokenType.SingleQuote || token.Type == SimpleTokenType.DoubleQuote)
        {
            return ParseString(ref reader, leadingTrivia);
        }
        
        // Digits - parse numeric
        if (token.Type == SimpleTokenType.Digits)
        {
            return ParseNumeric(ref reader, leadingTrivia);
        }
        
        // Dot followed by digits - decimal number
        if (token.Type == SimpleTokenType.Dot)
        {
            if (reader.TryPeek(1, out var next) && next.Type == SimpleTokenType.Digits)
            {
                return ParseDecimalFromDot(ref reader, leadingTrivia);
            }
        }
        
        // Comment detection
        if (token.Type == SimpleTokenType.Slash)
        {
            if (reader.TryPeek(1, out var next))
            {
                if (next.Type == SimpleTokenType.Slash && HasCommentStyle("//"))
                {
                    return ParseSingleLineComment(ref reader, leadingTrivia);
                }
                if (next.Type == SimpleTokenType.Asterisk && HasCommentStyle("/*"))
                {
                    return ParseMultiLineComment(ref reader, leadingTrivia);
                }
            }
        }
        
        // Tagged identifier
        if (IsTagPrefix(token))
        {
            if (reader.TryPeek(1, out var next) && next.Type == SimpleTokenType.Ident)
            {
                return ParseTaggedIdent(ref reader, leadingTrivia);
            }
        }
        
        // Operator
        var operatorNode = TryParseOperator(ref reader, leadingTrivia);
        if (operatorNode != null)
            return operatorNode;
        
        // Identifier (check for keywords if lookup is available)
        if (token.Type == SimpleTokenType.Ident)
        {
            var text = reader.CurrentText;
            reader.Advance();
            
            // Check if this identifier is a registered keyword (case-sensitive first, then case-insensitive)
            var identKind = NodeKind.Ident;
            if (_keywordsCaseSensitive != null && _keywordsCaseSensitive.TryGetValue(text, out var kind))
            {
                identKind = kind;
            }
            else if (_keywordsCaseInsensitive != null && _keywordsCaseInsensitive.TryGetValue(text, out kind))
            {
                identKind = kind;
            }
            
            // Leading-only trivia model: no trailing trivia
            return GreenNodeCache.Create(identKind, text, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
        }
        
        // Symbol (single character)
        var symbolText = reader.CurrentText;
        reader.Advance();
        // Leading-only trivia model: no trailing trivia
        var symbolKind = GetLeafKind(token.Type);
        return GreenNodeCache.Create(symbolKind, symbolText, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenBlock ParseBlock(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var openToken = reader.Current;
        reader.Advance();
        
        var opener = openToken.FirstChar;
        var closerType = TokenizerCore.GetMatchingCloser(openToken.Type);
        var closerChar = TokenizerCore.GetClosingDelimiter(opener);
        
        var children = ImmutableArray.CreateBuilder<GreenNode>();
        
        // Collect trailing trivia for opener (stops at newline, per trivia model)
        var openerTrailingTrivia = CollectTrailingTrivia(ref reader);
        
        // Collect leading trivia for first child (indentation after opener's trailing newline)
        var firstChildLeading = CollectLeadingTrivia(ref reader);
        
        // This will hold trivia before the next element (leading trivia for next child or closer)
        var childLeading = firstChildLeading;
        
        while (reader.HasMore)
        {
            var token = reader.Current;
            
            if (token.Type == closerType)
            {
                // Found closer - childLeading becomes closer's leading trivia
                reader.Advance();
                
                // Create opener node with leading trivia (before opener) and trailing trivia (after opener)
                var openerNode = GreenNodeCache.CreateDelimiter(opener, leadingTrivia, openerTrailingTrivia);
                
                // Create closer node with leading trivia (before closer, after last child)
                var closerNode = GreenNodeCache.CreateDelimiter(closerChar, childLeading);
                
                return new GreenBlock(openerNode, closerNode, children.ToImmutable());
            }
            
            // Parse child node with its leading trivia
            var child = ParseNode(ref reader, closerType, childLeading);
            if (child != null)
            {
                // Collect trailing trivia for this child
                var childTrailing = CollectTrailingTrivia(ref reader);
                child = AttachTrailingTrivia(child, childTrailing);
                children.Add(child);
            }
            
            // Collect leading trivia for next child (or closer)
            childLeading = CollectLeadingTrivia(ref reader);
        }
        
        // Unclosed block - create with whatever we have
        var openerNodeUnclosed = GreenNodeCache.CreateDelimiter(opener, leadingTrivia, openerTrailingTrivia);
        var closerNodeUnclosed = GreenNodeCache.CreateDelimiter(closerChar, childLeading);
        return new GreenBlock(openerNodeUnclosed, closerNodeUnclosed, children.ToImmutable());
    }
    
    private GreenLeaf ParseString(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var quoteToken = reader.Current;
        var quoteChar = quoteToken.FirstChar;
        var quoteType = quoteToken.Type;
        reader.Advance();
        
        var textBuilder = new System.Text.StringBuilder();
        textBuilder.Append(quoteChar);
        
        bool escaped = false;
        
        while (reader.HasMore)
        {
            var token = reader.Current;
            
            if (escaped)
            {
                textBuilder.Append(reader.CurrentText);
                reader.Advance();
                escaped = false;
                continue;
            }
            
            if (token.Type == SimpleTokenType.Backslash)
            {
                textBuilder.Append('\\');
                reader.Advance();
                escaped = true;
                continue;
            }
            
            if (token.Type == quoteType)
            {
                textBuilder.Append(quoteChar);
                reader.Advance();
                // Leading-only trivia model: no trailing trivia
                return new GreenLeaf(NodeKind.String, textBuilder.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
            }
            
            textBuilder.Append(reader.CurrentText);
            reader.Advance();
        }
        
        // Unterminated string - return what we have
        // Leading-only trivia model: no trailing trivia
        return new GreenLeaf(NodeKind.Error, textBuilder.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenLeaf ParseNumeric(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var digitsText = reader.CurrentText;
        reader.Advance();
        
        // Check for decimal point followed by more digits
        if (reader.TryPeek(out var dotToken) && dotToken.Type == SimpleTokenType.Dot)
        {
            if (reader.TryPeek(1, out var afterDot) && afterDot.Type == SimpleTokenType.Digits)
            {
                reader.Advance(); // consume dot
                var afterDotText = reader.CurrentText;
                reader.Advance(); // consume digits
                var text = digitsText + "." + afterDotText;
                // Leading-only trivia model: no trailing trivia
                return new GreenLeaf(NodeKind.Numeric, text, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
            }
        }
        
        // Leading-only trivia model: no trailing trivia
        return new GreenLeaf(NodeKind.Numeric, digitsText, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenLeaf ParseDecimalFromDot(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        reader.Advance(); // consume dot
        var digitsText = reader.CurrentText;
        reader.Advance(); // consume digits
        
        var text = "." + digitsText;
        // Leading-only trivia model: no trailing trivia
        return new GreenLeaf(NodeKind.Numeric, text, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenLeaf ParseTaggedIdent(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var tagText = reader.CurrentText;
        reader.Advance();
        var identText = reader.CurrentText;
        reader.Advance();
        
        var text = tagText + identText;
        // Leading-only trivia model: no trailing trivia
        return new GreenLeaf(NodeKind.TaggedIdent, text, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenLeaf ParseSingleLineComment(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var textBuilder = new System.Text.StringBuilder();
        
        // Consume // 
        textBuilder.Append(reader.CurrentText);
        reader.Advance();
        textBuilder.Append(reader.CurrentText);
        reader.Advance();
        
        // Consume until newline
        while (reader.HasMore)
        {
            var token = reader.Current;
            if (token.Type == SimpleTokenType.Newline)
                break;
            textBuilder.Append(reader.CurrentText);
            reader.Advance();
        }
        
        // Leading-only trivia model: no trailing trivia
        return new GreenLeaf(NodeKind.Symbol, textBuilder.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenLeaf ParseMultiLineComment(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var textBuilder = new System.Text.StringBuilder();
        
        // Consume /*
        textBuilder.Append(reader.CurrentText);
        reader.Advance();
        textBuilder.Append(reader.CurrentText);
        reader.Advance();
        
        // Consume until */
        while (reader.HasMore)
        {
            var token = reader.Current;
            
            if (token.Type == SimpleTokenType.Asterisk)
            {
                if (reader.TryPeek(1, out var next) && next.Type == SimpleTokenType.Slash)
                {
                    textBuilder.Append('*');
                    reader.Advance();
                    textBuilder.Append('/');
                    reader.Advance();
                    // Leading-only trivia model: no trailing trivia
                    return new GreenLeaf(NodeKind.Symbol, textBuilder.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
                }
            }
            
            textBuilder.Append(reader.CurrentText);
            reader.Advance();
        }
        
        // Unterminated - return as error
        return new GreenLeaf(NodeKind.Error, textBuilder.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenLeaf? TryParseOperator(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        if (_operatorTrie.IsEmpty)
            return null;
        
        // Build sequence of operator-capable characters for trie matching
        Span<char> chars = stackalloc char[16]; // Most operators are short
        int charCount = 0;
        int offset = 0;
        
        while (reader.TryPeek(offset, out var token))
        {
            var c = TokenizerCore.GetOperatorChar(token.Type);
            if (c == null)
            {
                if (token.Type == SimpleTokenType.Symbol && token.Content.Length == 1)
                    c = token.FirstChar;
                else
                    break;
            }
            
            if (charCount >= chars.Length)
                break; // Safety limit
            
            chars[charCount++] = c.Value;
            offset++;
        }
        
        if (charCount == 0)
            return null;
        
        // Use trie for O(k) greedy matching
        if (_operatorTrie.TryMatch(chars[..charCount], out var matchedOp) && matchedOp is not null)
        {
            // Consume tokens for this operator
            for (int i = 0; i < matchedOp.Length; i++)
                reader.Advance();
            
            // Leading-only trivia model: no trailing trivia
            return GreenNodeCache.Create(NodeKind.Operator, matchedOp, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
        }
        
        return null;
    }
    
    #endregion
    
    #region Trivia Collection
    
    /// <summary>
    /// Collects leading trivia: everything from after previous token's trailing trivia
    /// up to the current token. This includes newlines from previous lines and indentation.
    /// </summary>
    private ImmutableArray<GreenTrivia> CollectLeadingTrivia(ref TokenReader reader)
    {
        var builder = ImmutableArray.CreateBuilder<GreenTrivia>();
        
        while (reader.HasMore)
        {
            var token = reader.Current;
            
            if (token.Type == SimpleTokenType.Whitespace)
            {
                builder.Add(GreenTrivia.Whitespace(reader.CurrentText));
                reader.Advance();
            }
            else if (token.Type == SimpleTokenType.Newline)
            {
                builder.Add(GreenTrivia.Newline(reader.CurrentText));
                reader.Advance();
            }
            else if (IsCommentStart(ref reader))
            {
                var comment = ConsumeComment(ref reader);
                builder.Add(comment);
            }
            else
            {
                break;
            }
        }
        
        return builder.ToImmutable();
    }
    
    /// <summary>
    /// Collects trailing trivia: same-line content after the token up to and including newline.
    /// - Whitespace on same line → trailing
    /// - Comments starting on same line → trailing (even if multi-line)
    /// - Newline → trailing (terminates collection)
    /// </summary>
    private ImmutableArray<GreenTrivia> CollectTrailingTrivia(ref TokenReader reader)
    {
        var builder = ImmutableArray.CreateBuilder<GreenTrivia>();
        
        while (reader.HasMore)
        {
            var token = reader.Current;
            
            if (token.Type == SimpleTokenType.Whitespace)
            {
                // Same-line whitespace is trailing
                builder.Add(GreenTrivia.Whitespace(reader.CurrentText));
                reader.Advance();
            }
            else if (token.Type == SimpleTokenType.Newline)
            {
                // Newline terminates trailing trivia (and is included in it)
                builder.Add(GreenTrivia.Newline(reader.CurrentText));
                reader.Advance();
                break; // Stop after newline
            }
            else if (IsCommentStart(ref reader))
            {
                // Comment starting on same line is trailing (even if it spans lines)
                var comment = ConsumeComment(ref reader);
                builder.Add(comment);
                // If it was a single-line comment, the newline wasn't consumed, so continue
                // If it was a multi-line comment, continue collecting trailing
            }
            else
            {
                // Hit a significant token - stop collecting trailing
                break;
            }
        }
        
        return builder.ToImmutable();
    }
    
    /// <summary>
    /// Attaches trailing trivia to a node (fallback when leading trivia isn't possible).
    /// Creates a new node with the trivia appended to existing trailing trivia.
    /// </summary>
    private static GreenNode AttachTrailingTrivia(GreenNode node, ImmutableArray<GreenTrivia> trivia)
    {
        if (trivia.IsEmpty)
            return node;
        
        return node switch
        {
            GreenLeaf leaf => new GreenLeaf(
                leaf.Kind, 
                leaf.Text, 
                leaf.LeadingTrivia, 
                leaf.TrailingTrivia.AddRange(trivia)),
            
            // Block nodes: trailing trivia goes to the closer's trailing trivia (after closer)
            GreenBlock block => block.WithTrailingTrivia(block.CloserNode.TrailingTrivia.AddRange(trivia)),
            
            GreenList list when list.SlotCount > 0 => 
                list.WithSlot(list.SlotCount - 1, 
                    AttachTrailingTrivia(list.GetSlot(list.SlotCount - 1)!, trivia)),
            
            GreenSyntaxNode syntax when syntax.SlotCount > 0 =>
                new GreenSyntaxNode(syntax.Kind, 
                    syntax.Children.SetItem(syntax.Children.Length - 1, 
                        AttachTrailingTrivia(syntax.GetSlot(syntax.Children.Length - 1)!, trivia))),
            
            _ => node // Unknown node type or empty container, return unchanged
        };
    }
    
    /// <summary>
    /// Attaches trivia to the deepest last child of a node.
    /// Used for "before closer" trivia inside blocks that should go to the last token.
    /// Note: With the new GreenBlock design, this is rarely needed as blocks store
    /// "before closer" trivia in CloserNode.LeadingTrivia.
    /// </summary>
    private static GreenNode AttachTrailingTriviaToDeepestChild(GreenNode node, ImmutableArray<GreenTrivia> trivia)
    {
        if (trivia.IsEmpty)
            return node;
        
        return node switch
        {
            GreenLeaf leaf => new GreenLeaf(
                leaf.Kind, 
                leaf.Text, 
                leaf.LeadingTrivia, 
                leaf.TrailingTrivia.AddRange(trivia)),
            
            GreenBlock block when block.SlotCount > 0 => 
                // Recursively attach to last child of block
                block.WithSlot(block.SlotCount - 1, 
                    AttachTrailingTriviaToDeepestChild(block.GetSlot(block.SlotCount - 1)!, trivia)),
            
            GreenBlock block => 
                // Empty block - attach to closer's trailing trivia
                block.WithTrailingTrivia(block.CloserNode.TrailingTrivia.AddRange(trivia)),
            
            GreenList list when list.SlotCount > 0 => 
                list.WithSlot(list.SlotCount - 1, 
                    AttachTrailingTriviaToDeepestChild(list.GetSlot(list.SlotCount - 1)!, trivia)),
            
            GreenSyntaxNode syntax when syntax.SlotCount > 0 =>
                new GreenSyntaxNode(syntax.Kind, 
                    syntax.Children.SetItem(syntax.Children.Length - 1, 
                        AttachTrailingTriviaToDeepestChild(syntax.GetSlot(syntax.Children.Length - 1)!, trivia))),
            
            _ => node // Unknown node type or empty container, return unchanged
        };
    }
    
    // Roslyn-style trivia model:
    // - Trailing trivia = same-line content after token up to and including newline
    // - Leading trivia = content from after previous token's trailing up to current token
    // - First token gets all initial trivia as leading
    // - EOF: remaining trivia goes to last token as trailing
    
    private bool IsCommentStart(ref TokenReader reader)
    {
        if (!reader.TryPeek(out var token) || token.Type != SimpleTokenType.Slash)
            return false;
        
        if (!reader.TryPeek(1, out var next))
            return false;
        
        return (next.Type == SimpleTokenType.Slash && HasCommentStyle("//")) ||
               (next.Type == SimpleTokenType.Asterisk && HasCommentStyle("/*"));
    }
    
    private GreenTrivia ConsumeComment(ref TokenReader reader)
    {
        var textBuilder = new System.Text.StringBuilder();
        
        reader.TryPeek(1, out var second);
        bool isMultiLine = second.Type == SimpleTokenType.Asterisk;
        
        // Consume start
        textBuilder.Append(reader.CurrentText);
        reader.Advance();
        textBuilder.Append(reader.CurrentText);
        reader.Advance();
        
        if (isMultiLine)
        {
            // Consume until */
            while (reader.HasMore)
            {
                var token = reader.Current;
                if (token.Type == SimpleTokenType.Asterisk)
                {
                    if (reader.TryPeek(1, out var next) && next.Type == SimpleTokenType.Slash)
                    {
                        textBuilder.Append('*');
                        reader.Advance();
                        textBuilder.Append('/');
                        reader.Advance();
                        break;
                    }
                }
                textBuilder.Append(reader.CurrentText);
                reader.Advance();
            }
            return GreenTrivia.MultiLineComment(textBuilder.ToString());
        }
        else
        {
            // Consume until newline (don't consume the newline)
            while (reader.HasMore)
            {
                var token = reader.Current;
                if (token.Type == SimpleTokenType.Newline)
                    break;
                textBuilder.Append(reader.CurrentText);
                reader.Advance();
            }
            return GreenTrivia.SingleLineComment(textBuilder.ToString());
        }
    }
    
    #endregion
    
    #region Helpers
    
    private bool HasCommentStyle(string start)
    {
        return _options.CommentStyles.Any(s => s.Start == start);
    }
    
    private bool IsTagPrefix(SimpleToken token)
    {
        if (token.Content.Length != 1)
            return false;
        return _options.TagPrefixes.Contains(token.FirstChar);
    }
    
    private static NodeKind GetLeafKind(SimpleTokenType type)
    {
        return type switch
        {
            SimpleTokenType.Ident => NodeKind.Ident,
            SimpleTokenType.Digits => NodeKind.Numeric,
            SimpleTokenType.Symbol => NodeKind.Symbol,
            _ => NodeKind.Symbol
        };
    }
    
    #endregion
    
    #region TokenReader (ref struct for efficient iteration)
    
    private ref struct TokenReader
    {
        private readonly ImmutableArray<SimpleToken> _tokens;
        private int _position;
        
        public TokenReader(ImmutableArray<SimpleToken> tokens)
        {
            _tokens = tokens;
            _position = 0;
        }
        
        public bool HasMore => _position < _tokens.Length;
        
        public SimpleToken Current => _tokens[_position];
        
        /// <summary>Gets current token's text as a string.</summary>
        public string CurrentText => Current.Content.ToString();
        
        public void Advance() => _position++;
        
        public bool TryPeek(out SimpleToken token)
        {
            if (_position < _tokens.Length)
            {
                token = _tokens[_position];
                return true;
            }
            token = default;
            return false;
        }
        
        public bool TryPeek(int offset, out SimpleToken token)
        {
            var index = _position + offset;
            if (index >= 0 && index < _tokens.Length)
            {
                token = _tokens[index];
                return true;
            }
            token = default;
            return false;
        }
        
        /// <summary>Gets token text at offset as string.</summary>
        public string GetTextAt(int offset)
        {
            var index = _position + offset;
            return _tokens[index].Content.ToString();
        }
    }
    
    #endregion
}
