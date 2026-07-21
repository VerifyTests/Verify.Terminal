namespace Verify.Terminal.IntegrationTests;

// Covers the parameter naming axis. These need real method parameters (so Verify appends `_name=value`),
// which requires a [Theory]. The received file keeps all parameters; the verified file drops the ignored
// ones. The finder can undo a dropped *trailing* parameter, but not a dropped leading/middle one.
public class ParameterNamingTests : IntegrationTestBase
{
    [Theory]
    [InlineData("1", "2")]
    public async Task Parameters_ExistingVerified_IsDetected(string a, string b)
    {
        using var harness = new Harness(nameof(Parameters_ExistingVerified_IsDetected));

        VerifySettings Settings()
        {
            var settings = harness.CreateSettings();
            settings.UseTypeName("N");
            settings.UseMethodName("Params");
            settings.UseParameters(a, b);
            return settings;
        }

        var correctVerified = await ProduceReceived(Settings());
        correctVerified.ShouldBe("N.Params_a=1_b=2.verified.txt");

        var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
        received.ShouldBe($"N.Params_a=1_b=2.{Namer.RuntimeAndVersion}.received.txt");

        harness.SeedVerified(correctVerified, "old-verified");
        var snapshot = harness.FindSingle();
        System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(correctVerified);
        snapshot.IsRerouted.ShouldBeTrue();

        harness.Accept(snapshot).ShouldBeTrue();
        (await Verifies(Settings())).ShouldBeTrue();
    }

    [Theory]
    [InlineData("1", "2")]
    public async Task IgnoreTrailingParameter_ExistingVerified_IsDetected(string a, string b)
    {
        using var harness = new Harness(nameof(IgnoreTrailingParameter_ExistingVerified_IsDetected));

        VerifySettings Settings()
        {
            var settings = harness.CreateSettings();
            settings.UseTypeName("N");
            settings.UseMethodName("Params");
            settings.UseParameters(a, b);
            settings.IgnoreParameters("b");
            return settings;
        }

        var correctVerified = await ProduceReceived(Settings());
        // The trailing parameter `b` is dropped from the verified name.
        correctVerified.ShouldBe("N.Params_a=1.verified.txt");

        var received = harness.ReceivedFileNames().ShouldHaveSingleItem();
        received.ShouldBe($"N.Params_a=1_b=2.{Namer.RuntimeAndVersion}.received.txt");

        harness.SeedVerified(correctVerified, "old-verified");
        var snapshot = harness.FindSingle();
        System.IO.Path.GetFileName(snapshot.Verified.FullPath).ShouldBe(correctVerified);
        snapshot.IsRerouted.ShouldBeTrue();

        harness.Accept(snapshot).ShouldBeTrue();
        (await Verifies(Settings())).ShouldBeTrue();
    }

    [Theory]
    [InlineData("1", "2")]
    public async Task IgnoreLeadingParameter_ExistingVerified_IsAKnownGap(string a, string b)
    {
        using var harness = new Harness(nameof(IgnoreLeadingParameter_ExistingVerified_IsAKnownGap));

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

        // TODO: gap — a non-trailing ignored parameter cannot be reconstructed from the received name,
        // so the accept lands at the wrong verified file and Verify still fails. The received->verified
        // mapping proposed in VerifyTests/Verify#1809 would resolve this.
        (await Verifies(Settings())).ShouldBeFalse();
    }
}
