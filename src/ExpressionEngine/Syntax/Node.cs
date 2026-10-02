#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace ExpressionEngine.Syntax;

/// <summary>
/// A node of the parsed expression tree. Trees are immutable and are produced by <see cref="Engine.Parse"/>;
/// use <see cref="Accept{T}"/> with an <see cref="INodeVisitor{T}"/> to analyze or transform them.
/// </summary>
public abstract class Node
{
    private protected Node(int start, int length, int depth)
    {
        Start = start;
        Length = length;
        Depth = depth;
    }

    /// <summary>Zero-based index of the node's first character in the source text.</summary>
    public int Start { get; }

    /// <summary>Number of source characters the node spans.</summary>
    public int Length { get; }

    /// <summary>Zero-based index just past the node's last character.</summary>
    public int End => Start + Length;

    /// <summary>Height of the tree rooted at this node (a leaf has depth 1).</summary>
    internal int Depth { get; }

    /// <summary>Dispatches to the matching method of <paramref name="visitor"/>.</summary>
    public abstract T Accept<T>(INodeVisitor<T> visitor);

    /// <summary>The expression in normalized form: canonical spacing and only the parentheses that are needed.</summary>
    public override string ToString() => ExpressionPrinter.Print(this);

    internal static int DepthOf(params Node[] children) => 1 + children.Max(c => c.Depth);
}

/// <summary>A numeric literal such as <c>3.14</c> or <c>1e-3</c>.</summary>
public sealed class NumberNode : Node
{
    internal NumberNode(double value, int start, int length)
        : base(start, length, 1) => Value = value;

    /// <summary>The literal's value.</summary>
    public double Value { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>A boolean literal: <c>true</c> or <c>false</c>.</summary>
public sealed class BooleanNode : Node
{
    internal BooleanNode(bool value, int start, int length)
        : base(start, length, 1) => Value = value;

    /// <summary>The literal's value.</summary>
    public bool Value { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>A reference to an input variable or constant, such as <c>price</c> or <c>pi</c>.</summary>
public sealed class VariableNode : Node
{
    internal VariableNode(string name, int start, int length)
        : base(start, length, 1) => Name = name;

    /// <summary>The identifier as written in the expression.</summary>
    public string Name { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>A prefix operator applied to one operand: <c>-x</c>, <c>+x</c> or <c>!x</c>.</summary>
public sealed class UnaryNode : Node
{
    internal UnaryNode(UnaryOperator op, Node operand, int start)
        : base(start, operand.End - start, 1 + operand.Depth)
    {
        Operator = op;
        Operand = operand;
    }

    /// <summary>The operator.</summary>
    public UnaryOperator Operator { get; }

    /// <summary>The operand.</summary>
    public Node Operand { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>An infix operator applied to two operands, such as <c>a + b</c> or <c>a &amp;&amp; b</c>.</summary>
public sealed class BinaryNode : Node
{
    internal BinaryNode(BinaryOperator op, Node left, Node right)
        : base(left.Start, right.End - left.Start, DepthOf(left, right))
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    /// <summary>The operator.</summary>
    public BinaryOperator Operator { get; }

    /// <summary>The left operand.</summary>
    public Node Left { get; }

    /// <summary>The right operand.</summary>
    public Node Right { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>The conditional operator: <c>condition ? whenTrue : whenFalse</c>.</summary>
public sealed class ConditionalNode : Node
{
    internal ConditionalNode(Node condition, Node whenTrue, Node whenFalse)
        : base(condition.Start, whenFalse.End - condition.Start, DepthOf(condition, whenTrue, whenFalse))
    {
        Condition = condition;
        WhenTrue = whenTrue;
        WhenFalse = whenFalse;
    }

    /// <summary>The boolean condition.</summary>
    public Node Condition { get; }

    /// <summary>Evaluated when the condition is true.</summary>
    public Node WhenTrue { get; }

    /// <summary>Evaluated when the condition is false.</summary>
    public Node WhenFalse { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>A call to a function, such as <c>max(a, b)</c>.</summary>
public sealed class CallNode : Node
{
    internal CallNode(FunctionDefinition function, string name, IReadOnlyList<Node> arguments, int start, int end)
        : base(start, end - start, 1 + (arguments.Count == 0 ? 0 : arguments.Max(a => a.Depth)))
    {
        Function = function;
        Name = name;
        Arguments = arguments;
    }

    /// <summary>The function name as written in the expression.</summary>
    public string Name { get; }

    /// <summary>The function this call was bound to when the expression was parsed.</summary>
    public FunctionDefinition Function { get; }

    /// <summary>The argument expressions.</summary>
    public IReadOnlyList<Node> Arguments { get; }

    /// <inheritdoc/>
    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.Visit(this);
}
