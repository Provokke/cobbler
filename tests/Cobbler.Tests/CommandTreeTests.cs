using Cobbler;

namespace Cobbler.Tests;

public class CommandTreeTests
{
    private static Command Tree()
    {
        var remote = new Command("remote", "Manage remotes")
            .Add(new Command("add", "Add a remote").Argument("name").Argument("url").Handle(_ => { }))
            .Add(new Command("remove", "Remove a remote").Alias("rm").Argument("name").Handle(_ => { }));
        return new Command("git")
            .Flag("verbose", "Be chatty", 'v', global: true)
            .Option("config", "Config file", 'c')
            .Add(new Command("commit", "Record changes").Option("message", "Message", 'm').Handle(_ => { }))
            .Add(remote);
    }

    private static ParseResult Parse(params string[] args) => Parser.Parse(Tree(), args);

    [Fact]
    public void SubcommandIsResolved()
    {
        var r = Parse("commit", "-m", "hello");
        Assert.Equal("commit", r.Command.Name);
        Assert.Equal("hello", r.Value("message"));
    }

    [Fact]
    public void NestedSubcommandsAreResolved()
    {
        var r = Parse("remote", "add", "origin", "https://x");
        Assert.Equal(new[] { "git", "remote", "add" }, r.CommandPath);
        Assert.Equal("origin", r.Argument("name"));
        Assert.Equal("https://x", r.Argument("url"));
    }

    [Fact]
    public void AliasesResolveToTheCommand()
    {
        var r = Parse("remote", "rm", "origin");
        Assert.Equal("remove", r.Command.Name);
    }

    [Fact]
    public void GlobalOptionWorksBeforeTheSubcommand()
    {
        var r = Parse("-v", "commit");
        Assert.True(r.Flag("verbose"));
    }

    [Fact]
    public void GlobalOptionWorksAfterTheSubcommand()
    {
        var r = Parse("remote", "add", "--verbose", "o", "u");
        Assert.True(r.Flag("verbose"));
    }

    [Fact]
    public void GlobalOptionWorksAtEveryDepth()
    {
        var r = Parse("remote", "-v", "rm", "-v", "origin");
        Assert.Equal(2, r.Count("verbose"));
    }

    [Fact]
    public void NonGlobalParentOptionIsNotInherited()
    {
        var e = Assert.Throws<ParseException>(() => Parse("commit", "--config", "x"));
        Assert.Equal(ParseErrorKind.UnknownOption, e.Kind);
    }

    [Fact]
    public void NonGlobalParentOptionCannotPrecedeTheSubcommand()
    {
        var e = Assert.Throws<ParseException>(() => Parse("--config", "x", "commit"));
        Assert.Equal(ParseErrorKind.UnknownOption, e.Kind);
        Assert.Contains("cannot be used before 'commit'", e.Message);
    }

    [Fact]
    public void ParentOptionIsFineWhenNoSubcommandFollows()
    {
        var root = new Command("app").Option("config", shortName: 'c').Handle(_ => { });
        Assert.Equal("x", Parser.Parse(root, new[] { "-c", "x" }).Value("config"));
    }

    [Fact]
    public void UnknownCommandSuggestsTheClosest()
    {
        var e = Assert.Throws<ParseException>(() => Parse("comit"));
        Assert.Equal(ParseErrorKind.UnknownCommand, e.Kind);
        Assert.Contains("did you mean 'commit'?", e.Message);
    }

    [Fact]
    public void UnknownCommandSuggestionsCoverAliases()
    {
        var e = Assert.Throws<ParseException>(() => Parse("remote", "rn"));
        Assert.Contains("'rm'", e.Message);
    }

    [Fact]
    public void GroupCommandWithoutHandlerNeedsASubcommand()
    {
        var e = Assert.Throws<ParseException>(() => Parse("remote"));
        Assert.Equal(ParseErrorKind.MissingCommand, e.Kind);
        Assert.Equal("remote", e.Command!.Name);
    }

    [Fact]
    public void RootWithoutHandlerNeedsASubcommand()
    {
        var e = Assert.Throws<ParseException>(() => Parse());
        Assert.Equal(ParseErrorKind.MissingCommand, e.Kind);
    }

    [Fact]
    public void HelpWorksOnAGroupWithoutASubcommand()
    {
        var r = Parse("remote", "--help");
        Assert.True(r.HelpRequested);
        Assert.Equal("remote", r.Command.Name);
    }

    [Fact]
    public void AfterDoubleDashSubcommandsAreNotResolved()
    {
        var root = new Command("app").Argument("x", variadic: true).Add(new Command("go")).Handle(_ => { });
        var r = Parser.Parse(root, new[] { "--", "go" });
        Assert.Equal("app", r.Command.Name);
        Assert.Equal(new[] { "go" }, r.ArgumentValues("x"));
    }

    [Fact]
    public void CommandWithArgumentsAndSubcommandsPrefersSubcommands()
    {
        var root = new Command("app")
            .Argument("file", required: false)
            .Add(new Command("go").Handle(_ => { }))
            .Handle(_ => { });
        Assert.Equal("go", Parser.Parse(root, new[] { "go" }).Command.Name);
        Assert.Equal("data", Parser.Parse(root, new[] { "data" }).Argument("file"));
    }

    [Fact]
    public void PathFromRootListsAncestors()
    {
        var root = Tree();
        var add = root.FindSubcommand("remote")!.FindSubcommand("add")!;
        Assert.Equal(new[] { "git", "remote", "add" }, add.PathFromRoot().Select(c => c.Name));
        Assert.Same(root, add.Parent!.Parent);
    }

    [Fact]
    public void HandlerCanBeReplacedAndInvoked()
    {
        var c = new Command("c").Handle(r => 7);
        Assert.Equal(7, c.Handler!(Parser.Parse(c, Array.Empty<string>())));
    }
}
