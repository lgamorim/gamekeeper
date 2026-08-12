using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class JsonSyncStateStoreAtomicWriteTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime Timestamp = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_LeaveNoStagingFile_When_SaveSucceeds()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);

        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("save.dat", Timestamp, 12)]));

        Assert.Empty(StagingFiles(fileSystem));
        Assert.True(store.Load(GameRoot, CloudRoot).TryGet("save.dat", out _));
    }

    [Fact]
    public void Should_ReplaceTheManifestCompletely_When_SavingOverAnExistingOne()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonSyncStateStore(fileSystem, StateDir);
        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("first.dat", Timestamp, 1)]));

        store.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("second.dat", Timestamp, 2)]));

        SyncManifest loaded = store.Load(GameRoot, CloudRoot);
        Assert.Single(loaded.Files);
        Assert.True(loaded.TryGet("second.dat", out _));
        Assert.Empty(StagingFiles(fileSystem));
    }

    [Fact]
    public void Should_KeepThePreviousBaselineLoadable_When_TheSwapFails()
    {
        var mockFileSystem = new MockFileSystem();
        var healthyStore = new JsonSyncStateStore(mockFileSystem, StateDir);
        healthyStore.Save(GameRoot, CloudRoot, new SyncManifest([new FileState("save.dat", Timestamp, 12)]));
        var failingStore = new JsonSyncStateStore(FileSystemThatFailsOnMove(mockFileSystem), StateDir);

        Assert.Throws<IOException>(() => failingStore.Save(
            GameRoot, CloudRoot, new SyncManifest([new FileState("other.dat", Timestamp, 1)])));

        SyncManifest loaded = healthyStore.Load(GameRoot, CloudRoot);
        Assert.Single(loaded.Files);
        Assert.True(loaded.TryGet("save.dat", out _));
    }

    [Fact]
    public void Should_LeaveNoStagingFile_When_TheSwapFails()
    {
        var mockFileSystem = new MockFileSystem();
        var failingStore = new JsonSyncStateStore(FileSystemThatFailsOnMove(mockFileSystem), StateDir);

        Assert.Throws<IOException>(() => failingStore.Save(
            GameRoot, CloudRoot, new SyncManifest([new FileState("save.dat", Timestamp, 12)])));

        Assert.Empty(StagingFiles(mockFileSystem));
    }

    private static IEnumerable<string> StagingFiles(MockFileSystem fileSystem) =>
        fileSystem.AllFiles.Where(f => f.EndsWith(".gamekeeper-tmp", StringComparison.OrdinalIgnoreCase));

    // A file system that behaves like the mock except that every move fails, as a full disk or
    // yanked drive would mid-swap. Anything beyond the delegated members surfacing here means
    // the store grew a new dependency and the fake must be extended deliberately.
    private static IFileSystem FileSystemThatFailsOnMove(MockFileSystem inner)
    {
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.Path.Returns(inner.Path);
        fileSystem.Directory.Returns(inner.Directory);

        var file = Substitute.For<IFile>();
        file.Exists(Arg.Any<string>()).Returns(call => inner.File.Exists(call.Arg<string>()));
        file.ReadAllText(Arg.Any<string>()).Returns(call => inner.File.ReadAllText(call.Arg<string>()));
        file.When(f => f.WriteAllText(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => inner.File.WriteAllText(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        file.When(f => f.Delete(Arg.Any<string>()))
            .Do(call => inner.File.Delete(call.Arg<string>()));
        file.When(f => f.Move(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(_ => throw new IOException("The device is not ready."));
        fileSystem.File.Returns(file);

        return fileSystem;
    }
}
