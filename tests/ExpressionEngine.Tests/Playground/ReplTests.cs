using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

public class ReplTests
{
    private sealed class Run
    {
        public Run(string output, Session session)
        {
            Output = output;
            Session = session;
        }

        public string Output { get; }

        public Session Session { get; }
    }

    /// <summary>Runs a scripted, non-interactive session and returns everything that was printed.</summary>
    private static Run Script(params string[] lines) => Script(null, lines);

    private static Run Script(Func<bool>? more, params string[] lines)
    {
        var session = new Session();
        var output = new StringWriter();
        var repl = new Repl(session, output, Style.Plain, ManualViewer.Load(), "test", interactive: more is not null, more);
        var queue = new Queue<string>(lines);
        repl.ReadLine = (_, _) => queue.Count > 0 ? queue.Dequeue() : null;

        repl.Run(showBanner: false);
        return new Run(output.ToString().Replace("\r\n", "\n"), session);
    }

    [Fact]
    public void EvaluatesExpressionsAndRemembersTheLastResult()
    {
        var run = Script("1 + 2", "ans * 10", "1 < 2");

        Assert.Equal("3\n30\ntrue\n", run.Output);
    }

    [Fact]
    public void AssignmentsCreateVariables()
    {
        var run = Script("r = 10", "2 * pi * r", "ok = r > 5", "ok ? 1 : 0");

        Assert.Contains("r = 10", run.Output);
        Assert.Contains("62.83185307179586", run.Output);
        Assert.Contains("ok = true", run.Output);
        Assert.True(run.Session.Variables["ok"].AsBoolean());
    }

    [Fact]
    public void EqualityIsNotMistakenForAssignment()
    {
        var run = Script("x = 1", "x == 1");

        Assert.Equal("x = 1\ntrue\n", run.Output);
    }

    [Fact]
    public void ReservedLiteralsCannotBeAssigned()
    {
        var run = Script("true = 5");

        Assert.Contains("reserved literal", run.Output);
        Assert.Equal(0, run.Session.Variables.Count);
    }

    [Fact]
    public void ErrorsAreShownWithACaretUnderTheProblem()
    {
        var run = Script("5 + $");

        Assert.Equal(
            "error: Unexpected character '$' (at column 5)\n  5 + $\n      ^\n",
            run.Output);
    }

    [Fact]
    public void ErrorsDoNotEndTheSession()
    {
        var run = Script("1 / 0", "x + 1", "foo(", "2 + 2");

        Assert.Contains("Division by zero", run.Output);
        Assert.Contains("Unknown variable 'x'", run.Output);
        Assert.EndsWith("4\n", run.Output);
    }

    [Fact]
    public void BlankLinesAreIgnoredAndQuitEndsTheSession()
    {
        var run = Script(string.Empty, "   ", "1 + 1", "quit", "9 + 9");

        Assert.Equal("2\n", run.Output);
    }

    [Theory]
    [InlineData(":quit")]
    [InlineData(":q")]
    [InlineData("exit")]
    public void SeveralWaysToLeave(string command) =>
        Assert.Equal(string.Empty, Script(command, "1 + 1").Output);

    [Fact]
    public void BareWordsAreVariablesWhenSuchVariablesExist()
    {
        var run = Script("help = 5", "help + 1");

        Assert.Equal("help = 5\n6\n", run.Output);
    }

    [Fact]
    public void HelpListsTheCommands()
    {
        foreach (var command in new[] { ":help", "help", "?" })
        {
            var output = Script(command).Output;
            Assert.Contains(":run", output);
            Assert.Contains(":manual", output);
            Assert.Contains(":def", output);
        }
    }

    [Fact]
    public void UnknownCommandsAreReported() =>
        Assert.Contains("Unknown command ':bogus'", Script(":bogus").Output);

    // ---- examples ------------------------------------------------------------------------------------------------

    [Fact]
    public void ExamplesCommandListsTheIndex()
    {
        var output = Script(":examples").Output;

        Assert.Contains("Arithmetic", output);
        Assert.Contains("1 + 2 * 3", output);
        Assert.Contains("Understanding errors", output);
    }

    [Fact]
    public void ExamplesCanBeFilteredByCategory()
    {
        var output = Script(":examples booleans").Output;

        Assert.Contains("Booleans and comparisons", output);
        Assert.DoesNotContain("Arithmetic", output);
        Assert.Contains("No category matches", Script(":examples nothing").Output);
    }

    [Fact]
    public void RunShowsAnExampleWithItsExplanation()
    {
        var output = Script(":run 1").Output;

        Assert.Contains("1 + 2 * 3", output);
        Assert.Contains("= 7", output);
        Assert.Contains("Multiplication binds tighter", output);
        Assert.Contains(":edit 1", output);
    }

    [Fact]
    public void RunCanTakeACategoryOrAll()
    {
        var category = Script(":run Booleans").Output;
        Assert.Contains("Greater than", category);
        Assert.DoesNotContain("Pythagoras", category);

        var all = Script(":run all").Output;
        foreach (var example in ExampleCatalog.All)
        {
            Assert.Contains(example.Title, all);
        }
    }

    [Fact]
    public void RunReportsBadArguments()
    {
        Assert.Contains("Usage: :run", Script(":run").Output);
        Assert.Contains("No example matches '9999'", Script(":run 9999").Output);
        Assert.Contains("No example matches 'zzz'", Script(":run zzz").Output);
    }

    [Fact]
    public void RunUsesTheSessionOptions()
    {
        var divide = ExampleCatalog.All.Single(e => e.Expression == "1 / 0");

        var run = Script(":set nonfinite on", $":run {divide.Number}");

        Assert.Contains("= Infinity", run.Output);
        Assert.Contains("with the default options this gives: an error", run.Output);
    }

    [Fact]
    public void EditLoadsVariablesAndRunsTheExpressionWhenNotInteractive()
    {
        var example = ExampleCatalog.All.Single(e => e.Title == "Body mass index");

        var run = Script($":edit {example.Number}");

        Assert.Contains("Variables set: weight = 70, height = 1.75", run.Output);
        Assert.Contains("22.9", run.Output);
        Assert.Equal(70, run.Session.Variables["weight"].AsNumber());
    }

    [Fact]
    public void EditPrefillsThePromptWhenInteractive()
    {
        var session = new Session();
        var output = new StringWriter();
        var repl = new Repl(session, output, Style.Plain, ManualViewer.Load(), "test", interactive: true, more: () => true);
        var example = ExampleCatalog.All.Single(e => e.Title == "Sales tax");
        var prefills = new List<string>();
        var script = new Queue<string>(new[] { $":edit {example.Number}" });
        repl.ReadLine = (_, initial) =>
        {
            prefills.Add(initial);
            return script.Count > 0 ? script.Dequeue() : null;
        };

        repl.Run(showBanner: false);

        Assert.Equal(new[] { string.Empty, example.Expression }, prefills);
        Assert.Equal(100, session.Variables["price"].AsNumber());
    }

    [Fact]
    public void EditReportsBadArguments() =>
        Assert.Contains("Usage: :edit", Script(":edit nope").Output);

    // ---- manual --------------------------------------------------------------------------------------------------

    [Fact]
    public void ManualWithoutArgumentsShowsTheTableOfContents()
    {
        var output = Script(":manual").Output;

        Assert.Contains("ExpressionEngine Manual", output);
        Assert.Contains("Quick start", output);
        Assert.Contains("The playground", output);
    }

    [Fact]
    public void ManualShowsASectionByNumberOrName()
    {
        Assert.Contains("PROVIDING VARIABLES", Script(":manual providing").Output);
        Assert.Contains("PROVIDING VARIABLES", Script($":manual {ManualViewer.Load().Match("Providing").Single().Number}").Output);
        Assert.Contains("No manual section matches 'zzz'", Script(":manual zzz").Output);
        Assert.Contains("Several sections match 'ing'", Script(":manual ing").Output);
    }

    [Fact]
    public void ManualAllPrintsTheWholeManual()
    {
        var output = Script(":manual all").Output;

        foreach (var section in ManualViewer.Load().Sections)
        {
            Assert.Contains(section.Title.ToUpperInvariant(), output);
        }
    }

    [Fact]
    public void LongOutputIsPagedWhenInteractiveAndCanBeStopped()
    {
        var prompts = 0;
        var paged = Script(() => { prompts++; return false; }, ":manual all");
        var full = Script(":manual all");

        Assert.Equal(1, prompts);
        Assert.True(paged.Output.Length < full.Output.Length / 2);

        var keepGoing = 0;
        var everything = Script(() => { keepGoing++; return true; }, ":manual all");
        Assert.True(keepGoing > 3);
        Assert.Equal(full.Output.Length, everything.Output.Length);
    }

    [Fact]
    public void SearchFindsTextInTheManual()
    {
        var output = Script(":search tolerance").Output;

        Assert.Contains("tolerance", output);
        Assert.Contains("[", output);
        Assert.Contains("Nothing in the manual mentions", Script(":search zzzzzz").Output);
        Assert.Contains("Usage: :search", Script(":search").Output);
    }

    // ---- tools ---------------------------------------------------------------------------------------------------

    [Fact]
    public void AstShowsTheNormalizedFormVariablesAndTree()
    {
        var output = Script(":ast ((1+2) * x)").Output;

        Assert.Contains("normalized:  (1 + 2) * x", output);
        Assert.Contains("variables:   x", output);
        Assert.Contains("functions:   (none)", output);
        Assert.Contains("Binary Multiply", output);
        Assert.Contains("└─ Variable x", output);
    }

    [Fact]
    public void AstReportsSyntaxErrorsAndMissingArguments()
    {
        Assert.Contains("Unexpected end of expression", Script(":ast 1 +").Output);
        Assert.Contains("Usage: :ast", Script(":ast").Output);
    }

    [Fact]
    public void VariablesCanBeListedRemovedAndCleared()
    {
        var output = Script(":vars", "b = 2", "a = 1", ":vars", ":unset a", ":unset a", ":clear", ":vars").Output;

        Assert.Contains("(no variables)", output);
        Assert.Contains("a = 1\nans = 1\nb = 2\n", output);   // sorted, case-insensitively
        Assert.Contains("Removed a.", output);
        Assert.Contains("There is no variable 'a'", output);
        Assert.Contains("All variables removed.", output);
    }

    [Fact]
    public void FunctionsCommandListsBuiltInsOwnFunctionsAndConstants()
    {
        var output = Script(":def double(x) = x * 2", ":funcs").Output;

        Assert.Contains("sqrt(1)", output);
        Assert.Contains("max(1+)", output);
        Assert.Contains("round(1-2)", output);
        Assert.Contains("double(1)*", output);
        Assert.Contains("pi = 3.141592653589793", output);
    }

    // ---- :def, :const and :set -----------------------------------------------------------------------------------

    [Fact]
    public void DefineCreatesAUsableFunction()
    {
        var run = Script(":def hyp(a, b) = sqrt(a^2 + b^2)", "hyp(3, 4)", "hyp(5, 12) + 1");

        Assert.Contains("Defined hyp(a, b)", run.Output);
        Assert.EndsWith("5\n14\n", run.Output);
    }

    [Fact]
    public void DefinedFunctionsCheckTheirArgumentCount()
    {
        var run = Script(":def twice(x) = x * 2", "twice(1, 2)");

        Assert.Contains("Function 'twice' expects 1 argument but got 2", run.Output);
    }

    [Fact]
    public void FunctionsCanUseEarlierDefinitionsAndConstants()
    {
        var run = Script(":const rate = 0.5", ":def half(x) = x * rate", ":def quarter(x) = half(half(x))", "quarter(100)");

        Assert.EndsWith("25\n", run.Output);
    }

    [Fact]
    public void FunctionsCanBeRedefined()
    {
        var run = Script(":def f(x) = x + 1", "f(1)", ":def f(x) = x + 100", "f(1)");

        Assert.EndsWith("2\nDefined f(x). Try it: f(1)\n101\n", run.Output);
    }

    [Fact]
    public void FunctionsCannotCallThemselvesOrUseOutsideNames()
    {
        var recursive = Script(":def loop(n) = loop(n)");
        Assert.Contains("Unknown function 'loop'", recursive.Output);

        var outside = Script("z = 5", ":def bad(a) = a + z", "bad(1)");
        Assert.Contains("uses 'z', which is not one of its parameters", outside.Output);
        Assert.Contains("Unknown function 'bad'", outside.Output);
    }

    [Fact]
    public void DefineValidatesItsInput()
    {
        Assert.Contains("Usage: :def", Script(":def nonsense").Output);
        Assert.Contains("must be different", Script(":def f(a, a) = a").Output);
        Assert.Contains("not a valid parameter name", Script(":def f(1a) = 1").Output);
        Assert.Contains("Usage: :def", Script(":def 9f(a) = a").Output);
    }

    [Fact]
    public void FunctionsWithoutParametersWork()
    {
        var run = Script(":def answer() = 42", "answer() + 1");

        Assert.EndsWith("43\n", run.Output);
    }

    [Fact]
    public void ConstantsCaptureTheValueOfAnExpression()
    {
        var run = Script("base = 10", ":const limit = base * 2", "base = 1", "limit + 1");

        Assert.Contains("Defined constant limit = 20", run.Output);
        Assert.EndsWith("21\n", run.Output);
    }

    [Fact]
    public void ConstantsMustBeNumbers()
    {
        Assert.Contains("must be a number", Script(":const flag = 1 < 2").Output);
        Assert.Contains("Usage: :const", Script(":const").Output);
    }

    [Fact]
    public void OptionsCanBeShownAndChanged()
    {
        var run = Script(":set", ":set ignorecase on", ":set nonfinite on", ":set");

        Assert.Contains("ignorecase  off", run.Output);
        Assert.Contains("ignorecase is now on.", run.Output);
        Assert.Contains("nonfinite   on", run.Output);
        Assert.True(run.Session.IgnoreCase);
        Assert.True(run.Session.AllowNonFinite);
    }

    [Fact]
    public void OptionsChangeBehaviorAndKeepVariablesAndDefinitions()
    {
        var run = Script("Radius = 2", ":def sq(x) = x * x", ":set ignorecase on", "SQ(radius)", "1 / 0", ":set nonfinite on", "1 / 0");

        Assert.Contains("\n4\n", run.Output);
        Assert.Contains("Division by zero", run.Output);
        Assert.EndsWith("Infinity\n", run.Output);
    }

    [Fact]
    public void OptionsRejectBadInput()
    {
        Assert.Contains("Usage: :set", Script(":set ignorecase maybe").Output);
        Assert.Contains("Unknown option 'colour'", Script(":set colour on").Output);
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var run = Script("x = 1", ":def f(a) = a", ":const c = 2", ":set ignorecase on", ":reset", "x");

        Assert.Contains("Session reset", run.Output);
        Assert.Contains("Unknown variable 'x'", run.Output);
        Assert.False(run.Session.IgnoreCase);
        Assert.Empty(run.Session.UserConstantNames);
        Assert.False(run.Session.IsUserFunction("f"));
    }

    // ---- completion and banner -----------------------------------------------------------------------------------

    [Fact]
    public void CompletionKnowsCommandsFunctionsConstantsAndVariables()
    {
        var run = Script("radius = 3");
        var repl = new Repl(run.Session, new StringWriter(), Style.Plain, ManualViewer.Load(), "test", false, null);

        Assert.Equal(new[] { ":manual" }, repl.Complete(":ma", 3)!.Candidates);
        Assert.Contains(":run", repl.Complete(":r", 2)!.Candidates);
        Assert.Equal(new[] { "sqrt(" }, repl.Complete("2 * sq", 6)!.Candidates);
        Assert.Equal(new[] { "radius" }, repl.Complete("ra", 2)!.Candidates.Where(c => c == "radius"));
        Assert.Contains("pi", repl.Complete("p", 1)!.Candidates);
        Assert.Contains("true", repl.Complete("tr", 2)!.Candidates);
        Assert.Equal(4, repl.Complete("1 + ro", 6)!.Start);
        Assert.Null(repl.Complete("12", 2));
        Assert.Null(repl.Complete("1 + ", 4));
        Assert.Null(repl.Complete(":zzz", 4));
    }

    [Fact]
    public void BannerShowsTheExamplesAndNextSteps()
    {
        var output = new StringWriter();
        var repl = new Repl(new Session(), output, Style.Plain, ManualViewer.Load(), "9.9.9", false, null);
        repl.ReadLine = (_, _) => null;

        repl.Run(showBanner: true);
        var text = output.ToString();

        Assert.Contains("interactive playground", text);
        Assert.Contains("version 9.9.9", text);
        foreach (var example in ExampleCatalog.All)
        {
            Assert.Contains(example.Expression, text);
        }

        Assert.Contains(":manual", text);
        Assert.Contains(":run 1", text);
        Assert.Contains(":def hyp", text);
    }
}
