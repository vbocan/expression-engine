#nullable enable

namespace ExpressionEngine.Syntax;

/// <summary>Prefix operators.</summary>
public enum UnaryOperator
{
    /// <summary><c>+x</c> (numbers only; returns the operand unchanged).</summary>
    Plus,

    /// <summary><c>-x</c>.</summary>
    Negate,

    /// <summary><c>!x</c> (booleans only).</summary>
    Not,
}

/// <summary>Infix operators.</summary>
public enum BinaryOperator
{
    /// <summary><c>+</c></summary>
    Add,

    /// <summary><c>-</c></summary>
    Subtract,

    /// <summary><c>*</c></summary>
    Multiply,

    /// <summary><c>/</c></summary>
    Divide,

    /// <summary><c>%</c> (remainder with the sign of the dividend).</summary>
    Modulo,

    /// <summary><c>^</c> (right-associative).</summary>
    Power,

    /// <summary><c>==</c></summary>
    Equal,

    /// <summary><c>!=</c></summary>
    NotEqual,

    /// <summary><c>&lt;</c></summary>
    Less,

    /// <summary><c>&lt;=</c></summary>
    LessOrEqual,

    /// <summary><c>&gt;</c></summary>
    Greater,

    /// <summary><c>&gt;=</c></summary>
    GreaterOrEqual,

    /// <summary><c>&amp;&amp;</c> (short-circuiting).</summary>
    And,

    /// <summary><c>||</c> (short-circuiting).</summary>
    Or,
}

/// <summary>Precedence and spelling of every operator, shared by the parser and the printer.</summary>
internal static class OperatorInfo
{
    // Lowest to highest. Keep in sync with the grammar in the README.
    public const int Conditional = 1;
    public const int Or = 2;
    public const int And = 3;
    public const int Equality = 4;
    public const int Relational = 5;
    public const int Additive = 6;
    public const int Multiplicative = 7;
    public const int Unary = 8;
    public const int Power = 9;
    public const int Primary = 10;

    public static int PrecedenceOf(BinaryOperator op) => op switch
    {
        BinaryOperator.Or => Or,
        BinaryOperator.And => And,
        BinaryOperator.Equal or BinaryOperator.NotEqual => Equality,
        BinaryOperator.Less or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual => Relational,
        BinaryOperator.Add or BinaryOperator.Subtract => Additive,
        BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo => Multiplicative,
        BinaryOperator.Power => Power,
        _ => throw new System.ArgumentOutOfRangeException(nameof(op)),
    };

    public static bool IsRightAssociative(BinaryOperator op) => op == BinaryOperator.Power;

    public static string Symbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.Power => "^",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.Less => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.Greater => ">",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.And => "&&",
        BinaryOperator.Or => "||",
        _ => throw new System.ArgumentOutOfRangeException(nameof(op)),
    };

    public static string Symbol(UnaryOperator op) => op switch
    {
        UnaryOperator.Plus => "+",
        UnaryOperator.Negate => "-",
        UnaryOperator.Not => "!",
        _ => throw new System.ArgumentOutOfRangeException(nameof(op)),
    };
}
