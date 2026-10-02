#nullable enable

using System;
using System.Runtime.CompilerServices;
using ExpressionEngine.Syntax;

namespace ExpressionEngine;

/// <summary>Tree-walking evaluator. One instance per evaluation; holds no mutable state besides its inputs.</summary>
internal sealed class Interpreter : INodeVisitor<Value>
{
    private readonly Engine _engine;
    private readonly string _source;
    private readonly IVariableSource? _variables;

    private Interpreter(Engine engine, string source, IVariableSource? variables)
    {
        _engine = engine;
        _source = source;
        _variables = variables;
    }

    public static Value Run(Engine engine, string source, Node root, IVariableSource? variables)
    {
        try
        {
            return root.Accept(new Interpreter(engine, source, variables));
        }
        catch (InsufficientExecutionStackException)
        {
            throw new EvaluationException("Expression is nested too deeply to evaluate", source, root.Start, root.Length);
        }
    }

    private Value Evaluate(Node node)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        return node.Accept(this);
    }

    public Value Visit(NumberNode node) => node.Value;

    public Value Visit(BooleanNode node) => node.Value;

    public Value Visit(VariableNode node)
    {
        if (_variables is not null)
        {
            bool found;
            Value value;
            try
            {
                found = _variables.TryGetValue(node.Name, out value);
            }
            catch (Exception ex) when (IsUserCodeFailure(ex))
            {
                throw Error($"Cannot read variable '{node.Name}': {ex.Message}", node, ex);
            }

            if (found)
            {
                return value;
            }
        }

        if (_engine.TryGetConstant(node.Name, out var constant))
        {
            return constant;
        }

        throw new UnknownVariableException(node.Name, _source, node.Start, node.Length);
    }

    public Value Visit(UnaryNode node)
    {
        var operand = Evaluate(node.Operand);
        switch (node.Operator)
        {
            case UnaryOperator.Not:
                if (!operand.IsBoolean)
                {
                    throw Error($"Operator '!' needs a Boolean operand but got a {operand.Kind}", node);
                }

                return !operand.AsBoolean();

            case UnaryOperator.Negate:
                return -RequireNumber(operand, node, "-");

            default:
                return RequireNumber(operand, node, "+");
        }
    }

    public Value Visit(BinaryNode node)
    {
        var op = node.Operator;

        // Short-circuit: the right operand is not evaluated (or type-checked) when the left decides the result.
        if (op == BinaryOperator.And || op == BinaryOperator.Or)
        {
            var left = RequireBoolean(Evaluate(node.Left), node, op);
            if (op == BinaryOperator.And ? !left : left)
            {
                return left;
            }

            return RequireBoolean(Evaluate(node.Right), node, op);
        }

        var l = Evaluate(node.Left);
        var r = Evaluate(node.Right);

        if (op == BinaryOperator.Equal || op == BinaryOperator.NotEqual)
        {
            if (l.Kind != r.Kind)
            {
                throw Error($"Cannot compare a {l.Kind} with a {r.Kind} using '{OperatorInfo.Symbol(op)}'", node);
            }

            return l.Equals(r) == (op == BinaryOperator.Equal);
        }

        if (!l.IsNumber || !r.IsNumber)
        {
            throw Error($"Operator '{OperatorInfo.Symbol(op)}' needs Number operands but got {l.Kind} and {r.Kind}", node);
        }

        var a = l.AsNumber();
        var b = r.AsNumber();
        switch (op)
        {
            case BinaryOperator.Less: return a < b;
            case BinaryOperator.LessOrEqual: return a <= b;
            case BinaryOperator.Greater: return a > b;
            case BinaryOperator.GreaterOrEqual: return a >= b;
            case BinaryOperator.Add: return RequireFinite(a + b, node, "Result");
            case BinaryOperator.Subtract: return RequireFinite(a - b, node, "Result");
            case BinaryOperator.Multiply: return RequireFinite(a * b, node, "Result");
            case BinaryOperator.Divide:
                RequireNonZeroDivisor(b, node);
                return RequireFinite(a / b, node, "Result");
            case BinaryOperator.Modulo:
                RequireNonZeroDivisor(b, node);
                return RequireFinite(a % b, node, "Result");
            case BinaryOperator.Power: return RequireFinite(Math.Pow(a, b), node, "Result");
            default: throw new InvalidOperationException($"Unhandled operator {op}.");
        }
    }

    public Value Visit(ConditionalNode node)
    {
        var condition = Evaluate(node.Condition);
        if (!condition.IsBoolean)
        {
            throw Error($"The condition of '?:' must be a Boolean but is a {condition.Kind}", node.Condition);
        }

        return Evaluate(condition.AsBoolean() ? node.WhenTrue : node.WhenFalse);
    }

    public Value Visit(CallNode node)
    {
        var arguments = new double[node.Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = Evaluate(node.Arguments[i]);
            if (!argument.IsNumber)
            {
                throw Error(
                    $"Argument {i + 1} of '{node.Name}' must be a Number but is a {argument.Kind}", node.Arguments[i]);
            }

            arguments[i] = argument.AsNumber();
        }

        double result;
        try
        {
            result = node.Function.Invoke(arguments);
        }
        catch (Exception ex) when (IsUserCodeFailure(ex))
        {
            throw Error($"Function '{node.Name}' failed: {ex.Message}", node, ex);
        }

        return RequireFinite(result, node, $"Result of '{node.Name}'");
    }

    private static bool IsUserCodeFailure(Exception ex) =>
        ex is not ExpressionException && ex is not InsufficientExecutionStackException && ex is not OutOfMemoryException;

    private EvaluationException Error(string message, Node node, Exception? inner = null) =>
        new(message, _source, node.Start, node.Length, inner);

    private double RequireNumber(Value value, Node node, string symbol)
    {
        if (!value.IsNumber)
        {
            throw Error($"Operator '{symbol}' needs a Number operand but got a {value.Kind}", node);
        }

        return value.AsNumber();
    }

    private bool RequireBoolean(Value value, BinaryNode node, BinaryOperator op)
    {
        if (!value.IsBoolean)
        {
            throw Error($"Operator '{OperatorInfo.Symbol(op)}' needs Boolean operands but got a {value.Kind}", node);
        }

        return value.AsBoolean();
    }

    private void RequireNonZeroDivisor(double divisor, Node node)
    {
        if (divisor == 0 && !_engine.AllowsNonFiniteResults)
        {
            throw Error("Division by zero", node);
        }
    }

    private Value RequireFinite(double result, Node node, string what)
    {
        if (!_engine.AllowsNonFiniteResults && (double.IsNaN(result) || double.IsInfinity(result)))
        {
            throw Error(
                $"{what} is {(double.IsNaN(result) ? "not a number" : "too large to represent")}",
                node);
        }

        return result;
    }
}
