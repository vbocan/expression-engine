#nullable enable

namespace ExpressionEngine.Syntax;

/// <summary>
/// Visits every kind of <see cref="Node"/>. Implement it to walk an expression tree, for example to
/// collect variables, translate to another language, or pretty-print.
/// </summary>
/// <typeparam name="T">The type each visit returns.</typeparam>
public interface INodeVisitor<out T>
{
    /// <summary>Visits a numeric literal.</summary>
    T Visit(NumberNode node);

    /// <summary>Visits a boolean literal.</summary>
    T Visit(BooleanNode node);

    /// <summary>Visits a variable or constant reference.</summary>
    T Visit(VariableNode node);

    /// <summary>Visits a prefix operator.</summary>
    T Visit(UnaryNode node);

    /// <summary>Visits an infix operator.</summary>
    T Visit(BinaryNode node);

    /// <summary>Visits a conditional (<c>?:</c>) expression.</summary>
    T Visit(ConditionalNode node);

    /// <summary>Visits a function call.</summary>
    T Visit(CallNode node);
}
