namespace Verify.Terminal;

public sealed class SnapshotManager
{
    private readonly IFileSystem _fileSystem;
    private readonly InlineSnapshotManager _inline;

    public SnapshotManager(IFileSystem fileSystem, InlineSnapshotManager inline)
    {
        _fileSystem = fileSystem.NotNull();
        _inline = inline.NotNull();
    }

    public SnapshotResult Process(ISnapshot snapshot, SnapshotAction action)
    {
        return action switch
        {
            SnapshotAction.Accept => Accept(snapshot),
            SnapshotAction.Reject => Reject(snapshot),
            _ => throw new InvalidOperationException("Unknown snapshot action"),
        };
    }

    public SnapshotResult Accept(ISnapshot snapshot)
    {
        return snapshot.NotNull() switch
        {
            Snapshot file => AcceptFile(file),
            // An inline snapshot lives in a source file, so accepting rewrites a literal rather
            // than moving a file, and is usually done by whichever process holds the snapshot.
            InlineSnapshot inline => _inline.Accept(inline),
            _ => throw UnknownType(snapshot),
        };
    }

    public SnapshotResult Reject(ISnapshot snapshot)
    {
        return snapshot.NotNull() switch
        {
            Snapshot file => RejectFile(file),
            InlineSnapshot inline => _inline.Reject(inline),
            _ => throw UnknownType(snapshot),
        };
    }

    private SnapshotResult AcceptFile(Snapshot snapshot)
    {
        try
        {
            // Delete the verified file
            if (_fileSystem.File.Exists(snapshot.Verified))
            {
                _fileSystem.File.Delete(snapshot.Verified);
                if (_fileSystem.File.Exists(snapshot.Verified))
                {
                    // Could not delete the file
                    return SnapshotResult.Failure();
                }
            }

            // Now move the file
            _fileSystem.File.Move(snapshot.Received, snapshot.Verified);
        }
        catch
        {
            return SnapshotResult.Failure();
        }

        return SnapshotResult.Success;
    }

    private SnapshotResult RejectFile(Snapshot snapshot)
    {
        try
        {
            // Delete the received file
            if (_fileSystem.File.Exists(snapshot.Received))
            {
                _fileSystem.File.Delete(snapshot.Received);
                if (_fileSystem.File.Exists(snapshot.Received))
                {
                    // Could not delete the file
                    return SnapshotResult.Failure();
                }
            }
        }
        catch
        {
            return SnapshotResult.Failure();
        }

        return SnapshotResult.Success;
    }

    private static InvalidOperationException UnknownType(ISnapshot snapshot) =>
        new($"Unknown snapshot type: {snapshot.GetType().Name}");
}
