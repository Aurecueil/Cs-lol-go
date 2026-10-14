use camino::Utf8Path;
use ltk_game_data::{BinHash, IndexMap, Schema, Shape};
use ltk_meta::{Bin, PropertyValueEnum as V};
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet};

type StoredShape = (u8, Option<u8>, Option<u8>);
#[derive(Serialize, Deserialize)]
struct Cache {
    version: u32,
    fingerprint: u64,
    classes: Vec<u32>,
    fields: Vec<(u32, u32, Option<StoredShape>)>,
}

/// Shapes observed in the current game's BINs are a fallback, never an authoritative schema.
/// Conflicting observations are discarded rather than choosing an arbitrary field type.
#[derive(Default)]
pub struct ObservedSchema {
    classes: HashSet<BinHash>,
    fields: HashMap<(BinHash, BinHash), Option<Shape>>,
}
impl ObservedSchema {
    pub fn load(path: &Utf8Path, fingerprint: u64) -> Option<Self> {
        let cache: Cache = rmp_serde::from_slice(&std::fs::read(path).ok()?).ok()?;
        if cache.version != 1 || cache.fingerprint != fingerprint {
            return None;
        }
        let mut schema = Self::default();
        schema
            .classes
            .extend(cache.classes.into_iter().map(BinHash));
        for (class, field, shape) in cache.fields {
            let shape = match shape {
                Some((kind, key, item)) => Some(Shape {
                    kind: kind.try_into().ok()?,
                    key: key.map(TryInto::try_into).transpose().ok()?,
                    item: item.map(TryInto::try_into).transpose().ok()?,
                }),
                None => None,
            };
            schema
                .fields
                .insert((BinHash(class), BinHash(field)), shape);
        }
        Some(schema)
    }
    pub fn save(&self, path: &Utf8Path, fingerprint: u64) -> anyhow::Result<()> {
        let cache = Cache {
            version: 1,
            fingerprint,
            classes: self.classes.iter().map(|c| c.0).collect(),
            fields: self
                .fields
                .iter()
                .map(|((c, f), s)| {
                    (
                        c.0,
                        f.0,
                        s.map(|s| (s.kind.into(), s.key.map(Into::into), s.item.map(Into::into))),
                    )
                })
                .collect(),
        };
        std::fs::write(path, rmp_serde::to_vec(&cache)?)?;
        Ok(())
    }
    pub fn observe(&mut self, bin: &Bin) {
        for object in bin.objects.values() {
            self.fields(object.class_hash, &object.properties);
        }
    }
    fn fields(&mut self, class: BinHash, fields: &IndexMap<BinHash, V>) {
        self.classes.insert(class);
        for (&field, value) in fields {
            let shape = Shape::of(value);
            self.fields
                .entry((class, field))
                .and_modify(|old| {
                    if *old != Some(shape) {
                        *old = None;
                    }
                })
                .or_insert(Some(shape));
            self.value(value);
        }
    }
    fn value(&mut self, value: &V) {
        match value {
            V::Struct(s) => self.fields(s.class_hash, &s.properties),
            V::Embedded(s) => self.fields(s.0.class_hash, &s.0.properties),
            V::Container(c) => {
                for v in c.items() {
                    self.value(v);
                }
            }
            V::UnorderedContainer(c) => {
                for v in c.0.items() {
                    self.value(v);
                }
            }
            V::Optional(o) => {
                if let Some(v) = o.value() {
                    self.value(v);
                }
            }
            V::Map(m) => {
                for (k, v) in m.entries() {
                    self.value(k);
                    self.value(v);
                }
            }
            _ => {}
        }
    }
}
impl Schema for ObservedSchema {
    fn expected(&self, _: BinHash, _: BinHash) -> Option<Shape> {
        None
    }
    fn fallback(&self, class: BinHash, field: BinHash) -> Option<Shape> {
        if let Some(observed) = self.fields.get(&(class, field)) {
            return *observed; // A conflict must not fall through to a default.
        }
        // UiElementIconData.UseAlpha is a Bool with native default true, so the game
        // can omit it from every shipped instance. Verified in the public class schema:
        // https://github.com/LeagueToolkit/lol-meta-wiki/blob/main/db/meta.db.json
        // Keep this class-specific and only recognize classes present in this patch.
        if self.has_class(class) && class == BinHash(0x1a65e950) && field == BinHash(0x8fde0428) {
            return Some(Shape::bare(ltk_game_data::PropertyKind::Bool));
        }
        None
    }
    fn has_class(&self, class: BinHash) -> bool {
        self.classes.contains(&class)
    }
}
