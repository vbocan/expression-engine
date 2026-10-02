#nullable enable

using System;

namespace ExpressionEngine;

/// <summary>
/// Base class for every error the engine reports about an expression.
/// Catch this type to handle parse and evaluation failures uniformly.
/// </summary>
public abstract class ExpressionException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong (without location; it is appended automatically).</param>
    /// <param name="source">The expression text, if known.</param>
    /// <param name="position">Zero-based character index of the problem, or -1 if unknown.</param>
    /// <param name="length">Number of characters the problem spans.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    protected ExpressionException(string message, string? source, int position, int length, Exception? innerException = null)
        : base(Describe(message, source, position), innerException)
    {
        ExpressionText = source;
        Position = source is null ? -1 : position;
        Length = length;
        (Line, Column) = LineAndColumn(source, Position);
    }

    /// <summary>The expression text the error refers to, if known.</summary>
    public string? ExpressionText { get; }

    /// <summary>Zero-based character index of the problem in <see cref="ExpressionText"/>, or -1 if unknown.</summary>
    public int Position { get; }

    /// <summary>Number of characters the problem spans (0 if it is a single point, such as the end of input).</summary>
    public int Length { get; }

    /// <summary>One-based line of the problem, or 0 if unknown.</summary>
    public int Line { get; }

    /// <summary>One-based column of the problem, or 0 if unknown.</summary>
    public int Column { get; }

    private static string Describe(string message, string? source, int position)
    {
        if (source is null || position < 0)
        {
            return message;
        }

        var (line, column) = LineAndColumn(source, position);
        return line == 1
            ? $"{message} (at column {column})"
            : $"{message} (at line {line}, column {column})";
    }

    private static (int Line, int Column) LineAndColumn(string? source, int position)
    {
        if (source is null || position < 0)
        {
            return (0, 0);
        }

        var line = 1;
        var lineStart = 0;
        var end = Math.Min(position, source.Length);
        for (var i = 0; i < end; i++)
        {
            if (source[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, end - lineStart + 1);
    }
}

/// <summary>The expression text is not valid: bad syntax, an unknown function, or it exceeds a configured limit.</summary>
public sealed class ParseException : ExpressionException
{
    /// <summary>Creates a parse error.</summary>
    public ParseException(string message, string? source, int position, int length = 0)
        : base(message, source, position, length)
    {
    }
}

/// <summary>The expression is valid but could not be evaluated: type mismatch, division by zero, a missing variable, etc.</summary>
public class EvaluationException : ExpressionException
{
    /// <summary>Creates an evaluation error.</summary>
    public EvaluationException(string message, string? source, int position, int length = 0, Exception? innerException = null)
        : base(message, source, position, length, innerException)
    {
    }
}

/// <summary>The expression references a variable that neither the supplied variables nor the engine's constants define.</summary>
public sealed class UnknownVariableException : EvaluationException
{
    /// <summary>Creates the exception.</summary>
    public UnknownVariableException(string variableName, string? source, int position, int length = 0)
        : base($"Unknown variable '{variableName}'", source, position, length)
    {
        VariableName = variableName;
    }

    /// <summary>The name of the variable that could not be resolved.</summary>
    public string VariableName { get; }
}
