using System.Reflection;

namespace GameKeeper.App;

/// <summary>
/// Describes which build of GameKeeper is running.
/// </summary>
/// <remarks>
/// The deliverable is a single executable that may be found years later with no installer, no
/// folder structure, and no memory of where it came from, so the commit is reported alongside
/// the version when the build stamps one: it is what makes a binary traceable to its source.
/// </remarks>
public static class AppVersion
{
    /// <summary>Reported when the assembly carries no version information.</summary>
    private const string Unknown = "unknown version";

    /// <summary>Characters of the commit hash to show; enough to find it, short enough to read.</summary>
    private const int ShortCommitLength = 7;

    /// <summary>Describes the running build, for example <c>1.0.0 (b93a7f0)</c>.</summary>
    public static string Current =>
        Format(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion);

    /// <summary>Renders an informational version for display.</summary>
    /// <param name="informationalVersion">
    /// The raw value, which the build stamps as <c>version+commit</c> (the commit part may be
    /// absent).
    /// </param>
    /// <returns>The version, with a shortened commit when one is present.</returns>
    public static string Format(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return Unknown;
        }

        string trimmed = informationalVersion.Trim();
        int separator = trimmed.IndexOf('+');
        if (separator < 0)
        {
            return trimmed;
        }

        string version = trimmed[..separator];
        string commit = trimmed[(separator + 1)..];
        if (commit.Length == 0)
        {
            return version;
        }

        string shortCommit = commit.Length <= ShortCommitLength ? commit : commit[..ShortCommitLength];
        return $"{version} ({shortCommit})";
    }
}
