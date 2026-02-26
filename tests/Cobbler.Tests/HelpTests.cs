using Cobbler;

namespace Cobbler.Tests;

public class HelpTests
{
    private static CommandApp App()
    {
        var app = new CommandApp("tool", "Does things and also a rather long description that should wrap nicely when the width is small enough to force it")
            .Version("1.2.3");
        app.Root.Flag("verbose", "Be chatty", 'v', global: true);
        app.Add(new Command("add", "Add an item")
            .Alias("a")
            .Argument("name", "Item name")
            .Argument("tags", "Tags to apply", required: false, variadic: true)
            .Option("count", "How many", 'c', "N", defaultValue: "1")
            .Option("mode", "Mode", choices: new[] { "fast", "slow" }, required: true)
            .Option("tag", "Tag", repeatable: true)
            .Flag("force", "Overwrite")
            .Example("tool add thing --count 3")
            .Handle(_ => { }));
        app.Add(new Command("hidden", "Not listed") { Hidden = true }.Handle(_ => { }));
        return app;
    }

    [Fact]
    public void RootHelpListsCommandsAndOptions()
    {
        var expected =
            "tool 1.2.3\n" +
            "Does things and also a rather long description that should wrap nicely when the\n" +
            "width is small enough to force it\n" +
            "\n" +
            "Usage:\n" +
            "  tool <command> [options]\n" +
            "\n" +
            "Commands:\n" +
            "  add, a  Add an item\n" +
            "\n" +
            "Options:\n" +
            "  -v, --verbose  Be chatty\n" +
            "  -h, --help     Show this help and exit\n" +
            "      --version  Show the version and exit\n";
        Assert.Equal(expected, HelpWriter.Render(App().Root, "1.2.3"));
    }

    [Fact]
    public void SubcommandHelpShowsArgumentsOptionsAndGlobals()
    {
        var expected =
            "Add an item\n" +
            "\n" +
            "Usage:\n" +
            "  tool add [options] <name> [tags...]\n" +
            "\n" +
            "Arguments:\n" +
            "  <name>     Item name\n" +
            "  [tags...]  Tags to apply\n" +
            "\n" +
            "Options:\n" +
            "  -c, --count <N>     How many (default: 1)\n" +
            "      --mode <value>  Mode (choices: fast, slow; required)\n" +
            "      --tag <value>   Tag (repeatable)\n" +
            "      --force         Overwrite\n" +
            "  -h, --help          Show this help and exit\n" +
            "\n" +
            "Global options:\n" +
            "  -v, --verbose  Be chatty\n" +
            "\n" +
            "Examples:\n" +
            "  tool add thing --count 3\n";
        Assert.Equal(expected, HelpWriter.Render(App().Root.FindSubcommand("add")!, null, 60));
    }

    [Fact]
    public void HiddenCommandsAreNotListed()
    {
        Assert.DoesNotContain("hidden", HelpWriter.Render(App().Root));
    }

    [Fact]
    public void HiddenOptionsAreNotListed()
    {
        var c = new Command("c").Add(new OptionSpec("secret", "shh") { IsFlag = true, Hidden = true }).Handle(_ => { });
        Assert.DoesNotContain("secret", HelpWriter.Render(c));
    }

    [Fact]
    public void VersionOptionOnlyAppearsWithAVersion()
    {
        var root = new Command("t").Handle(_ => { });
        Assert.DoesNotContain("--version", HelpWriter.Render(root));
        Assert.Contains("--version", HelpWriter.Render(root, "1.0"));
    }

    [Fact]
    public void UsageShowsBothCommandsAndArguments()
    {
        var c = new Command("t").Argument("file", required: false).Add(new Command("go")).Handle(_ => { });
        Assert.Contains("t <command> [options] [file]", HelpWriter.Render(c));
    }

    [Fact]
    public void LongDescriptionsWrapUnderTheirColumn()
    {
        var c = new Command("t")
            .Flag("alpha", "one two three four five six seven eight nine ten eleven twelve thirteen fourteen", 'a')
            .Handle(_ => { });
        var lines = HelpWriter.Render(c, null, 40).Split('\n');
        var options = lines.SkipWhile(l => l != "Options:").Skip(1).Where(l => l.Length > 0).ToList();
        Assert.True(options.Count >= 3);
        Assert.All(options, l => Assert.True(l.Length <= 40, l));
        Assert.StartsWith("  -a, --alpha  one two", options[0]);
        Assert.StartsWith(new string(' ', 15), options[1]);
    }

    [Fact]
    public void VeryLongLeftColumnDropsTheDescriptionToTheNextLine()
    {
        var c = new Command("t")
            .Option("a-very-long-option-name-indeed", "Described", valueName: "something-long")
            .Handle(_ => { });
        var text = HelpWriter.Render(c);
        Assert.Contains("      --a-very-long-option-name-indeed <something-long>\n", text);
        Assert.Contains("\n" + new string(' ', 34) + "Described\n", text);
    }

    [Fact]
    public void UserHelpOptionReplacesTheBuiltIn()
    {
        var c = new Command("t").Flag("help", "My own help").Handle(_ => { });
        var text = HelpWriter.Render(c);
        Assert.Contains("My own help", text);
        Assert.DoesNotContain("Show this help and exit", text);
    }

    [Fact]
    public void ShortHIsOmittedFromBuiltInHelpWhenTaken()
    {
        var c = new Command("t").Option("host", "Host", 'h').Handle(_ => { });
        var text = HelpWriter.Render(c);
        Assert.Contains("  -h, --host <value>", text);
        Assert.Contains("      --help", text);
    }

    [Fact]
    public void CommandWithoutDescriptionStartsWithUsage()
    {
        var c = new Command("t").Handle(_ => { });
        Assert.StartsWith("Usage:\n  t [options]\n", HelpWriter.Render(c));
    }

    [Fact]
    public void WrapSplitsOnWordBoundaries()
    {
        Assert.Equal(new[] { "aaa bbb", "ccc" }, HelpWriter.Wrap("aaa bbb ccc", 8));
        Assert.Equal(new[] { "averyveryverylongword" }, HelpWriter.Wrap("averyveryverylongword", 5));
        Assert.Empty(HelpWriter.Wrap("", 10));
    }

    [Fact]
    public void WriteGoesToTheGivenWriter()
    {
        var sw = new StringWriter();
        HelpWriter.Write(sw, App().Root);
        Assert.Contains("Usage:", sw.ToString());
    }
}
