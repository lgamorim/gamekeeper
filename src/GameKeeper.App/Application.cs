using System.IO.Abstractions;

namespace GameKeeper.App;

/// <summary>
/// The console host: parses command-line arguments, validates the folders, and reports the
/// outcome. Kept free of static <c>Console</c> calls so it can be unit tested.
/// </summary>
public sealed class Application
{
    /// <summary>Exit code returned on a successful run.</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Exit code returned when synchronization could not be attempted.</summary>
    public const int ErrorExitCode = 1;

    /// <summary>Exit code returned for invalid usage (bad arguments or <c>--help</c>).</summary>
    public const int UsageExitCode = 2;

    private readonly IFileSystem _fileSystem;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="fileSystem">The file system used to validate the supplied folders.</param>
    /// <param name="output">Where normal output is written.</param>
    /// <param name="error">Where error and usage messages are written.</param>
    public Application(IFileSystem fileSystem, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        _fileSystem = fileSystem;
        _output = output;
        _error = error;
    }

    /// <summary>Runs the application for the given command-line arguments.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        CommandLineParseResult parsed = CommandLineParser.Parse(args);

        if (parsed.HelpRequested)
        {
            WriteUsage(_output);
            return UsageExitCode;
        }

        if (parsed.VersionRequested)
        {
            _output.WriteLine($"GameKeeper {AppVersion.Current}");
            return SuccessExitCode;
        }

        if (parsed.HasError)
        {
            _error.WriteLine(parsed.ErrorMessage);
            WriteUsage(_error);
            return UsageExitCode;
        }

        // A successful parse always resolves both folders (guaranteed by
        // CommandLineParseResult.Success), so neither is null at this point.
        return RunSync(parsed.GameFolder!, parsed.CloudFolder!);
    }

    private int RunSync(string gameFolder, string cloudFolder)
    {
        if (!_fileSystem.Directory.Exists(gameFolder))
        {
            _error.WriteLine($"Game folder not found: {gameFolder}");
            return ErrorExitCode;
        }

        // The CLI surface ships ahead of the engine, so a valid run resolves the folders and
        // stops; nothing on disk is touched until the sync engine lands.
        _output.WriteLine($"Would synchronize '{gameFolder}' <-> '{cloudFolder}'.");
        _output.WriteLine("The sync engine is not implemented yet, so no files were changed.");
        return SuccessExitCode;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("GameKeeper - two-way sync for game save folders.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  GameKeeper <gameFolder> <cloudFolder>");
        writer.WriteLine("  GameKeeper --game <gameFolder> --cloud <cloudFolder>");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -g, --game <path>    The local game folder.");
        writer.WriteLine("  -c, --cloud <path>   The shared cloud folder.");
        writer.WriteLine("      --version        Show the version, then exit.");
        writer.WriteLine("  -h, --help           Show this help.");
        writer.WriteLine();
        writer.WriteLine("The folders may be given positionally or by option, in any order.");
    }
}
