namespace GameKeeper.Core;

/// <summary>
/// Settings for a synchronization run. A record so callers can derive variants with
/// <c>with</c> expressions without mutating shared state.
/// </summary>
public sealed record SyncOptions
{
    /// <summary>The default options: two-way sync with a two-second timestamp tolerance.</summary>
    public static SyncOptions Default { get; } = new();

    /// <summary>The direction files may be copied in. Defaults to two-way.</summary>
    public SyncMode Mode { get; init; } = SyncMode.Bidirectional;

    /// <summary>
    /// When <see langword="true"/>, a file removed from one side (relative to the last-synced
    /// baseline) is deleted from the other side too. Defaults to <see langword="false"/> so the
    /// default behavior stays additive and no save is ever lost by surprise.
    /// </summary>
    public bool PropagateDeletions { get; init; }

    /// <summary>
    /// When <see langword="true"/>, any file about to be overwritten in a conflict or deleted
    /// is first copied into a <c>.gamekeeper-backups</c> folder at its root. Defaults to
    /// <see langword="true"/> so overwrites and deletions are reversible.
    /// </summary>
    public bool CreateBackups { get; init; } = true;

    /// <summary>
    /// How many backups to keep per file in <c>.gamekeeper-backups</c>. After a new backup is
    /// written, older ones beyond this count are removed, so an unattended tool cannot fill
    /// the disk (or the cloud quota) over time. Defaults to ten; zero keeps every backup
    /// forever. Only files matching GameKeeper's own backup naming are ever removed.
    /// </summary>
    public int KeepBackups { get; init; } = 10;

    /// <summary>
    /// When <see langword="true"/>, the run is a preview: the reported <see cref="SyncResult"/>
    /// reflects what would happen, but no file is copied, deleted, or backed up and the
    /// baseline is left unchanged. Defaults to <see langword="false"/>.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// How far apart two last-write timestamps may be (inclusive) while still counting as the
    /// same moment. Absorbs file-system and cloud-client rounding; two seconds covers FAT's
    /// coarsest granularity.
    /// </summary>
    public TimeSpan TimestampTolerance { get; init; } = TimeSpan.FromSeconds(2);
}
