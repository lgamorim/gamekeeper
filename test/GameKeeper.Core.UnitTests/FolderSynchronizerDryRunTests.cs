using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

/// <summary>
/// Dry-run behavior: the engine reports what it would do but performs no copies, deletions, or
/// backups, and leaves the persisted baseline untouched.
/// </summary>
public sealed class FolderSynchronizerDryRunTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";
    private const string BackupsFolder = ".gamekeeper-backups";

    private static readonly DateTime T1 = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_ReportTheCopyWithoutWriting_When_RunIsDry()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { DryRun = true });

        Assert.Equal(1, result.CopiedToSecond);
        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.DoesNotContain(fileSystem.AllFiles, f =>
            f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ReportTheDeletionWithoutDeletingOrBackingUp_When_RunIsDry()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\save.dat");
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { DryRun = true, PropagateDeletions = true });

        Assert.Equal(1, result.DeletedFromSecond);
        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.DoesNotContain(fileSystem.AllFiles, f => f.Contains(BackupsFolder));
    }

    [Fact]
    public void Should_LeaveTheBaselineUntouched_When_RunIsDry()
    {
        // The recorded state must describe what IS synced, not what a preview saw, or the next
        // real run would misclassify every previewed change as already handled.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\a.dat", "one", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);
        string manifest = fileSystem.AllFiles.Single(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        string recorded = fileSystem.File.ReadAllText(manifest);

        WriteFile(fileSystem, @"C:\game\b.dat", "two", T1);
        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { DryRun = true });

        Assert.Equal(recorded, fileSystem.File.ReadAllText(manifest));
    }

    [Fact]
    public void Should_NotCreateMissingRoots_When_RunIsDry()
    {
        var fileSystem = new MockFileSystem();
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { DryRun = true });

        Assert.False(fileSystem.Directory.Exists(GameRoot));
        Assert.False(fileSystem.Directory.Exists(CloudRoot));
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Should_LeaveTheRealRunFreeToAct_When_ItFollowsADryRun()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult preview = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { DryRun = true });
        SyncResult real = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(1, preview.CopiedToSecond);
        Assert.Equal(1, real.CopiedToSecond);
        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
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
