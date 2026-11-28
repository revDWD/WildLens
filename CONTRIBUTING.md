# Contributing to WildLens

Read [`AGENTS.md`](AGENTS.md) first: it explains what WildLens is for and what it deliberately is not. This file is the
practical side.

## Setup
1. Unity 2021.3 LTS or newer (Unity 6 + URP is the primary target), and Git.
2. A Unity project with the package added from disk (**Package Manager ▸ + ▸ Add package from disk…**, pick this
   repo's `package.json`) so your edits are live, or from the git URL to test a pushed version.
3. Open or create a scene, **WildLens ▸ Vertical Slice Builder ▸ Build Slice In Open Scene**, save, press Play.

## The loop
1. Change code or builder logic.
2. **Compile check** (seconds, no Unity needed; needs the .NET SDK 6+): `Tools~/CompileCheck/compile-check.sh` builds five
   configurations (2021.3 legacy input, Input System, Unity 6 + URP, and both editor builds). See its README.
3. **Rebuild the slice** in Unity (the scene is generated; never hand-edit it). Builder output changed? Bump
   `WildLensVersion.Environment`, or players' scenes won't be flagged as stale.
4. **Playtest the change in the place it affects, at the time of day it affects.** Hold T to let time pass. F3 shows
   camera diagnostics and the scene's build version; F4 runs the zoom self-test; **WildLens ▸ Capture Comparison
   Shots** saves before/after views from every scenic viewpoint.
5. Screenshots decide. If it doesn't look or feel better on screen, it isn't.

## Rules that keep the project healthy
- **Every file outside `~` folders needs a `.meta`** with a unique GUID (Unity can't generate them inside an
  immutable package). Folders ending in `~` (`Art~`, `Documentation~`, `Tools~`) are ignored by Unity.
- **Only CC0 art is committed** (`Art~/CREDITS.md`). Store assets stay in users' projects.
- **Enums that are serialised are append-only** (`AnimalActivity`, `HotspotType`, `PlaceholderBody`, `RhythmPlace`...).
- **Species assets and journal saves are versioned and migrated** (`SpeciesAssetMigrator`, `JournalData`): never
  break a user's tuned assets or journal.
- **Walkable things are sized for the player**: doors ≥ 1.0 m × 1.95 m, steps ≤ 0.4 m.
- **Never write to shared assets in Play mode** (copy sky materials and volume profiles first, as `DayCycle` does).

## Release checklist
1. `Tools~/CompileCheck/compile-check.sh` passes.
2. Rebuild the slice in a clean project (no Terrain) and in one with an existing Terrain; play a full day.
3. Bump `package.json` and `WildLensVersion.Package`; bump `WildLensVersion.Environment` if builder output changed;
   update `WildLensVersion.Features`, `RecentChanges`, `ReleaseName`, `ReleaseDate` (the trailhead board shows them).
4. `ArtLibrary.ImportVersion` and `Art~/CREDITS.md` if `Art~` changed.
5. `CHANGELOG.md`, `Documentation~/Systems.md`, and `AGENTS.md` §6 (current state), `Documentation~/History.md` (the release and its lessons) and
   `Documentation~/Roadmap.md` (next steps).
6. Commit, push; users update via Package Manager and **rebuild the slice**.
