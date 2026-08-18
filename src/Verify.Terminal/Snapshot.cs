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
            var received = new SnapshotHeader(Received.GetFilename().FullPath);

            // The verified name is only worth a line of its own when it is not the one the received
            // name reads as, which is exactly when the snapshot was rerouted.
            if (!IsRerouted)
            {
                return [received];
            }

            return [received, new(Verified.GetFilename().FullPath, "(rerouted)")];
        }
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
