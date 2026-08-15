namespace GameKeeper.Core;

/// <summary>
/// Decides which relative paths take part in a sync, from an optional allow-list of include
/// patterns and a block-list of exclude patterns. See <see cref="GlobMatcher"/> for the syntax.
/// </summary>
/// <remarks>
/// With no includes, everything is in scope except what the excludes remove. With includes,
/// only matching files are in scope, and excludes still subtract from that, so the two compose
/// as "of these, not those".
/// </remarks>
public sealed class PathFilter
{
    private readonly GlobMatcher _include;
    private readonly GlobMatcher _exclude;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="includePatterns">Patterns a file must match to be synced; empty means all.</param>
    /// <param name="excludePatterns">Patterns for paths to leave out.</param>
    public PathFilter(IEnumerable<string> includePatterns, IEnumerable<string> excludePatterns)
    {
        ArgumentNullException.ThrowIfNull(includePatterns);
        ArgumentNullException.ThrowIfNull(excludePatterns);
        _include = new GlobMatcher(includePatterns);
        _exclude = new GlobMatcher(excludePatterns);
    }

    /// <summary>Whether a file at the given relative path takes part in the sync.</summary>
    /// <param name="relativePath">The file's path relative to a folder root.</param>
    /// <returns>Whether the file is in scope.</returns>
    public bool AllowsFile(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        // An exclude always wins, so a narrow include list can still have holes cut in it.
        if (_exclude.IsMatch(relativePath))
        {
            return false;
        }

        return !_include.HasPatterns || _include.IsMatch(relativePath);
    }

    /// <summary>Whether a directory at the given relative path takes part in the sync.</summary>
    /// <param name="relativePath">The directory's path relative to a folder root.</param>
    /// <returns>Whether the directory is in scope.</returns>
    /// <remarks>
    /// Only excludes apply. Include patterns name files to sync (<c>*.sav</c>), and a directory
    /// is a container rather than a file, so testing one against them would match nothing and
    /// silently stop empty directories being replicated.
    /// </remarks>
    public bool AllowsDirectory(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return !_exclude.IsMatch(relativePath);
    }
}
