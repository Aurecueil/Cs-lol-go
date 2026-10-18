mod content;
mod schema;
#[cfg(test)]
mod tests;

use anyhow::{Context, Result, bail};
use camino::Utf8PathBuf;
use content::InstalledMod;
use ltk_game_index::{BuildOptions, GameIndex, ObjectIndex};
use ltk_meta::Bin;
use ltk_overlay::{EnabledMod, OverlayBuilder, game_data::GameDataDiagnosticKind as Kind};
use ltk_wad::{Wad, WadHash};
use serde::Deserialize;
use std::{
    collections::{BTreeMap, BTreeSet},
    fs,
    io::{BufReader, Cursor},
};

#[derive(Deserialize)]
struct Request {
    game: Utf8PathBuf,
    output: Utf8PathBuf,
    state: Utf8PathBuf,
    mods: Vec<Utf8PathBuf>,
    #[serde(default)]
    no_tft: bool,
}

fn build(request: Request) -> Result<()> {
    anyhow::ensure!(
        request.output.file_name() == Some("overlay")
            && request.state.file_name() == Some("game-data")
            && request.output.parent() == request.state.parent(),
        "Output must be <profile>/overlay and state must be <profile>/game-data"
    );
    fs::create_dir_all(&request.state)?;
    let profile = request.state.parent().unwrap().canonicalize_utf8()?;
    let game = request.game.canonicalize_utf8()?;
    anyhow::ensure!(
        !profile.starts_with(&game) && !game.starts_with(&profile),
        "The overlay profile must be separate from the game installation"
    );
    println!("Indexing installed game for BIN deltas...");
    let index = GameIndex::load_or_build(&request.game, &request.state.join("game_index.bin"))?;
    let mut providers = request
        .mods
        .into_iter()
        .map(InstalledMod::open)
        .collect::<Result<Vec<_>>>()?;
    // Parse before any output is changed. Unsupported declarations fail the build.
    for provider in &providers {
        for layer in &provider.layers {
            if provider
                .active_layers
                .as_ref()
                .is_some_and(|active| !active.contains(&layer.name))
            {
                continue;
            }
            provider.declarations(&layer.name)?;
        }
    }
    let schema = load_schema(&request.state, &index)?;
    let blocked = if request.no_tft {
        index
            .archives()
            .iter()
            .filter(|a| {
                let name = a.file_name().to_ascii_lowercase();
                name == "map21.wad.client" || name == "map22.wad.client"
            })
            .map(|a| a.file_name().to_owned())
            .collect()
    } else {
        vec![]
    };
    let staging = request.state.join("staging");
    // A cancelled build can leave partially rewritten WADs here. The saved
    // overlay state describes the last completed build, not those partial files.
    if staging.exists() {
        anyhow::ensure!(
            staging.canonicalize_utf8()? == request.state.canonicalize_utf8()?.join("staging"),
            "Staging directory must not redirect outside the profile build state"
        );
        fs::remove_dir_all(&staging)?;
    }
    let mut builder = OverlayBuilder::new(request.game, staging.clone(), request.state.clone())
        .with_game_data_schema(schema)
        .with_blocked_wads(blocked)
        .with_progress(|p| {
            if p.total == 0 {
                println!("BIN deltas: {:?}...", p.stage);
            } else {
                println!("BIN deltas: {:?} {}/{}", p.stage, p.current, p.total);
            }
        });
    // mod-tools --ignoreConflict uses last-mod-wins; LTK uses first-mod-wins.
    builder.set_enabled_mods(
        providers
            .drain(..)
            .rev()
            .map(|content| EnabledMod {
                id: content.root.to_string(),
                enabled_layers: content.active_layers.clone(),
                content: Box::new(content),
            })
            .collect(),
    );
    let result = builder.build()?;
    fs::write(
        request.state.join("diagnostics.json"),
        serde_json::to_vec_pretty(&result.game_data_diagnostics)?,
    )?;
    let failures: Vec<_> = result
        .game_data_diagnostics
        .iter()
        .filter(|d| {
            !matches!(
                d.kind,
                Kind::SchemaFallback
                    | Kind::EntryFanOut
                    | Kind::ObjectShadowsGame
                    | Kind::PropertyEditSkipped
                    | Kind::LinkRemovalUnmatched
                    | Kind::LinkUnsupported
            )
        })
        .collect();
    if !failures.is_empty() {
        for d in failures.iter().take(15) {
            eprintln!(
                "{} [{}] {}: {}",
                d.mod_id,
                d.layer,
                d.target.as_deref().unwrap_or(""),
                d.message
            );
        }
        bail!(
            "{} delta diagnostics require attention. Details: {}",
            failures.len(),
            request.state.join("diagnostics.json")
        );
    }
    let skipped_properties = result
        .game_data_diagnostics
        .iter()
        .filter(|d| matches!(d.kind, Kind::PropertyEditSkipped))
        .count();
    if skipped_properties > 0 {
        println!(
            "BIN delta warning: {skipped_properties} property edits were skipped; review {}",
            request.state.join("diagnostics.json")
        );
        for d in result
            .game_data_diagnostics
            .iter()
            .filter(|d| matches!(d.kind, Kind::PropertyEditSkipped))
            .take(5)
        {
            println!(
                "  {} [{}]: {} ({:?})",
                d.mod_id, d.layer, d.message, d.property
            );
        }
    }
    let backup = request.state.join("previous-overlay");
    fs::create_dir_all(
        request
            .output
            .parent()
            .context("Overlay needs a parent directory")?,
    )?;
    if backup.exists() {
        fs::remove_dir_all(&backup)?;
    }
    if request.output.exists() {
        fs::rename(&request.output, &backup)?;
    }
    if let Err(error) = fs::rename(&staging, &request.output) {
        if backup.exists() {
            fs::rename(&backup, &request.output)?;
        }
        return Err(error.into());
    }
    if backup.exists() {
        fs::remove_dir_all(backup)?;
    }
    println!(
        "BIN delta overlay complete ({} written, {} reused WADs).",
        result.wads_built.len(),
        result.wads_reused.len()
    );
    Ok(())
}

fn load_schema(state: &camino::Utf8Path, index: &GameIndex) -> Result<schema::ObservedSchema> {
    let cache = state.join("field-shapes.msgpack");
    let fingerprint = index.fingerprint().as_u64();
    if let Some(schema) = schema::ObservedSchema::load(&cache, fingerprint) {
        return Ok(schema);
    }
    println!("Indexing native BIN classes (cached until the game changes)...");
    let objects = ObjectIndex::load_or_build_with(
        index,
        &state.join("object_index.bin"),
        &BuildOptions::default(),
    )?;
    let mut archives: BTreeMap<usize, BTreeSet<WadHash>> = BTreeMap::new();
    let mut seen = BTreeSet::new();
    for object in objects.objects() {
        for declaration in objects.declarations(object) {
            if seen.insert(declaration.chunk) {
                archives
                    .entry(declaration.archive.index())
                    .or_default()
                    .insert(declaration.chunk);
            }
        }
    }
    let mut schema = schema::ObservedSchema::default();
    for (number, (archive, hashes)) in archives.iter().enumerate() {
        if number % 25 == 0 {
            println!(
                "Reading native BIN field shapes: {number}/{} archives",
                archives.len()
            );
        }
        let mut wad = Wad::mount(BufReader::new(fs::File::open(
            &index.archives()[*archive].path,
        )?))?;
        for hash in hashes {
            let chunk = wad
                .chunks()
                .get(*hash)
                .copied()
                .context("Indexed BIN missing")?;
            let bytes = wad.load_chunk_decompressed(&chunk)?;
            if bytes.starts_with(b"PROP")
                && let Ok(bin) = Bin::from_reader(&mut Cursor::new(bytes))
            {
                schema.observe(&bin);
            }
        }
    }
    schema.save(&cache, fingerprint)?;
    Ok(schema)
}

fn main() -> Result<()> {
    let request = std::env::args()
        .nth(1)
        .context("Usage: game-data-overlay <request.json>")?;
    build(serde_json::from_slice(&fs::read(request)?)?)
}
