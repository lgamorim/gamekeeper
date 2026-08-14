using System.IO.Abstractions.TestingHelpers;
using GameKeeper.Core;
using NSubstitute;
using Xunit;

namespace GameKeeper.App.UnitTests;

/// <summary>
/// The mass-deletion guard: a --delete run is previewed first and refused when it would wipe
/// out most of the tracked files, since that is the signature of a moved or half-downloaded
/// folder rather than a deliberate clear-out.
/// </summary>
public sealed class ApplicationGuardTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";

    private readonly IFolderSynchronizer _synchronizer = Substitute.For<IFolderSynchronizer>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public ApplicationGuardTests()
    {
        _fileSystem.AddDirectory(GameRoot);
    }

    [Fact]
    public void Should_Refuse_When_TheRunWouldWipeEverything()
    {
        SetupPreview(Preview(deletions: 50, untouched: 0));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete"]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        Assert.Contains("50", _error.ToString());
        AssertNothingWasSynchronizedForReal();
    }

    [Fact]
    public void Should_Refuse_When_TheRunWouldDeleteMostFiles()
    {
        // A cloud folder that has only partly finished downloading looks like mass deletion.
        SetupPreview(Preview(deletions: 47, untouched: 3));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete"]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        AssertNothingWasSynchronizedForReal();
    }

    [Fact]
    public void Should_Refuse_When_DeletionsAreJustOverHalf()
    {
        SetupPreview(Preview(deletions: 6, untouched: 4));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete"]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        AssertNothingWasSynchronizedForReal();
    }

    [Fact]
    public void Should_Proceed_When_DeletionsAreExactlyHalf()
    {
        // The rule is "more than half", so an even split proceeds.
        SetupPreview(Preview(deletions: 5, untouched: 5));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => !o.DryRun));
    }

    [Fact]
    public void Should_Proceed_When_ASingleFileIsDeleted()
    {
        SetupPreview(Preview(deletions: 1, untouched: 4));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => !o.DryRun));
    }

    [Fact]
    public void Should_Proceed_When_DeletionsAreBelowTheFloor()
    {
        // Two of three is a majority, but too small to be the "folder vanished" signature.
        SetupPreview(Preview(deletions: 2, untouched: 1));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => !o.DryRun));
    }

    [Fact]
    public void Should_ProceedWithoutAnyPreview_When_ForceIsGiven()
    {
        SetupPreview(Preview(deletions: 50, untouched: 0));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot, "--delete", "--force"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        _synchronizer.DidNotReceive().Synchronize(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Is<SyncOptions>(o => o.DryRun));
        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => !o.DryRun));
    }

    [Fact]
    public void Should_RunNoPreview_When_DeletionsAreNotRequested()
    {
        // Nothing can be deleted, so the extra pass would be pure cost.
        _synchronizer
            .Synchronize(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SyncOptions?>())
            .Returns(new SyncResult([]));
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot]);

        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Any<SyncOptions?>());
    }

    [Fact]
    public void Should_ExplainTheLikelyCauseAndTheWayForward_When_Refusing()
    {
        SetupPreview(Preview(deletions: 50, untouched: 0));
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot, "--delete"]);

        string message = _error.ToString();
        Assert.Contains("Nothing has been changed", message);
        Assert.Contains("--force", message);
        Assert.Equal(string.Empty, _output.ToString());
    }

    private Application CreateApplication() => new(_synchronizer, _fileSystem, _output, _error);

    /// <summary>
    /// A preview result describing <paramref name="deletions"/> deletions out of
    /// <paramref name="deletions"/> + <paramref name="untouched"/> tracked files.
    /// </summary>
    private static SyncResult Preview(int deletions, int untouched) =>
        new(
        [
            .. Enumerable.Range(0, deletions)
                .Select(i => new SyncedFile($"gone{i}.sav", SyncAction.DeletedFromSecond)),
            .. Enumerable.Range(0, untouched)
                .Select(i => new SyncedFile($"kept{i}.sav", SyncAction.None)),
        ]);

    // Both branches are configured explicitly: a test that forgot the real-pass branch would
    // otherwise silently get a default and could mask a wrong-path bug.
    private void SetupPreview(SyncResult preview)
    {
        _synchronizer
            .Synchronize(GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => o.DryRun))
            .Returns(preview);
        _synchronizer
            .Synchronize(GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => !o.DryRun))
            .Returns(new SyncResult([]));
    }

    private void AssertNothingWasSynchronizedForReal()
    {
        _synchronizer.DidNotReceive().Synchronize(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Is<SyncOptions>(o => !o.DryRun));
    }
}
