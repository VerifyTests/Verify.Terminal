using Spectre.IO;

namespace Verify.Terminal;

public static class FilePathExtensions
{
    public static FilePath AppendExtensionIfNotNull(this FilePath path, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return path;
        }

        return path.AppendExtension(extension);
    }
}
