# Architecture

How WildLens is put together: packaging, assemblies, the file map and the key flows through the code. Start from
[AGENTS.md](../AGENTS.md) for what the project is and why; come here when you need to find where something lives.
Per-system detail and tuning numbers are in [Systems.md](Systems.md).

## Packaging and workflow

- A Unity **UPM git package** (`package.json`, name `com.revdwd.wildlens`), installed by Package Manager git URL
  `https://github.com/revDWD/WildLens.git`. Unity 2021.3+, with Unity 6 tested. URP is supported (`WILDLENS_URP`);
  the Built-in pipeline is the fallback. Both the Input System (`WILDLENS_INPUT_SYSTEM`, `_1_9`) and legacy input work.
- Assemblies: `Scripts/WildLens.Runtime.asmdef` and `Editor/WildLens.Editor.asmdef` (version defines in each).
- Every file outside `~` folders needs a committed `.meta`. `Art~` and `Documentation~` are ignored by Unity.
- **The player's flow:** WildLens ▸ Vertical Slice Builder ▸ (Re)build Slice In Open Scene, then Play.
- **Compile check without Unity:** `Tools~/CompileCheck/compile-check.sh` (NuGet reference DLLs plus small stubs for
  uGUI / Input System / URP in `Tools~/CompileCheck/stubs/`; add a stub member when you use a new API from those).
- **When the builder's output changes, bump `WildLensVersion.Environment`** (`Scripts/Core/WildLensVersion.cs`)
  so old scenes are flagged as stale. Also bump `WildLensVersion.Package` and `package.json` each release.
- **When `Art~` changes, bump `ArtLibrary.ImportVersion`** (`Editor/ArtLibrary.cs`) so the art is re-copied.
- **Each release, update `WildLensVersion.Features`, `RecentChanges`, `ReleaseName` and `ReleaseDate`.** The
  trailhead board shows them.
- Species assets and journal saves carry data versions and are migrated automatically
  (`Editor/SpeciesAssetMigrator.cs`, `Scripts/Journal/JournalData.cs`). Enum values are serialized as integers:
  append to them, never reorder.

## File map

```
Scripts/
  Core/          WildLensGame (bootstrap, journal access), GameInput (both input backends), PlayerPresence
                 (noise/silhouette for awareness), MaterialLibrary (URP/Built-in materials), SceneQuery,
                 TerrainUtil, PrimitiveMeshes, WildLensVersion
  Player/        FirstPersonController (walk/sprint/crouch), PlayerCameraRig (bob, sway, breathing, pan lag,
                 raise roll; EnsureComponents wires the camera stack)
  Vehicles/      TrailBike (mount/park with R, kinematic terrain-following ride, rider look, engine noise to
                 wildlife), TrailBikeModel (primitive placeholder), TrailBikeEngine (procedural engine sound)
  CameraSystem/  CameraModeController (RMB raise, latch), ZoomLens (1/2/4/8/16× steps, FOV), FocusController
                 (AF hunt/lock), LensEffects (URP depth of field), CameraAudio (procedural shutter/AF sounds)
  Photography/   PhotoCamera (capture), PhotoAnalyzer (visibility, frame fill), PhotoQualityEvaluator
                 (Poor/Good/Excellent), FramingAssistant (live brackets), Photographable, PhotoStorage,
                 MacroSubject + MacroAnalyzer (macro: small subjects, close-range grading),
                 MacroMotion (1.7: flit, perch, hop and glow for small life from packs)
  Observation/   ObservationTracker (hold in view → identified → journal)
  Wildlife/      AnimalAgent → GroundAnimal / FlyingAnimal (behaviour states), Awareness/WildlifeAwareness
                 (Calm/Aware/Alert/Fleeing), WildlifeSpawner, WildlifeWorld (trees, perches, hotspots),
                 PlaceholderAnimalBuilder + AnimalRig + ProceduralAnimalAnimator (placeholder bodies),
                 AnimalAnimatorBridge (drives Mecanim on authored prefabs), WildlifeSchedule (daily rhythms)
  Environment/   HabitatHotspot, ScenicViewpoint, EnvironmentFeature, TrailPath, FoliageSway, WildLensWind,
                 SliceInfo (build version marker), ReserveDestination (named place, residents, arrival note),
                 ReserveTrack (track polyline), ObservationBlind (concealment), ReserveInfoBoard (trailhead board + map),
                 ReserveCreek (creek line), ReserveParking (bike pull-offs), WildlifeResidence (species at a place),
                 RideableSurface (bridge decks the bike rides), AmbientSound (procedural wind/brook),
                 DayCycle (clock, sun, sky, fog, grading), MistPatch (dawn mist), ScheduleAnchor (water/woodland/ridge),
                 WeatherCycle (four-day forecast, cloud cover, rain, overcast sky), WaterFlow (moving ripples),
                 Waterfall (spray, roar, spate after rain), ReserveDiscovery (hidden place), ReflectionRefresh
  Journal/       JournalService, JournalData (save + migration)
  UI/            HudController (owns views), ViewfinderView, PhotoPreviewView (prints, keeper), ShutterEffectView,
                 NoticeView, JournalUI, UIFactory
  Data/          SpeciesDefinition (all per-species tuning), SpeciesDefaults, SpeciesDatabase, AnimalActivity,
                 NaturalistSubject + SubjectDefaults (macro subjects; fourteen since 1.7)
  Debugging/     CameraDiagnostics (F3 overlay, F4 zoom self-test)
Editor/
  WildLensSliceBuilder (pipeline), WildLensBuilderWindow, SliceBuildSettings, TrailBuilder, PropBuilder,
  VegetationBuilder (+ partials TerrainPainter, HabitatHotspotBuilder, ViewpointBuilder, ReserveBuilder,
  EcosystemBuilder: creek, pull-offs, storytelling, residences, anchors, mist; RoadBuilder: Reserve Road),
  FoliageCards (leaf clusters, conifer crowns),
  ArtLibrary (CC0 art import, prefabs, sky), GeneratedAssets, ProceduralMeshes, ComparisonCapture,
  SpeciesAssetMigrator, AnimalModelSetup (bought model → species prefab),
  EnvironmentKit (free packs by ecological role), EnvironmentKitScanner (fills the kit from the project),
  KitPlacement (VegetationBuilder partial: places kit assets, adds colliders/features),
  FordBuilder (VegetationBuilder partial: the Creek Ford), SignKit (sign standard, pictograms, text measuring),
  SignCheck (probe: lettering measured against boards), FallsBuilder (Willow Falls), DiscoveryBuilder (hidden
  places), MacroBuilder (macro subjects; 1.7 small life from packs), GroundingPass (posts and legs to the ground),
  AssetAudit (1.7: the report of what is in the user's packs), MacroPackSetup (1.7: small-life models to subjects)
Art~/            CC0 Poly Haven skies, terrain, surfaces, models, foliage cards (CREDITS.md)
Documentation~/  Systems.md (systems and tuning), VisualRoadmap.md (assets, licensing, weaknesses),
                 ReserveExpansion.md (reserve design and next steps), WildlifeExpansion.md (species framework & roadmap),
                 EnvironmentMigration.md (environment audit, free assets, migration plan, before/after),
                 WildlifeAssetStrategy.md (animal model options and the style decision),
                 WildlifeCampaign.md (1.7: audit, species, small life, bike and ecology plan)
Tools~/          CompileCheck (compile the package without Unity)
CONTRIBUTING.md  setup, build → playtest loop, release checklist
```

Key flows:
- **Photography:** `CameraModeController` → `ZoomLens` sets FOV (re-asserted just before rendering) →
  `FramingAssistant` previews the grade → `PhotoCamera` captures → `PhotoAnalyzer` + `PhotoQualityEvaluator` grade →
  `JournalService` records → `HudController` shows the print.
- **Zoom:** discrete levels; wheel *direction* only (never magnitude); E/Q keys; zoom input raises the camera.
  FOV 50° at 1× down to 3.34° at 16×. Physical-camera, Cinemachine and other FOV writers are detected and overridden.
- **Awareness:** stimulus = distance × player noise × line of sight × species profile, with memory. Shutter sound
  is audible. Standing still always decays awareness. Tune on the species assets.
- **Behaviour:** per-locomotion state machines (deer walk/run/graze/rest, rabbit sit/hop, hawk fly/land/perch,
  squirrel climb/cling). Animals favour **habitat hotspots** (meadows, brush piles, mature trees, snags).
- **Journals:** identification by observation or photo; best photo per species; versioned save.
- **Visuals:** builder pipeline: trail loop → trail props (kiosk, bike) → **reserve plan** (sites, terrain shaping,
  tracks) → trees → outcrops → rocks/logs → bushes → hotspots → viewpoints/compositions → **destinations** → undergrowth →
  terrain paint and grass → wind → lighting (HDRI sky with sun alignment) → render quality → URP post → player.
- **Reserve:** destinations chosen from the terrain (ridge = prominence, pond = basin, grove = forest density,
  blind = open flat ground), tracks by A*, residents spawned by `WildlifeSpawner.SpawnResidents`. Everything near a
  path uses "distance to any path" (loop or track) for clearance and painting.
- **Ecosystem (0.8):** plan order is sites → terrain shaping (pond, ridge) → arrivals → **creek** (route downhill,
  carve, water) → blind pad → tracks (cross the creek squarely) → **pull-offs** → scenery (density zones; riparian
  bias; creek and keep-out exclusion) → destinations → **ecosystem build** (bridges, creek life, markers,
  residences) → painting (creek bed/banks; asters; no grass in keep-outs).
- **Time of day (0.9):** `DayCycle` → sun/sky/fog/grading each frame; `PeriodChanged` → `WildlifeSchedule` moves homes
  and sets activity bias, HUD shows a field note, board updates its "Now" line; `MistPatch` reads the hour.
- **Environment Kit (1.2):** `SliceBuildSettings.environmentKit` → `VegetationBuilder.PrepareAssets` merges kit props
  into the CC0 lists → `Conifer()`/`Deciduous()`/`GiantTree()` return kit trees by role (`KitPlacement`) → terrain
  painter swaps in kit layers and grass per slot. Empty kit: no change, same random sequence.
- **Weather (1.4):** `WeatherCycle` (forecast day = `DayCycle.Day`) → `Cover`/`Rain` → `DayCycle.Apply` dims and greys the
  light, swaps to the overcast sky; rain particles and sound; `WildLensWind.WeatherMultiplier`; `WildlifeSchedule`
  re-places animals when rain starts or stops; HUD field notes and board conditions on `Changed`.
- **Macro (1.5):** M → `ZoomLens.MacroMode` (24° FOV) → `FocusController` focuses on `MacroAnalyzer.Centred` (≥ 8 cm) →
  shutter → `MacroAnalyzer.Analyze` grades frame fill, focus, centring → `JournalService.RecordNotebook` → HUD note.
- **Riding:** `TrailBike` disables the walking controller and parents the player to the seat; it reports presence
  (with `EngineNoise`) and blocks camera raise while moving; parking puts the player back on foot beside it.

**1.6 additions:**
- `Editor/AnimalPrefabFactory.cs`: clip classification, Animator Controller and species prefab creation (shared).
- `Editor/WildlifePackSetup.cs`: **WildLens ▸ Wildlife Pack Setup**: scans the project for animated models, matches
  them to species (exact or stand-in), tints stand-ins, and sets up many species at once.
- `Editor/PropBuilder.cs` `BuildKiosk`/`BuildBikeBay`: the three-sided Reserve Board and the trailhead bike bay
  (replacing the separate guidelines post, register post and Reserve Road plank).
- `Editor/GroundingPass.cs` `SeatLogs`: log seats follow the slope.

**1.7 additions:**
- `Editor/AssetAudit.cs`: **WildLens ▸ Asset Audit Report**. Read-only probe: walks every model and prefab under `Assets/`,
  classifies by name then folder (reserve species via `WildlifePackSetup.MatchAsset`, other animals, macro subjects,
  vehicles, scenery), reads clips through `AnimalPrefabFactory.TryClassifyName`, and writes `WildLensAssetAudit.md` in
  the project folder and to the clipboard. Also a button in `WildLensBuilderWindow`.
- `Editor/WildlifePackSetup.cs`: the name tables sit between `<pure-begin>` and `<pure-end>` markers (no Unity types) so
  they can be tested outside Unity. A new species needs an entry in `Names` to be matched; `Reserved` lists animals that
  are never stand-ins. `Editor/AnimalPrefabFactory.cs`: token-aware clip classification, with an ignore list.
- **Small life (1.7):** `SubjectDefaults` (twelve new `NaturalistSubject` assets, created once in
  `Assets/WildLens/Data/Subjects`) → `MacroPackSetup.AutoSetUp` (start of the macro step of every build, if
  `autoMacroFromPacks`) fills each empty `prefab` from the user's packs → `VegetationBuilder.BuildMacroSubjects` places the
  bundled plants as before, then `BuildSmallLife` (own seeded generators, habitat by habitat) → each instance carries
  `MacroSubject` (focus, hours and weather via `IsShowing`, `RecentlyRained`) and, if not `Still`, `MacroMotion`.
