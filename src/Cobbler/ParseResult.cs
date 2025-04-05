namespace Cobbler;

public sealed class ParseResult
{
    private readonly Dictionary<string, List<string>> values;
    private readonly Dictionary<string, int> counts;
    private readonly HashSet<string> explicitNames;
    private readonly Dictionary<string, OptionSpec> specs;
    private readonly Dictionary<string, List<string>> arguments;

    internal ParseResult(
        Command command,
        Dictionary<string, OptionSpec> specs,
        Dictionary<string, List<string>> values,
        Dictionary<string, int> counts,
        HashSet<string> explicitNames,
        Dictionary<string, List<string>> arguments,
        bool helpRequested,
        bool versionRequested)
    {
        Command = command;
        this.specs = specs;
        this.values = values;
        this.counts = counts;
        this.explicitNames = explicitNames;
        this.arguments = arguments;
        HelpRequested = helpRequested;
        VersionRequested = versionRequested;
    }

    public Command Command { get; }

    public IReadOnlyList<string> CommandPath => Command.PathFromRoot().Select(c => c.Name).ToArray();

    public bool HelpRequested { get; }

    public bool VersionRequested { get; }

    public bool Flag(string name)
    {
        return Count(name) > 0;
    }

    public int Count(string name)
    {
        Require(name);
        return counts.TryGetValue(name, out var n) ? n : 0;
    }

    public bool Has(string name)
    {
        Require(name);
        return explicitNames.Contains(name);
    }

    public string? Value(string name)
    {
        var all = Values(name);
        return all.Count == 0 ? null : all[^1];
    }

    public IReadOnlyList<string> Values(string name)
    {
        Require(name);
        return values.TryGetValue(name, out var list) ? list : Array.Empty<string>();
    }

    public T Get<T>(string name)
    {
        var raw = Value(name) ?? throw new InvalidOperationException($"option '--{name}' has no value");
        return ValueConverter.Convert<T>(raw, "--" + name);
    }

    public T Get<T>(string name, T fallback)
    {
        var raw = Value(name);
        return raw == null ? fallback : ValueConverter.Convert<T>(raw, "--" + name);
    }

    public IReadOnlyList<T> GetAll<T>(string name)
    {
        return Values(name).Select(v => ValueConverter.Convert<T>(v, "--" + name)).ToArray();
    }

    public string? Argument(string name)
    {
        var all = ArgumentValues(name);
        return all.Count == 0 ? null : all[0];
    }

    public IReadOnlyList<string> ArgumentValues(string name)
    {
        if (!arguments.TryGetValue(name, out var list))
        {
            throw new ArgumentException($"'{name}' is not an argument of '{Command.Name}'", nameof(name));
        }

        return list;
    }

    public T Argument<T>(string name)
    {
        var raw = Argument(name) ?? throw new InvalidOperationException($"argument '{name}' has no value");
        return ValueConverter.Convert<T>(raw, $"<{name}>");
    }

    public IReadOnlyList<T> ArgumentValues<T>(string name)
    {
        return ArgumentValues(name).Select(v => ValueConverter.Convert<T>(v, $"<{name}>")).ToArray();
    }

    private void Require(string name)
    {
        if (!specs.ContainsKey(name))
        {
            throw new ArgumentException($"'{name}' is not an option of '{Command.Name}'", nameof(name));
        }
    }
}
