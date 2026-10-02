namespace ExpressionEngine.Tests;

public class ErrorTests
{
    private static ParseException ParseError(string expression) =>
        Assert.Throws<ParseException>(() => Engine.Default.Parse(expression));

    private static EvaluationException EvalError(string expression, object? variables = null) =>
        Assert.ThrowsAny<EvaluationException>(() => Engine.Default.Evaluate(expression, variables));

    [Theory]
    [InlineData("5 + $", "Unexpected character '$'", 4)]
    [InlineData("$", "Unexpected character '$'", 0)]
    [InlineData("5 $ 3", "Unexpected character '$'", 2)]
    [InlineData("1 + 2 # comment", "Unexpected character '#'", 6)]
    [InlineData("x = 1", "Did you mean '=='?", 2)]
    [InlineData("a & b", "Did you mean '&&'?", 2)]
    [InlineData("a | b", "Did you mean '||'?", 2)]
    [InlineData("٣", "Unexpected character '٣'", 0)]          // Arabic-Indic digit: not a number
    [InlineData("1 \u0001", "Unexpected character U+0001", 2)]
    public void UnrecognizedCharacters(string expression, string message, int position)
    {
        var ex = ParseError(expression);

        Assert.Contains(message, ex.Message);
        Assert.Equal(position, ex.Position);
    }

    [Theory]
    [InlineData("1.2.3", "Invalid number '1.2.3'")]
    [InlineData("1..2", "Invalid number '1..2'")]
    [InlineData(".", "Invalid number '.'")]
    [InlineData("3x", "Invalid number '3x'")]
    [InlineData("2e", "Invalid number '2e'")]
    [InlineData("2e+", "Invalid number '2e'")]
    [InlineData("1e999", "out of range")]
    public void MalformedNumbers(string expression, string message)
    {
        var ex = ParseError(expression);

        Assert.Contains(message, ex.Message);
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void DotInsideNameIsNotSilentlyAccepted()
    {
        var ex = ParseError("a.b");

        Assert.Contains("Invalid number '.b'", ex.Message);
        Assert.Equal(1, ex.Position);
    }

    [Theory]
    [InlineData("", "Expression is empty", 0)]
    [InlineData("   ", "Expression is empty", 0)]
    [InlineData("1 +", "Unexpected end of expression", 3)]
    [InlineData("(1 + 2", "Expected ')'", 6)]
    [InlineData("1 + 2)", "Unexpected ')'", 5)]
    [InlineData("2 3", "Unexpected '3'", 2)]
    [InlineData("2 x", "Unexpected 'x'", 2)]
    [InlineData("()", "Unexpected ')'", 1)]
    [InlineData("1 ? 2", "Expected ':'", 5)]
    [InlineData("1 ? 2 3", "Expected ':'", 6)]
    [InlineData("* 2", "Unexpected '*'", 0)]
    [InlineData("1 + * 2", "Unexpected '*'", 4)]
    [InlineData("max(1,)", "Unexpected ')'", 6)]
    [InlineData("max(1 2)", "Expected ',' or ')'", 6)]
    [InlineData("max(1, 2", "Expected ',' or ')'", 8)]
    [InlineData("2 ^", "Unexpected end of expression", 3)]
    [InlineData("!", "Unexpected end of expression", 1)]
    public void SyntaxErrors(string expression, string message, int position)
    {
        var ex = ParseError(expression);

        Assert.Contains(message, ex.Message);
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void UnclosedParenthesisPointsBackAtTheOpeningOne()
    {
        var ex = ParseError("1 + (2 * 3");

        Assert.Contains("opened at column 5", ex.Message);
    }

    [Fact]
    public void ErrorsKnowTheirLineAndColumn()
    {
        var ex = ParseError("1 +\n  $");

        Assert.Equal(2, ex.Line);
        Assert.Equal(3, ex.Column);
        Assert.Contains("at line 2, column 3", ex.Message);
        Assert.Equal("1 +\n  $", ex.ExpressionText);

        var single = ParseError("1 + $");
        Assert.Equal(1, single.Line);
        Assert.Equal(5, single.Column);
        Assert.Contains("(at column 5)", single.Message);
    }

    [Theory]
    [InlineData("true + 1", "needs Number operands but got Boolean and Number")]
    [InlineData("1 * false", "needs Number operands but got Number and Boolean")]
    [InlineData("1 < true", "needs Number operands")]
    [InlineData("-true", "needs a Number operand")]
    [InlineData("+false", "needs a Number operand")]
    [InlineData("!5", "needs a Boolean operand")]
    [InlineData("5 && true", "needs Boolean operands")]
    [InlineData("true && 5", "needs Boolean operands")]
    [InlineData("1 || true", "needs Boolean operands")]
    [InlineData("5 ? 1 : 2", "condition of '?:' must be a Boolean")]
    [InlineData("1 == true", "Cannot compare a Number with a Boolean")]
    [InlineData("true != 0", "Cannot compare a Boolean with a Number")]
    public void TypeErrors(string expression, string message) =>
        Assert.Contains(message, EvalError(expression).Message);

    [Fact]
    public void TypeErrorsPointAtTheOffendingNode()
    {
        var ex = EvalError("1 + (2 * true)");

        Assert.Equal(5, ex.Position);                 // the inner multiplication
        Assert.Equal("2 * true".Length, ex.Length);
    }

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("0 / 0")]
    [InlineData("5 % 0")]
    [InlineData("1 / (2 - 2)")]
    public void DivisionByZeroThrowsByDefault(string expression) =>
        Assert.Contains("Division by zero", EvalError(expression).Message);

    [Theory]
    [InlineData("sqrt(-1)", "not a number")]
    [InlineData("ln(-1)", "not a number")]
    [InlineData("10 ^ 400", "too large")]
    [InlineData("exp(1000)", "too large")]
    [InlineData("0 ^ -1", "too large")]
    public void NonFiniteResultsThrowByDefault(string expression, string message) =>
        Assert.Contains(message, EvalError(expression).Message);

    [Fact]
    public void NonFiniteResultsCanBeAllowed()
    {
        var engine = new EngineBuilder().AddMathFunctions().AllowNonFiniteResults().Build();

        Assert.True(engine.AllowsNonFiniteResults);
        Assert.Equal(double.PositiveInfinity, engine.EvaluateNumber("1 / 0"));
        Assert.Equal(double.NegativeInfinity, engine.EvaluateNumber("-1 / 0"));
        Assert.True(double.IsNaN(engine.EvaluateNumber("0 / 0")));
        Assert.True(double.IsNaN(engine.EvaluateNumber("sqrt(-1)")));
        Assert.Equal(double.PositiveInfinity, engine.EvaluateNumber("10 ^ 400"));
    }

    [Fact]
    public void ResultTypeMismatchIsReported()
    {
        Assert.Contains("evaluates to a Boolean", Assert.Throws<EvaluationException>(() => Engine.Default.EvaluateNumber("1 < 2")).Message);
        Assert.Contains("evaluates to a Number", Assert.Throws<EvaluationException>(() => Engine.Default.EvaluateBoolean("1 + 2")).Message);
    }

    [Fact]
    public void TryParseReportsErrorsWithoutThrowing()
    {
        Assert.True(Engine.Default.TryParse("1 + 2", out var ok, out var noError));
        Assert.NotNull(ok);
        Assert.Null(noError);

        Assert.False(Engine.Default.TryParse("1 +", out var none, out var error));
        Assert.Null(none);
        Assert.Equal(3, error!.Position);

        Assert.False(Engine.Default.TryParse(null, out _, out var nullError));
        Assert.Contains("empty", nullError!.Message);
    }

    [Fact]
    public void TryEvaluateReportsErrorsWithoutThrowing()
    {
        var expression = Engine.Default.Parse("a / b");

        Assert.True(expression.TryEvaluate(new { a = 6, b = 3 }, out var value, out var noError));
        Assert.Equal(2, value.AsNumber());
        Assert.Null(noError);

        Assert.False(expression.TryEvaluate(new { a = 6, b = 0 }, out _, out var error));
        Assert.Contains("Division by zero", error!.Message);

        Assert.False(expression.TryEvaluate(new { a = 6 }, out _, out var missing));
        Assert.IsType<UnknownVariableException>(missing);
    }

    [Fact]
    public void BothExceptionTypesShareABaseClassForUniformHandling()
    {
        Assert.IsAssignableFrom<ExpressionException>(ParseError("1 +"));
        Assert.IsAssignableFrom<ExpressionException>(EvalError("1 / 0"));
        Assert.IsAssignableFrom<EvaluationException>(EvalError("nope"));
    }

    [Fact]
    public void NullExpressionIsAnArgumentError() =>
        Assert.Throws<ArgumentNullException>(() => Engine.Default.Parse(null!));
}
