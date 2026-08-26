namespace Verify.Terminal;

/// <summary>
/// The process holding the pending inline snapshots: DiffEngineTray when one is running, and
/// otherwise the DiffEngineViewer a test run launched.
/// </summary>
/// <remarks>
/// An interface so the queue can be stood in for. Every call behind it is a short loopback exchange
/// with another process, which a test has none of, and which is refused outright on a machine where
/// nothing is pending.
/// </remarks>
public interface IInlineQueueOwner
{
    /// <summary>
    /// Every pending inline snapshot the owner holds. False when no owner answered, which is not
    /// the same as an empty queue: only the first means there may be staged files to fall back to.
    /// </summary>
    bool TryList(out IReadOnlyList<PendingInline> pending);

    /// <summary>
    /// Asks the owner to apply the patch for a call site and drop it. Applying in the owner rather
    /// than here is what keeps one writer per source file.
    /// </summary>
    InlineAcceptOutcome Accept(string key, out string? message);

    /// <summary>
    /// Drops a pending snapshot without applying it.
    /// </summary>
    bool Discard(string key, out string? message);

    /// <summary>
    /// Whether the owner still holds the call site, or null when it could not be asked.
    /// </summary>
    bool? StillPending(string key);
}

/// <summary>
/// <see cref="IInlineQueueOwner" /> over the real queue.
/// </summary>
public sealed class InlineQueueOwner : IInlineQueueOwner
{
    public bool TryList(out IReadOnlyList<PendingInline> pending) =>
        InlineQueueClient.TryList(out pending);

    public InlineAcceptOutcome Accept(string key, out string? message) =>
        InlineQueueClient.Accept(key, out message);

    public bool Discard(string key, out string? message) =>
        InlineQueueClient.Discard(key, out message);

    public bool? StillPending(string key)
    {
        // Over the listing that carries no patches, since the answer is a yes or a no rather than
        // anything to render. A listing that fails is not a no: the owner went away, or answered
        // with an error, and neither says the snapshot is gone.
        if (!InlineQueueClient.TryListKeys(out var keys))
        {
            return null;
        }

        return keys.Contains(key);
    }
}
