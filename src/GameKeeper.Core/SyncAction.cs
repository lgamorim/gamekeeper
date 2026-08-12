namespace GameKeeper.Core;

/// <summary>What a synchronization run did with one file.</summary>
public enum SyncAction
{
    /// <summary>The file was already in sync; nothing was copied.</summary>
    None,

    /// <summary>The file was copied into the first folder.</summary>
    CopiedToFirst,

    /// <summary>The file was copied into the second folder.</summary>
    CopiedToSecond,

    /// <summary>
    /// The pair is out of sync, but the one-way mode forbids writing the side that needs the
    /// update, so nothing was copied.
    /// </summary>
    Skipped,
}
