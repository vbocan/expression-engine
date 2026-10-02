using System.Globalization;
using System.Reflection;
using System.Text;
using ExpressionEngine;

namespace ExpressionEngine.Playground;

internal static class Program
{
    private const string Usage = """
        expr - the ExpressionEngine playground

        Usage:
          expr                                   start the interactive playground (REPL)
          expr <expression> [-v name=value]...   evaluate one expression and exit
          expr --manual [section]                print the manual (or one section)
          expr --examples [category]             list the built-in examples

        Options:
          -v, --var name=value   define a variable (a number, or true/false); repeatable
          -i, --ignore-case      match function, constant and variable names case-insensitively
          -h, --help             show this help
              --version          show the version
              --                 end of options (for expressions that start with '-')

        Examples:
          expr "2 * pi * r" -v r=10
          expr "price * (1 + tax) > 100 ? 1 : 0" -v price=90 -v tax=0.2
          expr --manual functions

        In Docker:  docker run -it --rm expression-engine
        """;

    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // No console attached; output encoding is irrelevant.
        }

        string? expression = null;
        string? manualSection = null;
        string? examplesFilter = null;
        var showManual = false;
        var showExamples = false;
        var ignoreCase = false;
        var optionsEnded = false;
        var variables = new List<(string Name, Value Value)>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!optionsEnded && arg == "--")
            {
                optionsEnded = true;   // lets an expression start with '-':  expr -- "-(1 + 2)"
                continue;
            }

            if (!optionsEnded && arg.StartsWith('-') && arg.Length > 1 && !char.IsDigit(arg[1]) && arg[1] != '.')
            {
                switch (arg)
                {
                    case "-h" or "--help":
                        Console.WriteLine(Usage);
                        return 0;
                    case "--version":
                        Console.WriteLine(Version());
                        return 0;
                    case "-i" or "--ignore-case":
                        ignoreCase = true;
                        break;
                    case "--manual":
                        showManual = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            manualSection = args[++i];
                        }

                        break;
                    case "--examples":
                        showExamples = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        {
                            examplesFilter = args[++i];
                        }

                        break;
                    case "-v" or "--var":
                        if (++i >= args.Length)
                        {
                            return Fail("Option -v needs a value like name=value");
                        }

                        if (!TryParseAssignment(args[i], out var name, out var value, out var problem))
                        {
                            return Fail(problem);
                        }

                        variables.Add((name, value));
                        break;
                    default:
                        return Fail($"Unknown option '{arg}'. Try --help.");
                }

                continue;
            }

            if (expression is not null)
            {
                return Fail("Only one expression can be given. Quote it:  expr \"1 + 2\"");
            }

            expression = arg;
        }

        var style = Style.Detect();

        if (showManual)
        {
            return PrintManual(manualSection, style);
        }

        if (showExamples)
        {
            var lines = ExampleView.Index(style, examplesFilter);
            if (lines.Count == 0)
            {
                return Fail($"No category matches '{examplesFilter}'. Categories: {string.Join(", ", ExampleCatalog.Categories)}");
            }

            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }

            return 0;
        }

        var session = new Session(ignoreCase);
        foreach (var (name, value) in variables)
        {
            session.Variables[name] = value;
        }

        return expression is null ? RunPlayground(session, style) : EvaluateOnce(session, expression);
    }

    private static int EvaluateOnce(Session session, string expression)
    {
        try
        {
            Console.WriteLine(session.Evaluate(expression));
            return 0;
        }
        catch (ExpressionException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            if (ex.ExpressionText is { } text && ex.Position >= 0 && !text.Contains('\n'))
            {
                Console.Error.WriteLine($"  {text}");
                Console.Error.WriteLine($"  {new string(' ', ex.Position)}{new string('^', Math.Max(1, ex.Length))}");
            }

            return 1;
        }
    }

    private static int PrintManual(string? section, Style style)
    {
        var manual = ManualViewer.Load();
        var width = Math.Min(Terminal.Width - 2, 100);

        if (section is null)
        {
            foreach (var line in manual.RenderAll(style, width))
            {
                Console.WriteLine(line);
            }

            return 0;
        }

        var matches = manual.Match(section);
        if (matches.Count == 0)
        {
            return Fail($"No manual section matches '{section}'. Sections: {string.Join(", ", manual.Sections.Select(s => s.Title))}");
        }

        foreach (var match in matches)
        {
            foreach (var line in manual.Render(match, style, width))
            {
                Console.WriteLine(line);
            }
        }

        return 0;
    }

    private static int RunPlayground(Session session, Style style)
    {
        var interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        var linesRead = 0;

        var repl = new Repl(session, Console.Out, style, ManualViewer.Load(), Version(), interactive, interactive ? WaitForMore : null);
        if (interactive)
        {
            var editor = new LineEditor(() => Console.ReadKey(intercept: true), Console.Out, repl.Complete);
            repl.ReadLine = (prompt, initial) =>
            {
                linesRead++;
                return editor.ReadLine(prompt, initial);
            };
        }
        else
        {
            // Piped input: echo each line after the prompt so a transcript reads like a session.
            repl.ReadLine = (prompt, _) =>
            {
                Console.Out.Write(prompt);
                var line = Console.In.ReadLine();
                Console.Out.WriteLine(line ?? string.Empty);
                if (line is not null)
                {
                    linesRead++;
                }

                return line;
            };
        }

        var exitCode = repl.Run(showBanner: interactive);
        if (!interactive && linesRead == 0)
        {
            Console.Error.WriteLine("No interactive terminal is attached, so there is nothing to type into.");
            Console.Error.WriteLine("Docker needs the -it flags:   docker run -it --rm expression-engine");
            Console.Error.WriteLine("Or evaluate one expression:   docker run --rm expression-engine \"2 * (3 + 4)\"");
        }

        return exitCode;
    }

    /// <summary>The pager's "-- more --" prompt. Returns false when the reader wants to stop.</summary>
    private static bool WaitForMore()
    {
        Console.Out.Write("\u001b[7m -- more --  Enter or Space: continue   q: stop \u001b[0m");
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            {
                Console.Out.Write("\r\u001b[K");
                return true;
            }

            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape || (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0))
            {
                Console.Out.Write("\r\u001b[K");
                return false;
            }
        }
    }

    private static bool TryParseAssignment(string text, out string name, out Value value, out string problem)
    {
        name = string.Empty;
        value = default;
        problem = $"'{text}' is not in the form name=value";

        var separator = text.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        name = text[..separator].Trim();
        var raw = text[(separator + 1)..].Trim();
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
        }
        else if (raw.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
        }
        else if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            value = number;
        }
        else
        {
            problem = $"The value of '{name}' must be a number or true/false, but was '{raw}'";
            return false;
        }

        return true;
    }

    private static string Version() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 2;
    }
}
