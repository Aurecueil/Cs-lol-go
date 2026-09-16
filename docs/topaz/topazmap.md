```markdown
# Topaz Map (`topaz.map`) Construction

The file is built in a single forward-only pass with zero seek-backs:

1. **Collect & Hash (Parallel):**
   * Scan files (excluding `topaz.map`).
   * Normalize paths to lowercase forward slashes (`path/to/file.ext`).
   * Resolve `uint64` hash (parse hex if root file; otherwise `xxHash64`).
   * Pre-encode paths to UTF-8 byte arrays.

2. **Sort:**
   * Sort entries ascending by `PathHash` for $O(\log N)$ binary searching.

3. **Pre-Compute Layout:**
   * String Pool starts at: `16 + (N * 16)` bytes.

---

### Exact Byte Structure Written to Disk


```

+-------------------------------------------------------------+
| Header (16 bytes)                                           |
|   [0..3]   4B : Magic ("TPZM")                              |
|   [4..7]   4B : Entry count N (uint32)                      |
|   [8..15]  8B : String pool offset (uint64) = 16 + (N * 16) |
+-------------------------------------------------------------+
| Index Records (N * 16 bytes)                                |
|   Each record is exactly 16 bytes:                          |
|   [0..7]   8B : PathHash (uint64)                           |
|   [8..11]  4B : Relative string offset (uint32)             |
|   [12..13] 2B : String length in bytes (uint16)             |
|   [14..15] 2B : Padding/Reserved (0x0000)                   |
+-------------------------------------------------------------+
| String Pool (Variable size)                                 |
|   Contiguous, raw UTF-8 path bytes                          |
|   (no separators, no null terminators)                      |
+-------------------------------------------------------------+

```

---

4. **Sequential Write:**
   * Stream **16-byte Header**.
   * Stream **$N \times 16$ bytes** of sorted Index Records, tracking a running `stringOffset`.
   * Stream **String Pool bytes** sequentially.
   * Flush and close.

```