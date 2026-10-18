using ModPkgLibSpace;
using Xunit;

public sealed class ProfileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cslol-profile-" + Guid.NewGuid());
    public ProfileTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void InstalledUnselectedModsDoNotChangeBackendOrSelection()
    {
        string selected = Path.Combine(root, "skin");
        Directory.CreateDirectory(selected);
        GameDataMods.SaveDeclarations(Path.Combine(root, "disabled-hud"), [new("base", 0) { GameData = [0x80] }]);
        var paths = ProfileOverlay.SelectedPaths(root, ["skin"]);
        Assert.Equal([selected], paths);
        Assert.DoesNotContain(paths, GameDataMods.HasDeclarations);
    }

    [Fact]
    public void RawOnlyModsHaveSelectableContent()
    {
        Directory.CreateDirectory(Path.Combine(root, "RAW/assets"));
        File.WriteAllText(Path.Combine(root, "RAW/assets/texture"), "test");
        Assert.True(ProfileOverlay.HasRawAssets(root));
    }

    [Fact]
    public void PublishingOrdinaryOverlayRemovesPreviousDeltaWads()
    {
        Directory.CreateDirectory(Path.Combine(root, "overlay/DATA/FINAL"));
        File.WriteAllText(Path.Combine(root, "overlay/DATA/FINAL/UI.wad.client"), "old hud");
        string state = Path.Combine(root, "mod-tools");
        string staging = Path.Combine(state, "staging");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "skin.wad.client"), "new skin");
        ProfileOverlay.Publish(root, state, staging);
        Assert.False(File.Exists(Path.Combine(root, "overlay/DATA/FINAL/UI.wad.client")));
        Assert.Equal("new skin", File.ReadAllText(Path.Combine(root, "overlay/skin.wad.client")));
    }

    [Theory]
    [InlineData(false, false, "test")]
    [InlineData(true, false, "test‗TFT")]
    [InlineData(false, true, "test‗PBE‗profile")]
    [InlineData(true, true, "test‗TFT‗PBE‗profile")]
    public void BuilderAndInjectorShareTheProfilePath(bool tft, bool pbe, string expected)
    {
        string game = pbe ? @"C:\League (PBE)\Game\League of Legends.exe" : @"C:\League\Game\League of Legends.exe";
        Assert.Equal(Path.Combine(root, "profiles", expected), ProfileOverlay.ProfilePath(root, "test", tft, game));
    }

    [Fact]
    public async Task FailedNativeBuildDoesNotReplacePreviousOverlay()
    {
        string helper = Environment.GetEnvironmentVariable("CSLOL_TEST_MOD_TOOLS");
        if (string.IsNullOrEmpty(helper)) return;
        Directory.CreateDirectory(Path.Combine(root, "installed/skin"));
        string profile = Path.Combine(root, "profile");
        Directory.CreateDirectory(Path.Combine(profile, "overlay"));
        string previous = Path.Combine(profile, "overlay/old.wad.client");
        File.WriteAllText(previous, "keep");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileOverlay.BuildNativeAsync(
            helper, Path.Combine(root, "missing-game"), profile, Path.Combine(root, "installed"),
            ["skin"], false, _ => { }, CancellationToken.None));
        Assert.Equal("keep", File.ReadAllText(previous));
        Assert.NotEmpty(File.ReadAllText(Path.Combine(profile, "mod-tools/stderr.txt")));
    }
}
