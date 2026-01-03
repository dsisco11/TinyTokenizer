namespace TinyTokenizer;

/// <summary>
/// A trie data structure optimized for greedy operator matching.
/// Provides O(k) lookup where k is the length of the matched operator.
/// </summary>
internal sealed class OperatorTrie
{
    private readonly TrieNode _root = new();

    /// <summary>
    /// Gets whether the trie contains any operators.
    /// </summary>
    public bool IsEmpty => _root.Children is null;

    /// <summary>
    /// Adds an operator to the trie.
    /// </summary>
    /// <param name="op">The operator string to add.</param>
    public void Add(string op)
    {
        if (string.IsNullOrEmpty(op))
            return;

        var node = _root;
        foreach (var c in op)
        {
            node.Children ??= new Dictionary<char, TrieNode>();
            if (!node.Children.TryGetValue(c, out var child))
            {
                child = new TrieNode();
                node.Children[c] = child;
            }
            node = child;
        }
        node.Operator = op;
    }

    /// <summary>
    /// Tries to match the longest operator from the input sequence.
    /// Uses greedy matching - returns the longest possible match.
    /// </summary>
    /// <param name="input">The input character sequence to match against.</param>
    /// <param name="matched">The matched operator string, or null if no match.</param>
    /// <returns>True if an operator was matched; otherwise, false.</returns>
    public bool TryMatch(ReadOnlySpan<char> input, out string? matched)
    {
        matched = null;
        
        if (_root.Children is null || input.IsEmpty)
            return false;

        var node = _root;
        string? longestMatch = null;

        foreach (var c in input)
        {
            if (node.Children is null || !node.Children.TryGetValue(c, out var child))
                break;

            node = child;
            
            // Track the longest match found so far (greedy)
            if (node.Operator is not null)
            {
                longestMatch = node.Operator;
            }
        }

        matched = longestMatch;
        return longestMatch is not null;
    }

    /// <summary>
    /// A node in the operator trie.
    /// </summary>
    private sealed class TrieNode
    {
        /// <summary>
        /// Child nodes keyed by character. Null if this is a leaf node.
        /// </summary>
        public Dictionary<char, TrieNode>? Children { get; set; }

        /// <summary>
        /// The operator string if this node marks the end of an operator; otherwise, null.
        /// </summary>
        public string? Operator { get; set; }
    }
}
