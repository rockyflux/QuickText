# AGENTS.md

Repository working agreement for AI coding agents. Read before changing code. If another tool has its own instruction file, treat it as a pointer here — not a second rule set.

QuickText is a **Windows-only WPF / .NET 10** tray app: snippet manager + text expander, shipped as a single-file portable exe. No web stack, no plugins. Hosted on the internal GitLab project (`UpdateService.ProjectUrl` / README download link). User docs: `README.md` (简体中文, default) and `README.en.md` (English). Release notes: `docs/release-notes/`. Build entry: README **构建** / **Build**, plus root `build.bat`.

## 1. Read before changing

- **`README.md` / `README.en.md`** — product promises and screens.
- **`docs/release-notes/`** — published notes are immutable; add a new file only when cutting that version (`docs/release-notes/<tag>.md`, e.g. `v1.0.1.md`).
- **`.github/workflows/ci.yml`** — canonical test / smoke / publish / checksum recipe. Local green ≠ released; confirm the GitLab release assets separately.
- **In-repo WPF pitfalls** (no external `wpf-desktop` skill assumed): `src/QuickText.App/app.manifest` (PerMonitorV2), `App.xaml.cs` foreground/`SetWindowPos` comments, `Ui/WindowTheming.cs`, `--smoke` / `--shots` in `App.xaml.cs`. Read those before layout, DPI, activation, tray, or harness changes.
- **`git status` / `git log`** — the tree may already be dirty; never treat it as a clean release tree.

## 2. Build and verify

Prefer the root menu (or pass the number directly):

```bash
build.bat 1    # Debug build
build.bat 3    # unit tests
build.bat 4    # --smoke (needs a prior Release build)
build.bat 8    # full local gate: test + Release + smoke
```

Or the same commands by hand:

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj -c Release
dotnet build src/QuickText.App -c Release
```

**`build.bat` kills a running `QuickText.exe`** before build/publish/smoke so MSBuild can overwrite the locked apphost. Do not use it against a user’s live session; use an isolated copy / portable data folder for UI work.

In-process self-checks (not a second-instance harness):

```bash
# After XAML / window-option changes: construct and lay out every window.
QuickText.exe --smoke

# UI matrix (languages, themes, short screens) → directory. Not in build.bat.
QuickText.exe --shots <directory>
```

- `--smoke` writes `%TEMP%\quicktext-smoke.txt` via `Path.GetTempPath()`. Success is marker content `OK`; CI treats missing/non-`OK` as failure even if the process exit code looks fine.
- `--shots` is off-screen and may set `MaxHeight` itself. A dialog without its own runtime cap can look fine in every image while the real primary action sits outside the monitor. For scrollable / height-capped windows, also check the primary action’s screen rect or take a full-screen capture.
- A second normal launch hands off to the running instance (only autostart stays silent). Do not use that as a test harness.
- Do not install a real global mouse hook or a full-screen topmost overlay on the machine you are using while developing.

Publish profiles (same as README / CI):

```bash
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64-needs-dotnet10
```

## 3. WPF and layout invariants

- Process is **PerMonitorV2** DPI-aware. Use the window’s actual monitor work area; never assume one desktop DPI.
- Do not hardcode a height that must serve both a short laptop and a 4K display. Derive from the active monitor’s work area; keep a conservative XAML `MaxHeight` fallback when monitor info is missing.
- Every `SizeToContent` window needs a runtime height cap **and** a XAML `MaxHeight` fallback. A window-level cap is not a content cap: scroll bodies can still overflow. Keep title + action buttons outside the scroll area.
- Do not bypass foreground / z-order guards. **Never** `SetWindowPos` a `Topmost` window (see `App.xaml.cs`).
- Custom focusable visuals need an accessibility story: `AutomationPeer`, `AutomationProperties.Name`, `LabeledBy` on form controls, list-item names from displayed data (not model type names). Confirm via UIA; XAML alone is not evidence.
- Keep single-instance, config location, and startup behavior intact. A second-instance test needs an isolated copy or a dedicated message name.
- First-run defaults go in settings defaults / seeds — not in a model property that existing configs already persist. A missing JSON field must keep prior behavior; test both fresh and old configs.
- Offline by default. Opt-in network only: **check updates** (GitLab Releases API) and **AI generate** (user-configured provider). No telemetry, cloud sync, or silent background network.

## 4. Localization and documentation

- Neutral `src/QuickText.Core/Localization/Strings.resx` is the Simplified Chinese source. Every UI key must exist in **all** `Strings.*.resx` satellites (currently 17 + neutral = 18 UI languages). Never ship a raw key or empty value.
- Keep `README.en.md` structurally aligned with `README.md` (including the Download heading).
- User-visible behavior changes update both READMEs in the same change when the docs should mention them.
- Comments explain **why**, not what. No drifting line numbers, test counts, release versions, or “this round” notes. Historical references to removed code are fine when the surrounding text says so.
- Do not rewrite historical plans/specs under `.superpowers/` or `docs/superpowers/`; they are records. Do not commit `bin/`/`obj/`, `artifacts/`, `dist/`, generated screenshots, temp configs, local settings, or `.qoder*`.

## 5. User data and safety

- **Installed** (default): machine state in `%APPDATA%\QuickText\` (`settings.json`, usage, backups, clipboard history, skills favorites, …); library defaults to `Documents\QuickText`.
- **Portable**: marker file `QuickText.portable` beside the exe → everything under `<exeDir>\Data\` (library under `Data\library`). Mode is pinned at startup; Settings toggles take effect next launch.
- Tests and harnesses must not edit the user’s real settings, library, startup entries, clipboard-history store, or running instance.
- Destructive actions keep existing confirmation and honest failure reporting. Do not swallow errors in user-triggered flows.
- Do not add a confirmation, extra step, or notification unless it removes a demonstrated confusion or risk. Prefer the smallest fix in copy, defaults, validation, or feedback.

## 6. CI and releases

- Version source is the git tag. Source builds use the newest reachable tag; CI/publish pass `-p:Version=` from the tag. Do not maintain a second version number in the app or docs.
- Before changing CI, read `.github/workflows/ci.yml`. Expected gates: Core tests with `--minimum-expected-tests` (raise when adding tests, never lower casually), Release app build, `--smoke` marker check; on `v*` tags, both publish profiles, zip + bare `QuickText.exe` + `SHA256SUMS.txt`, and a release body built from `docs/release-notes/<tag>.md` (missing notes = no release).
- Hosted releases and the in-app updater target **GitLab Releases** (see `UpdateService`). A green local build does not prove the release page has every asset users download.
- Do not add Dependabot. Before a public/shared release, check outdated/vulnerable packages manually (`dotnet list package --outdated`, `dotnet list package --vulnerable --include-transitive`) and review Action major bumps in the workflow pins.
- Do not add a winget publish step in this repo. Public winget packaging, if used at all, lives outside this tree (historically `rockbenben/winget-manifests`); a second manifest PR duplicates and gets rejected.
- Do not push, create a tag, publish a release, or edit published assets without explicit maintainer approval. A local commit is not a release.

## 7. Working with the tree

- Minimal diffs; match surrounding style. No abstractions, settings, or mechanisms for hypothetical futures.
- Separate commits by feature domain. Do not stage unrelated dirty files, generated shots, logs, or the user’s configuration.
- Do not add dependencies or generated assets without an in-scope reason.
- Run `git diff --check` before reporting a change.

## 8. Definition of done

Before calling a change complete:

1. Build and the relevant tests pass (`build.bat 8` or the equivalent hand commands).
2. `--smoke` passes after XAML/window changes; `--shots` when UI/layout changed, plus a screen-level check where the harness is blind.
3. New localization keys exist in every satellite resx; README zh/en stay structurally aligned.
4. Docs and comments describe current behavior — no stale counts, versions, or line numbers.
5. `git diff --check` passes. The report states what was verified, what was not, and that nothing was pushed or released unless the maintainer asked. Never claim a visual or release check that was not run.
