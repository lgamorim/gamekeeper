using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class SyncOptionsTests
{
    [Fact]
    public void Should_UseTwoWayModeAndTwoSecondTolerance_When_Defaulted()
    {
        SyncOptions options = SyncOptions.Default;

        Assert.Equal(SyncMode.Bidirectional, options.Mode);
        Assert.Equal(TimeSpan.FromSeconds(2), options.TimestampTolerance);
    }

    [Fact]
    public void Should_KeepDeletionsOff_When_Defaulted()
    {
        Assert.False(SyncOptions.Default.PropagateDeletions);
    }

    [Fact]
    public void Should_EnableBackupsKeepingTen_When_Defaulted()
    {
        Assert.True(SyncOptions.Default.CreateBackups);
        Assert.Equal(10, SyncOptions.Default.KeepBackups);
    }

    [Fact]
    public void Should_RunForReal_When_Defaulted()
    {
        Assert.False(SyncOptions.Default.DryRun);
    }
}
