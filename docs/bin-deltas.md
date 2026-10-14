# BIN delta mod packages

Some `.modpkg` mods contain textures/fonts plus instructions to edit the installed
game's BIN files. Extracting only the assets leaves those mods incomplete. Import
now preserves `layers[].game_data` and layer build resources, and applies them
when building a profile.

**Reimport affected packages installed by an older version.** Their declarations
were discarded at import; the original package is needed to recover them.

## Loading and export

- Profiles without active declarations still use `mod-tools.exe`.
- Profiles with declarations use `game-data-overlay.exe`, a small Rust wrapper
  around the public [LeagueToolkit libraries](https://github.com/LeagueToolkit/league-mod).
  It builds the complete selected profile so delta and ordinary asset mods compose
  together. The last selected mod wins, matching `mod-tools --ignoreConflict`.
- The base layer is always active. When `META/details.json` is present, other
  layers follow its existing `Layers[].is_active` flags. No new layer-selection
  UI is introduced. Without that manager file, all declared layers are included.
- Both unpacked and packed WADs and `RAW` assets are supported. TFT map filtering
  remains available. Cancellation terminates the helper before injection begins.
- Declarations are applied to the current game files at build time. Native class
  shapes and game/object indices are cached under the installation fingerprint,
  and rebuilt after a game change. The first delta build can take longer while
  it indexes native BINs.
- A staged overlay replaces the active overlay only after the build passes its
  structural checks. Missing targets, invalid declarations, and failed object
  edits fail the build. Skipped property edits produce visible log warnings;
  detailed diagnostics are in `profiles/<profile>/game-data/diagnostics.json`.

The manager stores original declaration MessagePack values as base64 in
`META/game-data.json`. This preserves numeric widths and unrecognized declaration
fields during export. No-WAD layer resources are kept under
`META/game-data/<WAD-or-WAD_layer>/`. A `game_data.yaml`, `.yml`, `.json`, or `.toml`
manifest there takes precedence over legacy embedded declarations. Resources are
not installed as game assets.

Use `.modpkg` when redistributing a delta mod. It exports the declarations back
into standard package metadata and resources back into their original layer.
The manager's private META sidecar is also retained by its Fantome round-trip,
but other managers need not understand that sidecar.

## Building

Install .NET 10 SDK and the stable Rust MSVC toolchain (including its C++ build
tools). The solution's launcher also targets .NET 8. `ModLoader.csproj` builds
the helper with Cargo and includes it in build/publish output automatically:

```powershell
dotnet build ModLoader.sln -c Release
dotnet publish ModLoader/ModLoader.csproj -c Release -o publish
```

`-p:SkipGameDataBuild=true` skips Cargo only when a helper already exists at
`game-data-overlay/target/release/game-data-overlay.exe`. CI builds and packages
the helper from source. No private Celestial source or binaries are included.

## Validation

```powershell
dotnet test tests/ModPkg.Tests/ModPkg.Tests.csproj -c Release
cargo test --locked --manifest-path game-data-overlay/Cargo.toml
cargo clippy --locked --manifest-path game-data-overlay/Cargo.toml --all-targets -- -D warnings
```

Tests use synthetic BIN/WAD/package fixtures, covering import/export preservation,
declaration-only mods, manifests, resource containment, active layers, mixed mods,
priority order, in-place edits, game patch changes, removal, and failed builds.
Real packages can be included in the round-trip test without committing them:

```powershell
$env:CSLOL_TEST_MODPKGS = 'C:\mods\first.modpkg|C:\mods\second.modpkg'
dotnet test tests/ModPkg.Tests/ModPkg.Tests.csproj -c Release
```

Before release, test loading a delta HUD alongside an ordinary skin in Practice
Tool, stop/restart the loader, change the selected mods, and verify the normal
mod-tools path after disabling the delta HUD. Automated overlay builds do not
replace an in-game test.
