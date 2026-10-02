#nullable enable

using System;
using System.Collections.Generic;

namespace ExpressionEngine;

/// <summary>The standard function library registered by <see cref="EngineBuilder.AddMathFunctions"/>.</summary>
internal static class MathFunctions
{
    public static IEnumerable<FunctionDefinition> All { get; } = Create();

    private static FunctionDefinition[] Create() => new[]
    {
        Unary("abs", Math.Abs),
        Unary("sign", x => double.IsNaN(x) ? double.NaN : Math.Sign(x)),
        Unary("sqrt", Math.Sqrt),
        Unary("cbrt", x => x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0)),
        Binary("pow", Math.Pow),
        Unary("exp", Math.Exp),
        Unary("ln", Math.Log),
        Range("log", 1, 2, a => a.Length == 1 ? Math.Log10(a[0]) : Math.Log(a[0], a[1])),
        Unary("log10", Math.Log10),
        Unary("floor", Math.Floor),
        Unary("ceil", Math.Ceiling),
        Range("round", 1, 2, a => Round(a)),
        Unary("trunc", Math.Truncate),
        Unary("sin", Math.Sin),
        Unary("cos", Math.Cos),
        Unary("tan", Math.Tan),
        Unary("asin", Math.Asin),
        Unary("acos", Math.Acos),
        Unary("atan", Math.Atan),
        Binary("atan2", Math.Atan2),
        Unary("sinh", Math.Sinh),
        Unary("cosh", Math.Cosh),
        Unary("tanh", Math.Tanh),
        Unary("deg", x => x * 180.0 / Math.PI),
        Unary("rad", x => x * Math.PI / 180.0),
        Range("min", 1, int.MaxValue, a => Fold(a, Math.Min)),
        Range("max", 1, int.MaxValue, a => Fold(a, Math.Max)),
        Range("sum", 1, int.MaxValue, a => Fold(a, (x, y) => x + y)),
        Range("avg", 1, int.MaxValue, a => Fold(a, (x, y) => x + y) / a.Length),
        new FunctionDefinition("clamp", 3, 3, a => Clamp(a[0], a[1], a[2])),
    };

    private static FunctionDefinition Unary(string name, Func<double, double> f) =>
        new(name, 1, 1, a => f(a[0]));

    private static FunctionDefinition Binary(string name, Func<double, double, double> f) =>
        new(name, 2, 2, a => f(a[0], a[1]));

    private static FunctionDefinition Range(string name, int min, int max, Func<double[], double> f) =>
        new(name, min, max, f);

    private static double Fold(double[] values, Func<double, double, double> combine)
    {
        var result = values[0];
        for (var i = 1; i < values.Length; i++)
        {
            result = combine(result, values[i]);
        }

        return result;
    }

    // Rounds half away from zero, the way people round by hand (Math.Round defaults to banker's rounding).
    private static double Round(double[] a)
    {
        if (a.Length == 1)
        {
            return Math.Round(a[0], MidpointRounding.AwayFromZero);
        }

        var digits = a[1];
        if (digits != Math.Floor(digits) || digits < 0 || digits > 15)
        {
            throw new ArgumentException("The number of digits must be a whole number between 0 and 15.");
        }

        return Math.Round(a[0], (int)digits, MidpointRounding.AwayFromZero);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (min > max)
        {
            throw new ArgumentException($"The lower bound ({min}) is greater than the upper bound ({max}).");
        }

        return value < min ? min : value > max ? max : value;
    }
}
