using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class GlobMatcherTests
{
    [Theory]
    [InlineData("*.log", "game.log")]
    [InlineData("*.log", @"saves\game.log")] // '*' spans directory separators
    [InlineData("cache*", "cache")]
    [InlineData("cache*", @"cache\data.bin")]
    [InlineData("cache*", "cachefile")]
    [InlineData("a?.sav", "ab.sav")] // '?' is exactly one character
    [InlineData(@"logs\debug.txt", @"logs\debug.txt")]
    public void Should_Match_When_PatternCoversThePath(string pattern, string path)
    {
        var matcher = new GlobMatcher([pattern]);

        Assert.True(matcher.IsMatch(path));
    }

    [Theory]
    [InlineData("*.log", "game.txt")]
    [InlineData("cache*", "backup")]
    [InlineData("a?.sav", "abc.sav")] // '?' matches a single character only
    [InlineData(@"logs\debug.txt", @"logs\info.txt")]
    public void Should_NotMatch_When_PatternDoesNotCoverThePath(string pattern, string path)
    {
        var matcher = new GlobMatcher([pattern]);

        Assert.False(matcher.IsMatch(path));
    }

    [Theory]
    [InlineData("*.LOG", "game.log")]
    [InlineData("*.log", "GAME.LOG")]
    public void Should_Match_When_OnlyTheCaseDiffers(string pattern, string path)
    {
        var matcher = new GlobMatcher([pattern]);

        Assert.True(matcher.IsMatch(path));
    }

    [Fact]
    public void Should_TreatBothSeparatorsAlike_When_Matching()
    {
        var matcher = new GlobMatcher(["cache/*"]);

        Assert.True(matcher.IsMatch(@"cache\data.bin"));
    }

    [Fact]
    public void Should_Match_When_AnyOfSeveralPatternsMatches()
    {
        var matcher = new GlobMatcher(["*.log", "*.tmp"]);

        Assert.True(matcher.IsMatch("crash.tmp"));
        Assert.True(matcher.IsMatch("game.log"));
        Assert.False(matcher.IsMatch("save.dat"));
    }

    [Fact]
    public void Should_MatchNothing_When_NoPatternsGiven()
    {
        var matcher = new GlobMatcher([]);

        Assert.False(matcher.IsMatch("anything.log"));
        Assert.False(matcher.HasPatterns);
    }

    [Fact]
    public void Should_IgnoreBlankPatterns_When_Constructed()
    {
        var matcher = new GlobMatcher(["", "   ", null!]);

        Assert.False(matcher.IsMatch("game.log"));
        Assert.False(matcher.HasPatterns);
    }

    [Fact]
    public void Should_Throw_When_PatternsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new GlobMatcher(null!));
    }

    [Fact]
    public void Should_Throw_When_PathIsNull()
    {
        var matcher = new GlobMatcher(["*.log"]);

        Assert.Throws<ArgumentNullException>(() => matcher.IsMatch(null!));
    }
}
