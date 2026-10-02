#nullable enable

using System.Globalization;
using System.Text;

namespace ExpressionEngine.Syntax;

/// <summary>Renders a tree back to text using canonical spacing and the minimum parentheses that preserve its meaning.</summary>
internal sealed class ExpressionPrinter : INodeVisitor<object?>
{
    private readonly StringBuilder _sb = new();

    public static string Print(Node node)
    {
        var printer = new ExpressionPrinter();
        node.Accept(printer);
        return printer._sb.ToString();
    }

    private static int PrecedenceOf(Node node) => node switch
    {
        ConditionalNode => OperatorInfo.Conditional,
        BinaryNode b => OperatorInfo.PrecedenceOf(b.Operator),
        UnaryNode => OperatorInfo.Unary,
        _ => OperatorInfo.Primary,
    };

    private void WriteChild(Node child, bool parenthesize)
    {
        if (parenthesize)
        {
            _sb.Append('(');
        }

        child.Accept(this);
        if (parenthesize)
        {
            _sb.Append(')');
        }
    }

    public object? Visit(NumberNode node)
    {
        _sb.Append(node.Value.ToString("R", CultureInfo.InvariantCulture));
        return null;
    }

    public object? Visit(BooleanNode node)
    {
        _sb.Append(node.Value ? Names.True : Names.False);
        return null;
    }

    public object? Visit(VariableNode node)
    {
        _sb.Append(node.Name);
        return null;
    }

    public object? Visit(UnaryNode node)
    {
        _sb.Append(OperatorInfo.Symbol(node.Operator));
        WriteChild(node.Operand, PrecedenceOf(node.Operand) < OperatorInfo.Unary);
        return null;
    }

    public object? Visit(BinaryNode node)
    {
        var precedence = OperatorInfo.PrecedenceOf(node.Operator);
        var rightAssociative = OperatorInfo.IsRightAssociative(node.Operator);

        var leftPrecedence = PrecedenceOf(node.Left);
        WriteChild(node.Left, leftPrecedence < precedence || (leftPrecedence == precedence && rightAssociative));

        // Power binds tighter than a prefix operator on its left, but the exponent may itself carry one: 2^-3.
        var exponentWithPrefix = node.Operator == BinaryOperator.Power && node.Right is UnaryNode;
        var rightPrecedence = PrecedenceOf(node.Right);
        var parenthesizeRight = !exponentWithPrefix
            && (rightPrecedence < precedence || (rightPrecedence == precedence && !rightAssociative));

        if (node.Operator == BinaryOperator.Power)
        {
            _sb.Append('^');
        }
        else
        {
            _sb.Append(' ').Append(OperatorInfo.Symbol(node.Operator)).Append(' ');
        }

        WriteChild(node.Right, parenthesizeRight);
        return null;
    }

    public object? Visit(ConditionalNode node)
    {
        WriteChild(node.Condition, PrecedenceOf(node.Condition) <= OperatorInfo.Conditional);
        _sb.Append(" ? ");
        node.WhenTrue.Accept(this);
        _sb.Append(" : ");
        node.WhenFalse.Accept(this);
        return null;
    }

    public object? Visit(CallNode node)
    {
        _sb.Append(node.Name).Append('(');
        for (var i = 0; i < node.Arguments.Count; i++)
        {
            if (i > 0)
            {
                _sb.Append(", ");
            }

            node.Arguments[i].Accept(this);
        }

        _sb.Append(')');
        return null;
    }
}
