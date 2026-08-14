using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

/// <summary>
/// Pruning keeps the newest backups of each file and never touches anything that does not
/// match GameKeeper's own backup naming - the backups folder is the user's to keep things in.
/// </summary>
public sealed class FolderSynchronizerBackupPruningTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";
    private const string CloudBackups = @"C:\cloud\.gamekeeper-backups";

    private static readonly DateTime Older = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    // The backup the run itself creates: the cloud copy's own mtime, per the backup naming.
    private const string NewBackup = "save.dat.20260101080000.bak";

    [Fact]
    public void Should_PruneTheOldest_When_BackupsExceedTheKeepLimit()
    {
        MockFileSystem fileSystem = WithExistingBackups(12);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 3 });

        Assert.Equal(
            ["save.dat.20250101000011.bak", "save.dat.20250101000012.bak", NewBackup],
            BackupsIn(fileSystem, CloudBackups));
    }

    [Fact]
    public void Should_RetainAllBackups_When_WithinTheKeepLimit()
    {
        MockFileSystem fileSystem = WithExistingBackups(2);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 10 });

        Assert.Equal(3, BackupsIn(fileSystem, CloudBackups).Length);
    }

    [Fact]
    public void Should_KeepEverything_When_KeepBackupsIsZero()
    {
        MockFileSystem fileSystem = WithExistingBackups(12);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 0 });

        Assert.Equal(13, BackupsIn(fileSystem, CloudBackups).Length);
    }

    [Fact]
    public void Should_KeepTenBackupsPerFile_When_OptionsAreDefaulted()
    {
        MockFileSystem fileSystem = WithExistingBackups(12);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(10, BackupsIn(fileSystem, CloudBackups).Length);
    }

    [Fact]
    public void Should_LeaveUnrecognizedFilesAlone_When_Pruning()
    {
        // Anything not matching '<file>.<14-digit stamp>.bak' is not ours to delete.
        MockFileSystem fileSystem = WithExistingBackups(12);
        WriteFile(fileSystem, @"C:\cloud\.gamekeeper-backups\notes.txt", "mine", Older);
        WriteFile(fileSystem, @"C:\cloud\.gamekeeper-backups\save.dat.bak", "no stamp", Older);
        WriteFile(fileSystem, @"C:\cloud\.gamekeeper-backups\save.dat.nonsense.bak", "bad", Older);
        WriteFile(fileSystem, @"C:\cloud\.gamekeeper-backups\save.dat.2025010100001.bak", "13 digits", Older);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 3 });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\.gamekeeper-backups\notes.txt"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\.gamekeeper-backups\save.dat.bak"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\.gamekeeper-backups\save.dat.nonsense.bak"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\.gamekeeper-backups\save.dat.2025010100001.bak"));
    }

    [Fact]
    public void Should_NotTouchAnotherFilesBackups_When_Pruning()
    {
        MockFileSystem fileSystem = WithExistingBackups(12);
        for (int i = 1; i <= 5; i++)
        {
            WriteFile(
                fileSystem,
                $@"C:\cloud\.gamekeeper-backups\other.dat.202501010000{i:D2}.bak", $"o{i}", Older);
        }

        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 3 });

        int otherRemaining = BackupsIn(fileSystem, CloudBackups)
            .Count(f => f.StartsWith("other.dat.", StringComparison.Ordinal));
        Assert.Equal(5, otherRemaining);
    }

    [Fact]
    public void Should_NeverDeleteLiveSaveFiles_When_Pruning()
    {
        MockFileSystem fileSystem = WithExistingBackups(12);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 1 });

        Assert.True(fileSystem.File.Exists(@"C:\game\save.dat"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\save.dat"));
    }

    [Fact]
    public void Should_PruneNothing_When_RunIsDry()
    {
        MockFileSystem fileSystem = WithExistingBackups(12);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { KeepBackups = 3, DryRun = true });

        Assert.Equal(12, BackupsIn(fileSystem, CloudBackups).Length);
    }

    [Fact]
    public void Should_PruneNothing_When_BackupsAreDisabled()
    {
        // With backups off nothing is written, so nothing is reaped either.
        MockFileSystem fileSystem = WithExistingBackups(12);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { KeepBackups = 3, CreateBackups = false });

        Assert.Equal(12, BackupsIn(fileSystem, CloudBackups).Length);
    }

    [Fact]
    public void Should_PruneWithinTheirOwnFolder_When_BackupsAreNested()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\slots\a.sav", "game wins", Newer);
        WriteFile(fileSystem, @"C:\cloud\slots\a.sav", "cloud loses", Older);
        for (int i = 1; i <= 5; i++)
        {
            WriteFile(
                fileSystem,
                $@"C:\cloud\.gamekeeper-backups\slots\a.sav.202501010000{i:D2}.bak", $"v{i}", Older);
        }

        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { KeepBackups = 2 });

        Assert.Equal(2, BackupsIn(fileSystem, @"C:\cloud\.gamekeeper-backups\slots").Length);
    }

    /// <summary>
    /// Sets up a conflict that backs up the cloud copy, with <paramref name="existing"/> older
    /// backups already sitting in the cloud backups folder.
    /// </summary>
    private static MockFileSystem WithExistingBackups(int existing)
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "game wins", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud loses", Older);

        for (int i = 1; i <= existing; i++)
        {
            // Stamps in 2025, so every seeded backup is older than the one the run creates.
            string stamp = $"202501010000{i:D2}";
            WriteFile(fileSystem, $@"C:\cloud\.gamekeeper-backups\save.dat.{stamp}.bak", $"v{i}", Older);
        }

        return fileSystem;
    }

    private static string[] BackupsIn(MockFileSystem fileSystem, string directory)
    {
        return [.. fileSystem.Directory.EnumerateFiles(directory)
            .Select(path => fileSystem.FileInfo.New(path).Name)
            .Order()];
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
