namespace Verify.Terminal;

public sealed class SnapshotManager
{
    private readonly IFileSystem _fileSystem;

    public SnapshotManager(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool Process(Snapshot snapshot, SnapshotAction action)
    {
        return action switch
        {
            SnapshotAction.Accept => Accept(snapshot),
            SnapshotAction.Reject => Reject(snapshot),
            _ => throw new InvalidOperationException("Unknown snapshot action"),
        };
    }

    public bool Accept(Snapshot snapshot)
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
                    return false;
                }
            }

            // Now move the file
            _fileSystem.File.Move(snapshot.Received, snapshot.Verified);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public bool Reject(Snapshot snapshot)
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
                    return false;
                }
            }
        }
        catch
        {
            return false;
        }

        return true;
    }
}
