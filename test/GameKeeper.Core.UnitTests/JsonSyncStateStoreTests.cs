using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class JsonSyncStateStoreTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime Timestamp = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_RoundTripTheFiles_When_SavingThenLoading()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);
        var manifest = new SyncManifest(
        [
            new FileState("save.dat", Timestamp, 12),
            new FileState(@"profiles\slot1.sav", Timestamp.AddMinutes(1), 34),
        ]);

        store.Save(GameRoot, CloudRoot, manifest);
        SyncManifest loaded = store.Load(GameRoot, CloudRoot);

        Assert.Equal(2, loaded.Files.Count);
        Assert.True(loaded.TryGet("save.dat", out FileState? state));
        Assert.NotNull(state);
        Assert.Equal(12, state.Length);
        Assert.Equal(Timestamp, state.LastWriteTimeUtc);
        Assert.True(loaded.TryGet(@"profiles\slot1.sav", out _));
    }

    [Fact]
    public void Should_ReturnEmpty_When_NoManifestExists()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);

        SyncManifest loaded = store.Load(GameRoot, CloudRoot);

        Assert.Empty(loaded.Files);
    }

    [Fact]
    public void Should_ReturnEmpty_When_TheManifestIsCorrupt()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);
        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("save.dat", Timestamp, 12)]));
        string manifestPath = fileSystem.AllFiles.Single(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        fileSystem.File.WriteAllText(manifestPath, "{ this is not valid json");

        SyncManifest loaded = store.Load(GameRoot, CloudRoot);

        Assert.Empty(loaded.Files);
    }

    [Fact]
    public void Should_KeepSlotsSeparate_When_PairsAreDistinct()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);

        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("a.dat", Timestamp, 1)]));
        store.Save(GameRoot, @"C:\other-cloud", new SyncManifest([new FileState("b.dat", Timestamp, 2)]));

        SyncManifest first = store.Load(GameRoot, CloudRoot);
        SyncManifest second = store.Load(GameRoot, @"C:\other-cloud");
        Assert.True(first.TryGet("a.dat", out _));
        Assert.False(first.TryGet("b.dat", out _));
        Assert.True(second.TryGet("b.dat", out _));
        Assert.False(second.TryGet("a.dat", out _));
    }

    [Fact]
    public void Should_UseADifferentSlot_When_ThePairIsReversed()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);

        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("a.dat", Timestamp, 1)]));

        SyncManifest reversed = store.Load(CloudRoot, GameRoot);
        Assert.Empty(reversed.Files);
    }

    [Fact]
    public void Should_UseTheSameSlot_When_PathsDifferOnlyByCase()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);

        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("a.dat", Timestamp, 1)]));

        SyncManifest loaded = store.Load(@"C:\GAME", @"C:\Cloud");
        Assert.True(loaded.TryGet("a.dat", out _));
    }

    [Fact]
    public void Should_Throw_When_ConstructedWithNullArguments()
    {
        var fileSystem = new MockFileSystem();

        Assert.Throws<ArgumentNullException>(() => new JsonSyncStateStore(null!, StateDir));
        Assert.ThrowsAny<ArgumentException>(() => new JsonSyncStateStore(fileSystem, " "));
    }
}
