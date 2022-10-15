using System.Threading.Tasks;
using Spectre.Console.Testing;
using Spectre.IO;
using Spectre.IO.Testing;
using VerifyTests;
using VerifyXunit;

namespace Verify.Terminal.Tests;

[UsesVerify]
[ExpectationPath("Rendering")]
public class SnapshotRendererTests
{
    [Theory]
    [Expectation("Render")]
    [InlineData("First")]
    [InlineData("Second")]
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
}