using System.Text.RegularExpressions;

namespace Cobbler;

public sealed class Parser
{
    private static readonly Regex NegativeNumber = new(@"^-\d+(\.\d+)?$", RegexOptions.Compiled);

    internal static readonly OptionSpec HelpOption = new("help", "Show this help and exit", 'h') { IsFlag = true };

    internal static readonly OptionSpec VersionOption = new("version", "Show the version and exit") { IsFlag = true };

    private readonly Command root;
    private readonly string? version;
    private Command current;
    private readonly Dictionary<OptionSpec, List<string>> given = new();
    private readonly Dictionary<OptionSpec, int> seen = new();
    private readonly List<string> positionals = new();
    private bool help;
    private bool versionRequested;

    private Parser(Command root, string? version)
    {
        this.root = root;
        this.version = version;
        current = root;
    }

    public static ParseResult Parse(Command root, IReadOnlyList<string> args, string? version = null)
    {
        return new Parser(root, version).Run(args);
    }

    internal static bool LooksLikeOption(string token)
    {
        return token.Length > 1 && token[0] == '-' && !NegativeNumber.IsMatch(token);
    }

    private ParseResult Run(IReadOnlyList<string> args)
    {
        try
        {
            Scan(args);
            return Finish();
        }
        catch (ParseException e)
        {
            e.Command ??= current;
            throw;
        }
    }

    private void Scan(IReadOnlyList<string> args)
    {
        var onlyPositionals = false;
        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (onlyPositionals)
            {
                positionals.Add(token);
            }
            else if (token == "--")
            {
                onlyPositionals = true;
            }
            else if (token.StartsWith("--", StringComparison.Ordinal))
            {
                i = ParseLong(args, i);
            }
            else if (LooksLikeOption(token))
            {
                i = ParseShortCluster(args, i);
            }
            else
            {
                Positional(token);
            }
        }
    }

    private void Positional(string token)
    {
        if (positionals.Count == 0 && current.Subcommands.Count > 0)
        {
            var sub = current.FindSubcommand(token);
            if (sub != null)
            {
                var inherited = sub.VisibleOptions().ToHashSet();
                foreach (var used in seen.Keys.Where(o => !inherited.Contains(o)))
                {
                    throw new ParseException(
                        ParseErrorKind.UnknownOption,
                        $"option '{used.Display}' cannot be used before '{sub.Name}'",
                        used.Display);
                }

                current = sub;
                return;
            }

            if (current.Arguments.Count == 0)
            {
                var names = current.Subcommands.Where(c => !c.Hidden).SelectMany(c => c.Aliases.Prepend(c.Name));
                throw new ParseException(
                    ParseErrorKind.UnknownCommand,
                    $"unknown command '{token}'" + DidYouMean(token, names),
                    token);
            }
        }

        positionals.Add(token);
    }

    private OptionSpec? FindLong(string name)
    {
        foreach (var o in current.VisibleOptions())
        {
            if (o.Name == name)
            {
                return o;
            }
        }

        if (name == HelpOption.Name)
        {
            return HelpOption;
        }

        return name == VersionOption.Name && version != null && current == root ? VersionOption : null;
    }

    private OptionSpec? FindShort(char c)
    {
        foreach (var o in current.VisibleOptions())
        {
            if (o.Short == c)
            {
                return o;
            }
        }

        return c == HelpOption.Short && !current.VisibleOptions().Any(o => o.Name == "help") ? HelpOption : null;
    }

    private int ParseLong(IReadOnlyList<string> args, int index)
    {
        var token = args[index];
        var body = token[2..];
        string? inline = null;
        var eq = body.IndexOf('=');
        if (eq >= 0)
        {
            inline = body[(eq + 1)..];
            body = body[..eq];
        }

        var spec = FindLong(body);
        if (spec == null)
        {
            var names = current.VisibleOptions().Where(o => !o.Hidden).Select(o => o.Name).Append("help");
            throw new ParseException(
                ParseErrorKind.UnknownOption,
                $"unknown option '--{body}'" + DidYouMean(body, names, "--"),
                "--" + body);
        }

        if (spec.IsFlag)
        {
            if (inline != null)
            {
                throw new ParseException(
                    ParseErrorKind.UnexpectedValue,
                    $"option '--{spec.Name}' does not take a value",
                    spec.Display);
            }

            Record(spec, null);
            return index;
        }

        if (inline != null)
        {
            Record(spec, inline);
            return index;
        }

        if (index + 1 < args.Count && !LooksLikeOption(args[index + 1]) && args[index + 1] != "--")
        {
            Record(spec, args[index + 1]);
            return index + 1;
        }

        throw new ParseException(ParseErrorKind.MissingValue, $"option '--{spec.Name}' needs a value", spec.Display);
    }

    private int ParseShortCluster(IReadOnlyList<string> args, int index)
    {
        var token = args[index];
        for (var pos = 1; pos < token.Length; pos++)
        {
            var c = token[pos];
            var spec = FindShort(c);
            if (spec == null)
            {
                throw new ParseException(ParseErrorKind.UnknownOption, $"unknown option '-{c}'", "-" + c);
            }

            if (spec.IsFlag)
            {
                Record(spec, null);
                continue;
            }

            var rest = token[(pos + 1)..];
            if (rest.Length > 0)
            {
                Record(spec, rest.StartsWith('=') ? rest[1..] : rest);
                return index;
            }

            if (index + 1 < args.Count && !LooksLikeOption(args[index + 1]) && args[index + 1] != "--")
            {
                Record(spec, args[index + 1]);
                return index + 1;
            }

            throw new ParseException(ParseErrorKind.MissingValue, $"option '-{c}' needs a value", "-" + c);
        }

        return index;
    }

    private void Record(OptionSpec spec, string? value)
    {
        if (ReferenceEquals(spec, HelpOption))
        {
            help = true;
            return;
        }

        if (ReferenceEquals(spec, VersionOption))
        {
            versionRequested = true;
            return;
        }

        seen[spec] = seen.GetValueOrDefault(spec) + 1;
        if (spec.IsFlag)
        {
            return;
        }

        if (!given.TryGetValue(spec, out var list))
        {
            list = new List<string>();
            given[spec] = list;
        }

        if (list.Count > 0 && !spec.Repeatable)
        {
            throw new ParseException(
                ParseErrorKind.DuplicateOption,
                $"option '--{spec.Name}' was given more than once",
                spec.Display);
        }

        if (spec.Choices != null && !spec.Choices.Contains(value!))
        {
            throw new ParseException(
                ParseErrorKind.InvalidValue,
                $"invalid value '{value}' for --{spec.Name}: expected one of {string.Join(", ", spec.Choices)}",
                spec.Display);
        }

        list.Add(value!);
    }

    private ParseResult Finish()
    {
        var specs = new Dictionary<string, OptionSpec>();
        foreach (var o in current.VisibleOptions())
        {
            specs[o.Name] = o;
        }

        var values = new Dictionary<string, List<string>>();
        var counts = new Dictionary<string, int>();
        var explicitNames = new HashSet<string>();
        var arguments = current.Arguments.ToDictionary(a => a.Name, _ => new List<string>());

        if (help || versionRequested)
        {
            return new ParseResult(current, specs, values, counts, explicitNames, arguments, help, versionRequested && !help);
        }

        if (current.Subcommands.Count > 0 && current.Handler == null && current.Arguments.Count == 0)
        {
            throw new ParseException(ParseErrorKind.MissingCommand, "a command is required");
        }

        foreach (var spec in specs.Values)
        {
            seen.TryGetValue(spec, out var count);
            counts[spec.Name] = count;
            if (count > 0)
            {
                explicitNames.Add(spec.Name);
            }

            if (spec.IsFlag)
            {
                continue;
            }

            if (given.TryGetValue(spec, out var list) && list.Count > 0)
            {
                values[spec.Name] = list;
            }
            else if (spec.Default != null)
            {
                values[spec.Name] = new List<string> { spec.Default };
            }
            else if (spec.Required)
            {
                throw new ParseException(
                    ParseErrorKind.MissingOption,
                    $"option '--{spec.Name}' is required",
                    spec.Display);
            }
        }

        AssignArguments(arguments);
        return new ParseResult(current, specs, values, counts, explicitNames, arguments, false, false);
    }

    private void AssignArguments(Dictionary<string, List<string>> arguments)
    {
        var next = 0;
        foreach (var arg in current.Arguments)
        {
            if (arg.Variadic)
            {
                arguments[arg.Name].AddRange(positionals.Skip(next));
                next = positionals.Count;
                if (arg.Required && arguments[arg.Name].Count == 0)
                {
                    throw MissingArgument(arg);
                }

                continue;
            }

            if (next < positionals.Count)
            {
                arguments[arg.Name].Add(positionals[next++]);
            }
            else if (arg.Required)
            {
                throw MissingArgument(arg);
            }
        }

        if (next < positionals.Count)
        {
            var extra = positionals[next];
            throw new ParseException(
                ParseErrorKind.UnexpectedArgument,
                $"unexpected argument '{extra}'",
                extra);
        }
    }

    private static ParseException MissingArgument(ArgumentSpec arg)
    {
        return new ParseException(
            ParseErrorKind.MissingArgument,
            $"missing required argument <{arg.Name}>",
            arg.Name);
    }

    private static string DidYouMean(string input, IEnumerable<string> candidates, string prefix = "")
    {
        var hit = Suggestions.Closest(input, candidates);
        return hit == null ? "" : $" (did you mean '{prefix}{hit}'?)";
    }
}
