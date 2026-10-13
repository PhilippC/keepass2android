# KeeShare: Test Hardening + "Lindrew" Real‑World Rollout — Design

**Date:** 2026-06-28
**Branch:** `keeshare-support` (PR [#3165](https://github.com/PhilippC/keepass2android/pull/3165), `beveradb` fork → `PhilippC:main`, currently `[WIP]`)
**Author:** Andrew Beveridge

> **Update 2026-06-28 (autonomous session).** Phase 0 is **done**: the app builds and runs (the "blocked on dotnet 8" assumption was wrong — the `/usr/local/share/dotnet` .NET 10 SDK with the `android` workload builds `net9.0-android`; see the build recipe in the findings report). Phase 1 findings are in **`../reviews/2026-06-28-keeshare-findings.md`**, and the executable breakdown is in **`../plans/2026-06-28-keeshare-implementation-plan.md`**. Two revisions to this spec emerged: (a) `keepassxc-cli` has no `keeshare` command, so the cross-compat suite tests the *unsigned* round-trip via CLI and uses *captured GUI fixtures* for signed containers; (b) the branch is 113 commits behind `main`, so a **rebase comes first** (most of the 187-file diff is phantom).

## Goal

Pick the parked KeeShare work back up and get it to a state where:

1. The existing work has been reviewed for code quality and **test** quality.
2. The feature is covered by **meaningful, non‑flaky automated tests at every layer**, including a true multi‑party end‑to‑end test across **two independent emulators** with a deterministic stand‑in for cloud file sync.
3. There is **video evidence** of the E2E tests running green.
4. The build is installed on **Andrew's and Lindsay's real phones** and used to share real secrets via a shared **"Lindrew"** folder between their two separate KeePass databases.
5. Only *then* is the PR de‑WIP'd and offered upstream — backed by the testing + dogfooding evidence.

Sequencing is deliberate (decision Q1=C): **prove it for real on our own devices first; that evidence becomes the upstream case.** We do not want to waste the maintainer's time with something untested, and we genuinely want to use this feature daily, so it has to be good.

## Non‑Goals

- Re‑architecting KeePass2Android beyond what serves KeeShare reliability.
- Real cloud (Dropbox) sync *inside the automated test loop* — explicitly avoided to keep automated tests deterministic. Real Dropbox is exercised only in the manual dogfooding phase (Phase 5).
- Broad refactoring unrelated to KeeShare.

## Key Decisions (from brainstorming)

| # | Decision | Choice |
|---|----------|--------|
| Q1 | Primary goal | **C** — dogfood for real first, then upstream; real‑world proof *is* the test and the PR case. |
| Q2 | Automated multi‑party E2E sync layer | **A** — two deterministic emulators; the "shared folder" is a harness‑controlled file location synced via `adb` at defined moments; real Android↔Android KeeShare logic, zero network/auth flakiness. |
| Q3 | Cross‑implementation (KeePassXC) coverage | **C** — two separate automated suites: Android↔Android sync **and** KP2A↔KeePassXC cross‑compat via `keepassxc-cli`. Each failure points at exactly one risk. |
| Q4 | How review findings are handled | **A** — written findings report first (code quality + test quality), triage together, then change. Nothing changes without sign‑off. |
| — | E2E orchestration tooling | **Option 1** — custom orchestrator script wrapping Maestro subflows + `adb` for the deterministic sync and dual‑screen recording. |

## Current State (verified 2026-06-28)

- **PR #3165:** OPEN, `[WIP]`, mergeable, CI checks 0/0. **45 commits** ahead of `main`. Last commit **2026‑01‑27** (~5 months parked).
- **Feature, per implementation notes, working on emulator:** in‑app "Add KeeShare" (FAB → dialog: group select/create, share type, password, SAF file browse), "Edit KeeShare" (password/type), friendly wrong‑password errors, and five documented bug fixes — notably the post‑sync `FindDatabaseForElement` crash fixed via `UpdateGlobals()` + `MarkAllGroupsAsDirty()` after `MergeIn`.
- **Tests present:** ~129 xUnit `[Fact]/[Theory]` methods across 6 classes in `src/KeeShare.Tests/` (docs claim 141 — drift to reconcile); Maestro E2E flows in `e2e-tests/.maestro/` (import flow is the main passing one) with `.kdbx` fixtures.
- **Docs:** `docs/KeeShare.md` (user guide), `docs/KeeShare-Implementation-Notes.md` (dev notes + "next steps").

### Known weaknesses to confront

- **Test fidelity:** unit tests exercise **copied logic helpers** (`TestHelpers/KeeShare*Logic.cs`), *not* the real `KeeShare.cs`. Tests can stay green while production code breaks. The notes even instruct manually re‑syncing helpers when `KeeShare.cs` changes — a smell.
- **Coverage gaps (per notes):** only **Import** mode is well‑exercised; **Synchronize** (two‑way) and **Export** modes, and **signed containers**, are under‑tested. All prior testing was emulator‑only.
- **Doc inconsistency:** the two docs disagree on fixture passwords (`test123`/`share123` vs `TestMain123!`/`TestKeeShare123!`).
- **Build blocker:** app targets `net9.0-android`; the dev machine has **only dotnet 8.0.123**. Building the APK needs the **.NET 9 SDK + Android workload**. (`adb`, `~/.maestro/bin/maestro`, and `keepassxc-cli` are installed.)

### Real‑world architecture wrinkle (Lindrew)

Andrew and Lindsay have **separate Dropbox accounts**. KeeShare needs **one shared container file both can read *and* write**. The natural solution is a **Dropbox shared folder** (Dropbox mirrors a shared folder into both accounts). Both run **Mac KeePassXC + Android KeePass2Android**, each syncing their *own* personal database via *their own* Dropbox; only the Lindrew container lives in the shared folder. Each party adds a **Synchronize** KeeShare group ("Lindrew") to their personal database pointing at that shared container.

## Phase Plan

Each phase has an explicit gate; later phases assume earlier gates passed.

### Phase 0 — Build bring‑up (prerequisite)
- Install .NET 9 SDK + `android` workload; build `keepass2android-app` (`net9.0-android`) Release APK.
- Boot an emulator, install, smoke‑test the existing import flow against the committed fixtures.
- **Gate:** APK builds and the existing import flow runs by hand on a local emulator.

### Phase 1 — Quality + test‑quality review → findings report (Q4=A)
- Code‑review the 45‑commit diff (`main..keeshare-support`) and the test suite. Dispatch focused review subagents where the diff is large.
- Produce a **ranked findings report** (`docs/superpowers/reviews/2026-06-28-keeshare-findings.md`): correctness bugs, UX rough edges, code‑quality issues, and test‑quality gaps (copied‑helper fidelity, mode coverage, flakiness, weak assertions, fixture/doc drift).
- Triage together → a decided list of fix / defer / drop.
- **Gate:** triaged, signed‑off action list.

### Phase 2 — Act on triaged findings
- Fix agreed bugs / UX / code‑quality items.
- **Likely central item:** extract the pure KeeShare sync/merge logic out of Android‑coupled `KeeShare.cs` into a **platform‑neutral library** referenced by *both* the app and the tests — so tests exercise real code and an integration layer becomes possible. (Confirmed/scoped during Phase 1 triage.)
- Reconcile fixture passwords + the 141‑vs‑129 test‑count drift across docs.
- **Gate:** findings list closed out; build + existing tests green.

### Phase 3 — Build the test pyramid (all layers)
1. **Unit** (net8): realign onto the real extracted logic instead of copies.
2. **Integration** (headless, no emulator): drive the real merge/sync against real `.kdbx` files — fast, high‑fidelity. *Depends on the Phase 2 extraction; if not done, the emulator E2E remains the lowest test of real code.*
3. **E2E** (two‑emulator, deterministic — Q2=A): Android↔Android Synchronize across two isolated AVDs, harness‑synced container, dual‑screen video. Covers Import, Export, **Synchronize**, and conflict/merge.
4. **Cross‑compat** (Q3=C): KP2A ↔ `keepassxc-cli` round‑trip — KeePassXC authors a container → emulator imports/syncs → KP2A exports → `keepassxc-cli` reads it back. Includes **signed containers**.
- **Gate:** every layer green at least once.

### Phase 4 — Reliability hardening (de‑flake)
- Run E2E + cross‑compat suites **N consecutive times** (target **10/10**); isolate AVD state per run; replace sleeps with explicit waits; make container‑sync steps idempotent and ordered.
- Capture **clean‑run video evidence** (dual‑emulator screen recordings) as the proof artifact.
- **Gate:** 10/10 green runs + recorded video.

### Phase 5 — Real‑device dogfooding ("Lindrew")
- Build a **signed** APK; install on Andrew's and Lindsay's phones.
- Create a **Dropbox shared folder** between the two accounts for the Lindrew container.
- On each personal database (Mac KeePassXC + Android), add a **Synchronize** KeeShare group "Lindrew" → shared container; seed a couple of genuinely shared secrets.
- Live with it for several days; verify edits propagate both directions across all four endpoints; watch for conflicts.
- **Gate:** reliable in real life over a multi‑day window.

### Phase 6 — Upstream prep
- De‑WIP; complete the PR checklist; fold the findings report, test pyramid, and video evidence into the PR description.
- Wire CI (GitHub Actions) for the **CLI‑able** suites (unit + integration + cross‑compat). The two‑emulator E2E stays a **local/manual gate with recorded evidence** (two emulators on hosted macOS CI is expensive and itself flaky).
- **Gate:** PR ready for maintainer review with evidence attached.

## E2E Orchestrator Design (Option 1)

A single top‑level orchestrator (shell or Python under `e2e-tests/`) that:

1. Boots **two isolated AVDs** (separate AVD names / data dirs), records each serial.
2. Installs the build on both; loads a distinct personal database fixture into each.
3. Runs **per‑emulator Maestro subflows** addressed by `--device <serial>`, reusing existing `.maestro` flows where possible.
4. Implements the **deterministic "shared folder"**: after party A exports/syncs, the orchestrator `adb pull`s the container from A and `adb push`es it to B (and vice versa) — standing in for Dropbox with full ordering control.
5. Records **both screens** (`adb screenrecord` / `scrcpy --record`) for the video artifact.
6. Asserts via Maestro UI assertions **and** out‑of‑band file/entry checks (e.g. `keepassxc-cli` reads of pulled containers).

Scenario coverage: one‑way Import, one‑way Export, **two‑way Synchronize**, and an **edit‑conflict** case (both sides edit the same entry between syncs → newer‑wins + history preserved).

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| .NET 9 / Android workload setup friction | Phase 0 gate before anything depends on a local build. |
| Pure‑logic extraction is more invasive than hoped | Decide scope in Phase 1 triage; if too costly, fall back to emulator E2E as lowest real‑code test and document the gap. |
| Two‑emulator E2E flakiness (the thing we're trying to avoid) | Deterministic harness sync; isolated AVD state; explicit waits; Phase 4 N‑run gate. |
| KP2A↔KeePassXC format incompatibility surfacing only in real life | Dedicated cross‑compat suite (Q3=C) incl. signed containers, before dogfooding. |
| Two separate Dropbox accounts | Dropbox **shared folder** for the Lindrew container; validated in Phase 5. |
| Real‑data risk during dogfooding | Start with throwaway secrets; keep per‑database backups before enabling Synchronize on real DBs. |

## Success Criteria

- All four test layers green; E2E + cross‑compat **10/10** with recorded video.
- KeeShare verified across **Import, Export, Synchronize, and signed containers**, and across **KP2A↔KeePassXC**.
- "Lindrew" Synchronize group working reliably across Andrew's + Lindsay's four endpoints for several days.
- PR #3165 de‑WIP'd with findings + test + video evidence attached.

## Open Questions (to resolve as we go)

- Exact scope of the pure‑logic extraction (decided in Phase 1 triage).
- Orchestrator language: shell vs Python (decide at Phase 3 start; Python likely easier for sequencing + assertions).
- Whether to also stand the integration layer up against KeePassLib directly vs. the extracted lib only.
