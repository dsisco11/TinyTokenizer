using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Non-generic interface for semantic node definitions.
/// Enables heterogeneous collections of definitions.
/// </summary>
public interface ISemanticNodeDefinition
{
    /// <summary>Unique name for this definition (used in queries and diagnostics).</summary>
    string Name { get; }
    
    /// <summary>Pattern alternatives to try matching (in order).</summary>
    ImmutableArray<NodePattern> Patterns { get; }
    
    /// <summary>Priority for disambiguation (higher = matched first).</summary>
    int Priority { get; }
    
    /// <summary>The .NET type of the semantic node.</summary>
    Type NodeType { get; }
    
    /// <summary>The assigned NodeKind (set by schema during registration).</summary>
    NodeKind Kind { get; }
    
    /// <summary>
    /// Attempts to create a semantic node from a match.
    /// </summary>
    /// <param name="match">The pattern match result.</param>
    /// <param name="context">Optional semantic context for dependencies.</param>
    /// <returns>The semantic node, or null if creation failed/filtered.</returns>
    SemanticNode? TryCreate(NodeMatch match, SemanticContext? context);
    
    /// <summary>
    /// Creates a copy of this definition with the assigned kind.
    /// Called by schema during registration.
    /// </summary>
    ISemanticNodeDefinition WithKind(NodeKind kind);
}

/// <summary>
/// Complete definition for a semantic node type.
/// Combines patterns, factory, and metadata into a single immutable configuration.
/// </summary>
/// <typeparam name="T">The semantic node type this definition produces.</typeparam>
public sealed record SemanticNodeDefinition<T> : ISemanticNodeDefinition where T : SemanticNode
{
    /// <summary>Unique name for this definition.</summary>
    public required string Name { get; init; }
    
    /// <summary>Pattern alternatives (tried in order until one matches).</summary>
    public required ImmutableArray<NodePattern> Patterns { get; init; }
    
    /// <summary>Factory delegate to construct the semantic node.</summary>
    public required Func<NodeMatch, SemanticContext?, NodeKind, T?> Create { get; init; }
    
    /// <summary>Priority for disambiguation (higher = matched first).</summary>
    public int Priority { get; init; } = 0;
    
    /// <summary>The assigned NodeKind (set by schema).</summary>
    public NodeKind Kind { get; init; } = NodeKind.Semantic;
    
    Type ISemanticNodeDefinition.NodeType => typeof(T);
    
    SemanticNode? ISemanticNodeDefinition.TryCreate(NodeMatch match, SemanticContext? context)
    {
        return Create(match, context, Kind);
    }
    
    ISemanticNodeDefinition ISemanticNodeDefinition.WithKind(NodeKind kind)
    {
        return this with { Kind = kind };
    }
}

/// <summary>
/// Entry point for fluent semantic node definition building.
/// </summary>
public static class Semantic
{
    /// <summary>
    /// Starts building a semantic node definition.
    /// </summary>
    /// <typeparam name="T">The semantic node type.</typeparam>
    /// <param name="name">Unique name for the definition.</param>
    public static SemanticNodeBuilder<T> Define<T>(string name) where T : SemanticNode
        => new(name);
}

/// <summary>
/// Fluent builder for constructing semantic node definitions.
/// </summary>
/// <typeparam name="T">The semantic node type being defined.</typeparam>
public sealed class SemanticNodeBuilder<T> where T : SemanticNode
{
    private readonly string _name;
    private readonly List<NodePattern> _patterns = [];
    private Func<NodeMatch, SemanticContext?, NodeKind, T?>? _factory;
    private int _priority;
    
    internal SemanticNodeBuilder(string name) => _name = name;
    
    /// <summary>
    /// Adds a pattern alternative using a pre-built pattern.
    /// </summary>
    public SemanticNodeBuilder<T> Match(NodePattern pattern)
    {
        _patterns.Add(pattern);
        return this;
    }
    
    /// <summary>
    /// Adds a pattern alternative using the fluent pattern builder.
    /// </summary>
    public SemanticNodeBuilder<T> Match(Action<PatternBuilder> configure)
    {
        var builder = new PatternBuilder();
        configure(builder);
        _patterns.Add(builder.Build());
        return this;
    }
    
    /// <summary>
    /// Sets the factory delegate (simple form, no context).
    /// </summary>
    public SemanticNodeBuilder<T> Create(Func<NodeMatch, NodeKind, T> factory)
    {
        _factory = (match, _, kind) => factory(match, kind);
        return this;
    }
    
    /// <summary>
    /// Sets the factory delegate (with optional context).
    /// </summary>
    public SemanticNodeBuilder<T> Create(Func<NodeMatch, SemanticContext?, NodeKind, T?> factory)
    {
        _factory = factory;
        return this;
    }
    
    /// <summary>
    /// Sets the disambiguation priority (higher = matched first).
    /// </summary>
    public SemanticNodeBuilder<T> WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }
    
    /// <summary>
    /// Builds the immutable definition.
    /// </summary>
    public SemanticNodeDefinition<T> Build()
    {
        if (_patterns.Count == 0)
            throw new InvalidOperationException($"No patterns defined for '{_name}'");
        if (_factory == null)
            throw new InvalidOperationException($"No factory defined for '{_name}'");
        
        return new SemanticNodeDefinition<T>
        {
            Name = _name,
            Patterns = [.. _patterns],
            Create = _factory,
            Priority = _priority
        };
    }
}
