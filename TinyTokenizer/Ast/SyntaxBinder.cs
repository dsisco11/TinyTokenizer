using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Binds a structural green tree to a syntax green tree by recognizing schema-defined patterns
/// and replacing matched subtrees with GreenSyntaxNode instances.
/// </summary>
/// <remarks>
/// The binder walks the tree in a single pass, attempting to match patterns at each node.
/// When a pattern matches, the matched children are wrapped in a GreenSyntaxNode.
/// Nested patterns are supported - a GreenSyntaxNode can contain other GreenSyntaxNodes.
/// </remarks>
public sealed class SyntaxBinder
{
    private readonly ImmutableArray<SyntaxNodeDefinition> _sortedDefinitions;
    
    /// <summary>
    /// Creates a syntax binder for the specified definitions.
    /// </summary>
    public SyntaxBinder(ImmutableArray<SyntaxNodeDefinition> definitions)
    {
        // Sort by priority (highest first)
        _sortedDefinitions = definitions
            .OrderByDescending(d => d.Priority)
            .ToImmutableArray();
    }
    
    /// <summary>
    /// Creates a syntax binder from a schema's syntax definitions.
    /// </summary>
    public SyntaxBinder(Schema schema) : this(schema.GetSyntaxDefinitions())
    {
    }
    
    /// <summary>
    /// Binds a structural green tree, producing a syntax green tree.
    /// </summary>
    /// <param name="root">The structural green root node.</param>
    /// <returns>A new green tree with GreenSyntaxNodes where patterns matched.</returns>
    public GreenNode Bind(GreenNode root)
    {
        return BindNode(root);
    }
    
    /// <summary>
    /// Binds a single node and its descendants.
    /// </summary>
    private GreenNode BindNode(GreenNode node)
    {
        // First, recursively bind children
        var boundChildren = BindChildren(node);
        
        // If this node has children, try to match patterns within its children
        if (boundChildren.Length > 0)
        {
            var processedChildren = ProcessChildSequence(boundChildren);
            
            // Rebuild node with processed children if any changed
            if (!ChildrenEqual(boundChildren, processedChildren))
            {
                return RebuildNode(node, processedChildren);
            }
        }
        
        return node;
    }
    
    /// <summary>
    /// Recursively binds all children of a node.
    /// </summary>
    private ImmutableArray<GreenNode> BindChildren(GreenNode node)
    {
        if (node.SlotCount == 0)
            return ImmutableArray<GreenNode>.Empty;
        
        var builder = ImmutableArray.CreateBuilder<GreenNode>(node.SlotCount);
        bool anyChanged = false;
        
        for (int i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child != null)
            {
                var bound = BindNode(child);
                builder.Add(bound);
                if (!ReferenceEquals(bound, child))
                    anyChanged = true;
            }
        }
        
        return anyChanged ? builder.ToImmutable() : GetOriginalChildren(node);
    }
    
    /// <summary>
    /// Gets the original children of a node as an immutable array.
    /// </summary>
    private static ImmutableArray<GreenNode> GetOriginalChildren(GreenNode node)
    {
        var builder = ImmutableArray.CreateBuilder<GreenNode>(node.SlotCount);
        for (int i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child != null)
                builder.Add(child);
        }
        return builder.ToImmutable();
    }
    
    /// <summary>
    /// Processes a sequence of children, looking for pattern matches.
    /// When a pattern matches, the matched children are replaced with a GreenSyntaxNode.
    /// </summary>
    private ImmutableArray<GreenNode> ProcessChildSequence(ImmutableArray<GreenNode> children)
    {
        if (_sortedDefinitions.IsEmpty)
            return children;
        
        var result = ImmutableArray.CreateBuilder<GreenNode>();
        int i = 0;
        
        while (i < children.Length)
        {
            // Try to match patterns starting at this position
            var matchResult = TryMatchAtPosition(children, i);
            
            if (matchResult.HasValue)
            {
                // Pattern matched! Create a GreenSyntaxNode wrapping the matched children
                var (definition, matchedCount, matchedChildren) = matchResult.Value;
                var syntaxNode = new GreenSyntaxNode(
                    definition.Kind,
                    definition.RedType,
                    matchedChildren);
                result.Add(syntaxNode);
                i += matchedCount;
            }
            else
            {
                // No match - keep the original child
                result.Add(children[i]);
                i++;
            }
        }
        
        return result.ToImmutable();
    }
    
    /// <summary>
    /// Attempts to match any pattern at the given position in the children sequence.
    /// Returns the first (highest priority) match found.
    /// </summary>
    private (SyntaxNodeDefinition Definition, int MatchedCount, ImmutableArray<GreenNode> Children)? 
        TryMatchAtPosition(ImmutableArray<GreenNode> children, int startIndex)
    {
        foreach (var definition in _sortedDefinitions)
        {
            var matchResult = TryMatchDefinition(definition, children, startIndex);
            if (matchResult.HasValue)
                return (definition, matchResult.Value.MatchedCount, matchResult.Value.Children);
        }
        return null;
    }
    
    /// <summary>
    /// Attempts to match a specific definition at the given position.
    /// </summary>
    private (int MatchedCount, ImmutableArray<GreenNode> Children)? 
        TryMatchDefinition(SyntaxNodeDefinition definition, ImmutableArray<GreenNode> children, int startIndex)
    {
        foreach (var pattern in definition.Patterns)
        {
            var matchResult = TryMatchPattern(pattern, children, startIndex);
            if (matchResult.HasValue)
                return matchResult;
        }
        return null;
    }
    
    /// <summary>
    /// Attempts to match a pattern starting at the given position.
    /// Returns the number of children consumed and the matched children.
    /// </summary>
    private (int MatchedCount, ImmutableArray<GreenNode> Children)? 
        TryMatchPattern(NodePattern pattern, ImmutableArray<GreenNode> children, int startIndex)
    {
        // Create a temporary red tree view for pattern matching
        // This is a simplification - in production we'd want a more efficient approach
        var greenList = new GreenList(children.Skip(startIndex).ToImmutableArray());
        var redList = (RedBlock)greenList.CreateRed(null, 0);
        
        // Get the children as red nodes for matching
        var redChildren = redList.Children.ToList();
        if (redChildren.Count == 0)
            return null;
        
        // Try to match starting at the first child
        if (pattern.TryMatch(redChildren[0], out var match))
        {
            // Collect the green nodes that were matched
            var matchedGreen = children.Skip(startIndex).Take(match.ConsumedCount).ToImmutableArray();
            return (match.ConsumedCount, matchedGreen);
        }
        
        return null;
    }
    
    /// <summary>
    /// Rebuilds a node with new children.
    /// </summary>
    private static GreenNode RebuildNode(GreenNode original, ImmutableArray<GreenNode> newChildren)
    {
        // Handle different node types
        return original switch
        {
            GreenBlock block => new GreenBlock(block.Opener, newChildren, block.LeadingTrivia, block.TrailingTrivia),
            GreenList => new GreenList(newChildren),
            GreenSyntaxNode syntax => new GreenSyntaxNode(syntax.Kind, syntax.RedType, newChildren),
            _ => original // Leaves don't have children to rebuild
        };
    }
    
    /// <summary>
    /// Checks if two child arrays are equal by reference.
    /// </summary>
    private static bool ChildrenEqual(ImmutableArray<GreenNode> a, ImmutableArray<GreenNode> b)
    {
        if (a.Length != b.Length)
            return false;
        
        for (int i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
                return false;
        }
        
        return true;
    }
}

/// <summary>
/// Definition for a syntax node type that can be bound from the green tree.
/// Similar to ISemanticNodeDefinition but focused on green tree transformation.
/// </summary>
public sealed record SyntaxNodeDefinition
{
    /// <summary>Unique name for this definition.</summary>
    public required string Name { get; init; }
    
    /// <summary>Pattern alternatives (tried in order until one matches).</summary>
    public required ImmutableArray<NodePattern> Patterns { get; init; }
    
    /// <summary>The concrete RedSyntaxNode subclass to instantiate.</summary>
    public required Type RedType { get; init; }
    
    /// <summary>Priority for disambiguation (higher = matched first).</summary>
    public int Priority { get; init; } = 0;
    
    /// <summary>The assigned NodeKind (set by schema during registration).</summary>
    public NodeKind Kind { get; init; } = NodeKind.Semantic;
    
    /// <summary>
    /// Creates a copy with the assigned kind.
    /// </summary>
    public SyntaxNodeDefinition WithKind(NodeKind kind) => this with { Kind = kind };
}

/// <summary>
/// Builder for SyntaxNodeDefinition using fluent API.
/// </summary>
public sealed class SyntaxNodeDefinitionBuilder<T> where T : RedSyntaxNode
{
    private readonly string _name;
    private readonly List<NodePattern> _patterns = [];
    private int _priority;
    
    public SyntaxNodeDefinitionBuilder(string name) => _name = name;
    
    /// <summary>
    /// Adds a pattern that this definition matches.
    /// </summary>
    public SyntaxNodeDefinitionBuilder<T> Match(NodePattern pattern)
    {
        _patterns.Add(pattern);
        return this;
    }
    
    /// <summary>
    /// Adds a pattern built from a sequence of queries.
    /// </summary>
    public SyntaxNodeDefinitionBuilder<T> Match(params INodeQuery[] sequence)
    {
        _patterns.Add(NodePattern.Sequence(sequence));
        return this;
    }
    
    /// <summary>
    /// Adds a pattern built using the pattern builder.
    /// </summary>
    public SyntaxNodeDefinitionBuilder<T> Match(Action<PatternBuilder> configure)
    {
        var builder = new PatternBuilder();
        configure(builder);
        _patterns.Add(builder.Build());
        return this;
    }
    
    /// <summary>
    /// Sets the matching priority (higher = matched first).
    /// </summary>
    public SyntaxNodeDefinitionBuilder<T> WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }
    
    /// <summary>
    /// Builds the syntax node definition.
    /// </summary>
    public SyntaxNodeDefinition Build() => new()
    {
        Name = _name,
        Patterns = [.. _patterns],
        RedType = typeof(T),
        Priority = _priority
    };
}

/// <summary>
/// Entry point for fluent syntax node definition building.
/// </summary>
public static class Syntax
{
    /// <summary>
    /// Starts building a syntax node definition.
    /// </summary>
    /// <typeparam name="T">The RedSyntaxNode subclass.</typeparam>
    /// <param name="name">Unique name for the definition.</param>
    public static SyntaxNodeDefinitionBuilder<T> Define<T>(string name) where T : RedSyntaxNode
        => new(name);
}
