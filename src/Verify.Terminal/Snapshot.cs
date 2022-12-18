using Spectre.IO;

namespace Verify.Terminal;

public sealed class Snapshot
{
    public FilePath Received { get; }
    public FilePath Verified { get; }
    public bool IsRerouted { get; }

    public Snapshot(FilePath received)
    {
        Received = received ?? throw new ArgumentNullException(nameof(received));
        Verified = GetVerified(Received);
    }

    public Snapshot(FilePath received, FilePath verified, bool isRerouted)
    {
        Received = received ?? throw new ArgumentNullException(nameof(received));
        Verified = verified ?? throw new ArgumentNullException(nameof(verified));
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
