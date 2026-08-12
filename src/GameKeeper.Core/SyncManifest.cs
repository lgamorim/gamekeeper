namespace GameKeeper.Core;

/// <summary>
/// The baseline recorded by the previous synchronization of a folder pair: one
/// <see cref="FileState"/> per file, looked up by relative path, case-insensitively to match
/// Windows path semantics.
/// </summary>
public sealed class SyncManifest
{
    private readonly Dictionary<string, FileState> _byPath;

    /// <summary>Initializes a manifest from the given file states; the last duplicate path wins.</summary>
    /// <param name="files">The recorded file states.</param>
    public SyncManifest(IEnumerable<FileState> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        _byPath = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
        foreach (FileState file in files)
        {
            _byPath[file.RelativePath] = file;
        }
    }

    /// <summary>A manifest with no recorded files, used when no baseline exists yet.</summary>
    public static SyncManifest Empty { get; } = new([]);

    /// <summary>The recorded file states.</summary>
    public IReadOnlyCollection<FileState> Files => _byPath.Values;

    /// <summary>Looks up the recorded state for a relative path.</summary>
    /// <param name="relativePath">The path to look up, relative to the sync root.</param>
    /// <param name="state">The recorded state, or <see langword="null"/> when absent.</param>
    /// <returns>Whether the path has a recorded state.</returns>
    public bool TryGet(string relativePath, out FileState? state)
    {
        return _byPath.TryGetValue(relativePath, out state);
    }
}
