namespace GameKeeper.Core;

/// <summary>One file's outcome in a synchronization run.</summary>
/// <param name="RelativePath">The file's path relative to the sync roots.</param>
/// <param name="Action">What the run did with the file.</param>
/// <param name="Conflict">
/// Whether this outcome resolved a conflict — both sides changed since the last sync, or an
/// edit collided with a delete — so one side's version had to be chosen over the other's.
/// </param>
public sealed record SyncedFile(string RelativePath, SyncAction Action, bool Conflict = false);
