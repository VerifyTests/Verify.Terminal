namespace Verify.Terminal.Tests;

public sealed class InlineSnapshotManagerTests
{
    [Fact]
    public void Should_Refuse_To_Accept_A_Conflicted_Snapshot()
    {
        // Only one of the frameworks' snapshots is rendered, so accepting would be picking between
        // them without having shown them.
        var queue = new FakeInlineQueueOwner();
        var snapshot = new InlineSnapshot(
            InlineTestData.Patch("new snapshot"),
            isQueued: true,
            conflict: "net8.0 / net10.0");

        var result = Create(queue).Accept(snapshot);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("net8.0 / net10.0");
        queue.Accepted.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Ask_The_Owner_To_Accept_A_Queued_Snapshot()
    {
        // Applying in the owner rather than here is what keeps one writer per source file.
        var queue = new FakeInlineQueueOwner { AcceptOutcome = InlineAcceptOutcome.Accepted };
        var patch = InlineTestData.Patch("new snapshot");

        var result = Create(queue).Accept(new(patch, isQueued: true));

        result.Succeeded.ShouldBeTrue();
        queue.Accepted.ShouldHaveSingleItem().ShouldBe(InlineKey.For(patch.SourceFile, patch.LineHint));
    }

    [Fact]
    public void Should_Report_An_Accept_The_Owner_Refused()
    {
        var queue = new FakeInlineQueueOwner
        {
            AcceptOutcome = InlineAcceptOutcome.Failed,
            AcceptMessage = "the file is open in an editor",
        };

        var result = Create(queue).Accept(new(InlineTestData.Patch("new snapshot"), isQueued: true));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe("the file is open in an editor");
    }

    [Fact]
    public void Should_Report_A_Queued_Accept_With_Nothing_To_Fall_Back_To()
    {
        // The owner went away between the listing and the accept, and the run staged nothing, so
        // the click moved no file, changed no source and would otherwise report nothing at all.
        var queue = new FakeInlineQueueOwner { AcceptOutcome = InlineAcceptOutcome.Unknown };

        var result = Create(queue).Accept(new(InlineTestData.Patch("new snapshot"), isQueued: true));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
    }

    [Fact]
    public void Should_Fall_Back_To_The_Staged_Patch_When_The_Owner_Went_Away()
    {
        // The other half of an Unknown: the owner went away between the listing and the accept, but
        // the run that handed it the patch staged one too, so there is something left to apply.
        using var source = new TemporarySource(
            """
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot("old snapshot");
            }
            """);

        var fileSystem = CreateFileSystem();
        var patch = InlineTestData.Patch("new snapshot", line: 4, source: source.Path);
        InlineTestData.Stage(fileSystem, patch);

        var queue = new FakeInlineQueueOwner { AcceptOutcome = InlineAcceptOutcome.Unknown };
        var snapshot = new InlineSnapshot(patch, isQueued: true, Staged(fileSystem, patch));

        Create(queue, fileSystem).Accept(snapshot).Succeeded.ShouldBeTrue();

        // Asked of the owner first, and applied here only because it did not answer for it.
        queue.Accepted.ShouldHaveSingleItem();
        source.Read().ShouldContain("""Snapshot("new snapshot")""");
        StagedFiles(fileSystem).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Clear_The_Staged_Files_When_The_Owner_Accepts()
    {
        var fileSystem = CreateFileSystem();
        var patch = InlineTestData.Patch("new snapshot");
        InlineTestData.Stage(fileSystem, patch);

        var queue = new FakeInlineQueueOwner { AcceptOutcome = InlineAcceptOutcome.Accepted };
        var snapshot = new InlineSnapshot(patch, isQueued: true, Staged(fileSystem, patch));

        Create(queue, fileSystem).Accept(snapshot).Succeeded.ShouldBeTrue();

        StagedFiles(fileSystem).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Apply_A_Staged_Snapshot()
    {
        using var source = new TemporarySource(
            """
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot("old snapshot");
            }
            """);

        var fileSystem = CreateFileSystem();
        var patch = InlineTestData.Patch("new snapshot", line: 4, source: source.Path);
        InlineTestData.Stage(fileSystem, patch);

        var snapshot = new InlineSnapshot(patch, isQueued: false, Staged(fileSystem, patch));

        Create(fileSystem: fileSystem).Accept(snapshot).Succeeded.ShouldBeTrue();

        source.Read().ShouldContain("""Snapshot("new snapshot")""");

        // The staged files are what a scan reads, so with the snapshot in the source they are all
        // that would still say it is pending.
        StagedFiles(fileSystem).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Report_A_Staged_Snapshot_Whose_Call_Site_Is_Gone()
    {
        using var source = new TemporarySource(
            """
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value);
            }
            """);

        var fileSystem = CreateFileSystem();
        var patch = InlineTestData.Patch("new snapshot", line: 4, source: source.Path);
        InlineTestData.Stage(fileSystem, patch);

        var snapshot = new InlineSnapshot(patch, isQueued: false, Staged(fileSystem, patch));

        var result = Create(fileSystem: fileSystem).Accept(snapshot);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain("Re-run the test");

        // Nothing was applied, so the snapshot stays pending.
        StagedFiles(fileSystem).ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_Discard_A_Queued_Snapshot_On_Reject()
    {
        var queue = new FakeInlineQueueOwner { DiscardResult = true };
        var patch = InlineTestData.Patch("new snapshot");

        var result = Create(queue).Reject(new(patch, isQueued: true));

        result.Succeeded.ShouldBeTrue();
        queue.Discarded.ShouldHaveSingleItem().ShouldBe(InlineKey.For(patch.SourceFile, patch.LineHint));
    }

    [Fact]
    public void Should_Treat_A_Discard_Of_Something_Already_Gone_As_Done()
    {
        // One error shape covers both "no entry for that key" and a refusal on a live one. An entry
        // that has already gone is the outcome a reject wanted.
        var queue = new FakeInlineQueueOwner
        {
            DiscardResult = false,
            DiscardMessage = "unknown key",
            StillPendingResult = false,
        };

        Create(queue).Reject(new(InlineTestData.Patch("new snapshot"), isQueued: true))
            .Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Should_Report_A_Discard_The_Owner_Refused()
    {
        var queue = new FakeInlineQueueOwner
        {
            DiscardResult = false,
            DiscardMessage = "still busy",
            StillPendingResult = true,
        };

        var result = Create(queue).Reject(new(InlineTestData.Patch("new snapshot"), isQueued: true));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe("still busy");
    }

    [Fact]
    public void Should_Treat_A_Discard_The_Owner_Could_Not_Be_Asked_About_As_Done()
    {
        // The discard failed and the owner could not be asked whether the entry survived it: it
        // went away, or answered with an error. Neither says the snapshot is still pending, so the
        // staging is cleared and the reject stands rather than reporting a failure it cannot back.
        var fileSystem = CreateFileSystem();
        var patch = InlineTestData.Patch("new snapshot");
        InlineTestData.Stage(fileSystem, patch);

        var queue = new FakeInlineQueueOwner
        {
            DiscardResult = false,
            DiscardMessage = "connection refused",
            StillPendingResult = null,
        };

        var snapshot = new InlineSnapshot(patch, isQueued: true, Staged(fileSystem, patch));

        Create(queue, fileSystem).Reject(snapshot).Succeeded.ShouldBeTrue();

        StagedFiles(fileSystem).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Clear_The_Staged_Files_On_Reject()
    {
        var fileSystem = CreateFileSystem();
        var patch = InlineTestData.Patch("new snapshot");
        InlineTestData.Stage(fileSystem, patch);

        var snapshot = new InlineSnapshot(patch, isQueued: false, Staged(fileSystem, patch));

        Create(fileSystem: fileSystem).Reject(snapshot).Succeeded.ShouldBeTrue();

        StagedFiles(fileSystem).ShouldBeEmpty();
    }

    private static FakeFileSystem CreateFileSystem() =>
        new(new FakeEnvironment(PlatformFamily.Linux));

    private static InlineSnapshotManager Create(
        IInlineQueueOwner? queue = null,
        FakeFileSystem? fileSystem = null) =>
        new(fileSystem ?? CreateFileSystem(), queue ?? new FakeInlineQueueOwner());

    // The staged trio as the finder builds it, for a test that starts from the manager instead.
    private static IReadOnlyList<StagedInline> Staged(FakeFileSystem fileSystem, InlinePatch patch)
    {
        var globber = new Globber(fileSystem, new FakeEnvironment(PlatformFamily.Linux));
        var finder = new InlineSnapshotFinder(
            globber,
            new FakeEnvironment(PlatformFamily.Linux),
            fileSystem,
            new FakeInlineQueueOwner());

        // Through the finder rather than by hand, so the manager is handed what it is handed in a
        // real run: the patch as it was read back off disk, beside the files it was read from.
        return finder
            .Find(new DirectoryPath("/Working"))
            .SelectMany(_ => _.Staged)
            .ToList();
    }

    private static IReadOnlyList<string> StagedFiles(FakeFileSystem fileSystem)
    {
        var globber = new Globber(fileSystem, new FakeEnvironment(PlatformFamily.Linux));
        return globber
            .Match("**/VerifyInline/*", new GlobberSettings { Root = new DirectoryPath("/Working") })
            .OfType<FilePath>()
            .Select(_ => _.FullPath)
            .ToList();
    }

    // A real source file, since applying a patch is a rewrite of one and DiffEngine does its own IO.
    private sealed class TemporarySource : IDisposable
    {
        private readonly string _directory;

        public TemporarySource(string content)
        {
            _directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"verify-terminal-inline-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(_directory);

            Path = System.IO.Path.Combine(_directory, "SampleTests.cs");
            System.IO.File.WriteAllText(Path, content);
        }

        public string Path { get; }

        public string Read() => System.IO.File.ReadAllText(Path);

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
}
