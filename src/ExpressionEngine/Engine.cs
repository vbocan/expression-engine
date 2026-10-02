#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ExpressionEngine;

/// <summary>
/// Parses and evaluates expressions. An engine owns the set of functions and constants that expressions may use,
/// plus options such as case sensitivity and safety limits. Engines are immutable and thread-safe; create one
/// with <see cref="EngineBuilder"/> and reuse it, or use <see cref="Default"/>.
/// </summary>
public sealed class Engine
{
    private readonly Dictionary<string, FunctionDefinition> _functions;
    private readonly Dictionary<string, double> _constants;

    internal Engine(
        Dictionary<string, FunctionDefinition> functions,
        Dictionary<string, double> constants,
        bool ignoreCase,
        bool allowNonFinite,
        int maxDepth,
        int maxLength)
    {
        _functions = functions;
        _constants = constants;
        IsCaseInsensitive = ignoreCase;
        AllowsNonFiniteResults = allowNonFinite;
        MaxDepth = maxDepth;
        MaxLength = maxLength;
        NameComparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }

    /// <summary>
    /// A ready-to-use engine with the built-in math functions and constants (see <see cref="EngineBuilder.AddMathFunctions"/>),
    /// case-sensitive names and the default safety limits.
    /// </summary>
    public static Engine Default { get; } = new EngineBuilder().AddMathFunctions().AddMathConstants().Build();

    /// <summary>Whether function, constant and (for object/<see cref="Variables"/> sources created by the engine) variable names ignore case.</summary>
    public bool IsCaseInsensitive { get; }

    /// <summary>Whether NaN, infinity and division by zero are returned as values instead of raising <see cref="EvaluationException"/>.</summary>
    public bool AllowsNonFiniteResults { get; }

    /// <summary>Maximum nesting depth of an expression (parentheses, unary chains, long operator chains).</summary>
    public int MaxDepth { get; }

    /// <summary>Maximum expression length in characters.</summary>
    public int MaxLength { get; }

    /// <summary>The functions expressions may call.</summary>
    public IReadOnlyCollection<FunctionDefinition> Functions => _functions.Values;

    /// <summary>The named constants expressions may use, such as <c>pi</c>.</summary>
    public IReadOnlyDictionary<string, double> Constants => _constants;

    internal StringComparer NameComparer { get; }

    internal bool TryGetFunction(string name, [MaybeNullWhen(false)] out FunctionDefinition function) =>
        _functions.TryGetValue(name, out function);

    internal bool TryGetConstant(string name, out double value) => _constants.TryGetValue(name, out value);

    /// <summary>Parses an expression.</summary>
    /// <exception cref="ParseException">The text is not a valid expression.</exception>
    public Expression Parse(string expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        return Parser.Parse(this, expression);
    }

    /// <summary>Parses an expression without throwing for invalid text.</summary>
    /// <returns><see langword="true"/> on success; otherwise <paramref name="error"/> describes the problem and its position.</returns>
    public bool TryParse(string? expression, [NotNullWhen(true)] out Expression? result, [NotNullWhen(false)] out ParseException? error)
    {
        try
        {
            result = Parse(expression ?? string.Empty);
            error = null;
            return true;
        }
        catch (ParseException ex)
        {
            result = null;
            error = ex;
            return false;
        }
    }

    /// <summary>Parses and evaluates in one step. Prefer <see cref="Parse"/> when evaluating the same expression repeatedly.</summary>
    /// <param name="expression">The expression text.</param>
    /// <param name="variables">An <see cref="IVariableSource"/>, a string-keyed dictionary, or an object whose public properties are the variables.</param>
    public Value Evaluate(string expression, object? variables = null) =>
        Parse(expression).Evaluate(VariableSources.From(variables, IsCaseInsensitive));

    /// <summary>Parses and evaluates an expression that must produce a number.</summary>
    public double EvaluateNumber(string expression, object? variables = null) =>
        Parse(expression).EvaluateNumber(variables);

    /// <summary>Parses and evaluates an expression that must produce a boolean.</summary>
    public bool EvaluateBoolean(string expression, object? variables = null) =>
        Parse(expression).EvaluateBoolean(variables);

    /// <summary>Creates an empty <see cref="Variables"/> set whose name matching follows this engine's case setting.</summary>
    public Variables CreateVariables() => new(IsCaseInsensitive);
}
