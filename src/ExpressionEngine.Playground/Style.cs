using System.Text.RegularExpressions;

namespace ExpressionEngine.Playground;

/// <summary>Terminal colors. A disabled style returns text unchanged, so output stays clean when piped.</summary>
internal sealed partial class Style
{
    public static readonly Style Plain = new(false);

    private readonly bool _enabled;

    public Style(bool enabled) => _enabled = enabled;

    public bool Enabled => _enabled;

    /// <summary>Colors only when writing to a real terminal that has not opted out (https://no-color.org).</summary>
    public static Style Detect()
    {
        var disabled = Console.IsOutputRedirected
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
            || Environment.GetEnvironmentVariable("TERM") == "dumb";
        return new Style(!disabled);
    }

    public string Bold(string text) => Wrap(text, "1");

    public string Dim(string text) => Wrap(text, "2");

    public string Heading(string text) => Wrap(text, "1;36");

    public string Accent(string text) => Wrap(text, "36");

    public string Good(string text) => Wrap(text, "32");

    public string Bad(string text) => Wrap(text, "31");

    public string Code(string text) => Wrap(text, "33");

    private string Wrap(string text, string code) => _enabled && text.Length > 0 ? $"\u001b[{code}m{text}\u001b[0m" : text;

    /// <summary>Number of characters a string occupies on screen (escape sequences take no space).</summary>
    public static int VisibleLength(string text) => AnsiPattern().Replace(text, string.Empty).Length;

    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex AnsiPattern();
}

/// <summary>Terminal dimensions with sane fallbacks when there is no terminal (pipes, CI).</summary>
internal static class Terminal
{
    public static int Width => Read(() => Console.WindowWidth, 100);

    public static int Height => Read(() => Console.WindowHeight, 30);

    private static int Read(Func<int> read, int fallback)
    {
        try
        {
            var value = read();
            return value > 0 ? value : fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
    }
}
