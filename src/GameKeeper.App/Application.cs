using System.IO.Abstractions;
using GameKeeper.Core;

namespace GameKeeper.App;

/// <summary>
/// The console host: parses command-line arguments, validates the folders, runs the sync, and
/// reports the outcome. Kept free of static <c>Console</c> calls so it can be unit tested.
/// </summary>
public sealed class Application
{
    /// <summary>Exit code returned on a successful run.</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Exit code returned when synchronization could not be attempted or completed.</summary>
    public const int ErrorExitCode = 1;

    /// <summary>Exit code returned for invalid usage (bad arguments or <c>--help</c>).</summary>
    public const int UsageExitCode = 2;

    private readonly IFolderSynchronizer _synchronizer;
    private readonly IFileSystem _fileSystem;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="synchronizer">The engine that syncs the two folders.</param>
    /// <param name="fileSystem">The file system used to validate the supplied folders.</param>
    /// <param name="output">Where normal output is written.</param>
    /// <param name="error">Where error and usage messages are written.</param>
    public Application(
        IFolderSynchronizer synchronizer,
        IFileSystem fileSystem,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(synchronizer);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        _synchronizer = synchronizer;
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

        var options = new SyncOptions
        {
            Mode = parsed.Mode,
            PropagateDeletions = parsed.PropagateDeletions,
        };

        // A successful parse always resolves both folders (guaranteed by
        // CommandLineParseResult.Success), so neither is null at this point.
        return RunSync(parsed.GameFolder!, parsed.CloudFolder!, options);
    }

    private int RunSync(string gameFolder, string cloudFolder, SyncOptions options)
    {
        if (!_fileSystem.Directory.Exists(gameFolder))
        {
            _error.WriteLine($"Game folder not found: {gameFolder}");
            return ErrorExitCode;
        }

        // The cloud folder may not exist yet on a brand-new machine; creating it is the
        // natural first step of the sync rather than an error.
        _fileSystem.Directory.CreateDirectory(cloudFolder);

        try
        {
            SyncResult result = _synchronizer.Synchronize(gameFolder, cloudFolder, options);
            WriteSummary(gameFolder, cloudFolder, options.Mode, result);
            return SuccessExitCode;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _error.WriteLine($"Sync failed: {ex.Message}");
            _error.WriteLine("No sync state was recorded, so it is safe to run again once the "
                + "cause is resolved (for example, after closing the game).");
            return ErrorExitCode;
        }
    }

    private void WriteSummary(string gameFolder, string cloudFolder, SyncMode mode, SyncResult result)
    {
        _output.WriteLine($"Synchronized '{gameFolder}' {DirectionArrow(mode)} '{cloudFolder}'.");
        _output.WriteLine($"  Copied to cloud: {result.CopiedToSecond}");
        _output.WriteLine($"  Copied to game:  {result.CopiedToFirst}");
        _output.WriteLine($"  Deleted from cloud: {result.DeletedFromSecond}");
        _output.WriteLine($"  Deleted from game:  {result.DeletedFromFirst}");
        _output.WriteLine($"  Conflicts: {result.Conflicts}");
        _output.WriteLine($"  Already in sync: {result.UpToDate}");

        // Only one-way runs can hold files back, so the line would be noise elsewhere.
        if (mode != SyncMode.Bidirectional)
        {
            _output.WriteLine($"  Skipped (one-way): {result.Skipped}");
        }

        WriteDetails(result);
    }

    /// <summary>
    /// Names the files behind the counts. Deletions and conflicts are always named: they are
    /// the outcomes a user may have to act on, and a count alone cannot be checked against
    /// anything. Routine copies stay counts-only so those two outcomes are never buried.
    /// </summary>
    private void WriteDetails(SyncResult result)
    {
        // A conflict is also a copy; it is reported once, in its own section, where the
        // winning side can be named too.
        WriteSection("Deleted from cloud:", result, f => f.Action == SyncAction.DeletedFromSecond);
        WriteSection("Deleted from game:", result, f => f.Action == SyncAction.DeletedFromFirst);
        WriteConflicts(result);
    }

    private void WriteSection(string heading, SyncResult result, Func<SyncedFile, bool> matches)
    {
        // The engine reports files in path order, so the listing needs no sorting of its own.
        string[] paths = [.. result.Files.Where(matches).Select(f => f.RelativePath)];
        if (paths.Length == 0)
        {
            return;
        }

        _output.WriteLine();
        _output.WriteLine(heading);
        foreach (string path in paths)
        {
            _output.WriteLine($"  {path}");
        }
    }

    private void WriteConflicts(SyncResult result)
    {
        SyncedFile[] conflicts = [.. result.Files.Where(f => f.Conflict)];
        if (conflicts.Length == 0)
        {
            return;
        }

        _output.WriteLine();
        _output.WriteLine("Conflicts:");
        foreach (SyncedFile conflict in conflicts)
        {
            // The copy that lost is on the side the file was copied *to*; naming the survivor
            // says which copy remains.
            string winner = conflict.Action == SyncAction.CopiedToSecond ? "game" : "cloud";
            _output.WriteLine($"  {conflict.RelativePath} (kept the {winner} copy)");
        }
    }

    private static string DirectionArrow(SyncMode mode) => mode switch
    {
        SyncMode.FirstToSecond => "->",
        SyncMode.SecondToFirst => "<-",
        _ => "<->",
    };

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("GameKeeper - two-way sync for game save folders.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  GameKeeper <gameFolder> <cloudFolder> [--mode <both|up|down>]");
        writer.WriteLine("  GameKeeper --game <gameFolder> --cloud <cloudFolder> [--mode <both|up|down>]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -g, --game <path>         The local game folder.");
        writer.WriteLine("  -c, --cloud <path>        The shared cloud folder.");
        writer.WriteLine("  -m, --mode <both|up|down> Sync direction: both (default, two-way),");
        writer.WriteLine("                            up (game -> cloud), down (cloud -> game).");
        writer.WriteLine("      --delete              Propagate deletions (off by default; files are");
        writer.WriteLine("                            only added or updated unless this is set).");
        writer.WriteLine("      --version             Show the version, then exit.");
        writer.WriteLine("  -h, --help                Show this help.");
        writer.WriteLine();
        writer.WriteLine("The folders may be given positionally or by option, in any order.");
        writer.WriteLine("The newer copy of each file wins. By default nothing is deleted: a file");
        writer.WriteLine("missing on one side is copied back from the other, and even with --delete");
        writer.WriteLine("an edited file always survives a deletion.");
    }
}
