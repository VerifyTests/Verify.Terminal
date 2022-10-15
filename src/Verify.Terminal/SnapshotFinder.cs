using System.Collections.Generic;
using System.Linq;
using Spectre.IO;

namespace Verify.Terminal;

public sealed class SnapshotFinder
{
    private readonly IGlobber _globber;
    private readonly IEnvironment _environment;

    public SnapshotFinder(
        IGlobber globber,
        IEnvironment environment)
    {
        _globber = globber ?? throw new ArgumentNullException(nameof(globber));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public ISet<Snapshot> Find(DirectoryPath? root = null)
    {
        root ??= _environment.WorkingDirectory;
        root = root.MakeAbsolute(_environment);

        var result = new HashSet<Snapshot>();

        root = root.MakeAbsolute(_environment);
        var received = _globber.Match("**/*.received.*", new GlobberSettings
        {
            Root = root,
        }).Cast<FilePath>();

        foreach (var receivedPath in received)
        {
            result.Add(new Snapshot(received: receivedPath));
        }

        return result;
    }
}
