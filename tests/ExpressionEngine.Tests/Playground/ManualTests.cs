using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

public class ManualTests
{
    private static readonly ManualViewer Manual = ManualViewer.Load();

    private static string Plain(IEnumerable<string> lines) => string.Join("\n", lines);

    [Fact]
    public void ManualIsEmbeddedAndHasTheExpectedSections()
    {
        var titles = Manual.Sections.Select(s => s.Title).ToArray();

        Assert.True(titles.Length >= 12);
        foreach (var expected in new[]
        {
            "Introduction", "Getting the engine into your project", "Quick start", "The expression language",
            "Built-in constants and functions", "Providing variables", "Custom functions and constants",
            "Configuring the engine", "Error handling", "The syntax tree", "Untrusted input", "The playground",
        })
        {
            Assert.Contains(expected, titles);
        }

        Assert.Equal(Enumerable.Range(1, titles.Length), Manual.Sections.Select(s => s.Number));
    }

    [Fact]
    public void FencedCodeIsNeverMistakenForASectionHeading()
    {
        var viewer = ManualViewer.Parse("# Title\n\nintro\n\n## One\n\n```bash\n## not a heading\n```\n\n## Two\n\ntext\n");

        Assert.Equal(new[] { "One", "Two" }, viewer.Sections.Select(s => s.Title));
        Assert.Contains("## not a heading", viewer.Sections[0].Lines);
    }

    [Theory]
    [InlineData("3", "Quick start")]
    [InlineData("quick start", "Quick start")]
    [InlineData("QUICK", "Quick start")]
    [InlineData("untrusted", "Untrusted input")]
    public void SectionsCanBeFoundByNumberTitleOrPartOfTitle(string query, string title)
    {
        var match = Assert.Single(Manual.Match(query));

        Assert.Equal(title, match.Title);
    }

    [Fact]
    public void AmbiguousAndUnknownQueriesAreReported()
    {
        Assert.True(Manual.Match("ing").Count > 1);
        Assert.Empty(Manual.Match("zzz-not-there"));
        Assert.Empty(Manual.Match("999"));
    }

    [Fact]
    public void RenderedManualLeavesNoMarkdownNoise()
    {
        var text = Plain(Manual.RenderAll(Style.Plain, 100));

        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("](", text);
        Assert.DoesNotContain("`", text.Replace("```", string.Empty).Replace("`ExpressionEngine`", string.Empty), StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?m)^\s*\|[\s:|-]*-{3}", text);   // markdown table rules
        Assert.DoesNotContain(" ", text);
    }

    [Fact]
    public void ParagraphsAreWrappedToTheRequestedWidth()
    {
        var lines = Manual.RenderAll(Style.Plain, 80);

        // Prose lines obey the width; code blocks and tables are shown as written.
        var prose = lines.Where(l => !l.StartsWith("    ") && !l.Contains(" │ ") && !l.Contains('─'));
        Assert.All(prose, l => Assert.True(l.Length <= 80, $"too wide ({l.Length}): {l}"));
    }

    [Fact]
    public void TablesAreAlignedIntoColumns()
    {
        var lines = ManualViewer.Format(new[] { "| Name | Value |", "| --- | --- |", "| `pi` | 3.14 |", "| tau | 6.28 |" }, Style.Plain, 80);

        Assert.Equal(4, lines.Count);   // header, rule, two rows (the markdown separator row is gone)
        var columns = lines.Where(l => l.Contains('│')).Select(l => l.IndexOf('│')).Distinct();
        Assert.Single(columns);
        Assert.Contains("pi", lines[2]);
    }

    [Fact]
    public void ListsHeadingsCodeAndQuotesAreFormatted()
    {
        var lines = ManualViewer.Format(
            new[] { "## Heading", "", "- first **bold** item", "- second [link](#x) item", "", "```", "code line", "```", "", "> quoted" },
            Style.Plain,
            80);
        var text = Plain(lines);

        Assert.Contains("HEADING", text);
        Assert.Contains("• first bold item", text);
        Assert.Contains("• second link item", text);
        Assert.Contains("    code line", text);
        Assert.Contains("│ quoted", text);
    }

    [Fact]
    public void ColorsAreAddedOnlyWhenEnabled()
    {
        var colored = Plain(ManualViewer.Format(new[] { "## Heading", "some `code` here" }, new Style(true), 80));
        var plain = Plain(ManualViewer.Format(new[] { "## Heading", "some `code` here" }, Style.Plain, 80));

        Assert.Contains("\u001b[", colored);
        Assert.DoesNotContain("\u001b[", plain);
        Assert.Equal(plain.Length, Style.VisibleLength(colored));
    }

    [Fact]
    public void TableOfContentsListsEverySection()
    {
        var toc = Plain(Manual.TableOfContents(Style.Plain));

        foreach (var section in Manual.Sections)
        {
            Assert.Contains(section.Title, toc);
        }

        Assert.Contains(":manual", toc);
    }

    [Fact]
    public void SearchFindsLinesAndNamesTheirSection()
    {
        var hits = Manual.Search("tolerance");

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Contains("tolerance", h.Line, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Manual.Search("zzz-not-there"));
    }

    [Fact]
    public void ManualMentionsEveryBuiltInFunctionAndConstant()
    {
        var text = string.Join("\n", Manual.Sections.SelectMany(s => s.Lines));

        foreach (var function in Engine.Default.Functions)
        {
            Assert.Contains(function.Name, text);
        }

        foreach (var constant in Engine.Default.Constants.Keys)
        {
            Assert.Contains($"`{constant}`", text);
        }
    }

    [Fact]
    public void ManualDocumentsEveryPlaygroundCommand()
    {
        var playground = string.Join("\n", Manual.Match("The playground").Single().Lines);

        foreach (var command in new[] { ":examples", ":run", ":edit", ":manual", ":search", ":ast", ":vars", ":unset", ":clear", ":funcs", ":def", ":const", ":set", ":reset", ":help", ":quit" })
        {
            Assert.Contains(command, playground);
        }
    }

    [Fact]
    public void ManualCoversEveryPublicBuilderMethod()
    {
        var text = string.Join("\n", Manual.Sections.SelectMany(s => s.Lines));

        foreach (var method in new[] { "AddMathFunctions", "AddMathConstants", "AddFunction", "AddConstant", "CaseInsensitive", "AllowNonFiniteResults", "WithMaxDepth", "WithMaxLength", "CreateVariables", "TryParse", "TryEvaluate", "IVariableSource", "INodeVisitor" })
        {
            Assert.Contains(method, text);
        }
    }
}
