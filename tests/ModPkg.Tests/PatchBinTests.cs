using Jade.Ritobin;
using Xunit;

public sealed class PatchBinTests
{
    // PTCH v1, no deletions, PROP v3, no dependencies/objects/property patches.
    private static readonly byte[] EmptyPatch = Convert.FromHexString(
        "50544348010000000000000050524f5003000000000000000000000000000000");

    [Fact]
    public void FixerPreservesTheNativePatchHeaderExactly()
    {
        var bin = new BinReader(EmptyPatch).Read();
        Assert.Equal(EmptyPatch, new BinWriter().Write(bin));
    }

    [Theory]
    [InlineData("50524f50030000000000000000000000")]
    [InlineData("50544348010000000000000050524f50020000000000000000000000")]
    [InlineData("50544348010000000000000050524f500300000000000000000000000100000011000000080000000a0100580000803f")]
    public void OrdinaryBinsAndPatchRecordsRoundTrip(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        Assert.Equal(bytes, new BinWriter().Write(new BinReader(bytes).Read()));
    }

    [Fact]
    public void PatchObjectDeletionsArePreserved()
    {
        byte[] bytes = Convert.FromHexString(
            "505443480100000001000000efbeadde50524f5003000000000000000000000000000000");
        var bin = new BinReader(bytes).Read();
        var deleted = Assert.IsType<BinList>(bin.Sections["deleted"]);
        Assert.Equal(0xdeadbeefu, Assert.IsType<BinHash>(Assert.Single(deleted.Items)).Value.Hash);
        Assert.Equal(bytes, new BinWriter().Write(bin));
    }

    [Fact]
    public void InvalidPatchVersionIsRejected()
    {
        byte[] broken = (byte[])EmptyPatch.Clone();
        broken[4] = 0;
        Assert.Throws<InvalidDataException>(() => new BinReader(broken).Read());
    }

    [Fact]
    public void OptionalLocalHudPatchesSurviveFixerRoundTrip()
    {
        string folder = Environment.GetEnvironmentVariable("CSLOL_TEST_PTCH_DIR");
        if (string.IsNullOrEmpty(folder)) return;
        int patches = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            byte[] bytes = File.ReadAllBytes(file);
            if (!bytes.AsSpan().StartsWith("PTCH"u8)) continue;
            Assert.Equal(bytes, new BinWriter().Write(new BinReader(bytes).Read()));
            patches++;
        }
        Assert.True(patches > 0, "The optional fixture must contain PTCH files.");
    }
}
