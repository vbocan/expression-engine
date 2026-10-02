using System.Text;
using ExpressionEngine.Syntax;

namespace ExpressionEngine.Playground;

/// <summary>Draws a syntax tree as indented text, to show how an expression was understood.</summary>
internal static class TreeDump
{
    public static IReadOnlyList<string> Render(Node root)
    {
        var lines = new List<string>();
        Write(root, string.Empty, isLast: true, isRoot: true, lines);
        return lines;
    }

    private static void Write(Node node, string prefix, bool isLast, bool isRoot, List<string> lines)
    {
        var branch = isRoot ? string.Empty : isLast ? "└─ " : "├─ ";
        lines.Add(prefix + branch + Describe(node));

        var children = ChildrenOf(node);
        var childPrefix = isRoot ? string.Empty : prefix + (isLast ? "   " : "│  ");
        for (var i = 0; i < children.Count; i++)
        {
            Write(children[i], childPrefix, i == children.Count - 1, isRoot: false, lines);
        }
    }

    private static string Describe(Node node)
    {
        var text = new StringBuilder();
        switch (node)
        {
            case NumberNode n:
                text.Append("Number ").Append(n.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case BooleanNode b:
                text.Append("Boolean ").Append(b.Value ? "true" : "false");
                break;
            case VariableNode v:
                text.Append("Variable ").Append(v.Name);
                break;
            case UnaryNode u:
                text.Append("Unary ").Append(u.Operator).Append("  (").Append(UnarySymbol(u.Operator)).Append(')');
                break;
            case BinaryNode bin:
                text.Append("Binary ").Append(bin.Operator).Append("  (").Append(BinarySymbol(bin)).Append(')');
                break;
            case ConditionalNode:
                text.Append("Conditional  (? :)");
                break;
            case CallNode call:
                text.Append("Call ").Append(call.Name).Append('/').Append(call.Arguments.Count);
                break;
            default:
                text.Append(node.GetType().Name);
                break;
        }

        return text.ToString();
    }

    private static string UnarySymbol(UnaryOperator op) => op switch
    {
        UnaryOperator.Plus => "+",
        UnaryOperator.Negate => "-",
        _ => "!",
    };

    // The library keeps its operator table internal, so the playground spells the operators itself.
    private static string BinarySymbol(BinaryNode node) => node.Operator switch
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
        _ => "||",
    };

    private static IReadOnlyList<Node> ChildrenOf(Node node) => node switch
    {
        UnaryNode u => new[] { u.Operand },
        BinaryNode b => new[] { b.Left, b.Right },
        ConditionalNode c => new[] { c.Condition, c.WhenTrue, c.WhenFalse },
        CallNode call => call.Arguments,
        _ => Array.Empty<Node>(),
    };
}
