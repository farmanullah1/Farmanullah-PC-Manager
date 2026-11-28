# Ordered checklist (Phase 1 → Phase 2)

Order matters: each item's verification is the next item's prerequisite. Estimates are focused
working time for one agent session, not calendar time. `☑` = verified, `☐` = pending, `⛔` = blocked.

## Phase 1 — readiness

| # | Item | Estimate | Completion criterion | State |
|---|---|---|---|---|
| 1.1 | Read the working protocol and all plan documents | 1.5 h | Notes exist; card list extracted; no invented requirements | ☑ |
| 1.2 | Inspect the repository, git state and existing docs | 0.5 h | `BASELINE.md` §1/§4 written from commands | ☑ |
| 1.3 | Record toolchain versions and compatibility | 0.5 h | `TOOLS.md` §1–§3 filled; gaps flagged | ☑ |
| 1.4 | Reproduce the baseline gates (build/tests/lint/contrast) | 0.5 h | Output pasted in `BASELINE.md` §3 | ☑ |
| 1.5 | Skill inventory + gap analysis (`/find-skills`) | 1 h | `SKILLS.md` lists available skills, gaps, search results | ☑ |
| 1.6 | Install the two project skills | 0.25 h | Installed **or** blocker recorded with resolution (currently blocked: library 50/50) | ⛔ |
| 1.7 | Sandbox-test the skill content | 0.25 h | Commands/paths in both skills resolve; no destructive step; result logged | ☑ |
| 1.8 | Scope and success criteria | 0.5 h | `README.md` §2–§3 approved | ☑ |
| 1.9 | Single source of truth | 0.25 h | `README.md` §1 + `AGENTS.md` point here | ☑ |
| 1.10 | Decision log and risk register | 0.75 h | `DECISIONS.md`, `RISKS.md` complete | ☑ |
| 1.11 | Definition of done (task + gate) | 0.25 h | `README.md` §4–§5 | ☑ |
| 1.12 | Version control, backup, rollback | 0.25 h | `README.md` + `BASELINE.md` procedure; baseline tag made | ☑ |
| 1.13 | Review checkpoints and approvers | 0.25 h | `README.md` §4 approved (Q-07) | ☑ (D-021 default cadence) |
| 1.14 | Traceability matrix | 1 h | `TRACEABILITY.md` maps every plan area to cards/files/criteria | ☑ |
| 1.15 | Stakeholders and communication cadence | 0.25 h | `README.md` §4; owner + agent roles explicit | ☑ |
| 1.16 | Effort estimates for every checklist item | 0.5 h | this table has an estimate per row | ☑ |
| 1.17 | Phase-gate acceptance criteria | 0.5 h | `README.md` §4 + `PHASE2_PLAN.md` wave gates | ☑ |
| **1.G** | **Phase 1 gate** | — | All rows ☑ or owner-accepted ⛔; owner approval recorded in `DECISIONS.md` | ☑ 2026-11-27 — D-015; 1.6 owner-accepted repo-only (D-017) |

## Phase 2 — document review and execution setup

| # | Item | Estimate | Completion criterion | State |
|---|---|---|---|---|
| 2.1 | Read v7 and all six FreeBuff documents in full | 2 h | Section map + card dependencies recorded | ☑ |
| 2.2 | Reconcile plan v7 cards with FreeBuff cards | 1 h | One ordered backlog in `PHASE2_PLAN.md`, no duplicates | ☑ |
| 2.3 | Identify prerequisites/blockers per wave | 0.5 h | Wave table has entry conditions | ☑ |
| 2.4 | Ask the owner the seven open questions | 0.5 h | Answers recorded in `DECISIONS.md` | ☑ — D-016…D-021; Q-07 answered by default |
| 2.5 | Approve the wave plan | 0.25 h | G-2 recorded | ☑ — D-016 execution order |
| 2.6 | Execute wave 1, one card per session | 20–30 h | Each card's DoD + acceptance evidenced | ☐ (R2, R4, W1–W5, T1, D1–D5, A1–A6 done; T2 unprivileged done, LHM/helper pending owner; U7 reduced-motion capture + cancel open; next A7) |
| 2.7 | D4 advanced debloat | 6–8 h | Typed target, hive unload, presets, drift, CLI, tests, probe, docs | ☑ 2026-11-28 — 260 tests, probe exit 0, build 0/0, lint 367, contrast OK; live UAC run not approved (R-33) |
| 2.8 | D5 Windows debloat tests | 4–6 h | Schema, revert/detect, gating, conflicts, journal, denylist and parity all covered; no live registry touched | ☑ 2026-11-28 — 267 tests, build 0/0, lint 367, contrast OK; dry-run 0 files; 16/16 exclusions; live UAC gaps remain (R-28/R-33) |
| 2.9 | A1 ADB service | 4–6 h | Locate/consent/download/verify adb, devices parsing, onboarding, device info, 2 s visible polling; unsigned adb rejected; no bundling | ☑ 2026-11-28 — 287 tests (20 A1), build 0/0, lint 367, contrast OK, IconGen 113; UIA probe exit 0 with real signed adb + NoDevice; no physical phone/live download (R-38/R-39) |
| 2.10 | A2 package lists | 3–5 h | Typed model + schema/validator for `uad_lists.json`; user-initiated update with diff/hash; embedded fallback | ☑ 2026-11-28 — `e33b5cb`; 302 tests (15 A2), embedded 2,400/2,394/6 + SHA-256, fake-source update/cache/diff, build 0/0, lint 367, contrast OK, IconGen 113; probe read embedded counts; no live upstream fetch (R-44) |
| 2.11 | A3 package engine | 4–6 h | State computation, typed commands, batch results, verify, dependency warnings; command builder rejects injection; continue on failure | ☑ 2026-11-28 — `3946e1d` + `0c50c62`; 321 tests (19 A3), fake-state/mutation tests, build 0/0, lint 367, contrast OK, IconGen 113, A2-tab probe exit 0; no live device or package mutation (R-47) |
| 2.12 | A6 multi-user/device/wireless | 3–5 h | Private-IP wireless pairing/connect/disconnect, per-device/user state, health pre-check and no cross-device actions | ☑ 2026-11-28 — 339 tests, private-IP/serial guards, selected-device/user scope, honest unavailable unlock status, build 0/0, lint 367, contrast OK, Android NoDevice probe; no live phone |
| 2.13 | A7 Android tests | 4–6 h | Recorded parsers, command/state safety, denylist, undo, snapshots, update/signature, parity and no real device | ☑ 2026-11-28 — 341 tests, 0/0 build, lint 367, contrast OK, IconGen 113, Android UIA probe, UAD parity and multi-device isolation tests |
| 2.14 | R3 professional installer | 6–10 h | Self-contained publish, Inno tasks, close/uninstall, portable ZIP, optional signing and SHA-256; test matrix | ☐ PARTIAL 2026-11-28 — publish/portable/installer compile, 341 tests, graceful --exit; clean/silent/upgrade/uninstall/coexistence matrix not proven |
| 2.15 | R5 README and screenshots | 4–8 h | Proper README, original logo/banner, real running-app captures and truthful gaps | ☐ PARTIAL 2026-11-28 — README/assets and Home/Storage/Memory/Debloat captures exist; installer wizard and full responsive/theme matrix remain unverified |
| 2.16 | R6 repository description/topics | 0.5 h | Description ≤350 chars and exact topics prepared or applied | ☑ 2026-11-28 — auth verified, metadata read, exact `gh repo edit` command prepared; external edit deferred |
| 2.17 | U7 shared Boost animation | 3–5 h | Shared state host, cancel, 2 s settlement, Reduced Motion and live visual capture | ☐ PARTIAL 2026-11-28 — cancel/CTS/settle implemented; 343 tests, 8 Boost tests; live Reduced/Failure/Cancel captures not run |
| 2.18 | U8 other animation states | 4–6 h | Real scan/loading, row stagger, cleanup/dialog/sensor/debloat motion with Reduced guard | ☐ PARTIAL 2026-11-28 — Storage/Debloat busy shimmer and cleanup stagger wired; live cleanup/toast/dialog/sensor matrix remains unverified |
| 2.19 | U9 scrolling and navigation | 3–5 h | Smooth wheel/keyboard, overlay scrollbar/back-to-top, history, shortcuts, scroll memory and focus | ☐ PARTIAL 2026-11-28 — history/shortcuts/scroll memory/back-to-top/wheel easing implemented; 345 tests; live nested-wheel/focus/offset retention remain unverified |
| 2.20 | U10 spacing and localization foundation | 4–6 h | Page spacing audit, token pass, `.resx` foundation, English/Urdu RTL and 360/560/1280 review | ☐ PARTIAL 2026-11-28 — Settings 31→24, baseline 367→360, English/Urdu resources and startup RTL hook; live widths/complete UI translation remain unverified |
| 2.21 | U11 existing-feature sweep | 6–10 h | Improve named existing features with real data, tests and honest gaps | ☐ PARTIAL 2026-11-28 — Apps process search/sort/virtualization plus 3 tests; broader feature sweep remains open |
| 2.22 | U12 app cleanup | 4–6 h | Remove dead code, duplicate styles/resources/packages, format, warnings and docs refresh | ☑ 2026-11-28 — package inventory, TODO/HACK/stub scan, analyzer and format verification, 16 whitespace fixes, 350-test gate |
| 2.23 | T3 toolbox layout | 3–5 h | Layout editor/persistence, toolbar and optional Home wiring, reset/export/import | ☐ PARTIAL 2026-11-28 — typed 9-entry layout, editor, enablement, move/reset/export/import, 354 tests; live reorder/toolbar-only/Home not verified |
| 2.24 | T4 Memory icon/polish | 2–3 h | Icon surfaces plus gauge count/progress and process stagger, no RAM-rule change | ☐ PARTIAL 2026-11-28 — real progress/row stagger wired; live gauge/process/icon matrix not captured |

## Waves (details in `PHASE2_PLAN.md`)

| Wave | Cards | Estimate | Gate |
|---|---|---|---|
| W0 Foundations | R2 licence/notices, R4 CI, gitleaks, SDK pin | 8–10 h | **done**: workflow validated, every job command run locally green, history scan clean, SDK pinned |
| W1 UI finish | U7, U8, U9, U10, U11, U12 (+ localization foundation in U10) | 30–40 h | All screens at 3 widths, reduced motion, 16 ms budget |
| W2 Windows | W1, W2, W3, W4, W5 | 20–28 h | Window lifecycle + toolbar survive close; tests |
| W3 Sensors/toolbox | T1, T2, T3, T4 | 16–20 h | Real readings or `Unavailable`; no fabricated values |
| W4 Windows debloat | D1–D5 | 30–40 h | Catalog parity, journal/undo, no real-registry tests |
| W5 Android debloat | A1–A7 | 30–40 h | Typed ADB only, denylist, snapshot/undo |
| W6 Release | R3 installer, R5 README/screenshots, R6 metadata | 12–16 h | Test matrix, signed-or-documented, truthful README |
