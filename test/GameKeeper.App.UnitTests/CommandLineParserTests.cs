using GameKeeper.Core;
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

    [Fact]
    public void Should_DefaultToTwoWayMode_When_ModeOptionAbsent()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.False(result.HasError);
        Assert.Equal(SyncMode.Bidirectional, result.Mode);
    }

    [Theory]
    [InlineData("both", SyncMode.Bidirectional)]
    [InlineData("up", SyncMode.FirstToSecond)]
    [InlineData("down", SyncMode.SecondToFirst)]
    [InlineData("BOTH", SyncMode.Bidirectional)]
    [InlineData("UP", SyncMode.FirstToSecond)]
    [InlineData("Down", SyncMode.SecondToFirst)]
    public void Should_MapModeValue_When_ModeOptionGiven(string value, SyncMode expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--mode", value]);

        Assert.False(result.HasError);
        Assert.Equal(expected, result.Mode);
    }

    [Theory]
    [InlineData("-m", "up")]
    [InlineData("--mode=up")]
    public void Should_MapModeValue_When_AliasOrInlineSyntaxUsed(params string[] modeArgs)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", .. modeArgs]);

        Assert.False(result.HasError);
        Assert.Equal(SyncMode.FirstToSecond, result.Mode);
    }

    [Fact]
    public void Should_Fail_When_ModeValueIsUnknown()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--mode", "sideways"]);

        Assert.True(result.HasError);
        Assert.Contains("Unknown sync mode: sideways", result.ErrorMessage);
        Assert.Contains("'both', 'up', or 'down'", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_ModeSpecifiedTwice()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            [@"C:\game", @"C:\cloud", "--mode", "up", "-m", "down"]);

        Assert.True(result.HasError);
        Assert.Contains("sync mode was specified more than once", result.ErrorMessage);
    }

    [Theory]
    [InlineData(@"C:\game", @"C:\cloud", "--mode")]
    [InlineData(@"C:\game", @"C:\cloud", "--mode=")]
    [InlineData("--mode", "--game", @"C:\game")]
    public void Should_Fail_When_ModeValueIsMissing(params string[] args)
    {
        CommandLineParseResult result = CommandLineParser.Parse(args);

        Assert.True(result.HasError);
        Assert.Contains("Missing value for --mode", result.ErrorMessage);
    }

    [Fact]
    public void Should_LeaveDeletionsOff_When_DeleteFlagAbsent()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.False(result.HasError);
        Assert.False(result.PropagateDeletions);
    }

    [Fact]
    public void Should_EnableDeletionPropagation_When_DeleteFlagGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--delete"]);

        Assert.False(result.HasError);
        Assert.True(result.PropagateDeletions);
    }

    [Fact]
    public void Should_CombineFlagsAndNamedOptions_When_GivenTogether()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--game", @"C:\game", "--cloud", @"C:\cloud", "--mode", "up", "--delete", "--no-backup"]);

        Assert.False(result.HasError);
        Assert.Equal(SyncMode.FirstToSecond, result.Mode);
        Assert.True(result.PropagateDeletions);
        Assert.False(result.CreateBackups);
    }

    [Fact]
    public void Should_KeepBackupsOnAndForceOff_When_NoFlagsGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.True(result.CreateBackups);
        Assert.False(result.Force);
    }

    [Fact]
    public void Should_DisableBackups_When_NoBackupFlagGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--no-backup"]);

        Assert.False(result.HasError);
        Assert.False(result.CreateBackups);
    }

    [Fact]
    public void Should_CaptureForce_When_ForceFlagGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            [@"C:\game", @"C:\cloud", "--delete", "--force"]);

        Assert.False(result.HasError);
        Assert.True(result.Force);
    }

    [Fact]
    public void Should_LeaveKeepBackupsUnset_When_OptionAbsent()
    {
        // Unset means "use the engine default" rather than a number duplicated in the parser.
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.False(result.HasError);
        Assert.Null(result.KeepBackups);
    }

    [Theory]
    [InlineData("--keep-backups", "3")]
    [InlineData("--keep-backups=3")]
    public void Should_CaptureKeepBackups_When_OptionGiven(params string[] keepArgs)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", .. keepArgs]);

        Assert.False(result.HasError);
        Assert.Equal(3, result.KeepBackups);
    }

    [Fact]
    public void Should_CaptureZeroKeepBackups_When_GivenInline()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--keep-backups=0"]);

        Assert.False(result.HasError);
        Assert.Equal(0, result.KeepBackups);
    }

    [Fact]
    public void Should_Fail_When_KeepBackupsIsNegativeInline()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--keep-backups=-1"]);

        Assert.True(result.HasError);
        Assert.Contains("Invalid value for --keep-backups: -1", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_KeepBackupsValueLooksLikeAnOption()
    {
        // The lookahead treats a following token starting with '-' as a missing value, so a
        // space-separated negative number lands here rather than in the numeric check.
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--keep-backups", "-1"]);

        Assert.True(result.HasError);
        Assert.Contains("Missing value for --keep-backups", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_KeepBackupsIsNotANumber()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--keep-backups", "many"]);

        Assert.True(result.HasError);
        Assert.Contains("keep-backups", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_KeepBackupsSpecifiedTwice()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            [@"C:\game", @"C:\cloud", "--keep-backups", "3", "--keep-backups", "4"]);

        Assert.True(result.HasError);
        Assert.Contains("backup count was specified more than once", result.ErrorMessage);
    }

    [Fact]
    public void Should_Fail_When_KeepBackupsValueIsMissing()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--keep-backups"]);

        Assert.True(result.HasError);
        Assert.Contains("Missing value for --keep-backups", result.ErrorMessage);
    }

    [Fact]
    public void Should_LeaveThePatternListsEmpty_When_NoFilterOptionsGiven()
    {
        // Empty means every file, not no files.
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.Empty(result.IncludePatterns);
        Assert.Empty(result.ExcludePatterns);
    }

    [Theory]
    [InlineData("--exclude")]
    [InlineData("-x")]
    public void Should_CollectTheExcludePattern_When_OptionGiven(string token)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", token, "*.log"]);

        Assert.False(result.HasError);
        Assert.Equal(["*.log"], result.ExcludePatterns);
    }

    [Fact]
    public void Should_CollectEveryExcludePattern_When_Repeated()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            [@"C:\game", @"C:\cloud", "--exclude", "*.log", "-x", "*.tmp", @"--exclude=cache\*"]);

        Assert.False(result.HasError);
        Assert.Equal(["*.log", "*.tmp", @"cache\*"], result.ExcludePatterns);
    }

    [Theory]
    [InlineData("--exclude")]
    [InlineData("--exclude=")]
    public void Should_Fail_When_ExcludeValueIsMissing(params string[] excludeArgs)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", .. excludeArgs]);

        Assert.True(result.HasError);
        Assert.Contains("Missing value for --exclude", result.ErrorMessage);
    }

    [Theory]
    [InlineData("--include")]
    [InlineData("-i")]
    public void Should_CollectTheIncludePattern_When_OptionGiven(string token)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", token, "*.sav"]);

        Assert.False(result.HasError);
        Assert.Equal(["*.sav"], result.IncludePatterns);
    }

    [Fact]
    public void Should_CollectEveryIncludePattern_When_Repeated()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            [@"C:\game", @"C:\cloud", "--include", "*.sav", "--include", "*.cfg"]);

        Assert.Equal(["*.sav", "*.cfg"], result.IncludePatterns);
    }

    [Fact]
    public void Should_CaptureBothLists_When_IncludeAndExcludeAreCombined()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            [@"C:\game", @"C:\cloud", "--include", "*.sav", "--exclude", @"backup\*"]);

        Assert.Equal(["*.sav"], result.IncludePatterns);
        Assert.Equal([@"backup\*"], result.ExcludePatterns);
    }

    [Fact]
    public void Should_Fail_When_IncludeValueIsMissing()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", "--include"]);

        Assert.True(result.HasError);
        Assert.Contains("Missing value for --include", result.ErrorMessage);
    }

    [Fact]
    public void Should_LeaveDryRunOff_When_FlagAbsent()
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud"]);

        Assert.False(result.DryRun);
    }

    [Theory]
    [InlineData("--dry-run")]
    [InlineData("-n")]
    public void Should_EnableDryRun_When_FlagGiven(string flag)
    {
        CommandLineParseResult result = CommandLineParser.Parse([@"C:\game", @"C:\cloud", flag]);

        Assert.True(result.DryRun);
        Assert.False(result.HasError);
    }
}
