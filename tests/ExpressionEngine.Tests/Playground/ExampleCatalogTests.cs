using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

public class ExampleCatalogTests
{
    public static IEnumerable<object[]> AllExamples() =>
        ExampleCatalog.All.Select(e => new object[] { e.Number, e.Title });

    /// <summary>The heart of the guarantee: whatever the playground documents as a result is what the engine really produces.</summary>
    [Theory]
    [MemberData(nameof(AllExamples))]
    public void EveryExampleProducesItsDocumentedOutcome(int number, string title)
    {
        var example = ExampleCatalog.All.Single(e => e.Number == number);

        var outcome = ExampleRunner.Run(Engine.Default, example);

        Assert.True(
            outcome.Matches(example),
            $"Example #{number} '{title}' ({example.Expression}): documented " +
            $"{(example.ExpectsError ? "error containing '" + example.ErrorContains + "'" : "'" + example.Result + "'")} but got '{outcome.Describe()}'");
    }

    [Fact]
    public void ThereAreALotOfExamplesInSeveralCategories()
    {
        Assert.True(ExampleCatalog.All.Count >= 80, $"only {ExampleCatalog.All.Count} examples");
        Assert.True(ExampleCatalog.Categories.Count >= 8);
    }

    [Fact]
    public void NumbersAreSequentialFromOne() =>
        Assert.Equal(Enumerable.Range(1, ExampleCatalog.All.Count), ExampleCatalog.All.Select(e => e.Number));

    [Fact]
    public void EveryExampleIsDescribed()
    {
        foreach (var example in ExampleCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(example.Title), $"#{example.Number} has no title");
            Assert.False(string.IsNullOrWhiteSpace(example.Expression), $"#{example.Number} has no expression");
            Assert.False(string.IsNullOrWhiteSpace(example.Category), $"#{example.Number} has no category");
        }
    }

    [Fact]
    public void SuccessfulExamplesSupplyEveryVariableTheyUse()
    {
        foreach (var example in ExampleCatalog.All.Where(e => !e.ExpectsError))
        {
            var needed = Engine.Default.Parse(example.Expression).Variables;
            var supplied = example.Variables.Select(v => v.Name).ToHashSet();

            Assert.True(
                needed.All(supplied.Contains),
                $"#{example.Number} '{example.Title}' uses {string.Join(", ", needed.Except(supplied))} without defining it");
        }
    }

    [Fact]
    public void ExamplesCoverSuccessAndFailure()
    {
        Assert.Contains(ExampleCatalog.All, e => e.ExpectsError);
        Assert.Contains(ExampleCatalog.All, e => !e.ExpectsError && e.Variables.Count > 0);
        Assert.Contains(ExampleCatalog.All, e => e.Result == "true");
    }

    [Fact]
    public void ExamplesThatDemonstrateOptionsReallyChangeWithThem()
    {
        var lenient = new EngineBuilder().AddMathFunctions().AddMathConstants().AllowNonFiniteResults().Build();
        var insensitive = new EngineBuilder().AddMathFunctions().AddMathConstants().CaseInsensitive().Build();

        var divide = ExampleCatalog.All.Single(e => e.Expression == "1 / 0");
        var sqrt = ExampleCatalog.All.Single(e => e.Expression == "SQRT(16)");

        Assert.Equal("Infinity", ExampleRunner.Run(lenient, divide).Describe());
        Assert.Equal("4", ExampleRunner.Run(insensitive, sqrt).Describe());
    }

    [Fact]
    public void IndexListsEveryExampleOnce()
    {
        var lines = ExampleView.Index(Style.Plain);

        foreach (var example in ExampleCatalog.All)
        {
            Assert.Single(lines, l => l.Contains(example.Expression) && l.TrimStart().StartsWith(example.Number.ToString()));
        }
    }

    [Fact]
    public void IndexCanBeFilteredByCategory()
    {
        var lines = ExampleView.Index(Style.Plain, "arith");

        Assert.Contains(lines, l => l.Contains("Arithmetic"));
        Assert.DoesNotContain(lines, l => l.Contains("Booleans"));
        Assert.Empty(ExampleView.Index(Style.Plain, "no such category"));
    }

    [Fact]
    public void DetailShowsExpressionVariablesResultAndNote()
    {
        var example = ExampleCatalog.All.Single(e => e.Title == "Sales tax");
        var outcome = ExampleRunner.Run(Engine.Default, example);

        var text = string.Join("\n", ExampleView.Detail(example, outcome, Style.Plain, defaultOptions: true, width: 80));

        Assert.Contains("price * (1 + taxRate)", text);
        Assert.Contains("with price = 100, taxRate = 0.2", text);
        Assert.Contains("= 120", text);
        Assert.Contains("Variables are supplied", text);
    }

    [Fact]
    public void DetailOfAnErrorPointsAtTheProblemAndExplainsDifferencesFromTheDefaults()
    {
        var example = ExampleCatalog.All.Single(e => e.Expression == "2 $ 3");
        var failure = string.Join("\n", ExampleView.Detail(example, ExampleRunner.Run(Engine.Default, example), Style.Plain, true, 80));
        Assert.Contains("error: Unexpected character '$'", failure);
        Assert.Contains("      ^", failure);   // caret under column 3 (4 indent + 2)

        var divide = ExampleCatalog.All.Single(e => e.Expression == "1 / 0");
        var lenient = new EngineBuilder().AllowNonFiniteResults().Build();
        var differs = string.Join("\n", ExampleView.Detail(divide, ExampleRunner.Run(lenient, divide), Style.Plain, defaultOptions: false, width: 80));
        Assert.Contains("= Infinity", differs);
        Assert.Contains("with the default options this gives: an error", differs);
    }
}
