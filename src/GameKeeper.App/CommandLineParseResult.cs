using GameKeeper.Core;

namespace GameKeeper.App;

/// <summary>
/// The outcome of parsing the command line: either the resolved folders, a request for help or
/// the version, or an error describing why parsing failed. Built via the factory methods, which
/// name every property they set so values cannot be transposed silently; properties not set keep
/// their defaults.
/// </summary>
public sealed record CommandLineParseResult
{
    /// <summary>The resolved game folder, or <see langword="null"/>.</summary>
    public string? GameFolder { get; init; }

    /// <summary>The resolved cloud folder, or <see langword="null"/>.</summary>
    public string? CloudFolder { get; init; }

    /// <summary>The requested sync direction; two-way unless <c>--mode</c> says otherwise.</summary>
    public SyncMode Mode { get; init; } = SyncMode.Bidirectional;

    /// <summary>Whether <c>--delete</c> was supplied.</summary>
    public bool PropagateDeletions { get; init; }

    /// <summary>Whether backups are enabled (false when <c>--no-backup</c> was supplied).</summary>
    public bool CreateBackups { get; init; } = true;

    /// <summary>
    /// Backups to keep per file, from <c>--keep-backups</c>, or <see langword="null"/> when the
    /// option was not given so the engine default applies.
    /// </summary>
    public int? KeepBackups { get; init; }

    /// <summary>Whether <c>--force</c> was supplied, overriding the mass-deletion guard.</summary>
    public bool Force { get; init; }

    /// <summary>Whether a help flag was supplied.</summary>
    public bool HelpRequested { get; init; }

    /// <summary>Whether <c>--version</c> was supplied.</summary>
    public bool VersionRequested { get; init; }

    /// <summary>The reason parsing failed, or <see langword="null"/> on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Whether parsing failed.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>Creates a result indicating that help was requested.</summary>
    public static CommandLineParseResult Help() =>
        new() { HelpRequested = true };

    /// <summary>Creates a result indicating that the version was requested.</summary>
    public static CommandLineParseResult Version() =>
        new() { VersionRequested = true };

    /// <summary>Creates a failed result carrying the given error message.</summary>
    public static CommandLineParseResult Failure(string message) =>
        new() { ErrorMessage = message };

    /// <summary>Creates a successful result carrying the resolved folders and sync settings.</summary>
    public static CommandLineParseResult Success(
        string gameFolder,
        string cloudFolder,
        SyncMode mode,
        bool propagateDeletions) =>
        new()
        {
            GameFolder = gameFolder,
            CloudFolder = cloudFolder,
            Mode = mode,
            PropagateDeletions = propagateDeletions,
        };
}
