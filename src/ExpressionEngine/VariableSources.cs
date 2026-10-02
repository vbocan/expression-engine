#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;

namespace ExpressionEngine;

/// <summary>Adapts the many shapes of "bag of values" that callers pass to <c>Evaluate</c> into an <see cref="IVariableSource"/>.</summary>
internal static class VariableSources
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> s_caseSensitive = new();
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> s_caseInsensitive = new();

    /// <summary>
    /// Wraps <paramref name="variables"/>. Accepted: <see cref="IVariableSource"/>, string-keyed dictionaries of
    /// numbers or objects, or any object (including anonymous types) whose public properties are the variables.
    /// </summary>
    public static IVariableSource? From(object? variables, bool ignoreCase)
    {
        switch (variables)
        {
            case null:
                return null;
            case IVariableSource source:
                return source;
            case IReadOnlyDictionary<string, double> numbers:
                return new DelegateSource((string name, out Value value) =>
                {
                    var found = numbers.TryGetValue(name, out var number);
                    value = number;
                    return found;
                });
            case IDictionary<string, double> numbers:
                return new DelegateSource((string name, out Value value) =>
                {
                    var found = numbers.TryGetValue(name, out var number);
                    value = number;
                    return found;
                });
            case IReadOnlyDictionary<string, object?> objects:
                return new DelegateSource((string name, out Value value) =>
                    objects.TryGetValue(name, out var raw) ? Convert(name, raw, out value) : NotFound(out value));
            case IDictionary<string, object?> objects:
                return new DelegateSource((string name, out Value value) =>
                    objects.TryGetValue(name, out var raw) ? Convert(name, raw, out value) : NotFound(out value));
            case System.Collections.IDictionary legacy:
                // Dictionary<string, int>, Dictionary<string, decimal>, Hashtable...
                return new DelegateSource((string name, out Value value) =>
                    legacy.Contains(name) ? Convert(name, legacy[name], out value) : NotFound(out value));
            default:
                return new ObjectSource(variables, PropertiesOf(variables.GetType(), ignoreCase));
        }
    }

    private static bool NotFound(out Value value)
    {
        value = default;
        return false;
    }

    private static bool Convert(string name, object? raw, out Value value)
    {
        if (Value.TryFromObject(raw, out value))
        {
            return true;
        }

        throw new InvalidOperationException(DescribeUnusable(name, raw));
    }

    private static string DescribeUnusable(string name, object? raw) => raw is null
        ? $"Variable '{name}' is null."
        : $"Variable '{name}' has unsupported type {raw.GetType().Name}; only numbers and booleans are supported.";

    private static Dictionary<string, PropertyInfo> PropertiesOf(Type type, bool ignoreCase)
    {
        var cache = ignoreCase ? s_caseInsensitive : s_caseSensitive;
        return cache.GetOrAdd(type, t =>
        {
            var map = new Dictionary<string, PropertyInfo>(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var property in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0 && !map.ContainsKey(property.Name))
                {
                    map.Add(property.Name, property);
                }
            }

            return map;
        });
    }

    private delegate bool TryGet(string name, out Value value);

    private sealed class DelegateSource : IVariableSource
    {
        private readonly TryGet _tryGet;

        public DelegateSource(TryGet tryGet) => _tryGet = tryGet;

        public bool TryGetValue(string name, out Value value) => _tryGet(name, out value);
    }

    private sealed class ObjectSource : IVariableSource
    {
        private readonly object _target;
        private readonly Dictionary<string, PropertyInfo> _properties;

        public ObjectSource(object target, Dictionary<string, PropertyInfo> properties)
        {
            _target = target;
            _properties = properties;
        }

        public bool TryGetValue(string name, out Value value)
        {
            if (!_properties.TryGetValue(name, out var property))
            {
                value = default;
                return false;
            }

            return Convert(name, property.GetValue(_target), out value);
        }
    }
}
