using Cobbler;

namespace Cobbler.Tests;

public class ParserTests
{
    private static Command Sample()
    {
        return new Command("tool", "A tool")
            .Flag("verbose", "Be chatty", 'v')
            .Flag("force", "Overwrite", 'f')
            .Flag("all", "Everything", 'a')
            .Option("count", "How many", 'n', "N", defaultValue: "1")
            .Option("name", "A name", 'm')
            .Option("tag", "Tags", 't', repeatable: true)
            .Argument("input", "Input file")
            .Argument("rest", "Others", required: false, variadic: true)
            .Handle(_ => { });
    }

    private static ParseResult Parse(Command c, params string[] args) => Parser.Parse(c, args);

    private static ParseException Fail(Command c, params string[] args)
        => Assert.Throws<ParseException>(() => Parser.Parse(c, args));

    [Fact]
    public void LongFlagIsSet()
    {
        var r = Parse(Sample(), "--verbose", "in.txt");
        Assert.True(r.Flag("verbose"));
        Assert.False(r.Flag("force"));
    }

    [Fact]
    public void ShortFlagIsSet()
    {
        var r = Parse(Sample(), "-f", "in.txt");
        Assert.True(r.Flag("force"));
    }

    [Fact]
    public void ShortFlagsCluster()
    {
        var r = Parse(Sample(), "-vfa", "in.txt");
        Assert.True(r.Flag("verbose") && r.Flag("force") && r.Flag("all"));
    }

    [Fact]
    public void FlagsCanBeCounted()
    {
        var r = Parse(Sample(), "-vvv", "--verbose", "in.txt");
        Assert.Equal(4, r.Count("verbose"));
    }

    [Fact]
    public void LongOptionWithSeparateValue()
    {
        var r = Parse(Sample(), "--name", "bob", "in.txt");
        Assert.Equal("bob", r.Value("name"));
    }

    [Fact]
    public void LongOptionWithEquals()
    {
        var r = Parse(Sample(), "--name=bob=smith", "in.txt");
        Assert.Equal("bob=smith", r.Value("name"));
    }

    [Fact]
    public void LongOptionWithEmptyEqualsValue()
    {
        var r = Parse(Sample(), "--name=", "in.txt");
        Assert.Equal("", r.Value("name"));
    }

    [Fact]
    public void ShortOptionWithSeparateValue()
    {
        var r = Parse(Sample(), "-m", "bob", "in.txt");
        Assert.Equal("bob", r.Value("name"));
    }

    [Fact]
    public void ShortOptionWithAttachedValue()
    {
        var r = Parse(Sample(), "-mbob", "in.txt");
        Assert.Equal("bob", r.Value("name"));
    }

    [Fact]
    public void ShortOptionWithAttachedEquals()
    {
        var r = Parse(Sample(), "-m=bob", "in.txt");
        Assert.Equal("bob", r.Value("name"));
    }

    [Fact]
    public void ShortClusterEndingInValueOption()
    {
        var r = Parse(Sample(), "-vfm", "bob", "in.txt");
        Assert.True(r.Flag("verbose") && r.Flag("force"));
        Assert.Equal("bob", r.Value("name"));
    }

    [Fact]
    public void ShortClusterWithAttachedValueAfterFlags()
    {
        var r = Parse(Sample(), "-vn5", "in.txt");
        Assert.True(r.Flag("verbose"));
        Assert.Equal(5, r.Get<int>("count"));
    }

    [Fact]
    public void DefaultsApplyWhenAbsent()
    {
        var r = Parse(Sample(), "in.txt");
        Assert.Equal("1", r.Value("count"));
        Assert.False(r.Has("count"));
    }

    [Fact]
    public void ExplicitValueBeatsDefault()
    {
        var r = Parse(Sample(), "-n", "9", "in.txt");
        Assert.Equal("9", r.Value("count"));
        Assert.True(r.Has("count"));
    }

    [Fact]
    public void RepeatableOptionCollectsValues()
    {
        var r = Parse(Sample(), "-t", "a", "--tag=b", "-tc", "in.txt");
        Assert.Equal(new[] { "a", "b", "c" }, r.Values("tag"));
        Assert.Equal("c", r.Value("tag"));
    }

    [Fact]
    public void NonRepeatableOptionRejectsDuplicates()
    {
        var e = Fail(Sample(), "--name", "a", "--name", "b", "in.txt");
        Assert.Equal(ParseErrorKind.DuplicateOption, e.Kind);
    }

    [Fact]
    public void ArgumentsAreAssignedInOrder()
    {
        var r = Parse(Sample(), "in.txt", "a", "b", "c");
        Assert.Equal("in.txt", r.Argument("input"));
        Assert.Equal(new[] { "a", "b", "c" }, r.ArgumentValues("rest"));
    }

    [Fact]
    public void OptionsMayAppearBetweenArguments()
    {
        var r = Parse(Sample(), "in.txt", "--verbose", "a", "-n", "3", "b");
        Assert.Equal(new[] { "a", "b" }, r.ArgumentValues("rest"));
        Assert.True(r.Flag("verbose"));
    }

    [Fact]
    public void DoubleDashEndsOptionParsing()
    {
        var r = Parse(Sample(), "in.txt", "--", "--verbose", "-f");
        Assert.False(r.Flag("verbose"));
        Assert.Equal(new[] { "--verbose", "-f" }, r.ArgumentValues("rest"));
    }

    [Fact]
    public void NegativeNumbersAreValuesNotOptions()
    {
        var r = Parse(Sample(), "-n", "-5", "-2.5", "x");
        Assert.Equal(-5, r.Get<int>("count"));
        Assert.Equal("-2.5", r.Argument("input"));
        Assert.Equal(new[] { "x" }, r.ArgumentValues("rest"));
    }

    [Fact]
    public void SingleDashIsAnArgument()
    {
        var r = Parse(Sample(), "-");
        Assert.Equal("-", r.Argument("input"));
    }

    [Fact]
    public void ValueOptionCannotSwallowAnotherOption()
    {
        var e = Fail(Sample(), "--name", "--verbose", "in.txt");
        Assert.Equal(ParseErrorKind.MissingValue, e.Kind);
        Assert.Equal("--name", e.Subject);
    }

    [Fact]
    public void ExplicitEqualsAllowsDashedValue()
    {
        var r = Parse(Sample(), "--name=--odd", "in.txt");
        Assert.Equal("--odd", r.Value("name"));
    }

    [Fact]
    public void MissingValueAtEndOfInput()
    {
        var e = Fail(Sample(), "in.txt", "-m");
        Assert.Equal(ParseErrorKind.MissingValue, e.Kind);
        Assert.Contains("-m", e.Message);
    }

    [Fact]
    public void FlagRejectsAnInlineValue()
    {
        var e = Fail(Sample(), "--verbose=yes", "in.txt");
        Assert.Equal(ParseErrorKind.UnexpectedValue, e.Kind);
    }

    [Fact]
    public void UnknownLongOption()
    {
        var e = Fail(Sample(), "--bogus", "in.txt");
        Assert.Equal(ParseErrorKind.UnknownOption, e.Kind);
        Assert.Equal("--bogus", e.Subject);
    }

    [Fact]
    public void UnknownShortOption()
    {
        var e = Fail(Sample(), "-z", "in.txt");
        Assert.Equal(ParseErrorKind.UnknownOption, e.Kind);
        Assert.Equal("-z", e.Subject);
    }

    [Fact]
    public void UnknownOptionSuggestsTheClosest()
    {
        var e = Fail(Sample(), "--verbos", "in.txt");
        Assert.Contains("did you mean '--verbose'?", e.Message);
    }

    [Fact]
    public void WildlyDifferentOptionGetsNoSuggestion()
    {
        var e = Fail(Sample(), "--zzzzzzzz", "in.txt");
        Assert.DoesNotContain("did you mean", e.Message);
    }

    [Fact]
    public void MissingRequiredArgument()
    {
        var e = Fail(Sample());
        Assert.Equal(ParseErrorKind.MissingArgument, e.Kind);
        Assert.Equal("input", e.Subject);
    }

    [Fact]
    public void TooManyArguments()
    {
        var c = new Command("c").Argument("one").Handle(_ => { });
        var e = Fail(c, "a", "b");
        Assert.Equal(ParseErrorKind.UnexpectedArgument, e.Kind);
        Assert.Equal("b", e.Subject);
    }

    [Fact]
    public void RequiredOptionMustBeGiven()
    {
        var c = new Command("c").Option("token", required: true).Handle(_ => { });
        var e = Fail(c);
        Assert.Equal(ParseErrorKind.MissingOption, e.Kind);
        Assert.Equal("--token", e.Subject);
        Assert.Equal("x", Parse(c, "--token", "x").Value("token"));
    }

    [Fact]
    public void RequiredOptionWithDefaultIsSatisfied()
    {
        var c = new Command("c").Option("token", required: true, defaultValue: "d").Handle(_ => { });
        Assert.Equal("d", Parse(c).Value("token"));
    }

    [Fact]
    public void ChoicesAreEnforced()
    {
        var c = new Command("c").Option("mode", choices: new[] { "fast", "slow" }).Handle(_ => { });
        Assert.Equal("fast", Parse(c, "--mode", "fast").Value("mode"));
        var e = Fail(c, "--mode", "medium");
        Assert.Equal(ParseErrorKind.InvalidValue, e.Kind);
        Assert.Contains("fast, slow", e.Message);
    }

    [Fact]
    public void OptionalArgumentMayBeOmitted()
    {
        var c = new Command("c").Argument("a").Argument("b", required: false).Handle(_ => { });
        var r = Parse(c, "x");
        Assert.Equal("x", r.Argument("a"));
        Assert.Null(r.Argument("b"));
    }

    [Fact]
    public void RequiredVariadicNeedsAtLeastOne()
    {
        var c = new Command("c").Argument("files", variadic: true).Handle(_ => { });
        Assert.Equal(ParseErrorKind.MissingArgument, Fail(c).Kind);
        Assert.Equal(2, Parse(c, "a", "b").ArgumentValues("files").Count);
    }

    [Fact]
    public void HelpFlagShortCircuitsValidation()
    {
        var r = Parse(Sample(), "--help");
        Assert.True(r.HelpRequested);
    }

    [Fact]
    public void ShortHelpFlag()
    {
        Assert.True(Parse(Sample(), "-h").HelpRequested);
    }

    [Fact]
    public void UserCanTakeTheShortHForThemselves()
    {
        var c = new Command("c").Option("host", shortName: 'h').Handle(_ => { });
        var r = Parse(c, "-h", "example.org");
        Assert.False(r.HelpRequested);
        Assert.Equal("example.org", r.Value("host"));
        Assert.True(Parse(c, "--help").HelpRequested);
    }

    [Fact]
    public void QueryingUndeclaredNamesThrows()
    {
        var r = Parse(Sample(), "in.txt");
        Assert.Throws<ArgumentException>(() => r.Flag("nope"));
        Assert.Throws<ArgumentException>(() => r.Value("nope"));
        Assert.Throws<ArgumentException>(() => r.ArgumentValues("nope"));
    }

    [Fact]
    public void ErrorRemembersTheCommandItHappenedIn()
    {
        var root = new Command("root").Add(new Command("sub").Argument("x").Handle(_ => { }));
        var e = Fail(root, "sub");
        Assert.Equal("sub", e.Command!.Name);
    }
}
