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
    // beside the patch. Returns the trio as a scan would report it, for a test that starts from
    // the manager rather than from a scan.
    public static StagedInline Stage(
        FakeFileSystem fileSystem,
        InlinePatch patch,
        string runtime = "DotNet10_0",
        bool withTexts = true)
    {
        var name = $"SampleTests.Sample.a1b2c3d4.{runtime}";

        // The source the patch names, which a run stages beside rather than instead of: the
        // snapshot it holds is a literal in that file, and an accept rewrites it there.
        if (!fileSystem.Exist(new FilePath(patch.SourceFile)))
        {
            fileSystem
                .CreateFile(new FilePath(patch.SourceFile))
                .SetTextContent("// the test this snapshot belongs to");
        }

        fileSystem
            .CreateFile($"{StagingDirectory}/{name}.inlinepatch")
            .SetTextContent(InlinePatchFile.Build(patch));

        if (!withTexts)
        {
            return new(patch, new($"{StagingDirectory}/{name}.inlinepatch"), null, null, runtime);
        }

        fileSystem
            .CreateFile($"{StagingDirectory}/{name}.received.txt")
            .SetTextContent(patch.NewContent);
        fileSystem
            .CreateFile($"{StagingDirectory}/{name}.expected.txt")
            .SetTextContent(patch.OriginalValue ?? string.Empty);

        return new(
            patch,
            new($"{StagingDirectory}/{name}.inlinepatch"),
            new($"{StagingDirectory}/{name}.received.txt"),
            new($"{StagingDirectory}/{name}.expected.txt"),
            runtime);
    }
}
