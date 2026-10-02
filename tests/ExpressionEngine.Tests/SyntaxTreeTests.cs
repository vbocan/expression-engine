using ExpressionEngine.Syntax;

namespace ExpressionEngine.Tests;

public class SyntaxTreeTests
{
    private static Node Root(string expression) => Engine.Default.Parse(expression).Root;

    [Theory]
    [InlineData("1+2*3", "1 + 2 * 3")]
    [InlineData("(1+2)*3", "(1 + 2) * 3")]
    [InlineData("((1))", "1")]
    [InlineData("1-(2-3)", "1 - (2 - 3)")]
    [InlineData("(1-2)-3", "1 - 2 - 3")]
    [InlineData("1+(2+3)", "1 + (2 + 3)")]
    [InlineData("2*(3/4)", "2 * (3 / 4)")]
    [InlineData("2^3^2", "2^3^2")]
    [InlineData("(2^3)^2", "(2^3)^2")]
    [InlineData("-2^2", "-2^2")]
    [InlineData("(-2)^2", "(-2)^2")]
    [InlineData("2^-3", "2^-3")]
    [InlineData("2^(1+1)", "2^(1 + 1)")]
    [InlineData("-(1+2)", "-(1 + 2)")]
    [InlineData("- -x", "--x")]
    [InlineData("!(a&&b)", "!(a && b)")]
    [InlineData("!a&&b", "!a && b")]
    [InlineData("a?b:c?d:e", "a ? b : c ? d : e")]
    [InlineData("(a?b:c)?d:e", "(a ? b : c) ? d : e")]
    [InlineData("a||b&&c", "a || b && c")]
    [InlineData("(a||b)&&c", "(a || b) && c")]
    [InlineData("max( 1 ,2 )", "max(1, 2)")]
    [InlineData("max(a?1:2,3)", "max(a ? 1 : 2, 3)")]
    [InlineData("1e3", "1000")]
    [InlineData("1.5E-2", "0.015")]
    [InlineData("x>=1&&y!=2||!z", "x >= 1 && y != 2 || !z")]
    public void PrintsNormalizedText(string expression, string expected)
    {
        Assert.Equal(expected, Root(expression).ToString());
    }

    [Theory]
    [InlineData("1 + 2 * (3 - x) / y ^ 2 ^ z")]
    [InlineData("a ? b < c && !d : -e % f")]
    [InlineData("max(1, min(2, 3)) - sqrt(x) * -2")]
    [InlineData("-(-(-1))")]
    [InlineData("1 - (2 - (3 - 4))")]
    [InlineData("0.1 + 0.2 * 1e-7 + 123456789012345")]
    public void PrintedFormParsesBackToTheSameTree(string expression)
    {
        var once = Root(expression).ToString();
        var twice = Root(once).ToString();

        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("1 + 2 * x - (y - 3) / 2", 5.5)]
    [InlineData("-x ^ 2 + 2 ^ -1", -3.5)]
    [InlineData("x > 1 ? y : -y", 2.0)]
    public void PrintedFormEvaluatesToTheSameValue(string expression, double expected)
    {
        var vars = new { x = 2, y = 2 };
        var original = Engine.Default.EvaluateNumber(expression, vars);
        var reprinted = Engine.Default.EvaluateNumber(Root(expression).ToString(), vars);

        Assert.Equal(original, reprinted);
        Assert.Equal(expected, original, precision: 10);
    }

    [Fact]
    public void TreeShapeReflectsPrecedence()
    {
        var root = Assert.IsType<BinaryNode>(Root("1 + 2 * 3"));

        Assert.Equal(BinaryOperator.Add, root.Operator);
        Assert.IsType<NumberNode>(root.Left);
        var product = Assert.IsType<BinaryNode>(root.Right);
        Assert.Equal(BinaryOperator.Multiply, product.Operator);
    }

    [Fact]
    public void PowerIsRightAssociative()
    {
        var root = Assert.IsType<BinaryNode>(Root("2 ^ 3 ^ 4"));

        Assert.Equal(2, Assert.IsType<NumberNode>(root.Left).Value);
        Assert.IsType<BinaryNode>(root.Right);
    }

    [Fact]
    public void NodesCarryTheirSourceSpans()
    {
        var root = Assert.IsType<BinaryNode>(Root("1 + foo * 22"));
        var product = Assert.IsType<BinaryNode>(root.Right);
        var foo = Assert.IsType<VariableNode>(product.Left);
        var literal = Assert.IsType<NumberNode>(product.Right);

        Assert.Equal((0, 12), (root.Start, root.Length));
        Assert.Equal((4, 8), (product.Start, product.Length));
        Assert.Equal((4, 3), (foo.Start, foo.Length));
        Assert.Equal((10, 2), (literal.Start, literal.Length));
        Assert.Equal(12, root.End);
    }

    [Fact]
    public void SpansCoverCallsAndUnaryOperators()
    {
        var call = Assert.IsType<CallNode>(Root("max(1, 2)"));
        Assert.Equal((0, 9), (call.Start, call.Length));
        Assert.Equal("max", call.Name);
        Assert.Equal(2, call.Arguments.Count);

        var negation = Assert.IsType<UnaryNode>(Root("  -  x"));
        Assert.Equal((2, 4), (negation.Start, negation.Length));
        Assert.Equal(UnaryOperator.Negate, negation.Operator);
    }

    private sealed class NodeCounter : INodeVisitor<int>
    {
        public int Visit(NumberNode node) => 1;
        public int Visit(BooleanNode node) => 1;
        public int Visit(VariableNode node) => 1;
        public int Visit(UnaryNode node) => 1 + node.Operand.Accept(this);
        public int Visit(BinaryNode node) => 1 + node.Left.Accept(this) + node.Right.Accept(this);
        public int Visit(ConditionalNode node) =>
            1 + node.Condition.Accept(this) + node.WhenTrue.Accept(this) + node.WhenFalse.Accept(this);
        public int Visit(CallNode node) => 1 + node.Arguments.Sum(a => a.Accept(this));
    }

    [Fact]
    public void VisitorsCanWalkEveryNodeKind()
    {
        // conditional(1) + condition x>1 (3) + call max(x, true) (3) + unary -y (2)
        var root = Root("x > 1 ? max(x, true) : -y");

        Assert.Equal(9, root.Accept(new NodeCounter()));
    }
}
