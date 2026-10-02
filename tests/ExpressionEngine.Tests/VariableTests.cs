using System.Collections;

namespace ExpressionEngine.Tests;

public class VariableTests
{
    private static readonly Engine Engine = ExpressionEngine.Engine.Default;

    [Fact]
    public void VariablesClassWithInitializer()
    {
        var vars = new Variables { ["price"] = 100, ["taxRate"] = 0.2, ["isMember"] = true };

        Assert.Equal(120, Engine.EvaluateNumber("price * (1 + taxRate)", vars));
        Assert.Equal(100, Engine.EvaluateNumber("isMember ? price : 0", vars));
    }

    [Fact]
    public void VariablesCanBeChangedBetweenEvaluations()
    {
        var expression = Engine.Parse("r * 2");
        var vars = new Variables { ["r"] = 5 };

        Assert.Equal(10, expression.EvaluateNumber(vars));
        vars["r"] = 21;
        Assert.Equal(42, expression.EvaluateNumber(vars));
    }

    [Fact]
    public void AnonymousObject() =>
        Assert.Equal(2 * Math.PI * 10, Engine.EvaluateNumber("2 * pi * r", new { r = 10 }), precision: 12);

    private sealed class Order
    {
        public int Quantity { get; set; } = 3;
        public decimal UnitPrice { get; set; } = 9.5m;
        public bool Express { get; set; } = true;
        public long Big { get; set; } = 5_000_000_000;
        public string? Name { get; set; } = "widget";
        public double? Discount { get; set; }
        public double this[int i] => i;   // indexers are ignored
        public double Field = 1;          // fields are ignored
    }

    [Fact]
    public void ObjectPropertiesOfCommonNumericTypes()
    {
        var order = new Order();

        Assert.Equal(28.5, Engine.EvaluateNumber("Quantity * UnitPrice", order));
        Assert.True(Engine.EvaluateBoolean("Express && Quantity > 2", order));
        Assert.Equal(5_000_000_000, Engine.EvaluateNumber("Big", order));
    }

    [Fact]
    public void ObjectPropertiesOfUnsupportedTypesProduceAClearError()
    {
        var order = new Order();

        var text = Assert.Throws<EvaluationException>(() => Engine.Evaluate("Name", order));
        Assert.Contains("'Name' has unsupported type String", text.Message);

        var nullable = Assert.Throws<EvaluationException>(() => Engine.Evaluate("Discount", order));
        Assert.Contains("'Discount' is null", nullable.Message);
        Assert.Equal(0, nullable.Position);
    }

    [Fact]
    public void FieldsAndIndexersAreNotVariables()
    {
        var order = new Order();

        Assert.Throws<UnknownVariableException>(() => Engine.Evaluate("Field", order));
        Assert.Throws<UnknownVariableException>(() => Engine.Evaluate("Item", order));
    }

    [Fact]
    public void DictionariesOfNumbers()
    {
        Assert.Equal(7, Engine.EvaluateNumber("a + b", new Dictionary<string, double> { ["a"] = 3, ["b"] = 4 }));
        Assert.Equal(7, Engine.EvaluateNumber("a + b", new Dictionary<string, int> { ["a"] = 3, ["b"] = 4 }));
        Assert.Equal(7, Engine.EvaluateNumber("a + b", new Dictionary<string, decimal> { ["a"] = 3, ["b"] = 4 }));
        Assert.Equal(7, Engine.EvaluateNumber("a + b", (IReadOnlyDictionary<string, double>)new Dictionary<string, double> { ["a"] = 3, ["b"] = 4 }));
        Assert.Equal(7, Engine.EvaluateNumber("a + b", new Hashtable { ["a"] = 3, ["b"] = 4.0 }));
    }

    [Fact]
    public void DictionariesOfObjects()
    {
        var vars = new Dictionary<string, object> { ["n"] = 4, ["flag"] = true, ["x"] = 1.5 };

        Assert.Equal(5.5, Engine.EvaluateNumber("flag ? n + x : 0", vars));
    }

    [Fact]
    public void DictionaryWithUnsupportedValueTypeProducesAClearError()
    {
        var vars = new Dictionary<string, object> { ["s"] = "text" };

        var ex = Assert.Throws<EvaluationException>(() => Engine.Evaluate("s", vars));
        Assert.Contains("unsupported type String", ex.Message);
    }

    private sealed class RowSource : IVariableSource
    {
        public bool TryGetValue(string name, out Value value)
        {
            if (name.StartsWith("col", StringComparison.Ordinal) && int.TryParse(name[3..], out var index))
            {
                value = index * 10;
                return true;
            }

            value = default;
            return false;
        }
    }

    [Fact]
    public void CustomVariableSource()
    {
        Assert.Equal(30, Engine.EvaluateNumber("col1 + col2", new RowSource()));
        Assert.Throws<UnknownVariableException>(() => Engine.Evaluate("nope", new RowSource()));
    }

    [Fact]
    public void ThrowingVariableSourceIsReportedWithPosition()
    {
        var source = new ThrowingSource();

        var ex = Assert.Throws<EvaluationException>(() => Engine.Evaluate("1 + broken", source));

        Assert.Contains("Cannot read variable 'broken'", ex.Message);
        Assert.NotNull(ex.InnerException);
        Assert.Equal(4, ex.Position);
    }

    private sealed class ThrowingSource : IVariableSource
    {
        public bool TryGetValue(string name, out Value value) => throw new IOException("disk on fire");
    }

    [Fact]
    public void MissingVariableReportsItsNameAndPosition()
    {
        var ex = Assert.Throws<UnknownVariableException>(() => Engine.Evaluate("1 + total * 2", new { other = 1 }));

        Assert.Equal("total", ex.VariableName);
        Assert.Equal(4, ex.Position);
        Assert.Equal(5, ex.Length);
        Assert.Contains("Unknown variable 'total'", ex.Message);
    }

    [Fact]
    public void EvaluatingWithoutAnyVariablesReportsAnUnknownVariable_NotANullReference()
    {
        var expression = Engine.Parse("x + 1");

        Assert.Throws<UnknownVariableException>(() => expression.Evaluate());
        Assert.Throws<UnknownVariableException>(() => expression.Evaluate(variables: null));
        Assert.Equal(3, Engine.Parse("1 + 2").EvaluateNumber());
    }

    [Fact]
    public void ProvidedVariablesOverrideConstants()
    {
        Assert.Equal(5, Engine.EvaluateNumber("e", new { e = 5 }));
        Assert.Equal(Math.E, Engine.EvaluateNumber("e"));
    }

    [Fact]
    public void VariableNamesAreCaseSensitiveByDefault()
    {
        Assert.Throws<UnknownVariableException>(() => Engine.Evaluate("X", new { x = 1 }));
        Assert.Throws<UnknownVariableException>(() => Engine.Evaluate("x", new Variables { ["X"] = 1 }));
    }

    [Fact]
    public void CaseInsensitiveEngineMatchesObjectsAndEngineVariables()
    {
        var engine = new EngineBuilder().AddMathFunctions().AddMathConstants().CaseInsensitive().Build();

        Assert.Equal(20, engine.EvaluateNumber("price * 2", new { Price = 10 }));
        Assert.Equal(4, engine.EvaluateNumber("SQRT(X)", new { x = 16 }));

        var vars = engine.CreateVariables();
        vars["Total"] = 6;
        Assert.Equal(7, engine.EvaluateNumber("TOTAL + 1", vars));
        Assert.True(engine.EvaluateBoolean("TRUE && True"));
    }

    [Fact]
    public void ParsedExpressionListsItsInputVariablesInOrderWithoutDuplicates()
    {
        var expression = Engine.Parse("b + a * b + pi + max(c, 1) + a");

        Assert.Equal(new[] { "b", "a", "c" }, expression.Variables);   // 'pi' is a constant, not an input
    }

    [Fact]
    public void ExpressionWithoutVariablesHasNone() =>
        Assert.Empty(Engine.Parse("1 + 2 * pi").Variables);

    [Fact]
    public void VariablesCollectionBehavesLikeADictionary()
    {
        var vars = new Variables { { "a", 1 }, { "b", true } };

        Assert.Equal(2, vars.Count);
        Assert.True(vars.Contains("a"));
        Assert.Equal(1.0, vars["a"].AsNumber());
        Assert.True(vars["b"].AsBoolean());
        Assert.Throws<ArgumentException>(() => vars.Add("a", 2));
        Assert.Throws<KeyNotFoundException>(() => vars["zzz"]);
        Assert.True(vars.Remove("a"));
        Assert.False(vars.Remove("a"));
        Assert.Single(vars);
        vars.Clear();
        Assert.Empty(vars);
    }
}
