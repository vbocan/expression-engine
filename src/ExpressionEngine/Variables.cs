#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace ExpressionEngine;

/// <summary>
/// A simple, mutable set of named input variables. Supports collection-initializer syntax:
/// <code>
/// var vars = new Variables { ["price"] = 100, ["taxRate"] = 0.2, ["isMember"] = true };
/// </code>
/// </summary>
public sealed class Variables : IVariableSource, IEnumerable<KeyValuePair<string, Value>>
{
    private readonly Dictionary<string, Value> _values;

    /// <summary>Creates an empty set of variables.</summary>
    /// <param name="ignoreCase">Match names case-insensitively. Use the same setting as your <see cref="Engine"/>, or create the set with <see cref="Engine.CreateVariables"/>.</param>
    public Variables(bool ignoreCase = false)
    {
        _values = new Dictionary<string, Value>(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    /// <summary>The number of variables.</summary>
    public int Count => _values.Count;

    /// <summary>Gets or sets a variable. Reading an undefined name throws <see cref="KeyNotFoundException"/>.</summary>
    public Value this[string name]
    {
        get => _values[name];
        set => _values[name] = value;
    }

    /// <summary>Adds a variable. Throws <see cref="ArgumentException"/> if it already exists.</summary>
    public void Add(string name, Value value) => _values.Add(name, value);

    /// <summary>Removes a variable.</summary>
    /// <returns><see langword="true"/> if it existed.</returns>
    public bool Remove(string name) => _values.Remove(name);

    /// <summary>Removes all variables.</summary>
    public void Clear() => _values.Clear();

    /// <summary>Whether a variable with this name exists.</summary>
    public bool Contains(string name) => _values.ContainsKey(name);

    /// <inheritdoc/>
    public bool TryGetValue(string name, out Value value) => _values.TryGetValue(name, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, Value>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
