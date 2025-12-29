using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TinyTokenizer.Ast;

/// <summary>
/// Optional context passed to semantic node factories during construction.
/// Provides dependency injection via service locator pattern.
/// </summary>
public class SemanticContext
{
    private readonly ConcurrentDictionary<Type, object> _services = new();
    
    /// <summary>
    /// The syntax tree being analyzed.
    /// </summary>
    public SyntaxTree? Tree { get; init; }
    
    /// <summary>
    /// Logger for reporting errors/warnings during semantic analysis.
    /// Defaults to NullLogger (no output).
    /// </summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;
    
    /// <summary>
    /// When true, semantic node factories should fail on unresolved references.
    /// </summary>
    public bool StrictMode { get; init; }
    
    /// <summary>
    /// Registers a service instance.
    /// </summary>
    public SemanticContext AddService<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
        return this;
    }
    
    /// <summary>
    /// Gets a registered service, or null if not found.
    /// </summary>
    public T? GetService<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
    }
    
    /// <summary>
    /// Gets a registered service, throwing if not found.
    /// </summary>
    public T GetRequiredService<T>() where T : class
    {
        return GetService<T>() ?? throw new InvalidOperationException(
            $"Service '{typeof(T).Name}' not registered in SemanticContext");
    }
    
    /// <summary>
    /// Checks if a service is registered.
    /// </summary>
    public bool HasService<T>() where T : class
    {
        return _services.ContainsKey(typeof(T));
    }
}
