namespace Verify.Terminal.Tests;

// The shape of what a test run leaves behind for an inline snapshot, so the tests describe the
// scenario rather than the file format.
internal static class InlineTestData
{
    public const string SourceFile = "/Working/src/SampleTests.cs";
    public const string StagingDirectory = "/Working/obj/VerifyInline";

    public static InlinePatch Patch(
        string content,
        string? original = "old snapshot",
        int line = 42,
        string source = SourceFile,
        InlinePatchMode mode = InlinePatchMode.Set) =>
        new(source, line, null, content, mode)
        {
            TestName = "SampleTests.Sample",
            MemberName = "Sample",
            OriginalValue = original,
        };

    // Verify names the staged files `{type}.{method}.{hash}.{runtime}`, and writes the two texts
    // beside the patch.
    public static void Stage(
        FakeFileSystem fileSystem,
        InlinePatch patch,
        string runtime = "DotNet10_0",
        bool withTexts = true)
    {
        var name = $"SampleTests.Sample.a1b2c3d4.{runtime}";

        fileSystem
            .CreateFile($"{StagingDirectory}/{name}.inlinepatch")
            .SetTextContent(InlinePatchFile.Build(patch));

        if (!withTexts)
        {
            return;
        }

        fileSystem
            .CreateFile($"{StagingDirectory}/{name}.received.txt")
            .SetTextContent(patch.NewContent);
        fileSystem
            .CreateFile($"{StagingDirectory}/{name}.expected.txt")
            .SetTextContent(patch.OriginalValue ?? string.Empty);
    }
}
