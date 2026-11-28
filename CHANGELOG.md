# Changelog

All notable changes to Farmanullah PC Manager. Dates are ISO (`YYYY-MM-DD`). Status markers
follow the audit legend: **[DONE]**, **[PARTIAL]**, **[MISSING]**.

## [Unreleased]

### Added
- **Android backup, restore, log and selection portability (A5)**: package application now creates
  an automatic per-device/per-user JSON snapshot, stores it under `%LOCALAPPDATA%\FPM\android\backups`,
  and logs package actions in a rotated `android_actions.log` using a SHA-256 serial hash, model,
  user, action and result. The Android tab can restore a snapshot, select Recommended-tier packages,
  export/import UAD text and FPM JSON selections, ignore unknown imported ids and open the action
  log. No personal serial is written to the log.

### Added
- **Android package engine (A3)**: typed package enumeration now joins UAD metadata with
  enabled/disabled/system state for a device and Android user. The engine exposes uninstall,
  disable, restore and enable actions over the closed ADB argument builder; unsafe/protected
  packages require Expert mode plus the exact package id; disable-instead-of-uninstall is a
  reviewable option. Batches continue after ordinary failures, stop on device disconnect, and
  re-query state after apply. Dependency and needed-by metadata are emitted as review warnings.
  No real phone or package mutation is used by tests.

### Added
- **Android package lists (A2)**: the Debloat → Android tab now loads a typed, validated UAD catalog from the embedded GPL package data (2,400 source rows, 2,394 unique ids, 6 reported duplicates) and shows its source, counts and SHA-256. A user-pressed **Check for list updates** fetches the official upstream JSON over HTTPS, validates it with the same schema, shows an added/removed/changed diff, and writes a cached copy plus SHA-256 sidecar under `%LOCALAPPDATA%\FPM\android\lists`; an absent, invalid or failed update falls back to the embedded list. No package operations are exposed yet.

### Added
- **Android ADB onboarding (A1)**: a new Debloat → Android tab locates a signed `adb` (configured path, FPM tools, Android SDK locations, then PATH), refuses unsigned copies, explains every device state (no device, unauthorized, offline, no USB permission), and reads device identity and Android profiles through a closed typed command set. Google platform-tools **35.0.2** is offered only after an explicit Android SDK license acceptance; the pinned archive is SHA-256 checked before extraction and the staged `adb.exe` must carry a valid Google Authenticode signature. The ADB server starts lazily, is stopped only when FPM started it, and the device list polls every 2 s only while the tab is visible. No platform-tools are bundled.

### Added
- **Windows debloat safety tests (D5)**: the planner now refuses contradictory selections
  (`SkippedConflict`) before elevation — the same registry value with different final data, or one
  optional feature both on and off — so presets cannot silently apply both sides of Spotlight,
  search-box, taskbar, Start-layout or drive-letter choices. Restore-state validation moved into a
  shared Core guard with unknown-path/hive tests, the parity test compares all 106 reference
  features (90 ported + 16 exclusions) with the embedded catalog, and the catalog now records the
  five exclusions the generator previously omitted.

### Added
- **Windows debloat advanced (D4)**: typed user targets for another local profile (live hive or an
  offline `NTUSER.DAT` loaded as `HKU\FPM_*` and always unloaded) and the Default user hive with a
  SYSPREP warning and Windows 10 gate; a target picker plus profile refresh; `fpm-debloat-preset.json`
  export/import with schema validation and unknown-id warnings; a report-only drift check; and a
  `--debloat` CLI with presets/tweaks/user, dry-run and silent/accept-risk gating that shares the
  page's planner, engine and journal. App removal remains refused, so `--apps` is parsed but does
  not execute yet.

### Added
- **Windows debloat page (D3)**: a real Debloat page in the rail with Windows and Apps tabs —
  OS/build header, category counts and search, five documented selection presets, risk and impact
  labels, a typed plan/review, Detect/Apply/Undo over the journaled engine, a restore-point toggle,
  success/declined/partial-rollback reports and journal history. The Apps tab is a searchable
  catalog only; removal stays refused until Appx state can be journaled. Unsupported and protected
  tweaks remain disabled with their planner reason. The Settings preview card was removed.

### Added
- **Windows debloat engine (D2)**: catalog tweaks now plan into an ordered, OS-gated list and run
  only through three typed helper commands (`DetectTweak`, `ApplyTweak`, `RevertTweak`). Every run
  writes a `.reg` backup and a journal of the exact pre-state, tries a restore point (recording a
  skip reason when unavailable), and undoes in reverse order; a failure mid-run rolls the applied
  tweaks back and says so. Settings gained a preview card for picking one tweak and seeing its plan;
  key deletion and Appx removal stay refused until they can be journaled faithfully.

### Added
- **Windows debloat catalog (D1)**: a typed, embedded, versioned port of Win11Debloat's data —
  90 registry/feature tweaks with OS gates, typed actions, detect probes and undo data, plus 161
  preinstalled apps and presets. A validator enforces the hive/path allow-list and FPM's protected
  package denylist at load; `docs/dev/win11debloat-feature-map.md` records all 106 reference
  features as ported or intentionally excluded with a reason.

### Added
- **Temperature sensor (T2, unprivileged)**: the shared sampler now also reads an NVIDIA GPU
  temperature through NVML when present and the ACPI thermal zone otherwise, labelled “System
  thermal zone” and never “CPU”. Thresholds (70/85 °C), °C/°F display and every unavailable reason
  live in one formatting class; Home, the toolbar and Settings show the same reading, including an
  honest “NeedsPermission” state when Windows blocks the zone.

### Added
- **Battery sensor (T1)**: one shared sampler feeds Home, the floating toolbar and Settings —
  battery percent, AC/charging state, time remaining (OS value, or a rate-based “(estimated)” value),
  and design-vs-full battery health, with 5 s sampling while visible and 30 s otherwise. Machines
  without a battery report “No battery detected” instead of a fake percentage.

### Added
- **Boost inside the toolbar (W5)**: Home and the floating toolbar now share one Boost run — the same
  Idle/Measuring/Working/Completed/Failed state machine, the same rule that only an explicit Boost may
  delete temporary files, and the same rocket/flame animation with a count-up result chip (0.6× in the
  toolbar). It works with the main window hidden and in `--toolbar-only` mode, and the result updates
  Home when it next appears.

### Added
- **Toolbar edge docking (W4)**: drag the toolbar to a screen edge and it snaps into a 32 px tab
  showing the RAM metric; hovering or clicking expands it (150 ms) into the full tool drawer, which
  collapses 1.5 s after the pointer leaves unless pinned. Right-click offers docking, an opacity
  slider, always-on-top, reset and hide. It hides itself while a fullscreen app or presentation is
  running, never takes focus, and stores its dock edge, pin, opacity and position per monitor/DPI.

### Added
- **Toolbar survives closing the app (W3)**: closing the main window hides it to the tray while the
  floating toolbar keeps running; the tray asks “Exit everything / exit but keep the toolbar / cancel”
  (with a remember option); `--toolbar-only` runs tray + toolbar without the shell; and a second
  launch hands off over the per-user `FPM.Instance.<SID>` pipe instead of starting a competitor. The
  toolbar’s own × turns the setting off, and sign-in registers `--toolbar-only` when the toolbar was
  left running.
- **Per-monitor, per-DPI window placement (W1)**: size and position are stored per monitor device
  and effective DPI, with a clamp to the visible work area on restore and the legacy values as
  fallback.

### Added
- **Virtual-desktop following (W2)**: the floating toolbar and (opt-in) the main window move to the
  desktop you switch to, using the documented `IVirtualDesktopManager` COM API. Following is a 500 ms
  poll that stops when the window is hidden or minimised, never activates the window, and never
  guesses when the foreground desktop id is unavailable. Windows has no documented “pin to all
  desktops” API, so Settings says so; toolbar follows by default, the main window does not.

### Changed
- **Window chrome (W1)**: every captioned window now derives from `Views/FpmWindow.cs`, which draws real
  DWM rounded corners instead of a layered window, follows the app theme for the system frame, restores
  Alt+Space, and answers `HTMAXBUTTON` so Windows 11 shows Snap Layouts over the custom maximize button.
  The title bar is 40 px with 46×36 caption buttons and automation names; the main window no longer sets
  `AllowsTransparency`, so WPF can compose on the GPU.
- **Settings at narrow widths**: the “Start with Windows” and “Smart Boost” cards use two columns, so
  their toggles no longer overlap the text at 560 px.

### Fixed
- Navigation rail items exposed their CLR type name to screen readers and UI automation; they now
  announce `Home`, `Memory`, `Protection`, `Storage`, `Apps`, `Toolbox` and `Settings`.

### Added
- **CI, secret scanning and a pinned SDK (R4)**: `.github/workflows/ci.yml` builds Release with
  warnings-as-errors, runs the full test suite, the XAML token lint, the contrast check, the icon
  generator check and a full-history secret scan on GitHub-hosted Windows runners.
  `tools/secret-scan.ps1` pins gitleaks 8.30.1, verifies the release checksum before installing it
  into `%LOCALAPPDATA%\FPM\tools`, prints findings redacted and preserves the exit code, so CI and
  a local run execute the identical command. `global.json` pins SDK 8.0.428 (`latestFeature`);
  build, tests, lint, contrast, icon check and a full-history gitleaks scan were all re-run locally
  and pass under the pin (a working-tree scan found only two JWT matches inside the ignored,
  never-committed `investigation.cany/` evidence folder; see `docs/readiness/RISKS.md` R-16).
- **Licence and community files (R2)**: FPM now ships the full GPL-3.0 text in `LICENSE`,
  `THIRD_PARTY_NOTICES.md` (Win11Debloat MIT notice verbatim, the Universal Android Debloater
  GPL reference, the Android platform-tools consent note, and every resolved NuGet dependency with
  its licence), `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, issue templates, a pull
  request checklist, Dependabot for NuGet and Actions, and CODEOWNERS. The README's licence section
  now states GPL-3.0-or-later, the use-at-your-own-risk warning and the non-affiliation disclaimer.
- **Page transitions and cached pages (U6)**: the shell now creates one visual per page on first
  use and reuses it, so revisits keep their list/scroll position (verified: Memory's relief list
  stays at 60 % after leaving and returning) and no longer rebuild the whole page. Page changes
  fade and slide 16 px along the nav direction (incoming 200 ms, outgoing 120 ms), reduced motion
  keeps only the cross-fade, motion-off swaps instantly. The rail draws one selection pill behind
  the items and slides it between them (render transform only), the active icon scales to 1.08 and
  tints gold. Pages are pre-warmed one per idle dispatcher turn after the shell appears, and
  revisit data refreshes start after the 260 ms transition instead of inside it.
- **Splash screen (U5)**: a borderless 480×300 splash now appears before the shell and reports the
  real startup stages (loading settings, preparing services, applying the theme, building the
  shell) on a thin gold progress line. Its four brand petals fly in and assemble, light sweeps the
  emblem and the wordmark fades up; reduced motion shows the assembled emblem with fades only, and
  motion-off paints the final frame. Version is read from `AppInfo`, first run adds a "Welcome"
  chip, and the window is skippable with a click or Esc. It stays visible at least 600 ms, closes
  by 3 s, and fades into the shell; `--no-splash` skips it and Settings → Appearance can turn it
  off permanently. Startup is now staged rather than one synchronous block, so the splash paints
  before the main window is built.
- **Motion kit (U4)**: one `IMotionPolicy` decides between full motion, fades-only and none,
  from the Windows animation switch, a new "Reduce motion" setting, battery saver, a remote
  session, Low Resource Mode and window visibility. Every primitive in the new
  `Motion` namespace consults it: fade, slide, scale, spring, stagger (30 ms, capped at 12
  items), count-up, progress tween, shake, pulse, shimmer, press ripple, hover lift and the
  icon tilt. Only opacity and render transforms are animated, never layout. Page changes and
  the command palette now go through the kit, gold buttons and action cards use the ripple
  and hover-lift behaviours, and `PCMANAGER_FRAME_TRACE=1` logs the page-transition frame
  pacing for the 16 ms budget check.

### Changed
- **Icon3D applied across the shell (U3)**: the navigation rail (compact and expanded, with a
  gold tint for hover and selection), all seven page headers, Home's tip, boost, quick-action,
  one-click-cleanup and capture cards, Protection's status banner and four tool cards, the
  Defender/Update detail cards, Memory's header, relief banner and app-relief card, every
  Storage card heading and the Cleanup Center buttons, the Apps section headings, the Settings
  cards and the floating toolbar (RAM tile, Boost, screenshot, Notepad, calculator, Task Manager
  and Open) now draw the layered `Icon3D` artwork. Icons inside gold primary buttons use the
  neutral tint so they stay readable. The tray menu's Open and Boost-now entries render the same
  artwork through `IconRasterizer`, so the tray no longer uses a second icon style.
- **Removed the superseded icon set**: `Styles/Icons.xaml` now holds only the brand emblem and
  the 12 px title-bar pin glyph; the flat `Icon.*` geometries and the 2.5D `Hero.*` /
  `HeroSmall.*` drawings are gone, along with the unused `IconKeyToGeometryConverter` and its
  `IconKey` model fields. Navigation items and toolbox tiles carry `Icon3DKey` names instead.
- The Apps search box moved below the "App management" heading, so the heading and search field
  no longer overlap at 560 px.
- Settings → Appearance gained "Show splash screen" and "Reduce motion (fades only)" switches next
  to the theme toggle; both are stored in `settings.json` and apply immediately without a restart.
  The shell now starts with `ShutdownMode.OnExplicitShutdown` and switches to
  `OnMainWindowClose` when the main window is shown, so the splash can close independently.

### Added
- **Icon3D system (U2)**: 113 hand-drawn icons on a 24 px grid, each a layered vector built
  from `tools/icons/*.svg` by the `tools/IconGen` console generator into frozen
  `PathGeometry` resources under `Assets/Icons3D/` (one dictionary per category, merged by
  `Assets/Icons3D.xaml`). Every icon has a tinted face and an optional graphite part; the
  `Icon3D` control adds the soft shadow, 1 px-down-right extrusion, gradient face, specular
  highlight clipped to the silhouette and the top-left rim, with one upper-left light source.
  Detail follows size: Small below 28 px (flat gradient, no extrusion), Medium 28-55 px
  (extrusion and specular band), Large from 56 px (shadow, deeper extrusion, crescent and rim).
  `Tint` covers Gold/Neutral/Success/Warning/Critical from the `Icon.*` theme brushes, and the
  optional `Animated` hover tilt (+-3 deg, 160 ms) only runs when Windows animations are on.
  Icons expose an automation name derived from their key and never replace a label.
- **Icon inventory** for brand, navigation, Home, Memory, Protection, Storage, Cleanup, Apps,
  Debloat, toolbox/AI, sensors, window chrome and states, listed in the generated
  `Assets/Icons3D.g.cs` catalog.
- **Dev gallery** `FPM.exe --icon-gallery`: every icon at 20/24/32/56/96 px, all three detail
  levels and five tints, with Light/Dark buttons for review. Not reachable from the normal
  shell.
- `Icon3D` palette tokens (`Icon.Gold/Neutral/Success/Warning/Critical.FaceFrom/FaceTo/Depth`,
  `Icon.Shadow`) in both theme dictionaries.
- `IconMetrics` in Core maps size to detail level and builds geometry/automation keys, covered
  by 20 new unit tests (156 total).

### Changed
- **Design tokens (U1)**: `Styles/Spacing.xaml` adds the 4-px scale (4-40) with `Thickness`
  variants plus page, card, row, button, icon-box, radius, title-bar, focus and motion tokens.
  Both theme dictionaries gained elevation surfaces (`Surface.0-3`), interaction state layers
  (`State.Hover` / `Pressed` / `Selected`), the amber ramp (`Amber.50-900`),
  `Gradient.GoldHero`, semantic fill and text pairs, six chart colours, and accent
  hover/pressed/bar brushes. `Card`, `RowCard`, `PrimaryButton`, `SecondaryButton` and
  `ThinProgress` now consume tokens only.
- **New base components** in `Styles/Controls.xaml`: `HeroCard` (the only gold-gradient surface),
  `ListRow` (48 px, separator), `IconButton` (40x40 hit target), `Chip`, `SectionHeader`.
- **Contrast gate** `tools/contrast-check.ps1` checks 24 documented pairs per theme. It caught the
  light theme's amber and emerald bar fills at 1.84:1 and 2.17:1 against the track, so bars use the
  darkened `App.AccentBar` / `App.Success.Fill` / `App.Warning.Fill` (the correction v7 8.2-D3
  prescribes). All pairs pass in both themes.
- **XAML lint** `tools/lint-xaml.ps1` flags literal `Margin` / `Padding` / `CornerRadius` and hex
  colours outside the token files, with `tools/xaml-lint-baseline.json` as a ratchet: 382 existing
  values across 10 view files, which cards U3/U10 must reduce, and `-Strict` for CI once the
  count reaches zero.


### Added
- **Two-phase Cleanup Center** in Storage: a read-only scan builds a numbered table (exact paths,
  measured bytes, file/subfolder counts, age, tier, confidence, running/elevation state), and a
  typed approval gate (`proceed`, `only 1, 2`, `only 3-7`, `keep 1, 2`) produces a plan that is
  confirmed before anything is removed. Invalid or ambiguous commands return a clarification and
  delete nothing. Plans expire after 15 minutes and are discarded on rescan. A dry-run mode logs
  the plan without deleting.
- **Runtime safety guard** (`ISafetyGuard`): canonical paths, rule allow-list roots, hard deny-list
  trees and name fragments, junction/reparse refusal and whitelist precedence, called before every
  file deletion and every rule-root enumeration.
- **Whitelist service** backed by `%LOCALAPPDATA%\FarmanullahPCManager\whitelist.json`; entries
  are excluded from scans and refused by the guard.
- **Whitelist manager** in Settings: list protected paths, add an existing folder or file, remove
  the selected entry, reload from disk and open the app data folder. Trailing separators are
  normalised so the same folder cannot be added twice.
- **Cleanup audit log** at `logs\cleanup_log.txt`, one line per item with timestamp, number, rule,
  paths, bytes and `DELETED` / `SKIPPED_*` / `DRYRUN` / `FAILED`, rotated at 5 MB.
- **Changed-file approval invalidation**: before deleting a planned row, the executor measures
  it again. A byte change beyond 5% / 64 KB, or a file-count change beyond 5% / 3 files, logs
  `CHANGED_SINCE_SCAN` and leaves that row untouched; the user must scan and approve again.
- **Cleanup profiles**: six presets (Quick Clean, Developer Clean, AI Workspace Clean, Browser
  Clean, Windows Clean, Deep Review) tick rows by family. They never select Opt-in or
  running-application rows, and they do not approve or delete anything. Changing any tick after
  review discards the pending plan, so an approval always describes the current selection.
- **Cleanup history and export**: the last 50 completed/dry-run summaries are kept in
  `cleanup_history.json`, the last five are shown in Storage, and the measured report exports as
  Markdown or CSV under `reports\`. History read/write failures are logged and never block cleanup.
- **Cleanup executor** with per-drive free-space before → after, actual bytes versus estimate,
  re-measurement of each folder, and `SKIPPED_ELEVATION` / `SKIPPED_LOCKED` / `SKIPPED_PROTECTED`
  / `CHANGED_SINCE_SCAN` reporting instead of aborting.
- **Cleanup CLI**: `--scan` (deletes nothing), `--clean --ids 1,2`, `--dry-run`,
  `--restore-point` and `--auto-clean`, using the same catalog, parser and executor as the UI.
- **Elevated helper** `FarmanullahPCManager.Helper.exe`: a separate `requireAdministrator` WinExe
  copied next to the UI. It accepts one typed request per launch over a `CurrentUserOnly` named
  pipe, and only `CleanRule` for five fixed Windows targets or `CreateRestorePoint`. No path,
  executable or shell string crosses the pipe. The update-cache command stops and restarts
  `wuauserv`/`bits`; every target is re-checked for path, age and reparse points. UAC decline is
  reported, not retried.
- **Restore-point offer**: a Storage checkbox (default on for Caution/Opt-In plans) and CLI
  `--restore-point` call the helper's fixed `Checkpoint-Computer` command. Failure is stated in
  the report and cleanup may continue; dry runs never create a point.
- **Helper audit log** at `logs\helper-YYYYMMDD.log`, with no paths beyond the fixed rule ids and
  no credentials.
- **Rule catalog** covering Windows temp/logs/update/WER/shader/Prefetch/`$PatchCache$`/Recycle Bin,
  every detected Edge/Chrome/Brave profile, Firefox `cache2`, npm, NuGet, pip, Yarn, pnpm, and the
  detected caches of Antigravity, Freebuff, Cursor, VS Code and Windsurf. Omnirush is reported as
  having no supported cleanup target.
- **Duplicate recycling**: duplicate groups are now confirmed with **full SHA-256** instead of
  edge samples, and each group names the oldest copy as its keeper. **Recycle extras** asks first,
  re-hashes the keeper and every extra immediately before acting, and refuses whitelisted paths,
  reparse points, Windows/Program Files/ProgramData/app-data/package folders, vanished files and
  anything whose size or hash changed after the scan. The only delete path is the shell's
  `SHFileOperation` + `FOF_ALLOWUNDO` (Recycle Bin), and **Open Recycle Bin** sits next to the
  scan button for restoration. Service behavior is unit-tested; the shell move was verified live
  on one temporary file (no real duplicate group recycled through the UI).
- **Restoreable quarantine**: an approval-gate checkbox moves eligible non-elevated cleanup files
  to `%LOCALAPPDATA%\FarmanullahPCManager\quarantine` instead of deleting them. `manifest.json`
  records the original path, stored path, UTC time, size, rule, reason and a SHA-256 for files up
  to 64 MB. Windows/Program Files, databases, keys, cookies and login/session files are refused,
  and a failed manifest write rolls the move back. Storage lists the entries with a per-file
  Restore that refuses an occupied destination or a hash mismatch, plus Open folder and Delete
  quarantined files; Settings sets retention (3/7/30 days) and the size cap (1/2/5/10 GB),
  applied before each quarantined run. CLI: `--quarantine`. Unit tests cover the batch move,
  manifest,   restore conflict, path traversal, database/credential refusal, rollback when the
  manifest cannot be written, and purge by age and size; no real cache cleanup was run in
  quarantine mode. Duplicate recycling tests cover the re-hash, whitelist refusal, a keeper
  outside the group and a shell refusal.
- **Win32 uninstall completion**: the uninstaller is still started from the registry command
  (`MsiExec /I…` rewritten to `/X`), but the result is now based on evidence: the service waits
  up to 10 minutes for the process (or for the Uninstall entry when the shell returned no wait
  handle), then up to 1 more minute for that app's registry entry to disappear. Only then is the
  app reported removed; the Apps list refreshes and any remaining `InstallLocation` is measured
  and reported read-only, never deleted. Compiled and build-verified; no live uninstall was run.
- **Store/MSIX app list** on Apps via a read-only `Get-AppxPackage` query. Framework, resource,
  system and Microsoft PC Manager packages are hidden and refused. The per-row uninstall asks
  Yes/No first, re-queries the exact package, and runs `Remove-AppxPackage` for the current user
  only; the package list is re-read before success is reported, and failures are stated.
  Parser/filtering tested; the compiled listing was exercised on
  this machine (57 eligible packages); actual removal not exercised.
- **Test project** `app/tests/PCManager.Tests` with 73 passing tests for the approval parser,
  safety guard, elevated command contract, changed-file tolerances, history, report export,
  profile matching, whitelist persistence, Store/MSIX JSON filtering and quarantine.
- Documentation: `PROJECT_SPEC.md`, `BUILD_STATUS.md`, `CHANGELOG.md` for the build audit.
- Shared `CardList` style so the Apps lists no longer repeat the same `ItemsControl` template, and
  a single `NavigationItemTemplate` shared by both rail lists.

### Changed
- The cleanup executor's optional quarantine mode reports quarantined bytes/files separately from
  deleted ones, so a run never claims free space for files that are still on disk. Markdown/CSV
  exports gained the quarantined totals and the history keeps the same honest summary.
- Storage's five-category checkbox list was replaced by the Cleanup Center; the same safe Windows
  and browser categories are now rules in one catalog, plus AI/IDE and developer caches. Large
  files gained a 500 MB filter.
- Cleanup scans use bounded parallelism (2–4 workers) instead of one serial pass; a 16-row,
  4.64 GB scan completed in 18.5–22.8 s across runs on the test machine.
- Machine-wide cleanup rules now route through the helper instead of attempting an unelevated
  delete and reporting `SKIPPED_ELEVATION`. Their roots are shared between the rule catalog and
  the helper via `Core/Elevation/ElevatedCleanTargets.cs`, so the reviewed path and the deleted
  path cannot drift.
- View models now use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`) instead of
  hand-written backing fields.
- Settings and Storage open folders through `IToolLauncher.OpenFolder`; `DefaultBrowserService`
  opens Default apps through `IToolLauncher.OpenUri`.
- `RunningAppViewModel` reuses `ProcessInfo.CpuText/MemoryText` rather than re-formatting bytes.
- Root `README.md` rewritten to describe the shipped stack (custom Fluent XAML, no UI framework
  package) and the six screens; `app/README.md` updated for the new project layout.

### Removed
- `DeepCleanupService` and `IDeepCleanupService` (superseded by the rule catalog, scanner and
  executor), plus the unused `CleanupCategory` / `CleanupOutcome` models.
- Dead health-check engine: `Services/HealthCheck/` (service + five scan modules),
  `IHealthCheckService`, `IHealthCheckModule`, `HealthCheckItem`, `HealthCheckSummary`,
  `StatusToBrushConverter`.
- Dead network diagnostics (`INetworkDiagnosticsService`, `NetworkDiagnosticsService`,
  `NetworkAdapterInfo`, `DnsLookupResult`) and taskbar pinning (`ITaskbarPinService`,
  `TaskbarPinService`, `TaskbarPinEntry`).
- Unused storage surface: `ScanFolderAsync`, `RunCleanupAsync`, `EmptyRecycleBinAsync`,
  `GetRecycleBinSize`, `IStorageAnalyzer.ScanFolder`, `FolderSize`.
- Unused model members (`ProcessInfo.Path/MemoryPercent/StartTime/ThreadCount/StartedText/
  ThreadsText/SystemText`, `StartupImpact`, `StartupApp.Path`, `MemorySnapshot.CommitUsedBytes`,
  `DefenderStatus.Available/LastScan`, `UpdateStatus.LastInstalled`, `DriveSpace.Label/DriveType/
  IsReady/UsedPercentText`, `AppInfo.ExecutableName/RegistryRoot/CacheFolder`) and helpers
  (`ByteFormatter.FormatRate`, `ToolboxTool.IsAvailable`, `CleanupCategory.CanClean`,
  `Converters.StatusToBrushConverter`, `Converters.InverseBooleanConverter`).
- Unused styles (`ListView`, `ListViewItem`, `GridViewColumnHeader`, `MetricCard`, `MetricValue`,
  `StatusPill`, `GhostButton`), unused icon geometries, unused converters resources.
- `app/MECHANICS.md` (described the original Microsoft 3.23 build, not this code base).
- Duplicate DPI configuration in `app.manifest` (DPI is set by `ApplicationHighDpiMode`), which
  removes the `WFAC010` build warning.

### Fixed
- Boost no longer claims to have "reclaimed" memory. A working-set trim only asks Windows to page
  out least-recently-used pages, so the result now reports the working sets trimmed, the measured
  before → after available physical memory with its signed delta (negative when the machine is
  busy), and the temp bytes actually deleted. The tray balloon and the log line use the same
  figures, and `MemorySnapshot.ReclaimedBytes` was removed so the old claim cannot come back.
- The Home result caption wraps, so the longer honest sentence is not clipped.
- `ProtectionViewModel` opened Default apps when asked to check Windows Update; it now opens
  `ms-settings:windowsupdate` through the new `ISystemRepairService.OpenWindowsUpdateSettings`.
- Restored `Icon.Storage` and `Icon.Toolbox`, which were pruned during the styles cleanup even
  though they are referenced by string keys from `MainViewModel`.
- Storage settings no longer initialise the large-file threshold twice; the filter pill label is
  derived from the threshold instead of duplicated in a field.
- Home and the floating toolbar share one `ApplySnapshot` helper, so the memory text cannot drift
  between the two surfaces.

### Known gaps carried forward
- **[PARTIAL]** Duplicate recycling is implemented and unit-tested; the shell move to the
  Recycle Bin was verified on one temporary file, but no real duplicate group was recycled from
  the UI and the card was not visually inspected.
- **[MISSING]** Treemap/downloads manager, the new
  AI Tools / Restore / Agents / Feedback / Health Check screens, scheduler, portable mode,
  Urdu RTL, Dark-theme screenshots, installer.
- **[PARTIAL]** Quarantine is implemented and unit-tested with temporary files; the executor's
  quarantine branch has not been run on real cache files, and its cards were not visually
  inspected.
- **[PARTIAL]** Store/MSIX listing and its confirmation-gated current-user removal are implemented;
  no package was actually removed, and the Apps Store list was not visually inspected.
- **[PARTIAL]** Win32 uninstall now waits for the uninstaller, re-checks the Uninstall registry
  entry and reports measured leftovers, but no live uninstall was exercised.
- **[MISSING]** Helper commands for HKLM startup, hosts-file blocking and network reset.
- **[PARTIAL]** The elevated helper was only smoke-tested with `RunAsInvoker` (unknown-rule
  rejection); no UAC-approved run deleted system files, stopped services or created a restore
  point. The scanner and executor share one assembly (no reflection test proves the scanner has
  no delete API); the delete path was only exercised as a dry run; the Cleanup Center, restore-
  point checkbox, profile, history, export and whitelist controls were not visually inspected;
  floating-toolbar position still does not persist; the simultaneous-run test with Microsoft PC
  Manager is still a side-by-side-install check only.

## [0.1.0] - 2026-11-22

First working build of the rebranded application. Commit `8702e7e` ("first commit") was published
to `github.com/farmanullah1/Farmanullah-PC-Manager`; documentation commits `f0bae92` and `9a56711`
followed.

### Added
- **Identity layer** — `app/src/Core/AppInfo.cs` as the single source of `Farmanullah PC Manager`,
  `FarmanullahPCManager` assembly/exe, `3.23.3`, data folder, Run value, protected process names.
  `FarmanullahPCManager.exe` build output; `Global\FarmanullahPCManager_SingleInstance_8A4F2C1E`
  mutex; AppUserModelID `FarmanullahPCManager.Desktop.8A4F2C1E` applied via
  `SetCurrentProcessExplicitAppUserModelID`; tray icon (`NotifyIcon`, tooltip "Farmanullah PC
  Manager", Open / Boost now / Exit).
- **Six-screen shell** — `MainWindow.xaml`: custom title bar, 236 px rail with Home, Protection,
  Storage, Apps, Toolbox and pinned Settings, `DataTemplate`-based view location, shared
  `NavigationService` broker for in-page navigation.
- **Home** — live memory gauge (`GlobalMemoryStatusEx`), temp-size probe, Boost (working-set trim +
  temp purge), Smart Boost with threshold and once-per-minute cap, four quick-action cards.
- **Protection** — Defender status (`MSFT_MpComputerStatus`, fallback `AntiVirusProduct`), Windows
  Update service + reboot-pending detection, four repair cards (notifications, default browser,
  Explorer restart, default apps).
- **Storage** — system-drive bar, deep cleanup over five measured categories, large-file scan with
  10/50/100 MB and 1 GB filters, duplicate detection (size + edge SHA-256, read-only), Storage
  Sense toggle.
- **Apps** — process list with CPU/RAM/End (self and `MSPCManager*` excluded and refused), startup
  entries from Run/RunOnce/Startup folders/Task Scheduler with real toggles, Win32 installed-app
  list with search, version, size and uninstall.
- **Toolbox** — floating always-on-top pill (live RAM, Boost, Screenshot, Notepad, Calculator) and
  six tools: screenshot (all monitors → PNG in Pictures), screen recorder (Game Bar → `psr.exe`
  fallback), calculator, notepad, DNS flush (`ipconfig /flushdns`), quick links.
- **Settings** — Start with Windows through the app's own Run value, Smart Boost enable + 50–95 %
  slider + reset, Light/Dark switch, About block with data folder and log path.
- **Theme** — `Styles/Themes/Light.xaml` / `Dark.xaml` warm palettes, `Styles/Controls.xaml`
  templated cards, buttons, toggle switch, slider, pills; `Styles/Icons.xaml` original vector icons.
- **Services** — `PcBoostService`, `MemoryOptimizer`, `ProcessManager`, `ProcessService`,
  `StartupManager`, `AppStartupService`, `DeepCleanupService`, `StorageAnalyzer`, `StorageService`,
  `StorageSenseService`, `DuplicateFinderService`, `InstalledAppService`, `SystemRepairService`,
  `ToolboxService`, `ToolLauncher`, `DefaultBrowserService`, `DefenderStatusService`,
  `WindowsUpdateStatusService`, `ProtectionService`, `TempFileCleaner`, `BrowserCacheCleaner`,
  `RecycleBinService`, `FolderCleaner`, `SafeFileSystem`, `ProcessRunner`, `AppLogger`,
  `SettingsService`; all registered in `ServiceCollectionExtensions`.
- **Safety** — no writes under `%LOCALAPPDATA%\Microsoft\*` or
  `HKCU\Software\Microsoft\PCManager\*`; no URI protocol registration; temp cleaning limited to
  files older than 48 h; browser cleaning limited to cache folders; failed deletes skipped and
  reported rather than retried blindly.
- **Docs & evidence** — `app/README.md` (architecture + per-page behaviour), root `README.md`
  (stack, architecture, six screens), `app/screenshots/` with one Light-theme capture per screen
  and the floating toolbar.

### Stubbed / placeholder behaviour (intentional)
- Programme-level default-browser changes and pop-up blocking are Windows-blocked from third-party
  apps; the cards open the supported Settings pages instead.
- "Check for updates" reads the local service/reboot state and then opens Windows Update; it does
  not install updates.
- Screen recording is delegated to Game Bar (or Steps Recorder); the app does not capture video
  itself.
- Store/MSIX apps are enumerated and can be removed for the current user after confirmation;
  this removal path was not exercised during verification.
- Duplicate detection reports groups only; there is no delete action.

### Fixed
- Deep-uninstall leftover cleanup exported every registry key to the same `.reg` path, so only the last key was recoverable. Each key now gets its own backup file, and leftover cleanup stops before deleting registry keys when the backup fails.

### Added
- Shell command palette (`Ctrl+K`) with page navigation.
- Maintenance readiness and network diagnostics services with tests.
- v7 theme token aliases in both theme dictionaries.

### Added
- User-started download speed test with configurable endpoint and measured Mbps/bytes/time.
- Pop-up management monitor listing visible windows with a persistent per-app allow list.
- Reversible hosts block through the elevated helper (`# BEGIN FPM` / `# END FPM`) with a timestamped backup before every write.

### Changed
- Home content is capped at 900 DIP and centred on maximized windows, so cards no longer stretch across the whole monitor.

### Added
- Local 30-day sighting counter for the pop-up monitor ("Seen Nx in 30 days"), stored only on this PC.
- Speed-test endpoint is remembered across restarts.

### Changed
- Hosts block apply/remove transforms moved into a shared, tested `HostsFileContent` helper used by both the app and the elevated worker.

### Changed
- Home uses a two-column Bento layout above 760 DIP of content width and caps its content at 1280 DIP, instead of stretching the compact stack across maximized windows.

### Fixed
- Protection no longer clips the update banner heading or the Pop-up management heading at the 560 px window width; the banner text wraps and the section actions moved below the description.

### Added
- Home tips carousel with three built-in tips, dots and previous/next controls.
- One-Click Cleanup entry with live memory and disk status chips linking to Memory and Storage.
