using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Lexer that produces GreenNodes directly from source text.
/// Optimized path that skips intermediate Token allocations.
/// Handles trivia attachment and recursive block parsing.
/// </summary>
public sealed class GreenLexer
{
    private readonly TokenizerOptions _options;
    private readonly Lexer _charLexer;
    private readonly ImmutableArray<string> _sortedOperators;
    
    /// <summary>
    /// Creates a new GreenLexer with default options.
    /// </summary>
    public GreenLexer() : this(TokenizerOptions.Default)
    {
    }
    
    /// <summary>
    /// Creates a new GreenLexer with the specified options.
    /// </summary>
    public GreenLexer(TokenizerOptions options)
    {
        _options = options;
        _charLexer = new Lexer(options);
        _sortedOperators = options.Operators
            .OrderByDescending(op => op.Length)
            .ToImmutableArray();
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
        
        while (reader.HasMore)
        {
            // Collect leading trivia for next token
            var leadingTrivia = CollectTrivia(ref reader);
            
            if (!reader.HasMore)
            {
                // End-of-file trivia - attach as trailing to last node (fallback)
                if (!leadingTrivia.IsEmpty && builder.Count > 0)
                {
                    var last = builder[^1];
                    builder[^1] = AttachTrailingTrivia(last, leadingTrivia);
                }
                break;
            }
            
            // Parse the main node with leading trivia
            var node = ParseNode(ref reader, null, leadingTrivia);
            if (node != null)
                builder.Add(node);
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
        if (IsClosingDelimiter(token.Type) && token.Type != expectedCloser)
        {
            reader.Advance();
            // Leading-only trivia model: no trailing trivia
            return new GreenLeaf(NodeKind.Error, token.Content.ToString(), leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
        }
        
        // Opening delimiter - parse block
        if (IsOpeningDelimiter(token.Type))
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
        
        // Identifier
        if (token.Type == SimpleTokenType.Ident)
        {
            var text = reader.CurrentText;
            reader.Advance();
            // Leading-only trivia model: no trailing trivia
            return GreenNodeCache.Create(NodeKind.Ident, text, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
        }
        
        // Symbol (single character)
        var symbolText = reader.CurrentText;
        reader.Advance();
        // Leading-only trivia model: no trailing trivia
        var kind = GetLeafKind(token.Type);
        return GreenNodeCache.Create(kind, symbolText, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
    }
    
    private GreenBlock ParseBlock(ref TokenReader reader, ImmutableArray<GreenTrivia> leadingTrivia)
    {
        var openToken = reader.Current;
        reader.Advance();
        
        var opener = openToken.FirstChar;
        var closerType = GetMatchingCloser(openToken.Type);
        
        var children = ImmutableArray.CreateBuilder<GreenNode>();
        
        while (reader.HasMore)
        {
            var token = reader.Current;
            
            if (token.Type == closerType)
            {
                // Found closer - no trivia before it
                reader.Advance();
                return new GreenBlock(opener, children.ToImmutable(), leadingTrivia);
            }
            
            // Collect leading trivia for child
            var childLeading = CollectTrivia(ref reader);
            
            if (!reader.HasMore || reader.Current.Type == closerType)
            {
                // Trivia before closer - attach as trailing to last child (fallback)
                if (!childLeading.IsEmpty && children.Count > 0)
                {
                    children[^1] = AttachTrailingTrivia(children[^1], childLeading);
                }
                
                if (reader.HasMore && reader.Current.Type == closerType)
                {
                    reader.Advance();
                    return new GreenBlock(opener, children.ToImmutable(), leadingTrivia);
                }
                break;
            }
            
            // Parse child node
            var child = ParseNode(ref reader, closerType, childLeading);
            if (child != null)
                children.Add(child);
        }
        
        // Unclosed block - return as error
        return new GreenBlock(opener, children.ToImmutable(), leadingTrivia);
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
        if (_sortedOperators.IsEmpty)
            return null;
        
        // Build sequence of operator-capable characters
        var chars = new List<char>();
        int offset = 0;
        
        while (reader.TryPeek(offset, out var token))
        {
            var c = GetOperatorChar(token.Type);
            if (c == null)
            {
                if (token.Type == SimpleTokenType.Symbol && token.Content.Length == 1)
                    c = token.FirstChar;
                else
                    break;
            }
            chars.Add(c.Value);
            offset++;
            
            if (_sortedOperators.Length > 0 && chars.Count > _sortedOperators[0].Length)
                break;
        }
        
        if (chars.Count == 0)
            return null;
        
        var sequence = new string(chars.ToArray());
        
        // Match longest operator
        foreach (var op in _sortedOperators)
        {
            if (sequence.StartsWith(op, StringComparison.Ordinal))
            {
                // Consume tokens for this operator
                for (int i = 0; i < op.Length; i++)
                    reader.Advance();
                
                // Leading-only trivia model: no trailing trivia
                return GreenNodeCache.Create(NodeKind.Operator, op, leadingTrivia, ImmutableArray<GreenTrivia>.Empty);
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region Trivia Collection
    
    private ImmutableArray<GreenTrivia> CollectTrivia(ref TokenReader reader)
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
            
            GreenBlock block when block.SlotCount > 0 => 
                // Attach to last child of block
                block.WithSlot(block.SlotCount - 1, 
                    AttachTrailingTrivia(block.GetSlot(block.SlotCount - 1)!, trivia)),
            
            GreenBlock block => 
                // Empty block - attach as block's trailing trivia
                new GreenBlock(block.Opener, ImmutableArray<GreenNode>.Empty, 
                    block.LeadingTrivia, block.TrailingTrivia.AddRange(trivia)),
            
            GreenList list when list.SlotCount > 0 => 
                list.WithSlot(list.SlotCount - 1, 
                    AttachTrailingTrivia(list.GetSlot(list.SlotCount - 1)!, trivia)),
            
            GreenSyntaxNode syntax when syntax.SlotCount > 0 =>
                new GreenSyntaxNode(syntax.Kind, syntax.RedType, 
                    syntax.Children.SetItem(syntax.Children.Length - 1, 
                        AttachTrailingTrivia(syntax.GetSlot(syntax.Children.Length - 1)!, trivia))),
            
            _ => node // Unknown node type or empty container, return unchanged
        };
    }
    
    // Note: CollectTrailingTrivia removed - using leading-preferred trivia model
    
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
    
    private static bool IsOpeningDelimiter(SimpleTokenType type)
    {
        return type is SimpleTokenType.OpenBrace or SimpleTokenType.OpenBracket or SimpleTokenType.OpenParen;
    }
    
    private static bool IsClosingDelimiter(SimpleTokenType type)
    {
        return type is SimpleTokenType.CloseBrace or SimpleTokenType.CloseBracket or SimpleTokenType.CloseParen;
    }
    
    private static SimpleTokenType GetMatchingCloser(SimpleTokenType opener)
    {
        return opener switch
        {
            SimpleTokenType.OpenBrace => SimpleTokenType.CloseBrace,
            SimpleTokenType.OpenBracket => SimpleTokenType.CloseBracket,
            SimpleTokenType.OpenParen => SimpleTokenType.CloseParen,
            _ => throw new ArgumentException($"Not an opener: {opener}")
        };
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
    
    private static char? GetOperatorChar(SimpleTokenType type)
    {
        return type switch
        {
            SimpleTokenType.Equals => '=',
            SimpleTokenType.Plus => '+',
            SimpleTokenType.Minus => '-',
            SimpleTokenType.LessThan => '<',
            SimpleTokenType.GreaterThan => '>',
            SimpleTokenType.Pipe => '|',
            SimpleTokenType.Ampersand => '&',
            SimpleTokenType.Percent => '%',
            SimpleTokenType.Caret => '^',
            SimpleTokenType.Tilde => '~',
            SimpleTokenType.Question => '?',
            SimpleTokenType.Exclamation => '!',
            SimpleTokenType.Colon => ':',
            SimpleTokenType.Slash => '/',
            SimpleTokenType.Asterisk => '*',
            SimpleTokenType.Dot => '.',
            _ => null
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
