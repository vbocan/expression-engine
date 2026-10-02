using ExpressionEngine.Syntax;

namespace ExpressionEngine.Tests;

/// <summary>
/// Executes the code samples from docs/MANUAL.md and README.md so the documentation cannot silently drift
/// from the library. If you change a sample there, change it here too.
/// </summary>
public class ManualExamples
{
    [Fact]
    public void Headline()
    {
        double total = Engine.Default.EvaluateNumber(
            "price * (1 + taxRate) - discount",
            new { price = 100, taxRate = 0.2, discount = 5 });

        Assert.Equal(115, total, precision: 10);
    }

    [Fact]
    public void EvaluateInOneLine()
    {
        double area = Engine.Default.EvaluateNumber("pi * r^2", new { r = 10 });
        bool adult = Engine.Default.EvaluateBoolean("age >= 18", new { age = 21 });

        Assert.Equal(314.159265358979, area, precision: 9);
        Assert.True(adult);

        Value result = Engine.Default.Evaluate("1 < 2");
        Assert.True(result.IsBoolean);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void ParseOnceEvaluateMany()
    {
        Expression price = Engine.Default.Parse("base * (1 + taxRate) - discount");

        var vars = new Variables { ["base"] = 100, ["taxRate"] = 0.2, ["discount"] = 5 };
        Assert.Equal(115, price.EvaluateNumber(vars), precision: 10);

        vars["discount"] = 20;
        Assert.Equal(100, price.EvaluateNumber(vars), precision: 10);
    }

    [Fact]
    public void FindOutWhichInputsAreNeeded() =>
        Assert.Equal(new[] { "a", "b", "c" }, Engine.Default.Parse("a * b + max(c, 1) + pi").Variables);

    [Fact]
    public void HandleMistakesInUserInput()
    {
        Assert.False(Engine.Default.TryParse("2 * (3 + $)", out _, out var error));

        Assert.Equal("Unexpected character '$' (at column 10)", error.Message);
        Assert.Equal(9, error.Position);
        Assert.Equal(1, error.Line);
        Assert.Equal(10, error.Column);
    }

    [Fact]
    public void VariablesSet()
    {
        var vars = new Variables { ["price"] = 100, ["taxRate"] = 0.2, ["isMember"] = true };

        Assert.Equal(90, Engine.Default.EvaluateNumber("isMember ? price * 0.9 : price", vars), precision: 10);
    }

    private record Order(int Quantity, decimal UnitPrice, bool Express);

    [Fact]
    public void ObjectProperties()
    {
        var cost = Engine.Default.Parse("Quantity * UnitPrice + (Express ? 15 : 0)");

        Assert.Equal(43.5, cost.EvaluateNumber(new Order(3, 9.5m, true)), precision: 10);

        var insensitive = new EngineBuilder().CaseInsensitive().Build();
        Assert.Equal(28.5, insensitive.EvaluateNumber("quantity * unitPrice", new Order(3, 9.5m, false)), precision: 10);
    }

    [Fact]
    public void DictionarySample() =>
        Assert.Equal(7, Engine.Default.EvaluateNumber("a + b", new Dictionary<string, double> { ["a"] = 3, ["b"] = 4 }));

    [Fact]
    public void CustomFunctionsAndConstants()
    {
        var engine = new EngineBuilder()
            .AddMathFunctions()
            .AddMathConstants()
            .AddFunction("vat", amount => amount * 0.19)
            .AddFunction("discount", (amount, percent) => amount * (1 - (percent / 100)))
            .AddFunction("lerp", (a, b, t) => a + ((b - a) * t))
            .AddFunction("count", 0, int.MaxValue, args => args.Length)
            .AddConstant("shipping", 4.90)
            .Build();

        Assert.Equal(39.1, engine.EvaluateNumber("vat(discount(price, 10)) + shipping", new { price = 200 }), precision: 10);
        Assert.Equal(15, engine.EvaluateNumber("lerp(10, 20, 0.5)"));
        Assert.Equal(3, engine.EvaluateNumber("count(1, 2, 3)"));
    }

    [Fact]
    public void CaseInsensitiveEngine()
    {
        var engine = new EngineBuilder().AddMathFunctions().AddMathConstants().CaseInsensitive().Build();

        Assert.Equal(4, engine.EvaluateNumber("SQRT(Area)", new { area = 16 }));
    }

    [Fact]
    public void ErrorHandlingSample()
    {
        var ex = Assert.Throws<EvaluationException>(() => Engine.Default.Evaluate("1 + (2 * true)"));

        Assert.Equal("Operator '*' needs Number operands but got Number and Boolean (at column 6)", ex.Message);
        Assert.Equal(5, ex.Position);
        Assert.Equal(8, ex.Length);
    }

    private sealed class Prefix : INodeVisitor<string>
    {
        public string Visit(NumberNode n) => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public string Visit(BooleanNode n) => n.Value ? "true" : "false";
        public string Visit(VariableNode n) => n.Name;
        public string Visit(UnaryNode n) => $"({UnaryName(n)} {n.Operand.Accept(this)})";
        public string Visit(BinaryNode n) => $"({n.Operator} {n.Left.Accept(this)} {n.Right.Accept(this)})";
        public string Visit(ConditionalNode n) => $"(if {n.Condition.Accept(this)} {n.WhenTrue.Accept(this)} {n.WhenFalse.Accept(this)})";
        public string Visit(CallNode n) => $"({n.Name} {string.Join(" ", n.Arguments.Select(a => a.Accept(this)))})";
        private static string UnaryName(UnaryNode n) => n.Operator.ToString().ToLowerInvariant();   // plus, negate, not
    }

    [Fact]
    public void VisitorSample()
    {
        Assert.Equal("(Add 1 (Multiply 2 x))", Engine.Default.Parse("1 + 2 * x").Root.Accept(new Prefix()));
        Assert.Equal("(if (Greater x 0) (max x 1) (negate x))", Engine.Default.Parse("x > 0 ? max(x, 1) : -x").Root.Accept(new Prefix()));
    }

    [Fact]
    public void NormalizedPrinting() =>
        Assert.Equal("(1 + 2) * 3 - 4", Engine.Default.Parse("((1+2)*3)  -  (4)").Root.ToString());

    [Fact]
    public void LanguageReferenceClaims()
    {
        // Precedence table notes
        Assert.Equal(-4, Engine.Default.EvaluateNumber("-2^2"));
        Assert.Equal(0.25, Engine.Default.EvaluateNumber("2^-2"));
        Assert.Equal(-1, Engine.Default.EvaluateNumber("-10 % 3"));
        Assert.Equal(512, Engine.Default.EvaluateNumber("2^3^2"));
        Assert.False(Engine.Default.EvaluateBoolean("false && (1/0 > 0)"));
        Assert.Equal(1, Engine.Default.EvaluateNumber("true ? 1 : 1/0"));
        Assert.Throws<ParseException>(() => Engine.Default.Parse("2x"));
        Assert.Throws<EvaluationException>(() => Engine.Default.Evaluate("1 && true"));
        Assert.Throws<EvaluationException>(() => Engine.Default.Evaluate("1 == true"));

        // Literal forms
        foreach (var literal in new[] { "42", "3.14", ".5", "5.", "1e3", "1.5E-2" })
        {
            Assert.True(Engine.Default.TryParse(literal, out _, out _), literal);
        }

        // Variables take precedence over constants
        Assert.Equal(5, Engine.Default.EvaluateNumber("e", new { e = 5 }));

        // Every function listed in the README table exists
        var documented = "abs sign min max clamp floor ceil trunc round sqrt cbrt pow exp ln log log10 sin cos tan asin acos atan atan2 sinh cosh tanh deg rad sum avg"
            .Split(' ');
        var actual = Engine.Default.Functions.Select(f => f.Name).OrderBy(n => n).ToArray();
        Assert.Equal(documented.OrderBy(n => n), actual);
    }
}
