#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ExpressionEngine.Syntax;

namespace ExpressionEngine;

/// <summary>
/// A parsed expression. Parse once with <see cref="Engine.Parse"/>, then evaluate as many times as you like
/// with different variables. Instances are immutable and safe to share between threads.
/// </summary>
public sealed class Expression
{
    private readonly Engine _engine;

    internal Expression(
        Engine engine,
        string source,
        Node root,
        IReadOnlyList<string> variables,
        IReadOnlyList<FunctionDefinition> functions)
    {
        _engine = engine;
        Source = source;
        Root = root;
        Variables = variables;
        Functions = functions;
    }

    /// <summary>The text this expression was parsed from.</summary>
    public string Source { get; }

    /// <summary>The root of the syntax tree.</summary>
    public Node Root { get; }

    /// <summary>
    /// The input variables the expression refers to, in order of first appearance, without duplicates.
    /// Names that the engine defines as constants (such as <c>pi</c>) are not listed.
    /// </summary>
    public IReadOnlyList<string> Variables { get; }

    /// <summary>The functions the expression calls, without duplicates.</summary>
    public IReadOnlyList<FunctionDefinition> Functions { get; }

    /// <summary>Evaluates the expression. Variables are looked up in <paramref name="variables"/> first, then in the engine's constants.</summary>
    /// <exception cref="EvaluationException">The expression could not be evaluated.</exception>
    public Value Evaluate(IVariableSource? variables = null) =>
        Interpreter.Run(_engine, Source, Root, variables);

    /// <summary>
    /// Evaluates the expression with variables taken from <paramref name="variables"/>, which may be an
    /// <see cref="IVariableSource"/>, a string-keyed dictionary of numbers or booleans, or any object whose public
    /// properties are the variables (for example an anonymous type: <c>new { x = 1, y = 2 }</c>).
    /// </summary>
    /// <exception cref="EvaluationException">The expression could not be evaluated.</exception>
    public Value Evaluate(object variables) =>
        Evaluate(VariableSources.From(variables, _engine.IsCaseInsensitive));

    /// <summary>Evaluates the expression and returns its numeric result.</summary>
    /// <exception cref="EvaluationException">Evaluation failed, or the result is a boolean.</exception>
    public double EvaluateNumber(object? variables = null)
    {
        var result = Evaluate(VariableSources.From(variables, _engine.IsCaseInsensitive));
        if (!result.IsNumber)
        {
            throw new EvaluationException("The expression evaluates to a Boolean, not a Number", Source, Root.Start, Root.Length);
        }

        return result.AsNumber();
    }

    /// <summary>Evaluates the expression and returns its boolean result.</summary>
    /// <exception cref="EvaluationException">Evaluation failed, or the result is a number.</exception>
    public bool EvaluateBoolean(object? variables = null)
    {
        var result = Evaluate(VariableSources.From(variables, _engine.IsCaseInsensitive));
        if (!result.IsBoolean)
        {
            throw new EvaluationException("The expression evaluates to a Number, not a Boolean", Source, Root.Start, Root.Length);
        }

        return result.AsBoolean();
    }

    /// <summary>Evaluates without throwing for expected failures (unknown variable, type mismatch, division by zero...).</summary>
    /// <returns><see langword="true"/> on success.</returns>
    public bool TryEvaluate(object? variables, out Value result, [NotNullWhen(false)] out EvaluationException? error)
    {
        try
        {
            result = Evaluate(VariableSources.From(variables, _engine.IsCaseInsensitive));
            error = null;
            return true;
        }
        catch (EvaluationException ex)
        {
            result = default;
            error = ex;
            return false;
        }
    }

    /// <summary>Returns <see cref="Source"/>. Use <c>Root.ToString()</c> for the normalized form.</summary>
    public override string ToString() => Source;
}
