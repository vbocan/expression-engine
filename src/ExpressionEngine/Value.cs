#nullable enable

using System;
using System.Globalization;

namespace ExpressionEngine;

/// <summary>The type of a <see cref="Value"/>.</summary>
public enum ValueKind
{
    /// <summary>A double-precision floating point number.</summary>
    Number,

    /// <summary>A boolean (<c>true</c> or <c>false</c>).</summary>
    Boolean,
}

/// <summary>
/// The result of evaluating an expression: either a number or a boolean.
/// Numbers and booleans convert implicitly to <see cref="Value"/>, so
/// <c>variables["x"] = 5</c> and <c>variables["flag"] = true</c> just work.
/// </summary>
public readonly struct Value : IEquatable<Value>
{
    private readonly double _number;
    private readonly ValueKind _kind;

    private Value(double number, ValueKind kind)
    {
        _number = number;
        _kind = kind;
    }

    /// <summary>The kind of value this is. A default-constructed <see cref="Value"/> is the number 0.</summary>
    public ValueKind Kind => _kind;

    /// <summary><see langword="true"/> if this value is a number.</summary>
    public bool IsNumber => _kind == ValueKind.Number;

    /// <summary><see langword="true"/> if this value is a boolean.</summary>
    public bool IsBoolean => _kind == ValueKind.Boolean;

    /// <summary>Creates a numeric value.</summary>
    public static Value FromNumber(double number) => new(number, ValueKind.Number);

    /// <summary>Creates a boolean value.</summary>
    public static Value FromBoolean(bool boolean) => new(boolean ? 1 : 0, ValueKind.Boolean);

    /// <summary>Returns the numeric value.</summary>
    /// <exception cref="InvalidOperationException">The value is not a number.</exception>
    public double AsNumber()
    {
        if (_kind != ValueKind.Number)
        {
            throw new InvalidOperationException($"The value is a {_kind}, not a {ValueKind.Number}.");
        }

        return _number;
    }

    /// <summary>Returns the boolean value.</summary>
    /// <exception cref="InvalidOperationException">The value is not a boolean.</exception>
    public bool AsBoolean()
    {
        if (_kind != ValueKind.Boolean)
        {
            throw new InvalidOperationException($"The value is a {_kind}, not a {ValueKind.Boolean}.");
        }

        return _number != 0;
    }

    /// <summary>Implicitly wraps a number.</summary>
    public static implicit operator Value(double number) => FromNumber(number);

    /// <summary>Implicitly wraps a boolean.</summary>
    public static implicit operator Value(bool boolean) => FromBoolean(boolean);

    /// <summary>Explicitly unwraps a number. Throws <see cref="InvalidOperationException"/> for booleans.</summary>
    public static explicit operator double(Value value) => value.AsNumber();

    /// <summary>Explicitly unwraps a boolean. Throws <see cref="InvalidOperationException"/> for numbers.</summary>
    public static explicit operator bool(Value value) => value.AsBoolean();

    /// <inheritdoc/>
    public bool Equals(Value other) => _kind == other._kind && _number.Equals(other._number);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Value other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => unchecked((_number.GetHashCode() * 397) ^ (int)_kind);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Value left, Value right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Value left, Value right) => !left.Equals(right);

    /// <summary>Formats the value using the invariant culture (<c>true</c>/<c>false</c> for booleans).</summary>
    public override string ToString() => _kind == ValueKind.Boolean
        ? (_number != 0 ? "true" : "false")
        : _number.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Converts the common .NET numeric types and <see cref="bool"/> to a <see cref="Value"/>.</summary>
    internal static bool TryFromObject(object? obj, out Value value)
    {
        switch (obj)
        {
            case Value v: value = v; return true;
            case double d: value = d; return true;
            case bool b: value = b; return true;
            case int i: value = i; return true;
            case float f: value = f; return true;
            case decimal m: value = (double)m; return true;
            case long l: value = l; return true;
            case short s: value = s; return true;
            case byte by: value = by; return true;
            case sbyte sb: value = sb; return true;
            case uint ui: value = ui; return true;
            case ulong ul: value = ul; return true;
            case ushort us: value = us; return true;
            default: value = default; return false;
        }
    }
}
