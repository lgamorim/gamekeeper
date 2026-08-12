using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using Xunit;

namespace GameKeeper.Core.UnitTests;

/// <summary>
/// Pins the staging-and-swap copy mechanics: temp files are invisible to the sync, and a failed
/// swap never leaves debris behind.
/// </summary>
public sealed class FolderSynchronizerAtomicCopyTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime Newer = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_IgnoreALeftoverTempFile_When_Syncing()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", Newer);
        WriteFile(fileSystem, @"C:\game\save.dat.gamekeeper-tmp", "junk", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat.gamekeeper-tmp"));
        Assert.Equal(1, result.CopiedToSecond);
    }

    [Fact]
    public void Should_KeepTempFilesOutOfTheBaseline_When_Syncing()
    {
        // A temp file that reached the baseline would look like a user deletion later.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat.gamekeeper-tmp", "junk", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var store = new JsonSyncStateStore(fileSystem, StateDir);
        var synchronizer = new FolderSynchronizer(fileSystem, store);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Empty(store.Load(GameRoot, CloudRoot).Files);
    }

    [Fact]
    public void Should_IgnoreALeftoverTempFile_When_ItSitsInASubfolder()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\slots\a.sav", "progress", Newer);
        WriteFile(fileSystem, @"C:\game\slots\a.sav.gamekeeper-tmp", "junk", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.True(fileSystem.File.Exists(@"C:\cloud\slots\a.sav"));
        SyncedFile file = Assert.Single(result.Files);
        Assert.Equal(@"slots\a.sav", file.RelativePath);
    }

    [Fact]
    public void Should_KeepTheSourceTimestampAndContent_When_AFileIsCopied()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(Newer, fileSystem.File.GetLastWriteTimeUtc(@"C:\cloud\save.dat"));
        Assert.Empty(StagingFiles(fileSystem));
    }

    [Fact]
    public void Should_ReplaceTheDestinationWithoutLeavingATemp_When_CopyingOverAnExistingFile()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "new", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "old", Newer.AddHours(-1));
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("new", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Empty(StagingFiles(fileSystem));
    }

    [Fact]
    public void Should_LeaveNoTempBehindAndPropagate_When_TheSwapFails()
    {
        var mockFileSystem = new MockFileSystem();
        WriteFile(mockFileSystem, @"C:\game\save.dat", "progress", Newer);
        mockFileSystem.AddDirectory(CloudRoot);
        var synchronizer = new FolderSynchronizer(
            FileSystemThatFailsOnMove(mockFileSystem),
            new JsonSyncStateStore(mockFileSystem, StateDir));

        Assert.Throws<IOException>(() => synchronizer.Synchronize(GameRoot, CloudRoot));

        Assert.Empty(StagingFiles(mockFileSystem));
        Assert.False(mockFileSystem.File.Exists(@"C:\cloud\save.dat"));
    }

    private static IEnumerable<string> StagingFiles(MockFileSystem fileSystem) =>
        fileSystem.AllFiles.Where(f => f.EndsWith(".gamekeeper-tmp", StringComparison.OrdinalIgnoreCase));

    private static FolderSynchronizer CreateSynchronizer(MockFileSystem fileSystem)
    {
        return new FolderSynchronizer(fileSystem, new JsonSyncStateStore(fileSystem, StateDir));
    }

    private static void WriteFile(MockFileSystem fileSystem, string path, string content, DateTime lastWriteUtc)
    {
        fileSystem.AddFile(path, new MockFileData(content) { LastWriteTime = lastWriteUtc });
    }

    // A file system that behaves like the mock except that every move fails, as a full disk or
    // yanked drive would mid-swap. The delegated members double as an inventory of everything
    // the engine touches; anything else surfacing here means it grew a new dependency.
    private static IFileSystem FileSystemThatFailsOnMove(MockFileSystem inner)
    {
        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.Path.Returns(inner.Path);
        fileSystem.Directory.Returns(inner.Directory);
        fileSystem.FileInfo.Returns(inner.FileInfo);

        var file = Substitute.For<IFile>();
        file.Exists(Arg.Any<string>()).Returns(call => inner.File.Exists(call.Arg<string>()));
        file.GetLastWriteTimeUtc(Arg.Any<string>())
            .Returns(call => inner.File.GetLastWriteTimeUtc(call.Arg<string>()));
        file.When(f => f.Copy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call => inner.File.Copy(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<bool>(2)));
        file.When(f => f.SetLastWriteTimeUtc(Arg.Any<string>(), Arg.Any<DateTime>()))
            .Do(call => inner.File.SetLastWriteTimeUtc(call.ArgAt<string>(0), call.ArgAt<DateTime>(1)));
        file.When(f => f.Delete(Arg.Any<string>()))
            .Do(call => inner.File.Delete(call.Arg<string>()));
        file.When(f => f.Move(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(_ => throw new IOException("The device is not ready."));
        fileSystem.File.Returns(file);

        return fileSystem;
    }
}
