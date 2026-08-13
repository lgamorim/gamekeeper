using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class FolderSynchronizerTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";
    private const string StateDir = @"C:\state";

    private static readonly DateTime Older = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Should_CopyToSecond_When_FileExistsOnlyInFirst()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
        Assert.Equal(0, result.CopiedToFirst);
    }

    [Fact]
    public void Should_CopyToFirst_When_FileExistsOnlyInSecond()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(GameRoot);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "progress", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal(1, result.CopiedToFirst);
        Assert.Equal(0, result.CopiedToSecond);
    }

    [Fact]
    public void Should_OverwriteSecond_When_FirstIsNewer()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "new", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "old", Older);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("new", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        SyncedFile file = Assert.Single(result.Files);
        Assert.Equal(SyncAction.CopiedToSecond, file.Action);
    }

    [Fact]
    public void Should_OverwriteFirst_When_SecondIsNewer()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "old", Older);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "new", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("new", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        SyncedFile file = Assert.Single(result.Files);
        Assert.Equal(SyncAction.CopiedToFirst, file.Action);
    }

    [Fact]
    public void Should_DoNothing_When_TimestampsAndLengthsAreEqual()
    {
        // Same moment, same size: the engine treats the copies as identical without ever
        // reading content, so deliberately different bytes must both survive.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "AAAA", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "BBBB", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("AAAA", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal("BBBB", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.UpToDate);
    }

    [Fact]
    public void Should_DoNothing_When_TimestampsDifferWithinTolerance()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "same", Newer.AddSeconds(1));
        WriteFile(fileSystem, @"C:\cloud\save.dat", "same", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(0, result.CopiedToFirst);
        Assert.Equal(0, result.CopiedToSecond);
        Assert.Equal(1, result.UpToDate);
    }

    [Fact]
    public void Should_DoNothing_When_TimestampsDifferExactlyAtTolerance()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "same", Newer.AddSeconds(2));
        WriteFile(fileSystem, @"C:\cloud\save.dat", "same", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(1, result.UpToDate);
    }

    [Fact]
    public void Should_Copy_When_TimestampsDifferJustBeyondTolerance()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "game", Newer.AddSeconds(3));
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("game", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
    }

    [Fact]
    public void Should_LetFirstWin_When_TimestampsTieWithDifferentLengths()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "GAME-IS-LONGER", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("GAME-IS-LONGER", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal(0, result.UpToDate);
    }

    [Fact]
    public void Should_SyncRecursively_When_FilesAreNested()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\profiles\slot1\save.dat", "one", Newer);
        WriteFile(fileSystem, @"C:\cloud\profiles\slot2\save.dat", "two", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("one", fileSystem.File.ReadAllText(@"C:\cloud\profiles\slot1\save.dat"));
        Assert.Equal("two", fileSystem.File.ReadAllText(@"C:\game\profiles\slot2\save.dat"));
    }

    [Fact]
    public void Should_MakeSecondRunANoOp_When_CopyPreservesTheTimestamp()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);
        SyncResult second = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(Newer, fileSystem.File.GetLastWriteTimeUtc(@"C:\cloud\save.dat"));
        Assert.Equal(0, second.CopiedToSecond);
        Assert.Equal(0, second.CopiedToFirst);
        Assert.Equal(1, second.UpToDate);
    }

    [Fact]
    public void Should_PerformNoCopies_When_TreesAreIdentical()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "same", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "same", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(0, result.CopiedToSecond);
        Assert.Equal(0, result.CopiedToFirst);
        Assert.Equal(1, result.UpToDate);
    }

    [Fact]
    public void Should_ProduceEmptyResult_When_BothFoldersAreEmpty()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(GameRoot);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Empty(result.Files);
    }

    [Fact]
    public void Should_CreateMissingFolders_When_RootsDoNotExist()
    {
        var fileSystem = new MockFileSystem();
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.True(fileSystem.Directory.Exists(GameRoot));
        Assert.True(fileSystem.Directory.Exists(CloudRoot));
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Should_DoNothing_When_BothRootsAreTheSamePath()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, GameRoot);

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        SyncedFile file = Assert.Single(result.Files);
        Assert.Equal(SyncAction.None, file.Action);
    }

    [Theory]
    [InlineData(SyncMode.Bidirectional)]
    [InlineData(SyncMode.FirstToSecond)]
    [InlineData(SyncMode.SecondToFirst)]
    public void Should_DoNothing_When_BothRootsAreTheSamePathRegardlessOfMode(SyncMode mode)
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "progress", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, GameRoot, new SyncOptions { Mode = mode });

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        SyncedFile file = Assert.Single(result.Files);
        Assert.Equal(SyncAction.None, file.Action);
    }

    [Fact]
    public void Should_CopyBinaryContentVerbatim_When_FileIsNotText()
    {
        var fileSystem = new MockFileSystem();
        byte[] payload = [0x00, 0xFF, 0x10, 0x7F, 0x80, 0x00, 0x42];
        fileSystem.AddFile(@"C:\game\save.bin", new MockFileData(payload) { LastWriteTime = Newer });
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(payload, fileSystem.File.ReadAllBytes(@"C:\cloud\save.bin"));
    }

    [Fact]
    public void Should_HandleSpecialCharacters_When_NamesContainThem()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save (1) naïve.dat", "progress", Newer);
        fileSystem.AddDirectory(CloudRoot);
        var synchronizer = CreateSynchronizer(fileSystem);

        synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal("progress", fileSystem.File.ReadAllText(@"C:\cloud\save (1) naïve.dat"));
    }

    [Fact]
    public void Should_ReportEachDirection_When_TreeIsMixed()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\game-only.dat", "a", Newer);
        WriteFile(fileSystem, @"C:\cloud\cloud-only.dat", "b", Newer);
        WriteFile(fileSystem, @"C:\game\shared.dat", "new", Newer);
        WriteFile(fileSystem, @"C:\cloud\shared.dat", "old", Older);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(GameRoot, CloudRoot);

        Assert.Equal(2, result.CopiedToSecond);
        Assert.Equal(1, result.CopiedToFirst);
        Assert.Equal(3, result.Files.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_Throw_When_FirstFolderIsBlank(string? folder)
    {
        var fileSystem = new MockFileSystem();
        var synchronizer = CreateSynchronizer(fileSystem);

        Assert.ThrowsAny<ArgumentException>(() => synchronizer.Synchronize(folder!, CloudRoot));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_Throw_When_SecondFolderIsBlank(string? folder)
    {
        var fileSystem = new MockFileSystem();
        var synchronizer = CreateSynchronizer(fileSystem);

        Assert.ThrowsAny<ArgumentException>(() => synchronizer.Synchronize(GameRoot, folder!));
    }

    [Fact]
    public void Should_PushWithoutPulling_When_ModeIsUp()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\push.dat", "push", Newer);
        WriteFile(fileSystem, @"C:\cloud\keep.dat", "keep", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\push.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\game\keep.dat"));
        Assert.Equal(1, result.CopiedToSecond);
        Assert.Equal(0, result.CopiedToFirst);
    }

    [Fact]
    public void Should_NotOverwriteEitherSide_When_ModeIsUpAndCloudIsNewer()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "old", Older);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "newer", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.Equal("old", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal("newer", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(0, result.CopiedToFirst);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.UpToDate);
    }

    [Fact]
    public void Should_OverwriteCloud_When_ModeIsUpAndGameIsNewer()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "new", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "old", Older);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.Equal("new", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
    }

    [Fact]
    public void Should_ReportSkippedWithoutCopying_When_ModeIsUpAndFileExistsOnlyInCloud()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(GameRoot);
        WriteFile(fileSystem, @"C:\cloud\cloud-only.dat", "keep", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.True(fileSystem.File.Exists(@"C:\cloud\cloud-only.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\game\cloud-only.dat"));
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.UpToDate);
    }

    [Fact]
    public void Should_PullWithoutPushing_When_ModeIsDown()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\keep.dat", "keep", Newer);
        WriteFile(fileSystem, @"C:\cloud\pull.dat", "pull", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.SecondToFirst });

        Assert.True(fileSystem.File.Exists(@"C:\game\pull.dat"));
        Assert.False(fileSystem.File.Exists(@"C:\cloud\keep.dat"));
        Assert.Equal(1, result.CopiedToFirst);
        Assert.Equal(0, result.CopiedToSecond);
    }

    [Fact]
    public void Should_NotOverwriteEitherSide_When_ModeIsDownAndGameIsNewer()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "newer", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "old", Older);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.SecondToFirst });

        Assert.Equal("newer", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal("old", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(0, result.CopiedToSecond);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void Should_OverwriteGame_When_ModeIsDownAndCloudIsNewer()
    {
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "old", Older);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "new", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.SecondToFirst });

        Assert.Equal("new", fileSystem.File.ReadAllText(@"C:\game\save.dat"));
        Assert.Equal(1, result.CopiedToFirst);
    }

    [Fact]
    public void Should_CopySourceOverDestination_When_OneWayTimestampsMatchButLengthsDiffer()
    {
        // Same moment but different sizes means the copies diverged; in a one-way run the
        // source is authoritative even though the timestamps alone cannot justify a copy.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "GAME-IS-LONGER", Newer);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "cloud", Newer.AddSeconds(1));
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.Equal("GAME-IS-LONGER", fileSystem.File.ReadAllText(@"C:\cloud\save.dat"));
        Assert.Equal(1, result.CopiedToSecond);
        Assert.Equal(1, result.Conflicts);
    }

    [Fact]
    public void Should_NotFlagAConflict_When_OneWaySkipsANewerDestination()
    {
        // A skip is a refusal to act, not a resolution: nothing was chosen over anything.
        var fileSystem = new MockFileSystem();
        WriteFile(fileSystem, @"C:\game\save.dat", "old", Older);
        WriteFile(fileSystem, @"C:\cloud\save.dat", "newer", Newer);
        var synchronizer = CreateSynchronizer(fileSystem);

        SyncResult result = synchronizer.Synchronize(
            GameRoot, CloudRoot, new SyncOptions { Mode = SyncMode.FirstToSecond });

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Conflicts);
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
