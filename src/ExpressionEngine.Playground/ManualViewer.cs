using System.Text;
using System.Text.RegularExpressions;

namespace ExpressionEngine.Playground;

/// <summary>
/// Reads the Markdown manual that is compiled into the program and renders it for a terminal:
/// word-wrapped paragraphs, aligned tables, colored headings. Sections are the manual's "## " headings.
/// </summary>
internal sealed partial class ManualViewer
{
    internal sealed record Section(int Number, string Title, IReadOnlyList<string> Lines);

    private readonly List<string> _header = new();
    private readonly List<Section> _sections = new();

    public IReadOnlyList<string> Header => _header;

    public IReadOnlyList<Section> Sections => _sections;

    public static ManualViewer Load()
    {
        using var stream = typeof(ManualViewer).Assembly.GetManifestResourceStream("MANUAL.md")
            ?? throw new InvalidOperationException("The manual is not embedded in this build.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static ManualViewer Parse(string markdown)
    {
        var viewer = new ManualViewer();
        List<string>? current = null;
        var title = string.Empty;
        var inFence = false;

        void Close()
        {
            if (current is not null)
            {
                viewer._sections.Add(new Section(viewer._sections.Count + 1, title, current));
            }
        }

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }

            if (!inFence && raw.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                title = raw[3..].Trim();
                current = new List<string> { raw };
                continue;
            }

            (current ?? viewer._header).Add(raw);
        }

        Close();
        return viewer;
    }

    /// <summary>Sections addressed by number, exact title, or part of a title (case-insensitive).</summary>
    public IReadOnlyList<Section> Match(string query)
    {
        query = query.Trim();
        if (int.TryParse(query, out var number))
        {
            return _sections.Where(s => s.Number == number).ToArray();
        }

        var exact = _sections.Where(s => s.Title.Equals(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return exact.Length > 0
            ? exact
            : _sections.Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    /// <summary>Lines of the manual containing <paramref name="text"/>, with the section they belong to.</summary>
    public IReadOnlyList<(Section Section, string Line)> Search(string text, int limit = 60)
    {
        var hits = new List<(Section, string)>();
        foreach (var section in _sections)
        {
            foreach (var line in section.Lines)
            {
                if (line.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add((section, line.Trim()));
                    if (hits.Count >= limit)
                    {
                        return hits;
                    }
                }
            }
        }

        return hits;
    }

    public IReadOnlyList<string> TableOfContents(Style style)
    {
        var lines = new List<string>();
        var title = _header.FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? "Manual";
        lines.Add(style.Heading(title));
        lines.Add(string.Empty);
        foreach (var section in _sections)
        {
            lines.Add($"  {style.Accent(section.Number.ToString().PadLeft(2))}  {section.Title}");
        }

        lines.Add(string.Empty);
        lines.Add(style.Dim("  Read a section with  :manual 3  or  :manual functions.   :manual all  prints everything.   :search text  finds text."));
        return lines;
    }

    public IReadOnlyList<string> Render(Section section, Style style, int width) => Format(section.Lines, style, width);

    public IReadOnlyList<string> RenderAll(Style style, int width)
    {
        var lines = new List<string>(Format(_header, style, width));
        foreach (var section in _sections)
        {
            lines.Add(string.Empty);
            lines.AddRange(Format(section.Lines, style, width));
        }

        return lines;
    }

    // ---- Markdown to terminal text -------------------------------------------------------------------------------

    [GeneratedRegex(@"^(\s*)([-*]|\d+[.)])\s+(.*)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"(?<code>`[^`]+`)|(?<bold>\*\*[^*]+\*\*)|(?<link>\[[^\]]+\]\([^)]+\))")]
    private static partial Regex InlineMarkup();

    [GeneratedRegex(@"^:?-{3,}:?$")]
    private static partial Regex TableRule();

    private const char NonBreakingSpace = ' ';
    private const int MaxUnbreakableCode = 36;

    internal static IReadOnlyList<string> Format(IReadOnlyList<string> source, Style style, int width)
    {
        var output = new List<string>();
        var paragraph = new List<string>();
        var table = new List<string>();
        var inFence = false;

        void Blank()
        {
            if (output.Count > 0 && output[^1].Length > 0)
            {
                output.Add(string.Empty);
            }
        }

        void FlushParagraph()
        {
            if (paragraph.Count > 0)
            {
                output.AddRange(Wrap(Inline(string.Join(" ", paragraph.Select(p => p.Trim())), style), string.Empty, string.Empty, width));
                paragraph.Clear();
            }
        }

        void FlushTable()
        {
            if (table.Count > 0)
            {
                output.AddRange(RenderTable(table, style));
                table.Clear();
            }
        }

        foreach (var line in source)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushTable();
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                output.Add("    " + style.Code(line));
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith('|'))
            {
                FlushParagraph();
                table.Add(trimmed);
                continue;
            }

            FlushTable();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                Blank();
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushParagraph();
                var text = line[2..].Trim();
                output.Add(style.Heading(text));
                output.Add(style.Dim(new string('═', Math.Min(width, text.Length))));
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph();
                Blank();
                var text = line[3..].Trim();
                output.Add(style.Heading(text.ToUpperInvariant()));
                output.Add(style.Dim(new string('─', Math.Min(width, text.Length))));
            }
            else if (line.StartsWith("###", StringComparison.Ordinal))
            {
                FlushParagraph();
                Blank();
                output.Add(style.Bold(line.TrimStart('#').Trim()));
            }
            else if (trimmed == "---")
            {
                FlushParagraph();
                output.Add(style.Dim(new string('─', Math.Min(width, 60))));
            }
            else if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushParagraph();
                output.AddRange(Wrap(Inline(trimmed[2..], style), "  │ ", "  │ ", width));
            }
            else if (ListItem().Match(line) is { Success: true } item)
            {
                FlushParagraph();
                var indent = item.Groups[1].Value;
                var marker = item.Groups[2].Value is "-" or "*" ? "•" : item.Groups[2].Value;
                output.AddRange(Wrap(
                    Inline(item.Groups[3].Value, style),
                    indent + marker + " ",
                    indent + new string(' ', marker.Length + 1),
                    width));
            }
            else
            {
                paragraph.Add(line);
            }
        }

        FlushParagraph();
        FlushTable();
        return output;
    }

    /// <summary>Applies bold/code styling and drops link syntax. Spaces inside code spans become non-breaking so wrapping keeps them whole.</summary>
    private static string Inline(string text, Style style) =>
        InlineMarkup().Replace(text.Replace("\\|", "|"), match =>
        {
            if (match.Groups["code"].Success)
            {
                // Keep short code spans in one piece when wrapping; a long one (a whole command line) must be allowed to break.
                var code = match.Value[1..^1];
                return style.Code(code.Length <= MaxUnbreakableCode ? code.Replace(' ', NonBreakingSpace) : code);
            }

            if (match.Groups["bold"].Success)
            {
                return style.Bold(match.Value[2..^2]);
            }

            return match.Value[1..match.Value.IndexOf(']')];
        });

    internal static IEnumerable<string> Wrap(string text, string firstPrefix, string restPrefix, int width)
    {
        var limit = Math.Max(20, width);
        var line = new StringBuilder(firstPrefix);
        var lineHasWord = false;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var needed = Style.VisibleLength(word) + (lineHasWord ? 1 : 0);
            if (lineHasWord && Style.VisibleLength(line.ToString()) + needed > limit)
            {
                yield return line.ToString().Replace(NonBreakingSpace, ' ');
                line.Clear().Append(restPrefix);
                lineHasWord = false;
                needed = Style.VisibleLength(word);
            }

            if (lineHasWord)
            {
                line.Append(' ');
            }

            line.Append(word);
            lineHasWord = true;
        }

        yield return line.ToString().Replace(NonBreakingSpace, ' ');
    }

    private static IEnumerable<string> RenderTable(IReadOnlyList<string> rows, Style style)
    {
        var parsed = new List<List<string>>();
        foreach (var row in rows)
        {
            var cells = row.Replace("\\|", "\u0001").Trim('|').Split('|')
                .Select(c => c.Trim().Replace('\u0001', '|'))
                .ToList();
            if (cells.All(c => TableRule().IsMatch(c)))
            {
                continue;
            }

            parsed.Add(cells);
        }

        var columns = parsed.Max(r => r.Count);
        var widths = new int[columns];
        var styled = parsed.Select(r => r.Select(c => Inline(c, style).Replace(NonBreakingSpace, ' ')).ToList()).ToList();
        foreach (var row in styled)
        {
            for (var i = 0; i < row.Count; i++)
            {
                widths[i] = Math.Max(widths[i], Style.VisibleLength(row[i]));
            }
        }

        string Line(List<string> row, bool header)
        {
            var cells = new List<string>();
            for (var i = 0; i < columns; i++)
            {
                var text = i < row.Count ? row[i] : string.Empty;
                var padded = text + new string(' ', widths[i] - Style.VisibleLength(text));
                cells.Add(header ? style.Bold(padded) : padded);
            }

            return "  " + string.Join(style.Dim("  │  "), cells).TrimEnd();
        }

        for (var r = 0; r < styled.Count; r++)
        {
            yield return Line(styled[r], header: r == 0);
            if (r == 0)
            {
                yield return "  " + style.Dim(string.Join("──┼──", widths.Select(w => new string('─', w))));
            }
        }
    }
}
