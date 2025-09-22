using Cobbler;

namespace Cobbler.Tests;

public class BuilderTests
{
    [Theory]
    [InlineData("")]
    [InlineData("-x")]
    [InlineData("has space")]
    [InlineData("_lead")]
    public void BadCommandNamesAreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => new Command(name));
    }

    [Fact]
    public void BadAliasIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new Command("c").Alias("bad alias"));
    }

    [Fact]
    public void BadOptionNamesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Command("c").Flag("--x"));
        Assert.Throws<ArgumentException>(() => new Command("c").Flag("x", shortName: '-'));
    }

    [Fact]
    public void DuplicateOptionNamesAreRejected()
    {
        var c = new Command("c").Flag("same");
        Assert.Throws<InvalidOperationException>(() => c.Option("same"));
    }

    [Fact]
    public void DuplicateShortNamesAreRejected()
    {
        var c = new Command("c").Flag("one", shortName: 'x');
        Assert.Throws<InvalidOperationException>(() => c.Flag("two", shortName: 'x'));
    }

    [Fact]
    public void RequiredArgumentCannotFollowOptional()
    {
        var c = new Command("c").Argument("a", required: false);
        Assert.Throws<InvalidOperationException>(() => c.Argument("b"));
    }

    [Fact]
    public void NothingCanFollowAVariadicArgument()
    {
        var c = new Command("c").Argument("a", variadic: true);
        Assert.Throws<InvalidOperationException>(() => c.Argument("b", required: false));
    }

    [Fact]
    public void DuplicateArgumentNamesAreRejected()
    {
        var c = new Command("c").Argument("a");
        Assert.Throws<InvalidOperationException>(() => c.Argument("a", required: false));
    }

    [Fact]
    public void DuplicateSubcommandsAndAliasesAreRejected()
    {
        var root = new Command("r").Add(new Command("go").Alias("g"));
        Assert.Throws<InvalidOperationException>(() => root.Add(new Command("go")));
        Assert.Throws<InvalidOperationException>(() => root.Add(new Command("other").Alias("g")));
    }

    [Fact]
    public void ACommandCanOnlyHaveOneParent()
    {
        var child = new Command("child");
        new Command("a").Add(child);
        Assert.Throws<InvalidOperationException>(() => new Command("b").Add(child));
    }

    [Fact]
    public void ChoicesAreCopied()
    {
        var list = new List<string> { "a", "b" };
        var c = new Command("c").Option("o", choices: list);
        list.Add("c");
        Assert.Equal(2, c.Options[0].Choices!.Count);
    }

    [Fact]
    public void FlagIsMarkedAsAFlag()
    {
        var c = new Command("c").Flag("f", "desc", 'f').Option("o");
        Assert.True(c.Options[0].IsFlag);
        Assert.False(c.Options[1].IsFlag);
        Assert.Equal("--f", c.Options[0].Display);
    }

    [Fact]
    public void FindSubcommandMatchesNamesAndAliases()
    {
        var root = new Command("r").Add(new Command("remove").Alias("rm", "del"));
        Assert.NotNull(root.FindSubcommand("remove"));
        Assert.NotNull(root.FindSubcommand("del"));
        Assert.Null(root.FindSubcommand("nothing"));
    }

    [Fact]
    public void ActionHandlersReturnZero()
    {
        var hit = false;
        var c = new Command("c").Handle(_ => hit = true);
        Assert.Equal(0, c.Handler!(Parser.Parse(c, Array.Empty<string>())));
        Assert.True(hit);
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("same", "same", 0)]
    [InlineData("flaw", "lawn", 2)]
    public void EditDistance(string a, string b, int expected)
    {
        Assert.Equal(expected, Suggestions.Distance(a, b));
    }

    [Fact]
    public void ClosestPicksTheNearestCandidate()
    {
        Assert.Equal("verbose", Suggestions.Closest("verbos", new[] { "version", "verbose", "force" }));
        Assert.Null(Suggestions.Closest("qqqqqq", new[] { "version", "verbose" }));
        Assert.Null(Suggestions.Closest("x", Array.Empty<string>()));
    }
}
