using Spectre.IO;

namespace Verify.Terminal;

public sealed class Snapshot
{
    public FilePath Received { get; }
    public FilePath Verified { get; }

    public Snapshot(FilePath received)
    {
        Received = received ?? throw new ArgumentNullException(nameof(received));
        Verified = GetVerified(Received);
    }

    private static FilePath GetVerified(FilePath received)
    {
        static FilePath StripExtensions(FilePath path, out string? extension)
        {
            extension = path.GetExtension();

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

        var path = StripExtensions(received, out var extension);
        var verifiedPath = path.AppendExtension(".verified");
        if (extension != null)
        {
            verifiedPath = verifiedPath.AppendExtension(extension);
        }

        return verifiedPath;
    }
}
