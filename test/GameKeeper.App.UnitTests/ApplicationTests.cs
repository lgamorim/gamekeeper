using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GameKeeper.App.UnitTests;

public sealed class ApplicationTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private Application CreateApplication() => new(_fileSystem, _output, _error);

    [Fact]
    public void Should_Throw_When_FileSystemIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(null!, _output, _error));
    }

    [Fact]
    public void Should_Throw_When_OutputIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(_fileSystem, null!, _error));
    }

    [Fact]
    public void Should_Throw_When_ErrorIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(_fileSystem, _output, null!));
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
    }

    [Fact]
    public void Should_PrintTheVersionAndSucceed_When_VersionRequested()
    {
        Application application = CreateApplication();

        int exitCode = application.Run(["--version"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        Assert.StartsWith("GameKeeper ", _output.ToString());
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact]
    public void Should_WriteReasonAndUsageToError_When_ParseFails()
    {
        Application application = CreateApplication();

        int exitCode = application.Run(["--bogus", @"C:\game", @"C:\cloud"]);

        Assert.Equal(Application.UsageExitCode, exitCode);
        Assert.Contains("Unknown option: --bogus", _error.ToString());
        Assert.Contains("Usage:", _error.ToString());
        Assert.Equal(string.Empty, _output.ToString());
    }

    [Theory]
    [InlineData]
    [InlineData(@"C:\game")]
    [InlineData(@"C:\game", @"C:\cloud", @"C:\extra")]
    public void Should_ReturnUsageCode_When_ArgumentCountIsWrong(params string[] args)
    {
        Application application = CreateApplication();

        int exitCode = application.Run(args);

        Assert.Equal(Application.UsageExitCode, exitCode);
        Assert.Contains("Usage:", _error.ToString());
    }

    [Fact]
    public void Should_ReturnErrorCode_When_GameFolderIsMissing()
    {
        Application application = CreateApplication();

        int exitCode = application.Run([@"C:\game", @"C:\cloud"]);

        Assert.Equal(Application.ErrorExitCode, exitCode);
        Assert.Contains(@"Game folder not found: C:\game", _error.ToString());
    }

    [Fact]
    public void Should_Succeed_When_FoldersAreValid()
    {
        _fileSystem.AddDirectory(@"C:\game");
        Application application = CreateApplication();

        int exitCode = application.Run([@"C:\game", @"C:\cloud"]);

        Assert.Equal(Application.SuccessExitCode, exitCode);
        Assert.Contains(@"C:\game", _output.ToString());
        Assert.Contains(@"C:\cloud", _output.ToString());
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact]
    public void Should_LeaveTheDiskUntouched_When_RunSucceeds()
    {
        _fileSystem.AddDirectory(@"C:\game");
        Application application = CreateApplication();

        application.Run([@"C:\game", @"C:\cloud"]);

        Assert.False(_fileSystem.Directory.Exists(@"C:\cloud"));
    }
}
