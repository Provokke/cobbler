using Cobbler;

namespace Cobbler.Tests;

public class AppTests
{
    private sealed class Run
    {
        public int Code;
        public string Out = "";
        public string Err = "";
    }

    private static Run Execute(CommandApp app, params string[] args)
    {
        var o = new StringWriter { NewLine = "\n" };
        var e = new StringWriter { NewLine = "\n" };
        var code = app.Run(args, o, e);
        return new Run { Code = code, Out = o.ToString(), Err = e.ToString() };
    }

    private static CommandApp App(List<string>? log = null)
    {
        log ??= new List<string>();
        var app = new CommandApp("calc", "A tiny calculator").Version("0.3.0");
        app.Add(new Command("add", "Add numbers")
            .Argument("numbers", "Numbers to add", variadic: true)
            .Option("precision", "Decimal places", 'p', defaultValue: "0")
            .Handle(r =>
            {
                var sum = r.ArgumentValues<double>("numbers").Sum();
                log.Add($"add:{sum}");
                return 0;
            }));
        app.Add(new Command("fail", "Always fails").Handle(_ => throw new CommandException("something broke", 7)));
        app.Add(new Command("exit", "Returns its argument").Argument("code").Handle(r => r.Argument<int>("code")));
        app.Add(new Command("lazy", "Converts late").Option("n").Handle(r => r.Get<int>("n")));
        return app;
    }

    [Fact]
    public void HandlerRunsAndReturnsZero()
    {
        var log = new List<string>();
        var run = Execute(App(log), "add", "1", "2", "3.5");
        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "add:6.5" }, log);
        Assert.Equal("", run.Err);
    }

    [Fact]
    public void HandlerExitCodeIsPassedThrough()
    {
        Assert.Equal(42, Execute(App(), "exit", "42").Code);
    }

    [Fact]
    public void CommandExceptionBecomesAnErrorLine()
    {
        var run = Execute(App(), "fail");
        Assert.Equal(7, run.Code);
        Assert.Equal("error: something broke\n", run.Err);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void ParseErrorIsReportedWithAHint()
    {
        var run = Execute(App(), "add", "--bogus");
        Assert.Equal(CommandApp.UsageErrorExitCode, run.Code);
        Assert.Equal("error: unknown option '--bogus'\nTry 'calc add --help' for usage.\n", run.Err);
        Assert.Equal("", run.Out);
    }

    [Fact]
    public void UnknownCommandIsAUsageError()
    {
        var run = Execute(App(), "mulitply");
        Assert.Equal(2, run.Code);
        Assert.StartsWith("error: unknown command 'mulitply'", run.Err);
        Assert.Contains("Try 'calc --help' for usage.", run.Err);
    }

    [Fact]
    public void MissingCommandIsAUsageError()
    {
        var run = Execute(App());
        Assert.Equal(2, run.Code);
        Assert.StartsWith("error: a command is required", run.Err);
    }

    [Fact]
    public void ConversionFailureInAHandlerIsAUsageError()
    {
        var run = Execute(App(), "lazy", "--n", "abc");
        Assert.Equal(2, run.Code);
        Assert.Contains("invalid value 'abc' for --n", run.Err);
    }

    [Fact]
    public void HelpGoesToStdoutWithExitZero()
    {
        var run = Execute(App(), "--help");
        Assert.Equal(0, run.Code);
        Assert.StartsWith("calc 0.3.0\nA tiny calculator\n", run.Out);
        Assert.Contains("add", run.Out);
        Assert.Equal("", run.Err);
    }

    [Fact]
    public void SubcommandHelp()
    {
        var run = Execute(App(), "add", "-h");
        Assert.Equal(0, run.Code);
        Assert.Contains("calc add [options] <numbers...>", run.Out);
        Assert.Contains("(default: 0)", run.Out);
    }

    [Fact]
    public void HelpBeatsAnInvalidRemainder()
    {
        var run = Execute(App(), "exit", "--help");
        Assert.Equal(0, run.Code);
        Assert.Contains("<code>", run.Out);
    }

    [Fact]
    public void VersionIsPrinted()
    {
        var run = Execute(App(), "--version");
        Assert.Equal(0, run.Code);
        Assert.Equal("calc 0.3.0\n", run.Out);
    }

    [Fact]
    public void VersionIsUnknownWhenNotConfigured()
    {
        var app = new CommandApp("t").Add(new Command("x").Handle(_ => { }));
        var run = Execute(app, "--version");
        Assert.Equal(2, run.Code);
        Assert.Contains("unknown option '--version'", run.Err);
    }

    [Fact]
    public void VersionIsOnlyForTheRoot()
    {
        var run = Execute(App(), "add", "--version");
        Assert.Equal(2, run.Code);
    }

    [Fact]
    public void RootHandlerRunsWhenNoSubcommandIsGiven()
    {
        var app = new CommandApp("t");
        app.Root.Argument("who", required: false).Handle(r =>
        {
            return r.Argument("who") == "bob" ? 5 : 0;
        });
        Assert.Equal(5, Execute(app, "bob").Code);
        Assert.Equal(0, Execute(app).Code);
    }

    [Fact]
    public void CommandWithoutHandlerPrintsItsHelp()
    {
        var app = new CommandApp("t").Add(new Command("leaf", "A leaf"));
        var run = Execute(app, "leaf");
        Assert.Equal(0, run.Code);
        Assert.Contains("A leaf", run.Out);
    }

    [Fact]
    public void HelpWidthIsHonoured()
    {
        var app = App();
        app.HelpWidth = 30;
        var run = Execute(app, "--help");
        var description = run.Out.Split('\n').First(l => l.StartsWith("A tiny"));
        Assert.True(description.Length <= 30);
    }

    [Fact]
    public void ParseIsAvailableWithoutRunning()
    {
        var r = App().Parse(new[] { "add", "1", "-p", "2" });
        Assert.Equal(2, r.Get<int>("precision"));
        Assert.Equal(new[] { "calc", "add" }, r.CommandPath);
    }

    [Fact]
    public void RunFallsBackToTheConsole()
    {
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        var o = new StringWriter();
        var e = new StringWriter();
        Console.SetOut(o);
        Console.SetError(e);
        try
        {
            Assert.Equal(0, App().Run(new[] { "--version" }));
            Assert.Equal(2, App().Run(new[] { "nope" }));
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }

        Assert.Contains("0.3.0", o.ToString());
        Assert.Contains("unknown command", e.ToString());
    }
}
