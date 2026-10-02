using ExpressionEngine.Playground;

namespace ExpressionEngine.Tests.Playground;

public class LineEditorTests
{
    private sealed class Rig
    {
        private readonly Queue<ConsoleKeyInfo> _keys = new();

        public Rig(Func<string, int, Completion?>? complete = null)
        {
            Output = new StringWriter();
            Editor = new LineEditor(() => _keys.Dequeue(), Output, complete);
        }

        public StringWriter Output { get; }

        public LineEditor Editor { get; }

        public Rig Type(string text)
        {
            foreach (var c in text)
            {
                _keys.Enqueue(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false));
            }

            return this;
        }

        public Rig Press(ConsoleKey key, int times = 1)
        {
            for (var i = 0; i < times; i++)
            {
                _keys.Enqueue(new ConsoleKeyInfo('\0', key, false, false, false));
            }

            return this;
        }

        public Rig Ctrl(ConsoleKey key)
        {
            _keys.Enqueue(new ConsoleKeyInfo('\0', key, false, false, true));
            return this;
        }

        public Rig Enter() => Press(ConsoleKey.Enter);

        public string? Read(string initial = "") => Editor.ReadLine("> ", initial);
    }

    [Fact]
    public void TypingAndEnterReturnsTheLine() =>
        Assert.Equal("1 + 2", new Rig().Type("1 + 2").Enter().Read());

    [Fact]
    public void BackspaceAndDeleteEditTheLine()
    {
        Assert.Equal("12", new Rig().Type("123").Press(ConsoleKey.Backspace).Enter().Read());
        Assert.Equal("13", new Rig().Type("123").Press(ConsoleKey.LeftArrow, 2).Press(ConsoleKey.Delete).Enter().Read());
        Assert.Equal(string.Empty, new Rig().Press(ConsoleKey.Backspace).Press(ConsoleKey.Delete).Enter().Read());
    }

    [Fact]
    public void ArrowsAndHomeEndMoveTheCursorForInsertion()
    {
        Assert.Equal("1X2", new Rig().Type("12").Press(ConsoleKey.LeftArrow).Type("X").Enter().Read());
        Assert.Equal("Z12", new Rig().Type("12").Press(ConsoleKey.Home).Type("Z").Enter().Read());
        Assert.Equal("12Z", new Rig().Type("12").Press(ConsoleKey.Home).Press(ConsoleKey.End).Type("Z").Enter().Read());
        Assert.Equal("12Z", new Rig().Type("12").Press(ConsoleKey.RightArrow, 5).Type("Z").Enter().Read());
        Assert.Equal("Z12", new Rig().Type("12").Press(ConsoleKey.LeftArrow, 5).Type("Z").Enter().Read());
    }

    [Fact]
    public void ControlKeysEditLikeReadline()
    {
        Assert.Equal("Xabc", new Rig().Type("abc").Ctrl(ConsoleKey.A).Type("X").Enter().Read());
        Assert.Equal("abcX", new Rig().Type("abc").Ctrl(ConsoleKey.A).Ctrl(ConsoleKey.E).Type("X").Enter().Read());
        Assert.Equal("c", new Rig().Type("abc").Press(ConsoleKey.LeftArrow).Ctrl(ConsoleKey.U).Enter().Read());
        Assert.Equal("ab", new Rig().Type("abc").Press(ConsoleKey.LeftArrow).Ctrl(ConsoleKey.K).Enter().Read());
        Assert.Equal("foo ", new Rig().Type("foo bar").Ctrl(ConsoleKey.W).Enter().Read());
        Assert.Equal(string.Empty, new Rig().Type("anything").Press(ConsoleKey.Escape).Enter().Read());
    }

    [Fact]
    public void CtrlDEndsInputOnlyOnAnEmptyLine()
    {
        Assert.Null(new Rig().Ctrl(ConsoleKey.D).Read());
        Assert.Equal("ac", new Rig().Type("abc").Press(ConsoleKey.LeftArrow, 2).Ctrl(ConsoleKey.D).Enter().Read());
    }

    [Fact]
    public void InitialTextIsEditable() =>
        Assert.Equal("price * 2!", new Rig().Type("!").Enter().Read("price * 2"));

    [Fact]
    public void HistoryRecallsPreviousLinesAndRestoresTheDraft()
    {
        var rig = new Rig();
        rig.Type("first").Enter().Type("second").Enter();
        Assert.Equal("first", rig.Read());
        Assert.Equal("second", rig.Read());

        // Browse back, then forward to the draft that was being typed.
        rig.Type("dra").Press(ConsoleKey.UpArrow).Press(ConsoleKey.UpArrow).Press(ConsoleKey.DownArrow).Press(ConsoleKey.DownArrow).Enter();
        Assert.Equal("dra", rig.Read());

        // Recalled text can be edited before running it.
        rig.Press(ConsoleKey.UpArrow).Type("!").Enter();
        Assert.Equal("dra!", rig.Read());
        Assert.Equal(new[] { "first", "second", "dra", "dra!" }, rig.Editor.History);
    }

    [Fact]
    public void HistoryIgnoresBlankLinesAndConsecutiveDuplicates()
    {
        var rig = new Rig();
        rig.Type("a").Enter().Type("a").Enter().Type("   ").Enter().Enter().Type("b").Enter();
        for (var i = 0; i < 5; i++)
        {
            rig.Read();
        }

        Assert.Equal(new[] { "a", "b" }, rig.Editor.History);
    }

    [Fact]
    public void UpArrowOnEmptyHistoryDoesNothing() =>
        Assert.Equal("x", new Rig().Press(ConsoleKey.UpArrow).Press(ConsoleKey.DownArrow).Type("x").Enter().Read());

    private static Completion? Words(string text, int cursor)
    {
        var start = cursor;
        while (start > 0 && char.IsLetter(text[start - 1]))
        {
            start--;
        }

        var prefix = text[start..cursor];
        var candidates = new[] { "sqrt(", "sum(", "sin(", "floor", "floppy", "max", "min" }
            .Where(w => prefix.Length > 0 && w.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        return new Completion(start, candidates);
    }

    [Fact]
    public void TabCompletesAUniqueCandidate() =>
        Assert.Equal("1 + sqrt(", new Rig(Words).Type("1 + sq").Press(ConsoleKey.Tab).Enter().Read());

    [Fact]
    public void TabCompletesTheCommonPrefix() =>
        Assert.Equal("flo", new Rig(Words).Type("f").Press(ConsoleKey.Tab).Enter().Read());

    [Fact]
    public void TabListsCandidatesWhenThereIsNothingMoreToAdd()
    {
        var rig = new Rig(Words);

        var line = rig.Type("m").Press(ConsoleKey.Tab).Type("a").Enter().Read();

        Assert.Equal("ma", line);
        Assert.Contains("max  min", rig.Output.ToString());
    }

    [Fact]
    public void TabWithoutCandidatesChangesNothing() =>
        Assert.Equal("7", new Rig(Words).Type("7").Press(ConsoleKey.Tab).Enter().Read());

    [Fact]
    public void TabInTheMiddleOfALineReplacesOnlyTheWordBeforeTheCursor()
    {
        var line = new Rig(Words).Type("sq + 1").Press(ConsoleKey.Home).Press(ConsoleKey.RightArrow, 2).Press(ConsoleKey.Tab).Enter().Read();

        Assert.Equal("sqrt( + 1", line);
    }

    [Fact]
    public void ThePromptAndTheLineAreDrawn()
    {
        var rig = new Rig().Type("ab").Enter();
        rig.Read();

        Assert.Contains("> ab", rig.Output.ToString());
    }
}
