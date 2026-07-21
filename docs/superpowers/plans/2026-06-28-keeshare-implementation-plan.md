# KeeShare — Implementation Plan (executable)

**Date:** 2026-06-28
**Inputs:** `specs/2026-06-28-keeshare-testing-and-lindrew-rollout-design.md` + `reviews/2026-06-28-keeshare-findings.md`
**State at write time:** build works, app runs on emulator, 141 unit tests pass (but test real copies, not prod), findings catalogued. Finding IDs (H-Sync, H-Lock, …) reference the findings report.

This plan is ordered so each step has a concrete verification gate. Use TDD for every production-behavior change (write/adjust a failing test first, then fix). Prefer the `feature-dev` / `superpowers:test-driven-development` flow.

---

## Phase A — Rebase & de-noise (do first; unblocks everything)

- [ ] **A1. Rebase `keeshare-support` onto `main`.** Branch is 113 behind. Most of the 187-file diff is phantom (PR template, `pref_app_display.xml`, `AddTemplateEntries.cs`, manifests — all 0 branch-commits).
  - Steps: `git fetch origin`; create a safety branch (`git branch keeshare-support-prerebase`); `git rebase origin/main`. Resolve conflicts in the **real** KeeShare-touched files only (`KeeShare.cs`, the two activities + layouts, `SaveDB.cs`, `LoadDB.cs`, `SyncUtil.cs`, `App.cs`, `GroupBaseActivity.cs`, `FileSelectActivity.cs`, tests, docs, maestro).
  - **Gate:** `git diff --stat main..keeshare-support` shows ~30 files (not 187); `dotnet build` (recipe below) still produces the APK; 141 unit tests still pass.

## Phase B — Safe quick wins (low risk, ship with build verification)

Some already done this session: `X509Certificate2` `using` leak fix (H-Cert) and `KeeShare.md` fixture-password/structure correction. Remaining:

- [ ] **B1. Tighten vacuous Maestro assertions** (`keeshare_full_test.yaml` `visible: ".*"` + `".*KeeShare.*"`; `keeshare_wrong_password.yaml` `.*Edit.*` branch; `keeshare_import_flow.yaml` `.*KeeShare.*` branch). Replace each with an assertion on a *specific* outcome string. **Verify by running each flow on the emulator** (don't ship blind — that's why it wasn't done autonomously).
- [ ] **B2. Localise hardcoded English** (Q1/Q2): `EditKeeShareActivity.cs:51` "Invalid group identifier"; default group name "KeeShare Import" at `ConfigureKeeShareActivity.cs:519/589/720`. Add `@string` resources.
- [ ] **B3. Remove premature no-op `Update()`** at `ConfigureKeeShareActivity.cs:738` (U2).
- [ ] **B4. Color resources for password-status** (`#CC6600`/`#008800`) → dark-mode-safe `@color`/theme attrs (Q3).
- **Gate:** build succeeds; affected Maestro flows pass on emulator.

## Phase C — Lindrew blockers (TDD; do before trusting real secrets)

Each needs the test layer from Phase D to verify properly; build C and D together (write the failing test in D, fix in C).

- [ ] **C1. H-RootGroup** — exclude root group from `CollectGroups` (`ConfigureKeeShareActivity.cs:470`); add a confirm dialog if a near-root/large group is chosen. *Test:* UI/integration test that root is not offered.
- [ ] **C2. H-Thread** — wrap per-group error `ShowMessage` at `KeeShare.cs:805` and `:715` in `_app.UiThreadHandler.Post(...)`. *Test:* a wrong-password sync does not crash (emulator E2E with a real wrong password).
- [ ] **C3. H-Lock** — wrap `SyncGroups` mutation (`ClearGroupContents`/`MergeIn`/`UpdateGlobals`/`MarkAllGroupsAsDirty`) and the export op in a `BackgroundDatabaseModificationLocker` (mirror `SynchronizeCachedDatabase`). *Test:* integration test that a sync while a read-lock is held doesn't corrupt lookup dicts.
- [ ] **C4. H-Sync** — make Synchronize actually export during background sync: after `Import(...)` for `type=="Synchronize"` in `ProcessKeeShare`, also run the export leg (guard against write-amplification; respect C3 locking). *Test (the important one):* two-database round-trip — A edits → sync → B sees it **and** B edits → sync → A sees it, with no explicit save. This is the Phase D two-emulator E2E.
- [ ] **C5. H-DeadCheck** — remove the dead `Contains("master key")` branch (`ConfigureKeeShareActivity.cs:229`); rely on the per-group message (now UI-dispatched). *Test:* wrong-password shows exactly one error.
- **Gate:** all C-tests green; manual two-emulator Synchronize round-trip works.

## Phase D — Test pyramid (the strategic fix)

- [ ] **D1. Extract `KeeShare.Core`** (`netstandard2.x`) from `KeeShare.cs`: constants, config (`EnableKeeShare`/`DisableKeeShare`/`UpdateKeeShareConfig`), KeePassXC compat, `VerifySignatureCore`, state predicates, and the merge orchestration (`SyncGroups`) behind an interface. Inject Android/IO/device deps as delegates (e.g. `Func<string,string>` for `IOConnectionInfo` parsing). Reference from **both** the app and `KeeShare.Tests`.
  - **Gate:** app builds; the existing 141 tests now run against `KeeShare.Core` (delete the `TestHelpers/*Logic.cs` copies). Fixes the already-drifted helpers (HasKeeShareGroups side-effect, VerifySignatureCore Base64-first, GetEffectiveFilePath algorithm).
- [ ] **D2. Real-logic unit + integration tests**: drive `SyncGroups`/Export against real `PwDatabase` (KeePassLib is netstandard) — CustomData survival after merge, Import destructive pre-clear, Synchronize additive merge, the `UpdateGlobals` crash-fix path, Export entry-clone correctness.
- [ ] **D3. Two-emulator deterministic E2E** (spec Q2=A, Option-1 orchestrator under `e2e-tests/`): boot 2 AVDs (a 2nd is creatable from the installed `android-35` images), install build on each, run per-emulator Maestro subflows by serial, sync the shared container between them via `adb pull`/`adb push`, record both screens. Scenarios: Import, Export, **Synchronize** (round-trip), edit-conflict (newer-wins + history).
- [ ] **D4. Cross-compat suite** (spec Q3=C, **revised**): `keepassxc-cli` has **no `keeshare` command** and can't make *signed* containers. So: (a) **unsigned round-trip** via `db-create`/`add`/`merge`/`export` — KP2A imports a CLI-authored container; CLI reads back a KP2A export; (b) **signed-container** compat via **fixtures captured once from a real KeePassXC GUI export** (commit them), imported by an automated KP2A test, + manual check in Phase F.
- **Gate:** all four layers green at least once.

## Phase E — Reliability hardening

- [ ] **E1.** Run D3 + D4 **10× back-to-back**; isolate AVD state per run; replace sleeps with explicit waits; make container-sync steps idempotent/ordered.
- [ ] **E2.** Capture clean-run **dual-emulator video** as the evidence artifact.
- **Gate:** 10/10 green + recorded video.

## Phase F — Real-device dogfooding ("Lindrew") — manual, needs Andrew + Lindsay

- [ ] F1. Build signed APK; install on both phones.
- [ ] F2. Create a **Dropbox shared folder** between the two accounts for the Lindrew container (the two-separate-accounts solution).
- [ ] F3. **Back up both personal databases first.** Add a Synchronize "Lindrew" group on each (Mac KeePassXC + Android), pointing at the shared container; seed a couple of throwaway secrets.
- [ ] F4. Live with it for several days; verify edits propagate both directions across all four endpoints; watch conflicts.
- **Gate:** reliable in real life over a multi-day window.

## Phase G — Upstream prep (do NOT start until F passes; per Q1=C)

- [ ] De-WIP; complete the PR checklist; fold the findings report + test pyramid + video into the PR description; wire CI for the CLI-able suites (unit + integration + cross-compat); leave the two-emulator E2E as a local/recorded gate.

---

## Build recipe (verified)
```bash
export ANDROID_HOME=$HOME/Library/Android/sdk ANDROID_SDK_ROOT=$HOME/Library/Android/sdk
export PATH=/usr/local/share/dotnet:$PATH        # .NET 10.0.102 SDK w/ android workload
dotnet build src/keepass2android-app/keepass2android-app.csproj -c Release -f net9.0-android -p:Flavor=NoNet
adb install -r src/keepass2android-app/bin/Release/net9.0-android/keepass2android.keepass2android_nonet-Signed.apk
```
Emulator: `Pixel_8_API_35_Root` (a 2nd AVD for D3 is creatable from `system-images/android-35/...`). Unit tests: `dotnet test src/KeeShare.Tests` (net8, uses the homebrew dotnet 8 fine).
