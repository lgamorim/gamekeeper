using Xunit;

namespace GameKeeper.App.UnitTests;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Should_AssignGameThenCloud_When_TwoPositionalsGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.False(result.HasError);
        Assert.Equal(@"C:\game", result.GameFolder);
        Assert.Equal(@"C:\cloud", result.CloudFolder);
    }

    [Theory]
    [InlineData("--game", @"C:\game", "--cloud", @"C:\cloud")]
    [InlineData("--cloud", @"C:\cloud", "--game", @"C:\game")]
    [InlineData("-g", @"C:\game", "-c", @"C:\cloud")]
    [InlineData(@"--game=C:\game", @"--cloud=C:\cloud")]
    public void Should_ResolveNamedOptions_When_GivenInAnyOrderOrSyntax(params string[] args)
    {
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.HasError);
        Assert.Equal(@"C:\game", result.GameFolder);
        Assert.Equal(@"C:\cloud", result.CloudFolder);
    }

    [Theory]
    [InlineData("--game", @"C:\game", @"C:\cloud")]   // cloud positional
    [InlineData(@"C:\game", "--cloud", @"C:\cloud")]  // game positional
    public void Should_FillRemainingSlot_When_PositionalAndNamedAreMixed(params string[] args)
    {
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.False(result.HasError);
        Assert.Equal(@"C:\game", result.GameFolder);
        Assert.Equal(@"C:\cloud", result.CloudFolder);
    }

    [Fact]
    public void Should_RecognizeOptionNames_When_CasedDifferently()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--GAME", @"C:\game", "--Cloud", @"C:\cloud"]);

        Assert.False(result.HasError);
        Assert.Equal(@"C:\game", result.GameFolder);
        Assert.Equal(@"C:\cloud", result.CloudFolder);
    }

    [Fact]
    public void Should_Fail_When_TooManyPositionalsGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", @"C:\extra"]);

        Assert.True(result.HasError);
        Assert.Contains("Too many", result.ErrorMessage);
    }

    [Theory]
    [InlineData]                          // no arguments at all
    [InlineData(@"C:\game")]
    [InlineData("--game", @"C:\game")]
    public void Should_Fail_When_RequiredFolderIsMissing(params string[] args)
    {
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.HasError);
        Assert.Contains("required", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_GameFolderSpecifiedTwice()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--game", @"C:\one", "-g", @"C:\two", "--cloud", @"C:\cloud"]);

        Assert.True(result.HasError);
        Assert.Contains("game folder was specified more than once", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_CloudFolderSpecifiedTwice()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--cloud", @"C:\one", "-c", @"C:\two", "--game", @"C:\game"]);

        Assert.True(result.HasError);
        Assert.Contains("cloud folder was specified more than once", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_OptionIsUnknown()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--bogus", @"C:\game", @"C:\cloud"]);

        Assert.True(result.HasError);
        Assert.Contains("Unknown option: --bogus", result.ErrorMessage);
    }

    [Theory]
    [InlineData("--game")]                     // no following value
    [InlineData("--game", "--cloud", @"C:\c")] // value looks like another option
    [InlineData("--game=")]                    // empty inline value
    public void Should_Fail_When_OptionValueIsMissing(params string[] args)
    {
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.HasError);
        Assert.Contains("Missing value for --game", result.ErrorMessage);
    }

    [Fact]
    public void Should_Throw_When_ArgsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => CommandLineParser.Parse(null!));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Should_RequestHelp_When_HelpFlagGivenAnywhere(string helpFlag)
    {
        CommandLineParseResult result = CommandLineParser.Parse([helpFlag, @"C:\game", @"C:\cloud"]);

        Assert.True(result.HelpRequested);
        Assert.False(result.HasError);
    }

    [Fact]
    public void Should_RequestHelp_When_HelpFollowsInvalidOptions()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--bogus", "--help"]);

        Assert.True(result.HelpRequested);
        Assert.False(result.HasError);
    }

    [Fact]
    public void Should_RequestVersion_When_VersionFlagGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--version"]);

        Assert.True(result.VersionRequested);
        Assert.False(result.HasError);
    }

    [Fact]
    public void Should_RequestVersion_When_VersionAppearsAnywhere()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", "--version", "--nonsense"]);

        Assert.True(result.VersionRequested);
        Assert.False(result.HasError);
    }

    [Fact]
    public void Should_PreferHelp_When_HelpAndVersionGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--version", "--help"]);

        Assert.True(result.HelpRequested);
        Assert.False(result.VersionRequested);
    }

    [Fact]
    public void Should_NotRequestVersion_When_VersionFlagAbsent()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.False(result.VersionRequested);
    }
}
