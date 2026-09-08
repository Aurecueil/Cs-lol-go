# Topaz Map Specification (`topaz.map`)

**Version:** 1.0  
**Magic Identifier:** `TPZM` (ASCII: `0x54 0x50 0x5A 0x4D`)  
**Byte Order:** Little-Endian  

---

## 1. Overview

The **Topaz Map** (`topaz.map`) is a lightweight, read-optimized binary index file designed to map 64-bit path hashes (xxHash64) to relative file paths. It serves as a fast lookup index for unpacked asset directories (such as unpacked Riot Games WAD archives), eliminating the need for full-directory file scans or archive unpacking during runtime lookups.

### Key Features
- **$O(\log N)$ Binary Search:** Hash entries are pre-sorted in ascending numerical order.
- **Fixed-Size Records:** Index entries have a uniform size (16 bytes), allowing direct disk seeking or array indexing via memory mapping (`MemoryMappedFile`) without loading path strings into memory.
- **Compact String Pool:** All relative path strings are UTF-8 encoded and stored contiguously at the end of the file.

---

## 2. Binary Layout

A `topaz.map` file consists of three sequential sections:

```
+-------------------------------------------------------------+
| Header (16 bytes)                                           |
+-------------------------------------------------------------+
| Index Table (Count * 16 bytes) [Sorted by Path Hash]        |
+-------------------------------------------------------------+
| String Pool (Variable size)                                 |
+-------------------------------------------------------------+
```

---

## 3. Section Details

### 3.1 Header (16 Bytes)

| Offset | Field | Type | Size | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `Magic` | `char[4]` | 4 bytes | File magic string: `"TPZM"` (`0x54, 0x50, 0x5A, 0x4D`). |
| `0x04` | `EntryCount` | `uint32` | 4 bytes | Total number of indexed entries ($N$). |
| `0x08` | `StringTableOffset` | `uint64` | 8 bytes | Absolute byte offset from file start to the beginning of the String Pool. |

---

### 3.2 Index Table ($N \times 16$ Bytes)

The index table starts immediately at offset `0x10` (byte 16). Entries are **strictly sorted in ascending order** by `PathHash`.

Each index record is exactly 16 bytes:

| Offset in Record | Field | Type | Size | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `PathHash` | `uint64` | 8 bytes | 64-bit xxHash of the normalized relative path or parsed hex name. |
| `+0x08` | `StringOffset` | `uint32` | 4 bytes | Byte offset relative to the start of the String Pool (`StringTableOffset`). |
| `+0x0C` | `StringLength` | `uint16` | 2 bytes | Byte length of the UTF-8 encoded path string (excluding null terminators). |
| `+0x0E` | `Reserved` | `uint16` | 2 bytes | Alignment padding / reserved for future flags (must be `0x0000`). |

---

### 3.3 String Pool (Variable Length)

- Starts at the absolute offset specified by `StringTableOffset` in the header.
- Contains raw, contiguous UTF-8 encoded bytes for each file path.
- Path strings are **normalized**:
  - Lowercase (`.ToLowerInvariant()`).
  - Forward slashes (`/`) as path separators (e.g., `assets/characters/generic/model.skn`).
  - Strings are **not null-terminated**; read length is explicitly governed by `StringLength` in the index record.

---

## 4. Hash Generation Rules

1. **Root Hex Files:**
   If a file resides directly in the root directory (no subdirectory separators) and its filename without extension is a valid 16-character hexadecimal string, the 64-bit unsigned integer parsed from that hex value is used directly as `PathHash`.
2. **Subdirectories & Named Files:**
   All paths in subdirectories or root files with standard non-hex names are normalized to lowercase forward-slash paths and hashed using xxHash64 (seed = 0).

---

## 5. Lookup Algorithm ($O(\log N)$)

Given a target 64-bit hash `targetHash`:

1. Read `EntryCount` from offset `0x04`.
2. Set binary search bounds: `low = 0`, `high = EntryCount - 1`.
3. While `low <= high`:
   - Calculate `mid = low + ((high - low) / 2)`.
   - Seek to file position: `16 + (mid * 16)`.
   - Read `currentHash` (`uint64`).
   - If `currentHash == targetHash`:
     - Read `stringOffset` (`uint32`) and `stringLength` (`uint16`).
     - Seek to `StringTableOffset + stringOffset`.
     - Read `stringLength` bytes and decode as UTF-8.
     - **Match found.**
   - Else if `currentHash < targetHash`: `low = mid + 1`.
   - Else: `high = mid - 1`.
4. If `low > high`, target hash does not exist in the index.