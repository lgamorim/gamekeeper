using System.IO.Abstractions;
using GameKeeper.App;

// Pure composition root: everything with behavior lives in Application, which takes its
// writers and file system injected so the whole surface stays unit-testable.
var application = new Application(new FileSystem(), Console.Out, Console.Error);
return application.Run(args);
