namespace Cobbler;

public sealed class CommandApp
{
    public const int UsageErrorExitCode = 2;

    public CommandApp(string name, string description = "")
    {
        Root = new Command(name, description);
    }

    public Command Root { get; }

    public string? VersionText { get; private set; }

    public int HelpWidth { get; set; } = 80;

    public CommandApp Version(string version)
    {
        VersionText = version;
        return this;
    }

    public CommandApp Add(Command command)
    {
        Root.Add(command);
        return this;
    }

    public ParseResult Parse(IReadOnlyList<string> args)
    {
        return Parser.Parse(Root, args, VersionText);
    }

    public int Run(IReadOnlyList<string> args, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        ParseResult result;
        try
        {
            result = Parse(args);
        }
        catch (ParseException e)
        {
            var command = e.Command ?? Root;
            var path = string.Join(" ", command.PathFromRoot().Select(c => c.Name));
            error.WriteLine($"error: {e.Message}");
            error.WriteLine($"Try '{path} --help' for usage.");
            return UsageErrorExitCode;
        }

        if (result.HelpRequested)
        {
            HelpWriter.Write(output, result.Command, VersionText, HelpWidth);
            return 0;
        }

        if (result.VersionRequested)
        {
            output.WriteLine($"{Root.Name} {VersionText}");
            return 0;
        }

        var handler = result.Command.Handler;
        if (handler == null)
        {
            HelpWriter.Write(output, result.Command, VersionText, HelpWidth);
            return 0;
        }

        try
        {
            return handler(result);
        }
        catch (CommandException e)
        {
            error.WriteLine($"error: {e.Message}");
            return e.ExitCode;
        }
        catch (ParseException e)
        {
            error.WriteLine($"error: {e.Message}");
            return UsageErrorExitCode;
        }
    }
}
