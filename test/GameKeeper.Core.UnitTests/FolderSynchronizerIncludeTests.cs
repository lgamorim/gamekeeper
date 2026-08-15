using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

/// <summary>
/// Behaviors of the include allow-list: only matching files sync, in either direction, and a
/// file falling out of scope is left alone rather than treated as deleted.
/// </summary>
public sealed class FolderSynchronizerIncludeTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime Now = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_CopyOnlyMatchingFiles_When_IncludeIsSet()
    {
        MockFileSystem fileSystem = GameWithMixedFiles();
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { IncludePatterns = ["*.sav"] });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save1.sav"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\slots\quick.sav"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\config.ini"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\debug.log"));
        Assert.Equal(2, result.CopiedToSecond);
    }

    [Fact]
    public void Should_CopyEverything_When_NoIncludeIsSet()
    {
        // The default must stay exactly as before: an empty include list is not include-none.
        MockFileSystem fileSystem = GameWithMixedFiles();
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(4, result.CopiedToSecond);
    }

    [Fact]
    public void Should_LetTheExcludeWin_When_CombinedWithAnInclude()
    {
        MockFileSystem fileSystem = GameWithMixedFiles();
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot,
            CloudRoot,
            new SyncOptions { IncludePatterns = ["*.sav"], ExcludePatterns = [@"slots\*"] });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save1.sav"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\slots\quick.sav"));
    }

    [Fact]
    public void Should_StillBringBackIncludedFiles_When_TheyOnlyExistOnTheOtherSide()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(GameRoot);
        WriteFile(fileSystem, @"C:\cloud\from-cloud.sav", "cloud", Now);
        WriteFile(fileSystem, @"C:\cloud\ignored.log", "log", Now);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { IncludePatterns = ["*.sav"] });

        Assert.True(fileSystem.File.Exists(@"C:\game\from-cloud.sav"));
        Assert.False(fileSystem.File.Exists(@"C:\game\ignored.log"));
    }

    [Fact]
    public void Should_RecordOnlyInScopeFiles_When_TheBaselineIsSaved()
    {
        MockFileSystem fileSystem = GameWithMixedFiles();
        var stateStore = new JsonSyncStateStore(fileSystem, StateDir);
        var synchronizer = new FolderSynchronizer(fileSystem, stateStore);

        synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { IncludePatterns = ["*.sav"] });

        SyncManifest baseline = stateStore.Load(GameRoot, CloudRoot);
        Assert.NotEmpty(baseline.Files);
        Assert.All(baseline.Files, f => Assert.EndsWith(".sav", f.RelativePath));
    }

    [Fact]
    public void Should_NotTreatItAsADeletion_When_AFileFallsOutOfTheIncludeList()
    {
        // Narrowing the include list must leave the other side alone, exactly as adding an
        // exclude does - the file is out of scope, not deleted.
        MockFileSystem fileSystem = GameWithMixedFiles();
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        SyncResult result = synchronizer.Synchronize(
            GameRoot,
            CloudRoot,
            new SyncOptions { IncludePatterns = ["*.sav"], PropagateDeletions = true });

        Assert.Equal(0, result.DeletedFromFirst);
        Assert.Equal(0, result.DeletedFromSecond);
        Assert.True(fileSystem.File.Exists(@"C:\cloud\config.ini"));
    }

    [Fact]
    public void Should_StillReplicateEmptyDirectories_When_AnIncludeIsSet()
    {
        // The trap: an include list names files, so no directory matches it. If includes were
        // applied to directories too, every directory would be filtered out and this would
        // silently stop working.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save1.sav", "save", Now);
        fileSystem.AddDirectory(@"C:\game\empty-slot");
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { IncludePatterns = ["*.sav"] });

        Assert.True(fileSystem.Directory.Exists(@"C:\cloud\empty-slot"));
    }

    [Fact]
    public void Should_NotReplicateExcludedDirectories_When_AnIncludeIsSet()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save1.sav", "save", Now);
        fileSystem.AddDirectory(@"C:\game\cache");
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot,
            CloudRoot,
            new SyncOptions { IncludePatterns = ["*.sav"], ExcludePatterns = ["cache*"] });

        Assert.False(fileSystem.Directory.Exists(@"C:\cloud\cache"));
    }

    private static MockFileSystem GameWithMixedFiles()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save1.sav", "save", Now);
        WriteFile(fileSystem, @"C:\game\slots\quick.sav", "quick", Now);
        WriteFile(fileSystem, @"C:\game\config.ini", "cfg", Now);
        WriteFile(fileSystem, @"C:\game\debug.log", "log", Now);
        fileSystem.AddDirectory(CloudRoot);
        return fileSystem;
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
