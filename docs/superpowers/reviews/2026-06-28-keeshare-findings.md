# KeeShare (PR #3165) — Quality & Test-Quality Findings Report

**Date:** 2026-06-28
**Branch:** `keeshare-support` (46 commits ahead / **113 behind** `main`)
**Reviewer:** autonomous session (4 parallel code-review agents + direct verification)
**Companion spec:** `docs/superpowers/specs/2026-06-28-keeshare-testing-and-lindrew-rollout-design.md`

## How this was produced

- **Build verified:** the app builds. `/usr/local/share/dotnet` (.NET **10.0.102** SDK, `android` workload) builds `net9.0-android` with `ANDROID_HOME=~/Library/Android/sdk`. Produces the 33 MB signed APK. (The spec's "blocked on dotnet 8" assumption is **resolved** — see Build Recipe below.)
- **App runs:** installed on emulator `Pixel_8_API_35_Root`, launches clean (`KP2A: Creating application … Version=244`, no crash).
- **Unit tests:** `dotnet test src/KeeShare.Tests` → **141 passed, 0 failed** (928 ms). But see the test-quality verdict — green here means little.
- **Reviews:** four focused agents over (1) core `KeeShare.cs`, (2) UI activities, (3) the test suite, (4) collateral/core-file changes. Cross-checked against `git log` to separate real edits from staleness.
- **Cross-impl probe:** `keepassxc-cli 2.7.11` has **no `keeshare` command** — KeeShare config and *signed*-container export are GUI-only. Affects the Phase 3 cross-compat plan (below).

## Overall verdict

The headline feature (in-app Add/Edit/Sync KeeShare, Import mode) **builds, runs, and demos**. But for a feature meant to sync real secrets between two people, there are **real correctness, concurrency, and security defects on the exact paths the "Lindrew" use case depends on (Synchronize + background sync)**, and the **test suite does not actually exercise the production code** — so its green status is not trustworthy. None of this is fatal; it's a clear, fixable worklist. **Do not enable Synchronize on real databases until H-Sync / H-Lock / H-Thread below are fixed and covered by tests.**

---

## CRITICAL / HIGH findings

### H-Sync — "Synchronize" never exports during background sync (the Lindrew path is half-broken)
**`KeeShare.cs` ~832–836 (`ProcessKeeShare`); confidence 90.** `KeeShareCheckOperation` only *imports* for both Import and Synchronize. The export leg is wired only to `SaveDb.OnSaveCompleteKeeShareExport`, which fires on **explicit save**, not during `SyncInBackground`. Result: after a background sync, the in-memory DB absorbs peers' changes but **your own changes are not written back to the shared container** until a manual save. For Andrew↔Lindsay, this means edits silently fail to propagate until someone explicitly saves — the opposite of what "Synchronize" implies. **This is the most important functional bug for the mission.** Fix needs care (it interacts with H-Lock and with write-amplification/conflict concerns) → test-driven.

### H-Lock — merge mutates the database without the background-modification write lock
**`KeeShare.cs` ~1087–1115 (`SyncGroups`), also the export op; confidence 90.** Every other background mutator (`SynchronizeCachedDatabase`, `SaveDb`) wraps changes in `BackgroundDatabaseModificationLocker` (`EnterWriteLock`). `SyncGroups` calls `ClearGroupContents`, `MergeIn`, `UpdateGlobals`, `MarkAllGroupsAsDirty` — all mutating non-thread-safe collections (`GroupsById`, `EntriesById`, `Elements`, `DirtyGroups`) — **with no write lock**. A UI activity holding a read lock (e.g. `ShareUrlResults`, `EntryEditActivity`) during a background sync can hit concurrent structural modification → crash or corrupted lookup state. Fix: take a `BackgroundDatabaseModificationLocker` around the mutation (same pattern as `SynchronizeCachedDatabase`).

### H-Thread — per-group sync error crashes on the common failure path
**`KeeShare.cs` 805 & 715; confidence 87.** Per-group error messages call `_app.ShowMessage(...)` **directly from the `OperationRunner` background thread**. With a `GroupBaseActivity` visible, `ChainedSnackbarPresenter` can call `snackbar.Show()` on that thread → `CalledFromWrongThreadException`. This is the *error* path (wrong password, file not found) — the most-hit path in real use. Fix: wrap both in `_app.UiThreadHandler.Post(...)` (the completion message in `SyncUtil` already does this; these two were missed).

### H-RootGroup — the entire vault can be shared with no guard
**`ConfigureKeeShareActivity.cs` ~470 (`CollectGroups`); confidence 85.** The add-dialog group spinner includes the **root group**. Selecting it enables KeeShare on root → *every* entry in the database is in the share boundary, exported to the external container. No warning, no confirmation. The user-facing docs even warn against this, but the UI offers it freely. Fix: exclude root from `CollectGroups` (and/or hard-confirm).

### H-Cert — `X509Certificate2` leaked per signature verification
**`KeeShare.cs` 1233; confidence 95.** `X509Certificate2.CreateFromPem(...)` is never disposed (holds native handles on Android). Wrap in `using`. Easy, safe fix.

### H-DeadCheck — wrong-password handling is dead code → double error toast
**`ConfigureKeeShareActivity.cs` 229–234; confidence 90.** The callback receives the generic `"KeeShare sync completed with errors"`, so the `Contains("master key")` branch never matches; the friendly per-group message is already shown by `ProcessGroup`, so the user gets **two** errors and the `keeshare_wrong_password` string is unreachable. Fix: drop the dead branch; rely on the per-group message (dispatched per H-Thread).

---

## MEDIUM findings

| ID | File:loc | Issue | Conf |
|----|----------|-------|------|
| M-SignedAsym | `KeeShare.cs` 930–934 | If a `.sig` is present but no trusted cert is configured, the container is imported **without verification** (silent "backward compatibility"). Inverse case correctly rejects. An attacker who can write the container can swap the payload. At minimum surface a user-visible warning; ideally require explicit opt-in. | 82 |
| M-SideEffect | `KeeShare.cs` 467–481 (`HasKeeShareGroups`) | The "is there KeeShare here?" check **mutates**: on KeePassXC-format detection it runs `TryImportKeePassXCConfig` → `EnableKeeShare` → `group.Touch()`. Called from `App.cs` load + `SyncUtil` sync as if read-only. Marks the DB dirty with no user action → can trigger spurious uploads / conflict warnings. Split the side-effecting import out of the predicate. | 80 |
| M-EditCtx | `EditKeeShareActivity.cs` 154 | Uses `ActionOnOperationFinished` instead of `ActionInContextInstanceOnOperationFinished`; a screen rotation during save fires the callback on a destroyed activity → `BadTokenException`. Match `ConfigureKeeShareActivity`'s pattern. | 80 |
| M-PathConflict | `EditKeeShareActivity.cs` 141,149 | Writes the **global** `FilePathKey`, while `ConfigureKeeShareActivity` writes the **device-specific** key (which `GetEffectiveFilePath` prefers). Editing the path here is silently ignored on the current device and leaks as the cross-device "original path". Unify on `SetDeviceFilePath`. | 80 |
| U-ContentUri | `ConfigureKeeShareActivity.cs` GetView ~134 | SAF selection shows the raw `content://…` URI as the device path instead of a human filename (`AndroidContentStorage.GetDisplayName` exists but isn't used). | 95 |
| M-SaveDefer | `SaveDB.cs` `TriggerKeeShareExportThenFinish` | `Finish(true)` is deferred behind KeeShare export and **hardcoded to success** — a bug in the export op hangs *every* save; export failure is silent; the catch-block message is unlocalised English. Fast-path guard protects non-KeeShare DBs. Document/flag the silent-failure; localise the string. | 85 |
| M-StaticDelegates | `SaveDB.cs`, `LoadDB.cs` | KeeShare hooks are `public static Action<…>` on core business classes (`OnSaveCompleteKeeShareExport`, `OnLoadCompleteKeeShareCheck`). Global mutable state; breaks test isolation for all `SaveDb`/`LoadDb` tests. Prefer an event/observer or DI. | 85 |
| M-OtpGated | `SyncUtil.cs` 119–137 | `SyncOtpAuxFileIfNeeded` now only runs inside the KeeShare callback branch; if KeeShare's callback doesn't fire, OTP aux sync silently stops. Plus `HasKeeShareGroups`'s side effect (M-SideEffect) runs on **every** sync for all users. | 80 |

## LOW findings (code quality / polish)

- Hardcoded English strings instead of `@string`: `EditKeeShareActivity.cs:51` ("Invalid group identifier"), default group name "KeeShare Import" at `ConfigureKeeShareActivity.cs:519/589/720`.
- Hardcoded hex colors (`#CC6600`/`#008800`) in `GetView` → break dark-mode contrast, parsed on every bind. Use color resources / theme attrs.
- `keeshare_config_row.xml` 78–111: four action buttons in one un-weighted row → clip on ≤360 dp screens.
- `dialog_add_keeshare.xml:132` / `dialog_edit_keeshare.xml:68`: `xmlns:app` declared on an inner view, not the root.
- `ConfigureKeeShareActivity.cs:738`: premature `Update()` (no-op; real refresh happens in the `SaveDatabase` callback). Remove.
- Debug `Log.Debug("KP2A", …)` lines in `GroupBaseActivity.cs` / `FileSelectActivity.cs` — confirm intentional before upstreaming.

---

## Test-quality verdict (this is the big one)

**The 141 passing unit tests do not test production code — they test hand-maintained *copies* of the logic in `src/KeeShare.Tests/TestHelpers/*Logic.cs`, and the copies have already silently diverged from `KeeShare.cs`:**

- `HasKeeShareGroups` helper is **missing the KeePassXC auto-import side-effect** that production has.
- `VerifySignatureCore` helper is **hex-only**; production tries **Base64 first**, then hex. The "whitespace" test asserts the *helper's* behavior, not production's.
- `GetEffectiveFilePath` helper uses a **string-heuristic**; production uses `IOConnectionInfo.UnserializeFromString`. They diverge on real inputs.
- `TrustedCertificateKey` is a named const in the helper but an inline literal in production.

**And the most dangerous code has zero coverage:** `SyncGroups` (the CustomData-preserving, UUID-aliased merge — where data loss would live) and the entire **Export** path are untested at the unit level. The Maestro suite never configures Export or Synchronize.

**Maestro assertions are partly vacuous** (always-true), so the E2E "passes" can be hollow:
- `keeshare_full_test.yaml`: `extendedWaitUntil: visible: ".*"` then `assertVisible: ".*KeeShare.*"` — both trivially true; it's a navigation smoke test, not a behavior test.
- `keeshare_wrong_password.yaml` / `import_flow.yaml`: `".*[Ww]rong password.*|.*Edit.*"` and `".*sync complete.*|.*KeeShare.*"` — the `.*Edit.*` / `.*KeeShare.*` branches match the config screen regardless of outcome.
- E2E fixtures are **mutable and accumulate state** across runs (README documents manual DB-recreation as the "fix") → a real flakiness source.

**Remedy (this is the spec's Phase 2/3 core):** extract the pure logic from `KeeShare.cs` into a platform-neutral `KeeShare.Core` library (`netstandard2.x`), referenced by **both** the app and the tests, injecting IO/device dependencies as delegates. Then: (a) the 141 tests exercise real code; (b) add `SyncGroups`/Export tests using real `PwDatabase` (KeePassLib is netstandard-compatible); (c) tighten the Maestro assertions to check specific outcome strings and use per-test `clearState`/fresh fixtures.

---

## Branch hygiene: rebase first

The branch is **113 commits behind main**. Most of the 187-file, ~19k-line diff is **phantom** — files `main` changed while the branch was parked (verified: 0 branch commits on `.github/PULL_REQUEST_TEMPLATE.md`, `pref_app_display.xml`, `AddTemplateEntries.cs`, `kp2akeytransform/AndroidManifest.xml`, etc.; those show as diffs only because the branch predates main's edits). **A rebase onto `main` collapses the diff to the true ~30-file KeeShare change set** and is the right first move before any upstreaming. Real KeeShare-touched core files (have branch commits): `KeeShare.cs`, the two activities + layouts, `SaveDB.cs`(4), `LoadDB.cs`(6), `SyncUtil.cs`(4), `App.cs`(2), `GroupBaseActivity.cs`(2), `FileSelectActivity.cs`(1), plus the tests/docs/maestro.

> Note: the collateral review initially flagged "PR template deleted — revert!" — that was a false alarm from reviewing without git history. There is nothing to revert; rebase resolves it.

---

## Cross-impl reality check (revises Phase 3 plan)

`keepassxc-cli` cannot configure KeeShare or produce a **signed** container (GUI-only). It *can* `db-create`/`add`/`merge`/`export`, so the cross-compat suite can still test the **unsigned container round-trip** (KP2A imports a CLI-authored container; CLI reads back a KP2A-exported one). **Signed-container** compatibility needs **fixtures captured once from a real KeePassXC GUI export** (committed to the repo), imported by an automated KP2A test, plus a manual check in Phase 5.

---

## Prioritised remediation roadmap

**Do before trusting real secrets (Lindrew blockers):**
1. H-Sync (Synchronize export leg) — test-driven; the mission depends on it.
2. H-Lock (write lock around merge) — concurrency/corruption.
3. H-Thread (UI-thread dispatch for errors) — crash on common path.
4. H-RootGroup (exclude root / confirm) — prevents catastrophic over-share.

**Quick, safe wins (low risk; some shipped this session — see below):**
5. H-Cert `using`; H-DeadCheck dead branch; U2 premature `Update()`; localise hardcoded strings; tighten vacuous Maestro assertions.

**Strategic (Phase 2/3):**
6. Extract `KeeShare.Core`; retarget unit tests at real code; add `SyncGroups`/Export tests; build the two-emulator E2E + cross-compat suites; de-flake.

**Hygiene:** rebase onto `main`; then de-WIP for upstream (Phase 6).

**Triage note:** Per Andrew's Q4=A decision, the substantive production-bug fixes (1–3, 6) are left for triaged, test-driven work rather than changed blind. This session ships only the safe, high-confidence quick wins and documents the rest here.

## Build recipe (verified working)

```bash
export ANDROID_HOME=$HOME/Library/Android/sdk ANDROID_SDK_ROOT=$HOME/Library/Android/sdk
export PATH=/usr/local/share/dotnet:$PATH        # .NET 10.0.102 SDK w/ android workload
dotnet build src/keepass2android-app/keepass2android-app.csproj \
  -c Release -f net9.0-android -p:Flavor=NoNet
# -> src/keepass2android-app/bin/Release/net9.0-android/keepass2android.keepass2android_nonet-Signed.apk
adb install -r <that apk>     # package: keepass2android.keepass2android_nonet
```
