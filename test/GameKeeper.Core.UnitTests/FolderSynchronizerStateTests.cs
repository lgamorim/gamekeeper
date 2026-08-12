using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

/// <summary>
/// Pins the behaviors that only exist because a baseline is recorded between runs: telling a
/// deletion from a file that never existed, and telling a routine update from a divergence.
/// </summary>
public sealed class FolderSynchronizerStateTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime T1 = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T3 = new(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_CopyBothWaysAndWriteTheManifest_When_FirstSyncRuns()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\a.dat", "from game", T1);
        WriteFile(fileSystem, @"C:\cloud\b.dat", "from cloud", T1);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.True(fileSystem.File.Exists(@"C:\cloud\a.dat"));
        Assert.True(fileSystem.File.Exists(@"C:\game\b.dat"));
        Assert.Contains(fileSystem.AllFiles, f =>
            f.Contains(@"\state\", StringComparison.OrdinalIgnoreCase)
            && f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ResurrectTheFile_When_OneSideDeletedIt()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\save.dat");
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal(1, result.CopiedToFirst);
    }

    [Fact]
    public void Should_ReportAllUpToDate_When_SecondRunHasNoChanges()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\a.dat", "from game", T1);
        WriteFile(fileSystem, @"C:\cloud\b.dat", "from cloud", T1);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        SyncResult second = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(0, second.CopiedToFirst);
        Assert.Equal(0, second.CopiedToSecond);
        Assert.Equal(2, second.UpToDate);
    }

    [Fact]
    public void Should_NotTouchTheFile_When_ItLivesInTheBackupsFolder()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", T1);
        WriteFile(fileSystem, @"C:\game\.gamekeeper-backups\old.bak", "stale", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\.gamekeeper-backups\old.bak"));
        SyncedFile file = Assert.Single(result.Files);
        Assert.Equal("save.dat", file.RelativePath);
    }

    [Fact]
    public void Should_CopyTheNewerSide_When_BothSidesChangedSinceTheBaseline()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\save.dat", "GAME-WINS", T3);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud-loses", T2);
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("GAME-WINS", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
    }

    [Fact]
    public void Should_LetTheGameWin_When_BothChangedToTheSameTimestampWithDifferentLengths()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\save.dat", "GAME-IS-LONGER", T2);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud", T2);
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("GAME-IS-LONGER", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
    }

    [Fact]
    public void Should_ConvergeToUpToDate_When_RunFollowsAResolvedDivergence()
    {
        // No baseline, timestamps within tolerance, different lengths: the first run copies
        // the raw-newer side across; the next run must then see the pair as in sync.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "GAME-IS-LONGER", T2.AddSeconds(1));
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud", T2);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult first = synchronizer.Synchronize(GameRoot, CloudRoot);
        SyncResult second = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("GAME-IS-LONGER", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, first.CopiedToSecond);
        Assert.Equal(0, second.CopiedToFirst);
        Assert.Equal(0, second.CopiedToSecond);
        Assert.Equal(1, second.UpToDate);
    }

    [Fact]
    public void Should_KeepTheEdit_When_TheOtherSideDeletedTheFile()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\save.dat", "edited", T2);
        fileSystem.File.Delete(@"C:\cloud\save.dat");
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("edited", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
    }

    [Fact]
    public void Should_DropThePathSilently_When_BothSidesDeletedIt()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\save.dat");
        fileSystem.File.Delete(@"C:\cloud\save.dat");
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Empty(result.Files);
        Assert.False(fileSystem.File.Exists(@"C:\game\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat"));
    }

    private static FolderSynchronizer CreateSynchronizer(MockFileSystem fileSystem)
    {
        return new FolderSynchronizer(fileSystem, new JsonSyncStateStore(fileSystem, StateDir));
    }

    private static void WriteFile(MockFileSystem fileSystem, string path, string content, DateTime lastWriteUtc)
    {
        fileSystem.AddFile(path, new MockFileData(content) { LastWriteTime = lastWriteUtc });
    }
}
