namespace Verify.Terminal.IntegrationTests;

// Covers the parameter naming axis. These need real method parameters (so Verify appends `_name=value`),
// which requires a [Theory]. The received file keeps all parameters; the verified file drops the ignored
// ones. The finder can undo a dropped *trailing* parameter, but not a dropped leading/middle one.
public class ParameterNamingTests : IntegrationTestBase
{
    [Theory]
    [InlineData("1", "2")]
    public Task Parameters_ExistingVerified_IsDetected(string a, string b) =>
        // Nothing ignored, so the verified name keeps both parameters.
        AssertParametersAreDetected(
            nameof(Parameters_ExistingVerified_IsDetected),
            a,
            b,
            ignored: null,
            expectedVerified: "N.Params_a=1_b=2.verified.txt");

    [Theory]
    [InlineData("1", "2")]
    public Task IgnoreTrailingParameter_ExistingVerified_IsDetected(string a, string b) =>
        // The trailing parameter `b` is dropped from the verified name, which the finder can undo.
        AssertParametersAreDetected(
            nameof(IgnoreTrailingParameter_ExistingVerified_IsDetected),
            a,
            b,
            ignored: "b",
            expectedVerified: "N.Params_a=1.verified.txt");

    // Run against both the map, which is how Verify behaves now, and the fallback, which still applies
    // to an older Verify or a build server. Looped rather than another [InlineData], since an extra
    // test method parameter would be appended to the snapshot name by Verify.
    async Task AssertParametersAreDetected(string name, string a, string b, string ignored, string expectedVerified)
    {
        await Run(withMap: true);
        await Run(withMap: false);

        return;

        async Task Run(bool withMap)
        {
            using var harness = new Harness(name);

            VerifySettings Settings()
            {
                var settings = harness.CreateSettings();
                settings.UseTypeName("N");
                settings.UseMethodName("Params");
                settings.UseParameters(a, b);
                if (ignored != null)
                {
                    settings.IgnoreParameters(ignored);
                }

                return settings;
            }

            var because = withMap ? "with map" : "without map";

            var correctVerified = await ProduceReceived(Settings());
            correctVerified.ShouldBe(expectedVerified, because);

            var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
            received.ShouldBe($"N.Params_a=1_b=2.{Namer.RuntimeAndVersion}.received.txt", because);

            harness.SeedVerified(correctVerified, "old-verified");
            if (withMap)
            {
                // Both paths reach the same file here, so assert the map really was published.
                harness.PublishMaps().ShouldBeGreaterThan(0);
            }

            var snapshot = harness.FindSingle();
            System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(correctVerified, because);
            snapshot.IsRerouted.ShouldBeTrue(because);

            harness.Accept(snapshot).ShouldBeTrue(because);
            (await Verifies(Settings())).ShouldBeTrue(because);
        }
    }

    [Theory]
    [InlineData("1", "2")]
    public async Task IgnoreLeadingParameter_WithMap_IsPlaced(string a, string b)
    {
        using var harness = new Harness(nameof(IgnoreLeadingParameter_WithMap_IsPlaced));

        VerifySettings Settings()
        {
            var settings = harness.CreateSettings();
            settings.UseTypeName("N");
            settings.UseMethodName("Params");
            settings.UseParameters(a, b);
            settings.IgnoreParameters("a");
            return settings;
        }

        var correctVerified = await ProduceReceived(Settings());
        correctVerified.ShouldBe("N.Params_b=2.verified.txt");

        harness.SeedVerified(correctVerified, "old-verified");
        harness.PublishMaps().ShouldBeGreaterThan(0);
        var snapshot = harness.FindSingle();

        // The map names the verified file, so the leading ignored parameter no longer matters.
        System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(correctVerified);
        snapshot.IsRerouted.ShouldBeTrue();

        harness.Accept(snapshot).ShouldBeTrue();
        (await Verifies(Settings())).ShouldBeTrue();
    }

    [Theory]
    [InlineData("1", "2")]
    public async Task IgnoreLeadingParameter_WithoutMap_CannotBePaired(string a, string b)
    {
        using var harness = new Harness(nameof(IgnoreLeadingParameter_WithoutMap_CannotBePaired));

        VerifySettings Settings()
        {
            var settings = harness.CreateSettings();
            settings.UseTypeName("N");
            settings.UseMethodName("Params");
            settings.UseParameters(a, b);
            settings.IgnoreParameters("a");
            return settings;
        }

        var correctVerified = await ProduceReceived(Settings());
        // The leading parameter `a` is dropped, so the verified name is not a prefix of the received name.
        correctVerified.ShouldBe("N.Params_b=2.verified.txt");

        var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
        received.ShouldBe($"N.Params_a=1_b=2.{Namer.RuntimeAndVersion}.received.txt");

        // The correct verified file exists, but the finder cannot reduce a non-trailing parameter and
        // falls back to the received-derived name.
        harness.SeedVerified(correctVerified, "old-verified");
        var snapshot = harness.FindSingle();
        var literal = received.Replace(".received.", ".verified.");
        System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(literal);
        snapshot.IsRerouted.ShouldBeFalse();

        harness.Accept(snapshot).ShouldBeTrue();

        // A non-trailing ignored parameter cannot be reconstructed from the received name, so the
        // accept lands at the wrong verified file and Verify still fails. This is only reachable
        // without a map, ie. an older Verify or a build server. See the WithMap case above.
        (await Verifies(Settings())).ShouldBeFalse();
    }
}
