#nullable enable

namespace ExpressionEngine;

/// <summary>
/// Supplies input variables to an expression at evaluation time.
/// Implement this to bind expressions to your own data (a database row, a config, a JSON document...).
/// </summary>
public interface IVariableSource
{
    /// <summary>Looks up a variable by the name used in the expression.</summary>
    /// <param name="name">The identifier exactly as written in the expression.</param>
    /// <param name="value">The variable's value, when found.</param>
    /// <returns><see langword="true"/> if the variable exists; <see langword="false"/> to report it as unknown.</returns>
    bool TryGetValue(string name, out Value value);
}
