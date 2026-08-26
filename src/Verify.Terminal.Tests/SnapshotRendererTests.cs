namespace Verify.Terminal.Tests;

[ExpectationPath("Rendering")]
public class SnapshotRendererTests
{
    [Theory]
    [Expectation("Render")]
    [InlineData("First")]
    [InlineData("Second")]
    [InlineData("Third")]
    [InlineData("Fourth")]
    public Task Should_Render_Correctly(string scenario)
    {
        // Given
        var environment = new FakeEnvironment(PlatformFamily.Linux);
        var filesystem = new FakeFileSystem(environment);
        var console = new TestConsole();
        var renderer = new SnapshotRenderer(console);
        var differ = new SnapshotDiffer(filesystem, environment);

        filesystem.CreateFile($"/input/{scenario}.verified.txt")
            .SetEmbedded($"Verify.Terminal.Tests/Data/{scenario}/old");
        filesystem.CreateFile($"/input/{scenario}.received.txt")
            .SetEmbedded($"Verify.Terminal.Tests/Data/{scenario}/new");

        var diff = differ.Diff(
            new Snapshot($"/input/{scenario}.received.txt"));

        // When
        console.Write(renderer.Render(diff, contextLines: 2));

        // Then
        return Verifier.Verify(console.Output)
            .UseTextForParameters(scenario);
    }

    [Fact]
    [Expectation("RenderInline")]
    public Task Should_Render_An_Inline_Snapshot()
    {
        // Given
        var environment = new FakeEnvironment(PlatformFamily.Linux);
        var filesystem = new FakeFileSystem(environment);
        var console = new TestConsole();
        var renderer = new SnapshotRenderer(console);
        var differ = new SnapshotDiffer(filesystem, environment);

        // An inline snapshot has no files to read: the literal in the source and the text the run
        // produced both ride on the patch.
        var diff = differ.Diff(
            new InlineSnapshot(
                InlineTestData.Patch(
                    """
                    line1
                    line2 changed
                    line3
                    """,
                    """
                    line1
                    line2
                    line3
                    """),
                isQueued: true));

        // When
        console.Write(renderer.Render(diff, contextLines: 2));

        // Then
        return Verifier.Verify(console.Output);
    }

    [Fact]
    [Expectation("RenderConflictedInline")]
    public Task Should_Render_A_Conflicted_Inline_Snapshot()
    {
        // Given
        var environment = new FakeEnvironment(PlatformFamily.Linux);
        var filesystem = new FakeFileSystem(environment);
        var console = new TestConsole();
        var renderer = new SnapshotRenderer(console);
        var differ = new SnapshotDiffer(filesystem, environment);

        // Only the first of the frameworks' snapshots can be shown, so the header says the others
        // are there.
        var diff = differ.Diff(
            new InlineSnapshot(
                InlineTestData.Patch("from net10"),
                isQueued: true,
                conflict: "net8.0 / net10.0"));

        // When
        console.Write(renderer.Render(diff, contextLines: 2));

        // Then
        return Verifier.Verify(console.Output);
    }
}