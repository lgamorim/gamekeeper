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
    private const string BackupsFolder = ".gamekeeper-backups";

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
        Assert.Equal(0, result.DeletedFromSecond);
    }

    [Fact]
    public void Should_DeleteTheCloudCopy_When_GameDeletedItAndDeletionsAreOn()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep-me", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\save.dat");
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { PropagateDeletions = true });

        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.DeletedFromSecond);
        Assert.Equal(0, result.Conflicts);
        Assert.Contains(fileSystem.AllFiles, f =>
            IsBackup(f, CloudRoot) && fileSystem.File.ReadAllText(f) == "keep-me");
    }

    [Fact]
    public void Should_StillDeleteWithoutBackingUp_When_BackupsAreDisabled()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep-me", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\save.dat");
        synchronizer.Synchronize(
            GameRoot, CloudRoot,
            new SyncOptions { PropagateDeletions = true, CreateBackups = false });

        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat"));
        Assert.DoesNotContain(fileSystem.AllFiles, f => f.Contains(BackupsFolder));
    }

    [Fact]
    public void Should_DeleteTheGameCopy_When_CloudDeletedItAndDeletionsAreOn()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep-me", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\cloud\save.dat");
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { PropagateDeletions = true });

        Assert.False(fileSystem.File.Exists(@"C:\game\save.dat"));
        Assert.Equal(1, result.DeletedFromFirst);
        Assert.Equal(0, result.DeletedFromSecond);
    }

    [Fact]
    public void Should_ForgetThePath_When_ADeletionWasPropagated()
    {
        // Once propagated, the file must not reappear or be re-deleted on the next run.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "keep-me", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\save.dat");
        synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { PropagateDeletions = true });
        SyncResult third = synchronizer.Synchronize(GameRoot, CloudRoot, new SyncOptions { PropagateDeletions = true });

        Assert.Empty(third.Files);
        Assert.False(fileSystem.File.Exists(@"C:\game\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat"));
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
        Assert.Equal(1, result.Conflicts);

        // The losing cloud copy survives in the cloud folder's backups, stamped with its own
        // last write time (T2), not the moment the backup was taken.
        Assert.Equal("cloud-loses", fileSystem.File.ReadAllText(
            @"C:\cloud\.gamekeeper-backups\save.dat.20260101100000.bak"));
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
        Assert.Equal(1, result.Conflicts);
        Assert.Contains(fileSystem.AllFiles, f =>
            IsBackup(f, CloudRoot) && fileSystem.File.ReadAllText(f) == "cloud");
    }

    [Fact]
    public void Should_BackUpTheLoserOnItsOwnRoot_When_TheGameSideLosesAConflict()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\save.dat", "game-loses", T2);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "CLOUD-WINS", T3);
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("CLOUD-WINS", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal(1, result.Conflicts);
        Assert.Contains(fileSystem.AllFiles, f =>
            IsBackup(f, GameRoot) && fileSystem.File.ReadAllText(f) == "game-loses");
        Assert.DoesNotContain(fileSystem.AllFiles, f => IsBackup(f, CloudRoot));
    }

    [Fact]
    public void Should_CreateTheBackupSubfolder_When_TheConflictedFileIsNested()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\slots\a.sav", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\slots\a.sav", "GAME-WINS", T3);
        WriteFile(fileSystem, @"C:\cloud\slots\a.sav", "cloud-loses", T2);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("cloud-loses", fileSystem.File.ReadAllText(
            @"C:\cloud\.gamekeeper-backups\slots\a.sav.20260101100000.bak"));
    }

    [Fact]
    public void Should_NotBackUp_When_TheOverwriteIsRoutine()
    {
        // Only conflicts and deletions destroy something the baseline cannot explain; a
        // routine update of the unchanged side is the sync working as intended.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\save.dat", "updated", T2);
        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(1, result.CopiedToSecond);
        Assert.Equal(0, result.Conflicts);
        Assert.DoesNotContain(fileSystem.AllFiles, f => f.Contains(BackupsFolder));
    }

    [Fact]
    public void Should_BackUpTheCloudCopy_When_OneWayOverwritesASameTimeDivergence()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "GAME-IS-LONGER", T1);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud", T1.AddSeconds(1));
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.Equal("GAME-IS-LONGER", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.Conflicts);
        Assert.Contains(fileSystem.AllFiles, f =>
            IsBackup(f, CloudRoot) && fileSystem.File.ReadAllText(f) == "cloud");
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
        Assert.Equal(1, first.Conflicts);
        Assert.Equal(0, second.CopiedToFirst);
        Assert.Equal(0, second.CopiedToSecond);
        Assert.Equal(0, second.Conflicts);
        Assert.Equal(1, second.UpToDate);
    }

    [Fact]
    public void Should_KeepTheEdit_When_TheOtherSideDeletedTheFile()
    {
        // Even with deletions on, an edit is never lost to a delete - but resolving the
        // collision that way is flagged as a conflict.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "orig", T1);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        WriteFile(fileSystem, @"C:\game\save.dat", "edited", T2);
        fileSystem.File.Delete(@"C:\cloud\save.dat");
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { PropagateDeletions = true });

        Assert.Equal("edited", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal(0, result.DeletedFromFirst);

        // The deleted side left nothing behind to preserve, so no backup is written.
        Assert.DoesNotContain(fileSystem.AllFiles, f => f.Contains(BackupsFolder));
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
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { PropagateDeletions = true });

        Assert.Empty(result.Files);
        Assert.Equal(0, result.DeletedFromFirst);
        Assert.Equal(0, result.DeletedFromSecond);
        Assert.False(fileSystem.File.Exists(@"C:\game\save.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\save.dat"));
    }

    [Fact]
    public void Should_OnlyDeleteFromTheCloud_When_ModeIsUpAndDeletionsAreOn()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\keep.dat", "keep", T1);
        WriteFile(fileSystem, @"C:\game\gone.dat", "gone", T1);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\gone.dat");
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot,
            new SyncOptions { Mode = SyncMode.FirstToSecond, PropagateDeletions = true });

        Assert.False(fileSystem.File.Exists(@"C:\cloud\gone.dat"));
        Assert.True(fileSystem.File.Exists(@"C:\cloud\keep.dat"));
        Assert.Equal(1, result.DeletedFromSecond);
        Assert.Equal(0, result.DeletedFromFirst);
    }

    [Fact]
    public void Should_NotDeleteAnything_When_ModeIsUpAndDeletionsAreOff()
    {
        // One-way with deletions off leaves the orphaned destination file alone, reported as
        // skipped: it is out of the source's authority, not deleted.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\gone.dat", "gone", T1);
        var synchronizer = CreateSynchronizer(fileSystem);
        synchronizer.Synchronize(GameRoot, CloudRoot);

        fileSystem.File.Delete(@"C:\game\gone.dat");
        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\gone.dat"));
        Assert.Equal(0, result.DeletedFromSecond);
        Assert.Equal(1, result.Skipped);
    }

    private static FolderSynchronizer CreateSynchronizer(MockFileSystem fileSystem)
    {
        return new FolderSynchronizer(fileSystem, new JsonSyncStateStore(fileSystem, StateDir));
    }

    private static bool IsBackup(string fullPath, string root)
    {
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && fullPath.Contains(BackupsFolder);
    }

    private static void WriteFile(MockFileSystem fileSystem, string path, string content, DateTime lastWriteUtc)
    {
        fileSystem.AddFile(path, new MockFileData(content) { LastWriteTime = lastWriteUtc });
    }
}
