namespace Verify.Terminal;

public sealed class Snapshot : ISnapshot
{
    public FilePath Received { get; }
    public FilePath Verified { get; }
    public bool IsRerouted { get; }

    public string Name => Received.FullPath;

    public IReadOnlyList<SnapshotHeader> Headers
    {
        get
        {
            var received = new SnapshotHeader(Describe(Received));

            // The verified name is only worth a line of its own when it is not the one the received
            // name reads as, which is exactly when the snapshot was rerouted.
            if (!IsRerouted)
            {
                return [received];
            }

            return [received, new(Describe(Verified), "(rerouted)")];
        }
    }

    // A split mode snapshot is named after its target, and the test it belongs to is the directory
    // holding it, so the file name alone does not say which snapshot is under review. Every split
    // mode snapshot in a run would otherwise read the same, commonly `target.txt`.
    private static string Describe(FilePath path)
    {
        var directory = path.GetDirectory().FullPath;
        var separator = directory.LastIndexOf('/');
        var directoryName = separator < 0 ? directory : directory[(separator + 1)..];

        if (directoryName.EndsWith(".received", StringComparison.Ordinal) ||
            directoryName.EndsWith(".verified", StringComparison.Ordinal))
        {
            return $"{directoryName}/{path.GetFilename().FullPath}";
        }

        return path.GetFilename().FullPath;
    }

    public Snapshot(FilePath received)
    {
        Received = received.NotNull();
        Verified = GetVerified(Received);
    }

    public Snapshot(FilePath received, FilePath verified, bool isRerouted)
    {
        Received = received.NotNull();
        Verified = verified.NotNull();
        IsRerouted = isRerouted;
    }

    private static FilePath GetVerified(FilePath received)
    {
        static FilePath StripExtensions(FilePath path, out string? extension)
        {
            extension = path.GetExtension();

            while (path.HasExtension)
            {
                var current = path.GetExtension();
                path = path.RemoveExtension();

                if (current == ".received")
                {
                    break;
                }
            }

            return path;
        }

        var path = StripExtensions(received, out var extension);
        var verifiedPath = path.AppendExtension(".verified");
        if (extension != null)
        {
            verifiedPath = verifiedPath.AppendExtension(extension);
        }

        return verifiedPath;
    }
}
