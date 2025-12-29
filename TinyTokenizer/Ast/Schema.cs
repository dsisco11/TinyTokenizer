using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Immutable unified configuration combining tokenization settings and semantic node definitions.
/// Created via <see cref="SchemaBuilder"/> and frozen at construction time.
/// </summary>
public sealed class Schema
{
    #region Tokenization Settings
    
    /// <summary>Characters recognized as symbol tokens.</summary>
    public ImmutableHashSet<char> Symbols { get; }
    
    /// <summary>Comment styles to recognize.</summary>
    public ImmutableArray<CommentStyle> CommentStyles { get; }
    
    /// <summary>Multi-character operator strings.</summary>
    public ImmutableHashSet<string> Operators { get; }
    
    /// <summary>Tag prefix characters for tagged identifiers (#, @, $, etc.).</summary>
    public ImmutableHashSet<char> TagPrefixes { get; }
    
    #endregion
    
    #region Semantic Definitions
    
    /// <summary>All registered semantic node definitions.</summary>
    public ImmutableArray<ISemanticNodeDefinition> Definitions { get; }
    
    /// <summary>Definitions indexed by name.</summary>
    private readonly ImmutableDictionary<string, ISemanticNodeDefinition> _definitionsByName;
    
    /// <summary>Definitions indexed by NodeKind.</summary>
    private readonly ImmutableDictionary<NodeKind, ISemanticNodeDefinition> _definitionsByKind;
    
    /// <summary>Definitions indexed by .NET type.</summary>
    private readonly ImmutableDictionary<Type, ISemanticNodeDefinition> _definitionsByType;
    
    /// <summary>Definitions sorted by priority (descending) for matching.</summary>
    private readonly ImmutableArray<ISemanticNodeDefinition> _sortedDefinitions;
    
    #endregion
    
    #region Syntax Definitions
    
    /// <summary>All registered syntax node definitions for tree binding.</summary>
    private readonly ImmutableArray<SyntaxNodeDefinition> _syntaxDefinitions;
    
    /// <summary>Syntax definitions indexed by name.</summary>
    private readonly ImmutableDictionary<string, SyntaxNodeDefinition> _syntaxDefinitionsByName;
    
    /// <summary>Syntax definitions indexed by RedType.</summary>
    private readonly ImmutableDictionary<Type, SyntaxNodeDefinition> _syntaxDefinitionsByType;
    
    #endregion
    
    #region Constructor
    
    internal Schema(
        ImmutableHashSet<char> symbols,
        ImmutableArray<CommentStyle> commentStyles,
        ImmutableHashSet<string> operators,
        ImmutableHashSet<char> tagPrefixes,
        ImmutableArray<ISemanticNodeDefinition> definitions,
        ImmutableArray<SyntaxNodeDefinition> syntaxDefinitions = default)
    {
        Symbols = symbols;
        CommentStyles = commentStyles;
        Operators = operators;
        TagPrefixes = tagPrefixes;
        
        // Assign NodeKind values to semantic definitions
        var assignedDefinitions = ImmutableArray.CreateBuilder<ISemanticNodeDefinition>();
        var byName = ImmutableDictionary.CreateBuilder<string, ISemanticNodeDefinition>();
        var byKind = ImmutableDictionary.CreateBuilder<NodeKind, ISemanticNodeDefinition>();
        var byType = ImmutableDictionary.CreateBuilder<Type, ISemanticNodeDefinition>();
        
        int kindOffset = 0;
        foreach (var def in definitions)
        {
            var kind = NodeKindExtensions.SemanticKind(kindOffset++);
            var assigned = def.WithKind(kind);
            
            assignedDefinitions.Add(assigned);
            byName[assigned.Name] = assigned;
            byKind[kind] = assigned;
            byType[assigned.NodeType] = assigned;
        }
        
        Definitions = assignedDefinitions.ToImmutable();
        _definitionsByName = byName.ToImmutable();
        _definitionsByKind = byKind.ToImmutable();
        _definitionsByType = byType.ToImmutable();
        
        // Sort by priority descending for matching
        _sortedDefinitions = Definitions
            .OrderByDescending(d => d.Priority)
            .ToImmutableArray();
        
        // Process syntax definitions (assign NodeKind values)
        var syntaxDefs = syntaxDefinitions.IsDefault 
            ? ImmutableArray<SyntaxNodeDefinition>.Empty 
            : syntaxDefinitions;
        
        var assignedSyntaxDefs = ImmutableArray.CreateBuilder<SyntaxNodeDefinition>();
        var syntaxByName = ImmutableDictionary.CreateBuilder<string, SyntaxNodeDefinition>();
        var syntaxByType = ImmutableDictionary.CreateBuilder<Type, SyntaxNodeDefinition>();
        
        foreach (var def in syntaxDefs)
        {
            var kind = NodeKindExtensions.SemanticKind(kindOffset++);
            var assigned = def.WithKind(kind);
            
            assignedSyntaxDefs.Add(assigned);
            syntaxByName[assigned.Name] = assigned;
            syntaxByType[assigned.RedType] = assigned;
        }
        
        _syntaxDefinitions = assignedSyntaxDefs.ToImmutable();
        _syntaxDefinitionsByName = syntaxByName.ToImmutable();
        _syntaxDefinitionsByType = syntaxByType.ToImmutable();
    }
    
    #endregion
    
    #region Definition Lookup
    
    /// <summary>Gets the NodeKind for a definition by name.</summary>
    public NodeKind GetKind(string name) =>
        _definitionsByName.TryGetValue(name, out var def) ? def.Kind : NodeKind.Semantic;
    
    /// <summary>Gets a definition by name.</summary>
    public ISemanticNodeDefinition? GetDefinition(string name) =>
        _definitionsByName.GetValueOrDefault(name);
    
    /// <summary>Gets a definition by NodeKind.</summary>
    public ISemanticNodeDefinition? GetDefinition(NodeKind kind) =>
        _definitionsByKind.GetValueOrDefault(kind);
    
    /// <summary>Gets a definition by semantic node type.</summary>
    public ISemanticNodeDefinition? GetDefinition<T>() where T : SemanticNode =>
        _definitionsByType.GetValueOrDefault(typeof(T));
    
    /// <summary>Gets all definitions sorted by priority.</summary>
    internal ImmutableArray<ISemanticNodeDefinition> SortedDefinitions => _sortedDefinitions;
    
    /// <summary>
    /// Gets syntax node definitions for tree binding, sorted by priority descending.
    /// </summary>
    public ImmutableArray<SyntaxNodeDefinition> SyntaxDefinitions => _syntaxDefinitions;
    
    /// <summary>
    /// Gets a syntax definition by name.
    /// </summary>
    public SyntaxNodeDefinition? GetSyntaxDefinition(string name) =>
        _syntaxDefinitionsByName.GetValueOrDefault(name);
    
    /// <summary>
    /// Gets a syntax definition by RedSyntaxNode type.
    /// </summary>
    public SyntaxNodeDefinition? GetSyntaxDefinition<T>() where T : SyntaxNode =>
        _syntaxDefinitionsByType.GetValueOrDefault(typeof(T));
    
    /// <summary>
    /// Gets syntax node definitions for tree binding (internal for SyntaxBinder).
    /// </summary>
    internal ImmutableArray<SyntaxNodeDefinition> GetSyntaxDefinitions() => _syntaxDefinitions;
    
    /// <summary>
    /// Gets the NodeKind for a semantic node type.
    /// </summary>
    /// <typeparam name="T">The semantic node type.</typeparam>
    /// <returns>The NodeKind for the type, or <see cref="NodeKind.Semantic"/> if not registered.</returns>
    public NodeKind GetKind<T>() where T : SemanticNode =>
        _definitionsByType.TryGetValue(typeof(T), out var def) ? def.Kind : NodeKind.Semantic;
    
    /// <summary>
    /// Creates a query that matches semantic nodes of the specified type.
    /// Uses the type's registered NodeKind for optimized scanning.
    /// </summary>
    /// <typeparam name="T">The semantic node type to query for.</typeparam>
    /// <returns>A NodeQuery that matches nodes of the specified semantic type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not registered in this schema.</exception>
    public SemanticNodeQuery Semantic<T>() where T : SemanticNode
    {
        var definition = GetDefinition<T>();
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Semantic node type '{typeof(T).Name}' is not registered in this schema. " +
                $"Register it using SchemaBuilder.Define<{typeof(T).Name}>().");
        }
        return new SemanticNodeQuery(definition.Kind);
    }
    
    #endregion
    
    #region TokenizerOptions Conversion
    
    /// <summary>
    /// Converts tokenization settings to TokenizerOptions for internal use.
    /// </summary>
    internal TokenizerOptions ToTokenizerOptions() => new TokenizerOptions()
        .WithSymbols(Symbols.ToArray())
        .WithCommentStyles(CommentStyles.ToArray())
        .WithOperators(Operators)
        .WithTagPrefixes(TagPrefixes.ToArray());
    
    #endregion
    
    #region Factory Methods
    
    /// <summary>
    /// Creates a new schema builder.
    /// </summary>
    public static SchemaBuilder Create() => new();
    
    /// <summary>
    /// Creates a schema from existing TokenizerOptions (migration helper).
    /// </summary>
    public static Schema FromOptions(TokenizerOptions options) => Create()
        .WithSymbols(options.Symbols.ToArray())
        .WithCommentStyles(options.CommentStyles.ToArray())
        .WithOperators(options.Operators)
        .WithTagPrefixes(options.TagPrefixes.ToArray())
        .Build();
    
    /// <summary>
    /// Default schema with common tokenization settings and built-in semantic nodes.
    /// </summary>
    public static Schema Default { get; } = Create()
        .WithOperators(CommonOperators.Universal)
        .WithCommentStyles(CommentStyle.CStyleSingleLine, CommentStyle.CStyleMultiLine)
        .Define(BuiltInDefinitions.FunctionName)
        .Define(BuiltInDefinitions.ArrayAccess)
        .Define(BuiltInDefinitions.PropertyAccess)
        .Define(BuiltInDefinitions.MethodCall)
        .Build();
    
    #endregion
}

/// <summary>
/// Fluent builder for constructing immutable Schema instances.
/// </summary>
public sealed class SchemaBuilder
{
    private ImmutableHashSet<char> _symbols = ImmutableHashSet.Create(
        '/', ':', ',', ';', '=', '+', '-', '*', '<', '>', '!', '&', '|', '.', '@', '#', '?', '%', '^', '~', '\\');
    private ImmutableArray<CommentStyle> _commentStyles = ImmutableArray<CommentStyle>.Empty;
    private ImmutableHashSet<string> _operators = CommonOperators.Universal;
    private ImmutableHashSet<char> _tagPrefixes = ImmutableHashSet<char>.Empty;
    private readonly List<ISemanticNodeDefinition> _definitions = [];
    
    internal SchemaBuilder() { }
    
    #region Tokenization Configuration
    
    /// <summary>Sets the symbol characters.</summary>
    public SchemaBuilder WithSymbols(params char[] symbols)
    {
        _symbols = ImmutableHashSet.CreateRange(symbols);
        return this;
    }
    
    /// <summary>Sets the symbol characters.</summary>
    public SchemaBuilder WithSymbols(ImmutableHashSet<char> symbols)
    {
        _symbols = symbols;
        return this;
    }
    
    /// <summary>Adds additional symbol characters.</summary>
    public SchemaBuilder AddSymbols(params char[] symbols)
    {
        _symbols = _symbols.Union(symbols);
        return this;
    }
    
    /// <summary>Sets the comment styles.</summary>
    public SchemaBuilder WithCommentStyles(params CommentStyle[] styles)
    {
        _commentStyles = ImmutableArray.CreateRange(styles);
        return this;
    }
    
    /// <summary>Adds additional comment styles.</summary>
    public SchemaBuilder AddCommentStyles(params CommentStyle[] styles)
    {
        _commentStyles = _commentStyles.AddRange(styles);
        return this;
    }
    
    /// <summary>Sets the operators.</summary>
    public SchemaBuilder WithOperators(params string[] operators)
    {
        _operators = ImmutableHashSet.CreateRange(operators);
        return this;
    }
    
    /// <summary>Sets the operators from a predefined set.</summary>
    public SchemaBuilder WithOperators(ImmutableHashSet<string> operators)
    {
        _operators = operators;
        return this;
    }
    
    /// <summary>Adds additional operators.</summary>
    public SchemaBuilder AddOperators(params string[] operators)
    {
        _operators = _operators.Union(operators);
        return this;
    }
    
    /// <summary>Sets the tag prefix characters.</summary>
    public SchemaBuilder WithTagPrefixes(params char[] prefixes)
    {
        _tagPrefixes = ImmutableHashSet.CreateRange(prefixes);
        return this;
    }
    
    /// <summary>Adds additional tag prefix characters.</summary>
    public SchemaBuilder AddTagPrefixes(params char[] prefixes)
    {
        _tagPrefixes = _tagPrefixes.Union(prefixes);
        return this;
    }
    
    #endregion
    
    #region Semantic Definitions
    
    /// <summary>Registers a semantic node definition.</summary>
    public SchemaBuilder Define<T>(SemanticNodeDefinition<T> definition) where T : SemanticNode
    {
        _definitions.Add(definition);
        return this;
    }
    
    /// <summary>Registers a semantic node definition (non-generic).</summary>
    public SchemaBuilder Define(ISemanticNodeDefinition definition)
    {
        _definitions.Add(definition);
        return this;
    }
    
    #endregion
    
    #region Syntax Definitions
    
    private readonly List<SyntaxNodeDefinition> _syntaxDefinitions = [];
    
    /// <summary>Registers a syntax node definition for tree binding.</summary>
    public SchemaBuilder DefineSyntax<T>(SyntaxNodeDefinition definition) where T : SyntaxNode
    {
        if (definition.RedType != typeof(T))
            throw new ArgumentException($"Definition RedType must be {typeof(T).Name}", nameof(definition));
        _syntaxDefinitions.Add(definition);
        return this;
    }
    
    /// <summary>Registers a syntax node definition for tree binding.</summary>
    public SchemaBuilder DefineSyntax(SyntaxNodeDefinition definition)
    {
        _syntaxDefinitions.Add(definition);
        return this;
    }
    
    /// <summary>Registers a syntax node definition using a builder action.</summary>
    public SchemaBuilder DefineSyntax<T>(string name, Action<SyntaxNodeDefinitionBuilder<T>> configure) 
        where T : SyntaxNode
    {
        var builder = new SyntaxNodeDefinitionBuilder<T>(name);
        configure(builder);
        _syntaxDefinitions.Add(builder.Build());
        return this;
    }
    
    #endregion
    
    #region Build
    
    /// <summary>
    /// Builds the immutable schema.
    /// </summary>
    public Schema Build() => new(
        _symbols,
        _commentStyles,
        _operators,
        _tagPrefixes,
        [.. _definitions],
        [.. _syntaxDefinitions]);
    
    #endregion
}

/// <summary>
/// Built-in semantic node definitions.
/// </summary>
public static class BuiltInDefinitions
{
    /// <summary>
    /// Function name: Ident followed by ParenBlock (captures only the ident).
    /// Uses lookahead to verify paren block follows without consuming it.
    /// </summary>
    public static SemanticNodeDefinition<FunctionNameNode> FunctionName { get; } =
        Semantic.Define<FunctionNameNode>("FunctionName")
            .Match(new LookaheadPattern(
                new QueryPattern(Query.Ident),
                new QueryPattern(Query.ParenBlock)))
            .Create((match, kind) => new FunctionNameNode(match, kind))
            .Build();
    
    /// <summary>
    /// Array access: Ident + BracketBlock
    /// </summary>
    public static SemanticNodeDefinition<ArrayAccessNode> ArrayAccess { get; } =
        Semantic.Define<ArrayAccessNode>("ArrayAccess")
            .Match(p => p.Ident().BracketBlock())
            .Create((match, kind) => new ArrayAccessNode(match, kind))
            .Build();
    
    /// <summary>
    /// Property access: Ident + Symbol(".") + Ident
    /// </summary>
    public static SemanticNodeDefinition<PropertyAccessNode> PropertyAccess { get; } =
        Semantic.Define<PropertyAccessNode>("PropertyAccess")
            .Match(p => p.Ident().Symbol(".").Ident())
            .Create((match, kind) => new PropertyAccessNode(match, kind))
            .WithPriority(5) // Lower priority than method call
            .Build();
    
    /// <summary>
    /// Method call: Ident + Symbol(".") + Ident + ParenBlock
    /// </summary>
    public static SemanticNodeDefinition<MethodCallNode> MethodCall { get; } =
        Semantic.Define<MethodCallNode>("MethodCall")
            .Match(p => p.Ident().Symbol(".").Ident().ParenBlock())
            .Create((match, kind) => new MethodCallNode(match, kind))
            .WithPriority(10) // Higher priority - longer match
            .Build();
}
