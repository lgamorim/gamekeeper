namespace GameKeeper.Core;

/// <summary>The direction a synchronization run is allowed to copy in.</summary>
public enum SyncMode
{
    /// <summary>Copy in both directions; the newer copy of each file wins.</summary>
    Bidirectional,

    /// <summary>Copy from the first folder to the second only; the first folder is never written.</summary>
    FirstToSecond,

    /// <summary>Copy from the second folder to the first only; the second folder is never written.</summary>
    SecondToFirst,
}
