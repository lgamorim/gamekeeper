namespace GameKeeper.Core;

/// <summary>Synchronizes the files of two folders.</summary>
public interface IFolderSynchronizer
{
    /// <summary>Synchronizes two folders and reports what was done with each file.</summary>
    /// <param name="firstFolder">The first folder (the game folder in the CLI).</param>
    /// <param name="secondFolder">The second folder (the cloud folder in the CLI).</param>
    /// <param name="options">The run's settings; <see langword="null"/> uses <see cref="SyncOptions.Default"/>.</param>
    /// <returns>The per-file outcomes.</returns>
    SyncResult Synchronize(string firstFolder, string secondFolder, SyncOptions? options = null);
}
