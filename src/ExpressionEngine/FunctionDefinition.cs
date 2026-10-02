#nullable enable

using System;

namespace ExpressionEngine;

/// <summary>A function that expressions can call, such as <c>sqrt(x)</c> or <c>max(a, b, c)</c>.</summary>
public sealed class FunctionDefinition
{
    private readonly Func<double[], double> _implementation;

    /// <summary>Defines a function taking between <paramref name="minArguments"/> and <paramref name="maxArguments"/> numeric arguments.</summary>
    /// <param name="name">The identifier used in expressions. Must start with a letter or underscore, followed by letters, digits or underscores.</param>
    /// <param name="minArguments">Minimum number of arguments (0 or more).</param>
    /// <param name="maxArguments">Maximum number of arguments, or <see cref="int.MaxValue"/> for a variadic function.</param>
    /// <param name="implementation">Receives the evaluated arguments; the array is never shared between calls.</param>
    public FunctionDefinition(string name, int minArguments, int maxArguments, Func<double[], double> implementation)
    {
        Names.ValidateIdentifier(name, nameof(name));
        if (minArguments < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minArguments), "Minimum argument count cannot be negative.");
        }

        if (maxArguments < minArguments)
        {
            throw new ArgumentOutOfRangeException(nameof(maxArguments), "Maximum argument count cannot be less than the minimum.");
        }

        Name = name;
        MinArguments = minArguments;
        MaxArguments = maxArguments;
        _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
    }

    /// <summary>The name used in expressions.</summary>
    public string Name { get; }

    /// <summary>Minimum number of arguments.</summary>
    public int MinArguments { get; }

    /// <summary>Maximum number of arguments; <see cref="int.MaxValue"/> when variadic.</summary>
    public int MaxArguments { get; }

    /// <summary><see langword="true"/> if the function accepts an unbounded number of arguments.</summary>
    public bool IsVariadic => MaxArguments == int.MaxValue;

    /// <summary>Whether <paramref name="count"/> arguments are acceptable.</summary>
    public bool AcceptsArgumentCount(int count) => count >= MinArguments && count <= MaxArguments;

    internal double Invoke(double[] arguments) => _implementation(arguments);

    /// <summary>Human-readable arity such as "1 argument", "2 to 3 arguments" or "at least 1 argument".</summary>
    internal string DescribeArity()
    {
        if (IsVariadic)
        {
            return $"at least {MinArguments} {Plural(MinArguments)}";
        }

        if (MinArguments == MaxArguments)
        {
            return $"{MinArguments} {Plural(MinArguments)}";
        }

        return $"{MinArguments} to {MaxArguments} arguments";
    }

    private static string Plural(int count) => count == 1 ? "argument" : "arguments";

    /// <inheritdoc/>
    public override string ToString() => $"{Name}({DescribeArity()})";
}
