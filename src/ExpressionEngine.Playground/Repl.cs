using System.Text.RegularExpressions;

namespace ExpressionEngine.Playground;

/// <summary>The interactive loop: reads lines, evaluates expressions, runs ':' commands.</summary>
internal sealed partial class Repl
{
    private static readonly string[] CommandNames =
    {
        ":help", ":examples", ":run", ":edit", ":manual", ":search", ":ast", ":vars", ":unset", ":clear",
        ":funcs", ":def", ":const", ":set", ":reset", ":quit",
    };

    private const string HelpText = """
        Type an expression and press Enter:             2 * (3 + 4)
        Assign a variable:                              r = 10          (the last result is always 'ans')

        :examples [category]    list the examples
        :run <n | category | all>   run example(s) and read the explanation
        :edit <n>               load an example's variables and edit its expression
        :manual [section]       read the manual (no argument: table of contents; 'all': everything)
        :search <text>          find text in the manual
        :ast <expression>       show the syntax tree and how the expression was understood
        :vars  :unset <name>  :clear      list, remove or clear variables
        :funcs                  list functions and constants
        :def f(a, b) = body     define your own function     :def hyp(a, b) = sqrt(a^2 + b^2)
        :const name = <expr>    define a constant            :const rate = 0.2
        :set [option on|off]    options: ignorecase, nonfinite
        :reset                  forget variables, definitions and options
        :quit                   leave (Ctrl+D also works)

        Keys: Up/Down history, Tab completes, Ctrl+A/E start/end of line, Ctrl+U clears to the start, Ctrl+L clears the screen.
        """;

    [GeneratedRegex(@"^([\p{L}_][\p{L}\p{Nd}_]*)\s*=(?!=)\s*(.+)$")]
    private static partial Regex Assignment();

    [GeneratedRegex(@"^([\p{L}_][\p{L}\p{Nd}_]*)\s*\(([^)]*)\)\s*=\s*(.+)$")]
    private static partial Regex FunctionDefinitionPattern();

    private readonly Session _session;
    private readonly TextWriter _out;
    private readonly Style _style;
    private readonly ManualViewer _manual;
    private readonly string _version;
    private readonly bool _interactive;
    private readonly Func<bool>? _more;
    private string _prefill = string.Empty;

    public Repl(Session session, TextWriter output, Style style, ManualViewer manual, string version, bool interactive, Func<bool>? more)
    {
        _session = session;
        _out = output;
        _style = style;
        _manual = manual;
        _version = version;
        _interactive = interactive;
        _more = more;
        ReadLine = (prompt, _) =>
        {
            _out.Write(prompt);
            return null;
        };
    }

    /// <summary>Supplies the next line given the prompt and any text to pre-fill. Returns null at end of input.</summary>
    public Func<string, string, string?> ReadLine { get; set; }

    private string Prompt => _style.Accent("> ");

    public int Run(bool showBanner)
    {
        if (showBanner)
        {
            PrintBanner();
        }

        while (true)
        {
            var prefill = _prefill;
            _prefill = string.Empty;
            var line = ReadLine(Prompt, prefill);
            if (line is null || !Execute(line))
            {
                return 0;
            }
        }
    }

    /// <summary>Runs one line of input. Returns false when the session should end.</summary>
    public bool Execute(string line)
    {
        var text = line.Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (text.StartsWith(':'))
        {
            return RunCommand(text);
        }

        if (!_session.Variables.Contains(text))
        {
            switch (text.ToLowerInvariant())
            {
                case "exit" or "quit":
                    return false;
                case "help" or "?":
                    ShowLines(HelpText.Split('\n').Select(l => l.TrimEnd('\r')).ToArray());
                    return true;
            }
        }

        try
        {
            var assignment = Assignment().Match(text);
            if (assignment.Success)
            {
                var name = assignment.Groups[1].Value;
                if (name.Equals("true", StringComparison.OrdinalIgnoreCase) || name.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    _out.WriteLine(_style.Bad($"error: '{name}' is a reserved literal and cannot be a variable name"));
                    return true;
                }

                var value = _session.Evaluate(assignment.Groups[2].Value);
                _session.Variables[name] = value;
                _session.Variables["ans"] = value;
                _out.WriteLine($"{_style.Dim(name + " =")} {_style.Good(value.ToString())}");
            }
            else
            {
                var value = _session.Evaluate(text);
                _session.Variables["ans"] = value;
                _out.WriteLine(_style.Good(value.ToString()));
            }
        }
        catch (ExpressionException ex)
        {
            ReportError(ex);
        }

        return true;
    }

    // ---- commands ------------------------------------------------------------------------------------------------

    private bool RunCommand(string text)
    {
        var space = text.IndexOf(' ');
        var name = (space < 0 ? text : text[..space]).ToLowerInvariant();
        var argument = space < 0 ? string.Empty : text[(space + 1)..].Trim();

        try
        {
            switch (name)
            {
                case ":quit" or ":q" or ":exit":
                    return false;
                case ":help" or ":h" or ":?":
                    ShowLines(HelpText.Split('\n').Select(l => l.TrimEnd('\r')).ToArray());
                    break;
                case ":examples" or ":ex":
                    ListExamples(argument);
                    break;
                case ":run":
                    RunExamples(argument);
                    break;
                case ":edit":
                    EditExample(argument);
                    break;
                case ":manual" or ":man":
                    ShowManual(argument);
                    break;
                case ":search":
                    Search(argument);
                    break;
                case ":ast":
                    ShowTree(argument);
                    break;
                case ":vars":
                    ListVariables();
                    break;
                case ":unset":
                    Unset(argument);
                    break;
                case ":clear":
                    _session.Variables.Clear();
                    _out.WriteLine("All variables removed.");
                    break;
                case ":funcs":
                    ListFunctions();
                    break;
                case ":def":
                    Define(argument);
                    break;
                case ":const":
                    DefineConstant(argument);
                    break;
                case ":set":
                    SetOption(argument);
                    break;
                case ":reset":
                    _session.Reset();
                    _out.WriteLine("Session reset: no variables or definitions, default options.");
                    break;
                default:
                    _out.WriteLine(_style.Bad($"Unknown command '{name}'.") + " Type :help for the list.");
                    break;
            }
        }
        catch (ExpressionException ex)
        {
            ReportError(ex);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _out.WriteLine(_style.Bad("error: " + ex.Message));
        }

        return true;
    }

    private void ListExamples(string filter)
    {
        var lines = ExampleView.Index(_style, filter);
        if (lines.Count == 0)
        {
            _out.WriteLine($"No category matches '{filter}'. Categories: {string.Join(", ", ExampleCatalog.Categories)}");
            return;
        }

        ShowLines(lines);
    }

    private void RunExamples(string argument)
    {
        if (argument.Length == 0)
        {
            _out.WriteLine("Usage: :run <number | category | all>     (see :examples)");
            return;
        }

        IReadOnlyList<Example> chosen;
        if (argument.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            chosen = ExampleCatalog.All;
        }
        else if (int.TryParse(argument, out var number))
        {
            chosen = ExampleCatalog.All.Where(e => e.Number == number).ToArray();
        }
        else
        {
            chosen = ExampleView.CategoriesMatching(argument).SelectMany(ExampleCatalog.InCategory).ToArray();
        }

        if (chosen.Count == 0)
        {
            _out.WriteLine($"No example matches '{argument}'. See :examples.");
            return;
        }

        var lines = new List<string>();
        var width = Math.Min(Terminal.Width - 2, 100);
        var defaultOptions = !_session.IgnoreCase && !_session.AllowNonFinite;
        foreach (var example in chosen)
        {
            lines.AddRange(ExampleView.Detail(example, ExampleRunner.Run(_session.Engine, example), _style, defaultOptions, width));
        }

        if (chosen.Count == 1)
        {
            lines.Add(string.Empty);
            lines.Add(_style.Dim($"    :edit {chosen[0].Number} to change it and try your own version."));
        }

        ShowLines(lines);
    }

    private void EditExample(string argument)
    {
        if (!int.TryParse(argument, out var number) || ExampleCatalog.All.FirstOrDefault(e => e.Number == number) is not { } example)
        {
            _out.WriteLine("Usage: :edit <example number>     (see :examples)");
            return;
        }

        foreach (var (name, value) in example.Variables)
        {
            _session.Variables[name] = value;
        }

        if (example.Variables.Count > 0)
        {
            _out.WriteLine(_style.Dim("Variables set: " + ExampleView.VariablesText(example)));
        }

        if (_interactive)
        {
            _out.WriteLine(_style.Dim("Edit the expression below and press Enter (Esc clears the line)."));
            _prefill = example.Expression;
        }
        else
        {
            Execute(example.Expression);
        }
    }

    private void ShowManual(string argument)
    {
        var width = Math.Min(Terminal.Width - 2, 100);
        if (argument.Length == 0)
        {
            ShowLines(_manual.TableOfContents(_style));
        }
        else if (argument.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            ShowLines(_manual.RenderAll(_style, width));
        }
        else
        {
            var matches = _manual.Match(argument);
            if (matches.Count == 0)
            {
                _out.WriteLine($"No manual section matches '{argument}'. Type :manual for the table of contents.");
            }
            else if (matches.Count == 1)
            {
                ShowLines(_manual.Render(matches[0], _style, width));
            }
            else
            {
                _out.WriteLine($"Several sections match '{argument}':");
                foreach (var section in matches)
                {
                    _out.WriteLine($"  {_style.Accent(section.Number.ToString().PadLeft(2))}  {section.Title}");
                }
            }
        }
    }

    private void Search(string text)
    {
        if (text.Length == 0)
        {
            _out.WriteLine("Usage: :search <text>");
            return;
        }

        var hits = _manual.Search(text);
        if (hits.Count == 0)
        {
            _out.WriteLine($"Nothing in the manual mentions '{text}'.");
            return;
        }

        var limit = Math.Min(Terminal.Width - 2, 110);
        var lines = new List<string>();
        foreach (var (section, line) in hits)
        {
            var label = $"[{section.Number}] ";
            var room = Math.Max(20, limit - label.Length);
            var shown = line.Length > room ? line[..(room - 1)] + "…" : line;
            lines.Add(_style.Accent(label) + shown);
        }

        lines.Add(string.Empty);
        lines.Add(_style.Dim("Read a section with :manual <number>."));
        ShowLines(lines);
    }

    private void ShowTree(string text)
    {
        if (text.Length == 0)
        {
            _out.WriteLine("Usage: :ast <expression>     for example  :ast 1 + 2 * x");
            return;
        }

        var expression = _session.Engine.Parse(text);
        var lines = new List<string>
        {
            _style.Dim("normalized:  ") + expression.Root,
            _style.Dim("variables:   ") + (expression.Variables.Count == 0 ? "(none)" : string.Join(", ", expression.Variables)),
            _style.Dim("functions:   ") + (expression.Functions.Count == 0 ? "(none)" : string.Join(", ", expression.Functions.Select(f => f.Name))),
            string.Empty,
        };
        lines.AddRange(TreeDump.Render(expression.Root));
        ShowLines(lines);
    }

    private void ListVariables()
    {
        if (_session.Variables.Count == 0)
        {
            _out.WriteLine("(no variables)  Set one with  x = 5");
            return;
        }

        foreach (var pair in _session.Variables.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
        {
            _out.WriteLine($"{pair.Key} = {_style.Good(pair.Value.ToString())}");
        }
    }

    private void Unset(string name)
    {
        _out.WriteLine(name.Length > 0 && _session.Variables.Remove(name)
            ? $"Removed {name}."
            : $"There is no variable '{name}'. See :vars.");
    }

    private void ListFunctions()
    {
        string Arity(FunctionDefinition f) => f.IsVariadic
            ? $"{f.MinArguments}+"
            : f.MinArguments == f.MaxArguments ? f.MinArguments.ToString() : $"{f.MinArguments}-{f.MaxArguments}";

        var functions = _session.Engine.Functions
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => $"{f.Name}({Arity(f)}){(_session.IsUserFunction(f.Name) ? "*" : string.Empty)}")
            .ToArray();

        var lines = new List<string>
        {
            _style.Bold("Functions") + _style.Dim("  name(number of arguments); * marks your own definitions"),
        };
        lines.AddRange(ManualViewer.Wrap(string.Join(' ', functions), "  ", "  ", Math.Min(Terminal.Width - 2, 100)));
        lines.Add(string.Empty);
        lines.Add(_style.Bold("Constants"));
        foreach (var constant in _session.Engine.Constants.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            lines.Add($"  {constant.Key} = {constant.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        ShowLines(lines);
    }

    private void Define(string text)
    {
        var match = FunctionDefinitionPattern().Match(text);
        if (!match.Success)
        {
            _out.WriteLine("Usage: :def name(param1, param2) = expression     for example  :def hyp(a, b) = sqrt(a^2 + b^2)");
            return;
        }

        var name = match.Groups[1].Value;
        var parameters = match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parameters.Distinct(StringComparer.Ordinal).Count() != parameters.Length)
        {
            _out.WriteLine(_style.Bad("error: parameter names must be different from each other"));
            return;
        }

        foreach (var parameter in parameters.Where(p => !IsIdentifier(p)))
        {
            _out.WriteLine(_style.Bad($"error: '{parameter}' is not a valid parameter name"));
            return;
        }

        _session.DefineFunction(name, parameters, match.Groups[3].Value.Trim());
        _out.WriteLine($"Defined {_style.Bold($"{name}({string.Join(", ", parameters)})")}. Try it: {name}({string.Join(", ", parameters.Select((_, i) => i + 1))})");
    }

    private void DefineConstant(string text)
    {
        var match = Assignment().Match(text);
        if (!match.Success)
        {
            _out.WriteLine("Usage: :const name = expression     for example  :const rate = 0.2");
            return;
        }

        _session.DefineConstant(match.Groups[1].Value, match.Groups[2].Value);
        _out.WriteLine($"Defined constant {match.Groups[1].Value} = {_session.Engine.Constants[match.Groups[1].Value].ToString("R", System.Globalization.CultureInfo.InvariantCulture)}");
    }

    private void SetOption(string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            _out.WriteLine($"ignorecase  {(_session.IgnoreCase ? "on" : "off")}    case-insensitive function, constant and variable names");
            _out.WriteLine($"nonfinite   {(_session.AllowNonFinite ? "on" : "off")}    allow infinity and NaN instead of errors");
            _out.WriteLine(_style.Dim("Change one with  :set ignorecase on  or  :set nonfinite off"));
            return;
        }

        if (parts.Length != 2 || !TryParseSwitch(parts[1], out var enabled))
        {
            _out.WriteLine("Usage: :set <ignorecase | nonfinite> <on | off>");
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "ignorecase":
                _session.SetIgnoreCase(enabled);
                break;
            case "nonfinite":
                _session.SetAllowNonFinite(enabled);
                break;
            default:
                _out.WriteLine($"Unknown option '{parts[0]}'. Options: ignorecase, nonfinite.");
                return;
        }

        _out.WriteLine($"{parts[0].ToLowerInvariant()} is now {(enabled ? "on" : "off")}.");
    }

    private static bool TryParseSwitch(string text, out bool value)
    {
        switch (text.ToLowerInvariant())
        {
            case "on" or "true" or "yes" or "1":
                value = true;
                return true;
            case "off" or "false" or "no" or "0":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static bool IsIdentifier(string text) =>
        text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');

    // ---- output --------------------------------------------------------------------------------------------------

    private void ReportError(ExpressionException ex)
    {
        _out.WriteLine(_style.Bad("error: " + ex.Message));

        // Point at the problem when the expression is a single line.
        if (ex.ExpressionText is { } text && ex.Position >= 0 && !text.Contains('\n'))
        {
            _out.WriteLine("  " + text);
            _out.WriteLine("  " + new string(' ', ex.Position) + _style.Bad(new string('^', Math.Max(1, ex.Length))));
        }
    }

    /// <summary>Writes lines, pausing every screenful when a person is reading.</summary>
    private void ShowLines(IReadOnlyList<string> lines)
    {
        var pageHeight = Terminal.Height - 1;
        if (_more is null || lines.Count <= pageHeight)
        {
            foreach (var line in lines)
            {
                _out.WriteLine(line);
            }

            return;
        }

        var shown = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            _out.WriteLine(lines[i]);
            shown++;
            if (shown >= pageHeight - 1 && i < lines.Count - 1)
            {
                if (!_more())
                {
                    return;
                }

                shown = 0;
            }
        }
    }

    private void PrintBanner()
    {
        const string title = "ExpressionEngine  ·  interactive playground";
        var inner = title.Length + 4;
        _out.WriteLine();
        _out.WriteLine(_style.Accent("  ╭" + new string('─', inner) + "╮"));
        _out.WriteLine(_style.Accent("  │  ") + _style.Bold(title) + _style.Accent("  │"));
        _out.WriteLine(_style.Accent("  ╰" + new string('─', inner) + "╯"));
        _out.WriteLine(_style.Dim($"  version {_version}"));
        _out.WriteLine();
        _out.WriteLine("  Try the engine before you copy it into your project. Type an expression and press Enter,");
        _out.WriteLine($"  for example   {_style.Code("2 * (3 + 4)")}   or   {_style.Code("sqrt(16) + max(1, 2)")}.   Here are the built-in examples:");

        foreach (var line in ExampleView.Index(_style))
        {
            _out.WriteLine(line);
        }

        _out.WriteLine();
        _out.WriteLine(_style.Heading("Next steps"));
        _out.WriteLine($"  {_style.Code(":run 1")}            run an example and read the explanation   ({_style.Code(":run Arithmetic")}, {_style.Code(":run all")})");
        var editable = ExampleCatalog.All.First(e => e.Variables.Count > 0).Number;
        _out.WriteLine($"  {_style.Code($":edit {editable}")}          load an example and change it");
        _out.WriteLine($"  {_style.Code(":manual")}           read the full manual                      ({_style.Code(":search text")} to look something up)");
        _out.WriteLine($"  {_style.Code(":ast 1 + 2 * x")}    see how an expression is understood");
        _out.WriteLine($"  {_style.Code(":def hyp(a, b) = sqrt(a^2 + b^2)")}   define your own function");
        _out.WriteLine($"  {_style.Code(":help")}             all commands, keys and options            {_style.Code(":quit")}  leave");
        _out.WriteLine();
    }

    // ---- completion ----------------------------------------------------------------------------------------------

    /// <summary>Tab completion for ':' commands and for function, constant and variable names.</summary>
    public Completion? Complete(string text, int cursor)
    {
        var comparison = _session.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var head = text[..cursor];

        if (head.StartsWith(':') && !head.Contains(' '))
        {
            var commands = CommandNames.Where(c => c.StartsWith(head, StringComparison.OrdinalIgnoreCase)).ToArray();
            return commands.Length == 0 ? null : new Completion(0, commands);
        }

        var start = cursor;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        var prefix = text[start..cursor];
        if (prefix.Length == 0 || char.IsDigit(prefix[0]))
        {
            return null;
        }

        var functionNames = _session.Engine.Functions.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var names = functionNames
            .Concat(_session.Engine.Constants.Keys)
            .Concat(_session.Variables.Select(v => v.Key))
            .Concat(new[] { "true", "false" })
            .Where(n => n.StartsWith(prefix, comparison))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        if (names.Length == 0)
        {
            return null;
        }

        // A lone function name completes with its opening parenthesis.
        if (names.Length == 1 && functionNames.Contains(names[0]))
        {
            names[0] += "(";
        }

        return new Completion(start, names);
    }
}
