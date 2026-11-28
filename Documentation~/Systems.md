# WildLens systems guide

How the gameplay systems fit together, and where to tune them. Values live on components (Inspector) or on
the species assets in `Assets/WildLens/Data/Species/`.

```
Input ─► FirstPersonController (walk / sprint / crouch) ──► PlayerPresence (noise, silhouette, head height)
     └─► CameraModeController ─► ZoomLens (FOV)                         │
                 │                    │                                  ▼
                 │                    │              WildlifeAwareness ──► GroundAnimal / FlyingAnimal
                 ▼                    ▼                                  ▲            ▲
          FramingAssistant ──► ObservationTracker ──► JournalService     │     WildlifeWorld (trees,
                 │                                          ▲            │     perches, hotspots)
                 ▼                                          │      shutter sound
          PhotoCamera ──► PhotoAnalyzer ──► PhotoQualityEvaluator
                 └──────────────── events ──► HudController (Viewfinder / Shutter / Preview / Notice views)
```

## The encounter loop this is tuned for

Spot an animal → raise the camera (RMB) → zoom (scroll) → the viewfinder brackets it; hold it there and it is
**identified by observation** → **crouch** (C) and approach → it becomes Alert and stares → **freeze**; it
relaxes → brackets turn green (Good) or gold (Excellent) → take the photograph.

## Zoom: why it looked broken, and the proof tools (0.5.0)

**Symptom (after 0.4.0):** scroll changed the zoom row, the big "8×" readout and the focal length, but the view barely changed.

**Root causes found in the code:**
1. *Initialization order.* Scenes built before 0.2.0 get their camera components from `PlayerCameraRig.EnsureComponents()`
   at runtime, and `ZoomLens` was added first. Unity runs `Awake` inside `AddComponent`, so `ZoomLens.Awake` looked up
   `CameraModeController` before it existed and cached `null`. Camera mode then never counted as active inside the lens, and
   the FOV stayed at the walking 65°. The HUD held its *own* valid reference, so every indicator kept updating.
   **Fix:** all cross-component references resolve lazily.
2. *Preselect by design.* Since 0.3.0, scrolling with the camera lowered only preselected a level and showed
   "Lens 8× · 205 mm" without touching the view. **Fix:** zoom input now raises the camera ("latched"; RMB, sprint
   or zooming out past 1× lowers it), so every zoom step is visible.

**Hardening:** the lens applies the FOV in `LateUpdate` at execution order 10000 and re-applies it in
`beginCameraRendering` / `onPreCull`, so a script writing the FOV later in the frame cannot win (counted in F3).
Physical-camera mode, a custom projection matrix, an orthographic camera or a `CinemachineBrain` on the player camera,
all of which stop `fieldOfView` from controlling the image, are corrected and logged once.

**Proof tools:** F3 shows a render-time probe: which cameras drew to the screen this frame, with both their `fieldOfView`
and the FOV in the projection they actually rendered with. **F4** runs a self-test: it raises the camera, steps
1×→16×, records the rendered projection, measured magnification and the on-screen height of the animal nearest the
centre at each level, saves `zoom_<n>x.png` screenshots to `persistentDataPath/WildLens/ZoomTest/`, and reports
PASS/FAIL (fails if another camera is on screen or the projection doesn't match the level).

Expected numbers: 1× = 50.0°, 2× = 26.2°, 4× = 13.3°, 8× = 6.7°, 16× = 3.35°. A 1.75 m deer at 80 m is 2.3% of the
frame height at 1× and 37.5% at 16×.

## Zoom: investigation and fix (0.4.0)

**Symptom:** the scroll wheel appeared to do nothing in camera mode.

**What was checked:** wheel input, Input System vs legacy backends, zoom state changes, FOV changes, viewfinder
readout. The state → FOV → readout chain was sound. Input was not.

**Root cause:** `GameInput.Zoom` divided the Input System's scroll value by 120, assuming Windows-style raw deltas
(±120 per notch). Since **Input System 1.9** (Unity 6 ships 1.11+), the default `InputSettings.scrollDeltaBehavior` is
`UniformAcrossAllPlatforms`, which normalises scroll to **±1 per notch**. One notch therefore read as 0.008, and the
lens needed an accumulated 0.5 (~60 notches) to change level. The legacy Input Manager path was unaffected.

**Fix:** zoom now uses the **direction** of the wheel (`GameInput.ZoomDirection`), never its magnitude, with a 0.09 s
step limit. It behaves identically with either Input System setting, the legacy Input Manager, mice and trackpads.
**E / Q** zoom in / out from the keyboard. Every step now also shows a large transient readout (e.g. "8×"), plays the
detent tick, and changes the focal-length readout and depth of field.

**Diagnostics (F3):** input backend and scroll-delta behaviour, raw scroll value and how it's interpreted (±1 notch,
±120 notch, fractional), zoom inputs received / applied / ignored (with the reason: cooldown, mouse not captured,
journal open, at limit), level, live and target FOV, focal length, camera mode. It warns, and logs once, if another
component changes the FOV or another camera renders over the player camera, the two other ways zoom could look
broken. While visible, each scroll event is logged to the Console.

## Camera — `Scripts/CameraSystem/`

| Component | Responsibility |
|---|---|
| `CameraModeController` | Hold **RMB**. `IsActive`, eased `Blend` (~0.22 s), `IsReady` (can shoot), movement ×0.5, look ×0.8 (then scaled by zoom). Events `Entered` / `Exited`. |
| `ZoomLens` | Sole owner of FOV. Walking 65°; camera 1× = 50° (≈26 mm). Steps **1× 2× 4× 8× 16×** (16× ≈ 410 mm), smoothed in log space. The wheel also works with the camera lowered: it **preselects** the lens (a notice confirms it). Exposes `EquivalentFocalLength` for the readout. |
| `FocusController` | Focus distance: the tracked subject, else whatever is under the centre. It pulls smoothly in log-distance space (a visible "rack"). `FocusLocked` fires when a centred subject comes sharp (soft double beep, focus marks pulse). |
| `LensEffects` | URP only. Runtime volume weighted by the camera-mode blend: bokeh **depth of field** using the lens's equivalent focal length (sharp at 1×, background melts at 8–16×) and a stronger vignette at the eye. Photos include it. |
| `CameraAudio` | Shutter, zoom detent, raise rustle, focus beep, identification chime (synthesised; assign clips to replace). |
| `PlayerCameraRig` (Player) | Head-bob (reduced in camera mode and crouched). Raised: dip + roll that settle, slow breathing, rotational inertia (lags fast pans, springs back), handheld sway (+25% per zoom doubling, settles when still/crouched). Adds any missing camera components at start-up. |
| `CameraDiagnostics` (Debugging) | F3 overlay described above. |

## Photography — `Scripts/Photography/`

* **`PhotoAnalyzer.TryMeasure`** measures one subject from the live projection, so zoom is accounted for automatically:
  * **frame fill**: the larger of subject-height/frame-height and subject-width/frame-width, so it doubles when the zoom doubles
  * **visibility**: 9 points inside the body that are in frame and unobstructed
  * **centring**: per-axis offset from centre; 0.33 means on a third
  * **distance**: relative to the species' `clearPhotoDistance`
* **`PhotoQualityEvaluator`** applies the rules (fills are × the species' `photoSizeScale`: deer 1, fox 0.85, hawk 0.8, rabbit 0.7, squirrel 0.6):

  | | Fill | Visibility | Centre offset | Distance |
  |---|---|---|---|---|
  | Identified | ≥ 2.5% | ≥ 30% | | |
  | Good | ≥ 10% | ≥ 45% | ≤ 0.75 | ≤ 2.5 × clear |
  | Excellent | ≥ 25% | ≥ 75% | ≤ 0.4 (thirds count) | ≤ clear |

  Below Excellent, a hint names the first thing to fix: *too close, zoom out* · *partly hidden* · *small in frame,
  zoom in* · *too far for fine detail, get closer* · *off-centre*. A photo that identifies nothing says why the
  nearest animal didn't count (*too small: zoom in*, *view blocked*, *too close*).
* **`FramingAssistant`** (subject tracking, 10 Hz): picks the subject nearest the frame centre (sticky), measures it with
  the same code as a photo and previews its class. The viewfinder draws brackets around it: dim if it can't be
  identified, then white/green/gold for Poor/Good/Excellent.
* **`PhotoCamera`**: shutter only in camera mode; capture flow and events as before; the shutter is audible to
  animals within 12 m.

### Why the old rules failed (0.2.0 → 0.3.0)
0.2.0 measured **area** coverage against a demanding per-species ideal. Area falls with distance², so at 1×
nearly nothing beyond an animal's flee distance could be identified, and 8× was the only zoom that worked.
Linear fill with species-independent thresholds makes every zoom step change the result, while the clarity
distance keeps the best photos tied to a careful approach.

## Observation — `Scripts/Observation/ObservationTracker.cs`

Keep a tracked, identifiable subject within half-frame of centre and ≥1.5× the minimum identifiable size for its
species' `observationTime` (1–1.5 s) and it is identified: journal entry, notice and chime, with no photo needed.
Brief interruptions are forgiven (0.5 s) and progress drains instead of resetting. Watching known species records
their behaviours (`SpeciesRecord.observedBehaviors`).

## Crouch — `Scripts/Player/FirstPersonController.cs`

**C** toggles (Left Ctrl hold also works in builds). 1.5 m/s (0.75 in camera mode), smooth capsule/camera
lowering (1.65 → 0.95 m eye height), won't stand up under cover, sprinting stands you up.
Effect on wildlife (`PlayerPresence`): footfall noise ×0.5, silhouette ×0.55, and the lower head is hidden by
bushes, brush piles and rocks.

## Wildlife Awareness — `Scripts/Wildlife/Awareness/WildlifeAwareness.cs`

`stimulus = noise × (line of sight ? silhouette : 0.35)`
`gain/s = suspicionGain × stimulus × shelter × memory × (0.25 + 1.75 × proximity²)`, ÷ `alertPatience` while Alert.
Standing still subtracts `suspicionDecay`/s; out of range decays at 1.5×. Memory after fleeing: ×1.3 for 20 s.

| State | Enter when | Animal does |
|---|---|---|
| Aware | suspicion ≥ 0.25 inside awareness radius | glances at you, won't rest, drifts away |
| Alert | ≥ 0.6 inside alert radius (suspicion is capped below this outside it) | stops and stares; rabbits/squirrels sit up; deer flag the tail |
| Fleeing | suspicion 1 (only while Alert), approaching past `alertPatience`, inside flee radius while noticed/conspicuous, sprint startle | runs a short way and **looks back** (4–8 s), squirrels climb, hawks fly away |

| Species | Aware / Alert / Flee (m) | Gain | Decay | Patience | Character |
|---|---|---|---|---|---|
| Deer | 36 / 24 / 10 | 0.8 | 0.08 | 6 s | watches a long time; freeze and it settles |
| Rabbit | 16 / 10 / 4 | 1.3 | 0.12 | 3 s | notices fast, freezes rather than bolts |
| Fox | 26 / 15 / 8 | 0.9 | 0.10 | 4 s | |
| Squirrel | 12 / 7 / 3.5 | 0.9 | 0.15 | 2.5 s | tolerant; escapes up a trunk and watches |
| Hawk (perched) | 40 / 26 / 14 | 0.9 | 0.08 | 3 s | needs patience; ignores you in flight |

Simulated outcomes (open ground, no cover): walking spooks everything at 7–22 m; crouch-creeping reaches
rabbits, foxes and squirrels; deer and perched hawks need the creep-and-freeze approach, which then allows
minutes inside Excellent range.

## Habitat hotspots — `Scripts/Environment/HabitatHotspot.cs`

Types: **Meadow** (deer; lush flower-rich grass), **BrushPile** (rabbits, foxes), **MatureTree** (squirrels; big old
trees), **Perch** (hawks; dead snags at meadow edges). Each species has `preferredHotspots` and
`hotspotAffinity` (chance a group's home range is centred on one). Hawks pick perches inside Perch hotspots 4× as
often. Animals still wander, flee and return home, so hotspots stay productive without scripting.
The builder places hotspots with visible cues; without any in the scene, `WildlifeWorld` derives them from the
scenery. Gizmos show them in the Scene view.

## Extension points (not implemented yet)

* Behaviour photography / field notes: `PhotoSubject.activity`, `SpeciesRecord.observedBehaviors`,
  `ObservationTracker` (one place to add behaviour-specific observation).
* Seasonal activity: `HabitatHotspot.weight` and `SpeciesDefinition.hotspotAffinity` are runtime-tunable weights.
* Rare variants: `SpeciesDefinition` plus `PhotoSubject.speciesId`; journal records are per species id.
* Sound identification: `ObservationTracker` already turns sustained attention into a journal entry; a sound-based
  source can call `JournalService.RecordObservation` the same way.

## Environment & presentation (0.4.0)

All built by **WildLens ▸ Build Vertical Slice In Open Scene** (rebuild to apply):

* **Terrain surface** (`Editor/TerrainPainter.cs`): five generated tileable layers with normal maps (grass, sunlit
  meadow, forest floor, worn dirt, rock), painted by slope, trail distance, forest density, tree proximity and meadow
  hotspots. The terrain's original TerrainData is backed up to `Generated/Backup` first. When the terrain is
  painted at ≤ 0.6 m per texel, the trail is a worn path in the terrain rather than a mesh ribbon.
* **Terrain details**: Unity terrain grass (waves in the wind in Built-in/URP): short grass, tall meadow grass, yellow
  and white wildflowers, woodland ferns, thinning along trail edges. HDRP or the option off: mesh grass as before.
* **Wind** (`WildLensWind`, `FoliageSway`): gusts travel across the land; trees and bushes bend from the base (higher
  parts move more). A WindZone drives prefab/SpeedTree trees.
* **Vegetation**: conifers with 4–5 irregular two-tone tiers; broadleaf crowns of several lobes in two shades; rocks in
  embedded outcrops (slopes, woodland edges) with a few lone boulders; stumps.
* **Scenic viewpoints** (`ScenicViewpoint`): Meadow Overlook platform and a Lookout bench, view cones cleared.
* **Lighting**: warm late-morning sun with soft shadows; sky-matched trilight ambient; exponential haze for atmospheric
  perspective; tuned procedural sky; shadow distance 160 m with 4 cascades and 2048 maps (pipeline or Quality settings).
* **Grading (URP)**: global volume: ACES, warm white balance, slight contrast and saturation, cool shadows, warm
  highlights, gentle bloom, faint vignette. Camera: HDR, post-processing, SMAA.
* **Comparison captures**: **WildLens ▸ Capture Comparison Shots** renders fixed views to `<project>/WildLensCaptures/`.

## Signage, woodwork and water (1.3.0)
* **Sign standard** (`Editor/SignKit.cs`): `Sign(parent, pos, rot, title, line, icons, number, post)` builds a dark
  stained board sized to its words: number disc (destinations), bold title (`TitleSize` 0.024 ≈ 10 cm capitals), one
  row of pictograms plus a short line (`LineSize` 0.015). `Panel(...)` builds the interpretive lectern: pictogram,
  title on a green band, two short lines. `TextWidth` sums the font's glyph advances (TextMesh: one pixel at size 64 is
  0.1 × characterSize m); `Fit` shrinks text to a width. `TimeIcons(bestTimes)` turns best-times words into sunrise /
  sun / sunset / moon pictograms. Icons are 128 px generated alpha PNGs in `Generated/Icons` (append to `SignIcon`).
* **Trailhead board** (`ReserveInfoBoard`): header (name; period, clock, `Conditions()` and `RightNow()`, refreshed
  every 2 s), a 582-unit map with round numbered markers, destinations (number disc, name, best-time icons from
  `timeIcons`, distance), footer (etiquette; version or stale warning). 500 canvas units per metre.
  `Conditions()` follows the light until a weather system exists.
* **Woodwork** (`GeneratedAssets.ApplyWoodGrain`): materials named `Wood`, `WoodDark`, `SignBoard` get the scanned plank
  texture with role tints. `PropBuilder.Box` gives textured boxes `MetricCube(size)` meshes: UVs in metres of the scan
  (1.8 m per repeat), U along each face's longer side, cached as sub-assets of `Generated/Mesh_PropBoxes.asset`.
* **Water** (`EcosystemBuilder.WaterMaterial`, `RippleNormal`): dark base, generated tileable ripple normal map,
  smoothness 0.8 (creek) / 0.88 (pond), set explicitly so old materials update. `WaterFlow` (runtime) scrolls the
  normal map on a material instance: creek 0.09 UV/s downstream (V runs along it, 4 m per unit), pond a slow drift.
* **Vegetation tweaks**: terrain fern detail near the creek only in shade (`shade` from the forest field); sunny
  banks get meadow grass (detail layer 1) instead. Spreading broadleaf limbs 20% steeper, rounder clusters, plus
  mid-limb clusters on alternate limbs and one raised top, from a position-seeded `System.Random` so placement is
  unchanged.

## Environment Kit and outcrops (1.2.0)
Plan, asset list and instructions: `EnvironmentMigration.md`.
* **Kit asset** (`Editor/EnvironmentKit.cs`, saved at `Assets/WildLens/Data/EnvironmentKit.asset`, referenced by
  `SliceBuildSettings.environmentKit`): prefab arrays per role (`conifers`, `broadleaf`, `riparian`, `oldGrowth`,
  `young`, `shrubs`, `ferns`, `rocks`, `boulders`, `logs`, `stumps`), five `TerrainLayer` slots in the painter's order
  (grass, meadow, forest floor, dirt, rock) and six `KitDetail` slots (short grass, meadow grass, yellow, white, fern,
  purple), each an instanced mesh prefab or a billboard texture with `density` and `scale`. `treeScale`,
  `replaceAllGeneratedTrees` (roles borrow from each other; never mix in generated trees), `mixWithBundledArt`.
* **Placement** (`Editor/KitPlacement.cs`, a `VegetationBuilder` partial): `Conifer()` and `Deciduous()` return a kit
  tree first, so every caller (scatter, grove, edges) is covered; `BuildTrees` asks for a **riparian** tree within 16 m
  of the creek (75%), swaps edge "young" trees for real saplings, and the grove's giants use `GiantTree()` (kit giants
  scaled ×1.1–1.3, not ×1.7–2). Kit props are merged into the CC0 lists (`_artShrubs`, `_artRocks` …) in
  `PrepareAssets`, so every builder that uses scans uses them too; `PlaceArt` gives them gameplay components; shrubs
  are used beyond 30 m only when the kit has some. Terrain: `ApplyKitLayers`, `ApplyKitDetails` (mesh details use
  `useInstancing` on 2022.2+ by reflection), per-slot density multipliers.
* **Gameplay components** (`EnsureTreeGameplay`, `EnsurePropGameplay`): solid trunk capsule (r = 0.12 + 1.2% of height)
  if the prefab has no solid collider; trigger crown capsule (0.6 h centre, 0.75 h tall, 0.17 h radius) if it has no
  trigger; `EnvironmentFeature`; one-level distance cull unless it has a LODGroup. Props: box collider from the
  unrotated mesh bounds (trigger for shrubs) and a feature. Kit trees and shrubs are never static-batched.
* **Determinism**: every kit check short-circuits before calling the random generator, so an empty kit leaves the
  sequence (and the reserve) identical. Keep it that way.
* **Scanner** (`Editor/EnvironmentKitScanner.cs`): every `TerrainData` (tree prototypes, detail meshes/textures,
  layers), every `TerrainLayer`, every prefab/model/SpeedTree asset under `Assets/` except `Assets/WildLens`. Words
  from the file name first, then folders (digits dropped, camelCase split); unnamed trees by silhouette (width/height
  < 0.42 → conifer); rocks > 2.5 m → boulders, > 12 m rejected (cliffs); no named giants → broadleaf trees ≥ 1.25×
  the median height and > 12 m. Rejects: stylised/low-poly/fantasy/wrong-biome words, dead trees, LOD1+/billboard/
  collider parts, materials missing or for another pipeline. Up to 8 per role, sorted by path. `Rescan` replaces.
* **Outcrops** (`VegetationBuilder.BuildOutcrops/PlaceOutcrop`, `ArtKind.Outcrop`): Poly Haven Rock Face 01/02 are open
  shells facing up and toward +Z; `ArtLibrary.FaceForward` re-measures that from the mesh normals (meshes imported
  readable), the material is double-sided, colliders are exact mesh colliders. Up to 9 (×population scale) on the
  steepest free slopes of 20–48° (≥ 7 m from paths, 9 m from the road, 6 m from the creek, 9 m apart), +Z aimed
  downhill ±20°, base sunk 18–28% of height; two more on the ridge overlook's flanks (75–105° off the view).

## 1.0.0 additions
* **Road grading** (`RoadBuilder.GradeRoads`): per road sample, a ±15 m moving average of ground height; heightmap
  cells within 2.1 + 3.2 m take the nearest profile height, full weight across the road, blending out over the
  shoulder; creek crossings skipped. Road points re-grounded afterwards. `RoadWidth` = 4.2 m. `Relax` smooths the
  route (Laplacian, ends fixed). Sight lines: `BuildTrees`/`BuildBushes` reject points within 7 m / 3.5 m of the road
  edge. `BuildRoadMarkers` places posts.
* **Ride smoothing** (`TrailBike.SnapToGround`): SmoothDamp on height (0.09 s) and pitch (0.16 s) while ridden, clamped
  so wheels never sink more than 3 cm.
* **Expansion framework**: `PlaceholderBody.Generic` + `BodyPlan` (`PlaceholderAnimalBuilder.BuildGeneric`),
  `DailyRhythm` (used by `WildlifeSchedule` when enabled), `HotspotType.Water`. See `WildlifeExpansion.md`.
* **Compile check**: `Tools~/CompileCheck/compile-check.sh` (see its README).

## Time of day (0.9.0)
* **`DayCycle`** (`Scripts/Environment/DayCycle.cs`, built by `WildLensSliceBuilder.BuildDayCycle`): `Hour` advances at
  `minutesPerHour` real minutes per hour (×`waitSpeed` while T is held and the player is still; ×`nightSpeed` at
  night). Periods: Dawn 5–7, Morning 7–11, Midday 11–15:30, Golden hour 15:30–18:30, Dusk 18:30–20:30, Night.
  `SunPosition` (azimuth 95°→265°, elevation peaking at 49°); a key table sets sun intensity/colour, ambient, sky
  exposure/tint, fog density/tint, white-balance and exposure offsets. Sky images: morning before 15:30, golden after,
  rotated (`_Rotation = skyAz − sunAz`) so the painted sun matches the light. Runtime copies of sky materials and the
  volume profile, so Play never edits assets. `DynamicGI.UpdateEnvironment` every 2.5 s. Settings: *Time of day* in the
  builder settings (on/off, start hour, minutes per hour).
* **`MistPatch`**: particle mist in a flat box; opacity from `MistPatch.Intensity(hour)` (dawn peak, gone by 09:00,
  light dusk haze). Material made by the builder (`M_DawnMist`) for the active pipeline.
* **`WildlifeSchedule`** (`Scripts/Wildlife/`): on each period change, moves every animal's home (`AnimalAgent.SetHome`)
  to the nearest place of the right kind within 320 m of its `OriginHome` (450 m for the ridge): meadow / meadow
  edge / brush / perch hotspots, or `ScheduleAnchor`s (Water: pond far shore and creek banks; Woodland: dense cover;
  Ridge: over the overlook view). Sets `ActivityBias` (`SpeciesDefinition.PickBehavior` multiplies weights). Rules
  are a small table in code (`Where`, `Bias`); no new AI.
* **Trail hierarchy** (`Editor/RoadBuilder.cs`): `ReserveTrack.kind` Road/Footpath. The road ring routes trailhead →
  each destination pull-off (in bearing order) → trailhead with an A* that strongly prefers gentle grades and stays
  outside the loop. `PathDistance` measures to each path's *edge* (normalised to the loop's width), so clearance and
  painting work for every width; roads paint as gravel (`RoadDistance` field). `TrailBike.Surface` sets the speed
  tier.
* **Trees** (`VegetationBuilder.WhorlConifer`, `BranchingBroadleaf`; `FoliageCards.ConiferCrown`;
  `ProceduralMeshes.Frustum`): see the 0.9 changelog. Placeholder trees without foliage cards keep the old builders.

## Ecosystem pass (0.8.0)
* **Willow Creek** (`Editor/EcosystemBuilder.cs` → `PlanCreek`): source on the ridge's flank, mouth in the pond
  (falls back to highest/lowest ground if either is missing). A* on 5 m cells that penalises climbing (water runs
  downhill), steep ground, structures and the blind's meadow; smoothed, given S-bends (`Meander`), resampled at
  2.5 m. Bed height per sample = lower of the banks 7 m either side − 1.1 m, never rising downstream, meeting the
  pond's water level. Carving lowers terrain toward a bed-plus-bank profile within 4.6 m (min(), so rebuilding is
  idempotent). Water is a ribbon mesh 0.32 m above the bed. `CreekDistance` (cached 4 m grid) feeds forest bias
  (riparian woodland), tree exclusion, splat (stony bed, cut banks, moist margin) and details (no grass in water,
  ferns along the banks). Runtime marker: `ReserveCreek`. Requires *Shape terrain at destinations*.
* **Crossings**: wherever the loop or a track intersects the creek, a 9.5 m footbridge; its deck collider carries
  `RideableSurface`, which `TrailBike.GroundHeight` rides on. Tracks are routed after the creek with a crossing
  penalty, so they cross once and squarely.
* **Density zones** (`VegetationBuilder.TreeChance/TreeSpacing`): dense woodland > 0.62 forest (spacing 2.2 m),
  woodland 0.45–0.62 (clumped), quiet groves 0.3–0.45, open grassland with lone trees < 0.3. Same budget, uneven
  distribution.
* **Pull-offs** (`PlanParking`): `ReserveParking` pads (levelled, painted like path, kept clear), 22–45 m short of
  each destination; scenic pull-off with bench midway on tracks > 160 m. HUD prompt while riding.
* **Residences** (`WildlifeResidence`): species groups at places that aren't destinations; spawned by
  `WildlifeSpawner.SpawnResidents` alongside destination residents.
* **Storytelling**: `InterpretivePanel`, `BuildTrackMarkers`, deer-crossing sign, nest boxes, ranger's log.
* **Ambience** (`AmbientSound`): procedural ridge wind and brook loops, 3D.
* **Blind**: see the 0.8 notes in `ReserveBuilder.BuildBlind`; `PlayerPresence.IsConcealed` drives the HUD's
  HIDDEN line and first-entry notice.

## Reserve (0.7.0)
* **Planning** (`Editor/ReserveBuilder.cs`, partial of `VegetationBuilder`): chooses four destination sites from the
  terrain, shapes the pond basin and overlook hill if needed, routes reserve tracks (A*), biases forest density
  (dense grove; open meadow, pond and ridge top), adds meadows, and widens the scatter area. Runs before scenery.
  Then `BuildDestinations` adds the structures, hotspots, signs, fingerposts and viewpoints. Settings: *Reserve
  (0.7)* in the builder settings. The design plan is in `ReserveExpansion.md`.
* **Runtime:** `ReserveDestination` (number, title, field note, resident species, wildlife centre; raises
  `Arrived`, which the HUD shows as a notice), `ReserveTrack` (open polyline; `DistanceToAnyPath`),
  `ObservationBlind` (concealment volume → `PlayerPresence.IsConcealed`: profile ×0.3, footfall ×0.5).
  `WildlifeSpawner.SpawnResidents` settles one group of each resident species at its destination.
* **Trailhead board** (`Scripts/Environment/ReserveInfoBoard.cs`): a world-space uGUI board built at Start from live
  data (`WildLensVersion` features and recent changes, `SliceInfo`, species database, journal, trails, tracks,
  viewpoints, destinations). The map is generated from terrain heights (hillshade and 5 m contours), tree density,
  water, trails and tracks, and oriented to the reader. **Each release: update `WildLensVersion.Features`,
  `RecentChanges`, `ReleaseName` and `ReleaseDate`.**
* **Trail bike** (`Scripts/Vehicles/`): `TrailBike` (kinematic: terrain-following on two wheel samples, sphere-cast
  stops, grade-aware acceleration; 11 m/s on paths, 6.5 m/s off them; climbs ≤30°; mount/park with R; rider look
  clamped ±120° with gentle recentring at speed; camera blocked while moving via `CameraModeController.RaiseBlocked`),
  `TrailBikeModel` (primitive placeholder; a purchased model can live under a child named `Model` with the same pivot
  names), `TrailBikeEngine` (procedural engine loop). While riding, `PlayerPresence.EngineNoise` (1.3 idle → 3 at
  speed) is louder than sprinting: wildlife reacts to the bike long before it reacts to footsteps.

## Presentation pass (0.6.0)
* **Leaf-card trees** (`Editor/FoliageCards.cs`, `Editor/VegetationBuilder.cs`): every canopy blob and conifer tier
  becomes a darker core volume wrapped in alpha-cut cards. Broadleaf: ~40 leaf-cluster cards per lobe; conifer:
  13 drooping branch cards per tier lying just over the cone. Six shared mesh variants (in
  `Generated/FoliageCardMeshes.asset`) with normals bent outward from the crown so the foliage shades as a volume.
  Cards are children of the swaying parts, so wind moves them; they are excluded from static batching.
  Textures: `Art~/Foliage` (composed from CC0 Poly Haven leaf and twig atlases).
* **Terrain colour** (`Editor/TerrainPainter.cs`): no CC0 scan is lush grass, so the leafy-grass scan is used twice,
  tinted green (open ground) and golden-olive (meadows), at unrelated tile sizes, mixed by ~20 m noise patches to
  hide tiling. Terrain grass draws to 150 m.
* **Stale-scene detection** (`Scripts/Core/WildLensVersion.cs`, `Scripts/Environment/SliceInfo.cs`): the builder
  stamps the slice root with the package and environment version. Bump `WildLensVersion.Environment` whenever the
  builder's output changes. Old scenes get a Play-mode notice, a builder-window warning and an F3 line.
* **Deer placeholder**: chest, haunch, forward-carried neck, short broad ears, dark shins and hooves; lying down
  tucks the legs under the body instead of swinging them out.
* **Photography feel**: hints vanish in camera mode, focus lock snaps the marks and dot, prints have a drop
  shadow and settle with a slight tilt.
* **Animal Model Setup** (`Editor/AnimalModelSetup.cs`, menu WildLens ▸ Animal Model Setup): turns a bought animated
  model into a species prefab: classifies clips by name, builds an Animator Controller (Speed blend tree; any-state
  transitions on Activity), scales the model to real size, root motion off, assigns `species.prefab`.

## Data migrations

* Species assets: `dataVersion` 3. `SpeciesAssetMigrator` applies current gameplay tuning to the built-in species
  after script reload (**WildLens ▸ Upgrade Species Assets** to run manually).
* Journal save: version 3 (adds `identifiedBy`, `observedBehaviors`); version-1 scores were converted in 0.2.0.
