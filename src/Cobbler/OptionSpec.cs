namespace Cobbler;

public sealed class OptionSpec
{
    public OptionSpec(string name, string description = "", char? shortName = null)
    {
        Name = name;
        Description = description;
        Short = shortName;
    }

    public string Name { get; }

    public string Description { get; }

    public char? Short { get; }

    public bool IsFlag { get; init; }

    public string ValueName { get; init; } = "value";

    public bool Required { get; init; }

    public bool Repeatable { get; init; }

    public string? Default { get; init; }

    public IReadOnlyList<string>? Choices { get; init; }

    public bool Global { get; init; }

    public bool Hidden { get; init; }

    public string Display => "--" + Name;
}

public sealed class ArgumentSpec
{
    public ArgumentSpec(string name, string description = "", bool required = true, bool variadic = false)
    {
        Name = name;
        Description = description;
        Required = required;
        Variadic = variadic;
    }

    public string Name { get; }

    public string Description { get; }

    public bool Required { get; }

    public bool Variadic { get; }
}
