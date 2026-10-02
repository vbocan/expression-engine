namespace ExpressionEngine.Tests;

public class EngineTests
{
    [Fact]
    public void DefaultEngineHasMathFunctionsAndConstants()
    {
        var engine = Engine.Default;

        Assert.Contains(engine.Functions, f => f.Name == "sqrt");
        Assert.Contains(engine.Functions, f => f.Name == "clamp");
        Assert.Equal(Math.PI, engine.Constants["pi"]);
        Assert.False(engine.IsCaseInsensitive);
        Assert.False(engine.AllowsNonFiniteResults);
        Assert.Equal(EngineBuilder.DefaultMaxDepth, engine.MaxDepth);
        Assert.Equal(EngineBuilder.DefaultMaxLength, engine.MaxLength);
    }

    [Fact]
    public void NewBuilderStartsEmpty()
    {
        var engine = new EngineBuilder().Build();

        Assert.Empty(engine.Functions);
        Assert.Empty(engine.Constants);
        Assert.Equal(3, engine.EvaluateNumber("1 + 2"));
        Assert.Throws<ParseException>(() => engine.Parse("sqrt(4)"));
        Assert.Throws<UnknownVariableException>(() => engine.Evaluate("pi"));
    }

    [Fact]
    public void CustomConstants()
    {
        var engine = new EngineBuilder().AddConstant("taxRate", 0.2).AddConstant("answer", 42).Build();

        Assert.Equal(42, engine.EvaluateNumber("answer"));
        Assert.Equal(120, engine.EvaluateNumber("100 * (1 + taxRate)"), precision: 10);
        Assert.Empty(engine.Parse("answer + taxRate").Variables);
    }

    [Fact]
    public void LaterConstantsReplaceEarlierOnes()
    {
        var engine = new EngineBuilder().AddMathConstants().AddConstant("pi", 3).Build();

        Assert.Equal(3, engine.EvaluateNumber("pi"));
    }

    [Theory]
    [InlineData("1abc")]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("true")]
    public void InvalidConstantNamesAreRejected(string name) =>
        Assert.Throws<ArgumentException>(() => new EngineBuilder().AddConstant(name, 1));

    [Fact]
    public void BuilderCanBeReusedAfterBuild()
    {
        var builder = new EngineBuilder().AddMathFunctions();
        var first = builder.Build();
        builder.AddFunction("extra", x => x + 1);
        var second = builder.Build();

        Assert.Throws<ParseException>(() => first.Parse("extra(1)"));
        Assert.Equal(2, second.EvaluateNumber("extra(1)"));
    }

    [Fact]
    public void CaseInsensitiveEngineIgnoresCaseOfFunctionsConstantsAndLiterals()
    {
        var engine = new EngineBuilder().AddMathFunctions().AddMathConstants().CaseInsensitive().Build();

        Assert.True(engine.IsCaseInsensitive);
        Assert.Equal(4, engine.EvaluateNumber("SQRT(16)"));
        Assert.Equal(Math.PI, engine.EvaluateNumber("PI"));
        Assert.True(engine.EvaluateBoolean("True && !FALSE"));
    }

    [Fact]
    public void DefaultEngineIsCaseSensitive()
    {
        Assert.Throws<ParseException>(() => Engine.Default.Parse("SQRT(16)"));
        Assert.Throws<UnknownVariableException>(() => Engine.Default.Evaluate("PI"));
        Assert.Throws<UnknownVariableException>(() => Engine.Default.Evaluate("True"));   // an ordinary variable name
    }

    [Fact]
    public void ExpressionKeepsItsSourceAndRoot()
    {
        var expression = Engine.Default.Parse("  1+2  ");

        Assert.Equal("  1+2  ", expression.Source);
        Assert.Equal("  1+2  ", expression.ToString());
        Assert.Equal("1 + 2", expression.Root.ToString());
    }

    [Fact]
    public void EvaluateReturnsTypedValues()
    {
        var number = Engine.Default.Evaluate("1 + 2");
        var boolean = Engine.Default.Evaluate("1 < 2");

        Assert.True(number.IsNumber);
        Assert.Equal(3, number.AsNumber());
        Assert.True(boolean.IsBoolean);
        Assert.True(boolean.AsBoolean());
    }

    [Fact]
    public void EachEngineKeepsItsOwnFunctions()
    {
        var a = new EngineBuilder().AddFunction("f", x => x + 1).Build();
        var b = new EngineBuilder().AddFunction("f", x => x + 100).Build();

        Assert.Equal(2, a.EvaluateNumber("f(1)"));
        Assert.Equal(101, b.EvaluateNumber("f(1)"));

        // An expression stays bound to the functions of the engine that parsed it.
        var parsedByA = a.Parse("f(1)");
        Assert.Equal(2, parsedByA.EvaluateNumber());
    }
}
