using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

/// <summary>Numbers quoted in the docs must match the code, so the docs cannot quietly go stale.</summary>
public class DocumentationConsistencyTests
{
    private static string RepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExpressionEngine.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }

    [Fact]
    public void ReadmeQuotesTheRealExampleCount() =>
        Assert.Contains($"**{ExampleCatalog.All.Count} runnable examples**", RepositoryFile("README.md"));

    [Fact]
    public void ReadmeAndChangelogQuoteTheRealFunctionCount()
    {
        var count = Engine.Default.Functions.Count;

        Assert.Contains($"{count} built-in math functions", RepositoryFile("README.md"));
        Assert.Contains($"{count} functions and the constants", RepositoryFile("CHANGELOG.md"));
    }

    [Fact]
    public void ChangelogQuotesTheRealExampleCount() =>
        Assert.Contains($"{ExampleCatalog.All.Count} runnable", RepositoryFile("CHANGELOG.md"));

    [Fact]
    public void ReadmeSampleSessionMatchesWhatTheReplPrints()
    {
        var session = new Session();
        var output = new StringWriter();
        var repl = new Repl(session, output, Style.Plain, ManualViewer.Load(), "test", false, null);
        foreach (var line in new[] { "r = 10", "2 * pi * r", ":def hyp(a, b) = sqrt(a^2 + b^2)", "hyp(3, 4)", "5 + $", ":ast 1 + 2 * r" })
        {
            repl.Execute(line);
        }

        var text = output.ToString().Replace("\r\n", "\n");
        var readme = RepositoryFile("README.md").Replace("\r\n", "\n");

        foreach (var expected in new[]
        {
            "r = 10\n", "62.83185307179586\n", "Defined hyp(a, b). Try it: hyp(1, 2)\n", "5\n",
            "error: Unexpected character '$' (at column 5)\n  5 + $\n      ^\n",
            "normalized:  1 + 2 * r\nvariables:   r\nfunctions:   (none)\n\nBinary Add  (+)\n├─ Number 1\n└─ Binary Multiply  (*)\n   ├─ Number 2\n   └─ Variable r\n",
        })
        {
            Assert.Contains(expected, text);
        }

        Assert.Contains("Defined hyp(a, b). Try it: hyp(1, 2)", readme);
        Assert.Contains("└─ Binary Multiply  (*)\n   ├─ Number 2\n   └─ Variable r", readme);
    }

    [Fact]
    public void ReadmeSnippetsRun()
    {
        Expression price = Engine.Default.Parse("base * (1 + taxRate) - discount");
        var vars = new Variables { ["base"] = 100, ["taxRate"] = 0.2, ["discount"] = 5 };
        Assert.Equal(115, price.EvaluateNumber(vars), precision: 10);
        Assert.Equal(new[] { "base", "taxRate", "discount" }, price.Variables);

        var engine = new EngineBuilder()
            .AddMathFunctions().AddMathConstants()
            .AddFunction("vat", amount => amount * 0.19)
            .Build();
        Assert.Equal(38, engine.EvaluateNumber("vat(price)", new { price = 200 }), precision: 10);

        Assert.False(engine.TryParse("2 * (3 + $)", out _, out var error));
        Assert.Equal("Unexpected character '$' (at column 10)", error.Message);
    }

    [Fact]
    public void NoDocumentMentionsPackagesThatDoNotExist()
    {
        foreach (var file in new[] { "README.md", "docs/MANUAL.md", "CONTRIBUTING.md" })
        {
            var text = RepositoryFile(file);
            Assert.DoesNotContain("dotnet add package", text);
            Assert.DoesNotContain("nuget.org", text);
        }
    }
}
