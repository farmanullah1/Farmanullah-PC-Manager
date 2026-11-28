# Progress log

Only verified work is recorded here. Each entry: date, item, command/evidence, result.

## 2026-11-27 — Phase 1

| Step | Evidence | Result |
|---|---|---|
| Read the working protocol (`START` §1–§4) and all six plan documents; read v7 in full | `FPM_FreeBuff_0*.md`, `FPM_Master_Plan_v7.md` | Card list, laws, DoD and safety rules extracted (see `TRACEABILITY.md`) |
| Inspected repository state | `git rev-parse --abbrev-ref HEAD` → `main`; `git rev-parse --short HEAD` → `ccdbff4`; `git status --porcelain` → 0 | Clean tree at the U6 commit; 276 tracked files; no tags; not pushed |
| Recorded toolchain | `dotnet --list-sdks` → 8.0.428, 10.0.404; PowerShell 5.1.26100.10068; Git 2.56.0; Node 22.23.5; Bun 1.4.2 | Documented in `BASELINE.md`; `pwsh` absent, gitleaks absent, Inno not verified |
| Reproduced baseline gates | `dotnet build app/PCManager.sln … --no-restore` → 0 warnings/0 errors; `dotnet test` → 166 passed; `lint-xaml.ps1` → 380/10 files; `contrast-check.ps1` → OK | Baseline is green; recorded in `BASELINE.md` §3 |
| Checked required release artefacts | `ls` probes for LICENSE, THIRD_PARTY_NOTICES, CI workflows | Missing: license, notices, CI, community files (R2/R4 scope) |
| Located reference material | `reference/win11debloat`, `reference/universal-android-debloater` | Present and read-only; installer script `installer/FPM.iss` present |
| Skill inventory | account library list (50 enabled) | No WPF/.NET desktop skill exists; web/cloud skills dominate |
| Skill discovery | `npx skills find wpf`, `dotnet`, `windows desktop` | Only vendor/low-install skills; none suitable (details in `SKILLS.md` §4) |
| Authored two project skills | `docs/readiness/skills/fpm-wpf-verification.md`, `fpm-safety-contract.md` | Written |
| Attempted skill installation | `cloud_manage_skills save` ×2 | **Blocked:** “Your library is full at 50 skills.” Recorded in `SKILLS.md` §5 / R-06 |
| Sandbox-tested skill content | copied both files to `.tmp-skill-sandbox/`; resolved every path/command; scanned for destructive verbs; removed sandbox | 2 files, all paths and commands resolve, no destructive verbs, `Stop-Process` scoped to FPM only; sandbox removed |
| Wrote readiness artefacts | `docs/readiness/*.md`, `AGENTS.md` | README, baseline, skills, tools, decisions, risks, checklist, traceability, phase-2 plan |
| Phase 1 gate | This log + owner confirmation | **Pending owner** (Q-02 and Q-07) |

## 2026-11-27 — Phase 1 gate approved, R2 complete

| Step | Evidence | Result |
|---|---|---|
| Owner answers recorded | owner replies to Q-01…Q-06; Q-07 defaulted | D-015…D-021 written in `DECISIONS.md` |
| **Phase 1 gate (G-1)** | approval of the readiness pack | **approved** |
| R2 licence text | copied the GPL-3.0 text from the read-only UAD reference (`LICENSE`, 674 lines, verified header and tail; gnu.org was unreachable) | `LICENSE` added; “or later” stated in README/notices |
| R2 notices | `dotnet list package` on 2026-11-27; Win11Debloat `LICENSE` read from the reference | `THIRD_PARTY_NOTICES.md` lists the MIT notice verbatim, UAD GPL reference, platform-tools note and all 10 resolved packages |
| R2 community files | files written and staged | `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1), `SECURITY.md`, `.github/ISSUE_TEMPLATE/{bug,feature}.yml`, `PULL_REQUEST_TEMPLATE.md`, `dependabot.yml`, `CODEOWNERS` |
| R2 README disclaimer | patched the outdated “personal and educational” licence section | GPL-3.0-or-later, risk warning, non-affiliation, trademark attribution |
| R2 gate evidence | `dotnet build …` → 0 warnings/0 errors; `dotnet test` → 166 passed | docs-only card, gates re-run and green |

**Next card: R4** (CI workflow, secret scan tool, SDK pin).

## 2026-11-27 — R4 complete (CI + secret scanning + SDK pin)

| Step | Command / evidence | Result |
|---|---|---|
| Installed gitleaks 8.30.1 | release zip from `github.com/gitleaks/gitleaks` + `checksums.txt`; `sha256sum -c` reported **OK**; binary at `%LOCALAPPDATA%\FPM\tools\gitleaks\gitleaks.exe` | `gitleaks version` → **8.30.1** |
| Secret scan, full history | `gitleaks git . --redact --no-banner --verbose --log-opts=--all --report-format json` | **all commits scanned (78 at the R4 commit), no leaks found**, exit 0 |
| Committed scanner wrapper | `tools/secret-scan.ps1` (pinned 8.30.1, checksum-verified download, redacted output, exit code preserved) | CI and local run the identical command |
| Scanner fresh-install branch | temp `-ToolsRoot`, empty tools folder | downloaded, checksum verified, **installed under temp root: True**, scan clean |
| Installed actionlint 1.7.12 | release zip from `github.com/rhysd/actionlint` | `actionlint --version` → 1.7.12 |
| Workflow validation | `actionlint -no-color -oneline .github/workflows/ci.yml` | **exit 0**, no findings |
| CI job 1: restore | `dotnet restore app/PCManager.sln` | exit 0 |
| CI job 1: build | `dotnet build app/PCManager.sln -c Release --no-restore -nodeReuse:false -m:1 -warnaserror` | **Build succeeded, 0 warnings, 0 errors**, exit 0 |
| CI job 1: tests | `dotnet test app/tests/PCManager.Tests/PCManager.Tests.csproj -c Release --no-build -nodeReuse:false` | **166 passed**, 0 failed, 0 skipped, exit 0 |
| CI job 2: lint | `powershell … tools/lint-xaml.ps1` | OK: 380 literals / 10 files, none above baseline, exit 0 |
| CI job 2: contrast | `powershell … tools/contrast-check.ps1` | OK: every pair meets its ratio in both themes, exit 0 |
| CI job 2: icons | `dotnet run --project tools/IconGen -- --check` | “113 icons, 213 geometries, up to date”, exit 0 |
| CI job 3: secret scan | `powershell … tools/secret-scan.ps1` | gitleaks 8.30.1, 78 commits, **no leaks**, exit 0 |
| Working-tree scan (extra) | `gitleaks dir . --redact` | **2 findings**, both `jwt` matches inside `investigation.cany/evidence/…` — an ignored path (`.gitignore:92`), never committed (`git log --all -- investigation.cany` empty). Not a repository leak; see R-16. |
| SDK pin | `global.json` → 8.0.428, `rollForward: latestFeature`; `dotnet --version` in repo | **8.0.428**; restore, Release build (`-warnaserror`) and 166 tests re-run green under the pin |

Not executed locally, by nature: the `actions/checkout` and `actions/setup-dotnet` steps (GitHub-hosted actions). Every `run:` command of every job was executed here with the results above, and the workflow passes actionlint.

**R4 gate: met.** Next card: **W1** (window chrome + frame budget), per the approved order.

## 2026-11-27 — W1 implementation (frame gate blocked by the display)

| Step | Evidence | Result |
|---|---|---|
| Shared base | new `Views/FpmWindow.cs`: DWM rounded corners (Win11) with square fallback, dark system frame that follows the app theme, Alt+Space system menu, WM_NCHITTEST → `HTMAXBUTTON` for Snap Layouts | builds 0/0 |
| Layered window removed | `MainWindow` no longer sets `AllowsTransparency`; background is the solid `App.Background`; the clipped shell drop shadow is gone | composition no longer software-forced |
| Chrome metrics | title bar 40 px, caption buttons 46×36 with automation names `Minimise` / `Maximize or restore` / `Close` | verified by UI Automation |
| Windows on the base | `MainWindow` and `IconGalleryWindow`; splash and floating toolbar stay borderless by design | — |
| Window controls | UIA invoke: maximize → `IsZoomed=True` (700→1936 px), restore → `False`, minimise → `IsIconic=True` | pass |
| Nav automation names | were the CLR type name (`PCManager.ViewModels.NavigationItem`); now `Home … Settings` via the container style | defect found and fixed |
| 560 px overlap | “Start with Windows” and “Smart Boost” toggles overlapped their text at 560 px; cards now use two columns | defect found and fixed |
| Visual regression | Light/Dark at 560 and 1280; both themes render correctly, no clipping after the fix | reviewed |
| Frame trace | `PCMANAGER_FRAME_TRACE=1`, page switches: 9–14 frames per ~280 ms, worst frame 32.0 / 32.1 / 42.9 / 91.6 / 275.3 ms | **over the 16 ms budget** |
| Why | `Render tier 2; DWM composition on.` but `Win32_VideoController.CurrentRefreshRate = 40` (AMD Radeon, 1920×1080) | 25 ms per refresh: 16 ms is unreachable on this panel |
| Gates | build 0/0, 166 tests, lint **376** (baseline ratcheted), contrast OK, IconGen unchanged | pass |

**W1 is implemented and green except the frame-budget acceptance, which this 40 Hz display cannot evidence.**
**Q-08 answered by the owner (“continue”): accept the refresh-limited result.** W1 gate: met with that
caveat recorded (R-17); 16 ms must be re-measured on 60 Hz hardware before it is claimed anywhere.

## 2026-11-27 — W2 complete (follow the user across virtual desktops)

| Step | Evidence | Result |
|---|---|---|
| Documented API only | `Services/Desktop/VirtualDesktopManagerApi.cs`: COM `CLSID_VirtualDesktopManager`, `IsWindowOnCurrentVirtualDesktop`, `GetWindowDesktopId`, `MoveWindowToDesktop`; no `IVirtualDesktopPinnedApps` | COM instance created at runtime (log: “Virtual desktop tracking started”) |
| Follower logic | `Services/Desktop/VirtualDesktopFollower.cs`: skip when disabled/hidden/minimised, skip when already current, take the foreground window's desktop id, never guess when it is unknown, move without activating | 7 unit tests: move, already current, disabled, no handle, unknown id, failed move, dispose |
| 500 ms loop | `PCManager/Services/DesktopFollowLoop.cs`, UI-thread `DispatcherTimer`, one per followed window; logic stays WPF-free in Services | starts on `SourceInitialized`, disposed on `Closed` |
| Settings | `MainWindowFollowDesktops` (default **off**), `ToolbarFollowDesktops` (default **on**); “Virtual desktops” card in Settings explains the no-pin limitation | toggling “Toolbar follows me” wrote `ToolbarFollowDesktops: false` to `settings.json` |
| Toolbar wiring | `FloatingToolbarWindow` follows while `IsVisible` | wired |
| Gates | build 0/0, **173 tests** pass | pass |

Not checked: a real desktop switch (that would create a desktop on this machine) and Windows 10. The
move path is exercised by unit tests against the interface, and the COM activation is proven; the
switch itself is recorded as R-18 until it can be tried on a scratch desktop.

### W1 addendum — per-monitor/per-DPI placement (2026-11-27)

The card (02 §W1) does require “remember size/position per window type, per monitor/DPI; clamp to
visible area on restore”, and the first pass only remembered one set of values. Now implemented:

- `Core/Models/WindowBounds.cs`: `WindowBoundsTable.Key(device, dpi)`, `Resolve` (exact monitor+DPI,
  then the same monitor at another DPI, else nothing) and `Clamp` (work area, minimum size).
- `AppSettings.WindowBoundsByMonitor` persists a placement per monitor device and DPI; the legacy
  `MainWindow*` fields remain as the fallback for older settings files.
- `PCManager/Services/MonitorIdentity.cs` supplies the monitor device and effective DPI;
  `MainWindow` saves on close and restores before showing.
- 7 unit tests (`WindowBoundsTableTests`).
- Runtime: moved the window to 60,50 700×975 px, closed it; `settings.json` gained
  `\\.\DISPLAY1@120` → `left=48 top=40 w=560 h=780`; relaunch restored 60,45 700×975 px
  (same placement at 120 DPI).

## 2026-11-27 — W3 complete (toolbar survives closing the app)

| Behaviour | Evidence from a real launch | Result |
|---|---|---|
| Close to tray | main window closed with `WM_CLOSE`: `alive=True mainVisible=False`, visible windows `FPM Toolbar`; log “Main window closed to the tray; the toolbar keeps FPM running.” | pass |
| Restore toolbar | startup now shows the toolbar when the setting is on (a gap found by the first probe, fixed) | pass |
| Single-instance hand-off | second `FPM.exe`: `exited=True exitCode=0`; first instance `mainVisible=True`; log “Single-instance hand-off received; showing FPM instead of starting a second process.” | pass |
| `--toolbar-only` | `alive=True mainHandle=0`, visible window `FPM Toolbar`; log “Toolbar-only mode started (requested on the command line)”; working set 124.6 MB, private 50.2 MB | pass |
| Hand-off from toolbar-only | normal launch while toolbar-only ran: launcher exited, toolbar-only exited, a new process appeared with a main window; log “Hand-off: starting the full UI and ending toolbar-only mode.” | pass |
| Toolbar × | sets `ShowFloatingToolbar=false` and hides (code path, not clicked in this probe) | not exercised |
| Exit dialog / tray menu | `ExitDialog` with three choices and “Remember my choice”; `AppLifetimePolicy` unit-tested (8 tests) | dialog not clicked headlessly |
| Sign-in mode | `AppStartupService.SetEnabled(enabled, toolbarOnly)` writes `"exe" --toolbar-only` when the toolbar is on | registry write not exercised |

Followed by a build 0/0 and **188 tests** passing. Remaining gaps are listed in `RISKS.md` (R-19, R-20)
and were not implied as passed.

## 2026-11-27 — W4 complete (toolbar edge docking)

| Step | Evidence | Result |
|---|---|---|
| Geometry rules | `Core/Models/ToolbarDock.cs`: 24 px snap threshold, 32 px strip, centred tab, expanded drawer clamped to the work area | 6 unit tests |
| Docked tab | restored with `ToolbarDock=Right`: window rect **40×210 px** at x=1880 on a 1920-wide screen (32×168 DIP at 120 DPI), flush right; log “Toolbar restored docked to the right edge.” | pass |
| Hover expansion | pointer over the tab: **442×105 px** at x=1438 with the full tool drawer (Shot / Note / Calc / Task / Open) | pass |
| Auto-collapse | pointer away for 3 s: back to **40×210 px** at x=1880 | pass |
| Pin / opacity / topmost / reset / hide | pin persists in settings and blocks auto-collapse; context menu with opacity slider (60–100 %) and presets, always-on-top toggle, reset, hide | menu items not clicked headlessly |
| Drag away | dragging the expanded toolbar away from the edge calls `Undock()` and keeps the new floating position (code path) | gesture not simulated |
| Never steals focus | `FloatingToolbarHost` no longer calls `Activate()`; window has `ShowActivated=False` | pass by inspection |
| Fullscreen hiding | 1 s poll of `SHQueryUserNotificationState`; hides on busy / D3D fullscreen / presentation mode and restores afterwards | not exercised |
| Persistence | dock edge, pin, opacity, topmost and floating position stored per monitor + DPI (`ToolbarBoundsByMonitor`) | save path wired |
| Gates | build 0/0, **194 tests**, lint 376, contrast OK | pass |

## 2026-11-27 — W5 complete (Boost inside the toolbar), U7 host delivered

| Step | Evidence | Result |
|---|---|---|
| Shared state machine | `Core/Models/BoostPhase.cs` + `Services/Boost/BoostCoordinator.cs`: Idle → Measuring → Working → Completed/Failed, one run app-wide, duplicate presses ignored | 6 tests (manual, automatic, rule, failure, duplicate, change notifications) |
| Confirmation rule | `BoostPolicy.AllowsTempCleanup`: only an explicit **manual** Boost deletes temporary files; Smart/Automatic trims working sets and touches no files | tested |
| Shared host | `Controls/BoostAnimationHost`: rocket, flame, pulse while measuring, bob/flame while working, count-up result chip, shake on failure, `IsCompact` 0.6×, reduced motion falls back to fades | used on Home and in the toolbar |
| Toolbar-only / hidden window | main window closed to tray (alive=True, mainVisible=False), then Boost invoked from the toolbar via UI Automation | Boost ran: log “Boost (Manual): 66 working sets trimmed … 2 MB temp”, toolbar chip “2 MB freed” |
| Result chip → Home | hand-off back to the full UI, Home read from the live automation tree: “2 MB of temporary files released.” and “66 working sets trimmed · 2 MB temp removed · …” | pass |
| Working state capture | 250 ms after the press: rocket up, headline “Releasing temporary files…”, Boost button disabled | screenshot |
| Real temp file | probe created `%TEMP%\fpm-w5-probe.bin` (2 MB, 3 days old); the run removed it (2 MB reported) | pass |
| Bug found and fixed | result chip stuck at “0 B freed” because the byte binding arrived after the phase; `OnFreedChanged` now starts the count-up when the phase is already Completed | fixed, re-verified “2 MB freed” |
| Gates | build 0/0, **200 tests**, lint 367 (baseline ratcheted down), contrast OK | pass |

**U7 status:** the shared host, state machine, count-up and shake exist and are used by two surfaces, but
U7's reduced-motion capture and an explicit cancel affordance are still outstanding, so U7 is **not**
marked done. W5 itself is done: same rule, same state machine, works in toolbar-only / hidden-main.

## 2026-11-27 — T1 complete (sensor service + battery)

| Step | Evidence | Result |
|---|---|---|
| Typed reading | `Core/Models/SensorModels.cs`: `SensorStatus` (Ok/Unavailable/NeedsPermission/Error), `BatteryReading` with nullable percent/time/health, `Reason`, `Source`, `SampledAt`; no default numbers | model in place |
| One sampler | `Services/Sensors/BatterySensorService` + `ISensorService`: single `Timer`, 5 s while a surface is registered visible, 30 s otherwise, `SetDemand` from the main window and the toolbar | sample A `16:25:09` → sample B `16:25:19` (5 s cadence), then 5 s steps in the log |
| Probe | `WindowsBatteryProbe`: `GetSystemPowerStatus` (percent, AC, charging flag, lifetime; 0xFFFFFFFF = unknown) plus `root\WMI` `BatteryStatus`/`BatteryStaticData`/`BatteryFullChargedCapacity`; capacities cached once | live: 100 %, AC, health 100 % |
| Honest absence | No battery → `Unavailable` + “No battery detected”; unknown state → `Unavailable` + “Windows did not report a battery state”; unknown time → “Calculating…”; rate-based time labelled “(estimated)” | unit tests |
| Shared presentation | `ViewModels/BatteryStatusViewModel` (one subscription) + `Controls/BatteryTile`; Home adds the tile inside the Boost card, toolbar adds it at compact scale, Settings always shows it with the sample time | screenshots |
| Runtime: Home | 100 %, “On AC power”, “Fully charged”, “Health 100 %” in the Boost card | screenshot |
| Runtime: toolbar + hidden main | toolbar pill shows “100% On AC power”; after closing the main window to tray the same tile still reads 100 % | screenshot |
| Runtime: toolbar-only | `--toolbar-only` process shows the battery tile at 100 % | screenshot |
| Runtime: Settings | “Battery” card with 100 %, Fully charged, Health 100 %, `Sampled 16:26:17` (updates on the shared cadence) | screenshot |
| Bug found and fixed | `Run Text="{Binding Battery.TimeText}"` defaulted to TwoWay on a read-only property → unhandled UI exception, main window never appeared; fixed with `Mode=OneWay` | fixed, re-verified |
| Gates | build 0/0, **208 tests**, lint 367 (unchanged, none added), contrast OK | pass |

**Not exercised (recorded in RISKS R-23):** this laptop has a battery on AC at 100 %, so the live
no-battery desktop path, live discharge “time remaining”, charging “time to full”, and the
`NeedsPermission`/`Error` visuals were only covered by fixtures, not by a real capture.

## 2026-11-27 — T2 landed (temperatures, unprivileged path); elevated hardware sensors open

| Step | Evidence | Result |
|---|---|---|
| Typed reading | `Core/Models/TemperatureModels.cs`: name, °C, source, `Status`, `Reason`, `SampledAt`; never a bare number | model in place |
| Central rules | `Core/Formatting/TemperatureText.cs`: thresholds 70/85 °C, °C/°F conversion, unit symbols, zone labels and reasons; `SensorCadence` reused | tests |
| Shared sampler | `SensorService` now samples battery **and** temperature on the one existing timer; `ISensorService.Temperatures` + `TemperatureChanged`; the same `SetDemand` owner set drives it | sample A `16:36:50` → B `16:36:55` |
| Probes | `WindowsTemperatureProbe`: NVIDIA `nvml.dll` when present, then ACPI `root\WMI MSAcpi_ThermalZoneTemperature` (tenths of Kelvin converted, implausible zones skipped); `ManagementException.AccessDenied` → `NeedsPermission` with a reason | log: `Temperature: NeedsPermission: Windows blocks thermal zone access…` |
| Labels | ACPI zone is `System thermal zone` with source in the tooltip; nothing calls it a CPU temperature | Home/toolbar screenshots |
| Shared visual | `ViewModels/TemperatureStatusViewModel` + `Controls/TemperatureTile` (compact toolbar variant keeps the zone label); Home, toolbar and Settings bind to the same object | screenshots |
| Runtime: Home | `Unavailable · System thermal zone · Windows blocks thermal zone access; hardware sensors need administrator rights` | screenshot |
| Runtime: toolbar / hidden main / toolbar-only | same state in the toolbar, after closing the main window to tray, and in `--toolbar-only` | screenshots |
| Runtime: Settings | Temperature card with Unavailable + reason, `Show Fahrenheit (°F)` toggle and `Sampled 16:36:50` refreshing on the shared cadence | screenshot |
| Gates | build 0/0, **218 tests**, lint 367 (unchanged), contrast OK | pass |

**Not exercised / not built:** this machine denies the ACPI thermal zone to standard users and has no
NVIDIA NVML, so a real temperature value was never rendered; present-value, thresholds and °F are
fixture-tested only (R-24). The card's priority-1 **LibreHardwareMonitorLib inside the elevated
helper**, a typed helper `ReadSensors` command, the ADLX source, the performance-counter fallback and
the “Enable hardware sensors” button are **not implemented**: LHM ships a kernel-mode driver and
needs an administrator opt-in and a Defender check that only the owner can approve, so T2 is
**partial** — see DECISIONS D-043 and the owner question at the end of the log.

## 2026-11-27 — D1 complete (Windows debloat catalog port)

| Step | Evidence | Result |
|---|---|---|
| Reference read | `reference/win11debloat` (MIT, read-only): `Config/Features.json` (106 features, 12 categories), `Config/Apps.json` (162 apps, 2 presets), 252 `.reg` files, `Scripts/Helpers/Get-RegFileOperations.ps1` for parse/detect semantics | mapped |
| Generator | `tools/debloat_catalog_gen.py` (our code, committed) parses the reference data into typed actions; nothing executes reference PowerShell | reproducible |
| Catalog | `app/src/Core/Assets/debloat/catalog.json` (embedded, versioned): **90 tweaks**, **161 apps**, 2 app presets, 16 excluded features, 1 protected app dropped | validator + tests |
| Typed actions/probes | `RegistrySet`, `RegistryDelete`, `RegistryKeyDelete`, `OptionalFeature`, `AppxRemove`; probes `RegistryEquals`, `RegistryAbsent`, `RegistryKeyAbsent`, `OptionalFeature`, `AppxAbsent` (polymorphic JSON) | schema tests |
| Safety contract | validator rejects unknown hives, paths outside `Software`/`System`/`Control Panel`, `HKU` outside `Default`, missing detect, missing revert without a note, protected packages, duplicate ids | 8 tests |
| Feature map | `docs/dev/win11debloat-feature-map.md`: 106 rows = **90 ported + 16 intentionally excluded**, each exclusion with a reason | parity verified by script |
| Exclusions | Edge force-removal (destructive), Start layout replacement (4 features, needs bundled templates), Store-DB mutation, app selection/flow features, `HKEY_CLASSES_ROOT` feature, service-SID HKU feature | reasons in map |
| Apps | All 161 reference apps with recommendation/removal method; `Microsoft.WindowsStore` dropped because FPM's denylist forbids offering it (reference marks it “unsafe”) | test asserts denylist absent |
| Gates | build 0/0, **226 tests**, lint 367 (unchanged), contrast OK | pass |

**Not done in D1 (by scope):** no planner/executor/journal (D2), no UI (D3), nothing touched the real
registry, and no reference tweak was applied. Descriptions are generated from the reference file and
feature id; curated “why/what changes/what breaks” text belongs to D3. The reference is not version
pinned, so the generator must be re-run if the folder is updated (R-25).

Next card: **D2** (planner, helper executor, journal/undo).

## Earlier entry

## Carried state from the last code cards

- U5 splash committed; U6 page caching/transitions committed as `ccdbff4`.
- Last verified gates: build 0/0, 166 tests, lint 380, contrast OK.
- Frame trace after U6: 11–12 frames per 280 ms window, worst frame 32–45 ms — the multi-second
  page stalls are fixed; 60 fps remains blocked by the layered-window decision (Q-04).
- Settings restored to Light, `ReduceMotion=false`.

## Blockers

| Blocker | Impact | Resolution |
|---|---|---|
| Skill library full (50/50) | The two skills cannot be installed into the account library | Owner frees two slots (Q-02) or accepts repo-only skills |
| No `gitleaks`/`trufflehog` | Secret scan cannot be evidenced for R4 | Install before W0 gate |
| Only 125 % DPI verified | UI scaling claims at 100/150/200 % unproven | Verify in W1/U10 |

## Next step

Owner answers Q-01…Q-07; then the Phase 1 gate is marked approved and Wave 1 starts with **U7**
(boost animation), per the default order in `PHASE2_PLAN.md`.

## 2026-11-27 — D2 complete (typed debloat engine, journal, undo)

| Step | Evidence | Result |
|---|---|---|
| Planner | `DebloatPlanner`: ordered by catalog category then id; refuses unknown ids, `RegistryKeyDelete`, `AppxRemove` (Apps flow), protected packages, and OS-gated tweaks; exposes reboot/Explorer flags | 3 tests |
| Helper surface | `HelperCommand.ApplyTweak/RevertTweak/DetectTweak`, `HelperRequest.TweakId/Restore`, `HelperResponse.RolledBack/Detected/Values`; `DebloatCommands` validates ids against the embedded catalog, captures pre-state, applies typed registry/optional-feature actions via fixed `dism.exe` args, self-rolls-back on failure | key-delete/Appx refused helper-side too |
| Revert safety | Revert accepts only the journal's pre-state limited to that tweak's own registry targets (≤128 entries) and the hive allow-list; no free-form path reaches the helper | validation in `DebloatCommands` |
| Engine | `DebloatEngine`: plan → detect-all (skip already applied) → `.reg` backup → apply sequentially → journal after each tweak; on failure reverts applied entries in reverse and records `RolledBack`; undo replays the journal in reverse and refuses stale/rolled-back runs | journal at `%LOCALAPPDATA%\FPM\debloat\journal`, backups at `…\debloat\backups` |
| Restore point | attempted only for non-Low-risk plans; a decline/failure is recorded as `Skipped: <reason>`, never faked | fake test records `Skipped` |
| Transport | `ElevationService.RunDebloatAsync` reuses the one-shot pipe transport extracted into `ExchangeAsync`; declined UAC maps to a cancelled outcome | existing elevation tests still green |
| UI surface | Settings card “Windows debloat (engine preview)” with tweak picker, computed plan line, Detect/Apply/Undo; `DebloatEngineViewModel` is registered in DI | UI probe found the card and `Plan: ready` |
| Tests | `DebloatPlannerTests` (ordering, OS gate/unknown, key-delete/Appx/protected) and `DebloatEngineTests` (apply→undo exact restore, mid-run failure rollback, journal round-trip, backup file) against an in-memory registry, never the live registry | **232 tests**, 6 new |
| Gates | build 0/0; tests 232 passed; lint 367 (unchanged); contrast OK | pass |
| Runtime | launched `FPM.exe`; `tools/ui-probe-debloat.ps1` selected Settings and read the card + plan text | card renders; **UAC not approved**, so live detect/apply/undo was not run |

**Not done in D2 (by scope):** no Apps flow or presets UI (D3), no other-user/sysprep/CLI (D4), no
key-deletion or Appx executor (still planner-refused), no Explorer restart or reboot prompt, and no
elevated run against the live registry. Journal/backup files hold user-specific pre-state and stay
out of the repo. The `.reg` QWord write uses `hex(b)`; it is an auxiliary human-readable backup, not
the undo source (the journal is).

## 2026-11-28 — D3 complete (full Debloat page)

| Step | Evidence | Result |
|---|---|---|
| Navigation | `PageKeys.Debloat`, a seventh rail entry (“Debloat”, icon `Debloat`), a Ctrl+7 palette entry and a review command; `MainWindow` maps the page to `DebloatView` | reachable from the shell |
| Windows tab | OS/build header, rule-based presets (Recommended safe, Privacy focus, Performance, Minimal, Custom), category list with counts, search, risk/impact labels, unsupported rows disabled with the planner's reason, applied badges from the journal | `DebloatView.xaml`, `DebloatViewModel` |
| Plan/review | Selection becomes a typed plan through `DebloatEngine.Plan`; summary shows selected/ready/skipped and Low/Medium/High counts plus reboot/Explorer flags; review lists typed target changes and exposes **Apply selected** | plan probe below |
| Detect/Apply/Undo | Detect selected reads through `DebloatEngine.DetectAsync`; Apply uses `ApplyAsync` with the new restore-point toggle; Undo replays the engine journal in reverse; report distinguishes success, failure, permission decline and partial rollback (chip) | report unit tests |
| History | `DebloatJournalStore.LoadAll` now returns up to 20 records; the page shows the latest 10 with the engine's latest-run undo | store/engine tests |
| Apps tab | Searchable catalog of 161 preinstalled apps with recommendation/removal-method labels. Removal is explicitly not offered: Appx is still refused by the planner/helper until package state can be journaled (D-051/D-054) | `Apps tab visible` probe |
| States | no catalog → error card; empty selection → plan text; running → indeterminate progress + UAC explanation; report → success/declined/partial-rollback; history | XAML bindings |
| Tests | 11 new tests: preset rules, category+search filtering, plan impact summary, review text, run-report success/rollback/decline, and the restore-point-off path | **243 tests**, 11 new |
| Gates | build 0/0; tests 243 passed; lint 367 (new page at 0 literals); contrast OK | pass |
| Runtime | launched `FPM.exe`; `tools/ui-probe-debloat.ps1` selected the Debloat rail item, read `Plan: 53 selected · 53 ready · 0 skipped · risk 53 Low · reboot required · Explorer restart`, then opened the Apps tab and found “Preinstalled apps” | page renders and computes from the embedded catalog |

**Not checked / not delivered in D3:** no UAC-approved detect/apply/undo was run, so helper registry writes, rollback and DISM remain fake-executor evidence only (R-29). The Apps tab has no live installed detection or removal (R-31). Review shows typed target changes, not per-value old→new diffs (R-32). Detect/Apply can raise one prompt per tweak because the helper is one-shot (R-30). To run the UIA probe, the floating toolbar was temporarily disabled in local settings (the toolbar's desktop-following kept the main window off the probe's desktop); the settings file was restored immediately afterwards.

Next card: **D4** (other-user/Default hive, CLI, preset import/export, drift check) and then **D5** (parity tests).

## 2026-11-28 — D4 complete (typed targets, preset files, drift, CLI)

| Step | Evidence | Result |
|---|---|---|
| Target model | `DebloatTargets.cs`: tokens `current`/empty/current SID → HKCU; `default` → `HKU\FPM_Default` with a Windows 10 build gate and SYSPREP warning; `sid:<sid>`/`user:<name>`/bare name → live `HKU\<sid>` when loaded, otherwise offline `HKU\FPM_<sid digits>` with `NTUSER.DAT`; malformed/unknown profiles refused | resolver + hive tests |
| Hive lifecycle | `IDebloatHiveApi`/`DebloatHiveScope`; helper `HelperHive.cs` resolves profiles from its own ProfileList and uses `RegLoadKey`/`RegUnloadKey`; a scope always unloads in `Dispose`, including after an apply exception, and reports an unload failure | fake-API load/unload success + failure tests |
| Helper mapping | `DebloatRegistry.cs` maps logical catalog `HKCU` to the selected live/offline HKU root, reads/writes/restores values, and keeps logical HKCU names in the journal; helper request carries only `TargetUser`, never a path | helper + engine target propagation tests |
| Target in engine/journal | `DebloatEngine.DetectAsync/ApplyAsync/UndoAsync` pass `targetUser`; `DebloatRunRecord.TargetUser` is written and undo/detect reuse it; old journals with no target still mean current user | target propagation test |
| Page surface | Debloat Windows tab gained a typed **User target** picker with profile refresh, honest live/offline/SYSPREP warnings, **Export preset**, **Import preset**, **Check drift** and a preset-file/result line; Apps rows are selectable for preset files only | UIA probe below |
| Preset files | `fpm-debloat-preset.json` version 1 with `tweaks` and `apps` arrays; serialize/parse in `DebloatPresetFile`, unknown ids dropped with warnings, unsupported version/bad JSON refused, round-trip tested | 2 tests |
| Drift | Manual report-only check re-detects the latest journaled run at its recorded target; `DebloatDrift.Summarize` says what changed and explicitly says nothing was reapplied | drift summary test |
| CLI | `DebloatCli` shares `DebloatEngine`/journal; `--preset/--tweaks/--user/--dry-run/--silent/--accept-risk`; silent requires accept-risk; plan is printed before any write; exit codes 0/1/2/3/4 documented in the class and usage | `FPM.exe --debloat --preset minimal --dry-run` exited 0, target “This account”, `Plan: 14 selected, 14 ready, 0 skipped`, no prompt |
| Tests | 17 new D4 cases across target resolution, hive cleanup on failure, preset round-trip/version, CLI gates, target propagation into detect/apply/undo and drift summary | **260 tests passed**, 0 failed |
| Gates | build **0 warnings / 0 errors**; `lint-xaml.ps1` **367** (baseline unchanged); `contrast-check.ps1` OK in both themes | pass |
| Runtime probe | toolbar temporarily disabled, app launched, `tools/ui-probe-debloat.ps1` read `debloat page visible`, the plan, target status, `Changes apply…`, preset-file line, refreshed profiles, found all three D4 commands, then opened Apps and read `apps tab visible` | page renders D4 surface; probe exit 0 |

**Not checked / gaps:** no UAC prompt was approved, so no live other-user hive load/unload, no Default/SYSPREP write, and no live detect/apply/drift run (R-28/R-33). The hive API and unload path are fake-API tests only. `--apps` is parsed but deliberately refused because Appx removal still cannot be journaled (D-051/D-063); the CLI cannot remove apps. The drift check is manual, not scheduled, and each detect can raise its own UAC prompt (R-35). The probe read the default target and the presence of the commands; it did not select another profile or open a file dialog. Settings were restored after the probe.

Next card: **D5** (parity/schema tests).

## 2026-11-28 — ignore policy reconciled (mission deliverable audit)

| Step | Evidence | Result |
|---|---|---|
| Read the source protocol | `docs/freebuff/FPM_FreeBuff_00_START_HERE.md` says the six plan copies are **git-ignored by design**; `docs/dev/REPO_AUDIT.md` records the same policy | local-only plan inputs are intentional, not a missing deliverable |
| Replaced the ignore list | `.gitignore` is now a lean deny-list: build/IDE output, packages, installer output, secrets, local app data, reference trees, agent files, local planning/prompt/handoff files, OS noise | no broad root-document rule; public docs are tracked by default |
| Verified tracked files are not ignored | `git ls-files -ci --exclude-standard` | **empty** |
| Verified the source of truth is visible | `git check-ignore -q --no-index` on `docs/readiness/README.md`, `CHECKLIST.md`, `DECISIONS.md`, `RISKS.md`, `TRACEABILITY.md`, `PROGRESS_LOG.md`, `README.md`, `CHANGELOG.md`, `docs/dev/REPO_AUDIT.md` | all **visible**; `docs/freebuff/…` and `AGENT_STATE.md` remain ignored on purpose |
| Recorded the decision | D-066 added to `DECISIONS.md`; `README.md` §1 and `REPO_AUDIT.md` state that `docs/readiness/` is the tracked single source of truth | no mission deliverable is hidden by accident |

## 2026-11-28 — D5 complete (Windows debloat safety/parity tests)

| Step | Evidence | Result |
|---|---|---|
| Conflict refusal | `DebloatConflicts.Find` compares final registry data and optional-feature state; `DebloatPlanner` marks every side `SkippedConflict` with the target and the other tweak ids before elevation | synthetic opposite/identical-value tests; real Spotlight pair test |
| Restore guard | `DebloatRestoreGuard` is shared Core validation for journal pre-state: only the tweak's own targets and the catalog hive allow-list pass | own target accepted; foreign path and `HKCR` rejected; helper delegates to the same guard |
| Reference parity | `DebloatReferenceParityTests` parses `docs/dev/win11debloat-feature-map.md`: 106 unique rows, 90 ported ids exactly equal the catalog ids, 16 excluded ids exactly equal `catalog.excludedFeatures`, every exclusion has a reason | parity is now a test, not a one-time script read |
| Catalog data fix | `tools/debloat_catalog_gen.py` now records dynamic exclusions too; regenerated `catalog.json` (90 tweaks, 161 apps, **16** excluded features) | D1's map already said 16; the embedded catalog had 11. Fixed (D-068) |
| Detection/action invariant | `DebloatCatalogDetectionTests` checks every detect probe against the typed action that satisfies it: `RegistryEquals` matches the set's type/value, `RegistryAbsent` matches a delete, `RegistryKeyAbsent` matches a key delete, optional-feature state matches, Appx absence matches; every action is also detected in reverse | all 90 catalog tweaks pass |
| Existing D5 coverage retained | schema/validator, every tweak has detect + revert, OS gating, key-delete/Appx/protected refusals, fake-registry apply/undo/rollback, journal round-trip, hive cleanup, protected apps, preset round-trip | all green |
| Dry-run no-write | `FPM.exe --debloat --preset minimal --dry-run` → exit **0**, `Plan: 14 selected, 7 ready, 7 skipped`; journal+backup file count **0 → 0** | no elevation, journal or backup file |
| UI probe | launched `FPM.exe`; `tools/ui-probe-debloat.ps1` → exit **0**: `debloat page visible`, `Plan: 53 selected · 21 ready · 32 skipped · risk 21 Low · reboot required · Explorer restart`, target status, target picker, warning, preset-file line, refreshed profiles, all three D4 commands, `apps tab visible` | page computes conflict refusals and still shows the D4 surface |
| Gates | build **0 warnings / 0 errors**; **267 tests passed**, 0 failed; lint **367** (baseline unchanged); contrast OK both themes | pass |

**Not checked / gaps:** no UAC prompt was approved, so no live detect/apply/undo, other-user hive load/unload or Default/SYSPREP run was executed (R-28/R-33). The conflict refusal is unit-tested against synthetic targets and the real Spotlight pair; the page shows all 32 skipped rows but does not yet group alternatives or choose a default (R-37). The dry-run evidence is the built app plus file counts, not an automated CLI process test. Settings were restored after the probe.

Next card: **A1** (ADB service: locate/consent/download/verify adb, device parsing). No user-only blocker for A1; D-043 (LHM) and Q-05 (push/signing) remain owner decisions that do not block it.

## 2026-11-28 — A1 complete (ADB service)

A1 implementation commit: `5eca8a7e19c49779809dbf4b539c0294c8cadcef`.

| Step | Evidence | Result |
|---|---|---|
| Command surface | `Core/Android/AdbCommandLine.cs`: closed enum `StartServer`, `KillServer`, `Devices`, `GetProperties`, `ListUsers`; `ArgumentList` only; serial regex rejects spaces/empty. `AdbParserTests.Command_builder_only_emits_the_typed_a1_surface_and_rejects_bad_serials` | no free-form shell; A2+ must extend the enum |
| Locator/order | `AdbPathResolver` checks configured path → `%LOCALAPPDATA%\FPM\tools\platform-tools` → common SDK locations → PATH, skips invalid paths and does not stop at an unsigned candidate. `AdbServiceTests.Locator_prefers_a_configured_signed_path_and_does_not_accept_an_unsigned_one`, `Missing_adb_and_unsigned_adb_are_distinct_states` | signed local wins when the configured copy is unsigned; missing and unverified are distinct |
| Consent/checksum/signature | `AdbProvisioner` returns `Declined` before touching the source, hashes the archive before extraction, then installs only when the staged `adb.exe` is Google-signed. `AdbProvisionerTests` cover consent, checksum mismatch (extraction/signature not reached), unsigned valid-checksum rejection and a successful staged install. `AdbPackage` pins 35.0.2, the official URL, 6,700,829 bytes and SHA-256 `2975a3ea…300bb9`. | no download without consent; mismatch/unsigned cannot install |
| Server ownership | `AdbService.EnsureServerAsync` watches start-server output; `Shutdown` calls `kill-server` only when FPM saw `daemon started successfully`/`daemon not running`. `No_device…` asserts kill; `Existing_server…` asserts no kill. | no other tool's server is stopped |
| Device parsing/onboarding | `AdbParsers` handles `device`, `unauthorized`, `offline`, `???????????? no permissions`, emulator/wireless/USB, malformed/header lines; `getprop` fields and `pm list users` id/name/flags/work/running. `AdbOnboarding` gives the phone steps and driver link. `AdbParserTests` | every A1 state has typed text, not a fake “ready” |
| ViewModel/UI wiring | `AndroidDebloatViewModel` owns consent checkbox, optional path, `Refresh`/`Use this path`/download commands, devices/profile collections and a 2 s `DispatcherTimer`; `SetVisible` starts/stops it. `DebloatViewModel` adds the Android tab and page-visibility propagation; `DebloatView.xaml` shows status/path/onboarding/license/devices/profiles. | polling is tab-visible-only by construction |
| Build | `dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore` | **0 warnings / 0 errors** |
| Tests | `cd app && dotnet test tests/PCManager.Tests/PCManager.Tests.csproj --no-build -nodeReuse:false` | **287 passed**, 0 failed; 20 A1 tests (parsers/commands/package 4, service 10 including three theory cases, provisioner 4, package-source 2) |
| XAML/theme gates | `tools/lint-xaml.ps1`, `tools/contrast-check.ps1`, `dotnet run --project tools/IconGen -- --check` | lint **367** (baseline unchanged); contrast **OK both themes**; **113 icons / 213 geometries**, up to date |
| Runtime probe | launched the built `FPM.exe`, temporarily set `ShowFloatingToolbar=false`/`CloseToTray=false`, ran `tools/ui-probe-android.ps1` | **exit 0**; `ADB status: NoDevice - No Android device is connected.`; `ADB: C:\Users\farma\AppData\Local\Android\Sdk\platform-tools\adb.exe`; onboarding text and license notice read; controls present: Android device, Official platform-tools download, Accept Android SDK license, ADB path override, Refresh ADB, Download platform-tools, Android devices, Connected devices |
| Ownership runtime evidence | `%LOCALAPPDATA%\FPM\logs\app-20261128.log` | `ADB found via Android SDK…` → `ADB server started by FPM.` → `FPM exiting.` → `ADB server stopped because FPM started it.`; `tasklist /FI "IMAGENAME eq adb.exe"` after the probe: no task |
| Read-only guarantee | `Authorized_device_reads_getprop_and_profiles_with_the_owned_server` asserts the exact call sequence `StartServer, Devices, GetProperties, ListUsers` and serial propagation; no package command exists in the enum | only read-only identity commands were executed; no package action is reachable |
| Settings cleanup | probe backed up `%LOCALAPPDATA%\FPM\settings.json` and restored it | `ShowFloatingToolbar=True`, `CloseToTray=True` back; no FPM process left |

**A1 acceptance:** locate/consent/download/verify adb = covered; typed missing/unverified/declined/checksum/device states = covered; `adb devices -l` parsing including unauthorized/offline/no permissions = covered; getprop identity and `pm list users` profiles = covered; 2 s visible-only polling and multi-device selector = wired; unsigned adb rejected and no bundling = covered. The runtime probe exercised the real locator and real `adb start-server`/`devices` path; it did not have a phone attached.

**Not checked / gaps:** no physical Android device, so no live USB authorization prompt, live `getprop`, live work-profile list, or live unauthorized/offline/no-permissions rendering (R-38). The real download path was not run in the app; consent/checksum/signature use a fake package source (R-39). The new tab was not captured at 360/560/1280 or in both themes (R-40). Poll stop-on-leave is not runtime-measured (R-41). Authenticode revocation is not checked online (R-42). Settings were restored after the probe.

Next card: **A2** (package lists). No user-only blocker for A2. D-043 (LHM) and Q-05 (push/signing) remain owner decisions that do not block it.

## 2026-11-28 — A2 complete (package lists)

**Card acceptance quoted before implementation:** “Typed model + schema + validator for `uad_lists.json`; user-initiated update with diff/hash.” **Acceptance:** “Schema tests; embedded fallback.”

A2 implementation commit: `e33b5cb62a1aab340087261df0a4d24e2129dd3c` (25 files; code, asset, tests, feature map and initial evidence). Cache-reload assertion: `3e5a2cf1804afc9c3e380b45a1360cab5b40aea2`. Evidence follow-up: `f851a41` (docs).

| Step | Evidence | Result |
|---|---|---|
| Embedded data | Copied `reference/universal-android-debloater/resources/assets/uad_lists.json` (commit `11f27c6…`) to `Core/Assets/android/uad_lists.json`; verified **845,509 bytes**, SHA-256 `e25a5087ea1730575e48aefa0d1f65990c0eaffc179c9950867aaf4ffbd85383`; embedded as `PCManager.Core.Assets.android.uad_lists.json` | 2,400 source rows, 2,394 unique package ids, 6 duplicate ids; no reference scripts run |
| Schema/validator | `UadListParser` requires an array of objects with string id/list/description/removal and string arrays for dependencies/neededBy/labels; unknown list/removal, wrong types and malformed roots are rejected; 4,000,000-character bound; `android` plus dotted ids accepted (D-076) | valid reference accepted; invalid fixtures rejected with row-specific errors |
| Duplicate handling | Dictionary normalization last-wins, `SourceRowCount` and `DuplicateIdCount` retained in `UadListCatalog`; six reference duplicates are reported, not hidden | embedded count 2,394; duplicate count 6 |
| Cache/provider | `UadListProvider.LoadAsync` selects a valid cached file, otherwise embedded; `UpdateAsync(false)` makes no request; invalid remote leaves current/cache untouched; identical SHA is `AlreadyCurrent`; successful update writes `%LOCALAPPDATA%\FPM\android\lists\uad_lists.json` through a temporary file and a `.sha256` sidecar | fake-source tests cover cache, fallback, gate, invalid, same, updated, network failure |
| Difference summary | `UadListDiff` compares ids and typed fields; reports added/removed/changed counts, capped sample ids, and duplicate normalization; UI prints the summary | updated fixture reports 1 added / 0 removed / 1 changed |
| UI surface | `PackageListViewModel` loads without network on Android-tab visibility; `Check for list updates` is the only call to `UpdateAsync(true)`; card shows source, 2,394 ids, 2,400 source rows, duplicate count, SHA prefix and the Chinese-OEM warning | A2 card visible on the existing Android tab; no package rows/actions added |
| Build | `dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore` | **0 warnings / 0 errors** |
| Tests | `cd app && dotnet test tests/PCManager.Tests/PCManager.Tests.csproj --no-build -nodeReuse:false` | **302 passed**, 0 failed; **15 A2 tests** (8 parser/embedded, 7 provider) |
| Static gates | `tools/lint-xaml.ps1`, `tools/contrast-check.ps1`, `dotnet run --project tools/IconGen -- --check` | lint **367** (baseline unchanged); contrast **OK both themes**; **113 icons / 213 geometries** |
| Runtime probe | `tools/ui-probe-android.ps1` (same settings backup/restore and recorded-PID cleanup as A1) | **exit 0**; `UAD package list: 2,394 package ids ready`; `Embedded UAD list · 2,400 source rows · 6 duplicate id(s) normalized · SHA-256 e25a5087ea17`; `UAD package list` and `Check for list updates` present; ADB NoDevice path still visible |
| Reference map | `docs/dev/uad-feature-map.md` records the pinned commit, schema, feature → component rows and deliberate differences; `Core/Assets/android/README.md` records provenance/license/hash | A7 can automate parity later; no upstream screenshots/code copied |

**A2 acceptance:** typed model/schema/validator = covered by 8 parser/embedded tests; user-initiated update with diff/hash = covered by 7 provider tests and the UI command; embedded fallback = `LoadAsync` uses it on missing/invalid cache and the probe shows `Embedded UAD list`. The update request is gated by `UpdateAsync(userInitiated: true)` and is not triggered by startup, tab visibility or polling.

**Not checked / gaps:** no live upstream GitHub fetch was performed; the real URL, proxy/timeout behaviour and current upstream compatibility are fake-source evidence only (R-44). No package rows, filters or actions were added (A3/A4). The A2 card was not visually captured at 360/560/1280 or in both themes (R-45). The remote list is HTTPS/schema-validated but not signed (R-46), and six upstream duplicate ids are last-wins (R-43). Settings were restored after the probe.

Next card: **A3** (package engine). No user-only blocker for A3. D-043 (LHM) and Q-05 (push/signing) remain owner decisions that do not block it.

## 2026-11-28 — A3 complete (Android package engine)

A3 implementation commits: `3946e1d` (typed surface, engine, tests, initial evidence), `360c126` (interface split + command tests), `0c50c62` (rules extraction). Final evidence follow-up: pending local docs commit.

| Step | Evidence | Result |
|---|---|---|
| Typed command surface | `AdbCommandKind` now includes package list, uninstall, disable, enable and restore; `AdbCommandLine.Build` validates serial and dotted package ids and supplies argument lists only | no shell/free-form package command |
| Package state model | `AndroidPackage`, `PackageScan`, `PackageActionRequest`, batch/result/warning records; system, enabled, disabled and uninstalled states | metadata joined by package id |
| Engine | `IPackageEngine`/`PackageEngine` scans all/disabled/system lists, applies sequentially, supports disable-instead-of-uninstall, warns on dependencies, re-scans and verifies final state | continue-on-failure; disconnect stop |
| Denylist | `PackageDenylist` protects critical launcher, shell, telephony, settings, installer and system packages; unsafe UAD rows are also denied | exact typed package confirmation required in Expert mode |
| Wiring | `IAdbCommandExecutor` added to `AdbService`; `IPackageEngine` registered in the service collection | no package mutation occurs in ADB discovery/onboarding |
| Tests | `PackageEngineTests` (9) + `PackageCommandTests` (10 cases) cover the four-query state join, malformed package/serial/user rejection, every command's exact argument list, denylist block/exact Expert confirmation, Already without mutation, continue-on-failure, disconnect stop, verification, disable-instead and dependency warnings | **321 passed**, 0 failed (**19 A3 tests**) |
| Build | `dotnet build app/PCManager.sln -nodeReuse:false -m:1 --no-restore` | 0 warnings / 0 errors |
| Static gates | `lint-xaml.ps1`, `contrast-check.ps1`, `IconGen --check` | lint 367; contrast OK Light/Dark; 113 icons/213 geometries |
| Runtime regression | `tools/ui-probe-android.ps1` after the A3 build; A3 has no UI yet (A4 owns it), so this proves the existing Android tab and A2 embedded list still load | **exit 0**; `ADB status: NoDevice`; `UAD package list: 2,394 package ids ready`; embedded 2,400/6/SHA prefix; no FPM or adb process left |

**A3 acceptance:** the typed command builder rejects shell text and malformed ids/users; state is computed from all/system/enabled/disabled queries with UAD metadata; batches return per-package `OK|ALREADY|FAILED|BLOCKED_DENYLIST`, continue after ordinary failures, stop on disconnect, re-query to verify, and emit dependency/needed-by warnings. Denylisted/Unsafe packages require Expert mode plus the exact package id. No real device or package was mutated.

**Not checked:** no physical Android device, USB authorization, live `pm` state or package mutation was exercised (R-38/R-39/R-47); A3 exposes no UI controls, so no A3 screen exists to capture until A4. The runtime probe is an A2-tab regression check only. The background .NET 11 dependency bump was reverted to the last .NET 8 set because it produced unsupported-TFM warnings (D-084).

Next card: **A4** (Android package UI). No owner-only blocker.

## 2026-11-28 — A4 complete (Android package UI)

| Step | Evidence | Result |
|---|---|---|
| VM and package model | `AndroidDebloatViewModel` now owns device/user selection, package scan, filters, review, apply/undo and safety acknowledgement; `AndroidPackageRow` exposes typed metadata and selection automation | no package action executes before acknowledgement and confirmation |
| Package UI | `DebloatView.xaml` Android tab now includes search, list/tier/state filters, selected-only, visible select/clear/invert, virtualized rows, action options, review, apply/undo and result text | controls retain 3D icons and token styles |
| Safety | denylisted/Unsafe rows are blocked unless Expert mode and exact package id match; final MessageBox confirmation; first-run acknowledgement persists in settings | no real package mutation |
| Tests | `AndroidPackageRowTests` plus existing A3 package engine tests | **325 passed**, 0 failed |
| Build/static gates | solution build, XAML lint, contrast check, IconGen check | 0/0 build; lint 367; contrast both themes; 113 icons |
| Runtime probe | `tools/ui-probe-android.ps1` | **exit 0**; NoDevice; A4 device/user/filter/list/review/undo controls found; settings restored |

**A4 acceptance:** the Android tab exposes the package engine through device/user selectors, filters, virtualized rows, review, denylist/Expert confirmation and inverse undo. No live phone or package mutation was exercised.

**Not checked:** real 360/560/1280 captures, both-theme visual review and live device package apply/undo remain unverified (R-45/R-47). A5 owns persistent snapshot/log/export behavior.

Next card: **A5** (Android backup, restore, log and export/import). No owner-only blocker.

## 2026-11-28 — A5 complete (Android backup, restore, log and selection portability)

| Step | Evidence | Result |
|---|---|---|
| Snapshot store | `AndroidBackupService` creates `%LOCALAPPDATA%\FPM\android\backups\<serial-hash>_<user>_<timestamp>.json` from the latest typed package scan | stores package id + enabled/disabled/uninstalled state |
| Restore | restore maps snapshot states to Enable/Disable/Restore and uses `IPackageEngine` batch verification | reports OK, Already, Failed and Blocked |
| Action log | append-only `android_actions.log` with 5 MB rotation | serial SHA-256, model, user, package, action and result; no raw serial |
| Selection portability | `.txt` UAD-compatible export/import and FPM JSON export/import | unknown ids are reported and ignored |
| Recommended preset | Android package UI selects only `Recommended` metadata rows | still requires review and final confirmation |
| Tests | `AndroidBackupServiceTests` covers snapshot JSON, restore inverse mapping, text round-trip, unknown ids and hashed logs | 328 tests total, 0 failed |
| Build/static/UI | solution build, lint, contrast, IconGen and Android UIA probe | 0/0 build; lint 367; contrast OK; icons up to date; A5 controls found |

**A5 acceptance:** automatic snapshot creation, snapshot restore, privacy-safe append-only log,
UAD text and FPM JSON selection portability and the Recommended preset are implemented.

**Not checked:** no live Android snapshot/restore or real action log was exercised; only disposable test paths were used.

Next card: **A6** (Android multi-user, multi-device and wireless).

## 2026-11-28 — A6 complete (multi-user, multi-device, wireless)

| Step | Evidence | Result |
|---|---|---|
| Multi-user/device state | `AndroidDebloatViewModel` loads users with the selected device and `AndroidPackageViewModel.SetDevice` resets package state for that one device/user pair | no cross-device package state |
| Wireless ADB | typed `PairWireless`, `ConnectWireless`, `DisconnectWireless` commands use private IPv4 validation, numeric ports and a 6-digit pairing code | private targets only; bad serial/host/code rejected |
| Disconnect guard | disconnect is allowed only for a selected wireless device | USB selections are not passed to `adb disconnect` |
| Health pre-check | selected-device battery query plus a 30% warning and connection/authorization status; missing unlock evidence is labelled unavailable | no fabricated unlock state |
| Tests | `PackageCommandTests` adds battery/health and wireless serial coverage | **339 passed**, 0 failed |
| Build/static/UI | solution build, lint, contrast, IconGen and Android UIA probe | 0/0 build; lint 367; contrast OK; 113 icons up to date; NoDevice probe exit 0 |

**A6 acceptance:** profiles and device selectors remain scoped to one selected device/user; wireless
pair/connect/disconnect UI uses typed validation; device health pre-check shows real battery data and
honest unavailable unlock evidence. No cross-device apply path or live phone mutation was used.

**Not checked:** no live pairing, wireless connection, device unlock or low-battery ADB run was exercised.

Next card: **A7** (Android tests).

## 2026-11-28 — A7 complete (Android tests)

| Step | Evidence | Result |
|---|---|---|
| Command/parser/state coverage | existing recorded-output parser, typed command, package scan/state and batch suites cover devices, users, getprop, package ids/users, continue/failure/disconnect, denylist and verification | no real device |
| Snapshot/selection/signature coverage | backup/snapshot, text/JSON selection, update/list schema, consent/checksum/Google-signature and unsigned-adb suites use temp paths/fakes | no personal data or live download |
| Device isolation | `PackageEngineTests.Separate_devices_keep_actions_on_their_requested_serial` records the serial for every command in two selected-device runs | no cross-device request |
| Reference parity | `AndroidParityTests.Feature_map_has_no_unimplemented_reference_features` reads `docs/dev/uad-feature-map.md` and rejects `not started` rows | UAD feature map closed |
| Tests/build/gates/UI | full solution build and test suite; lint; contrast; IconGen; Android UIA probe | 0/0 build; **341 passed**; lint 367; contrast OK; 113 icons; probe exit 0 |

**A7 acceptance:** the Android test suite covers command rejection, recorded parsers, state
computation, denylist/Expert confirmation, batch behavior, undo/restore, snapshots, list validation,
selection round-trips, download consent/signature checks and reference parity. All mutations and
network/signature cases use fakes or disposable files.

**Not checked:** no live phone, real wireless pairing, package mutation, official live download or
physical low-battery/unlocked-device pre-check was exercised.

Next card: **R3** (professional installer).

## 2026-11-28 — R3 partial (professional installer)

| Step | Evidence | Result |
|---|---|---|
| Release publish | `tools/build-release.ps1` publishes `FPM.exe` and `FPM.Helper.exe` for `win-x64` self-contained and creates `portable.flag` | app/helper published; not signed |
| Inno compile | Inno Setup 6.7.3 compiled `FPM-Setup-3.23.3.0.exe` | installer output 379,895,612 bytes |
| Portable archive | `FPM-portable-3.23.3.0.zip` | archive 360,406,830 bytes |
| Checksums | `publish/SHA256SUMS.txt` | portable `e62421a4…`; installer `0788aa92…` |
| Graceful exit | published app launched with `--no-splash`, accepted `--exit` through `FPM.Instance.<SID>`, stopped PID 21272 and did not hit an unrelated process | pass |
| Silent install | two silent installer attempts returned exit 0 but left no visible `C:\Program Files\FPM` or Add/Remove Programs entry and no setup log | **not proven**; artifact may require interactive elevation/owner execution |
| Upgrade/uninstall/coexistence | no live Win10/11 matrix, upgrade, uninstall or side-by-side Microsoft PC Manager run | **not executed** |
| Build/tests/gates | solution build, tests, lint, contrast, IconGen | 0/0 build; 341 tests; lint 367; contrast OK; 113 icons |

**R3 acceptance status:** partial. The script, Inno source, self-contained publish and portable package
compile; the named-pipe shutdown path is verified. Installer discovery and silent/upgrade/uninstall
behavior is not marked green until a machine run creates the expected install and registry entries.

Next card: **R5** (README and screenshots); R3 remains open for owner/live matrix verification.

## 2026-11-28 — R5 partial (README and screenshots)

| Step | Evidence | Result |
|---|---|---|
| README | root README replaced with current features, safety, build, privacy and attribution sections | no stale executable/name examples |
| Logo/banner | `docs/images/logo.svg`, `logo.png`, `banner.svg`, `banner.png` | original FPM geometry; 512 px and 1280×640 |
| Captures | PrintWindow/UIA from running FPM: Home Light/Dark, Storage, Memory, Windows Debloat, Android Debloat | real app windows; settings restored |
| Toolbar | real existing floating-toolbar capture copied to `docs/images/toolbar-docked.png` | under 300 KB |
| Gaps | installer wizard, compact/mini mode, complete three-width/two-theme matrix | not fabricated; recorded |

**R5 acceptance status:** partial. README and real assets exist, but the full screenshot matrix
and installer-wizard capture remain open. Next card: **R6** (repository description/topics).

## 2026-11-28 — R6 complete (repository metadata prepared)

| Step | Evidence | Result |
|---|---|---|
| Authentication | `gh auth status` | logged in as `farmanullah1`; `repo` and `workflow` scopes present |
| Current metadata | `gh repo view farmanullah1/FPM --json nameWithOwner,description,repositoryTopics,url` | existing description/topics read; repository not renamed |
| Description | exact owner-reviewed text below | 196 characters; under 350 |
| Topics | exact owner-reviewed topic list below | includes Windows, Android/ADB, WPF and privacy |
| External edit | not run | owner has not confirmed the GitHub metadata action |

Prepared description:

`FPM — free, open-source Windows PC manager: safe cache/junk cleaner (AI IDEs, dev tools, browsers), RAM & storage insights, Windows & Android debloater, live temperature/battery toolbar. Local-first, no telemetry.`

Prepared command:

```powershell
gh repo edit farmanullah1/FPM `
  --description "FPM — free, open-source Windows PC manager: safe cache/junk cleaner (AI IDEs, dev tools, browsers), RAM & storage insights, Windows & Android debloater, live temperature/battery toolbar. Local-first, no telemetry." `
  --homepage "https://github.com/farmanullah1/FPM/releases" `
  --add-topic windows --add-topic windows-11 --add-topic windows-10 `
  --add-topic pc-manager --add-topic system-optimizer --add-topic cleaner `
  --add-topic disk-cleanup --add-topic debloat --add-topic android-debloat `
  --add-topic adb --add-topic privacy --add-topic system-monitor `
  --add-topic wpf --add-topic dotnet --add-topic csharp `
  --add-topic fluent-design --add-topic open-source
```

**R6 acceptance:** metadata text and exact command are prepared. No repository content or
topics were changed externally without the owner's explicit confirmation.

## 2026-11-28 — U7 partial (Boost cancellation and state settlement)

| Step | Evidence | Result |
|---|---|---|
| Shared coordinator | `BoostCoordinator.RunAsync` links caller cancellation to an owned CTS; `Cancel()` cancels only an active run | cancellation test passed |
| Shared UI | `BoostAnimationHost` exposes Cancel in Measuring/Working and is bound from Home and floating toolbar | 0/0 build |
| Settlement | Completed schedules a 2 s return to Idle; cancellation sets Idle immediately | settlement test passed |
| Tests | focused Boost suite | 8 Boost tests passed |
| Full suite/gates | solution test, lint, contrast, IconGen | 343 passed; lint 367; contrast OK; icons up to date |
| Not captured | reduced-motion live run, failure/cancel visuals and the 2 s settle in UI | not claimed |

**U7 acceptance status:** partial. Cancel, settle and state behavior are implemented and tested;
live reduced-motion/failure/cancel captures remain open.
