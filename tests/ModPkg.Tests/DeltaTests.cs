using ModPkgLibSpace;
using Xunit;

public sealed class DeltaTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cslol-delta-tests-" + Guid.NewGuid());
    public DeltaTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void DeclarationsAndBuildResourcesSurviveImportExport()
    {
        // { version:1, modules:[], numbers:[f32(1), f64(1), u64::MAX] }
        byte[] declaration = Convert.FromHexString("83a776657273696f6e01a76d6f64756c657390a76e756d6265727393ca3f800000cb3ff0000000000000cfffffffffffffffff");
        string package = Path.Combine(root, "source.modpkg");
        using (var writer = new ModpkgWriter(package))
        {
            writer.AddLayer("base", 0);
            writer.AddLayer("alternate", 9);
            writer.SetMetadata(new ModpkgMetadata
            {
                Name = "delta-test", SchemaVersion = 4,
                Layers = [new("base", 0) { GameData = declaration }, new("alternate", 9) { GameData = declaration }]
            });
            writer.AddChunk("data/example.bin", [1, 2, 3], "base", "UI.wad.client");
            writer.AddChunk("patches/change.ptch", [4, 5, 6], "alternate");
            writer.Write();
        }
        RoundTrip(package);
        Assert.Equal(declaration, ModPkgLib.GetMetadata(package).Metadata.Layers[0].GameData);
    }

    [Fact]
    public void DeclarationOnlyLayerIsRetainedAndDetected()
    {
        string mod = Path.Combine(root, "installed");
        GameDataMods.SaveDeclarations(mod, [new("base", 0) { GameData = [0x80] }]);
        Assert.True(GameDataMods.HasDeclarations(mod));
        Assert.Empty(GameDataMods.Resources(mod, "base"));
    }

    [Fact]
    public void ManifestOnlyPackageIsDetected()
    {
        string mod = Path.Combine(root, "manifest");
        GameDataMods.SaveDeclarations(mod, [new("base", 0)]);
        string path = GameDataMods.ResourcePath(mod, "base", "game_data.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "version: 1\nmodules: []\n");
        Assert.True(GameDataMods.HasDeclarations(mod));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("../../outside")]
    [InlineData("C:/outside")]
    public void ResourcesCannotEscapeLayer(string path) =>
        Assert.Throws<InvalidDataException>(() => GameDataMods.ResourcePath(root, "base", path));

    [Fact]
    public void OrdinaryModDoesNotUseDeltaBuilder() => Assert.False(GameDataMods.HasDeclarations(root));

    [Fact]
    public void InactiveLayerDoesNotSwitchTheOverlayBackend()
    {
        GameDataMods.SaveDeclarations(root, [new("alternate", 5) { GameData = [0x80] }]);
        string details = Path.Combine(root, "META", "details.json");
        File.WriteAllText(details, "{\"Layers\":[{\"Name\":\"alternate\",\"is_active\":false}]}");
        Assert.False(GameDataMods.HasDeclarations(root));
        File.WriteAllText(details, "{\"Layers\":[{\"Name\":\"alternate\",\"is_active\":true}]}");
        Assert.True(GameDataMods.HasDeclarations(root));
    }

    [Fact]
    public void ReimportDoesNotRetainADeletedManifest()
    {
        GameDataMods.SaveDeclarations(root, [new("base", 0)]);
        string manifest = GameDataMods.ResourcePath(root, "base", "game_data.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, "version: 1\nmodules: []\n");
        Assert.True(GameDataMods.HasDeclarations(root));
        GameDataMods.SaveDeclarations(root, [new("base", 0)]);
        Assert.False(GameDataMods.HasDeclarations(root));
    }

    [Fact]
    public void OptionalLocalPackagesRoundTripWithoutLosingGameData()
    {
        // Private/local assets are not checked into this repository.
        foreach (string package in (Environment.GetEnvironmentVariable("CSLOL_TEST_MODPKGS") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries))
            RoundTrip(package);
    }

    private void RoundTrip(string package)
    {
        string mod = Path.Combine(root, Guid.NewGuid().ToString());
        var info = ModPkgLib.GetMetadata(package);
        ModPkgLib.Extract(package, mod);
        string repacked = Path.Combine(root, Guid.NewGuid() + ".modpkg");
        ModPkgLib.Pack(info.Layers.Select(l => (Path.Combine(mod, l.folder_name), l.Name, l.Priority)).ToList(),
            new ModpkgMetadata { Name = "roundtrip" }, repacked);
        using var before = new ModpkgReader(package);
        using var after = new ModpkgReader(repacked);
        foreach (var layer in before.Metadata.Layers)
            Assert.Equal(layer.GameData, after.Metadata.Layers.Single(l => l.Name == layer.Name).GameData);
        foreach (var chunk in before.Chunks.Where(c => c.LayerIndex != Constants.NO_LAYER_INDEX))
        {
            var counterpart = Assert.Single(after.Chunks, c => c.LayerHash == chunk.LayerHash && c.PathHash == chunk.PathHash);
            Assert.Equal(before.ExtractChunk(chunk), after.ExtractChunk(counterpart));
            Assert.Equal(chunk.WadIndex == Constants.NO_WAD_INDEX, counterpart.WadIndex == Constants.NO_WAD_INDEX);
        }
        Assert.Equal(before.Chunks.Count(c => c.LayerIndex != Constants.NO_LAYER_INDEX), after.Chunks.Count(c => c.LayerIndex != Constants.NO_LAYER_INDEX));
        // Retain these for an opt-in end-to-end test against a real game installation.
        string destination = Environment.GetEnvironmentVariable("CSLOL_TEST_EXTRACT");
        if (!string.IsNullOrEmpty(destination) && Path.GetFileName(package) is "ZZZ HUD.modpkg" or "R6 HUD.modpkg")
            ModPkgLib.Extract(repacked, Path.Combine(destination, Path.GetFileNameWithoutExtension(package)));
    }
}
