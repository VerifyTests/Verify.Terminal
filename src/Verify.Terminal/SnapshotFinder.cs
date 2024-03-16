using System.Collections.Generic;
using System.Linq;
using Spectre.IO;

namespace Verify.Terminal;

public sealed class SnapshotFinder
{
    private readonly IFileSystem _fileSystem;
    private readonly IGlobber _globber;
    private readonly IEnvironment _environment;

    public SnapshotFinder(
        IFileSystem fileSystem,
        IGlobber globber,
        IEnvironment environment)
    {
        _fileSystem = fileSystem.NotNull();
        _globber = globber.NotNull();
        _environment = environment.NotNull();
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
            var (verifiedPath, isRerouted) = GetVerified(receivedPath);
            result.Add(new Snapshot(receivedPath, verifiedPath, isRerouted));
        }

        return result;
    }

    private (FilePath VerifiedPath, bool IsRerouted) GetVerified(FilePath received)
    {
        var isRerouted = false;
        var path = StripExtensions(received, out var originalExtension);

        var extension = path.GetExtension();
        if (extension != null)
        {
            if (extension.StartsWith(".DotNet") ||
                extension.StartsWith(".Mono") ||
                extension.StartsWith(".Net") ||
                extension.StartsWith(".Core"))
            {
                var temp = path.RemoveExtension()
                    .AppendExtension(".verified")
                    .AppendExtensionIfNotNull(originalExtension);

                if (_fileSystem.File.Exists(temp))
                {
                    isRerouted = true;
                    path = path.RemoveExtension();
                }
            }
        }

        path = path
            .AppendExtension(".verified")
            .AppendExtensionIfNotNull(originalExtension);

        return (path, isRerouted);
    }

    private static FilePath StripExtensions(FilePath path, out string? originalExtension)
    {
        originalExtension = path.GetExtension();

        while (path.HasExtension)
        {
            var current = path.GetExtension();
            if (current == ".received")
            {
                path = path.RemoveExtension();
                break;
            }

            path = path.RemoveExtension();
        }

        return path;
    }
}