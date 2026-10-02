namespace ExpressionEngine.Tests;

public class FunctionTests
{
    private static double Num(string expression) => Engine.Default.EvaluateNumber(expression);

    [Theory]
    [InlineData("sqrt(16)", 4)]
    [InlineData("abs(-3)", 3)]
    [InlineData("abs(3)", 3)]
    [InlineData("sign(-5)", -1)]
    [InlineData("sign(0)", 0)]
    [InlineData("sign(9)", 1)]
    [InlineData("cbrt(27)", 3)]
    [InlineData("cbrt(-8)", -2)]
    [InlineData("pow(2, 10)", 1024)]
    [InlineData("exp(0)", 1)]
    [InlineData("ln(e)", 1)]
    [InlineData("log(1000)", 3)]
    [InlineData("log(8, 2)", 3)]
    [InlineData("log10(100)", 2)]
    [InlineData("floor(-1.5)", -2)]
    [InlineData("ceil(1.2)", 2)]
    [InlineData("trunc(-1.9)", -1)]
    [InlineData("round(2.5)", 3)]            // half away from zero, not banker's rounding
    [InlineData("round(-2.5)", -3)]
    [InlineData("round(3.14159, 2)", 3.14)]
    [InlineData("round(1234.5678, 0)", 1235)]
    [InlineData("sin(pi / 2)", 1)]
    [InlineData("cos(0)", 1)]
    [InlineData("tan(0)", 0)]
    [InlineData("atan2(1, 1)", Math.PI / 4)]
    [InlineData("deg(pi)", 180)]
    [InlineData("rad(180)", Math.PI)]
    [InlineData("min(4, 2, 8)", 2)]
    [InlineData("max(1, 5, 3)", 5)]
    [InlineData("max(7)", 7)]
    [InlineData("sum(1, 2, 3)", 6)]
    [InlineData("avg(1, 2, 3, 6)", 3)]
    [InlineData("clamp(15, 0, 10)", 10)]
    [InlineData("clamp(-5, 0, 10)", 0)]
    [InlineData("clamp(5, 0, 10)", 5)]
    [InlineData("max(1, 2) * min(3, 4) + sqrt(9)", 9)]
    [InlineData("sqrt(sqrt(81))", 3)]
    [InlineData("max(sqrt(16), pow(2, 3), 1 + 1)", 8)]
    public void BuiltInFunctions(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 10);

    [Theory]
    [InlineData("pi", Math.PI)]
    [InlineData("e", Math.E)]
    [InlineData("tau", 2 * Math.PI)]
    [InlineData("2 * pi", 2 * Math.PI)]
    public void BuiltInConstants(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 12);

    [Fact]
    public void CustomFunctionsOfEveryArity()
    {
        var engine = new EngineBuilder()
            .AddFunction("zero", () => 7)
            .AddFunction("twice", x => x * 2)
            .AddFunction("rectArea", (w, h) => w * h)
            .AddFunction("lerp", (a, b, t) => a + ((b - a) * t))
            .AddFunction("count", 0, int.MaxValue, args => args.Length)
            .Build();

        Assert.Equal(7, engine.EvaluateNumber("zero()"));
        Assert.Equal(14, engine.EvaluateNumber("twice(7)"));
        Assert.Equal(200, engine.EvaluateNumber("rectArea(10, 20)"));
        Assert.Equal(15, engine.EvaluateNumber("lerp(10, 20, 0.5)"));
        Assert.Equal(0, engine.EvaluateNumber("count()"));
        Assert.Equal(4, engine.EvaluateNumber("count(1, 2, 3, 4)"));
    }

    [Fact]
    public void CustomFunctionsCanReplaceBuiltIns()
    {
        var engine = new EngineBuilder()
            .AddMathFunctions()
            .AddFunction("abs", x => 99)
            .Build();

        Assert.Equal(99, engine.EvaluateNumber("abs(-1)"));
    }

    [Fact]
    public void FunctionArgumentsAreEvaluatedLeftToRight()
    {
        var log = new List<double>();
        var engine = new EngineBuilder()
            .AddFunction("note", x => { log.Add(x); return x; })
            .AddFunction("pair", (a, b) => a + b)
            .Build();

        engine.Evaluate("pair(note(1), note(2))");

        Assert.Equal(new double[] { 1, 2 }, log);
    }

    [Theory]
    [InlineData("sqrt()", "expects 1 argument but got 0")]
    [InlineData("sqrt(1, 2)", "expects 1 argument but got 2")]
    [InlineData("round(1, 2, 3)", "expects 1 to 2 arguments but got 3")]
    [InlineData("max()", "expects at least 1 argument but got 0")]
    [InlineData("clamp(1, 2)", "expects 3 arguments but got 2")]
    public void WrongArgumentCountIsAParseError(string expression, string expectedMessage)
    {
        var ex = Assert.Throws<ParseException>(() => Engine.Default.Parse(expression));
        Assert.Contains(expectedMessage, ex.Message);
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void UnknownFunctionIsAParseErrorWithPosition()
    {
        var ex = Assert.Throws<ParseException>(() => Engine.Default.Parse("1 + frobnicate(2)"));

        Assert.Contains("Unknown function 'frobnicate'", ex.Message);
        Assert.Equal(4, ex.Position);
        Assert.Equal("frobnicate".Length, ex.Length);
    }

    [Fact]
    public void ACallOnAVariableNameIsAnUnknownFunction()
    {
        var ex = Assert.Throws<ParseException>(() => Engine.Default.Parse("x(2)"));
        Assert.Contains("Unknown function 'x'", ex.Message);
    }

    [Fact]
    public void ExceptionsInsideFunctionsAreWrappedWithPosition()
    {
        var engine = new EngineBuilder()
            .AddFunction("boom", x => throw new InvalidOperationException("kaboom"))
            .Build();

        var ex = Assert.Throws<EvaluationException>(() => engine.Evaluate("1 + boom(2)"));

        Assert.Contains("Function 'boom' failed: kaboom", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal(4, ex.Position);
    }

    [Theory]
    [InlineData("round(1.5, 2.5)")]
    [InlineData("round(1.5, -1)")]
    [InlineData("round(1.5, 16)")]
    [InlineData("clamp(1, 5, 0)")]
    public void InvalidArgumentsToBuiltInsAreEvaluationErrors(string expression) =>
        Assert.Throws<EvaluationException>(() => Engine.Default.Evaluate(expression));

    [Fact]
    public void BooleanArgumentsAreRejected()
    {
        var ex = Assert.Throws<EvaluationException>(() => Engine.Default.Evaluate("max(1, true)"));
        Assert.Contains("Argument 2 of 'max' must be a Number", ex.Message);
    }

    [Fact]
    public void ParsedExpressionExposesTheFunctionsItCalls()
    {
        var expression = Engine.Default.Parse("max(a, b) + sqrt(c) + max(1, 2)");

        Assert.Equal(new[] { "max", "sqrt" }, expression.Functions.Select(f => f.Name));
    }

    [Theory]
    [InlineData("1abc")]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("a-b")]
    [InlineData("true")]
    [InlineData("False")]
    public void InvalidFunctionNamesAreRejected(string name) =>
        Assert.Throws<ArgumentException>(() => new EngineBuilder().AddFunction(name, x => x));

    [Fact]
    public void NullFunctionArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineBuilder().AddFunction("f", (Func<double, double>)null!));
        Assert.Throws<ArgumentNullException>(() => new EngineBuilder().AddFunction((FunctionDefinition)null!));
        Assert.Throws<ArgumentNullException>(() => new EngineBuilder().AddFunction(null!, x => x));
    }
}
