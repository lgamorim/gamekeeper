namespace GameKeeper.Core;

/// <summary>One file's outcome in a synchronization run.</summary>
/// <param name="RelativePath">The file's path relative to the sync roots.</param>
/// <param name="Action">What the run did with the file.</param>
public sealed record SyncedFile(string RelativePath, SyncAction Action);
