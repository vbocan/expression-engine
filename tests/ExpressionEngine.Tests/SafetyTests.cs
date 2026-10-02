namespace ExpressionEngine.Tests;

/// <summary>
/// Regression tests for the failures the original implementation had on hostile or sloppy input:
/// infinite loops, process-killing stack overflows and raw framework exceptions.
/// </summary>
public class SafetyTests
{
    /// <summary>Runs on a thread with a deliberately small stack and fails the test if it takes too long (infinite loop) or crashes.</summary>
    private static T RunGuarded<T>(Func<T> work, int stackBytes = 1024 * 1024, int timeoutSeconds = 20)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            },
            stackBytes)
        { IsBackground = true };

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(timeoutSeconds)), "The operation did not finish: infinite loop?");

        if (error is not null)
        {
            throw new AggregateException(error);
        }

        return result!;
    }

    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Theory]
    [InlineData("5 + $")]        // the original tokenizer looped forever on this
    [InlineData("$$$$")]
    [InlineData("1 + ~2")]
    [InlineData("(((#")]
    public void UnrecognizedCharactersNeverHang(string expression)
    {
        var error = RunGuarded(() => Capture(() => Engine.Default.Parse(expression)));

        Assert.IsType<ParseException>(error);
    }

    [Fact]
    public void DeeplyNestedParenthesesAreRejectedNotCrashed()
    {
        // 1,000 levels fit inside the default length limit and exceed the default depth limit.
        var expression = new string('(', 1000) + "1" + new string(')', 1000);

        var error = RunGuarded(() => Capture(() => Engine.Default.Parse(expression)));

        var ex = Assert.IsType<ParseException>(error);
        Assert.Contains("nested too deeply", ex.Message);
    }

    [Fact]
    public void LongUnaryChainsAreRejectedNotCrashed()
    {
        var expression = new string('-', 5000) + "1";

        var error = RunGuarded(() => Capture(() => Engine.Default.Parse(expression)));

        Assert.IsType<ParseException>(error);
    }

    [Fact]
    public void LongPowerChainsAreRejectedNotCrashed()
    {
        var expression = string.Join("^", Enumerable.Repeat("1", 5000));

        var error = RunGuarded(() => Capture(() => Engine.Default.Parse(expression)));

        Assert.IsType<ParseException>(error);
    }

    [Fact]
    public void LongOperatorChainsAreRejectedNotCrashed()
    {
        // The parser builds these iteratively, but the tree is as deep as the chain is long.
        var expression = string.Join("+", Enumerable.Repeat("1", 5000));

        var error = RunGuarded(() => Capture(() => Engine.Default.Parse(expression)));

        Assert.IsType<ParseException>(error);
    }

    [Fact]
    public void ChainsWithinTheDepthLimitStillWork()
    {
        var expression = string.Join("+", Enumerable.Repeat("1", EngineBuilder.DefaultMaxDepth));

        var result = RunGuarded(() => Engine.Default.EvaluateNumber(expression));

        Assert.Equal(EngineBuilder.DefaultMaxDepth, result);
    }

    [Fact]
    public void DepthLimitIsConfigurable()
    {
        var engine = new EngineBuilder().WithMaxDepth(3).Build();

        Assert.Equal(6, engine.EvaluateNumber("1 + 2 + 3"));
        Assert.Throws<ParseException>(() => engine.Parse("1 + 2 + 3 + 4"));
    }

    [Fact]
    public void ARaisedDepthLimitCannotCrashTheProcess()
    {
        // Even when the configured limits are absurd, the engine must fail with an exception rather than a stack overflow.
        var engine = new EngineBuilder().WithMaxDepth(int.MaxValue).WithMaxLength(100_000_000).Build();
        var nested = new string('(', 500_000) + "1" + new string(')', 500_000);
        var chain = string.Join("+", Enumerable.Repeat("1", 300_000));

        var nestedError = RunGuarded(() => Capture(() => engine.Parse(nested)));
        var chainError = RunGuarded(() => Capture(() => engine.Evaluate(chain)));

        Assert.IsAssignableFrom<ExpressionException>(nestedError);
        Assert.IsAssignableFrom<ExpressionException>(chainError);
    }

    [Fact]
    public void OverlongExpressionsAreRejected()
    {
        var expression = new string('1', EngineBuilder.DefaultMaxLength + 1);

        var ex = Assert.Throws<ParseException>(() => Engine.Default.Parse(expression));

        Assert.Contains("longer than the maximum", ex.Message);
    }

    [Fact]
    public void LengthLimitIsConfigurable()
    {
        var engine = new EngineBuilder().WithMaxLength(5).Build();

        Assert.Equal(12, engine.EvaluateNumber("12"));
        Assert.Throws<ParseException>(() => engine.Parse("1 + 2 + 3"));
    }

    [Fact]
    public void InvalidLimitsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EngineBuilder().WithMaxDepth(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EngineBuilder().WithMaxLength(0));
    }

    [Fact]
    public void RandomInputNeverCrashesHangsOrLeaksFrameworkExceptions()
    {
        string[] pieces =
        {
            "0", "1", "42", "3.14", ".", "..", "1e", "1e5", "e", "pi", "x", "y", "z", "true", "false",
            "+", "-", "*", "/", "%", "^", "(", ")", ",", "?", ":", "!", "==", "!=", "<", "<=", ">", ">=",
            "&&", "||", "&", "|", "=", "sqrt", "max", "min", "round", "clamp", "$", "#", " ", "\n", "٣", "abc_1",
        };
        var variables = new { x = 1.5, y = 2.0 };

        var failures = RunGuarded(() =>
        {
            var random = new Random(20260702);
            var found = new List<string>();
            for (var i = 0; i < 30_000; i++)
            {
                var text = string.Concat(Enumerable.Range(0, random.Next(0, 14)).Select(_ => pieces[random.Next(pieces.Length)]));
                var error = Capture(() => Engine.Default.Evaluate(text, variables));
                if (error is not null and not ExpressionException)
                {
                    found.Add($"'{text}' -> {error.GetType().Name}: {error.Message}");
                }
            }

            return found;
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void ValidRandomExpressionsRoundTripThroughThePrinter()
    {
        var failures = RunGuarded(() =>
        {
            var random = new Random(7);
            var found = new List<string>();
            for (var i = 0; i < 3_000; i++)
            {
                var text = RandomExpression(random, depth: 4);
                var first = Engine.Default.Parse(text);
                var printed = first.Root.ToString();
                var second = Engine.Default.Parse(printed);

                if (second.Root.ToString() != printed)
                {
                    found.Add($"'{text}' -> '{printed}' -> '{second.Root}'");
                }
            }

            return found;
        });

        Assert.Empty(failures);
    }

    private static string RandomExpression(Random random, int depth)
    {
        if (depth == 0 || random.Next(4) == 0)
        {
            return random.Next(5) switch
            {
                0 => "x",
                1 => "true",
                2 => random.Next(0, 100).ToString(),
                3 => (random.NextDouble() * 10).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                _ => "y",
            };
        }

        string Sub() => RandomExpression(random, depth - 1);
        string[] binary = { "+", "-", "*", "/", "%", "^", "==", "!=", "<", "<=", ">", ">=", "&&", "||" };
        return random.Next(6) switch
        {
            0 => $"({Sub()})",
            1 => $"-{Sub()}",
            2 => $"!{Sub()}",
            3 => $"{Sub()} ? {Sub()} : {Sub()}",
            4 => $"max({Sub()}, {Sub()})",
            _ => $"{Sub()} {binary[random.Next(binary.Length)]} {Sub()}",
        };
    }
}
