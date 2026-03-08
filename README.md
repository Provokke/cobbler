<div align="center">

# cobbler

A small command-line argument parser for .NET. Describe your commands, options, flags and arguments with a fluent API and cobbler parses `args`, converts values, writes the help text and reports mistakes with something useful. No dependencies.

![License](https://img.shields.io/badge/LICENSE-MIT-blue?style=for-the-badge)  ![Language](https://img.shields.io/badge/C%23-512bd4?style=for-the-badge&logo=dotnet&logoColor=white)  ![Tests](https://img.shields.io/badge/TESTS-140-brightgreen?style=for-the-badge)

Refer to [LICENSE](LICENSE) for the legal bits.

</div>

## What's in it

- **Commands** in a tree, with aliases, hidden commands and as much nesting as you like. A command that only groups others gets a "a command is required" error instead of silently doing nothing.
- **Flags and value options** in every style people type: `--name value`, `--name=value`, `-n value`, `-nvalue`, `-n=value`, and clustered short flags like `-vfa` or `-vn5`. Flags can be counted (`-vvv`).
- **Positional arguments**, required or optional, with one variadic argument at the end. `--` stops option parsing, and a lone `-` is just an argument.
- **Global options** that are inherited by every subcommand underneath, and work before or after the subcommand name.
- **Defaults, required options, repeatable options and choices**, all checked during parsing.
- **Typed access**: `Get<int>("count")`, `GetAll<T>`, `Argument<T>`, with conversion for numbers (culture-invariant), booleans (`true/yes/on/1`), enums, `Guid`, `TimeSpan`, `DateOnly`, `FileInfo` and friends. A bad value becomes a `ParseException` that names the option.
- **Help generation**: usage line, argument, command and option tables aligned in columns, wrapped to a configurable width, with defaults, choices and examples shown. `--help` and `-h` come for free, plus `--version` once you set one.
- **Helpful errors**: unknown options and commands suggest the closest match ("did you mean '--verbose'?"). Usage mistakes go to stderr with exit code 2; handlers can throw `CommandException` to choose their own exit code.
- **Builder checks** that fail early when you wire things up wrongly: duplicate names, a required argument after an optional one, anything after a variadic argument.

```csharp
using Cobbler;

var app = new CommandApp("greeter", "Says hello in a few ways").Version("1.0.0");
app.Root.Flag("shout", "Use capitals", 's', global: true);

app.Add(new Command("hello", "Greet someone")
    .Argument("name", "Who to greet")
    .Option("times", "How many greetings", 'n', "N", defaultValue: "1")
    .Handle(r =>
    {
        for (var i = 0; i < r.Get<int>("times"); i++)
            Console.WriteLine(r.Flag("shout") ? $"HELLO, {r.Argument("name")!.ToUpper()}!" : $"Hello, {r.Argument("name")}!");
    }));

return app.Run(args);
```

## 🏗️ Architecture

```
 string[] args
      │
      ▼
 Parser ──── walks the Command tree, resolves subcommands, collects options ───► ParseResult
      │                                                                              │
      ├─ ParseException (kind, subject, command)                                     ├─ Flag / Count / Value / Values
      └─ Suggestions (edit distance)                                                 ├─ Get<T> / GetAll<T> ── ValueConverter
                                                                                     └─ Argument / ArgumentValues
 CommandApp.Run ── Parser ── handler ── exit code        HelpWriter ── Command tree ── text
```

- **Command** is the builder: options, arguments, subcommands, aliases, examples and a handler. It checks what you give it as you go.
- **OptionSpec** and **ArgumentSpec** describe one option or argument. An option is either a flag or takes a value.
- **Parser** does a single pass over the arguments. Subcommands are resolved as it goes, so options for the parent can appear before the subcommand name only if they are global. Once the command is known it applies defaults, checks required options and assigns positionals.
- **ParseResult** is what your handler sees. Asking for a name the command doesn't declare throws immediately, so a typo in a handler doesn't quietly return nothing.
- **ValueConverter** turns strings into the types you ask for and reports failures as `ParseException`.
- **HelpWriter** renders help for any command in the tree.
- **CommandApp** is the root command plus `Run`, which wires parsing, help, version, exit codes and the output streams together. You can pass your own `TextWriter`s, which is how the tests check output.

## 🚀 Quick start

```
dotnet build
dotnet test
dotnet run --project samples/Greeter -- hello Josh -n 2 --style fancy
```

The sample app in `samples/Greeter`:

```
$ greeter hello Josh -n 2 --style fancy
Good day to you, Josh.
Good day to you, Josh.

$ greeter hello --style loud x
error: invalid value 'loud' for --style: expected one of plain, fancy
Try 'greeter hello --help' for usage.

$ greeter hellp
error: unknown command 'hellp' (did you mean 'hello'?)
Try 'greeter --help' for usage.

$ greeter hello --help
Greet someone

Usage:
  greeter hello [options] <name>

Arguments:
  <name>  Who to greet

Options:
  -n, --times <N>      How many greetings (default: 1)
      --style <value>  Tone of voice (choices: plain, fancy; default: plain)
  -h, --help           Show this help and exit

Global options:
  -s, --shout  Use capitals

Examples:
  greeter hello Josh -n 2 --style fancy
```

## 🧪 Testing

140 xUnit tests, all passing:

- `ParserTests` (41): every option style, clusters, counting, defaults, repeatable and duplicate options, `--`, negative numbers, error kinds and subjects, suggestions, required options and arguments, choices, variadics, and the help flag behaviour.
- `ConversionTests` (27): integers, overflow, doubles under a German culture, booleans, enums (including undefined numeric values), `Guid`, `TimeSpan`, dates, files, nullables, fallbacks and error messages.
- `BuilderTests` (23): name validation, duplicate and ordering rules, aliases, parent rules, edit distance and suggestions.
- `CommandTreeTests` (18): nested commands, aliases, global options at every depth, non-global parent options, unknown-command suggestions and group commands.
- `AppTests` (18): exit codes, stdout against stderr, `CommandException`, version, help, handlers, and the console fallback.
- `HelpTests` (13): exact help output for the root and a subcommand, hidden items, wrapping, long left columns, and overridden help options.
