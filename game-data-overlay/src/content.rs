use base64::{Engine, prelude::BASE64_STANDARD};
use camino::{Utf8Path, Utf8PathBuf};
use ltk_game_data::{DeclarationDocument, Declarations};
use ltk_mod_project::ModProject;
use ltk_overlay::{Error, ModContentProvider, Result};
use ltk_wad::{Wad, WadHash};
use serde::Deserialize;
use std::{collections::HashSet, fs, io::BufReader};

#[derive(Clone, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct Layer {
    pub name: String,
    pub priority: i32,
    pub game_data: Option<String>,
}

pub struct InstalledMod {
    pub root: Utf8PathBuf,
    pub layers: Vec<Layer>,
    pub active_layers: Option<HashSet<String>>,
}

fn io_error(path: &Utf8Path, error: impl std::fmt::Display) -> Error {
    Error::Read {
        path: path.to_owned(),
        source: std::io::Error::other(error.to_string()),
    }
}

pub fn layer_folder(name: &str) -> String {
    if name.eq_ignore_ascii_case("base") {
        "WAD".into()
    } else {
        format!("WAD_{name}")
    }
}

// Check canonical paths too: a resource symlink must not escape its mod.
pub fn inside(root: &Utf8Path, relative: &str) -> std::io::Result<Utf8PathBuf> {
    let relative = relative.replace('\\', "/");
    if relative.starts_with('/') || relative.split('/').any(|p| p == ".." || p.contains(':')) {
        return Err(std::io::Error::other("invalid layer-relative path"));
    }
    let path = root.join(&relative).canonicalize_utf8()?;
    if !path.starts_with(root.canonicalize_utf8()?) {
        return Err(std::io::Error::other("resource leaves mod directory"));
    }
    Ok(path)
}

impl InstalledMod {
    pub fn open(root: Utf8PathBuf) -> anyhow::Result<Self> {
        let sidecar = root.join("META/game-data.json");
        let mut layers: Vec<Layer> = if sidecar.exists() {
            serde_json::from_slice(&fs::read(sidecar)?)?
        } else {
            vec![]
        };
        for entry in fs::read_dir(&root)? {
            let entry = entry?;
            let name = entry.file_name().to_string_lossy().into_owned();
            let layer = if name.eq_ignore_ascii_case("WAD") {
                Some("base")
            } else {
                name.strip_prefix("WAD_")
            };
            if let Some(layer) = layer
                && !layers.iter().any(|l| l.name.eq_ignore_ascii_case(layer))
            {
                layers.push(Layer {
                    name: layer.into(),
                    priority: 0,
                    game_data: None,
                });
            }
        }
        for layer in &layers {
            anyhow::ensure!(
                !layer.name.is_empty()
                    && !layer.name.contains(['/', '\\', ':'])
                    && layer.name != "."
                    && layer.name != "..",
                "Invalid layer name"
            );
        }
        let details = root.join("META/details.json");
        let active_layers = if details.exists() {
            let details: serde_json::Value = serde_json::from_slice(&fs::read(details)?)?;
            let selected: HashSet<String> = details
                .get("Layers")
                .and_then(|v| v.as_array())
                .into_iter()
                .flatten()
                .filter(|l| l.get("is_active").and_then(|v| v.as_bool()) == Some(true))
                .filter_map(|l| {
                    l.get("Name")
                        .and_then(|v| v.as_str())
                        .map(str::to_ascii_lowercase)
                })
                .collect();
            Some(
                layers
                    .iter()
                    .filter(|l| {
                        l.name.eq_ignore_ascii_case("base")
                            || selected.contains(&l.name.to_ascii_lowercase())
                    })
                    .map(|l| l.name.clone())
                    .collect(),
            )
        } else {
            None
        };
        Ok(Self {
            root,
            layers,
            active_layers,
        })
    }

    fn resources(&self, layer: &str) -> Utf8PathBuf {
        self.root.join("META/game-data").join(layer_folder(layer))
    }

    fn wad(&self, layer: &str, wad: &str) -> Result<Utf8PathBuf> {
        let root = self.root.join(layer_folder(layer));
        if !root.join(wad).exists()
            && let Some(short) = wad.strip_suffix(".client")
            && root.join(short).exists()
        {
            return inside(&root, short).map_err(|e| io_error(&root, e));
        }
        inside(&root, wad).map_err(|e| io_error(&root, e))
    }

    pub fn declarations(&self, layer: &str) -> anyhow::Result<Option<Declarations>> {
        let resources = self.resources(layer);
        // Authoring manifests take precedence over legacy embedded declarations.
        for name in ltk_game_data::MANIFEST_NAMES {
            if resources.join(name).exists() {
                let file = inside(&resources, name)?;
                return Ok(Some(ltk_game_data::load_declarations(
                    name,
                    &fs::read_to_string(file)?,
                    |path| {
                        inside(&resources, path)
                            .and_then(fs::read_to_string)
                            .map_err(|e| ltk_game_data::Error::io(path, &e))
                    },
                )?));
            }
        }
        self.layers
            .iter()
            .find(|l| l.name == layer)
            .and_then(|l| l.game_data.as_ref())
            .map(|data| -> anyhow::Result<_> {
                let document: DeclarationDocument =
                    rmp_serde::from_slice(&BASE64_STANDARD.decode(data)?)?;
                Ok(document.parse()?)
            })
            .transpose()
    }
}

fn files(root: &Utf8Path) -> Result<Vec<(Utf8PathBuf, Vec<u8>)>> {
    if !root.exists() {
        return Ok(vec![]);
    }
    let mut result = vec![];
    for entry in walkdir::WalkDir::new(root).sort_by_file_name() {
        let entry = entry.map_err(|e| io_error(root, e))?;
        if entry.file_type().is_symlink() {
            return Err(io_error(root, "symlinks are not mod assets"));
        }
        if entry.file_type().is_file() {
            let path =
                Utf8Path::from_path(entry.path()).ok_or_else(|| io_error(root, "non-UTF8 path"))?;
            result.push((
                path.strip_prefix(root).unwrap().to_owned(),
                fs::read(path).map_err(|e| io_error(path, e))?,
            ));
        }
    }
    Ok(result)
}

impl ModContentProvider for InstalledMod {
    fn mod_project(&mut self) -> Result<ModProject> {
        Ok(serde_json::from_value(serde_json::json!({
            "name": "installed-mod", "display_name": "Installed mod", "version": "1.0.0",
            "description": "", "authors": [],
            "layers": self.layers.iter().map(|l| serde_json::json!({"name":l.name,"priority":l.priority})).collect::<Vec<_>>()
        }))?)
    }
    fn game_data_declarations(
        &mut self,
        layer: &str,
    ) -> std::result::Result<Option<Declarations>, ltk_game_data::Error> {
        self.declarations(layer)
            .map_err(|e| ltk_game_data::Error::io(layer, &e))
    }
    fn read_game_data_resource(&mut self, layer: &str, path: &str) -> Result<Vec<u8>> {
        let root = self.resources(layer);
        let file = inside(&root, path).map_err(|e| io_error(&root, e))?;
        fs::read(&file).map_err(|e| io_error(&file, e))
    }
    fn list_layer_wads(&mut self, layer: &str) -> Result<Vec<String>> {
        let root = self.root.join(layer_folder(layer));
        if !root.exists() {
            return Ok(vec![]);
        }
        let mut names = vec![];
        for entry in fs::read_dir(&root).map_err(|e| io_error(&root, e))? {
            let entry = entry.map_err(|e| io_error(&root, e))?;
            let name = entry.file_name().to_string_lossy().into_owned();
            if name.to_ascii_lowercase().ends_with(".wad.client") {
                names.push(name);
            } else if name.to_ascii_lowercase().ends_with(".wad") {
                names.push(format!("{name}.client"));
            }
        }
        names.sort();
        names.dedup();
        Ok(names)
    }
    fn read_wad_overrides(
        &mut self,
        layer: &str,
        name: &str,
    ) -> Result<Vec<(Utf8PathBuf, Vec<u8>)>> {
        let path = self.wad(layer, name)?;
        if path.is_dir() {
            return files(&path);
        }
        let mut wad = Wad::mount(BufReader::new(
            fs::File::open(&path).map_err(|e| io_error(&path, e))?,
        ))
        .map_err(|e| io_error(&path, e))?;
        let chunks: Vec<_> = wad.chunks().iter().copied().collect();
        chunks
            .into_iter()
            .map(|chunk| {
                let bytes = wad
                    .load_chunk_decompressed(&chunk)
                    .map_err(|e| io_error(&path, e))?;
                Ok((format!("{:016x}", chunk.path_hash.0).into(), bytes.to_vec()))
            })
            .collect()
    }
    fn read_wad_override_file(
        &mut self,
        layer: &str,
        name: &str,
        relative: &Utf8Path,
    ) -> Result<Vec<u8>> {
        let path = self.wad(layer, name)?;
        if path.is_dir() {
            let file = inside(&path, relative.as_str()).map_err(|e| io_error(&path, e))?;
            return fs::read(&file).map_err(|e| io_error(&file, e));
        }
        let hash = u64::from_str_radix(relative.as_str(), 16).map_err(|e| io_error(&path, e))?;
        let mut wad = Wad::mount(BufReader::new(
            fs::File::open(&path).map_err(|e| io_error(&path, e))?,
        ))
        .map_err(|e| io_error(&path, e))?;
        let chunk = wad
            .chunks()
            .get(WadHash(hash))
            .copied()
            .ok_or_else(|| io_error(&path, "missing chunk"))?;
        Ok(wad
            .load_chunk_decompressed(&chunk)
            .map_err(|e| io_error(&path, e))?
            .to_vec())
    }
    fn read_raw_overrides(&mut self) -> Result<Vec<(Utf8PathBuf, Vec<u8>)>> {
        files(&self.root.join("RAW"))
    }
    fn read_raw_override_file(&mut self, relative: &Utf8Path) -> Result<Vec<u8>> {
        let root = self.root.join("RAW");
        let file = inside(&root, relative.as_str()).map_err(|e| io_error(&root, e))?;
        fs::read(&file).map_err(|e| io_error(&file, e))
    }
    // Deliberately no persistent content fingerprint. Installed mods can be edited in place;
    // enumerate them on every build rather than risk reusing stale declarations/assets.
}
