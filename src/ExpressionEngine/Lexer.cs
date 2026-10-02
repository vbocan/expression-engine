#nullable enable

using System.Globalization;

namespace ExpressionEngine;

internal enum TokenKind
{
    End,
    Number,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Caret,
    LeftParen,
    RightParen,
    Comma,
    Question,
    Colon,
    Bang,
    EqualEqual,
    BangEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    AmpAmp,
    PipePipe,
}

internal readonly struct Token
{
    public Token(TokenKind kind, int start, int length, double number = 0)
    {
        Kind = kind;
        Start = start;
        Length = length;
        Number = number;
    }

    public TokenKind Kind { get; }

    public int Start { get; }

    public int Length { get; }

    public int End => Start + Length;

    /// <summary>The parsed value, for <see cref="TokenKind.Number"/> tokens.</summary>
    public double Number { get; }
}

/// <summary>Turns expression text into tokens, one at a time. Never loops: every call consumes at least one character or returns End.</summary>
internal sealed class Lexer
{
    private readonly string _source;
    private int _position;

    public Lexer(string source) => _source = source;

    public string Source => _source;

    public Token Next()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }

        if (_position >= _source.Length)
        {
            return new Token(TokenKind.End, _source.Length, 0);
        }

        var start = _position;
        var c = _source[start];

        if (IsAsciiDigit(c) || c == '.')
        {
            return LexNumber(start);
        }

        if (Names.IsIdentifierStart(c))
        {
            _position++;
            while (_position < _source.Length && Names.IsIdentifierPart(_source[_position]))
            {
                _position++;
            }

            return new Token(TokenKind.Identifier, start, _position - start);
        }

        var next = start + 1 < _source.Length ? _source[start + 1] : '\0';
        switch (c)
        {
            case '+': return One(TokenKind.Plus, start);
            case '-': return One(TokenKind.Minus, start);
            case '*': return One(TokenKind.Star, start);
            case '/': return One(TokenKind.Slash, start);
            case '%': return One(TokenKind.Percent, start);
            case '^': return One(TokenKind.Caret, start);
            case '(': return One(TokenKind.LeftParen, start);
            case ')': return One(TokenKind.RightParen, start);
            case ',': return One(TokenKind.Comma, start);
            case '?': return One(TokenKind.Question, start);
            case ':': return One(TokenKind.Colon, start);
            case '!': return next == '=' ? Two(TokenKind.BangEqual, start) : One(TokenKind.Bang, start);
            case '<': return next == '=' ? Two(TokenKind.LessEqual, start) : One(TokenKind.Less, start);
            case '>': return next == '=' ? Two(TokenKind.GreaterEqual, start) : One(TokenKind.Greater, start);
            case '=':
                if (next == '=')
                {
                    return Two(TokenKind.EqualEqual, start);
                }

                throw Error("Unexpected '='. Did you mean '=='?", start, 1);
            case '&':
                if (next == '&')
                {
                    return Two(TokenKind.AmpAmp, start);
                }

                throw Error("Unexpected '&'. Did you mean '&&'?", start, 1);
            case '|':
                if (next == '|')
                {
                    return Two(TokenKind.PipePipe, start);
                }

                throw Error("Unexpected '|'. Did you mean '||'?", start, 1);
            default:
                throw Error($"Unexpected character {Describe(c)}", start, 1);
        }
    }

    private Token One(TokenKind kind, int start)
    {
        _position = start + 1;
        return new Token(kind, start, 1);
    }

    private Token Two(TokenKind kind, int start)
    {
        _position = start + 2;
        return new Token(kind, start, 2);
    }

    private Token LexNumber(int start)
    {
        var p = start;
        var mantissaDigits = 0;

        while (p < _source.Length && IsAsciiDigit(_source[p]))
        {
            p++;
            mantissaDigits++;
        }

        if (p < _source.Length && _source[p] == '.')
        {
            p++;
            while (p < _source.Length && IsAsciiDigit(_source[p]))
            {
                p++;
                mantissaDigits++;
            }
        }

        // Optional exponent: only consumed when it is complete, so "2e" is not silently half-read.
        if (mantissaDigits > 0 && p < _source.Length && (_source[p] == 'e' || _source[p] == 'E'))
        {
            var q = p + 1;
            if (q < _source.Length && (_source[q] == '+' || _source[q] == '-'))
            {
                q++;
            }

            if (q < _source.Length && IsAsciiDigit(_source[q]))
            {
                while (q < _source.Length && IsAsciiDigit(_source[q]))
                {
                    q++;
                }

                p = q;
            }
        }

        // A number must not run straight into more number or name characters: 1.2.3, 3x, 2e
        var malformed = mantissaDigits == 0;
        while (p < _source.Length && (_source[p] == '.' || Names.IsIdentifierPart(_source[p])))
        {
            malformed = true;
            p++;
        }

        _position = p;
        var text = _source.Substring(start, p - start);
        if (malformed)
        {
            throw Error($"Invalid number '{text}'", start, p - start);
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsInfinity(number))
        {
            throw Error($"Number '{text}' is out of range", start, p - start);
        }

        return new Token(TokenKind.Number, start, p - start, number);
    }

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    private static string Describe(char c) => char.IsControl(c) || char.IsSurrogate(c)
        ? $"U+{(int)c:X4}"
        : $"'{c}'";

    private ParseException Error(string message, int start, int length) => new(message, _source, start, length);
}
