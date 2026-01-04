using System.Collections.Immutable;

namespace TinyTokenizer.Ast;

/// <summary>
/// Defines a category of well-known words (keywords) that receive unique <see cref="NodeKind"/> values.
/// Each keyword in the category gets its own NodeKind, enabling semantic queries and language-specific tokenization.
/// </summary>
/// <param name="Name">The category name (e.g., "TypeNames", "ClassType", "ControlFlow").</param>
/// <param name="Words">The keywords in this category.</param>
/// <param name="CaseSensitive">Whether keyword matching is case-sensitive. Default is true.</param>
/// <example>
/// <code>
/// // Define a category of type keywords
/// var typeKeywords = new KeywordCategory("TypeNames", ["int", "float", "double", "void"]);
/// 
/// // Case-insensitive keywords (e.g., for SQL)
/// var sqlKeywords = new KeywordCategory("SqlKeywords", ["SELECT", "FROM", "WHERE"], CaseSensitive: false);
/// 
/// // Use with schema
/// var schema = Schema.Create()
///     .DefineKeywords(typeKeywords)
///     .Build();
/// </code>
/// </example>
public sealed record KeywordCategory(
    string Name,
    ImmutableArray<string> Words,
    bool CaseSensitive = true)
{
    /// <summary>
    /// Creates a keyword category from an array of words.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <param name="words">The keywords in this category.</param>
    public KeywordCategory(string name, params string[] words)
        : this(name, [.. words], CaseSensitive: true)
    {
    }
    
    /// <summary>
    /// Creates a keyword category from an array of words with explicit case sensitivity.
    /// </summary>
    /// <param name="name">The category name.</param>
    /// <param name="caseSensitive">Whether keyword matching is case-sensitive.</param>
    /// <param name="words">The keywords in this category.</param>
    public KeywordCategory(string name, bool caseSensitive, params string[] words)
        : this(name, [.. words], caseSensitive)
    {
    }
    
    /// <summary>
    /// Gets the <see cref="StringComparer"/> to use for keyword matching based on <see cref="CaseSensitive"/>.
    /// </summary>
    public StringComparer Comparer => CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}

/// <summary>
/// Metadata about an assigned keyword, used for reverse lookups.
/// </summary>
/// <param name="Category">The category this keyword belongs to.</param>
/// <param name="Keyword">The keyword text.</param>
/// <param name="Kind">The assigned NodeKind.</param>
public sealed record KeywordInfo(string Category, string Keyword, NodeKind Kind);

/// <summary>
/// Predefined keyword categories for common programming languages.
/// </summary>
/// <example>
/// <code>
/// var schema = Schema.Create()
///     .DefineKeywords(CommonKeywords.CTypes)
///     .DefineKeywords(CommonKeywords.CppClassifiers)
///     .DefineKeywords(CommonKeywords.ControlFlow)
///     .Build();
/// </code>
/// </example>
public static class CommonKeywords
{
    /// <summary>
    /// C-style primitive type names.
    /// Includes: void, char, short, int, long, float, double, signed, unsigned
    /// </summary>
    public static KeywordCategory CTypes { get; } = new("CTypes",
        "void", "char", "short", "int", "long", "float", "double", "signed", "unsigned");
    
    /// <summary>
    /// C99/C++ fixed-width integer types.
    /// Includes: int8_t, int16_t, int32_t, int64_t, uint8_t, uint16_t, uint32_t, uint64_t
    /// </summary>
    public static KeywordCategory CFixedWidthTypes { get; } = new("CFixedWidthTypes",
        "int8_t", "int16_t", "int32_t", "int64_t",
        "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "size_t", "ptrdiff_t", "intptr_t", "uintptr_t");
    
    /// <summary>
    /// C++ class/type definition keywords.
    /// Includes: class, struct, enum, union, typedef, typename, template, namespace
    /// </summary>
    public static KeywordCategory CppClassifiers { get; } = new("CppClassifiers",
        "class", "struct", "enum", "union", "typedef", "typename", "template", "namespace");
    
    /// <summary>
    /// C-family control flow keywords.
    /// Includes: if, else, for, while, do, switch, case, default, break, continue, return, goto
    /// </summary>
    public static KeywordCategory ControlFlow { get; } = new("ControlFlow",
        "if", "else", "for", "while", "do", "switch", "case", "default",
        "break", "continue", "return", "goto");
    
    /// <summary>
    /// C++ access specifiers and modifiers.
    /// Includes: public, private, protected, virtual, override, final, static, const, constexpr, mutable, volatile, inline, explicit
    /// </summary>
    public static KeywordCategory CppModifiers { get; } = new("CppModifiers",
        "public", "private", "protected", "virtual", "override", "final",
        "static", "const", "constexpr", "mutable", "volatile", "inline", "explicit");
    
    /// <summary>
    /// C++ exception handling keywords.
    /// Includes: try, catch, throw, noexcept
    /// </summary>
    public static KeywordCategory CppExceptions { get; } = new("CppExceptions",
        "try", "catch", "throw", "noexcept");
    
    /// <summary>
    /// C++ memory and lifetime keywords.
    /// Includes: new, delete, nullptr, this, sizeof, alignof, decltype, auto
    /// </summary>
    public static KeywordCategory CppMemory { get; } = new("CppMemory",
        "new", "delete", "nullptr", "this", "sizeof", "alignof", "decltype", "auto");
    
    /// <summary>
    /// C# type keywords.
    /// Includes: bool, byte, sbyte, char, short, ushort, int, uint, long, ulong, float, double, decimal, string, object, dynamic, var
    /// </summary>
    public static KeywordCategory CSharpTypes { get; } = new("CSharpTypes",
        "bool", "byte", "sbyte", "char", "short", "ushort", "int", "uint",
        "long", "ulong", "float", "double", "decimal", "string", "object", "dynamic", "var");
    
    /// <summary>
    /// C# type definition keywords.
    /// Includes: class, struct, interface, enum, record, delegate, namespace
    /// </summary>
    public static KeywordCategory CSharpClassifiers { get; } = new("CSharpClassifiers",
        "class", "struct", "interface", "enum", "record", "delegate", "namespace");
    
    /// <summary>
    /// C# access modifiers.
    /// Includes: public, private, protected, internal, static, readonly, const, volatile, virtual, override, abstract, sealed, new, partial, extern, unsafe, fixed
    /// </summary>
    public static KeywordCategory CSharpModifiers { get; } = new("CSharpModifiers",
        "public", "private", "protected", "internal", "static", "readonly", "const", "volatile",
        "virtual", "override", "abstract", "sealed", "new", "partial", "extern", "unsafe", "fixed");
    
    /// <summary>
    /// GLSL type keywords.
    /// Includes: void, bool, int, uint, float, double, vec2-4, ivec2-4, uvec2-4, bvec2-4, mat2-4, sampler types
    /// </summary>
    public static KeywordCategory GlslTypes { get; } = new("GlslTypes",
        "void", "bool", "int", "uint", "float", "double",
        "vec2", "vec3", "vec4", "ivec2", "ivec3", "ivec4",
        "uvec2", "uvec3", "uvec4", "bvec2", "bvec3", "bvec4",
        "mat2", "mat3", "mat4", "mat2x2", "mat2x3", "mat2x4",
        "mat3x2", "mat3x3", "mat3x4", "mat4x2", "mat4x3", "mat4x4",
        "sampler1D", "sampler2D", "sampler3D", "samplerCube",
        "sampler2DShadow", "samplerCubeShadow");
    
    /// <summary>
    /// GLSL storage qualifiers.
    /// Includes: const, in, out, inout, uniform, varying, attribute, centroid, flat, smooth, noperspective, layout, buffer, shared
    /// </summary>
    public static KeywordCategory GlslQualifiers { get; } = new("GlslQualifiers",
        "const", "in", "out", "inout", "uniform", "varying", "attribute",
        "centroid", "flat", "smooth", "noperspective", "layout", "buffer", "shared");
    
    /// <summary>
    /// JavaScript/TypeScript keywords.
    /// Includes: var, let, const, function, class, extends, implements, interface, type, enum, import, export, default, async, await, yield
    /// </summary>
    public static KeywordCategory JavaScriptKeywords { get; } = new("JavaScriptKeywords",
        "var", "let", "const", "function", "class", "extends", "implements",
        "interface", "type", "enum", "import", "export", "default", "from",
        "async", "await", "yield", "new", "this", "super", "typeof", "instanceof");
    
    /// <summary>
    /// Boolean literal keywords common across languages.
    /// Includes: true, false, null, nil, undefined
    /// </summary>
    public static KeywordCategory BooleanLiterals { get; } = new("BooleanLiterals",
        "true", "false", "null", "nil", "undefined", "nullptr", "None");
    
    /// <summary>
    /// SQL keywords (case-insensitive).
    /// Includes: SELECT, FROM, WHERE, JOIN, ON, AND, OR, NOT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, TABLE, INDEX, PRIMARY, KEY, FOREIGN, REFERENCES
    /// </summary>
    public static KeywordCategory SqlKeywords { get; } = new("SqlKeywords", caseSensitive: false,
        "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON",
        "AND", "OR", "NOT", "IN", "BETWEEN", "LIKE", "IS", "NULL",
        "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE",
        "CREATE", "DROP", "ALTER", "TABLE", "INDEX", "VIEW",
        "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "UNIQUE", "CONSTRAINT",
        "ORDER", "BY", "ASC", "DESC", "GROUP", "HAVING", "LIMIT", "OFFSET",
        "AS", "DISTINCT", "COUNT", "SUM", "AVG", "MIN", "MAX");
}
