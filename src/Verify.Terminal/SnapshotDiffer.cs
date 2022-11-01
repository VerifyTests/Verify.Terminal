using System.IO;
using DiffPlex.DiffBuilder;
using Spectre.IO;

namespace Verify.Terminal;

public sealed class SnapshotDiffer
{
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;

    public SnapshotDiffer(IFileSystem fileSystem, IEnvironment environment)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public SnapshotDiff Diff(Snapshot snapshot)
    {
        var oldText = ReadText(snapshot.Verified) ?? string.Empty;
        var newText = ReadText(snapshot.Received) ?? string.Empty;

        var diff = SideBySideDiffBuilder.Instance.BuildDiffModel(oldText, newText);

        return new SnapshotDiff(snapshot, diff.OldText.Lines, diff.NewText.Lines);
    }

    private string? ReadText(FilePath path)
    {
        path = path.MakeAbsolute(_environment);

        if (!_fileSystem.File.Exists(path))
        {
            return null;
        }

        using (var stream = _fileSystem.File.OpenRead(path))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}
