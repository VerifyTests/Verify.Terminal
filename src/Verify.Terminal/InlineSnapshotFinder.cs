namespace Verify.Terminal;

/// <summary>
/// Finds the inline snapshots a test run left pending.
/// </summary>
/// <remarks>
/// Two sources, because a run hands its patch to whichever process owns the inline queue and only
/// stages files under `obj/VerifyInline/` when nothing answered. Both are read: a machine running
/// DiffEngineTray has everything in the queue and nothing on disk, and a machine without one has
/// the opposite, so a tool that reads only one of them reports nothing pending for half its users.
/// </remarks>
public sealed class InlineSnapshotFinder
{
    /// <summary>
    /// The directory Verify stages an inline snapshot under, inside the intermediate (obj)
    /// directory of the test project. Only a convention, so everything read out of it is checked
    /// rather than assumed.
    /// </summary>
    public const string StagingDirectoryName = "VerifyInline";

    private const string PatchPattern = $"**/{StagingDirectoryName}/*.inlinepatch";

    // Windows and macOS paths are case insensitive, and a source file reaches the queue from
    // different senders with different casing.
    private static readonly StringComparison _pathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IGlobber _globber;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IInlineQueueOwner _queue;

    public InlineSnapshotFinder(
        IGlobber globber,
        IEnvironment environment,
        IFileSystem fileSystem,
        IInlineQueueOwner queue)
    {
        _globber = globber.NotNull();
        _environment = environment.NotNull();
        _fileSystem = fileSystem.NotNull();
        _queue = queue.NotNull();
    }

    public IReadOnlyList<InlineSnapshot> Find(DirectoryPath? root = null)
    {
        root ??= _environment.WorkingDirectory;
        root = root.MakeAbsolute(_environment);

        var staged = Staged(root);
        var result = new List<InlineSnapshot>();

        // The owner holds the live state, so what it has takes precedence over anything left on
        // disk. Staged files for the same call site are carried across rather than dropped: they
        // are still there to clean up, and they are the fallback if the owner goes away before the
        // accept.
        foreach (var pending in Queued(root))
        {
            staged.Remove(pending.Key, out var files);
            result.Add(
                new(
                    pending.Patch,
                    isQueued: true,
                    files,
                    pending.Conflicted ? pending.OriginsLabel : null));
        }

        result.AddRange(staged.Values.Select(FromStaged));

        return result;
    }

    private IEnumerable<PendingInline> Queued(DirectoryPath root)
    {
        // A refused connection means nothing owns a queue, which is the ordinary state of a machine
        // with nothing pending, and also the state a run that staged its patches left behind.
        if (!_queue.TryList(out var pending))
        {
            return [];
        }

        // The queue is machine wide: one owner holds the pending snapshots of every solution on the
        // machine. Only the ones under the directory being scanned are this run's business.
        return pending
            .Where(_ => IsReviewable(_.Patch))
            .Where(_ => IsUnder(root, _.Patch.SourceFile))
            .OrderBy(_ => _.Patch.SourceFile, StringComparer.Ordinal)
            .ThenBy(_ => _.Patch.LineHint);
    }

    private Dictionary<string, List<StagedInline>> Staged(DirectoryPath root)
    {
        var result = new Dictionary<string, List<StagedInline>>(StringComparer.Ordinal);

        // Ordered, so the patch a conflicted call site renders is the same one on every run.
        var paths = _globber
            .Match(
                PatchPattern,
                new()
                {
                    Root = root
                })
            .OfType<FilePath>()
            .OrderBy(_ => _.FullPath, StringComparer.Ordinal);

        foreach (var path in paths)
        {
            if (!TryReadPatch(path, out var patch) ||
                !IsReviewable(patch))
            {
                continue;
            }

            var key = InlineKey.For(patch.SourceFile, patch.LineHint);
            if (!result.TryGetValue(key, out var group))
            {
                result[key] = group = [];
            }

            group.Add(
                new(
                    patch,
                    path,
                    Sibling(path, "received.txt"),
                    Sibling(path, "expected.txt"),
                    Origin(path)));
        }

        return result;
    }

    private static InlineSnapshot FromStaged(List<StagedInline> staged)
    {
        // A multi targeted run stages one patch per framework. Identical content is a single
        // snapshot; content that differs is the frameworks disagreeing, and only one of them can be
        // rendered. Compared through the patch's own definition of sameness, which ignores which
        // framework produced it.
        var conflicted = staged.Any(_ => !_.Patch.Matches(staged[0].Patch));

        var conflict = conflicted
            ? string.Join(" / ", staged.Select(_ => _.Origin ?? "unknown").Distinct(StringComparer.Ordinal))
            : null;

        return new(staged[0].Patch, isQueued: false, staged, conflict);
    }

    // A Remove is applied by whoever produced it and is never reviewed, so one reaching here is not
    // a pending snapshot. Checked rather than assumed, since a patch is read off disk.
    private static bool IsReviewable(InlinePatch patch) =>
        patch.Mode != InlinePatchMode.Remove;

    private bool IsUnder(DirectoryPath root, string path)
    {
        var prefix = root.FullPath.TrimEnd('/') + "/";
        return new FilePath(path)
            .MakeAbsolute(_environment)
            .FullPath
            .StartsWith(prefix, _pathComparison);
    }

    // Verify names the staged files `{type}.{method}.{hash}.{runtime}`, so the last segment says
    // which framework produced them. Best effort: it only ever labels a conflict.
    private static string? Origin(FilePath path)
    {
        var stem = path.GetFilenameWithoutExtension().FullPath;
        var index = stem.LastIndexOf('.');
        if (index < 0 ||
            index == stem.Length - 1)
        {
            return null;
        }

        return stem[(index + 1)..];
    }

    // The two texts a run stages beside a patch. Not needed to render the snapshot, since the patch
    // carries both, but they are part of what says it is still pending, so part of the cleanup.
    private FilePath? Sibling(FilePath patch, string extension)
    {
        var path = new FilePath(
            $"{patch.GetDirectory().FullPath}/{patch.GetFilenameWithoutExtension().FullPath}.{extension}");

        return _fileSystem.File.Exists(path) ? path : null;
    }

    private bool TryReadPatch(FilePath path, [NotNullWhen(true)] out InlinePatch? patch)
    {
        patch = null;

        // Read through the file system abstraction rather than DiffEngine's own TryRead, so this
        // scan sees what the rest of the tool sees.
        if (!_fileSystem.File.Exists(path))
        {
            return false;
        }

        string text;
        try
        {
            using var stream = _fileSystem.File.OpenRead(path);
            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read is one snapshot that cannot be reviewed, rather than a
            // scan that fails.
            return false;
        }

        return InlinePatchFile.TryParse(text, out patch);
    }
}
