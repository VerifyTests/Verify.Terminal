namespace Verify.Terminal.IntegrationTests;

// UseUniqueDirectory in split mode names the directory `{prefix}.received` and the files inside it
// after their targets, so no file name carries a `.received.` marker. A scan for `**/*.received.*`
// finds nothing, and the tool reported no pending snapshots at all for such a project.
//
// Verify records the pair for every received file it leaves on disk, split mode included, so the
// maps are what make these findable.
public class SplitModeTests : IntegrationTestBase
{
    [Fact]
    public async Task SingleTarget_IsDetectedAndAccepts()
    {
        using var harness = new Harness(nameof(SingleTarget_IsDetectedAndAccepts));

        var settings = SplitSettings(harness, "Single");
        await ProduceReceived(settings);

        // The marker is on the directory, not the file, which is the whole reason this needed fixing.
        var received = harness.SplitReceivedFiles().ShouldHaveSingleItem();
        System.IO.Path.GetFileName(received).ShouldNotContain(".received");
        harness.ReceivedFileNames().ShouldBeEmpty();

        harness.PublishMaps().ShouldBeGreaterThan(0);

        var snapshot = harness.FindSingle();
        snapshot.Received.FullPath.ShouldBe(Normalize(received));

        // The verified file goes in the sibling `.verified` directory under the same name.
        snapshot.Verified.FullPath.ShouldEndWith(
            $".verified/{System.IO.Path.GetFileName(received)}");

        Harness.Accept(snapshot).Succeeded.ShouldBeTrue();
        (await Verifies(settings)).ShouldBeTrue();
    }

    // A test with several targets produces one file per target inside the one directory, so the
    // scan has to yield all of them rather than the directory once.
    [Fact]
    public async Task MultipleTargets_AreAllDetected()
    {
        using var harness = new Harness(nameof(MultipleTargets_AreAllDetected));

        var settings = SplitSettings(harness, "Targets");
        var exception = await Record.ExceptionAsync(
            async () => await Verifier.Verify(
                new List<Target>
                {
                    new("txt", "first", "one"),
                    new("txt", "second", "two")
                },
                settings));
        exception.ShouldNotBeNull();

        harness.SplitReceivedFiles().Count.ShouldBe(2);
        harness.PublishMaps().ShouldBeGreaterThan(0);

        harness.FindFileSnapshots().Count.ShouldBe(2);
    }

    // Both conventions in one tree. Split mode is additive: the flat received file is still found
    // the way it always was.
    [Fact]
    public async Task AlongsideAFlatSnapshot_BothAreDetected()
    {
        using var harness = new Harness(nameof(AlongsideAFlatSnapshot_BothAreDetected));

        await ProduceReceived(SplitSettings(harness, "Split"));

        var flat = harness.CreateSettings();
        flat.UseTypeName("N");
        flat.UseMethodName("Flat");
        await ProduceReceived(flat);

        harness.SplitReceivedFiles().ShouldHaveSingleItem();
        harness.ReceivedFileNames().ShouldHaveSingleItem();
        harness.PublishMaps().ShouldBeGreaterThan(0);

        harness.FindFileSnapshots().Count.ShouldBe(2);
    }

    // Split mode names every file after its target, so the file name alone says nothing about which
    // test is under review. The directory holding it is what does.
    [Fact]
    public async Task Header_NamesTheDirectory()
    {
        using var harness = new Harness(nameof(Header_NamesTheDirectory));

        await ProduceReceived(SplitSettings(harness, "Header"));
        harness.PublishMaps().ShouldBeGreaterThan(0);

        var header = harness.FindSingle().Headers[0];

        System.IO.Path.GetFileName(header.Path).ShouldBe("target.txt");
        header.Path.ShouldStartWith("N.Header");
        header.Path.ShouldContain(".received/");
    }

    // A map records absolute paths and a test can send its snapshots anywhere, so a map found under
    // the scanned root can name a file outside it. Reviewing a directory must not reach out of it.
    [Fact]
    public async Task MappedOutsideTheRoot_IsNotDetected()
    {
        using var harness = new Harness(nameof(MappedOutsideTheRoot_IsNotDetected));
        using var outside = new Harness($"{nameof(MappedOutsideTheRoot_IsNotDetected)}-outside");

        // A real received file, in a directory the scan below never looks at.
        await ProduceReceived(SplitSettings(outside, "Outside"));
        var stray = outside.SplitReceivedFiles().ShouldHaveSingleItem();

        // Its map, published under the root that is scanned.
        harness.WriteMap(stray, stray.Replace(".received", ".verified", StringComparison.Ordinal));

        harness.FindFileSnapshots().ShouldBeEmpty();
    }

    // Without a map there is nothing naming the pair, and the file name says nothing either. The
    // scan yields nothing rather than guessing, which is the same contract the flat fallback has:
    // never invent a pairing it cannot support.
    [Fact]
    public async Task WithoutMap_IsNotDetected()
    {
        using var harness = new Harness(nameof(WithoutMap_IsNotDetected));

        await ProduceReceived(SplitSettings(harness, "NoMap"));

        harness.SplitReceivedFiles().ShouldHaveSingleItem();

        harness.FindFileSnapshots().ShouldBeEmpty();
    }

    private static VerifySettings SplitSettings(Harness harness, string method)
    {
        var settings = harness.CreateSettings();
        settings.UseTypeName("N");
        settings.UseMethodName(method);
        settings.UseUniqueDirectory();
        settings.UseSplitModeForUniqueDirectory();
        return settings;
    }

    // Spectre.IO reports paths with forward slashes, so a path read off the file system has to be
    // put in the same form before comparing.
    private static string Normalize(string path) =>
        new FilePath(path).FullPath;
}
