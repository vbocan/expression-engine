using System.Text;

namespace ExpressionEngine.Playground;

/// <summary>What Tab should insert: the candidates that fit the word being typed, which starts at <see cref="Start"/>.</summary>
internal sealed record Completion(int Start, IReadOnlyList<string> Candidates);

/// <summary>
/// A small interactive line editor: cursor movement, history (up/down) and Tab completion.
/// Keys come from a delegate and output goes to a writer, so it can be tested without a terminal.
/// </summary>
/// <remarks>
/// The line is redrawn from the start of the current terminal row after every key press,
/// so a line that wraps past the terminal width is not redrawn cleanly.
/// </remarks>
internal sealed class LineEditor
{
    private readonly Func<ConsoleKeyInfo> _readKey;
    private readonly TextWriter _out;
    private readonly Func<string, int, Completion?> _complete;
    private readonly List<string> _history = new();

    public LineEditor(Func<ConsoleKeyInfo> readKey, TextWriter output, Func<string, int, Completion?>? complete = null)
    {
        _readKey = readKey;
        _out = output;
        _complete = complete ?? ((_, _) => null);
    }

    public IReadOnlyList<string> History => _history;

    /// <summary>Reads one line. Returns <see langword="null"/> when the user ends input (Ctrl+D on an empty line).</summary>
    public string? ReadLine(string prompt, string initial = "")
    {
        var buffer = new StringBuilder(initial);
        var cursor = buffer.Length;
        var historyIndex = _history.Count;      // equal to Count while editing a fresh line
        var draft = string.Empty;               // what was being typed before browsing history

        void Redraw()
        {
            _out.Write('\r');
            _out.Write(prompt);
            _out.Write(buffer.ToString());
            _out.Write("\u001b[K");
            var back = buffer.Length - cursor;
            if (back > 0)
            {
                _out.Write($"\u001b[{back}D");
            }

            _out.Flush();
        }

        void Replace(string text)
        {
            buffer.Clear().Append(text);
            cursor = buffer.Length;
        }

        Redraw();
        while (true)
        {
            var key = _readKey();
            var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;

            if (key.Key == ConsoleKey.Enter)
            {
                _out.Write("\r\n");
                _out.Flush();
                var line = buffer.ToString();
                if (line.Trim().Length > 0 && (_history.Count == 0 || _history[^1] != line))
                {
                    _history.Add(line);
                }

                return line;
            }

            if (ctrl && key.Key == ConsoleKey.D)
            {
                if (buffer.Length == 0)
                {
                    _out.Write("\r\n");
                    return null;
                }

                if (cursor < buffer.Length)
                {
                    buffer.Remove(cursor, 1);
                }
            }
            else if (ctrl && key.Key == ConsoleKey.A)
            {
                cursor = 0;
            }
            else if (ctrl && key.Key == ConsoleKey.E)
            {
                cursor = buffer.Length;
            }
            else if (ctrl && key.Key == ConsoleKey.U)
            {
                buffer.Remove(0, cursor);
                cursor = 0;
            }
            else if (ctrl && key.Key == ConsoleKey.K)
            {
                buffer.Remove(cursor, buffer.Length - cursor);
            }
            else if (ctrl && key.Key == ConsoleKey.W)
            {
                var start = cursor;
                while (start > 0 && buffer[start - 1] == ' ')
                {
                    start--;
                }

                while (start > 0 && buffer[start - 1] != ' ')
                {
                    start--;
                }

                buffer.Remove(start, cursor - start);
                cursor = start;
            }
            else if (ctrl && key.Key == ConsoleKey.L)
            {
                _out.Write("\u001b[2J\u001b[H");
            }
            else
            {
                switch (key.Key)
                {
                    case ConsoleKey.Backspace:
                        if (cursor > 0)
                        {
                            buffer.Remove(cursor - 1, 1);
                            cursor--;
                        }

                        break;
                    case ConsoleKey.Delete:
                        if (cursor < buffer.Length)
                        {
                            buffer.Remove(cursor, 1);
                        }

                        break;
                    case ConsoleKey.LeftArrow:
                        cursor = Math.Max(0, cursor - 1);
                        break;
                    case ConsoleKey.RightArrow:
                        cursor = Math.Min(buffer.Length, cursor + 1);
                        break;
                    case ConsoleKey.Home:
                        cursor = 0;
                        break;
                    case ConsoleKey.End:
                        cursor = buffer.Length;
                        break;
                    case ConsoleKey.Escape:
                        buffer.Clear();
                        cursor = 0;
                        break;
                    case ConsoleKey.UpArrow:
                        if (historyIndex > 0)
                        {
                            if (historyIndex == _history.Count)
                            {
                                draft = buffer.ToString();
                            }

                            historyIndex--;
                            Replace(_history[historyIndex]);
                        }

                        break;
                    case ConsoleKey.DownArrow:
                        if (historyIndex < _history.Count)
                        {
                            historyIndex++;
                            Replace(historyIndex == _history.Count ? draft : _history[historyIndex]);
                        }

                        break;
                    case ConsoleKey.Tab:
                        Complete(buffer, ref cursor, prompt, Redraw);
                        break;
                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            buffer.Insert(cursor, key.KeyChar);
                            cursor++;
                        }

                        break;
                }
            }

            Redraw();
        }
    }

    private void Complete(StringBuilder buffer, ref int cursor, string prompt, Action redraw)
    {
        var completion = _complete(buffer.ToString(), cursor);
        if (completion is null || completion.Candidates.Count == 0)
        {
            return;
        }

        var typed = buffer.ToString(completion.Start, cursor - completion.Start);
        var common = CommonPrefix(completion.Candidates);
        if (common.Length > typed.Length || completion.Candidates.Count == 1)
        {
            buffer.Remove(completion.Start, cursor - completion.Start);
            buffer.Insert(completion.Start, common);
            cursor = completion.Start + common.Length;
            return;
        }

        // Several candidates and nothing more to add: list them below the line, then redraw it.
        _out.Write("\r\n");
        _out.Write(string.Join("  ", completion.Candidates));
        _out.Write("\r\n");
        redraw();
    }

    private static string CommonPrefix(IReadOnlyList<string> words)
    {
        var prefix = words[0];
        foreach (var word in words)
        {
            var length = 0;
            while (length < prefix.Length && length < word.Length && prefix[length] == word[length])
            {
                length++;
            }

            prefix = prefix[..length];
        }

        return prefix;
    }
}
