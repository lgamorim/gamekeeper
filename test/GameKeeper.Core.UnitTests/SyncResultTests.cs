using Xunit;

namespace GameKeeper.Core.UnitTests;

public sealed class SyncResultTests
{
    [Fact]
    public void Should_CountEachAction_When_FilesAreMixed()
    {
        var result = new SyncResult(
        [
            new SyncedFile("a.dat", SyncAction.CopiedToSecond),
            new SyncedFile("b.dat", SyncAction.CopiedToSecond),
            new SyncedFile("c.dat", SyncAction.CopiedToFirst),
            new SyncedFile("d.dat", SyncAction.None),
            new SyncedFile("e.dat", SyncAction.Skipped),
            new SyncedFile("f.dat", SyncAction.DeletedFromFirst),
            new SyncedFile("g.dat", SyncAction.DeletedFromSecond),
        ]);

        Assert.Equal(2, result.CopiedToSecond);
        Assert.Equal(1, result.CopiedToFirst);
        Assert.Equal(1, result.UpToDate);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.DeletedFromFirst);
        Assert.Equal(1, result.DeletedFromSecond);
        Assert.Equal(7, result.Files.Count);
    }

    [Fact]
    public void Should_CountConflictsIndependentlyOfTheAction_When_FilesAreFlagged()
    {
        // A conflicted copy counts as both a copy and a conflict; the flag is orthogonal.
        var result = new SyncResult(
        [
            new SyncedFile("a.dat", SyncAction.CopiedToSecond, Conflict: true),
            new SyncedFile("b.dat", SyncAction.CopiedToFirst, Conflict: true),
            new SyncedFile("c.dat", SyncAction.CopiedToSecond),
        ]);

        Assert.Equal(2, result.Conflicts);
        Assert.Equal(2, result.CopiedToSecond);
        Assert.Equal(1, result.CopiedToFirst);
    }

    [Fact]
    public void Should_ReportNoConflict_When_TheFlagIsDefaulted()
    {
        var file = new SyncedFile("a.dat", SyncAction.CopiedToSecond);

        Assert.False(file.Conflict);
    }

    [Fact]
    public void Should_ReportZeroCounts_When_ThereAreNoFiles()
    {
        var result = new SyncResult([]);

        Assert.Empty(result.Files);
        Assert.Equal(0, result.CopiedToSecond);
        Assert.Equal(0, result.CopiedToFirst);
        Assert.Equal(0, result.UpToDate);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Should_Throw_When_FilesAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SyncResult(null!));
    }
}
