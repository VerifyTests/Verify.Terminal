namespace Verify.Terminal;

/// <summary>
/// Accepts and rejects inline snapshots.
/// </summary>
/// <remarks>
/// A queued snapshot is accepted by asking its owner, which is what keeps one writer per source
/// file and leaves every surface agreeing about what is still pending. Only a snapshot no owner
/// holds is applied here, from the patch the test run staged.
/// </remarks>
public sealed class InlineSnapshotManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IInlineQueueOwner _queue;

    public InlineSnapshotManager(IFileSystem fileSystem, IInlineQueueOwner queue)
    {
        _fileSystem = fileSystem.NotNull();
        _queue = queue.NotNull();
    }

    /// <summary>
    /// Puts the snapshot in the source file. Succeeds when it is there afterwards, whether this
    /// call put it there or an earlier one did.
    /// </summary>
    public SnapshotResult Accept(InlineSnapshot snapshot)
    {
        snapshot.NotNull();

        // Only one of the frameworks' snapshots is rendered, so accepting would be picking between
        // them without having shown them. Refused here as it is in the tray, and for the same
        // reason.
        if (snapshot.Conflict != null)
        {
            return SnapshotResult.Failure(
                $"Conflicting snapshots ({snapshot.Conflict}). Resolve them in DiffEngineViewer, or re-run the tests so the frameworks agree.");
        }

        if (snapshot.IsQueued)
        {
            var outcome = _queue.Accept(snapshot.Key, out var message);
            if (outcome == InlineAcceptOutcome.Accepted)
            {
                // The owner applied it and dropped the entry. A run whose patch an owner took
                // stages nothing, so there is usually nothing left to clean up.
                return DeleteStaged(snapshot);
            }

            if (outcome == InlineAcceptOutcome.Failed)
            {
                return SnapshotResult.Failure(message);
            }

            // Unknown: the owner went away between the listing and the accept, or something else
            // took the entry first. Whatever the run staged, if anything, is all that is left.
            if (snapshot.Staged.Count == 0)
            {
                return SnapshotResult.Failure(
                    "The queue owner did not apply it. It may have been accepted elsewhere, or the owner may have exited, and the test run staged no patch to fall back on. Re-run the test if the snapshot is still pending.");
            }
        }

        return Apply(snapshot);
    }

    /// <summary>
    /// Drops the snapshot, leaving the source file as it is.
    /// </summary>
    public SnapshotResult Reject(InlineSnapshot snapshot)
    {
        snapshot.NotNull();

        if (snapshot.IsQueued &&
            !_queue.Discard(snapshot.Key, out var message) &&
            _queue.StillPending(snapshot.Key) == true)
        {
            // One error shape covers both "no entry for that key" and a refusal on a live one, so
            // which it was is asked rather than read out of the text. An entry that has already
            // gone is the outcome a reject wanted.
            return SnapshotResult.Failure(message);
        }

        return DeleteStaged(snapshot);
    }

    private SnapshotResult Apply(InlineSnapshot snapshot)
    {
        var staged = snapshot.Staged.FirstOrDefault();
        if (staged == null)
        {
            return SnapshotResult.Failure("The test run staged no patch to apply.");
        }

        // InlineApplier owns all locking, in process and cross process, so applying beside a tray or
        // a viewer doing the same is safe. No locking is added here.
        var result = InlineApplier.Apply(staged.Patch);
        switch (result.Status)
        {
            case InlineApplyStatus.Applied:
            case InlineApplyStatus.AlreadyApplied:
                // The run that staged these files may still have queued the patch with an owner
                // that arrived afterwards, and that queue outlives the run. Without this a tray
                // keeps offering a snapshot that is already in the source.
                DiffRunner.SettleInline(staged.Patch.SourceFile, staged.Patch.LineHint);
                return DeleteStaged(snapshot);

            case InlineApplyStatus.NotFound:
                return SnapshotResult.Failure(
                    "The call site could not be found, so the source has changed since the test ran. Re-run the test and accept again.");

            default:
                return SnapshotResult.Failure(result.Message);
        }
    }

    /// <summary>
    /// Clears the files a run staged. They are what a scan reads, so with them gone the snapshot
    /// stops being pending, and while they are there it does not.
    /// </summary>
    private SnapshotResult DeleteStaged(InlineSnapshot snapshot)
    {
        foreach (var staged in snapshot.Staged)
        {
            foreach (var path in new[] { staged.PatchPath, staged.ReceivedPath, staged.ExpectedPath })
            {
                if (path == null ||
                    !_fileSystem.File.Exists(path))
                {
                    continue;
                }

                try
                {
                    _fileSystem.File.Delete(path);
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    return SnapshotResult.Failure($"The staged file could not be deleted: {path.FullPath}");
                }

                if (_fileSystem.File.Exists(path))
                {
                    return SnapshotResult.Failure($"The staged file could not be deleted: {path.FullPath}");
                }
            }
        }

        return SnapshotResult.Success;
    }
}
