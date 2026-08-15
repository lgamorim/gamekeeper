using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

/// <summary>
/// Behaviors of the exclude filter: excluded paths are never copied, never deleted, and never
/// mistaken for a deletion when they linger in the baseline.
/// </summary>
public sealed class FolderSynchronizerExcludeTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime T1 = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_NotCopyTheFile_When_ItIsExcluded()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep", T1);
        WriteFile(fileSystem, @"C:\game\debug.log", "noise", T1);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { ExcludePatterns = ["*.log"] });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\debug.log"));
    }

    [Fact]
    public void Should_NeitherCopyNorReplicate_When_ASubfolderIsExcluded()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep", T1);
        WriteFile(fileSystem, @"C:\game\cache\data.bin", "noise", T1);
        var synchronizer = CreateSynchronizer(fileSystem);

        // 'cache*' matches both the folder and everything beneath it.
        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { ExcludePatterns = ["cache*"] });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\cache\data.bin"));
        Assert.False(fileSystem.Directory.Exists(@"C:\cloud\cache"));
    }

    [Fact]
    public void Should_ApplyEveryPattern_When_SeveralAreGiven()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep", T1);
        WriteFile(fileSystem, @"C:\game\debug.log", "noise", T1);
        WriteFile(fileSystem, @"C:\game\crash.tmp", "noise", T1);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { ExcludePatterns = ["*.log", "*.tmp"] });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\debug.log"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\crash.tmp"));
    }

    [Fact]
    public void Should_NotTreatItAsADeletion_When_AnExcludedPathLingersInTheBaseline()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep", T1);
        WriteFile(fileSystem, @"C:\game\debug.log", "noise", T1);
        var synchronizer = CreateSynchronizer(fileSystem);
        // The first sync without excludes records debug.log in the baseline on both sides.
        synchronizer.Synchronize(GameRoot, CloudRoot);

        // Now exclude the log and allow deletions: the excluded path must be left alone, not
        // deleted as if it had disappeared.
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot,
            new SyncOptions { ExcludePatterns = ["*.log"], PropagateDeletions = true });

        Assert.True(fileSystem.File.Exists(@"C:\game\debug.log"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\debug.log"));
        Assert.Equal(0, result.DeletedFromFirst);
        Assert.Equal(0, result.DeletedFromSecond);
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
