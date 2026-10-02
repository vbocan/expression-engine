using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

public class TreeDumpTests
{
    private static string Dump(string expression) =>
        string.Join("\n", TreeDump.Render(Engine.Default.Parse(expression).Root));

    [Fact]
    public void LeafIsASingleLine() => Assert.Equal("Number 42", Dump("42"));

    [Fact]
    public void ShowsOperandsAsABranchingTree() =>
        Assert.Equal(
            "Binary Add  (+)\n├─ Number 1\n└─ Binary Multiply  (*)\n   ├─ Number 2\n   └─ Variable x",
            Dump("1 + 2 * x"));

    [Fact]
    public void ParenthesesChangeTheShapeNotTheNodes() =>
        Assert.Equal(
            "Binary Multiply  (*)\n├─ Binary Add  (+)\n│  ├─ Number 1\n│  └─ Number 2\n└─ Variable x",
            Dump("(1 + 2) * x"));

    [Fact]
    public void ShowsUnaryConditionalAndCallNodes() =>
        Assert.Equal(
            "Conditional  (? :)\n" +
            "├─ Boolean true\n" +
            "├─ Unary Negate  (-)\n" +
            "│  └─ Variable y\n" +
            "└─ Call max/2\n" +
            "   ├─ Number 1\n" +
            "   └─ Number 2",
            Dump("true ? -y : max(1, 2)"));

    [Fact]
    public void ShowsPowerAsRightLeaning() =>
        Assert.Equal(
            "Binary Power  (^)\n├─ Number 2\n└─ Binary Power  (^)\n   ├─ Number 3\n   └─ Number 2",
            Dump("2 ^ 3 ^ 2"));

    [Fact]
    public void EveryOperatorHasASymbol()
    {
        foreach (var op in new[] { "+", "-", "*", "/", "%", "^", "==", "!=", "<", "<=", ">", ">=", "&&", "||" })
        {
            var text = op is "&&" or "||" ? "true " + op + " true" : "1 " + op + " 1";
            Assert.Contains($"({op})", Dump(text));
        }

        Assert.Contains("(!)", Dump("!true"));
        Assert.Contains("(+)", Dump("+1"));
    }
}
