namespace Verify.Terminal;

/// <summary>
/// What became of an accept or a reject.
/// </summary>
/// <remarks>
/// A result rather than a bool, because an inline snapshot can refuse for reasons the caller
/// cannot work out from the snapshot itself: the source moved since the test ran, the frameworks
/// disagreed, or the process holding the snapshot had something to say about it.
/// </remarks>
public sealed class SnapshotResult
{
    private SnapshotResult(bool succeeded, string? message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    public bool Succeeded { get; }

    /// <summary>
    /// What to tell the user about a failure, and null when there is nothing to add beyond the
    /// failure itself.
    /// </summary>
    public string? Message { get; }

    public static readonly SnapshotResult Success = new(true, null);

    public static SnapshotResult Failure(string? message = null) => new(false, message);
}
