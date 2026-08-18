namespace Verify.Terminal;

/// <summary>
/// A pending inline snapshot: the expected text lives in a string literal in the test source, so
/// accepting rewrites that source instead of moving a file.
/// </summary>
/// <remarks>
/// A test run hands its patch to whichever process owns the inline queue, which is DiffEngineTray
/// when one is running and otherwise the DiffEngineViewer the run launched. Only when nothing
/// answers does it stage the patch, and the two texts, under `obj/VerifyInline/`. So a snapshot
/// here came from one of those two places, and occasionally from both, when a run staged its patch
/// and an owner arrived afterwards.
/// </remarks>
public sealed class InlineSnapshot : ISnapshot
{
    public InlineSnapshot(
        InlinePatch patch,
        bool isQueued,
        IReadOnlyList<StagedInline>? staged = null,
        string? conflict = null)
    {
        Patch = patch.NotNull();
        IsQueued = isQueued;
        Staged = staged ?? [];
        Conflict = conflict;
    }

    /// <summary>
    /// The edit the test run produced. Also carries the anchors that say which call it came from,
    /// so a source file that has shifted since still patches.
    /// </summary>
    public InlinePatch Patch { get; }

    /// <summary>
    /// Held by a queue owner, so accepting is asked of it rather than done here. That is what keeps
    /// one writer per source file, and leaves every surface agreeing about what is still pending.
    /// </summary>
    public bool IsQueued { get; }

    /// <summary>
    /// What a run staged when nothing owned a queue, empty when one did. More than one when a
    /// multi targeted run staged a patch per framework.
    /// </summary>
    public IReadOnlyList<StagedInline> Staged { get; }

    /// <summary>
    /// Set when this call site has more than one content, which a multi targeted run produces when
    /// its frameworks disagree. Names them. Only one of them is rendered, so accepting would be
    /// picking between them silently, and is refused instead.
    /// </summary>
    public string? Conflict { get; }

    /// <summary>
    /// How the queue addresses this call site. A re-run of the same test produces the same key.
    /// </summary>
    public string Key => InlineKey.For(Patch.SourceFile, Patch.LineHint);

    public string SourceFile => Patch.SourceFile;

    /// <summary>
    /// 1 based line of the call. A hint the patcher starts from rather than an address: the literal
    /// itself is found by content search.
    /// </summary>
    public int Line => Patch.LineHint;

    /// <summary>
    /// The snapshot the source holds as it stands. Empty for one that has no literal yet, which
    /// compares as an empty verified file does.
    /// </summary>
    public string Expected => Patch.OriginalValue ?? string.Empty;

    /// <summary>
    /// The snapshot the test run produced.
    /// </summary>
    public string Received => Patch.NewContent;

    public string Name => $"{SourceFile}:{Line}";

    public IReadOnlyList<SnapshotHeader> Headers =>
        [new($"{new FilePath(SourceFile).GetFilename().FullPath}:{Line}", Note)];

    // Said on every inline snapshot, since the header is otherwise a source file where a reviewer
    // is used to reading a `.received.` file, and accepting one edits that source.
    private string Note =>
        Conflict == null
            ? "(inline)"
            : $"(inline, conflicting: {Conflict})";
}

/// <summary>
/// The files a test run left behind for one inline snapshot when nothing owned a queue: the patch,
/// and the two texts it was staged beside.
/// </summary>
/// <param name="Patch">The edit, as read back from <paramref name="PatchPath" />.</param>
/// <param name="PatchPath">The staged patch file.</param>
/// <param name="ReceivedPath">The staged received text, or null when it is not there.</param>
/// <param name="ExpectedPath">The staged expected text, or null when it is not there.</param>
/// <param name="Origin">
/// The framework that staged this, read off the file name. Null when the name does not carry one.
/// </param>
public sealed record StagedInline(
    InlinePatch Patch,
    FilePath PatchPath,
    FilePath? ReceivedPath,
    FilePath? ExpectedPath,
    string? Origin);
