namespace Verify.Terminal.IntegrationTests;

// An isolated temp directory that real Verify writes into and the real SnapshotFinder scans.
public sealed class Harness : IDisposable
{
    private readonly string _directory;

    public Harness(string name)
    {
        // Verify writes no received maps on a build server, so force it off to keep these scenarios
        // deterministic locally and on CI. The assembly disables test parallelization, so this is safe.
        DiffEngine.BuildServerDetector.Detected = false;

        _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "verify-terminal-it",
            $"{name}-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(_directory);
        Directory = new(_directory);
    }

    public DirectoryPath Directory { get; }

    // Verify writes maps to this test project's obj directory, but a real run scans a root that
    // contains obj. So copy this scenario's maps under the harness directory to match that layout.
    // Returns how many were copied, so a test can assert the map path is actually set up rather than
    // silently falling back.
    public int PublishMaps()
    {
        var copied = 0;
        var source = System.IO.Path.Combine(
            AttributeReader.GetIntermediateDirectory(typeof(Harness).Assembly),
            "VerifyReceived");
        if (!System.IO.Directory.Exists(source))
        {
            return copied;
        }

        var target = System.IO.Path.Combine(_directory, "obj", "VerifyReceived");
        System.IO.Directory.CreateDirectory(target);

        foreach (var file in System.IO.Directory.GetFiles(source))
        {
            var lines = System.IO.File.ReadAllLines(file);
            if (lines.Length > 0 &&
                lines[0].StartsWith(_directory, StringComparison.OrdinalIgnoreCase))
            {
                System.IO.File.Copy(file, System.IO.Path.Combine(target, System.IO.Path.GetFileName(file)), true);
                copied++;
            }
        }

        return copied;
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

    public void SeedVerified(string fileName, string content) =>
        System.IO.File.WriteAllText(System.IO.Path.Combine(_directory, fileName), content);

    public IReadOnlyList<string> ReceivedFileNames() =>
        System.IO.Directory
            .GetFiles(_directory, "*.received.*")
            .Select(System.IO.Path.GetFileName)
            .ToList();

    // Runs the real SnapshotFinder (real globber, real filesystem) over the temp directory.
    public Snapshot FindSingle()
    {
        var environment = new Spectre.IO.Environment();
        var fileSystem = new FileSystem();
        var globber = new Globber(fileSystem, environment);
        var finder = new SnapshotFinder(globber, environment);
        return finder.Find(Directory).Single();
    }

    public bool Accept(Snapshot snapshot) =>
        new SnapshotManager(new FileSystem()).Accept(snapshot);

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best effort cleanup of the temp directory.
        }
    }
}
