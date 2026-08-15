using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class PathFilterTests
{
    [Fact]
    public void Should_AllowEverything_When_NoPatternsGiven()
    {
        var filter = new PathFilter([], []);

        Assert.True(filter.AllowsFile("save.dat"));
        Assert.True(filter.AllowsFile(@"slots\quick.sav"));
        Assert.True(filter.AllowsDirectory("slots"));
    }

    [Fact]
    public void Should_BlockMatchingFiles_When_OnlyExcludesGiven()
    {
        var filter = new PathFilter([], ["*.log"]);

        Assert.False(filter.AllowsFile("debug.log"));
        Assert.False(filter.AllowsFile(@"logs\verbose.log"));
        Assert.True(filter.AllowsFile("save.dat"));
    }

    [Fact]
    public void Should_AllowNothingElse_When_IncludesGiven()
    {
        var filter = new PathFilter(["*.sav"], []);

        Assert.True(filter.AllowsFile("save.sav"));
        Assert.True(filter.AllowsFile(@"slots\quick.sav"));
        Assert.False(filter.AllowsFile("config.ini"));
        Assert.False(filter.AllowsFile("debug.log"));
    }

    [Fact]
    public void Should_AllowAnyOfThem_When_SeveralIncludesGiven()
    {
        var filter = new PathFilter(["*.sav", "*.cfg"], []);

        Assert.True(filter.AllowsFile("a.sav"));
        Assert.True(filter.AllowsFile("b.cfg"));
        Assert.False(filter.AllowsFile("c.log"));
    }

    [Fact]
    public void Should_LetTheExcludeWin_When_BothListsMatch()
    {
        // Narrow with includes, then carve an exception out with an exclude.
        var filter = new PathFilter(["*.sav"], [@"backup\*"]);

        Assert.True(filter.AllowsFile("quick.sav"));
        Assert.False(filter.AllowsFile(@"backup\old.sav"));
    }

    [Fact]
    public void Should_NotApplyIncludesToDirectories_When_Deciding()
    {
        // A directory is a container, not a file: requiring it to match '*.sav' would filter
        // out every directory and silently stop empty folders being replicated.
        var filter = new PathFilter(["*.sav"], []);

        Assert.True(filter.AllowsDirectory("slots"));
        Assert.True(filter.AllowsDirectory(@"slots\autosave"));
    }

    [Fact]
    public void Should_StillApplyExcludesToDirectories_When_Deciding()
    {
        var filter = new PathFilter(["*.sav"], ["cache*"]);

        Assert.False(filter.AllowsDirectory("cache"));
        Assert.True(filter.AllowsDirectory("slots"));
    }

    [Fact]
    public void Should_IgnoreBlankPatterns_When_Constructed()
    {
        // A blank include must not be read as "include nothing".
        var filter = new PathFilter(["   ", ""], []);

        Assert.True(filter.AllowsFile("anything.dat"));
    }

    [Fact]
    public void Should_Throw_When_PatternListsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PathFilter(null!, []));
        Assert.Throws<ArgumentNullException>(() => new PathFilter([], null!));
    }

    [Fact]
    public void Should_Throw_When_PathIsNull()
    {
        var filter = new PathFilter([], []);

        Assert.Throws<ArgumentNullException>(() => filter.AllowsFile(null!));
        Assert.Throws<ArgumentNullException>(() => filter.AllowsDirectory(null!));
    }
}
