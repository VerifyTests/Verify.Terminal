namespace Verify.Terminal.Tests;

public sealed class InlineSnapshotFinderTests
{
    [Fact]
    public void Should_Find_A_Staged_Snapshot()
    {
        var fileSystem = CreateFileSystem();
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("new snapshot"));

        var result = Find(fileSystem).ShouldHaveSingleItem();

        result.SourceFile.ShouldBe(InlineTestData.SourceFile);
        result.Line.ShouldBe(42);
        result.Expected.ShouldBe("old snapshot");
        result.Received.ShouldBe("new snapshot");
        result.IsQueued.ShouldBeFalse();
        result.Conflict.ShouldBeNull();

        var staged = result.Staged.ShouldHaveSingleItem();
        staged.Origin.ShouldBe("DotNet10_0");
        staged.PatchPath.GetFilename().FullPath
            .ShouldBe("SampleTests.Sample.a1b2c3d4.DotNet10_0.inlinepatch");
        staged.ReceivedPath.ShouldNotBeNull();
        staged.ExpectedPath.ShouldNotBeNull();
    }

    [Fact]
    public void Should_Find_A_Staged_Snapshot_Without_Its_Texts()
    {
        // The two texts are for looking at, so a snapshot is still reviewable without them: the
        // patch carries both.
        var fileSystem = CreateFileSystem();
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("new snapshot"), withTexts: false);

        var staged = Find(fileSystem).ShouldHaveSingleItem().Staged.ShouldHaveSingleItem();

        staged.ReceivedPath.ShouldBeNull();
        staged.ExpectedPath.ShouldBeNull();
    }

    [Fact]
    public void Should_Ignore_A_Staged_Remove()
    {
        // A Remove is applied by whoever produced it and is never reviewed.
        var fileSystem = CreateFileSystem();
        InlineTestData.Stage(
            fileSystem,
            InlineTestData.Patch(string.Empty, mode: InlinePatchMode.Remove));

        Find(fileSystem).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Ignore_An_Unreadable_Patch()
    {
        var fileSystem = CreateFileSystem();
        fileSystem
            .CreateFile($"{InlineTestData.StagingDirectory}/broken.inlinepatch")
            .SetTextContent("not a patch");

        Find(fileSystem).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Merge_Staged_Patches_That_Agree()
    {
        // A multi targeted run stages one patch per framework. Agreeing frameworks are one snapshot,
        // with both sets of files to clear once it is dealt with.
        var fileSystem = CreateFileSystem();
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("new snapshot"), "DotNet8_0");
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("new snapshot"));

        var result = Find(fileSystem).ShouldHaveSingleItem();

        result.Conflict.ShouldBeNull();
        result.Received.ShouldBe("new snapshot");
        result.Staged.Select(_ => _.Origin).ShouldBe(["DotNet10_0", "DotNet8_0"], ignoreOrder: true);
    }

    [Fact]
    public void Should_Flag_Staged_Patches_That_Disagree()
    {
        var fileSystem = CreateFileSystem();
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("from net8"), "DotNet8_0");
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("from net10"));

        var result = Find(fileSystem).ShouldHaveSingleItem();

        result.Conflict.ShouldBe("DotNet10_0 / DotNet8_0");
        result.Headers.ShouldHaveSingleItem().Note
            .ShouldBe("(inline, conflicting: DotNet10_0 / DotNet8_0)");
    }

    [Fact]
    public void Should_Prefer_The_Queued_Snapshot_Over_The_Staged_One()
    {
        // A run can stage its patch and have an owner arrive afterwards. The owner holds the live
        // state, but the staged files are still there to clear.
        var fileSystem = CreateFileSystem();
        InlineTestData.Stage(fileSystem, InlineTestData.Patch("staged"));

        var queue = new FakeInlineQueueOwner();
        queue.Queue(InlineTestData.Patch("queued"));

        var result = Find(fileSystem, queue).ShouldHaveSingleItem();

        result.IsQueued.ShouldBeTrue();
        result.Received.ShouldBe("queued");
        result.Staged.ShouldHaveSingleItem();
    }

    [Fact]
    public void Should_Flag_A_Conflicted_Queued_Snapshot()
    {
        var queue = new FakeInlineQueueOwner();
        queue.Queue(
            Origin(InlineTestData.Patch("from net8"), "net8.0"),
            Origin(InlineTestData.Patch("from net10"), "net10.0"));

        var result = Find(CreateFileSystem(), queue).ShouldHaveSingleItem();

        result.IsQueued.ShouldBeTrue();
        result.Conflict.ShouldBe("net8.0 / net10.0");
    }

    [Fact]
    public void Should_Ignore_Queued_Snapshots_Outside_The_Root()
    {
        // The queue is machine wide: one owner holds the pending snapshots of every solution on it.
        var queue = new FakeInlineQueueOwner();
        queue.Queue(InlineTestData.Patch("elsewhere", source: "/Other/src/SampleTests.cs"));

        Find(CreateFileSystem(), queue).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Ignore_A_Queued_Remove()
    {
        var queue = new FakeInlineQueueOwner();
        queue.Queue(InlineTestData.Patch(string.Empty, mode: InlinePatchMode.Remove));

        Find(CreateFileSystem(), queue).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Find_Nothing_When_Nothing_Is_Pending()
    {
        Find(CreateFileSystem()).ShouldBeEmpty();
    }

    private static InlinePatch Origin(InlinePatch patch, string framework)
    {
        patch.Framework = framework;
        return patch;
    }

    private static FakeFileSystem CreateFileSystem() =>
        new(new(PlatformFamily.Linux));

    private static IReadOnlyList<InlineSnapshot> Find(
        FakeFileSystem fileSystem,
        IInlineQueueOwner? queue = null)
    {
        var environment = new FakeEnvironment(PlatformFamily.Linux);
        var globber = new Globber(fileSystem, environment);
        var finder = new InlineSnapshotFinder(
            globber,
            environment,
            fileSystem,
            queue ?? new FakeInlineQueueOwner());

        return finder.Find();
    }
}
