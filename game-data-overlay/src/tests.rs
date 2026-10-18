use super::*;
use base64::{Engine, prelude::BASE64_STANDARD};
use ltk_game_data::{DeclarationDocument, load_declarations};
use ltk_meta::{BinObject, property::values};
use ltk_overlay::ModContentProvider;
use ltk_wad::{WadBuilder, WadChunkBuilder, WadChunkCompression};
use std::io::Write;

struct Fixture {
    _temp: tempfile::TempDir,
    root: Utf8PathBuf,
}
impl Fixture {
    fn new() -> Self {
        let temp = tempfile::tempdir().unwrap();
        let root = Utf8PathBuf::from_path_buf(temp.path().to_owned()).unwrap();
        let fixture = Self { _temp: temp, root };
        fixture.game(7);
        fixture
    }
    fn bin(extra: i32) -> Vec<u8> {
        let bin = Bin::new(
            [BinObject::builder(0x11u32, 0x22u32)
                .property(0x33u32, values::I32::new(1))
                .property(0x44u32, values::I32::new(extra))
                .build()],
            std::iter::empty::<&str>(),
        );
        let mut cursor = Cursor::new(Vec::new());
        bin.to_writer(&mut cursor).unwrap();
        cursor.into_inner()
    }
    fn game(&self, extra: i32) {
        write_wad(
            &self.root.join("Game/DATA/FINAL/UI.wad.client"),
            &[
                ("data/test.bin", &Self::bin(extra)),
                ("assets/texture", b"original"),
            ],
        );
    }
    fn mod_dir(&self, name: &str) -> Utf8PathBuf {
        let root = self.root.join(name);
        fs::create_dir_all(&root).unwrap();
        root
    }
    fn delta(&self, name: &str, value: i32) -> Utf8PathBuf {
        let root = self.mod_dir(name);
        fs::create_dir_all(root.join("META")).unwrap();
        let yaml = format!(
            "version: 1\nmodules:\n  - target: data/test.bin\n    '0x00000011':\n      '0x00000033': {value}\n"
        );
        let declarations = load_declarations("game_data.yaml", &yaml, |_| unreachable!()).unwrap();
        let document = DeclarationDocument::try_from(declarations).unwrap();
        let encoded = BASE64_STANDARD.encode(rmp_serde::to_vec_named(&document).unwrap());
        fs::write(
            root.join("META/game-data.json"),
            serde_json::to_vec(&serde_json::json!([
                {"Name":"base", "Priority":0, "GameData":encoded}
            ]))
            .unwrap(),
        )
        .unwrap();
        root
    }
    fn request(&self, mods: Vec<Utf8PathBuf>) -> Request {
        Request {
            game: self.root.join("Game"),
            output: self.root.join("profile/overlay"),
            state: self.root.join("profile/game-data"),
            mods,
            no_tft: true,
        }
    }
    fn bytes(&self, path: &str) -> Vec<u8> {
        read_chunk(
            &self.root.join("profile/overlay/DATA/FINAL/UI.wad.client"),
            path,
        )
    }
    fn value(&self, field: u32) -> i32 {
        let bin = Bin::from_reader(&mut Cursor::new(self.bytes("data/test.bin"))).unwrap();
        match &bin.objects[&ltk_game_data::BinHash(0x11)].properties[&ltk_game_data::BinHash(field)]
        {
            ltk_meta::PropertyValueEnum::I32(v) => v.value,
            _ => panic!("expected integer"),
        }
    }
}

fn write_wad(path: &Utf8PathBuf, chunks: &[(&str, &[u8])]) {
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    let mut builder = WadBuilder::default();
    for (name, _) in chunks {
        builder = builder.with_chunk(
            WadChunkBuilder::default()
                .with_path(*name)
                .with_force_compression(WadChunkCompression::None),
        );
    }
    let mut output = Cursor::new(Vec::new());
    builder
        .build_to_writer(&mut output, |hash, writer| {
            let (_, bytes) = chunks
                .iter()
                .find(|(name, _)| WadHash(ltk_game_data::path_hash(name)) == hash)
                .unwrap();
            writer.write_all(bytes)?;
            Ok(())
        })
        .unwrap();
    fs::write(path, output.into_inner()).unwrap();
}
fn read_chunk(path: &Utf8PathBuf, name: &str) -> Vec<u8> {
    let mut wad = Wad::mount(BufReader::new(fs::File::open(path).unwrap())).unwrap();
    let chunk = *wad
        .chunks()
        .get(WadHash(ltk_game_data::path_hash(name)))
        .unwrap();
    wad.load_chunk_decompressed(&chunk).unwrap().to_vec()
}

#[test]
fn delta_only_mod_edits_current_game_and_keeps_new_patch_fields() {
    let f = Fixture::new();
    let delta = f.delta("delta", 20);
    build(f.request(vec![delta.clone()])).unwrap();
    assert_eq!(f.value(0x33), 20);
    assert_eq!(f.value(0x44), 7);
    f.game(99);
    build(f.request(vec![delta])).unwrap();
    assert_eq!(f.value(0x33), 20);
    assert_eq!(f.value(0x44), 99);
}

#[test]
fn mixed_profiles_keep_assets_and_use_last_mod_wins() {
    let f = Fixture::new();
    let first = f.delta("first", 2);
    let second = f.delta("second", 3);
    let skin = f.mod_dir("skin");
    write_wad(
        &skin.join("WAD/UI.wad.client"),
        &[("assets/texture", b"skin")],
    );
    build(f.request(vec![first.clone(), skin.clone(), second.clone()])).unwrap();
    assert_eq!(f.value(0x33), 3);
    assert_eq!(f.bytes("assets/texture"), b"skin");
    build(f.request(vec![second, skin, first])).unwrap();
    assert_eq!(f.value(0x33), 2);
}

#[test]
fn installed_but_disabled_mods_cannot_affect_a_hud_build() {
    let f = Fixture::new();
    let hud = f.delta("installed/hud", 20);
    build(f.request(vec![hud.clone()])).unwrap();
    let before = f.bytes("data/test.bin");
    let disabled = f.delta("installed/disabled", 99);
    write_wad(
        &disabled.join("WAD/UI.wad.client"),
        &[("assets/texture", b"disabled")],
    );
    // A malformed disabled declaration must not even be parsed.
    fs::write(disabled.join("META/game-data.json"), b"invalid").unwrap();
    build(f.request(vec![hud])).unwrap();
    assert_eq!(f.bytes("data/test.bin"), before);
    assert_eq!(f.bytes("assets/texture"), b"original");
}

#[test]
fn corrupted_fixer_patch_is_rejected_before_publishing() {
    let f = Fixture::new();
    let hud = f.delta("hud", 20);
    build(f.request(vec![hud.clone()])).unwrap();
    let previous = f.bytes("data/test.bin");
    // Reproduce the old C# writer: eight extra zero bytes before the PTCH version.
    let mut broken = b"PTCH".to_vec();
    broken.extend_from_slice(&[0; 8]);
    broken.extend_from_slice(&1u32.to_le_bytes());
    broken.extend_from_slice(&0u32.to_le_bytes());
    broken.extend_from_slice(b"PROP");
    broken.extend_from_slice(&3u32.to_le_bytes());
    broken.extend_from_slice(&[0; 12]);
    for packed in [false, true] {
        let skin = f.mod_dir(if packed { "packed" } else { "loose" });
        if packed {
            write_wad(&skin.join("WAD/UI.wad.client"), &[("data/layout", &broken)]);
        } else {
            fs::create_dir_all(skin.join("WAD/UI.wad.client/data")).unwrap();
            fs::write(skin.join("WAD/UI.wad.client/data/layout"), &broken).unwrap();
        }
        let error = build(f.request(vec![hud.clone(), skin])).unwrap_err();
        assert!(format!("{error:#}").contains("Invalid PTCH asset"));
        assert_eq!(f.bytes("data/test.bin"), previous);
    }
}

#[test]
fn interrupted_staging_is_not_reused_against_completed_build_state() {
    let f = Fixture::new();
    let hud = f.delta("hud", 20);
    build(f.request(vec![hud.clone()])).unwrap();
    let previous = f.bytes("data/test.bin");
    let staging = f.root.join("profile/game-data/staging/DATA/FINAL");
    fs::create_dir_all(&staging).unwrap();
    fs::write(staging.join("UI.wad.client"), b"interrupted partial WAD").unwrap();
    build(f.request(vec![hud])).unwrap();
    assert_eq!(f.bytes("data/test.bin"), previous);
}

#[test]
fn disabling_a_previously_built_skin_removes_its_overrides() {
    let f = Fixture::new();
    let hud = f.delta("installed/hud", 20);
    let skin = f.mod_dir("installed/skin");
    write_wad(
        &skin.join("WAD/UI.wad.client"),
        &[("assets/texture", b"skin")],
    );
    build(f.request(vec![hud.clone(), skin])).unwrap();
    assert_eq!(f.bytes("assets/texture"), b"skin");
    build(f.request(vec![hud])).unwrap();
    assert_eq!(f.value(0x33), 20);
    assert_eq!(f.bytes("assets/texture"), b"original");
}

#[test]
fn failed_delta_keeps_previous_overlay_and_reports_error() {
    let f = Fixture::new();
    let delta = f.delta("delta", 8);
    build(f.request(vec![delta.clone()])).unwrap();
    let resources = delta.join("META/game-data/WAD");
    fs::create_dir_all(&resources).unwrap();
    fs::write(
        resources.join("game_data.yaml"),
        "version: 1\nmodules:\n  - target: missing.bin\n    links: [dependency.bin]\n",
    )
    .unwrap();
    assert!(build(f.request(vec![delta])).is_err());
    assert_eq!(f.value(0x33), 8);
}

#[test]
fn yaml_only_layers_and_declaration_changes_are_rebuilt() {
    let f = Fixture::new();
    let root = f.mod_dir("manifest");
    let resources = root.join("META/game-data/WAD");
    fs::create_dir_all(&resources).unwrap();
    fs::write(
        root.join("META/game-data.json"),
        r#"[{"Name":"base","Priority":0}]"#,
    )
    .unwrap();
    for value in [4, 5] {
        fs::write(resources.join("game_data.yaml"), format!("version: 1\nmodules:\n  - target: data/test.bin\n    '0x00000011':\n      '0x00000033': {value}\n")).unwrap();
        build(f.request(vec![root.clone()])).unwrap();
        assert_eq!(f.value(0x33), value);
    }
}

#[test]
fn packed_wads_raw_assets_and_windows_paths_are_read() {
    let f = Fixture::new();
    let root = f.mod_dir("assets");
    write_wad(&root.join("WAD/UI.wad"), &[("assets/texture", b"packed")]);
    fs::create_dir_all(root.join("RAW/assets")).unwrap();
    fs::write(root.join("RAW/assets/file"), b"raw").unwrap();
    let mut provider = InstalledMod::open(root.clone()).unwrap();
    assert_eq!(
        provider
            .read_wad_overrides("base", "UI.wad.client")
            .unwrap()[0]
            .1,
        b"packed"
    );
    assert_eq!(
        provider
            .read_raw_override_file(camino::Utf8Path::new("assets\\file"))
            .unwrap(),
        b"raw"
    );
    assert!(content::inside(&root, "../Game/DATA/FINAL/UI.wad.client").is_err());
}

#[test]
fn unsupported_declarations_are_not_silently_ignored() {
    let f = Fixture::new();
    let root = f.delta("delta", 1);
    fs::write(
        root.join("META/game-data.json"),
        r#"[{"Name":"base","Priority":0,"GameData":"bad"}]"#,
    )
    .unwrap();
    assert!(build(f.request(vec![root])).is_err());
    assert!(!f.root.join("profile/overlay").exists());
}

#[test]
fn omitted_use_alpha_has_only_its_verified_class_specific_type() {
    use ltk_game_data::{BinHash, PropertyKind, Schema, Shape};
    let mut schema = schema::ObservedSchema::default();
    let class = BinHash(0x1a65e950);
    let field = BinHash(0x8fde0428);
    assert_eq!(schema.fallback(class, field), None);
    schema.observe(&Bin::new(
        [BinObject::new(1, class)],
        std::iter::empty::<&str>(),
    ));
    assert_eq!(
        schema.fallback(class, field),
        Some(Shape::bare(PropertyKind::Bool))
    );
    assert_eq!(schema.fallback(BinHash(0x22), field), None);
}

#[test]
fn optional_untypable_property_is_reported_while_valid_edits_apply() {
    let f = Fixture::new();
    let root = f.delta("delta", 1);
    let resources = root.join("META/game-data/WAD");
    fs::create_dir_all(&resources).unwrap();
    fs::write(resources.join("game_data.yaml"), "version: 1\nmodules:\n  - target: data/test.bin\n    '0x00000011':\n      '0x00000033': 17\n      '0xdeadbeef': true\n").unwrap();
    build(f.request(vec![root])).unwrap();
    assert_eq!(f.value(0x33), 17);
    let diagnostics: Vec<ltk_overlay::game_data::GameDataDiagnostic> = serde_json::from_slice(
        &fs::read(f.root.join("profile/game-data/diagnostics.json")).unwrap(),
    )
    .unwrap();
    assert_eq!(
        diagnostics
            .iter()
            .filter(|d| matches!(d.kind, Kind::PropertyEditSkipped))
            .count(),
        1
    );
}

#[test]
fn layer_priorities_and_removal_do_not_leave_old_edits() {
    let f = Fixture::new();
    let root = f.delta("delta", 2);
    let extra = f.delta("extra", 9);
    let extra_layers: serde_json::Value =
        serde_json::from_slice(&fs::read(extra.join("META/game-data.json")).unwrap()).unwrap();
    let mut layers: serde_json::Value =
        serde_json::from_slice(&fs::read(root.join("META/game-data.json")).unwrap()).unwrap();
    let mut additional = extra_layers[0].clone();
    additional["Name"] = "alternate".into();
    additional["Priority"] = 100.into();
    layers.as_array_mut().unwrap().push(additional);
    fs::write(
        root.join("META/game-data.json"),
        serde_json::to_vec(&layers).unwrap(),
    )
    .unwrap();
    build(f.request(vec![root.clone()])).unwrap();
    assert_eq!(f.value(0x33), 9);
    layers.as_array_mut().unwrap().pop();
    fs::write(
        root.join("META/game-data.json"),
        serde_json::to_vec(&layers).unwrap(),
    )
    .unwrap();
    build(f.request(vec![root])).unwrap();
    assert_eq!(f.value(0x33), 2);
    build(f.request(vec![])).unwrap();
    assert!(
        !f.root
            .join("profile/overlay/DATA/FINAL/UI.wad.client")
            .exists()
    );
}

#[test]
fn installed_layer_selection_keeps_inactive_declarations_out() {
    let f = Fixture::new();
    let root = f.delta("delta", 2);
    let mut layers: serde_json::Value =
        serde_json::from_slice(&fs::read(root.join("META/game-data.json")).unwrap()).unwrap();
    layers
        .as_array_mut()
        .unwrap()
        .push(serde_json::json!({"Name":"inactive","Priority":100,"GameData":"invalid"}));
    fs::write(
        root.join("META/game-data.json"),
        serde_json::to_vec(&layers).unwrap(),
    )
    .unwrap();
    fs::write(
        root.join("META/details.json"),
        r#"{"Layers":[{"Name":"base","is_active":false},{"Name":"inactive","is_active":false}]}"#,
    )
    .unwrap();
    build(f.request(vec![root])).unwrap();
    assert_eq!(f.value(0x33), 2);
}

/// Opt-in integration check against local assets; no game files enter the repository.
#[test]
#[ignore = "requires CSLOL_GAME, CSLOL_INSTALLED, CSLOL_HUD, CSLOL_SKIN, CSLOL_MOD_TOOLS"]
fn real_profile_switching_preserves_skin_assets_and_removes_disabled_mods() {
    let env = |name| Utf8PathBuf::from(std::env::var(name).unwrap());
    let game = env("CSLOL_GAME");
    let installed = env("CSLOL_INSTALLED");
    let hud = std::env::var("CSLOL_HUD").unwrap();
    let skin = std::env::var("CSLOL_SKIN").unwrap();
    let temp = tempfile::tempdir().unwrap();
    let profile = Utf8PathBuf::from_path_buf(temp.path().to_owned()).unwrap();
    let request = |names: &[&str]| Request {
        game: game.clone(),
        output: profile.join("overlay"),
        state: profile.join("game-data"),
        mods: names.iter().map(|n| installed.join(n)).collect(),
        no_tft: false,
    };
    let native = || {
        let result = std::process::Command::new(env("CSLOL_MOD_TOOLS"))
            .args([
                "mkoverlay",
                installed.as_str(),
                profile.join("overlay").as_str(),
            ])
            .arg(format!("--game:{game}"))
            .arg(format!("--mods:{skin}"))
            .arg("--ignoreConflict")
            .output()
            .unwrap();
        assert!(
            result.status.success(),
            "{}",
            String::from_utf8_lossy(&result.stderr)
        );
    };
    let check_skin = || {
        let mut source = InstalledMod::open(installed.join(&skin)).unwrap();
        let mut checked = 0;
        for name in source.list_layer_wads("base").unwrap() {
            let output = walkdir::WalkDir::new(profile.join("overlay"))
                .into_iter()
                .map(Result::unwrap)
                .find(|e| e.file_name().to_string_lossy().eq_ignore_ascii_case(&name))
                .unwrap();
            let mut wad =
                Wad::mount(BufReader::new(fs::File::open(output.path()).unwrap())).unwrap();
            for (path, bytes) in source.read_wad_overrides("base", &name).unwrap() {
                let hash = ltk_overlay::utils::resolve_chunk_hash(&path, &bytes).unwrap();
                let chunk = *wad
                    .chunks()
                    .get(hash)
                    .unwrap_or_else(|| panic!("Missing {path}"));
                assert_eq!(
                    wad.load_chunk_decompressed(&chunk).unwrap().as_ref(),
                    bytes.as_slice(),
                    "{path}"
                );
                checked += 1;
            }
        }
        assert!(checked > 0);
        println!("Verified {checked} ordinary mod assets byte-for-byte");
    };
    native();
    check_skin();
    build(request(&[&hud, &skin])).unwrap();
    check_skin();
    build(request(&[&hud])).unwrap();
    assert!(
        !profile.join("overlay/DATA/FINAL/Champions").exists()
            || fs::read_dir(profile.join("overlay/DATA/FINAL/Champions"))
                .unwrap()
                .next()
                .is_none()
    );
    native();
    check_skin();
    assert!(!profile.join("overlay/DATA/FINAL/UI.wad.client").exists());
}
