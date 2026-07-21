namespace Verify.Terminal.IntegrationTests;

// Covers the UniqueFor* naming axis. In a multi-targeted project the received file always gets the
// runtime and version; the verified file gets whatever UniqueFor* the test asked for.
public class UniquenessNamingTests : IntegrationTestBase
{
    [Fact]
    public Task Plain_ExistingVerified_IsDetected() =>
        // received `N.Plain.{RaV}` -> verified `N.Plain`
        AssertExistingVerifiedIsDetected(
            "Plain",
            _ => { },
            "N.Plain.verified.txt");

    [Fact]
    public Task UniqueForRuntime_ExistingVerified_IsDetected() =>
        // received `N.UniqueForRuntime.{RaV}` -> verified `N.UniqueForRuntime.{Runtime}`
        AssertExistingVerifiedIsDetected(
            "UniqueForRuntime",
            _ => _.UniqueForRuntime(),
            $"N.UniqueForRuntime.{Namer.Runtime}.verified.txt");

    [Fact]
    public Task UniqueForRuntimeAndVersion_ExistingVerified_IsDetected() =>
        // received and verified are identical: `N.UniqueForRuntimeAndVersion.{RaV}`
        AssertExistingVerifiedIsDetected(
            "UniqueForRuntimeAndVersion",
            _ => _.UniqueForRuntimeAndVersion(),
            $"N.UniqueForRuntimeAndVersion.{Namer.RuntimeAndVersion}.verified.txt");

    [Fact]
    public Task UniqueForArchitecture_ExistingVerified_IsDetected() =>
        // received `N.UniqueForArchitecture.{Arch}.{RaV}` -> verified `N.UniqueForArchitecture.{Arch}`
        AssertExistingVerifiedIsDetected(
            "UniqueForArchitecture",
            _ => _.UniqueForArchitecture(),
            $"N.UniqueForArchitecture.{Namer.Architecture}.verified.txt");

    [Fact]
    public Task UniqueForOSPlatform_ExistingVerified_IsDetected() =>
        AssertExistingVerifiedIsDetected(
            "UniqueForOSPlatform",
            _ => _.UniqueForOSPlatform(),
            $"N.UniqueForOSPlatform.{Namer.OperatingSystemPlatform}.verified.txt");

    [Fact]
    public Task UniqueForAssemblyConfiguration_ExistingVerified_IsDetected() =>
        AssertExistingVerifiedIsDetected(
            "UniqueForAssemblyConfiguration",
            _ => _.UniqueForAssemblyConfiguration(),
            $"N.UniqueForAssemblyConfiguration.{AssemblyConfiguration()}.verified.txt");

    [Fact]
    public Task IgnoreParametersForVerified_ExistingVerified_IsDetected() =>
        // received keeps the parameter text, verified drops it: `N.IgnoreAll_p.{RaV}` -> `N.IgnoreAll`
        AssertExistingVerifiedIsDetected(
            "IgnoreAll",
            _ =>
            {
                _.UseTextForParameters("p");
                _.IgnoreParametersForVerified();
            },
            "N.IgnoreAll.verified.txt");

    [Fact]
    public Task Plain_NewSnapshot_IsAKnownGap() =>
        // With no verified file to pair against, the finder keeps the runtime suffix, but the correct
        // verified name has none. TODO: needs the received->verified mapping from VerifyTests/Verify#1809.
        AssertNewSnapshot(
            "Plain",
            _ => { },
            "N.Plain.verified.txt",
            expectRoundTrips: false);

    [Fact]
    public Task UniqueForRuntime_NewSnapshot_IsAKnownGap() =>
        // The finder cannot know to collapse the received `{RaV}` down to the verified `{Runtime}`.
        // TODO: needs the received->verified mapping from VerifyTests/Verify#1809.
        AssertNewSnapshot(
            "UniqueForRuntime",
            _ => _.UniqueForRuntime(),
            $"N.UniqueForRuntime.{Namer.Runtime}.verified.txt",
            expectRoundTrips: false);

    [Fact]
    public Task UniqueForRuntimeAndVersion_NewSnapshot_Succeeds() =>
        // The only new-snapshot case that works: the received-derived name already equals the correct
        // verified name, because UniqueForRuntimeAndVersion keeps the runtime and version on both.
        AssertNewSnapshot(
            "UniqueForRuntimeAndVersion",
            _ => _.UniqueForRuntimeAndVersion(),
            $"N.UniqueForRuntimeAndVersion.{Namer.RuntimeAndVersion}.verified.txt",
            expectRoundTrips: true);
}
