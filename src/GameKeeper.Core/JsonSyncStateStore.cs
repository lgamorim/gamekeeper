using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameKeeper.Core;

/// <summary>
/// Persists each folder pair's baseline as a JSON file in a machine-local directory, named by a
/// hash of the pair so distinct pairs never collide.
/// </summary>
public sealed class JsonSyncStateStore : ISyncStateStore
{
    // Kept identical to the synchronizer's staging suffix on purpose: everything the app writes
    // and later swaps into place shares one recognizable, ignorable extension.
    private const string StagingFileSuffix = ".gamekeeper-tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;
    private readonly string _baseDirectory;

    /// <summary>Initializes a store rooted at the given directory.</summary>
    /// <param name="fileSystem">The file system to persist through.</param>
    /// <param name="baseDirectory">The directory the manifests are kept in.</param>
    public JsonSyncStateStore(IFileSystem fileSystem, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _fileSystem = fileSystem;
        _baseDirectory = baseDirectory;
    }

    /// <inheritdoc/>
    public SyncManifest Load(string firstFolder, string secondFolder)
    {
        string path = ManifestPath(firstFolder, secondFolder);
        if (!_fileSystem.File.Exists(path))
        {
            return SyncManifest.Empty;
        }

        try
        {
            string json = _fileSystem.File.ReadAllText(path);
            List<FileState>? files = JsonSerializer.Deserialize<List<FileState>>(json, SerializerOptions);
            return files is null ? SyncManifest.Empty : new SyncManifest(files);
        }
        catch (JsonException)
        {
            // A corrupt manifest is treated as no baseline: the next sync starts fresh and
            // additively, which is safe. IO errors still propagate - they mean the state
            // directory itself is unhealthy.
            return SyncManifest.Empty;
        }
    }

    /// <inheritdoc/>
    public void Save(string firstFolder, string secondFolder, SyncManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        _fileSystem.Directory.CreateDirectory(_baseDirectory);
        string path = ManifestPath(firstFolder, secondFolder);
        string json = JsonSerializer.Serialize(manifest.Files, SerializerOptions);

        // Write to a staging file and swap it in, so an interrupted save leaves the previous
        // baseline intact rather than a truncated one.
        string stagingPath = path + StagingFileSuffix;
        try
        {
            _fileSystem.File.WriteAllText(stagingPath, json);
            _fileSystem.File.Move(stagingPath, path, overwrite: true);
        }
        catch
        {
            DiscardStagingFile(stagingPath);
            throw;
        }
    }

    private string ManifestPath(string firstFolder, string secondFolder)
    {
        return _fileSystem.Path.Combine(_baseDirectory, ComputeKey(firstFolder, secondFolder) + ".json");
    }

    private string ComputeKey(string firstFolder, string secondFolder)
    {
        // The key is directional: syncing A with B records a different history than B with A.
        string first = _fileSystem.Path.GetFullPath(firstFolder).ToLowerInvariant();
        string second = _fileSystem.Path.GetFullPath(secondFolder).ToLowerInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{first}|{second}"));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private void DiscardStagingFile(string stagingPath)
    {
        // Best effort only: cleanup must never mask the failure that brought us here.
        try
        {
            if (_fileSystem.File.Exists(stagingPath))
            {
                _fileSystem.File.Delete(stagingPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
