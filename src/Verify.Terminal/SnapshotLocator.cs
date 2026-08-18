namespace Verify.Terminal;

/// <summary>
/// Every snapshot a test run left pending under a directory, whichever form it takes.
/// </summary>
/// <remarks>
/// The two kinds are found in completely different ways — one by globbing `.received.` files, the
/// other by asking the process holding the inline queue — but a review or an accept treats them the
/// same, so the commands are handed one list rather than the seam between them.
/// </remarks>
public sealed class SnapshotLocator
{
    private readonly SnapshotFinder _snapshotFinder;
    private readonly InlineSnapshotFinder _inlineSnapshotFinder;

    public SnapshotLocator(
        SnapshotFinder snapshotFinder,
        InlineSnapshotFinder inlineSnapshotFinder)
    {
        _snapshotFinder = snapshotFinder.NotNull();
        _inlineSnapshotFinder = inlineSnapshotFinder.NotNull();
    }

    /// <summary>
    /// File snapshots first, then inline ones, so a review stays in a stable order rather than
    /// interleaving two unrelated scans.
    /// </summary>
    public IReadOnlyList<ISnapshot> Find(DirectoryPath? root = null) =>
    [
        .. _snapshotFinder.Find(root),
        .. _inlineSnapshotFinder.Find(root),
    ];
}
