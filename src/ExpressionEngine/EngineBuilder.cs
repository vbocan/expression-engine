#nullable enable

using System;
using System.Collections.Generic;

namespace ExpressionEngine;

/// <summary>Configures and creates an <see cref="Engine"/>.</summary>
/// <remarks>
/// A new builder is empty: no functions and no constants. Call <see cref="AddMathFunctions"/> and
/// <see cref="AddMathConstants"/> for the standard library, then add your own.
/// </remarks>
public sealed class EngineBuilder
{
    /// <summary>The default maximum nesting depth.</summary>
    public const int DefaultMaxDepth = 256;

    /// <summary>The default maximum expression length, in characters.</summary>
    public const int DefaultMaxLength = 10_000;

    private readonly List<FunctionDefinition> _functions = new();
    private readonly List<KeyValuePair<string, double>> _constants = new();
    private bool _ignoreCase;
    private bool _allowNonFinite;
    private int _maxDepth = DefaultMaxDepth;
    private int _maxLength = DefaultMaxLength;

    /// <summary>Adds a function that takes no arguments, e.g. <c>random()</c>.</summary>
    public EngineBuilder AddFunction(string name, Func<double> function)
    {
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        return AddFunction(new FunctionDefinition(name, 0, 0, _ => function()));
    }

    /// <summary>Adds a one-argument function, e.g. <c>AddFunction("double", x =&gt; x * 2)</c>.</summary>
    public EngineBuilder AddFunction(string name, Func<double, double> function)
    {
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        return AddFunction(new FunctionDefinition(name, 1, 1, a => function(a[0])));
    }

    /// <summary>Adds a two-argument function.</summary>
    public EngineBuilder AddFunction(string name, Func<double, double, double> function)
    {
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        return AddFunction(new FunctionDefinition(name, 2, 2, a => function(a[0], a[1])));
    }

    /// <summary>Adds a three-argument function.</summary>
    public EngineBuilder AddFunction(string name, Func<double, double, double, double> function)
    {
        if (function is null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        return AddFunction(new FunctionDefinition(name, 3, 3, a => function(a[0], a[1], a[2])));
    }

    /// <summary>Adds a function taking a variable number of arguments.</summary>
    /// <param name="name">The function name.</param>
    /// <param name="minArguments">Minimum argument count.</param>
    /// <param name="maxArguments">Maximum argument count, or <see cref="int.MaxValue"/> for no limit.</param>
    /// <param name="function">Receives the evaluated arguments.</param>
    public EngineBuilder AddFunction(string name, int minArguments, int maxArguments, Func<double[], double> function) =>
        AddFunction(new FunctionDefinition(name, minArguments, maxArguments, function));

    /// <summary>Adds a prebuilt function. A function with the same name added earlier is replaced.</summary>
    public EngineBuilder AddFunction(FunctionDefinition function)
    {
        _functions.Add(function ?? throw new ArgumentNullException(nameof(function)));
        return this;
    }

    /// <summary>Adds a named constant. A constant with the same name added earlier is replaced. Input variables of the same name take precedence at evaluation time.</summary>
    public EngineBuilder AddConstant(string name, double value)
    {
        Names.ValidateIdentifier(name, nameof(name));
        _constants.Add(new KeyValuePair<string, double>(name, value));
        return this;
    }

    /// <summary>Adds the standard math functions: abs, sign, sqrt, cbrt, pow, exp, ln, log, log10, floor, ceil, round, trunc, sin, cos, tan, asin, acos, atan, atan2, sinh, cosh, tanh, deg, rad, min, max, sum, avg, clamp.</summary>
    public EngineBuilder AddMathFunctions()
    {
        foreach (var function in MathFunctions.All)
        {
            AddFunction(function);
        }

        return this;
    }

    /// <summary>Adds the constants <c>pi</c>, <c>e</c> and <c>tau</c>.</summary>
    public EngineBuilder AddMathConstants()
    {
        AddConstant("pi", Math.PI);
        AddConstant("e", Math.E);
        AddConstant("tau", 2 * Math.PI);
        return this;
    }

    /// <summary>Makes function, constant and variable names case-insensitive (<c>SQRT(X)</c> equals <c>sqrt(x)</c>). Off by default.</summary>
    public EngineBuilder CaseInsensitive(bool enabled = true)
    {
        _ignoreCase = enabled;
        return this;
    }

    /// <summary>
    /// Lets division by zero, overflow and invalid operations such as <c>sqrt(-1)</c> produce infinity or NaN
    /// instead of raising <see cref="EvaluationException"/>. Off by default.
    /// </summary>
    public EngineBuilder AllowNonFiniteResults(bool enabled = true)
    {
        _allowNonFinite = enabled;
        return this;
    }

    /// <summary>Sets the maximum nesting depth (default <see cref="DefaultMaxDepth"/>). Protects against stack exhaustion from hostile input.</summary>
    public EngineBuilder WithMaxDepth(int maxDepth)
    {
        if (maxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "The maximum depth must be at least 1.");
        }

        _maxDepth = maxDepth;
        return this;
    }

    /// <summary>Sets the maximum expression length in characters (default <see cref="DefaultMaxLength"/>).</summary>
    public EngineBuilder WithMaxLength(int maxLength)
    {
        if (maxLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "The maximum length must be at least 1.");
        }

        _maxLength = maxLength;
        return this;
    }

    /// <summary>Creates the engine. The builder can be reused afterwards.</summary>
    public Engine Build()
    {
        var comparer = _ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        var functions = new Dictionary<string, FunctionDefinition>(comparer);
        foreach (var function in _functions)
        {
            functions[function.Name] = function;
        }

        var constants = new Dictionary<string, double>(comparer);
        foreach (var constant in _constants)
        {
            constants[constant.Key] = constant.Value;
        }

        return new Engine(functions, constants, _ignoreCase, _allowNonFinite, _maxDepth, _maxLength);
    }
}
