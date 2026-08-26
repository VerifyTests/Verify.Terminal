namespace Verify.Terminal;

public sealed class SnapshotDiffer
{
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;

    public SnapshotDiffer(IFileSystem fileSystem, IEnvironment environment)
    {
        _fileSystem = fileSystem.NotNull();
        _environment = environment.NotNull();
    }

    public SnapshotDiff Diff(ISnapshot snapshot)
    {
        var (oldText, newText) = Text(snapshot);

        var diff = SideBySideDiffBuilder.Instance.BuildDiffModel(oldText, newText, false);

        return new(snapshot, diff.OldText.Lines, diff.NewText.Lines);
    }

    private (string Old, string New) Text(ISnapshot snapshot)
    {
        return snapshot.NotNull() switch
        {
            Snapshot file => (ReadText(file.Verified) ?? string.Empty, ReadText(file.Received) ?? string.Empty),
            // An inline snapshot is held in memory: its expected text is the literal in the source,
            // which the patch already carries, so there is nothing to read to compare the two.
            InlineSnapshot inline => (inline.Expected, inline.Received),
            _ => throw new InvalidOperationException($"Unknown snapshot type: {snapshot.GetType().Name}"),
        };
    }

    private string? ReadText(FilePath path)
    {
        path = path.MakeAbsolute(_environment);

        if (!_fileSystem.File.Exists(path))
        {
            return null;
        }

        using var stream = _fileSystem.File.OpenRead(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
