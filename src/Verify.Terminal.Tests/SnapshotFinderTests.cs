namespace Verify.Terminal.Tests;

public sealed class SnapshotFinderTests
{
    [Fact]
    public void Should_Return_Expected_Snapshot()
    {
        var result = Find(
            "/Working/lol.received.txt",
            "/Working/lol.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeFalse();
        result.Received.FullPath.ShouldBe("/Working/lol.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.verified.txt");
    }

    [Fact]
    public void Should_Return_Expected_Snapshot_For_Non_Framework_Specific_File()
    {
        var result = Find(
            "/Working/lol.DotNet6_0.received.txt",
            "/Working/lol.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/lol.DotNet6_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.verified.txt");
    }

    [Fact]
    public void Should_Return_Expected_Snapshot_For_Framework_Specific_File()
    {
        var result = Find(
            "/Working/lol.DotNet6_0.received.txt",
            "/Working/lol.DotNet6_0.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeFalse();
        result.Received.FullPath.ShouldBe("/Working/lol.DotNet6_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.DotNet6_0.verified.txt");
    }

    [Fact]
    public void Should_Return_Expected_Snapshot_For_Runtime_Specific_File()
    {
        var result = Find(
            "/Working/lol.DotNet6_0.received.txt",
            "/Working/lol.DotNet.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/lol.DotNet6_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.DotNet.verified.txt");
    }

    [Fact]
    public void Should_Return_Expected_Snapshot_For_Net_Framework_Runtime_Specific_File()
    {
        var result = Find(
            "/Working/lol.Net4_8.received.txt",
            "/Working/lol.Net.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/lol.Net4_8.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.Net.verified.txt");
    }

    [Fact]
    public void Should_Return_Expected_Snapshot_For_Runtime_Specific_File_Targeting_A_Single_Framework()
    {
        var result = Find(
            "/Working/lol.received.txt",
            "/Working/lol.DotNet.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/lol.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.DotNet.verified.txt");
    }

    [Fact]
    public void Should_Prefer_Framework_Specific_File_Over_Runtime_Specific_File()
    {
        var result = Find(
            "/Working/lol.DotNet6_0.received.txt",
            "/Working/lol.DotNet6_0.verified.txt",
            "/Working/lol.DotNet.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeFalse();
        result.Received.FullPath.ShouldBe("/Working/lol.DotNet6_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.DotNet6_0.verified.txt");
    }

    [Fact]
    public void Should_Prefer_Runtime_Specific_File_Over_Non_Framework_Specific_File()
    {
        var result = Find(
            "/Working/lol.DotNet6_0.received.txt",
            "/Working/lol.DotNet.verified.txt",
            "/Working/lol.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/lol.DotNet6_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.DotNet.verified.txt");
    }

    [Fact]
    public void Should_Not_Reroute_Snapshot_That_Only_Looks_Like_A_Runtime()
    {
        var result = Find(
            "/Working/lol.Networking.received.txt",
            "/Working/lol.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeFalse();
        result.Received.FullPath.ShouldBe("/Working/lol.Networking.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/lol.Networking.verified.txt");
    }

    [Fact]
    public void Should_Return_Verified_When_All_Parameters_Ignored()
    {
        // IgnoreParametersForVerified drops all parameters from the verified name.
        var result = Find(
            "/Working/Foo_a=1_b=2.received.txt",
            "/Working/Foo.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/Foo_a=1_b=2.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo.verified.txt");
    }

    [Fact]
    public void Should_Return_Verified_When_Trailing_Parameter_Ignored()
    {
        // IgnoreParameters("b") drops a trailing parameter from the verified name.
        var result = Find(
            "/Working/Foo_a=1_b=2.received.txt",
            "/Working/Foo_a=1.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/Foo_a=1_b=2.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo_a=1.verified.txt");
    }

    [Fact]
    public void Should_Prefer_More_Specific_Parameter_Match()
    {
        var result = Find(
            "/Working/Foo_a=1_b=2.received.txt",
            "/Working/Foo_a=1.verified.txt",
            "/Working/Foo.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/Foo_a=1_b=2.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo_a=1.verified.txt");
    }

    [Fact]
    public void Should_Return_Verified_When_Parameters_Ignored_And_Multi_Targeting()
    {
        var result = Find(
            "/Working/Foo_a=1.DotNet11_0.received.txt",
            "/Working/Foo.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/Foo_a=1.DotNet11_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo.verified.txt");
    }

    [Fact]
    public void Should_Return_Runtime_Verified_When_Parameters_Ignored_And_Multi_Targeting()
    {
        var result = Find(
            "/Working/Foo_a=1.DotNet11_0.received.txt",
            "/Working/Foo.DotNet.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/Foo_a=1.DotNet11_0.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo.DotNet.verified.txt");
    }

    [Fact]
    public void Should_Not_Match_Different_Test_With_Shared_Prefix()
    {
        // `Foo` is a string prefix of `FooBar` but not a parameter-boundary reduction of it, so it
        // must not be rerouted to the unrelated `Foo` snapshot.
        var result = Find(
            "/Working/FooBar_a=1.received.txt",
            "/Working/Foo.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeFalse();
        result.Received.FullPath.ShouldBe("/Working/FooBar_a=1.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/FooBar_a=1.verified.txt");
    }

    [Fact]
    public void Should_Reroute_Multi_Target_Indexed_File()
    {
        var result = Find(
            "/Working/Foo.DotNet11_0#00.received.txt",
            "/Working/Foo#00.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeTrue();
        result.Received.FullPath.ShouldBe("/Working/Foo.DotNet11_0#00.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo#00.verified.txt");
    }

    [Fact]
    public void Should_Not_Cross_Match_Indexed_Files()
    {
        // The `#index` differs, so these are different targets and must not be paired.
        var result = Find(
            "/Working/Foo.DotNet11_0#00.received.txt",
            "/Working/Foo#01.verified.txt");

        result.ShouldNotBeNull();
        result.IsRerouted.ShouldBeFalse();
        result.Received.FullPath.ShouldBe("/Working/Foo.DotNet11_0#00.received.txt");
        result.Verified.FullPath.ShouldBe("/Working/Foo.DotNet11_0#00.verified.txt");
    }

    [Fact]
    public void Should_Ignore_The_Inline_Staging_Directory()
    {
        // Verify stages the received text of an inline snapshot under obj, named like any other
        // received file. The snapshot it belongs to lives in a source file, so accepting it here
        // would rename it to a verified file nothing reads and leave the real snapshot pending.
        Find(
            "/Working/obj/VerifyInline/N.Sample.a1b2c3d4.DotNet10_0.received.txt",
            "/Working/obj/VerifyInline/N.Sample.a1b2c3d4.DotNet10_0.expected.txt")
            .ShouldBeNull();
    }

    private static Snapshot? Find(params string[] files)
    {
        var environment = new FakeEnvironment(PlatformFamily.Linux);
        var filesystem = new FakeFileSystem(environment);
        var globber = new Globber(filesystem, environment);

        foreach (var file in files)
        {
            filesystem.CreateFile(file);
        }

        var finder = new SnapshotFinder(globber, environment);
        return finder.Find().SingleOrDefault();
    }
}
