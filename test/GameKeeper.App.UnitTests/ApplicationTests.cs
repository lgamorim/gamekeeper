using System.IO.Abstractions.TestingHelpers;
using GameKeeper.Core;
using NSubstitute;
using Xunit;

namespace GameKeeper.App.UnitTests;

public sealed class ApplicationTests
{
    private const string GameRoot = @"C:\game";
    private const string CloudRoot = @"C:\cloud";

    private readonly IFolderSynchronizer _synchronizer = Substitute.For<IFolderSynchronizer>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public ApplicationTests()
    {
        _synchronizer
            .Synchronize(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SyncOptions?>())
            .Returns(new SyncResult([]));
    }

    private Application CreateApplication() => new(_synchronizer, _fileSystem, _output, _error);

    [Fact]
    public void Should_Throw_When_SynchronizerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(null!, _fileSystem, _output, _error));
    }

    [Fact]
    public void Should_Throw_When_FileSystemIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(_synchronizer, null!, _output, _error));
    }

    [Fact]
    public void Should_Throw_When_OutputIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(_synchronizer, _fileSystem, null!, _error));
    }

    [Fact]
    public void Should_Throw_When_ErrorIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(_synchronizer, _fileSystem, _output, null!));
    }

    [Fact]
    public void Should_Throw_When_ArgsIsNull()
    {
        Application application = CreateApplication();

        Assert.Throws<ArgumentNullException>(() => application.Run(null!));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Should_PrintUsageToOutputAndReturnUsageCode_When_HelpRequested(string helpFlag)
    {
        Application application = CreateApplication();

        int exitCode = application.Run([helpFlag]);

        Assert.Equal(Application.UsageExitCode, exitCode);
        Assert.Contains("Usage:", _output.ToString());
        Assert.Equal(string.Empty, _error.ToString());
        _synchronizer.DidNotReceiveWithAnyArgs().Synchronize(default!, default!, default);
    }

    [Fact]
    public void Should_PrintTheVersionAndSucceed_When_VersionRequested()
    {
        Application application = CreateApplication();

        int exitCode = application.Run(["--version"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        Assert.StartsWith("GameKeeper ", _output.ToString());
        Assert.Equal(string.Empty, _error.ToString());
        _synchronizer.DidNotReceiveWithAnyArgs().Synchronize(default!, default!, default);
    }

    [Fact]
    public void Should_WriteReasonAndUsageToError_When_ParseFails()
    {
        Application application = CreateApplication();

        int exitCode = application.Run(["--bogus", GameRoot, CloudRoot]);

        Assert.Equal(Application.UsageExitCode, exitCode);
        Assert.Contains("Unknown option: --bogus", _error.ToString());
        Assert.Contains("Usage:", _error.ToString());
        Assert.Equal(string.Empty, _output.ToString());
        _synchronizer.DidNotReceiveWithAnyArgs().Synchronize(default!, default!, default);
    }

    [Theory]
    [InlineData]
    [InlineData(GameRoot)]
    [InlineData(GameRoot, CloudRoot, @"C:\extra")]
    public void Should_ReturnUsageCode_When_ArgumentCountIsWrong(params string[] args)
    {
        Application application = CreateApplication();

        int exitCode = application.Run(args);

        Assert.Equal(Application.UsageExitCode, exitCode);
        Assert.Contains("Usage:", _error.ToString());
        _synchronizer.DidNotReceiveWithAnyArgs().Synchronize(default!, default!, default);
    }

    [Fact]
    public void Should_ReturnErrorCodeWithoutSyncing_When_GameFolderIsMissing()
    {
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        Assert.Contains(@"Game folder not found: C:\game", _error.ToString());
        _synchronizer.DidNotReceiveWithAnyArgs().Synchronize(default!, default!, default);
    }

    [Fact]
    public void Should_InvokeTheSynchronizerOnceAndSucceed_When_ArgumentsAreValid()
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Any<SyncOptions?>());
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact]
    public void Should_ResolveTheFolders_When_NamedOptionsAreUsed()
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        application.Run(["--cloud", CloudRoot, "--game", GameRoot]);

        _synchronizer.Received(1).Synchronize(GameRoot, CloudRoot, Arg.Any<SyncOptions?>());
    }

    [Fact]
    public void Should_ForwardTheMode_When_ModeOptionIsGiven()
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot, "--mode", "up"]);

        _synchronizer.Received(1).Synchronize(
            GameRoot, CloudRoot, Arg.Is<SyncOptions>(o => o.Mode == SyncMode.FirstToSecond));
    }

    [Fact]
    public void Should_CreateTheCloudFolder_When_ItDoesNotExist()
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot]);

        Assert.True(_fileSystem.Directory.Exists(CloudRoot));
    }

    [Fact]
    public void Should_ReturnErrorAndExplain_When_SynchronizerThrowsIOException()
    {
        _fileSystem.AddDirectory(GameRoot);
        _synchronizer
            .Synchronize(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SyncOptions?>())
            .Returns(_ => throw new IOException("disk full"));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        Assert.Contains("Sync failed", _error.ToString());
        Assert.Contains("disk full", _error.ToString());
        Assert.Contains("safe to run again", _error.ToString());
    }

    [Fact]
    public void Should_ReturnErrorAndExplain_When_SynchronizerThrowsUnauthorizedAccess()
    {
        _fileSystem.AddDirectory(GameRoot);
        _synchronizer
            .Synchronize(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SyncOptions?>())
            .Returns(_ => throw new UnauthorizedAccessException("save.dat is locked"));
        Application application = CreateApplication();

        int exitCode = application.Run([GameRoot, CloudRoot]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        Assert.Contains("save.dat is locked", _error.ToString());
    }

    [Fact]
    public void Should_IncludeTheCounts_When_SummaryIsWritten()
    {
        _fileSystem.AddDirectory(GameRoot);
        _synchronizer
            .Synchronize(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SyncOptions?>())
            .Returns(new SyncResult(
            [
                new SyncedFile("to-cloud.dat", SyncAction.CopiedToSecond),
                new SyncedFile("to-game.dat", SyncAction.CopiedToFirst),
                new SyncedFile("same.dat", SyncAction.None),
            ]));
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot]);

        string output = _output.ToString();
        Assert.Contains("Copied to cloud: 1", output);
        Assert.Contains("Copied to game:  1", output);
        Assert.Contains("Already in sync: 1", output);
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    public void Should_IncludeTheSkippedCount_When_ModeIsOneWay(string mode)
    {
        _fileSystem.AddDirectory(GameRoot);
        _synchronizer
            .Synchronize(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SyncOptions?>())
            .Returns(new SyncResult([new SyncedFile("held-back.dat", SyncAction.Skipped)]));
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot, "--mode", mode]);

        Assert.Contains("Skipped (one-way): 1", _output.ToString());
    }

    [Fact]
    public void Should_OmitTheSkippedLine_When_ModeIsTwoWay()
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot]);

        Assert.DoesNotContain("Skipped", _output.ToString());
    }

    [Theory]
    [InlineData("both", "<->")]
    [InlineData("up", "->")]
    [InlineData("down", "<-")]
    public void Should_ReflectTheMode_When_SummaryArrowIsWritten(string mode, string expectedArrow)
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot, "--mode", mode]);

        Assert.Contains($"Synchronized '{GameRoot}' {expectedArrow} '{CloudRoot}'.", _output.ToString());
    }

    [Fact]
    public void Should_UseTheTwoWayArrow_When_ModeIsAbsent()
    {
        _fileSystem.AddDirectory(GameRoot);
        Application application = CreateApplication();

        application.Run([GameRoot, CloudRoot]);

        Assert.Contains(@"Synchronized 'C:\game' <-> 'C:\cloud'.", _output.ToString());
    }
}
