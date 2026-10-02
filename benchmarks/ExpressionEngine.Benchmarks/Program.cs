using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ExpressionEngine;

BenchmarkRunner.Run<EvaluationBenchmarks>(args: args);

/// <summary>
/// Run with: dotnet run -c Release --project benchmarks/ExpressionEngine.Benchmarks -- --filter "*"
/// </summary>
[MemoryDiagnoser]
public class EvaluationBenchmarks
{
    private const string Text = "price * (1 + taxRate) - discount + max(quantity, 1) * 0.5";

    private readonly Engine _engine = Engine.Default;
    private Expression _parsed = null!;
    private Variables _variables = null!;
    private (double price, double taxRate, double discount, double quantity) _inputs;

    // Note: there is deliberately no "native C#" baseline. The JIT folds such a formula to almost nothing,
    // which would only produce a misleading ratio column.

    [GlobalSetup]
    public void Setup()
    {
        _parsed = _engine.Parse(Text);
        _inputs = (100, 0.2, 5, 3);
        _variables = new Variables
        {
            ["price"] = _inputs.price,
            ["taxRate"] = _inputs.taxRate,
            ["discount"] = _inputs.discount,
            ["quantity"] = _inputs.quantity,
        };
    }

    /// <summary>Parse once, evaluate many times with a Variables set (the recommended pattern).</summary>
    [Benchmark(Baseline = true)]
    public double EvaluateParsed_Variables() => _parsed.EvaluateNumber(_variables);

    /// <summary>Evaluate with an anonymous object (convenient; uses cached reflection).</summary>
    [Benchmark]
    public double EvaluateParsed_AnonymousObject() =>
        _parsed.EvaluateNumber(new { price = 100.0, taxRate = 0.2, discount = 5.0, quantity = 3.0 });

    /// <summary>Parse and evaluate on every call (what to avoid in hot paths).</summary>
    [Benchmark]
    public double ParseAndEvaluateEveryTime() => _engine.EvaluateNumber(Text, _variables);

    /// <summary>Parsing alone.</summary>
    [Benchmark]
    public Expression ParseOnly() => _engine.Parse(Text);
}
