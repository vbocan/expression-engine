namespace ExpressionEngine.Playground;

/// <summary>Formats examples for the terminal: the index, and the detail shown by <c>:run</c>.</summary>
internal static class ExampleView
{
    private const int ExpressionColumn = 48;

    /// <summary>Categories whose name contains <paramref name="filter"/>, or all of them when it is empty.</summary>
    public static IReadOnlyList<string> CategoriesMatching(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
            ? ExampleCatalog.Categories
            : ExampleCatalog.Categories
                .Where(c => c.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();

    /// <summary>The numbered index of examples, grouped by category.</summary>
    public static IReadOnlyList<string> Index(Style style, string? filter = null)
    {
        var lines = new List<string>();
        foreach (var category in CategoriesMatching(filter))
        {
            var examples = ExampleCatalog.InCategory(category).ToArray();
            lines.Add(string.Empty);
            lines.Add(style.Heading(category) + style.Dim($"  ({examples.Length})"));
            foreach (var example in examples)
            {
                var number = style.Accent(example.Number.ToString().PadLeft(3));
                lines.Add($"  {number}  {example.Expression.PadRight(ExpressionColumn)}  {style.Dim(example.Title)}");
            }
        }

        return lines;
    }

    public static string VariablesText(Example example) =>
        string.Join(", ", example.Variables.Select(v => $"{v.Name} = {v.Value}"));

    /// <summary>One example, run: its expression, variables, result and note.</summary>
    public static IReadOnlyList<string> Detail(
        Example example,
        ExampleOutcome outcome,
        Style style,
        bool defaultOptions,
        int width)
    {
        var lines = new List<string>
        {
            string.Empty,
            $"{style.Accent("#" + example.Number)}  {style.Bold(example.Title)}  {style.Dim("[" + example.Category + "]")}",
            "    " + style.Code(example.Expression),
        };

        if (example.Variables.Count > 0)
        {
            lines.Add("    " + style.Dim("with " + VariablesText(example)));
        }

        if (outcome.Succeeded)
        {
            lines.Add("    " + style.Good("= " + outcome.Describe()));
        }
        else
        {
            var error = outcome.Error!;
            lines.Add("    " + style.Bad("error: " + error.Message));
            if (error.Position >= 0)
            {
                lines.Add("    " + new string(' ', error.Position) + style.Bad(new string('^', Math.Max(1, error.Length))));
            }
        }

        if (!defaultOptions && !outcome.Matches(example))
        {
            var documented = example.ExpectsError ? "an error (\"" + example.ErrorContains + "\")" : example.Result!;
            lines.Add("    " + style.Dim("with the default options this gives: " + documented));
        }

        if (example.Note.Length > 0)
        {
            foreach (var line in ManualViewer.Wrap(example.Note, "    ", "    ", width))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
