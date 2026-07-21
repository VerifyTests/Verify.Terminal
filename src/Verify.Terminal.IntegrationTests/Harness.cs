namespace Verify.Terminal.IntegrationTests;

// An isolated temp directory that real Verify writes into and the real SnapshotFinder scans.
public sealed class Harness : IDisposable
{
    private readonly string _directory;

    public Harness(string name)
    {
        _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "verify-terminal-it",
            $"{name}-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(_directory);
        Directory = new(_directory);
    }

    public DirectoryPath Directory { get; }

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
