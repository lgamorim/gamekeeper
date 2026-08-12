using System.Collections.ObjectModel;

namespace GameKeeper.Core;

/// <summary>The outcome of a synchronization run: one entry per file the run considered.</summary>
public sealed class SyncResult
{
    /// <summary>Initializes a result from the per-file outcomes.</summary>
    /// <param name="files">The per-file outcomes.</param>
    public SyncResult(IEnumerable<SyncedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Files = new ReadOnlyCollection<SyncedFile>([.. files]);
    }

    /// <summary>The per-file outcomes, in case-insensitive path order.</summary>
    public IReadOnlyList<SyncedFile> Files { get; }

    /// <summary>How many files were copied into the second folder.</summary>
    public int CopiedToSecond => Files.Count(f => f.Action == SyncAction.CopiedToSecond);

    /// <summary>How many files were copied into the first folder.</summary>
    public int CopiedToFirst => Files.Count(f => f.Action == SyncAction.CopiedToFirst);

    /// <summary>How many files were already in sync.</summary>
    public int UpToDate => Files.Count(f => f.Action == SyncAction.None);

    /// <summary>How many out-of-sync files the one-way mode refused to update.</summary>
    public int Skipped => Files.Count(f => f.Action == SyncAction.Skipped);
}
