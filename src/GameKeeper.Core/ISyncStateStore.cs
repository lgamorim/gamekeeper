namespace GameKeeper.Core;

/// <summary>Persists the per-folder-pair baseline between synchronization runs.</summary>
public interface ISyncStateStore
{
    /// <summary>
    /// Loads the baseline recorded for a folder pair, or <see cref="SyncManifest.Empty"/> when
    /// none was saved or it cannot be read.
    /// </summary>
    /// <param name="firstFolder">The first folder of the pair.</param>
    /// <param name="secondFolder">The second folder of the pair.</param>
    /// <returns>The recorded baseline, or an empty manifest.</returns>
    SyncManifest Load(string firstFolder, string secondFolder);

    /// <summary>Saves the baseline for a folder pair, replacing any previous one.</summary>
    /// <param name="firstFolder">The first folder of the pair.</param>
    /// <param name="secondFolder">The second folder of the pair.</param>
    /// <param name="manifest">The baseline to record.</param>
    void Save(string firstFolder, string secondFolder, SyncManifest manifest);
}
