# Universal Android Debloater — feature map

Reference read-only tree: `reference/universal-android-debloater`  
Pinned reference commit: `11f27c671cba278d71296cdef4c5a5dba06add5e` (2023-04-10)  
License: GPL-3.0 (upstream `LICENSE`); FPM keeps the upstream notice and does not run or bundle
the Rust application.

This map is the A2 baseline for the Android debloat port. A7 adds the automated parity check; the
rows below name the reference area and the FPM component that owns it, so later cards cannot
silently drop a feature.

## Reference data schema

`resources/assets/uad_lists.json` is a JSON array of package records. Every record has:

| Field | Type | FPM handling |
|---|---|---|
| `id` | string | package id; `android` and dotted ids accepted, duplicates normalized last-wins |
| `list` | string enum | `Aosp`, `Oem`, `Carrier`, `Google`, `Misc`, `Pending`, `Unlisted` |
| `description` | string | displayed as upstream text; no execution |
| `dependencies` | string array | retained for A3 dependency warnings |
| `neededBy` | string array | retained for A3 dependency warnings |
| `labels` | string array | retained for A3/A4 metadata |
| `removal` | string enum | `Recommended`, `Advanced`, `Expert`, `Unsafe`, `Unlisted` |

The pinned reference file has **2,400 source rows**, **2,394 unique package ids** and **6 duplicate
ids**. The parser reports and normalizes duplicates last-wins, matching the reference's
`HashMap` loading behaviour. The embedded copy is
`app/src/Core/Assets/android/uad_lists.json`, SHA-256
`e25a5087ea1730575e48aefa0d1f65990c0eaffc179c9950867aaf4ffbd85383`.

## Feature → component

| Upstream feature | Reference area | FPM component / card | Status |
|---|---|---|---|
| Embedded package list and schema | `resources/assets/uad_lists.json`, `src/core/uad_lists.rs` | `Core/Android/UadList*`, `Services/Android/PackageListProvider.cs`; **A2** | done |
| Remote list update, cache and fallback | `src/core/uad_lists.rs` (`load_debloat_lists`, `get_local_lists`) | A2 `IUadListSource`, `IPackageListProvider`; embedded fallback | done (network only on user action) |
| Lists: GFAM/AOSP/OEM/carrier/misc | same JSON, `list` field | `UadListKind`; **A2** data, **A4** filters | data done |
| Package state detection | `src/core/sync.rs`, `src/core/utils.rs` | **A3** package engine | done |
| Uninstall/disable/restore/enable | `src/core/sync.rs`, `src/gui/widgets/package_row.rs` | **A3** typed actions; **A4** review | done |
| Multi-user/work profile | `src/core/sync.rs`, `src/gui/views/list.rs` | **A3/A6** user selection and `--user` actions | done |
| Multi-device support | `src/gui/views/list.rs` | **A1** discovery; **A6** per-device state | done |
| Export/import selection (`uad_exported_selection.txt`) | `src/core/save.rs` | **A5** export/import plus FPM JSON | done |
| Action log | `src/core/save.rs` | **A5** append-only `android_actions.log` | done |
| Dependency and needed-by metadata | `dependencies`, `neededBy` in JSON | **A3** warning path; retained by A2 | data done |
| Wireless debugging/pairing | UAD README/ADB workflow; not in the old GUI | **A6** `adb pair`/`connect` helper | done |
| Pre-apply snapshot/restore | UAD relies on package reinstall; FPM improvement | **A5** snapshot and inverse actions | done |
| Chinese OEM AOSP-name caveat | upstream README lines 160–162 | **A2** warning text; **A4** first-run guidance | visible in A2 |
| Platform-tools setup/signature gate | upstream README setup | **A1** locator/consent/Authenticode | done |
| Bootloop/factory-reset warning and protected packages | upstream README/FAQ, `removal: Unsafe` | **A4/A7** denylist, Expert confirmation and warnings | done |
| Package list update diff/hash | FPM improvement (not in this reference snapshot) | **A2** `UadListDiff` and SHA-256 sidecar | done |

## Deliberate differences

- FPM never runs the UAD binary or its scripts; all package operations go through the typed ADB
  command surface added in A3.
- The reference downloads the remote list on launch. FPM loads embedded/cached data first and
  contacts upstream only when the user presses **Check for list updates**.
- FPM reports duplicate ids instead of hiding them; the last record wins, as in the reference.
- No reference screenshots, logos or code are copied; only the GPL package data and its schema.
