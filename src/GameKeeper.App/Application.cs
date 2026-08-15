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
            CreateBackups = parsed.CreateBackups,
            KeepBackups = parsed.KeepBackups ?? SyncOptions.Default.KeepBackups,
            IncludePatterns = parsed.IncludePatterns,
            ExcludePatterns = parsed.ExcludePatterns,
            DryRun = parsed.DryRun,
        };

        // A successful parse always resolves both folders (guaranteed by
        // CommandLineParseResult.Success), so neither is null at this point.
        return RunSync(parsed.GameFolder!, parsed.CloudFolder!, options, parsed.Force);
    }

    private int RunSync(string gameFolder, string cloudFolder, SyncOptions options, bool force)
    {
        if (!_fileSystem.Directory.Exists(gameFolder))
        {
            _error.WriteLine($"Game folder not found: {gameFolder}");
            return ErrorExitCode;
        }

        // The cloud folder may not exist yet on a brand-new machine; creating it is the
        // natural first step of the sync rather than an error. A dry run previews without
        // touching the disk, so it is skipped there.
        if (!options.DryRun)
        {
            _fileSystem.Directory.CreateDirectory(cloudFolder);
        }

        try
        {
            // Propagating deletions is the only way this tool can destroy a save it was not
            // asked to touch, so preview the run first and refuse if it looks like a folder
            // went missing rather than the user tidying up. The preview changes nothing, so
            // a refusal leaves no partial state behind.
            if (options.PropagateDeletions && !options.DryRun && !force)
            {
                SyncResult preview = _synchronizer.Synchronize(
                    gameFolder, cloudFolder, options with { DryRun = true });
                if (IsMassDeletion(preview))
                {
                    WriteMassDeletionRefusal(preview, gameFolder, cloudFolder);
                    return ErrorExitCode;
                }
            }

            SyncResult result = _synchronizer.Synchronize(gameFolder, cloudFolder, options);
            WriteSummary(gameFolder, cloudFolder, options.Mode, result, options.DryRun);

            // A preview is harmless in itself, but it is exactly when the user wants to know
            // that the real run would be turned away.
            if (options.DryRun && options.PropagateDeletions && IsMassDeletion(result))
            {
                WriteMassDeletionWarning(result);
            }

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

    /// <summary>
    /// Deletions must exceed this share of the tracked files before the guard engages, so an
    /// ordinary tidy-up is never blocked.
    /// </summary>
    private const double MassDeleteShare = 0.5;

    /// <summary>
    /// Below this many deletions the guard stays quiet: on a folder holding two or three
    /// saves, removing most of them is a plausible thing to have meant.
    /// </summary>
    private const int MassDeleteFloor = 3;

    private static int DeletionsIn(SyncResult result) =>
        result.DeletedFromFirst + result.DeletedFromSecond;

    // Losing most of what GameKeeper is tracking is the signature of a folder that is not
    // where it used to be, rather than of a deliberate clear-out.
    private static bool IsMassDeletion(SyncResult result)
    {
        int deletions = DeletionsIn(result);
        return deletions >= MassDeleteFloor && deletions > result.Files.Count * MassDeleteShare;
    }

    private void WriteMassDeletionRefusal(SyncResult preview, string gameFolder, string cloudFolder)
    {
        _error.WriteLine(
            $"Refused: this run would delete {DeletionsIn(preview)} of the "
            + $"{preview.Files.Count} files GameKeeper is tracking.");
        _error.WriteLine();
        _error.WriteLine("That usually means a folder is not where GameKeeper expects it - the game was");
        _error.WriteLine("reinstalled, a drive letter changed, or your cloud folder has not finished");
        _error.WriteLine("downloading. Nothing has been changed.");
        _error.WriteLine();
        _error.WriteLine($"  Check with:  GameKeeper \"{gameFolder}\" \"{cloudFolder}\" --delete --dry-run");
        _error.WriteLine($"  Proceed:     GameKeeper \"{gameFolder}\" \"{cloudFolder}\" --delete --force");
    }

    private void WriteMassDeletionWarning(SyncResult preview)
    {
        _output.WriteLine();
        _output.WriteLine(
            $"Warning: a real run would be refused, because {DeletionsIn(preview)} of the "
            + $"{preview.Files.Count} tracked files would be deleted.");
        _output.WriteLine("Re-run with --force if that is genuinely what you want.");
    }

    private void WriteSummary(
        string gameFolder,
        string cloudFolder,
        SyncMode mode,
        SyncResult result,
        bool dryRun)
    {
        if (dryRun)
        {
            _output.WriteLine("Dry run: no files were copied, deleted, or backed up, and the sync record");
            _output.WriteLine("was left unchanged. The counts below show what a real run would do.");
        }

        string verb = dryRun ? "Would synchronize" : "Synchronized";
        _output.WriteLine($"{verb} '{gameFolder}' {DirectionArrow(mode)} '{cloudFolder}'.");
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

        WriteDetails(result, mode, dryRun);
    }

    /// <summary>
    /// Names the files behind the counts. Deletions and conflicts are always named: they are
    /// the outcomes a user may have to act on, and a count alone cannot be checked against
    /// anything. Routine copies are named only on a dry run, where seeing what a real run
    /// would do is the entire point; naming them on every run would bury the two outcomes
    /// that matter under a wall of ordinary ones.
    /// </summary>
    private void WriteDetails(SyncResult result, SyncMode mode, bool dryRun)
    {
        // A conflict is also a copy, so it is carved out of the copy sections and reported
        // once, below, where the winning side can be named too.
        if (dryRun)
        {
            WriteSection("Copied to cloud:", result, f => f.Action == SyncAction.CopiedToSecond && !f.Conflict);
            WriteSection("Copied to game:", result, f => f.Action == SyncAction.CopiedToFirst && !f.Conflict);
        }

        WriteSection("Deleted from cloud:", result, f => f.Action == SyncAction.DeletedFromSecond);
        WriteSection("Deleted from game:", result, f => f.Action == SyncAction.DeletedFromFirst);
        WriteConflicts(result);

        if (dryRun && mode != SyncMode.Bidirectional)
        {
            WriteSection("Skipped (one-way):", result, f => f.Action == SyncAction.Skipped);
        }
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
        writer.WriteLine("      --no-backup           Do not back up overwritten or deleted files.");
        writer.WriteLine("      --keep-backups <n>    Backups to keep per file (default 10; 0 keeps all).");
        writer.WriteLine("      --force               Allow a run that would delete most tracked files.");
        writer.WriteLine("  -i, --include <glob>      Sync only paths matching the glob (repeatable).");
        writer.WriteLine("                            Applies to files; folders follow --exclude.");
        writer.WriteLine("  -x, --exclude <glob>      Skip paths matching the glob (repeatable), e.g.");
        writer.WriteLine("                            --exclude *.log. '*' and '?' are wildcards.");
        writer.WriteLine("  -n, --dry-run             Preview the actions without changing any files.");
        writer.WriteLine("      --version             Show the version, then exit.");
        writer.WriteLine("  -h, --help                Show this help.");
        writer.WriteLine();
        writer.WriteLine("The folders may be given positionally or by option, in any order.");
        writer.WriteLine("The newer copy of each file wins. By default nothing is deleted, an edited");
        writer.WriteLine("file always survives a deletion, and anything overwritten in a conflict or");
        writer.WriteLine("deleted is first copied into a '.gamekeeper-backups' folder.");
    }
}
