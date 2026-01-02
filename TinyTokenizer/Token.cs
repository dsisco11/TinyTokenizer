using System.Collections.Immutable;
using System.Text;

namespace TinyTokenizer;

#region Base Token

/// <summary>
/// Abstract base record for all token types.
/// Tokens are immutable and reference content via <see cref="ReadOnlyMemory{T}"/> to avoid copying.
/// Properties use defaults to support parameterless construction via new().
/// </summary>
public abstract record Token : ITextSerializable
{
    /// <summary>
    /// Gets or sets the token content as memory.
    /// </summary>
    public ReadOnlyMemory<char> Content { get; init; }

    /// <summary>
    /// Gets the token type.
    /// </summary>
    public abstract TokenType Type { get; }

    /// <summary>
    /// Gets or sets the absolute position in the source where this token starts.
    /// </summary>
    public long Position { get; init; }

    /// <summary>
    /// Gets the content as a <see cref="ReadOnlySpan{T}"/> for efficient processing.
    /// </summary>
    public ReadOnlySpan<char> ContentSpan => Content.Span;

    /// <inheritdoc />
    public virtual void WriteTo(StringBuilder builder) => builder.Append(Content.Span);

    /// <inheritdoc />
    public virtual string ToText() => new(Content.Span);

    /// <summary>
    /// Returns a debug representation of this token.
    /// Use <see cref="ToText"/> to get the serialized text content.
    /// </summary>
    public override string ToString() => $"{Type}@{Position}";
}

#endregion

#region Concrete Token Types

/// <summary>
/// Represents identifier/text content that is not a block, symbol, or whitespace.
/// </summary>
public sealed record IdentToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Ident;
}

/// <summary>
/// Represents whitespace characters (spaces, tabs, newlines).
/// </summary>
public sealed record WhitespaceToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Whitespace;
}

/// <summary>
/// Represents a symbol character such as /, :, ,, ;, etc.
/// Symbols are single characters not matched by configured operators.
/// </summary>
public sealed record SymbolToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Symbol;

    /// <summary>
    /// Gets the symbol character.
    /// </summary>
    public char Symbol => Content.Span[0];
}

/// <summary>
/// Represents an operator (single or multi-character) such as ==, !=, &amp;&amp;, ||, etc.
/// Which character sequences are recognized as operators is configured via <see cref="TokenizerOptions.Operators"/>.
/// </summary>
public sealed record OperatorToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Operator;

    /// <summary>
    /// Gets the operator as a string.
    /// </summary>
    public string Operator => Content.Span.ToString();
}

/// <summary>
/// Represents a tagged identifier - a prefix character followed by an identifier.
/// Examples: #define, @attribute, $variable
/// </summary>
public sealed record TaggedIdentToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.TaggedIdent;

    /// <summary>
    /// Gets the prefix tag character (e.g., '#', '@', '$').
    /// </summary>
    public required char Tag { get; init; }

    /// <summary>
    /// Gets the identifier name (e.g., "define", "attribute", "variable").
    /// </summary>
    public required ReadOnlyMemory<char> Name { get; init; }

    /// <summary>
    /// Gets the identifier name as a span.
    /// </summary>
    public ReadOnlySpan<char> NameSpan => Name.Span;
}

/// <summary>
/// Represents a numeric literal (integer or floating-point).
/// </summary>
public sealed record NumericToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Numeric;

    /// <summary>
    /// Gets the type of numeric literal.
    /// </summary>
    public required NumericType NumericType { get; init; }
}

/// <summary>
/// Represents a string literal delimited by single or double quotes.
/// </summary>
public sealed record StringToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.String;

    /// <summary>
    /// Gets the quote character used.
    /// </summary>
    public required char Quote { get; init; }

    /// <summary>
    /// Gets the string value without the surrounding quotes.
    /// </summary>
    public ReadOnlySpan<char> Value => Content.Span.Length >= 2 
        ? Content.Span[1..^1] 
        : ReadOnlySpan<char>.Empty;
}

/// <summary>
/// Represents a comment token.
/// </summary>
public sealed record CommentToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Comment;

    /// <summary>
    /// Gets whether this is a multi-line comment.
    /// </summary>
    public required bool IsMultiLine { get; init; }
}

#endregion

#region Composite Tokens (Level 3)

/// <summary>
/// Base record for composite tokens created by pattern matching.
/// Composite tokens wrap matched token sequences from Level 2 output.
/// Properties use defaults to support parameterless construction via new().
/// </summary>
public abstract record CompositeToken : Token
{
    /// <inheritdoc/>
    public override TokenType Type => TokenType.Composite;

    /// <summary>
    /// Gets or sets the tokens that were matched to create this composite token.
    /// Used by derived types to compute their specific properties.
    /// </summary>
    public ImmutableArray<Token> MatchedTokens { get; init; } = [];

    /// <summary>
    /// Gets the child tokens that were matched to create this composite token.
    /// Alias for <see cref="MatchedTokens"/> for backwards compatibility.
    /// </summary>
    public ImmutableArray<Token> Children => MatchedTokens;

    /// <summary>
    /// Gets or sets the name of the pattern that matched to create this token.
    /// </summary>
    public string PatternName { get; init; } = string.Empty;
}

/// <summary>
/// Represents a function call: identifier followed by parenthesized arguments.
/// Example: func(a, b)
/// </summary>
public sealed record FunctionCallToken : CompositeToken
{
    /// <summary>
    /// Gets the function name, computed from the first matched token.
    /// </summary>
    public ReadOnlyMemory<char> FunctionName => MatchedTokens.Length > 0 
        ? MatchedTokens[0].Content 
        : ReadOnlyMemory<char>.Empty;

    /// <summary>
    /// Gets the function name as a span.
    /// </summary>
    public ReadOnlySpan<char> FunctionNameSpan => FunctionName.Span;
}

/// <summary>
/// Represents a property or member access: target.member
/// Example: obj.property
/// </summary>
public sealed record PropertyAccessToken : CompositeToken
{
    /// <summary>
    /// Gets the target (left side of the dot), computed from matched tokens.
    /// </summary>
    public ReadOnlyMemory<char> Target => GetIdentAt(0);

    /// <summary>
    /// Gets the member name (right side of the dot), computed from matched tokens.
    /// </summary>
    public ReadOnlyMemory<char> Member => GetIdentAt(1);

    /// <summary>
    /// Gets the target as a span.
    /// </summary>
    public ReadOnlySpan<char> TargetSpan => Target.Span;

    /// <summary>
    /// Gets the member as a span.
    /// </summary>
    public ReadOnlySpan<char> MemberSpan => Member.Span;

    private ReadOnlyMemory<char> GetIdentAt(int index)
    {
        int count = 0;
        foreach (var token in MatchedTokens)
        {
            if (token is IdentToken)
            {
                if (count == index)
                    return token.Content;
                count++;
            }
        }
        return ReadOnlyMemory<char>.Empty;
    }
}

/// <summary>
/// Represents a type annotation: name: type
/// Example: param: string
/// </summary>
public sealed record TypeAnnotationToken : CompositeToken
{
    /// <summary>
    /// Gets the annotated name, computed from matched tokens.
    /// </summary>
    public ReadOnlyMemory<char> Name => GetIdentAt(0);

    /// <summary>
    /// Gets the type name, computed from matched tokens.
    /// </summary>
    public ReadOnlyMemory<char> TypeName => GetIdentAt(1);

    /// <summary>
    /// Gets the name as a span.
    /// </summary>
    public ReadOnlySpan<char> NameSpan => Name.Span;

    /// <summary>
    /// Gets the type name as a span.
    /// </summary>
    public ReadOnlySpan<char> TypeNameSpan => TypeName.Span;

    private ReadOnlyMemory<char> GetIdentAt(int index)
    {
        int count = 0;
        foreach (var token in MatchedTokens)
        {
            if (token is IdentToken)
            {
                if (count == index)
                    return token.Content;
                count++;
            }
        }
        return ReadOnlyMemory<char>.Empty;
    }
}

/// <summary>
/// Represents an assignment: target = value
/// Example: x = 5
/// </summary>
public sealed record AssignmentToken : CompositeToken
{
    /// <summary>
    /// Gets the assignment target (left side), computed from matched tokens.
    /// </summary>
    public ReadOnlyMemory<char> Target => MatchedTokens.Length > 0 
        ? MatchedTokens[0].Content 
        : ReadOnlyMemory<char>.Empty;

    /// <summary>
    /// Gets the assigned value tokens (right side), computed from matched tokens.
    /// Skips the target identifier and the '=' operator.
    /// </summary>
    public ImmutableArray<Token> ValueTokens => MatchedTokens.Length > 2 
        ? MatchedTokens.Skip(2).ToImmutableArray() 
        : ImmutableArray<Token>.Empty;

    /// <summary>
    /// Gets the target as a span.
    /// </summary>
    public ReadOnlySpan<char> TargetSpan => Target.Span;
}

/// <summary>
/// Represents an array or indexer access: target[index]
/// Example: arr[0]
/// </summary>
public sealed record ArrayAccessToken : CompositeToken
{
    /// <summary>
    /// Gets the target being indexed, computed from matched tokens.
    /// </summary>
    public ReadOnlyMemory<char> Target => MatchedTokens.Length > 0 
        ? MatchedTokens[0].Content 
        : ReadOnlyMemory<char>.Empty;

    /// <summary>
    /// Gets the index block token, computed from matched tokens.
    /// </summary>
    public SimpleBlock? IndexBlock => MatchedTokens.OfType<SimpleBlock>().FirstOrDefault();

    /// <summary>
    /// Gets the target as a span.
    /// </summary>
    public ReadOnlySpan<char> TargetSpan => Target.Span;
}

#endregion
