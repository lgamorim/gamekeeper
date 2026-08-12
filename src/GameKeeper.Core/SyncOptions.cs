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
    /// How far apart two last-write timestamps may be (inclusive) while still counting as the
    /// same moment. Absorbs file-system and cloud-client rounding; two seconds covers FAT's
    /// coarsest granularity.
    /// </summary>
    public TimeSpan TimestampTolerance { get; init; } = TimeSpan.FromSeconds(2);
}
