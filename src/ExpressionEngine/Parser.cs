#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExpressionEngine.Syntax;

namespace ExpressionEngine;

/// <summary>
/// Precedence-climbing parser. Grammar, lowest precedence first:
/// <code>
/// conditional : or ( '?' conditional ':' conditional )?
/// or          : and ( '||' and )*
/// and         : equality ( '&amp;&amp;' equality )*
/// equality    : relational ( ('==' | '!=') relational )*
/// relational  : additive ( ('&lt;' | '&lt;=' | '&gt;' | '&gt;=') additive )*
/// additive    : multiplicative ( ('+' | '-') multiplicative )*
/// multiplicative : unary ( ('*' | '/' | '%') unary )*
/// unary       : ('+' | '-' | '!') unary | power
/// power       : primary ( '^' unary )?
/// primary     : number | 'true' | 'false' | identifier | identifier '(' arguments? ')' | '(' conditional ')'
/// </code>
/// </summary>
internal sealed class Parser
{
    private readonly Engine _engine;
    private readonly Lexer _lexer;
    private readonly string _source;
    private readonly List<string> _variables = new();
    private readonly HashSet<string> _variableSet;
    private readonly List<FunctionDefinition> _functions = new();
    private int _nesting;
    private Token _current;

    private Parser(Engine engine, string source)
    {
        _engine = engine;
        _source = source;
        _lexer = new Lexer(source);
        _variableSet = new HashSet<string>(engine.NameComparer);
        _current = _lexer.Next();
    }

    public static Expression Parse(Engine engine, string source)
    {
        if (source.Length > engine.MaxLength)
        {
            throw new ParseException(
                $"Expression is longer than the maximum of {engine.MaxLength} characters", source, engine.MaxLength);
        }

        try
        {
            var parser = new Parser(engine, source);
            if (parser._current.Kind == TokenKind.End)
            {
                throw new ParseException("Expression is empty", source, 0);
            }

            var root = parser.ParseConditional();
            if (parser._current.Kind != TokenKind.End)
            {
                throw parser.Unexpected(parser._current);
            }

            return new Expression(engine, source, root, parser._variables, parser._functions);
        }
        catch (InsufficientExecutionStackException)
        {
            throw new ParseException("Expression is nested too deeply to parse", source, 0);
        }
    }

    private void Advance() => _current = _lexer.Next();

    private string TextOf(Token token) => _source.Substring(token.Start, token.Length);

    private ParseException Unexpected(Token token) => token.Kind == TokenKind.End
        ? new ParseException("Unexpected end of expression", _source, token.Start)
        : new ParseException($"Unexpected '{TextOf(token)}'", _source, token.Start, token.Length);

    private void Expect(TokenKind kind, string message)
    {
        if (_current.Kind != kind)
        {
            throw _current.Kind == TokenKind.End
                ? new ParseException(message + ", but the expression ended", _source, _current.Start)
                : new ParseException($"{message}, but found '{TextOf(_current)}'", _source, _current.Start, _current.Length);
        }

        Advance();
    }

    private void Enter()
    {
        if (++_nesting > _engine.MaxDepth)
        {
            throw new ParseException(
                $"Expression is nested too deeply (maximum depth is {_engine.MaxDepth})", _source, _current.Start);
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
    }

    private void Leave() => _nesting--;

    private T Check<T>(T node) where T : Node
    {
        if (node.Depth > _engine.MaxDepth)
        {
            throw new ParseException(
                $"Expression is too complex (maximum depth is {_engine.MaxDepth})", _source, node.Start, node.Length);
        }

        return node;
    }

    private Node ParseConditional()
    {
        Enter();
        try
        {
            var condition = ParseBinary(OperatorInfo.Or);
            if (_current.Kind != TokenKind.Question)
            {
                return condition;
            }

            Advance();
            var whenTrue = ParseConditional();
            Expect(TokenKind.Colon, "Expected ':' to complete the conditional expression");
            var whenFalse = ParseConditional();
            return Check(new ConditionalNode(condition, whenTrue, whenFalse));
        }
        finally
        {
            Leave();
        }
    }

    // All left-associative infix operators except '^', which is handled in ParsePower.
    private Node ParseBinary(int minimumPrecedence)
    {
        var left = ParseUnary();
        while (TryGetBinaryOperator(_current.Kind, out var op))
        {
            var precedence = OperatorInfo.PrecedenceOf(op);
            if (precedence < minimumPrecedence)
            {
                break;
            }

            Advance();
            var right = ParseBinary(precedence + 1);
            left = Check(new BinaryNode(op, left, right));
        }

        return left;
    }

    private static bool TryGetBinaryOperator(TokenKind kind, out BinaryOperator op)
    {
        switch (kind)
        {
            case TokenKind.Plus: op = BinaryOperator.Add; return true;
            case TokenKind.Minus: op = BinaryOperator.Subtract; return true;
            case TokenKind.Star: op = BinaryOperator.Multiply; return true;
            case TokenKind.Slash: op = BinaryOperator.Divide; return true;
            case TokenKind.Percent: op = BinaryOperator.Modulo; return true;
            case TokenKind.EqualEqual: op = BinaryOperator.Equal; return true;
            case TokenKind.BangEqual: op = BinaryOperator.NotEqual; return true;
            case TokenKind.Less: op = BinaryOperator.Less; return true;
            case TokenKind.LessEqual: op = BinaryOperator.LessOrEqual; return true;
            case TokenKind.Greater: op = BinaryOperator.Greater; return true;
            case TokenKind.GreaterEqual: op = BinaryOperator.GreaterOrEqual; return true;
            case TokenKind.AmpAmp: op = BinaryOperator.And; return true;
            case TokenKind.PipePipe: op = BinaryOperator.Or; return true;
            default: op = default; return false;
        }
    }

    private Node ParseUnary()
    {
        UnaryOperator op;
        switch (_current.Kind)
        {
            case TokenKind.Plus: op = UnaryOperator.Plus; break;
            case TokenKind.Minus: op = UnaryOperator.Negate; break;
            case TokenKind.Bang: op = UnaryOperator.Not; break;
            default: return ParsePower();
        }

        Enter();
        try
        {
            var start = _current.Start;
            Advance();
            var operand = ParseUnary();
            return Check(new UnaryNode(op, operand, start));
        }
        finally
        {
            Leave();
        }
    }

    // '^' is right-associative and its exponent may carry a prefix operator (2^-3), so the exponent is a unary.
    // That makes -2^2 parse as -(2^2), the usual mathematical convention.
    private Node ParsePower()
    {
        var left = ParsePrimary();
        if (_current.Kind != TokenKind.Caret)
        {
            return left;
        }

        Enter();
        try
        {
            Advance();
            var right = ParseUnary();
            return Check(new BinaryNode(BinaryOperator.Power, left, right));
        }
        finally
        {
            Leave();
        }
    }

    private Node ParsePrimary()
    {
        var token = _current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new NumberNode(token.Number, token.Start, token.Length);

            case TokenKind.Identifier:
                return ParseIdentifier();

            case TokenKind.LeftParen:
                Advance();
                var inner = ParseConditional();
                Expect(TokenKind.RightParen, "Expected ')' to close the parenthesis opened at column " + (token.Start + 1));
                return inner;

            default:
                throw Unexpected(token);
        }
    }

    private Node ParseIdentifier()
    {
        var token = _current;
        var name = TextOf(token);
        Advance();

        if (_current.Kind != TokenKind.LeftParen)
        {
            if (_engine.NameComparer.Equals(name, Names.True))
            {
                return new BooleanNode(true, token.Start, token.Length);
            }

            if (_engine.NameComparer.Equals(name, Names.False))
            {
                return new BooleanNode(false, token.Start, token.Length);
            }

            if (!_engine.TryGetConstant(name, out _) && _variableSet.Add(name))
            {
                _variables.Add(name);
            }

            return new VariableNode(name, token.Start, token.Length);
        }

        if (!_engine.TryGetFunction(name, out var function))
        {
            throw new ParseException($"Unknown function '{name}'", _source, token.Start, token.Length);
        }

        Advance(); // '('
        var arguments = new List<Node>();
        if (_current.Kind != TokenKind.RightParen)
        {
            while (true)
            {
                arguments.Add(ParseConditional());
                if (_current.Kind != TokenKind.Comma)
                {
                    break;
                }

                Advance();
            }
        }

        var closing = _current;
        Expect(TokenKind.RightParen, $"Expected ',' or ')' in the call to '{name}'");

        if (!function.AcceptsArgumentCount(arguments.Count))
        {
            throw new ParseException(
                $"Function '{name}' expects {function.DescribeArity()} but got {arguments.Count}",
                _source,
                token.Start,
                closing.End - token.Start);
        }

        if (_functions.IndexOf(function) < 0)
        {
            _functions.Add(function);
        }

        return Check(new CallNode(function, name, arguments, token.Start, closing.End));
    }
}
