namespace ExpressionEngine.Tests;

public class EvaluationTests
{
    private static double Num(string expression, object? variables = null) =>
        Engine.Default.EvaluateNumber(expression, variables);

    private static bool Bool(string expression, object? variables = null) =>
        Engine.Default.EvaluateBoolean(expression, variables);

    [Theory]
    [InlineData("42", 42)]
    [InlineData("10 + 20", 30)]
    [InlineData("10 - 20", -10)]
    [InlineData("10 + 20 - 40 + 100", 90)]
    [InlineData("10 * 20", 200)]
    [InlineData("10 / 20", 0.5)]
    [InlineData("10 * 20 / 50", 4)]
    [InlineData("10 % 4", 2)]
    [InlineData("-10 % 3", -1)]
    [InlineData("5.5 % 2", 1.5)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("  7  *  6  ", 42)]
    public void Arithmetic(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 10);

    [Theory]
    [InlineData("1e3", 1000)]
    [InlineData("1E3", 1000)]
    [InlineData("1.5e-2", 0.015)]
    [InlineData("2e+2", 200)]
    [InlineData(".5 + .5", 1)]
    [InlineData("5.", 5)]
    [InlineData("0.1 * 3", 0.3)]
    public void NumberLiterals(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 10);

    [Theory]
    [InlineData("10 + 20 * 30", 610)]
    [InlineData("(10 + 20) * 30", 900)]
    [InlineData("-(10 + 20) * 30", -900)]
    [InlineData("-((10 + 20) * 5) * 30", -4500)]
    [InlineData("2 * 3 ^ 2", 18)]
    [InlineData("10 - 4 - 3", 3)]          // left-associative
    [InlineData("100 / 10 / 5", 2)]        // left-associative
    [InlineData("2 ^ 3 ^ 2", 512)]         // right-associative
    [InlineData("(2 ^ 3) ^ 2", 64)]
    [InlineData("-2 ^ 2", -4)]             // power binds tighter than unary minus
    [InlineData("(-2) ^ 2", 4)]
    [InlineData("2 ^ -1", 0.5)]            // exponent may carry a sign
    [InlineData("-2 ^ -2", -0.25)]
    [InlineData("1 + 2 * 3 - 4 / 2", 5)]
    public void PrecedenceAndAssociativity(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 10);

    [Theory]
    [InlineData("-10", -10)]
    [InlineData("+10", 10)]
    [InlineData("--10", 10)]
    [InlineData("--++-+-10", 10)]
    [InlineData("10 + -20 - +30", -40)]
    [InlineData("2 * -3", -6)]
    public void UnaryOperators(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 10);

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 < 1", false)]
    [InlineData("2 <= 2", true)]
    [InlineData("3 > 2", true)]
    [InlineData("2 >= 3", false)]
    [InlineData("1 + 1 == 2", true)]
    [InlineData("1 != 2", true)]
    [InlineData("true == true", true)]
    [InlineData("true != false", true)]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("!true", false)]
    [InlineData("!!true", true)]
    [InlineData("true && false || true", true)]     // && binds tighter than ||
    [InlineData("true || false && false", true)]
    [InlineData("!true || true", true)]              // ! binds tighter than ||
    [InlineData("!(1 < 2)", false)]
    [InlineData("1 < 2 && 2 < 3", true)]
    [InlineData("1 + 1 == 2 && 3 * 3 == 9", true)]
    public void Logic(string expression, bool expected) =>
        Assert.Equal(expected, Bool(expression));

    [Theory]
    [InlineData("1 < 2 ? 10 : 20", 10)]
    [InlineData("1 > 2 ? 10 : 20", 20)]
    [InlineData("true ? 1 : 2 + 3", 1)]
    [InlineData("false ? 1 : 2 + 3", 5)]
    [InlineData("false ? 1 : true ? 2 : 3", 2)]      // right-associative
    [InlineData("(true ? false : true) ? 1 : 2", 2)]
    [InlineData("1 + (true ? 2 : 3)", 3)]
    public void Conditional(string expression, double expected) =>
        Assert.Equal(expected, Num(expression), precision: 10);

    [Fact]
    public void ConditionalChainPicksFirstMatch()
    {
        var expression = Engine.Default.Parse("x > 0 ? 1 : x < 0 ? -1 : 0");
        Assert.Equal(1, expression.EvaluateNumber(new { x = 5 }));
        Assert.Equal(-1, expression.EvaluateNumber(new { x = -5 }));
        Assert.Equal(0, expression.EvaluateNumber(new { x = 0 }));
    }

    [Theory]
    [InlineData("false && (1 / 0 > 0)")]
    [InlineData("true || (1 / 0 > 0)")]
    [InlineData("false && 5")]       // the unevaluated operand is not type-checked either
    public void LogicalOperatorsShortCircuit(string expression) =>
        Assert.IsType<bool>(Bool(expression));

    [Fact]
    public void ConditionalOnlyEvaluatesTheChosenBranch()
    {
        Assert.Equal(1, Num("true ? 1 : 1 / 0"));
        Assert.Equal(2, Num("false ? 1 / 0 : 2"));
    }

    [Fact]
    public void ExpressionsAreCultureIndependent()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(2.5, Num("1.5 + 1"));
            Assert.Equal("2.5", Engine.Default.Evaluate("1.5 + 1").ToString());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void ExpressionIsReusableAndThreadSafe()
    {
        var expression = Engine.Default.Parse("a * b + 1");
        var results = new double[1000];

        Parallel.For(0, results.Length, i => results[i] = expression.EvaluateNumber(new { a = i, b = 2 }));

        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal((i * 2) + 1, results[i]);
        }
    }
}
