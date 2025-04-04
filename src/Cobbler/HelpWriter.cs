using System.Text;

namespace Cobbler;

public static class HelpWriter
{
    private const int MaxLeftColumn = 30;

    public static string Render(Command command, string? version = null, int width = 80)
    {
        var writer = new StringWriter { NewLine = "\n" };
        Write(writer, command, version, width);
        return writer.ToString();
    }

    public static void Write(TextWriter output, Command command, string? version = null, int width = 80)
    {
        var path = command.PathFromRoot();
        var isRoot = path.Count == 1;

        if (isRoot && version != null)
        {
            output.WriteLine($"{command.Name} {version}");
        }

        if (command.Description.Length > 0)
        {
            foreach (var line in Wrap(command.Description, width))
            {
                output.WriteLine(line);
            }
        }

        if (isRoot && version != null || command.Description.Length > 0)
        {
            output.WriteLine();
        }

        output.WriteLine("Usage:");
        output.WriteLine("  " + UsageLine(command, path));

        var rows = new List<(string Left, string Right)>();

        if (command.Arguments.Count > 0)
        {
            rows.Clear();
            foreach (var a in command.Arguments)
            {
                rows.Add((ArgumentToken(a), a.Description));
            }

            Section(output, "Arguments:", rows, width);
        }

        var visibleSubcommands = command.Subcommands.Where(c => !c.Hidden).ToList();
        if (visibleSubcommands.Count > 0)
        {
            rows.Clear();
            foreach (var c in visibleSubcommands)
            {
                var names = string.Join(", ", c.Aliases.Prepend(c.Name));
                rows.Add((names, c.Description));
            }

            Section(output, "Commands:", rows, width);
        }

        var own = command.Options.Where(o => !o.Hidden).ToList();
        rows.Clear();
        foreach (var o in own)
        {
            rows.Add((OptionLeft(o), OptionDescription(o)));
        }

        if (!command.Options.Any(o => o.Name == "help"))
        {
            rows.Add((OptionLeft(Parser.HelpOption, !own.Any(o => o.Short == 'h')), Parser.HelpOption.Description));
        }

        if (isRoot && version != null)
        {
            rows.Add((OptionLeft(Parser.VersionOption), Parser.VersionOption.Description));
        }

        Section(output, "Options:", rows, width);

        var inherited = command.VisibleOptions().Where(o => !o.Hidden && !command.Options.Contains(o)).ToList();
        if (inherited.Count > 0)
        {
            rows.Clear();
            foreach (var o in inherited)
            {
                rows.Add((OptionLeft(o), OptionDescription(o)));
            }

            Section(output, "Global options:", rows, width);
        }

        if (command.Examples.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Examples:");
            foreach (var e in command.Examples)
            {
                output.WriteLine("  " + e);
            }
        }
    }

    private static string UsageLine(Command command, IReadOnlyList<Command> path)
    {
        var parts = new List<string> { string.Join(" ", path.Select(c => c.Name)) };
        if (command.Subcommands.Any(c => !c.Hidden))
        {
            parts.Add("<command>");
        }

        parts.Add("[options]");
        parts.AddRange(command.Arguments.Select(ArgumentToken));
        return string.Join(" ", parts);
    }

    private static string ArgumentToken(ArgumentSpec a)
    {
        var name = a.Variadic ? a.Name + "..." : a.Name;
        return a.Required ? $"<{name}>" : $"[{name}]";
    }

    private static string OptionLeft(OptionSpec o, bool allowShort = true)
    {
        var shortPart = o.Short is { } s && allowShort ? $"-{s}, " : "    ";
        var value = o.IsFlag ? "" : $" <{o.ValueName}>";
        return shortPart + "--" + o.Name + value;
    }

    private static string OptionDescription(OptionSpec o)
    {
        var text = new StringBuilder(o.Description);
        var notes = new List<string>();
        if (o.Choices != null)
        {
            notes.Add("choices: " + string.Join(", ", o.Choices));
        }

        if (o.Default != null)
        {
            notes.Add("default: " + o.Default);
        }

        if (o.Required && !o.IsFlag)
        {
            notes.Add("required");
        }

        if (o.Repeatable)
        {
            notes.Add("repeatable");
        }

        if (notes.Count > 0)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append('(').Append(string.Join("; ", notes)).Append(')');
        }

        return text.ToString();
    }

    private static void Section(TextWriter output, string title, List<(string Left, string Right)> rows, int width)
    {
        if (rows.Count == 0)
        {
            return;
        }

        output.WriteLine();
        output.WriteLine(title);
        var left = Math.Min(rows.Max(r => r.Left.Length), MaxLeftColumn);
        const int indent = 2;
        const int gap = 2;
        var descWidth = Math.Max(20, width - indent - left - gap);
        foreach (var (l, r) in rows)
        {
            var lines = Wrap(r, descWidth);
            var padding = new string(' ', indent);
            if (l.Length > left)
            {
                output.WriteLine(padding + l);
                foreach (var line in lines)
                {
                    output.WriteLine(padding + new string(' ', left + gap) + line);
                }

                continue;
            }

            output.WriteLine((padding + l.PadRight(left + gap) + (lines.Count > 0 ? lines[0] : "")).TrimEnd());
            foreach (var line in lines.Skip(1))
            {
                output.WriteLine(padding + new string(' ', left + gap) + line);
            }
        }
    }

    internal static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }
}
