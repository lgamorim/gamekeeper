using Xunit;

namespace GameKeeper.App.UnitTests;

public sealed class AppVersionTests
{
    [Fact]
    public void Should_ShortenTheCommit_When_VersionCarriesOne()
    {
        string result = AppVersion.Format("1.0.0+b93a7f0e40d2c6a1f5e8b3d7c2a9f4e1b6d8c3a7");

        Assert.Equal("1.0.0 (b93a7f0)", result);
    }

    [Fact]
    public void Should_ReturnTheVersionAlone_When_NoCommitIsPresent()
    {
        string result = AppVersion.Format("1.0.0");

        Assert.Equal("1.0.0", result);
    }

    [Fact]
    public void Should_ReturnTheVersionAlone_When_TheCommitIsEmpty()
    {
        string result = AppVersion.Format("1.0.0+");

        Assert.Equal("1.0.0", result);
    }

    [Fact]
    public void Should_KeepTheWholeCommit_When_ShorterThanTheShortForm()
    {
        string result = AppVersion.Format("1.0.0+abc");

        Assert.Equal("1.0.0 (abc)", result);
    }

    [Fact]
    public void Should_PreserveThePreReleaseSuffix_When_VersionIsPreRelease()
    {
        string result = AppVersion.Format("1.0.0-alpha.1+b93a7f0e40d2c6a1f5e8b3d7c2a9f4e1b6d8c3a7");

        Assert.Equal("1.0.0-alpha.1 (b93a7f0)", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ReportUnknown_When_VersionIsMissing(string? informationalVersion)
    {
        string result = AppVersion.Format(informationalVersion);

        Assert.Equal("unknown version", result);
    }

    [Fact]
    public void Should_DescribeTheRunningBuild_When_CurrentIsRead()
    {
        string result = AppVersion.Current;

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual("unknown version", result);
    }
}
