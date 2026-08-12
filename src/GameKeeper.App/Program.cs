using System.IO.Abstractions;
using GameKeeper.App;
using GameKeeper.Core;

// Pure composition root: everything with behavior lives in Application and the engine, which
// take their collaborators injected so the whole surface stays unit-testable.
var fileSystem = new FileSystem();

// Sync state is machine-local by design (it records what THIS machine has seen), so it lives
// under local app data rather than the roaming profile.
string stateDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "GameKeeper",
    "state");

var stateStore = new JsonSyncStateStore(fileSystem, stateDirectory);
var synchronizer = new FolderSynchronizer(fileSystem, stateStore);
var application = new Application(synchronizer, fileSystem, Console.Out, Console.Error);
return application.Run(args);
