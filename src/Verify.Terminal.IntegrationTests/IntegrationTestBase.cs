namespace Verify.Terminal.IntegrationTests;

// Base for the naming integration tests. Drives real Verify to produce received files, then asserts
// how the real SnapshotFinder pairs them with verified files.
//
// The purpose is to pin the assumptions Verify.Terminal makes about Verify's received/verified naming,
// so a future Verify version that changes naming breaks these tests rather than silently misbehaving.
public abstract class IntegrationTestBase
{
    // The value verified in every scenario. After a successful accept the verified file holds this,
    // so a re-verify with the same value passes.
    protected const string Value = "the-received-value";

    // Runs Verify expecting failure (new or changed snapshot) and returns the verified file name Verify
    // itself reports it wants. That name is the ground truth for the verified naming.
    protected static async Task<string> ProduceReceived(VerifySettings settings)
    {
        var exception = await Record.ExceptionAsync(async () => await Verifier.Verify(Value, settings));
        exception.ShouldNotBeNull("Verify was expected to fail and produce a received file.");
        return ParseVerifiedFileName(exception.Message);
    }

    // Runs Verify and reports whether it passed (no exception).
    protected static async Task<bool> Verifies(VerifySettings settings)
    {
        var exception = await Record.ExceptionAsync(async () => await Verifier.Verify(Value, settings));
        return exception is null;
    }

    protected static string AssemblyConfiguration() =>
        typeof(IntegrationTestBase).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!
            .Configuration;

    // A scenario where the correct verified file exists, so the finder should pair the received file
    // with it (rerouting when the names differ) and an accept round-trips cleanly.
    // Run against both, since the map is how Verify behaves now, while the fallback still applies to
    // snapshots from an older Verify or from a build server. Both have to reach the same verified file.
    // Looped rather than a [Theory], since a test method parameter would be appended to the snapshot
    // name by Verify and change the very names under assertion.
    protected async Task AssertExistingVerifiedIsDetected(
        string method,
        Action<VerifySettings> configure,
        string expectedVerified)
    {
        await Run(withMap: true);
        await Run(withMap: false);

        return;

        async Task Run(bool withMap)
        {
            using var harness = new Harness(method);

            VerifySettings Settings()
            {
                var settings = harness.CreateSettings();
                settings.UseTypeName("N");
                settings.UseMethodName(method);
                configure(settings);
                return settings;
            }

            var because = withMap ? "with map" : "without map";

            var correctVerified = await ProduceReceived(Settings());

            // Verify's own verified name matches what Verify.Terminal assumes.
            correctVerified.ShouldBe(expectedVerified, because);

            // In a multi-targeted project the received file always ends with the runtime and version.
            var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
            received.ShouldEndWith($".{Namer.RuntimeAndVersion}.received.txt", customMessage: because);

            // Make the correct verified file exist, then let the finder pair against it.
            harness.SeedVerified(correctVerified, "old-verified");
            if (withMap)
            {
                // Both paths reach the same file here, so assert the map really was published,
                // otherwise this would silently be testing the fallback twice.
                harness.PublishMaps().ShouldBeGreaterThan(0);
            }

            var snapshot = harness.FindSingle();

            System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(correctVerified, because);
            var literal = received.Replace(".received.", ".verified.");
            snapshot.IsRerouted.ShouldBe(correctVerified != literal, because);

            harness.Accept(snapshot).ShouldBeTrue(because);

            // The received value now lives at the correct verified name, so Verify passes.
            (await Verifies(Settings())).ShouldBeTrue(because);
        }
    }

    // A brand new snapshot with no verified file on disk. With nothing to pair against, the finder
    // falls back to the received-derived name. Whether that is correct depends on whether the correct
    // verified name equals the received-derived name.
    protected async Task AssertNewSnapshot(
        string method,
        Action<VerifySettings> configure,
        string expectedVerified,
        bool expectRoundTrips)
    {
        using var harness = new Harness(method);

        VerifySettings Settings()
        {
            var settings = harness.CreateSettings();
            settings.UseTypeName("N");
            settings.UseMethodName(method);
            configure(settings);
            return settings;
        }

        var correctVerified = await ProduceReceived(Settings());
        correctVerified.ShouldBe(expectedVerified);

        var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
        received.ShouldEndWith($".{Namer.RuntimeAndVersion}.received.txt");

        // No verified file exists, so the finder can only fall back to the received-derived name.
        var snapshot = harness.FindSingle();
        var literal = received.Replace(".received.", ".verified.");
        System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(literal);
        snapshot.IsRerouted.ShouldBeFalse();

        harness.Accept(snapshot).ShouldBeTrue();

        (await Verifies(Settings())).ShouldBe(expectRoundTrips);
    }

    // A brand new snapshot, but with Verify's received map available. The map names the verified file,
    // so the finder places it correctly instead of falling back to the received-derived name.
    protected async Task AssertNewSnapshotWithMap(
        string method,
        Action<VerifySettings> configure,
        string expectedVerified)
    {
        using var harness = new Harness(method);

        VerifySettings Settings()
        {
            var settings = harness.CreateSettings();
            settings.UseTypeName("N");
            settings.UseMethodName(method);
            configure(settings);
            return settings;
        }

        var correctVerified = await ProduceReceived(Settings());
        correctVerified.ShouldBe(expectedVerified);

        var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
        received.ShouldEndWith($".{Namer.RuntimeAndVersion}.received.txt");

        harness.PublishMaps().ShouldBeGreaterThan(0);
        var snapshot = harness.FindSingle();

        System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(correctVerified);
        snapshot.IsRerouted.ShouldBe(correctVerified != received.Replace(".received.", ".verified."));

        harness.Accept(snapshot).ShouldBeTrue();

        // The accept landed where Verify expects, so the next run passes.
        (await Verifies(Settings())).ShouldBeTrue();
    }

    private static string ParseVerifiedFileName(string message)
    {
        const string marker = "Verified:";
        foreach (var line in message.Split('\n'))
        {
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                var path = line[(index + marker.Length)..].Trim();
                return System.IO.Path.GetFileName(path);
            }
        }

        throw new InvalidOperationException($"No 'Verified:' line found in Verify exception message:\n{message}");
    }
}
