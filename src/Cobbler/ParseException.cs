namespace Cobbler;

public enum ParseErrorKind
{
    UnknownOption,
    UnknownCommand,
    MissingValue,
    UnexpectedValue,
    MissingOption,
    MissingArgument,
    UnexpectedArgument,
    InvalidValue,
    DuplicateOption,
    MissingCommand,
}

public sealed class ParseException : Exception
{
    public ParseException(ParseErrorKind kind, string message, string? subject = null, Command? command = null)
        : base(message)
    {
        Kind = kind;
        Subject = subject;
        Command = command;
    }

    public ParseErrorKind Kind { get; }

    public string? Subject { get; }

    public Command? Command { get; internal set; }
}

public sealed class CommandException : Exception
{
    public CommandException(string message, int exitCode = 1)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
