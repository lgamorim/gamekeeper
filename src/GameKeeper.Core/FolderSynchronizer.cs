using System.IO.Abstractions;

namespace GameKeeper.Core;

/// <summary>
/// Synchronizes two folders by comparing each file's last write time and size against the
/// baseline recorded on the previous run. The newer copy wins. By default nothing is deleted -
/// a file missing on one side is restored from the other - unless deletions are explicitly
/// propagated, and even then an edit always outlives a delete.
/// </summary>
public sealed class FolderSynchronizer : IFolderSynchronizer
{
    // Reserved for the backup feature: the folder must be invisible to the sync itself even
    // before backups exist, so no baseline ever records a path under it.
    private const string BackupsDirectoryName = ".gamekeeper-backups";

    // Kept identical to the state store's staging suffix on purpose: everything the app writes
    // and later swaps into place shares one recognizable, ignorable extension.
    private const string StagingFileSuffix = ".gamekeeper-tmp";

    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IFileSystem _fileSystem;
    private readonly ISyncStateStore _stateStore;

    /// <summary>Initializes a synchronizer.</summary>
    /// <param name="fileSystem">The file system the folders live on.</param>
    /// <param name="stateStore">Where the per-pair baseline is kept between runs.</param>
    public FolderSynchronizer(IFileSystem fileSystem, ISyncStateStore stateStore)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(stateStore);
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    /// <inheritdoc/>
    public SyncResult Synchronize(string firstFolder, string secondFolder, SyncOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondFolder);
        options ??= SyncOptions.Default;

        // Everything downstream, including the state key, works on the normalized roots.
        string firstRoot = _fileSystem.Path.GetFullPath(firstFolder);
        string secondRoot = _fileSystem.Path.GetFullPath(secondFolder);

        _fileSystem.Directory.CreateDirectory(firstRoot);
        _fileSystem.Directory.CreateDirectory(secondRoot);

        if (PathComparer.Equals(firstRoot, secondRoot))
        {
            // Syncing a folder with itself is a no-op; recording state for it would only
            // pollute the baseline with a nonsensical pair.
            return BuildSelfSyncResult(firstRoot);
        }

        SyncManifest baseline = _stateStore.Load(firstRoot, secondRoot);
        Dictionary<string, string> firstFiles = EnumerateRelativeFiles(firstRoot);
        Dictionary<string, string> secondFiles = EnumerateRelativeFiles(secondRoot);

        var outcomes = new List<SyncedFile>();
        var newBaseline = new List<FileState>();

        foreach (string relativePath in AllPaths(firstFiles, secondFiles, baseline))
        {
            LiveFile? first = LiveFileAt(firstFiles, relativePath);
            LiveFile? second = LiveFileAt(secondFiles, relativePath);
            baseline.TryGet(relativePath, out FileState? baseEntry);

            Reconciliation outcome = options.Mode == SyncMode.Bidirectional
                ? ReconcileTwoWay(relativePath, first, second, baseEntry, firstRoot, secondRoot, options)
                : ReconcileOneWay(relativePath, first, second, baseEntry, firstRoot, secondRoot, options);

            if (outcome.Entry is not null)
            {
                newBaseline.Add(outcome.Entry);
            }

            // A path gone from both sides produces no row at all; everything else is reported.
            if (outcome.Action != SyncAction.None || outcome.Entry is not null)
            {
                outcomes.Add(new SyncedFile(relativePath, outcome.Action, outcome.Conflict));
            }
        }

        // Saved once, at the end: a run that fails mid-copy records nothing, so rerunning it
        // is always safe.
        _stateStore.Save(firstRoot, secondRoot, new SyncManifest(newBaseline));
        return new SyncResult(outcomes);
    }

    private Reconciliation ReconcileTwoWay(
        string relativePath,
        LiveFile? first,
        LiveFile? second,
        FileState? baseEntry,
        string firstRoot,
        string secondRoot,
        SyncOptions options)
    {
        Change firstChange = Classify(first, baseEntry, options.TimestampTolerance);
        Change secondChange = Classify(second, baseEntry, options.TimestampTolerance);

        return (firstChange, secondChange) switch
        {
            (Change.Created, Change.Absent) =>
                CopyOutcome(first!.Value, relativePath, secondRoot, SyncAction.CopiedToSecond),
            (Change.Absent, Change.Created) =>
                CopyOutcome(second!.Value, relativePath, firstRoot, SyncAction.CopiedToFirst),
            (Change.Created, Change.Created) or (Change.Modified, Change.Modified) =>
                ReconcileBothPresent(relativePath, first!.Value, second!.Value, firstRoot, secondRoot, options),
            (Change.Modified, Change.Unchanged) =>
                CopyOutcome(first!.Value, relativePath, secondRoot, SyncAction.CopiedToSecond),
            (Change.Unchanged, Change.Modified) =>
                CopyOutcome(second!.Value, relativePath, firstRoot, SyncAction.CopiedToFirst),

            // Delete versus unchanged: propagate the deletion when asked to; otherwise the
            // sync stays additive and the survivor resurrects the file.
            (Change.Deleted, Change.Unchanged) => options.PropagateDeletions
                ? DeleteOutcome(second!.Value.FullPath, SyncAction.DeletedFromSecond)
                : CopyOutcome(second!.Value, relativePath, firstRoot, SyncAction.CopiedToFirst),
            (Change.Unchanged, Change.Deleted) => options.PropagateDeletions
                ? DeleteOutcome(first!.Value.FullPath, SyncAction.DeletedFromFirst)
                : CopyOutcome(first!.Value, relativePath, secondRoot, SyncAction.CopiedToSecond),

            // Edit versus delete: an edit is never lost to a delete, even with deletions on;
            // choosing the edit over the delete is a conflict resolution worth flagging.
            (Change.Modified, Change.Deleted) =>
                CopyOutcome(first!.Value, relativePath, secondRoot, SyncAction.CopiedToSecond, conflict: true),
            (Change.Deleted, Change.Modified) =>
                CopyOutcome(second!.Value, relativePath, firstRoot, SyncAction.CopiedToFirst, conflict: true),

            (Change.Unchanged, Change.Unchanged) => new Reconciliation(SyncAction.None, false, baseEntry),

            // Both absent or both deleted: the path simply ages out of the baseline.
            _ => new Reconciliation(SyncAction.None, false, null),
        };
    }

    private Reconciliation ReconcileBothPresent(
        string relativePath,
        LiveFile first,
        LiveFile second,
        string firstRoot,
        string secondRoot,
        SyncOptions options)
    {
        if (WithinTolerance(first.LastWriteTimeUtc, second.LastWriteTimeUtc, options.TimestampTolerance)
            && first.Length == second.Length)
        {
            // Same moment, same size: treated as the same file. Content is never read - the
            // baseline exists precisely so routine cases stay this cheap.
            return new Reconciliation(SyncAction.None, false, StateOf(relativePath, first));
        }

        // A genuine conflict: both copies exist and differ. The raw timestamps pick the
        // winner; an exact tie goes to the first (game) folder, since size says nothing
        // about recency.
        return first.LastWriteTimeUtc >= second.LastWriteTimeUtc
            ? CopyOutcome(first, relativePath, secondRoot, SyncAction.CopiedToSecond, conflict: true)
            : CopyOutcome(second, relativePath, firstRoot, SyncAction.CopiedToFirst, conflict: true);
    }

    private Reconciliation ReconcileOneWay(
        string relativePath,
        LiveFile? first,
        LiveFile? second,
        FileState? baseEntry,
        string firstRoot,
        string secondRoot,
        SyncOptions options)
    {
        bool up = options.Mode == SyncMode.FirstToSecond;
        LiveFile? source = up ? first : second;
        LiveFile? destination = up ? second : first;
        string destinationRoot = up ? secondRoot : firstRoot;
        SyncAction copyAction = up ? SyncAction.CopiedToSecond : SyncAction.CopiedToFirst;
        SyncAction deleteAction = up ? SyncAction.DeletedFromSecond : SyncAction.DeletedFromFirst;

        if (source is not { } sourceFile)
        {
            // Source absent. If the baseline had it, the source side deleted it and the
            // deletion may propagate; otherwise a destination-only file is outside the
            // source's authority and is left alone but reported, so the run does not
            // pretend the pair is in sync.
            if (baseEntry is not null && destination is { } deletedDestination && options.PropagateDeletions)
            {
                return DeleteOutcome(deletedDestination.FullPath, deleteAction);
            }

            return destination is not null
                ? new Reconciliation(SyncAction.Skipped, false, baseEntry)
                : new Reconciliation(SyncAction.None, false, null);
        }

        if (destination is not { } destinationFile)
        {
            return CopyOutcome(sourceFile, relativePath, destinationRoot, copyAction);
        }

        if (WithinTolerance(sourceFile.LastWriteTimeUtc, destinationFile.LastWriteTimeUtc, options.TimestampTolerance))
        {
            // Same moment: same size means in sync; a size mismatch means the copies
            // diverged. The source is authoritative in a one-way run, but the timestamps
            // cannot justify the overwrite, so it is flagged as a conflict.
            return sourceFile.Length == destinationFile.Length
                ? new Reconciliation(SyncAction.None, false, StateOf(relativePath, sourceFile))
                : CopyOutcome(sourceFile, relativePath, destinationRoot, copyAction, conflict: true);
        }

        if (destinationFile.LastWriteTimeUtc >= sourceFile.LastWriteTimeUtc)
        {
            // The destination is newer but this direction may not touch the source, so the
            // pair stays out of sync - reported as skipped rather than blending into
            // up to date. The baseline still records the source's view of the file.
            return new Reconciliation(SyncAction.Skipped, false, StateOf(relativePath, sourceFile));
        }

        return CopyOutcome(sourceFile, relativePath, destinationRoot, copyAction);
    }

    private static Change Classify(LiveFile? live, FileState? baseEntry, TimeSpan tolerance)
    {
        if (live is not { } liveFile)
        {
            return baseEntry is null ? Change.Absent : Change.Deleted;
        }

        if (baseEntry is null)
        {
            return Change.Created;
        }

        bool differs = !WithinTolerance(liveFile.LastWriteTimeUtc, baseEntry.LastWriteTimeUtc, tolerance)
            || liveFile.Length != baseEntry.Length;
        return differs ? Change.Modified : Change.Unchanged;
    }

    private static bool WithinTolerance(DateTime a, DateTime b, TimeSpan tolerance)
    {
        return (a - b).Duration() <= tolerance;
    }

    private static FileState StateOf(string relativePath, LiveFile file)
    {
        return new FileState(relativePath, file.LastWriteTimeUtc, file.Length);
    }

    private Reconciliation CopyOutcome(
        LiveFile source,
        string relativePath,
        string destinationRoot,
        SyncAction action,
        bool conflict = false)
    {
        CopyFile(source.FullPath, _fileSystem.Path.Combine(destinationRoot, relativePath));

        // The copy preserves the source's timestamp, so its live state describes both sides.
        return new Reconciliation(action, conflict, StateOf(relativePath, source));
    }

    private Reconciliation DeleteOutcome(string fullPath, SyncAction action)
    {
        _fileSystem.File.Delete(fullPath);

        // A propagated deletion is never a conflict, and the path drops out of the baseline
        // because it no longer exists anywhere.
        return new Reconciliation(action, false, null);
    }

    private void CopyFile(string sourcePath, string destinationPath)
    {
        string? destinationDirectory = _fileSystem.Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            _fileSystem.Directory.CreateDirectory(destinationDirectory);
        }

        // The staging file sits beside the destination so the final move is a same-volume
        // rename: an interrupted run leaves either the old file or the new one, never a
        // truncated hybrid. The timestamp is stamped before the swap for the same reason - a
        // fresh mtime on a half-copied file would make it look newer than the intact source.
        string stagingPath = destinationPath + StagingFileSuffix;
        try
        {
            _fileSystem.File.Copy(sourcePath, stagingPath, overwrite: true);
            _fileSystem.File.SetLastWriteTimeUtc(stagingPath, _fileSystem.File.GetLastWriteTimeUtc(sourcePath));
            _fileSystem.File.Move(stagingPath, destinationPath, overwrite: true);
        }
        catch
        {
            DiscardStagingFile(stagingPath);
            throw;
        }
    }

    private void DiscardStagingFile(string stagingPath)
    {
        // Best effort only: cleanup must never mask the failure that brought us here.
        try
        {
            if (_fileSystem.File.Exists(stagingPath))
            {
                _fileSystem.File.Delete(stagingPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private SyncResult BuildSelfSyncResult(string root)
    {
        return new SyncResult(EnumerateRelativeFiles(root).Keys
            .OrderBy(path => path, PathComparer)
            .Select(path => new SyncedFile(path, SyncAction.None)));
    }

    private Dictionary<string, string> EnumerateRelativeFiles(string root)
    {
        var map = new Dictionary<string, string>(PathComparer);
        if (!_fileSystem.Directory.Exists(root))
        {
            return map;
        }

        foreach (string fullPath in _fileSystem.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relativePath = _fileSystem.Path.GetRelativePath(root, fullPath);
            if (!IsExcluded(relativePath))
            {
                map[relativePath] = fullPath;
            }
        }

        return map;
    }

    private IEnumerable<string> AllPaths(
        Dictionary<string, string> firstFiles,
        Dictionary<string, string> secondFiles,
        SyncManifest baseline)
    {
        var paths = new HashSet<string>(firstFiles.Keys, PathComparer);
        paths.UnionWith(secondFiles.Keys);
        foreach (FileState entry in baseline.Files)
        {
            // Only the baseline can carry a path that is excluded today, so it is filtered
            // again here.
            if (!IsExcluded(entry.RelativePath))
            {
                paths.Add(entry.RelativePath);
            }
        }

        return paths.OrderBy(path => path, PathComparer);
    }

    private bool IsExcluded(string relativePath)
    {
        return IsInBackups(relativePath)
            || relativePath.EndsWith(StagingFileSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInBackups(string relativePath)
    {
        int separator = relativePath.IndexOfAny(
            [_fileSystem.Path.DirectorySeparatorChar, _fileSystem.Path.AltDirectorySeparatorChar]);
        string firstSegment = separator < 0 ? relativePath : relativePath[..separator];
        return string.Equals(firstSegment, BackupsDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A file as found on disk during enumeration.</summary>
    private readonly record struct LiveFile(string FullPath, DateTime LastWriteTimeUtc, long Length);

    /// <summary>How one side of a pair changed relative to the recorded baseline.</summary>
    private enum Change
    {
        Absent,
        Created,
        Modified,
        Deleted,
        Unchanged,
    }

    /// <summary>One path's outcome: what was done and what the new baseline records for it.</summary>
    private readonly record struct Reconciliation(SyncAction Action, bool Conflict, FileState? Entry);

    private LiveFile? LiveFileAt(Dictionary<string, string> files, string relativePath)
    {
        if (!files.TryGetValue(relativePath, out string? fullPath))
        {
            return null;
        }

        return new LiveFile(
            fullPath,
            _fileSystem.File.GetLastWriteTimeUtc(fullPath),
            _fileSystem.FileInfo.New(fullPath).Length);
    }
}
