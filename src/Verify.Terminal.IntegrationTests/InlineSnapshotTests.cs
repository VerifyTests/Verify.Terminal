namespace Verify.Terminal.IntegrationTests;

// Covers inline snapshots, where the expected text lives in a string literal in the test source
// rather than in a `.verified.` file, so accepting rewrites that source instead of moving a file.
//
// Drives real Verify until it leaves a real staged patch, then runs the real InlineSnapshotFinder
// and SnapshotManager over it. The purpose is to pin the assumptions Verify.Terminal makes about
// how Verify stages an inline snapshot and what accepting one has to produce, so a future Verify
// that changes either breaks these tests rather than silently misbehaving.
public class InlineSnapshotTests
{
    // Multi line on purpose: a single line literal would pass whatever shape the accept wrote it
    // in, and the shape is half of what has to hold for the next run to read it back.
    private const string Value = "line one\nline two";

    [Fact]
    public async Task ChangedSnapshot_IsStaged_AndAcceptRewritesTheSource()
    {
        using var harness = new Harness(nameof(ChangedSnapshot_IsStaged_AndAcceptRewritesTheSource));

        var source = harness.CreateSource(
            """
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot("old snapshot");
            }
            """);

        // The call site is passed rather than left to the caller attributes, so the snapshot
        // belongs to the generated source above instead of to this file.
        await Fails(harness, _ => _.Snapshot("old snapshot", source, 4, "\"old snapshot\"", "Sample"));

        // Nothing owned a queue, so the patch and its two texts are on disk. The names carry the
        // framework that produced them, which is what tells one framework's snapshot from another's
        // when a multi targeted run disagrees with itself.
        harness.PublishInline().ShouldBe(1);
        harness.StagedInlineFileNames()
            .Select(_ => System.IO.Path.GetExtension(_))
            .ShouldBe([".inlinepatch", ".txt", ".txt"], ignoreOrder: true);

        var snapshot = harness.FindSingleInline();
        snapshot.SourceFile.ShouldBe(source);
        snapshot.Line.ShouldBe(4);
        snapshot.Expected.ShouldBe("old snapshot");
        snapshot.Received.ShouldBe(Value);
        snapshot.IsQueued.ShouldBeFalse();
        snapshot.Conflict.ShouldBeNull();
        snapshot.Staged.ShouldHaveSingleItem();

        // The staged received text is named like any other received file. It belongs to a snapshot
        // that lives in a source file, so the file scan has to leave it alone: accepting it as a
        // file snapshot would rename it to a verified file nothing reads, and leave the real
        // snapshot pending.
        harness.FindFileSnapshots().ShouldBeEmpty();

        harness.Accept(snapshot).Succeeded.ShouldBeTrue();

        // The literal is written as the raw string Verify reads back, which is what makes the next
        // run pass rather than fail against its own snapshot.
        harness.ReadSource().ShouldBe(
            """"
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot(
                        """
                        line one
                        line two
                        """);
            }
            """");

        // The staged files are all that would still say the snapshot is pending.
        harness.StagedInlineFileNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task NewSnapshot_IsStaged_AndAcceptWritesTheLiteral()
    {
        using var harness = new Harness(nameof(NewSnapshot_IsStaged_AndAcceptWritesTheLiteral));

        // A Snapshot call with no expected argument: the snapshot has never been accepted, so there
        // is no literal to compare against and accepting writes the first one.
        var source = harness.CreateSource(
            """
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot();
            }
            """);

        await Fails(harness, _ => _.Snapshot(null, source, 4, null, "Sample"));

        harness.PublishInline().ShouldBe(1);

        var snapshot = harness.FindSingleInline();

        // No literal yet, which compares as an empty verified file does.
        snapshot.Expected.ShouldBeEmpty();
        snapshot.Received.ShouldBe(Value);

        harness.Accept(snapshot).Succeeded.ShouldBeTrue();

        harness.ReadSource().ShouldBe(
            """"
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot(
                        """
                        line one
                        line two
                        """);
            }
            """");
    }

    [Fact]
    public async Task RejectedSnapshot_LeavesTheSourceAlone()
    {
        using var harness = new Harness(nameof(RejectedSnapshot_LeavesTheSourceAlone));

        var before =
            """
            public class SampleTests
            {
                public Task Sample() =>
                    Verify(value).Snapshot("old snapshot");
            }
            """;
        var source = harness.CreateSource(before);

        await Fails(harness, _ => _.Snapshot("old snapshot", source, 4, "\"old snapshot\"", "Sample"));
        harness.PublishInline().ShouldBe(1);

        harness.Reject(harness.FindSingleInline()).Succeeded.ShouldBeTrue();

        harness.ReadSource().ShouldBe(before);

        // Rejecting clears the staging, so the snapshot stops being pending without the source
        // having been touched.
        harness.StagedInlineFileNames().ShouldBeEmpty();
    }

    // Runs Verify against the literal the generated source holds, expecting the mismatch (or the
    // new snapshot) that leaves a patch behind.
    private static async Task Fails(Harness harness, Action<VerifySettings> snapshot)
    {
        var settings = harness.CreateInlineSettings();
        settings.UseTypeName("N");
        settings.UseMethodName("Sample");
        snapshot(settings);

        var exception = await Record.ExceptionAsync(async () => await Verifier.Verify(Value, settings));
        exception.ShouldNotBeNull("Verify was expected to fail and leave an inline snapshot pending.");
    }
}
