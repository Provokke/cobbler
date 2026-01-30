using Cobbler;

namespace Cobbler.Tests;

public class ConversionTests
{
    private enum Level
    {
        Low,
        High,
    }

    private static ParseResult Parse(params string[] args)
    {
        var c = new Command("c")
            .Option("n", shortName: 'n')
            .Option("many", repeatable: true)
            .Argument("arg", required: false)
            .Handle(_ => { });
        return Parser.Parse(c, args);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-7", -7)]
    [InlineData("0", 0)]
    public void Integers(string raw, int expected)
    {
        Assert.Equal(expected, Parse("--n=" + raw).Get<int>("n"));
    }

    [Fact]
    public void IntegerFailureNamesTheOption()
    {
        var e = Assert.Throws<ParseException>(() => Parse("--n=abc").Get<int>("n"));
        Assert.Equal(ParseErrorKind.InvalidValue, e.Kind);
        Assert.Equal("invalid value 'abc' for --n: expected an integer", e.Message);
    }

    [Fact]
    public void IntegerOverflowIsAParseError()
    {
        Assert.Throws<ParseException>(() => Parse("--n=99999999999").Get<int>("n"));
        Assert.Equal(99999999999L, Parse("--n=99999999999").Get<long>("n"));
    }

    [Fact]
    public void Doubles()
    {
        Assert.Equal(2.5, Parse("--n=2.5").Get<double>("n"));
        Assert.Equal(1.25m, Parse("--n=1.25").Get<decimal>("n"));
    }

    [Fact]
    public void DoublesIgnoreTheCurrentCulture()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal(2.5, Parse("--n=2.5").Get<double>("n"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = old;
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("No", false)]
    [InlineData("off", false)]
    [InlineData("0", false)]
    public void Booleans(string raw, bool expected)
    {
        Assert.Equal(expected, Parse("--n=" + raw).Get<bool>("n"));
    }

    [Fact]
    public void BooleanFailure()
    {
        var e = Assert.Throws<ParseException>(() => Parse("--n=maybe").Get<bool>("n"));
        Assert.Contains("true or false", e.Message);
    }

    [Fact]
    public void EnumsIgnoreCase()
    {
        Assert.Equal(Level.High, Parse("--n=HIGH").Get<Level>("n"));
        Assert.Equal(Level.Low, Parse("--n=low").Get<Level>("n"));
    }

    [Fact]
    public void EnumFailureListsTheChoices()
    {
        var e = Assert.Throws<ParseException>(() => Parse("--n=medium").Get<Level>("n"));
        Assert.Contains("one of low, high", e.Message);
    }

    [Fact]
    public void UndefinedNumericEnumValueIsRejected()
    {
        Assert.Throws<ParseException>(() => Parse("--n=9").Get<Level>("n"));
    }

    [Fact]
    public void GuidsAndTimeSpans()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id, Parse("--n=" + id).Get<Guid>("n"));
        Assert.Equal(TimeSpan.FromMinutes(90), Parse("--n=01:30:00").Get<TimeSpan>("n"));
    }

    [Fact]
    public void DatesAndFiles()
    {
        Assert.Equal(new DateOnly(2026, 11, 19), Parse("--n=2026-11-19").Get<DateOnly>("n"));
        Assert.Equal("a.txt", Parse("--n=a.txt").Get<FileInfo>("n").Name);
    }

    [Fact]
    public void NullableTypes()
    {
        Assert.Equal(5, Parse("--n=5").Get<int?>("n"));
    }

    [Fact]
    public void FallbackIsUsedWhenThereIsNoValue()
    {
        Assert.Equal(10, Parse().Get("n", 10));
        Assert.Equal(3, Parse("--n=3").Get("n", 10));
    }

    [Fact]
    public void GetWithoutAValueThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Parse().Get<int>("n"));
    }

    [Fact]
    public void GetAllConvertsEachValue()
    {
        Assert.Equal(new[] { 1, 2, 3 }, Parse("--many=1", "--many=2", "--many=3").GetAll<int>("many"));
    }

    [Fact]
    public void ArgumentsConvertToo()
    {
        var r = Parse("12");
        Assert.Equal(12, r.Argument<int>("arg"));
        var e = Assert.Throws<ParseException>(() => Parse("zz").Argument<int>("arg"));
        Assert.Contains("<arg>", e.Message);
    }

    [Fact]
    public void ValueConverterWorksOnItsOwn()
    {
        Assert.Equal(7, ValueConverter.Convert<int>("7", "--x"));
        Assert.Equal("hi", ValueConverter.Convert<string>("hi", "--x"));
    }
}
