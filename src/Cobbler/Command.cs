using System.Text.RegularExpressions;

namespace Cobbler;

public class Command
{
    private static readonly Regex NamePattern = new("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.Compiled);

    private readonly List<OptionSpec> options = new();
    private readonly List<ArgumentSpec> arguments = new();
    private readonly List<Command> subcommands = new();
    private readonly List<string> aliases = new();
    private readonly List<string> examples = new();

    public Command(string name, string description = "")
    {
        if (!NamePattern.IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a valid command name", nameof(name));
        }

        Name = name;
        Description = description;
    }

    public string Name { get; }

    public string Description { get; }

    public bool Hidden { get; set; }

    public Command? Parent { get; private set; }

    public Func<ParseResult, int>? Handler { get; private set; }

    public IReadOnlyList<OptionSpec> Options => options;

    public IReadOnlyList<ArgumentSpec> Arguments => arguments;

    public IReadOnlyList<Command> Subcommands => subcommands;

    public IReadOnlyList<string> Aliases => aliases;

    public IReadOnlyList<string> Examples => examples;

    public Command Alias(params string[] names)
    {
        foreach (var name in names)
        {
            if (!NamePattern.IsMatch(name))
            {
                throw new ArgumentException($"'{name}' is not a valid alias", nameof(names));
            }

            aliases.Add(name);
        }

        return this;
    }

    public Command Example(string line)
    {
        examples.Add(line);
        return this;
    }

    public Command Flag(string name, string description = "", char? shortName = null, bool global = false)
    {
        return Add(new OptionSpec(name, description, shortName) { IsFlag = true, Global = global });
    }

    public Command Option(
        string name,
        string description = "",
        char? shortName = null,
        string valueName = "value",
        string? defaultValue = null,
        bool required = false,
        bool repeatable = false,
        IEnumerable<string>? choices = null,
        bool global = false)
    {
        return Add(new OptionSpec(name, description, shortName)
        {
            ValueName = valueName,
            Default = defaultValue,
            Required = required,
            Repeatable = repeatable,
            Choices = choices?.ToArray(),
            Global = global,
        });
    }

    public Command Add(OptionSpec option)
    {
        if (!NamePattern.IsMatch(option.Name))
        {
            throw new ArgumentException($"'{option.Name}' is not a valid option name", nameof(option));
        }

        if (option.Short is { } s && !char.IsLetterOrDigit(s))
        {
            throw new ArgumentException($"'{s}' is not a valid short option", nameof(option));
        }

        if (options.Any(o => o.Name == option.Name))
        {
            throw new InvalidOperationException($"option '--{option.Name}' is already defined on '{Name}'");
        }

        if (option.Short is { } sc && options.Any(o => o.Short == sc))
        {
            throw new InvalidOperationException($"option '-{sc}' is already defined on '{Name}'");
        }

        options.Add(option);
        return this;
    }

    public Command Argument(string name, string description = "", bool required = true, bool variadic = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("argument name is empty", nameof(name));
        }

        if (arguments.Count > 0)
        {
            var last = arguments[^1];
            if (last.Variadic)
            {
                throw new InvalidOperationException($"argument '{name}' cannot follow the variadic argument '{last.Name}'");
            }

            if (!last.Required && required)
            {
                throw new InvalidOperationException($"required argument '{name}' cannot follow optional argument '{last.Name}'");
            }
        }

        if (arguments.Any(a => a.Name == name))
        {
            throw new InvalidOperationException($"argument '{name}' is already defined on '{Name}'");
        }

        arguments.Add(new ArgumentSpec(name, description, required, variadic));
        return this;
    }

    public Command Add(Command subcommand)
    {
        if (subcommand.Parent != null)
        {
            throw new InvalidOperationException($"command '{subcommand.Name}' already has a parent");
        }

        var names = subcommand.aliases.Prepend(subcommand.Name);
        foreach (var name in names)
        {
            if (FindSubcommand(name) != null)
            {
                throw new InvalidOperationException($"command '{name}' is already defined under '{Name}'");
            }
        }

        subcommand.Parent = this;
        subcommands.Add(subcommand);
        return this;
    }

    public Command Handle(Func<ParseResult, int> handler)
    {
        Handler = handler;
        return this;
    }

    public Command Handle(Action<ParseResult> handler)
    {
        Handler = result =>
        {
            handler(result);
            return 0;
        };
        return this;
    }

    public Command? FindSubcommand(string name)
    {
        return subcommands.FirstOrDefault(c => c.Name == name || c.aliases.Contains(name));
    }

    public IReadOnlyList<Command> PathFromRoot()
    {
        var path = new List<Command>();
        for (var c = this; c != null; c = c.Parent)
        {
            path.Insert(0, c);
        }

        return path;
    }

    internal IEnumerable<OptionSpec> VisibleOptions()
    {
        var seen = new HashSet<string>();
        foreach (var o in options)
        {
            if (seen.Add(o.Name))
            {
                yield return o;
            }
        }

        for (var p = Parent; p != null; p = p.Parent)
        {
            foreach (var o in p.options.Where(o => o.Global))
            {
                if (seen.Add(o.Name))
                {
                    yield return o;
                }
            }
        }
    }
}
