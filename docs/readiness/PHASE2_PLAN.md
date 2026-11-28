# Phase 2 — execution plan

Built from `FPM_Master_Plan_v7.md` §21–§22, `FPM_FreeBuff_00_START_HERE.md` §5, and the six
FreeBuff documents. Where the FreeBuff cards cover a topic they win; v7 remains authoritative for
everything they do not cover. No card is started before its dependencies and entry gate are met.

## 1. Current position

Done and committed: **G0, R1, R2, R4, U1–U6, W1–W5, T1, D1–D5, A1–A4** (A4 is the current card;
see `PROGRESS_LOG.md`). T2 is partial: the unprivileged path ships, LHM/helper awaits D-043. U7's
reduced-motion capture and cancel are still open.
Next by the approved order: **A6** (Android multi-user, multi-device and wireless). Owner-gated: D-043 (LHM), Q-05 (push/signing).
Not started: U7–U12, T2 helper/LHM, T3–T4, A6–A7, R3, R5–R6, and the v7-only backlog in §9.

## 2. Wave 0 — release foundations (est. 8–12 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **R2** Licence + notices + community files | — | GPL-3.0 text, SPDX headers policy, `THIRD_PARTY_NOTICES.md` (Win11Debloat MIT verbatim, UAD GPL link, NuGet licences, LHM later), `CONTRIBUTING`, `CODE_OF_CONDUCT`, `SECURITY`, issue/PR templates, CODEOWNERS, dependabot. | Files exist, licence text is complete, every dependency listed, README disclaimer. |
| **R4** CI + secret scan | R2 | `ci.yml` (restore, build warnings-as-errors, tests, lint, contrast, CodeQL, dependency review), `release.yml` skeleton, install `gitleaks` locally and in CI, optional `global.json` SDK pin. | CI green on the commit; gitleaks clean on tree and history. |

**Wave gate:** a pushed-or-local CI run proves build/tests/lint/contrast/secret scan; owner has
authorised (or explicitly deferred) pushing.

## 3. Wave 1 — window chrome and performance (est. 8–12 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **W1** `FpmWindow` base chrome | Wave 0 | Base window (WindowChrome, 40 px title bar, caption buttons 46×36, Snap Layouts `HTMAXBUTTON`, Alt+Space, per-monitor size/position, DPI clamp), migrate MainWindow first; **replace `AllowsTransparency` with DWM rounded corners** if Q-04 approves. | All windows pass min/max/close + automation names; frame trace measured again and recorded; visual regression captures. |

**Wave gate:** frame budget re-measured; if ≥ 60 fps not reachable, the cause and numbers are
recorded and the owner accepts or rejects the remaining gap.

## 4. Wave 2 — UI completion (est. 30–40 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **U7** Boost animation (shared host) | W1, U4 | `BoostAnimationHost` driven by the real Boost state machine (Idle → Measuring → Working → Result), rocket/flame, count-up, result chip, cancel/error shake, toolbar reuse at 0.6×. **Partial 2026-11-28** — cancel/settle implemented and tested; live Reduced/Failure captures pending. | Real measurements only; duplicate clicks disabled; reduced motion path captured; state-machine tests. |
| **U8** Other animations | U7 | Scan radar/counter/chips, cleanup row dissolve + count-up, card/list stagger, toggle spring, slider/progress easing, toasts, dialogs, shimmer/empty states, sensor value cross-fade. | Each effect uses the motion kit; reduced motion verified; no fake progress. |
| **U9** Scrolling + navigation system | U8 | Inertial wheel/keyboard scrolling, thin overlay scrollbar, sticky headers, back-to-top, scroll memory, history stack/Back/Forward, keyboard shortcuts, focus restoration. | Keyboard-only pass; nested lists don't trap; focus restored. |
| **U10** Padding/margin/gap + localization foundation | U9 | Page-by-page spacing audit (`docs/dev/UI_SPACING_AUDIT.md`), 360/560/1280 fixes (known overlaps), `.resx` foundation + English keys (Urdu RTL when Q-03 allows). | Lint count falls (baseline updated downward), no clipping at three widths, all new strings keyed. |
| **U11** Existing-feature improvement sweep | U10 | Table in `01-U11` applied (Home, health, processes, startup, storage, files, cleanup, protection, network, settings, tray, errors, QoL). | Each improvement real-data, tested; no dead controls. |
| **U12** App cleanup | U11 | Dead code/usings/resources/packages, duplicate styles/converters, naming, `dotnet format`, warnings; docs refreshed. | Tests green before/after; behaviour unchanged; docs current. |

**Wave gate:** Light/Dark + 360/560/1280 captures for every screen; reduced motion; keyboard;
contrast and lint gates; frame budget evidence.

## 5. Wave 3 — windows, toolbar, sensors, toolbox (est. 36–48 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **W2** Virtual-desktop follow | W1 | `IVirtualDesktopManager` follower, settings, polling budget. | 3-desktop test notes; no undocumented APIs. |
| **W3** Toolbar survives close | W1 | Close-to-tray, exit dialog, `--toolbar-only`, single-instance handoff, persistence. | Lifecycle tests + manual; memory/CPU overhead recorded. |
| **W4** Edge docking | W3 | Snap/stick, slim bar, expand/collapse, multi-monitor/DPI, fullscreen auto-hide, opacity/pin/reset. | Persistence per monitor/DPI; never steals focus. |
| **W5** Toolbar boost | U7, W3 | Same host at compact scale; works hidden/toolbar-only. | Same state machine and confirmation rule. |
| **T1** Sensor service + battery | W3 | Typed readings with `Unavailable` reasons; battery time/health; shared sampler cadence. | Fixtures + no-battery path; honest labels. |
| **T2** CPU/GPU/laptop temperatures | T1 | LHM inside helper (opt-in), NVML/ADLX, thermal zones, thresholds, °C/°F. | Licence reviewed; failure modes tested; never labelled “CPU” for an ACPI zone. |
| **T3** Toolbox customization | T1, T2, W4 | Widget editor, layout file, toolbar + Home wiring, import/export. | Persists across restart/toolbar-only; unavailable tiles disabled with reason. |
| **T4** Memory icon + polish | T3 | `Icon3D.Memory` in header/chips/tray/toolbar, gauge count-up, list stagger. | Captures; no rule changes. |

**Wave gate:** sensors report real or explicitly unavailable values; toolbar lifecycle proven;
memory overhead recorded in `BUILD_STATUS.md`.

## 6. Wave 4 — Windows debloat (est. 30–40 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **D1** Catalog port | W1, R2, reference | Read `reference/win11debloat`, write `docs/dev/win11debloat-feature-map.md`, `Assets/debloat/catalog.json` with typed actions and OS gating. | Every reference tweak mapped or intentionally excluded with reason. |
| **D2** Engine | D1, helper | Planner (dependencies/conflicts/OS), executor via helper, journal/undo, restore point, `.reg` backup, idempotency, report codes. | Fake-registry tests; failure isolation; dry-run writes nothing. |
| **D3** UI (Debloat → Windows) | D2, U10 | Presets, categories, search, risk/applied badges, apps tab, diff preview, apply, report, undo, history. | Keyboard accessible; typed plan == UI selection; no free-form commands. |
| **D4** Advanced | D3 | Other user / Default hive, CLI (`--debloat`), preset export/import, drift check (report-only). **Done 2026-11-28** — see `PROGRESS_LOG.md`; live UAC target run remains R-33. | Hive always unloaded; CLI exit codes documented. |
| **D5** Tests | D4 | Schema, revert/detect presence, gating, conflicts, journal, denylist, parity. **Done 2026-11-28** — planner conflict refusal, shared restore guard, 16/16 exclusion parity, dry-run 0 files; see `PROGRESS_LOG.md`. | All green (266 tests); no real registry touched. |

**Wave gate:** safety checklist per card; catalogue parity report; no untyped actions possible.

## 7. Wave 5 — Android debloat (est. 30–40 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **A1** ADB service | R2 | Locate/consent/download/verify adb, devices parsing, onboarding, device info, polling. **Done 2026-11-28** — pinned 35.0.2, real signed adb locator, 20 A1 tests, Android tab probe; see `PROGRESS_LOG.md`. | Unsigned adb rejected; no bundling; 287 tests; probe exit 0 (no physical phone/download: R-38/R-39). |
| **A2** Package lists | A1 | Typed model + schema + validator for `uad_lists.json`; user-initiated update with diff/hash. **Done 2026-11-28 (`e33b5cb`)** — embedded 2,400/2,394/6, SHA-256; fake-source update/cache/diff; 15 tests; see `PROGRESS_LOG.md`. | Schema tests; embedded fallback; no live upstream fetch (R-44). |
| **A3** Package engine | A2 | State computation, typed commands, batch results, verify, dependency warnings. **Done 2026-11-28 (`3946e1d` + `0c50c62`)** — 19 A3 tests, fake executor, denylist/Expert gate, disconnect stop; see `PROGRESS_LOG.md`. | Command builder rejects injection; continue-on-failure; no live device or package mutation (R-47). |
| **A4** UI (Debloat → Android) | A3, U10 | Device/user selectors, filters, virtualized list, review/confirm, denylist UX, report, undo. **Done 2026-11-28** — controls, safety acknowledgement, Expert confirmation, batch review/apply/undo and UIA probe; see `PROGRESS_LOG.md`. | Expert typed confirmation; first-run warnings; no live device. |
| **A5** Backup/restore/log/export | A4 | Pre-apply snapshot, restore, append-only log, selection export/import (UAD-compatible). **Done 2026-11-28** — snapshot JSON, restore mapping, hashed rotated log, text/JSON selection and Recommended preset. | Snapshot restore verified with fakes; no personal data in logs. |
| **A6** Multi-user/device/wireless | A5 | Profiles, multiple devices, `adb pair/connect` UI, health pre-check. **Done 2026-11-28** — private-IP/serial guards, per-device/user state, health display and 339 tests. | Private-IP validation; no cross-device actions; no live phone. |
| **A7** Tests | A6 | Parsers, state, denylist, undo inverse, snapshots, signature check, parity. **Done 2026-11-28** — 341 tests, multi-device serial isolation and UAD parity test. | All green; no real device. |

**Wave gate:** safety checklist; typed ADB only; snapshot/undo proven; reference parity map.

## 8. Wave 6 — release (est. 12–16 h)

| Card | Depends on | Work | Acceptance |
|---|---|---|---|
| **R3** Installer | W6 everything | Inno Setup updates (tasks, AUMID, close running instances, uninstall data prompt, silent install, portable zip, signing hook, SHA256SUMS). **Partial 2026-11-28** — publish/installer/portable compile; live matrix pending. | Test matrix recorded in `BUILD_STATUS.md`. |
| **R5** README + screenshots | R3 | Logo/banner, real Light/Dark screenshots of every screen, sections, safety model, credits, build commands. **Partial 2026-11-28** — README/assets and core screen captures; matrix/wizard pending. | No mockups; links valid; every claim true. |
| **R6** Repo description/topics | R5 | Apply description/topics or print exact `gh` commands. **Done 2026-11-28** — exact command prepared; external edit deferred. | Description ≤ 350 chars; topics set. |

## 9. v7-only backlog (not covered by FreeBuff cards)

These remain required by v7 §22 and must be scheduled after the listed waves unless the owner
deprioritises them in writing:

1. Localization (English/Urdu RTL/Hindi/Arabic) — partially in U10 (Q-03).
2. Cleanup rule packs A–K, approval grammar, quarantine, one-click coordinator, ledger,
   recommendations, Cleanup Center, Activity, Privacy, Diagnostics pages (v7 §10, §16).
3. Health check page, boot time, health score (§9).
4. Protection additions: real Defender scan, default-browser guard, popup/hosts, network test (§9).
5. Restore/drift, AI Tools/OCR, Agents sandbox, Feedback (§9).
6. Storage Map, growth watch, drive health, DISM analyse (§14).
7. Command palette completion, onboarding, focus mode, Undo Center, maintenance readiness (§16).
8. Settings export/import, portable mode, scheduler `\FPM\` (§19).
9. Performance pass with measured p50/p95 and soak test (§17).
10. Accessibility pass (screen reader, high contrast, focus ≥ 3:1) (§18).

## 10. Working mechanics

- Read START §1–§4 + the card section only; update `AGENTS.md`/`PROGRESS_LOG.md` after each card.
- Stop `FPM.exe` before builds; run the gates in `fpm-wpf-verification`; never raise the lint
  baseline; never push without the owner.
- Commit each green card; tag checkpoints at phase gates (`git tag phase-<n>`).
- If context reaches ~70 %, write a handoff in `AGENT_STATE.md` (done / half-done / exact next
  command) and stop.
- Ask the owner only for irreversible/destructive decisions; everything else is decided here.
