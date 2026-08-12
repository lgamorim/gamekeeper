namespace GameKeeper.Core;

/// <summary>
/// The recorded state of one synced file: the identity used to detect changes between runs.
/// </summary>
/// <param name="RelativePath">The file's path relative to its sync root.</param>
/// <param name="LastWriteTimeUtc">The file's last write time, in UTC.</param>
/// <param name="Length">The file's size in bytes.</param>
public sealed record FileState(string RelativePath, DateTime LastWriteTimeUtc, long Length);
