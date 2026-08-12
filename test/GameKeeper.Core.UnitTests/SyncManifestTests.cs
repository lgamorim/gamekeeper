using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class SyncManifestTests
{
    private static readonly DateTime Timestamp = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_HaveNoFiles_When_Empty()
    {
        Assert.Empty(SyncManifest.Empty.Files);
    }

    [Fact]
    public void Should_ReturnStoredEntry_When_PathIsKnown()
    {
        var manifest = new SyncManifest([new FileState("save.dat", Timestamp, 10)]);

        bool found = manifest.TryGet("save.dat", out FileState? state);

        Assert.True(found);
        Assert.NotNull(state);
        Assert.Equal(10, state.Length);
        Assert.Equal(Timestamp, state.LastWriteTimeUtc);
    }

    [Fact]
    public void Should_MatchCaseInsensitively_When_LookingUpAPath()
    {
        var manifest = new SyncManifest([new FileState(@"Profiles\Save.dat", Timestamp, 10)]);

        bool found = manifest.TryGet(@"profiles\save.dat", out FileState? state);

        Assert.True(found);
        Assert.NotNull(state);
    }

    [Fact]
    public void Should_ReturnFalseAndNull_When_PathIsAbsent()
    {
        var manifest = new SyncManifest([new FileState("save.dat", Timestamp, 10)]);

        bool found = manifest.TryGet("missing.dat", out FileState? state);

        Assert.False(found);
        Assert.Null(state);
    }

    [Fact]
    public void Should_KeepTheLastEntry_When_PathsDuplicate()
    {
        var manifest = new SyncManifest(
        [
            new FileState("save.dat", Timestamp, 10),
            new FileState("SAVE.DAT", Timestamp, 20),
        ]);

        bool found = manifest.TryGet("save.dat", out FileState? state);

        Assert.True(found);
        Assert.NotNull(state);
        Assert.Equal(20, state.Length);
        Assert.Single(manifest.Files);
    }

    [Fact]
    public void Should_Throw_When_FilesAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SyncManifest(null!));
    }
}
