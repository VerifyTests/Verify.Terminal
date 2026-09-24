namespace Verify.Terminal.IntegrationTests;

// An isolated temp directory that real Verify writes into and the real SnapshotFinder scans.
//
// Nothing here reaches a real tray or viewer: DeadInlineQueue points every queue exchange this
// process makes at a port nothing listens on, before any scenario runs. A scenario that wants an
// owner stands its own up (InlineQueueHost), which overrides that for as long as it is alive.
public sealed class Harness : IDisposable
{
    private readonly string _directory;

    // DiffEngine reads this ahead of anything set in process, so a machine with it set overrides
    // MaxInstancesToLaunch entirely. Which is how these tests came to open diff windows.
    private const string MaxInstancesVariable = "DiffEngine_MaxInstances";

    // All process wide, so they are put back in Dispose rather than left set for whatever runs next.
    // Captured rather than assumed, since DiffEngine and the machine decide them between them.
    private readonly bool _diffDisabled = DiffEngine.DiffRunner.Disabled;
    private readonly string? _inlineViewer =
        System.Environment.GetEnvironmentVariable(DiffEngine.DiffRunner.InlineViewerVariable);
    private readonly string? _maxInstances =
        System.Environment.GetEnvironmentVariable(MaxInstancesVariable);

    public Harness(string name)
    {
        // Verify writes no received maps on a build server, so force it off to keep these scenarios
        // deterministic locally and on CI. The assembly disables test parallelization, so this is safe.
        DiffEngine.BuildServerDetector.Detected = false;

        // Nothing here launches a diff tool. The environment variable is what actually decides it,
        // so setting it in process alone is not enough; the call after it is what drops DiffEngine's
        // cached value so the new one is read.
        System.Environment.SetEnvironmentVariable(MaxInstancesVariable, "0");
        DiffEngine.DiffRunner.MaxInstancesToLaunch(0);

        _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "verify-terminal-it",
            $"{name}-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(_directory);
        Directory = new(_directory);
    }

    public DirectoryPath Directory { get; }

    // Verify writes maps to this test project's obj directory, but a real run scans a root that
    // contains obj. So this scenario's maps are moved under the harness directory to match that
    // layout. Returns how many were moved, so a test can assert the map path is actually set up
    // rather than silently falling back.
    //
    // Moved rather than copied: the paths in them are this scenario's temp directory, which goes
    // when it does, so a copy leaves a map naming nothing behind in obj for every scenario ever run.
    public int PublishMaps()
    {
        var moved = 0;
        var source = System.IO.Path.Combine(
            AttributeReader.GetIntermediateDirectory(typeof(Harness).Assembly),
            "VerifyReceived");
        if (!System.IO.Directory.Exists(source))
        {
            return moved;
        }

        var target = System.IO.Path.Combine(_directory, "obj", "VerifyReceived");
        System.IO.Directory.CreateDirectory(target);

        foreach (var file in System.IO.Directory.GetFiles(source))
        {
            var lines = File.ReadAllLines(file);
            if (lines.Length > 0 &&
                lines[0].StartsWith(_directory, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(file, System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)), true);
                moved++;
            }
        }

        return moved;
    }

    public VerifySettings CreateSettings()
    {
        var settings = new VerifySettings();
        settings.UseDirectory(_directory);
        // No diff tool, and allow the same prefix to be verified twice (generate then re-verify).
        settings.DisableDiff();
        settings.DisableRequireUniquePrefix();
        return settings;
    }

    // Inline snapshots are the one thing here that cannot have diff off. Staging is the tail of the
    // diff path, so `DisableDiff` leaves nothing on disk to find, and DiffEngine switches diff off
    // by itself on a build server, under continuous testing and under an AI CLI. So it is turned on
    // for these scenarios only, and put back in Dispose: the file snapshot tests keep the diff path
    // they have always had, which is none.
    public VerifySettings CreateInlineSettings(bool useQueue = false)
    {
        DiffEngine.DiffRunner.Disabled = false;

        // Being on that path would otherwise mean depending on whether a viewer happens to be
        // running. The default keeps an inline snapshot on the staging fallback; a scenario that
        // stood up its own queue owner (InlineQueueHost) opts back in, so its patch crosses the
        // socket exactly as it would to a tray or viewer.
        System.Environment.SetEnvironmentVariable(
            DiffEngine.DiffRunner.InlineViewerVariable,
            useQueue ? "true" : "false");

        var settings = new VerifySettings();
        settings.UseDirectory(_directory);
        settings.DisableRequireUniquePrefix();
        return settings;
    }

    // An inline snapshot lives in a string literal in the test source, so a scenario needs a source
    // file of its own to hold one.
    public string CreateSource(string content)
    {
        var path = System.IO.Path.Combine(_directory, "SampleTests.cs");
        File.WriteAllText(path, content);
        return path;
    }

    public string ReadSource() =>
        File.ReadAllText(System.IO.Path.Combine(_directory, "SampleTests.cs"));

    // Verify stages inline snapshots in this test project's obj directory, but a real run scans a
    // root that contains obj. So this scenario's staged files are moved under the harness directory
    // to match that layout. Returns how many were moved, so a test can assert that staging really
    // happened rather than silently finding nothing.
    //
    // Moved rather than copied, for the reason PublishMaps is: these patches name a source file in
    // this scenario's temp directory, so a copy leaves a trio naming a file that has gone behind in
    // obj, once per scenario per run, for whatever scans this project next.
    public int PublishInline()
    {
        var moved = 0;
        var source = System.IO.Path.Combine(
            AttributeReader.GetIntermediateDirectory(typeof(Harness).Assembly),
            InlineSnapshotFinder.StagingDirectoryName);
        if (!System.IO.Directory.Exists(source))
        {
            return moved;
        }

        var target = System.IO.Path.Combine(_directory, "obj", InlineSnapshotFinder.StagingDirectoryName);
        System.IO.Directory.CreateDirectory(target);

        foreach (var patch in System.IO.Directory.GetFiles(source, "*.inlinepatch"))
        {
            // The patch names the source file it edits, which is what tells this scenario's staging
            // from that of every other run this project has ever done.
            if (!File.ReadAllText(patch).Contains(_directory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The patch and the two texts beside it, which all share a name.
            var stem = System.IO.Path.GetFileNameWithoutExtension(patch);
            foreach (var file in System.IO.Directory.GetFiles(source, $"{stem}.*"))
            {
                File.Move(
                    file,
                    System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)),
                    true);
            }

            moved++;
        }

        return moved;
    }

    // What the harness directory holds once PublishInline has run, which is what a scan reads and
    // so what still says a snapshot is pending.
    public IReadOnlyList<string> StagedInlineFileNames()
    {
        var directory = System.IO.Path.Combine(
            _directory,
            "obj",
            InlineSnapshotFinder.StagingDirectoryName);
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        return System.IO.Directory
            .GetFiles(directory)
            .Select(_ => System.IO.Path.GetFileName(_))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public void SeedVerified(string fileName, string content) =>
        File.WriteAllText(System.IO.Path.Combine(_directory, fileName), content);

    public IReadOnlyList<string> ReceivedFileNames() =>
        System.IO.Directory
            .GetFiles(_directory, "*.received.*")
            .Select(_ => System.IO.Path.GetFileName(_))
            .ToList();

    // Writes a received map by hand, in the same shape Verify writes them, for a scenario that needs
    // a map naming something this harness did not produce.
    public void WriteMap(string received, string verified)
    {
        var target = System.IO.Path.Combine(_directory, "obj", "VerifyReceived");
        System.IO.Directory.CreateDirectory(target);
        File.WriteAllLines(
            System.IO.Path.Combine(target, $"{Guid.NewGuid():N}.txt"),
            [received, verified]);
    }

    // UseUniqueDirectory in split mode puts the marker in the directory name and names the files
    // inside after their targets, so none of them are visible to a scan for `*.received.*` files.
    public IReadOnlyList<string> SplitReceivedFiles() =>
        System.IO.Directory
            .GetDirectories(_directory, "*.received")
            .SelectMany(_ => System.IO.Directory.GetFiles(_, "*", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToList();

    // Runs the real SnapshotFinder (real globber, real filesystem) over the temp directory.
    public Snapshot FindSingle() => FindFileSnapshots().Single();

    public ISet<Snapshot> FindFileSnapshots()
    {
        var environment = new Spectre.IO.Environment();
        var fileSystem = new FileSystem();
        var globber = new Globber(fileSystem, environment);
        var finder = new SnapshotFinder(globber, environment);
        return finder.Find(Directory);
    }

    // Runs the real InlineSnapshotFinder over the temp directory. The real queue is asked as well,
    // as a real run would, which is safe here because the constructor forced this scenario's
    // snapshot onto the staging path and the queue is filtered to this directory.
    public InlineSnapshot FindSingleInline()
    {
        var environment = new Spectre.IO.Environment();
        var fileSystem = new FileSystem();
        var globber = new Globber(fileSystem, environment);
        var finder = new InlineSnapshotFinder(globber, environment, fileSystem, new InlineQueueOwner());
        return finder.Find(Directory).Single();
    }

    public static SnapshotResult Accept(ISnapshot snapshot) =>
        CreateManager().Accept(snapshot);

    public static SnapshotResult Reject(ISnapshot snapshot) =>
        CreateManager().Reject(snapshot);

    private static SnapshotManager CreateManager()
    {
        var fileSystem = new FileSystem();
        var inline = new InlineSnapshotManager(fileSystem, new InlineQueueOwner());
        return new(fileSystem, inline);
    }

    public void Dispose()
    {
        DiffEngine.DiffRunner.Disabled = _diffDisabled;
        System.Environment.SetEnvironmentVariable(DiffEngine.DiffRunner.InlineViewerVariable, _inlineViewer);
        System.Environment.SetEnvironmentVariable(MaxInstancesVariable, _maxInstances);

        ClearIntermediate();

        try
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best effort cleanup of the temp directory.
        }
    }

    // Verify writes into this test project's obj as it runs: a map for every received file it
    // leaves, and a staged trio for an inline snapshot nothing owned. What a scenario published was
    // moved, but one that published nothing, or wrote more afterwards, leaves the rest naming a
    // temp directory that goes with this harness. Taken here rather than left to accumulate a
    // run's worth per run, for whatever scans this project next.
    private void ClearIntermediate()
    {
        var intermediate = AttributeReader.GetIntermediateDirectory(typeof(Harness).Assembly);

        // A map holds the received path on its first line, so the file itself says whose it is.
        ClearNaming(
            System.IO.Path.Combine(intermediate, "VerifyReceived"),
            "*",
            _ => System.IO.Path.GetFileName(_));

        // Only the patch of a staged trio names the source; the two texts beside it are snapshot
        // content, and are found by the name they share with it.
        ClearNaming(
            System.IO.Path.Combine(intermediate, InlineSnapshotFinder.StagingDirectoryName),
            "*.inlinepatch",
            _ => $"{System.IO.Path.GetFileNameWithoutExtension(_)}.*");
    }

    private void ClearNaming(string directory, string pattern, Func<string, string> companions)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in System.IO.Directory.GetFiles(directory, pattern))
        {
            try
            {
                if (!File.ReadAllText(file).Contains(_directory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var owned in System.IO.Directory.GetFiles(directory, companions(file)))
                {
                    File.Delete(owned);
                }
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // Best effort: a file left here is noise in obj rather than a failed scenario.
            }
        }
    }
}
