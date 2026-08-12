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
}
