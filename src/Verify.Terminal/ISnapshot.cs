namespace Verify.Terminal;

/// <summary>
/// A pending snapshot, whether its expected text lives in a `.verified.` file or in a string
/// literal in the test source.
/// </summary>
public interface ISnapshot
{
    /// <summary>
    /// Identifies the snapshot in summaries and in errors: a path for a file snapshot, and a
    /// `path:line` call site for an inline one.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The lines shown above the diff, never empty. Each is a path, plus an optional note about it.
    /// </summary>
    IReadOnlyList<SnapshotHeader> Headers { get; }
}

/// <summary>
/// One line of a diff header: what is being shown, and a caveat about it when there is one.
/// </summary>
public sealed record SnapshotHeader(string Path, string? Note = null);
