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

## After the Rain (1.9.0)

Four workstreams: natural wildlife events, small life without packs, the screen, and the reserve's geography. **None has
run in Unity.** It supersedes the 1.8.0 and earlier notes where they differ (corrections are listed at the end).

### Natural wildlife events

* **The idea.** An event is a **condition**, never a script: the hour, the weather now, the weather lately, the four-day
  forecast. Nothing keys off the player, a counter or a marker, and the same day always repeats. The data is
  `SpeciesDefinition.weatherHabits` (`WeatherHabits`); the rules are `Scripts/Wildlife/WeatherHabitRules.cs`
  (pure functions, no state, no randomness, no Unity objects); `WildlifeSchedule` applies the result.
* **Data model** (`WeatherHabits`, off by default, so old assets behave exactly as before):

  | Field | Meaning |
  |---|---|
  | `rainShelter`, `rainPlace` | where it sits **while it rains**; off: the general rule (open ground goes to the woods, the ridge to a perch) |
  | `afterRain` (list of `AfterRainStage`: place, `untilHours`, `needsSun`) | where it goes after rain stops, first stage whose window still holds; a `needsSun` stage is skipped in grey weather |
  | `activeAfterRain` | a livelier window after rain (see Bias); lasts `AfterRainWindow()`: the longest stage, at least 2 game hours |
  | `fairOnlyPeriods` (bit mask, `PeriodMask(...)`), `foulPlace` | in those day periods the rhythm's place is used **only in fair weather**; otherwise `foulPlace` |
  | `overcastEarly` | on a grey day it keeps the next period's hours (late midday keeps golden hour's place from 14:00; golden hour keeps dusk's) |
  | `roostLabel` | for `RhythmPlace.Roost`: the residence it gathers at |

  `RhythmPlace` gained `Roost` (appended). Day periods are indices: 0 night, 1 dawn, 2 morning, 3 midday, 4 golden hour, 5 dusk.
* **The decision** (`WeatherHabitRules.Decide`), in order: the species' rhythm (or the built-in one for the five originals,
  `LegacyPlace`) for the period, after `overcastEarly`; then fair-only; then the after-rain stages (never at night, never
  while raining); then rain (`rainShelter`, else the general rule). The result is a place, the period being kept, and flags
  (raining, active after rain, sun). `Key` packs them so an unchanged decision changes nothing.
* **Bias** (`WeatherHabitRules.Bias`) multiplies behaviour weights: the old time-of-day rules (resting 4× at midday and
  night, feeding 1.6× at dawn, golden hour and dusk...), rain (resting and sitting 2×, feeding 0.6×) and, for
  `activeAfterRain`, resting 0.4×, sitting 0.7×, feeding 1.5× (2× in the sun), flying 1.2× (1.5× in the sun).
* **The table** (what each species does; place names are `RhythmPlace`):

  | Species | In rain | After rain | Fair only | Other |
  |---|---|---|---|---|
  | White-tailed deer, wild turkey | woodland | meadow, up to 3 h, **needs sun** (so in practice about 2 h); livelier | | |
  | Cardinal, blackbird, goldfinch, song sparrow | brush | water's edge, 2.5 h; livelier | | |
  | American robin | brush | meadow 1 h (worms on the open grass), then water 2.5 h; livelier | | |
  | Louisiana waterthrush | home (the run) | water, 2.5 h; livelier | | |
  | Eastern bluebird | woodland | meadow, 2.5 h, needs sun; livelier | | |
  | Great blue heron | general rule | water, 2.5 h; livelier | | |
  | Eastern cottontail | general rule | meadow edge, up to 8 h (a wet night) | | |
  | Red-tailed hawk | perch | | morning, midday, golden hour (else perch) | |
  | Black bear | home (deep cover) | | dusk (else home) | |
  | Barred owl | woodland | | | rhythm: dusk on a snag (Perch), night at the meadow edge |
  | American crow | general rule | | | rhythm: roosts at the trailhead residence at dusk and night |
  | Raccoon, opossum | general rule | | | `overcastEarly` |
  | Red fox, squirrel, groundhog, kingfisher, phoebe... | general rule | | | no habits (the fox and turkey gained colours only) |
  | Tufted titmouse | general rule | | | none |

  The after-rain "sun" is `WeatherCycle.SunAfterRain`: rain ended within **2 game hours**, the sky is not overcast, cover
  below 0.6. Each habit has one hint sentence at the end of the species' field notes.
* **Stagger** (`WildlifeSchedule`, `weatherStagger` 14 s). The first placement and a change of day period are immediate (as
  before). A change of weather alone is answered after a stable delay: `weatherStagger × group + 3 s × animal`, where each
  share comes from `WeatherHabitRules.Hash01` (FNV-style hash of the species id and position or index: no `System.Random`,
  identical on every run). The group is a 12 m cell of the animal's origin, so a flock shares a delay; each animal adds a
  few seconds. A pending move keeps the earlier due time if the wanted state changes while it waits (never endlessly
  re-armed). The delay counts down in game time (faster while T is held or at night). Polling is four times a second.
* **Jitter and home.** Each animal's stable jitter (±3 m) comes from the same hash with its list index as salt (so a group
  with one territory does not stack on one point). A move to the same place and spot is skipped, so circling birds are not
  yanked into transit. `RhythmPlace.Roost` looks for one of the species' own `WildlifeResidence`s (by `roostLabel`, else the
  first) and ignores `maxShift`.
* **`FlyingAnimal`**: a bird that is perched or landing when its home changes now remembers it (`_homeDirty`) and heads for
  the new home at its next flight. Before, it hopped between perches near the old place for minutes.
* **`WeatherCycle` helpers** (the one memory of the rain, read by the animals and by small life): `IsRaining` (Rain > 0.25),
  `HoursSinceRain` (0 while raining, huge if never), `RecentlyRained(h)`, `RainEndedWithin(h)`, `SunAfterRain`,
  `IsFair` (not raining, not overcast, more than an hour since rain), `IsFairEvening` (golden hour or dusk, and fair),
  `LastRainTime`, `HasRainMemory`, `AnimalSunAfterRainHours` (2).
* **Seeded rain memory.** At the start (and whenever the clock is set back) the memory is seeded from the cycle's own
  forecast (`SeedRainMemory`): the hour each forecast day's rain stops is found by scanning the day's keys in hundredths of an
  hour (day 3: about 16:06; day 4: 23.99, the dusk rain runs on to midnight), so the morning after is damp even when nobody
  watched the rain fall. The always-fair pattern and a hand-set weather have no memory.
* **Forecast** (hour, cloud cover, rain): day 1 fair; day 2 grey morning clearing by 15:00; day 3 fair, then cloud and a
  shower from about 13:30 to 16:06; day 4 overcast, rain building from about 17:30, heavy at dusk, running to midnight. A good
  watch: day 3 from 13:00 to dusk.
* **Migration** (data version 4, `SpeciesAssetMigrator`, `SpeciesDefaults.ApplyHabits`). An asset already at version 3 gets
  only the natural-events data: the habits, the field-note hint (an unedited note takes the new default; a hand-edited note
  keeps its text and gains the hint sentence if missing), the owl's and crow's new rhythm **only if it still equals the old
  default**, and the fox's and turkey's three colours. Older assets get `ApplyTuning`, which now includes the habits. Never
  touched: awareness, behaviours, groups, prefab, hand-tuned values.
* **The three new birds** (`SpeciesDefaults`; residents only, `groupCount` 0, placed by residences): eastern bluebird (18 cm;
  aware 18 m, alert 10 m, flee 5 m; perches long on a post), tufted titmouse (16 cm; aware 11 m, alert 6 m, flee 3 m, patience 7 s,
  nearly as accepting as the chickadee), Louisiana waterthrush (15 cm; aware 20 m, alert 12 m, flee 6.5 m: wary). Body `length`
  is about 0.45 of the real total, as for the others. Each is in `WildlifeSpawner`'s own-stream set, `WildlifePackSetup`'s name
  table (bluebird: stand-ins sparrow, finch, songbird, bird; titmouse: chickadee, sparrow...; waterthrush: thrush, sparrow,
  wren...) and `AnimalPrefabFactory.RealSize`. "Bluebird" is no longer a stand-in word for the blue jay.
* **Colours** (baked into the model's materials when it is built; `FactoryVersion` 3). Fox: primary (0.93, 0.42, 0.12), white
  (0.97, 0.95, 0.92), near-black socks (0.10, 0.08, 0.07); the model's own colour multiplies it, so it needs headroom. Turkey:
  bronze-brown (0.25, 0.18, 0.11), lighter bronze (0.36, 0.26, 0.15), bare red (0.66, 0.20, 0.16).
* **The upgrade rule** (`WildlifePackSetup.UpgradeBuilds`, `IsStrictUpgrade`). In `AutoSetUp`, for each species whose prefab
  was **built by the automatic route** (`record.manual` false, a source model that still exists and still matches), the best
  candidate replaces it only if strictly better, compared on (kind, not "owned" by another species, keyword rank): the
  real animal over any stand-in; among stand-ins one that is nobody else's real animal; the closer keyword ("shiba" before
  "dog"). Equal candidates never swap, two real animals never swap, and a current model that no longer matches by name is
  left alone. The new build uses the default size, yaw 0 and the usual tint; the record then names the new model so the
  next run changes nothing. A **kept placeholder** or a **manual** build is never touched. One Console line:
  "Wildlife models upgraded (a better match was imported)".

### Small life without packs

* **Where models come from, in order**: a model from the user's packs (`prefab`), else the **generated** model named by
  `generatedModel` (when the builder setting `generatedSmallLife` is on), else nothing. `FernScan` is the bundled CC0 fern
  scan, not generated.
* **Generated models** (`Editor/SmallLifeShapes.cs` recipes, plain C# with no `UnityEngine` so they can be run and looked at
  outside Unity; `SmallLifeGeometry.cs` the geometry and palette-strip textures; `SmallLifeModels.cs` the Unity side: meshes,
  materials and a prefab per model under `Assets/WildLens/Generated/SmallLife/Models/{Key}_v{ModelVersion}/`, made once and
  reused). **`SmallLifeModels.ModelVersion` is 2**: wings, petals, leaves and blades are two-sided in the mesh itself (a
  reversed copy with negated normals) and keep back-face culling on; version 1 relied on a cull property the Built-in
  Standard shader lacks. Bump it when a recipe changes (the old folder is then replaced). Each model is authored in metres,
  normalised to its stated size, pivot at the feet or attachment point; wings are children named `Wing L`/`Wing R`
  (butterflies, bees: also `FL`, `FR`, `HL`, `HR`), a dragonfly sits on a `Perch` stem with the animal under a `Rider` group,
  a firefly has a `Lantern` and a `Halo`. The first build shows a progress bar and logs one line (count and seconds).
  The 26 keys: Monarch, Swallowtail, CabbageWhite, LunaMoth, Whitetail, Jewelwing, BumbleBee, HoneyBee, Firefly, LadyBeetle,
  Mantis, LeopardFrog, GreenFrog, GrayTreefrog, RedEft, RedBackedSalamander, Snail, HoneyMushroom, YellowFlyAgaric,
  TurkeyTail, OxeyeDaisy, Milkweed, BlackEyedSusan, Violet, HaircapMoss, Cattail.
* **The 29 subjects** (`SubjectDefaults.CreateAll`; ids and order never change, new ones are appended). Size in metres; Fill
  is the frame fill for Good / Excellent; Hours: N night, D dawn, M morning, Md midday, G golden hour, Du dusk.

  | Subject (scientific name) | Size | Fill | Where | Hours | Condition | Second tier | Count |
  |---|---|---|---|---|---|---|---|
  | Common dandelion (*Taraxacum officinale*), bundled scan | 0.05 | 0.18 / 0.35 | meadow verge | M Md G | closes in rain | | bundled |
  | Lesser periwinkle (*Vinca minor*), bundled scan | 0.035 | 0.18 / 0.35 | woodland floor | D M Md G Du | | | bundled |
  | Monarch (*Danaus plexippus*) | 0.09 | 0.18 / 0.35 | flowers (milkweed first) | M Md G | sunshine | sun after rain, 40% | 8 |
  | Luna moth (*Actias luna*) | 0.10 | 0.18 / 0.35 | trunks | N D Du | | | 4 |
  | Common whitetail (*Plathemis lydia*) | 0.06 | 0.18 / 0.35 | water's edge, own perch | M Md G | warm | | 5 |
  | Bumble bee (*Bombus impatiens*) | 0.02 | 0.08 / 0.15 | flowers | M Md G | sunshine | sun after rain, 33% | 8 |
  | Lady beetle (*Coleomegilla maculata*) | 0.012 | 0.08 / 0.15 | flowers | M Md G | | | 6 |
  | Carolina mantis (*Stagmomantis carolina*) | 0.06 | 0.12 / 0.25 | meadow verge | M Md G Du | | | 4 |
  | Northern leopard frog (*Lithobates pipiens*) | 0.08 | 0.10 / 0.20 | water's edge | all | | after rain or dusk, 40% | 8 |
  | Eastern newt, red eft (*Notophthalmus viridescens*) | 0.07 | 0.14 / 0.28 | woodland floor | M Md G | after rain | | 4 |
  | Whitelip snail (*Neohelix albolabris*) | 0.03 | 0.12 / 0.25 | logs and stumps | N D M Du | after rain | | 4 |
  | Firefly (*Photinus pyralis*), 0.8 m up | 0.012 | 0.08 / 0.16 | meadow verge, 3 clusters + the pond | N Du | fair evening | | 16 |
  | Honey mushroom (*Armillaria mellea*) | 0.07 | 0.15 / 0.30 | stumps and logs | all | | rained today or yesterday, 40% | 14 |
  | Oxeye daisy (*Leucanthemum vulgare*) | 0.05 | 0.18 / 0.35 | meadow verge | M Md G | | | 6 |
  | Eastern tiger swallowtail (*Papilio glaucus*) | 0.10 | 0.18 / 0.35 | flowers (milkweed, black-eyed Susan) | M Md G | sunshine | sun after rain, 40% | 5 |
  | Cabbage white (*Pieris rapae*) | 0.045 | 0.14 / 0.28 | flowers (daisy, dandelion) | M Md G | sunshine | sun after rain, 40% | 6 |
  | Ebony jewelwing (*Calopteryx maculata*) | 0.055 | 0.18 / 0.35 | creek only | M Md G | warm | | 5 |
  | Western honey bee (*Apis mellifera*) | 0.013 | 0.07 / 0.13 | flowers (daisy, black-eyed Susan) | M Md G | sunshine | sun after rain, 33% | 6 |
  | Green frog (*Lithobates clamitans*) | 0.085 | 0.10 / 0.20 | water's edge | all | | after rain or dusk, 40% | 8 |
  | Gray treefrog (*Dryophytes versicolor*) | 0.05 | 0.12 / 0.24 | trunks | N D Du | wet night | | 6 |
  | Red-backed salamander (*Plethodon cinereus*) | 0.09 | 0.18 / 0.35 | woodland floor | D M Md G Du | after rain | | 4 |
  | Yellow fly agaric (*Amanita muscaria* var. *guessowii*) | 0.10 | 0.18 / 0.35 | woodland floor | all | | rained today or yesterday, 40% | 8 |
  | Turkey tail (*Trametes versicolor*) | 0.08 | 0.18 / 0.35 | logs and stumps | all | | | 8 |
  | Woodland fern (*Dryopteris* sp.), bundled scan, graded on a 0.12 m patch | 0.5 | 0.20 / 0.35 | woodland floor | D M Md G Du | | | 6 |
  | Common milkweed (*Asclepias syriaca*) | 0.09 | 0.18 / 0.35 | meadow verge | M Md G | | | 12 |
  | Black-eyed Susan (*Rudbeckia hirta*) | 0.07 | 0.18 / 0.35 | meadow verge | M Md G | | | 12 |
  | Common blue violet (*Viola sororia*) | 0.025 | 0.07 / 0.12 | woodland floor | M Md G | | | 6 |
  | Haircap moss (*Polytrichum commune*) | 0.08 | 0.18 / 0.35 | boulders (and old logs) | D M Md G Du | | | 8 |
  | Common cattail (*Typha latifolia*), graded on its 0.10 m head | 0.14 | 0.20 / 0.32 | ponds only | D M Md G Du | | | 5 |

  The first two rows are the 1.5 scans. The default counts of the generated subjects add up to
  192 instances (before the bundled plants). "Second tier" is a share of `count` placed as a **second set of instances** that is out only in
  those conditions; the rest are unaffected. A pack model, if one is found, replaces the generated one for that subject
  (matching in `MacroPackSetup` is stricter for the new subjects: a generic "butterfly" stays with the monarch, "damselfly"
  with the jewelwing, "Tree_Frog" with the treefrog and nobody else; a moss model must be 3-D and more than 2 cm tall).
* **Conditions and tiers** (`Scripts/Photography/NaturalConditions.cs`; reads only the clock and the weather; cached, four
  times a second; no allocation):

  | Rule | True when |
  |---|---|
  | Sunshine (`Sunny`) | not raining, cloud cover below 0.55, 06:30 to 18:30 |
  | Warm | not raining, cover below 0.8, 09:30 to 18:30 |
  | Fair evening | not raining, cover below 0.5, **more than 8 game hours since rain**, from 18:45 plus a stagger of 0 to 0.34 h, until 22:00 plus up to 0.6 h (each firefly its own) |
  | Wet night | 18:30 to 06:30, within **14 game hours** of rain |
  | Sun after rain (small life) | sunshine, within **3 game hours** of rain |
  | After rain (tier) | within 6 game hours of rain; after rain or dusk: that, or 18:30 to 07:00 |
  | Rained lately (tier) | it rained today or yesterday (the rest of that day and the next) |

  `SubjectCondition` (None, Sunshine, Warm, FairEvening, WetNight) and `SubjectExtra` (None, SunAfterRain, AfterRain,
  AfterRainOrDusk, RainedLately) are enums (append only). `MacroSubject.tier` and `stagger` carry them to an instance; a
  hidden instance has its renderers off and does nothing (so the second tier costs nothing out of season); a plant only keeps
  the shared rain memory up to date. Animals' "sun after rain" is 2 game hours, small life's is 3: different on purpose.
* **Motion** (`MacroMotion`, cosmetic; nothing chases you or leaves its patch; a subject farther than 25 m from the
  camera does only a once-a-second distance check). Wings: children `Wing L`... roll about their own Z axis; a
  butterfly rests with them closed (68°) and now and then slowly opens them flat (to 2°) for a few seconds, beats them in flight;
  a jewelwing opens and shuts black wings the same way; a bee's wings buzz and tremble on the flower; a dragonfly's shimmer;
  a firefly's whirr. A dragonfly moves only its `Rider`. **Fireflies rise** from the grass over 8 to 20 s when they come out
  (they start on the ground below their hover point, 0.8 m up), drift, and flash the lantern and halo (a pack model glows all
  over). Frogs: still until the camera is within 0.9 m **along the ground**, then one hop (0.4 m, 0.1 m high, 0.35 s) on a 1.2 m
  leash, as in 1.7.
* **Habitats** (`SubjectHabitat`, appended: `Trunk`, `Rock`). Trunk: the lower trunk of a tree with a collider within 30 m of a
  path (0.4 to 1.6 m up, the side facing the nearest path, found by a ray; at most two of a species per tree); falls back to dead
  wood. Rock: boulders and rocks 0.4 to 4 m across (moss); falls back to the woodland floor. Water edge also covers any water
  hotspot not on the creek (a ring on the bank, never in the bed); jewelwings use the creek only, cattails ponds only. **One
  insect per flower** (flowers are tracked with their kind and an offset to the head: a milkweed's umbel 3.2 cm up, a daisy's
  disc 0.5 cm): monarchs choose milkweed first, swallowtails milkweed then black-eyed Susan, bumble bees black-eyed Susan,
  daisy then milkweed, honey bees daisy then black-eyed Susan, cabbage whites daisy then dandelion, lady beetles black-eyed
  Susan then daisy. Flowers are placed first so insects have somewhere to sit. Fireflies gather in **clusters**: three on the
  meadow verge (four with no pond) and one at the pond shore.
* **Grading arithmetic** (corrected in 1.9). The camera is **1.65 m up standing and 0.95 m crouched**, so a subject on the
  ground is never nearer than about a metre. At the macro lens's 1.7× step the frame is 0.25 d tall at distance d, and the
  focus box is 1.4 × the subject's size, so seen head-on **fill = 5.6 × size / d**; Excellent needs d ≤ 5.6 × size / fill.y, and
  macro works to 2.5 m. The thresholds above were set so that Excellent is reachable crouched at about a metre (fill.y at most
  5.6 × size for ground subjects; a little less for the hoppers, which are not approached closer than 0.9 m along the ground,
  so 1.3 m crouched or 1.8 m standing). A tall plant would be Excellent from anywhere (a 0.5 m fern's 0.7 m box fills the
  frame), so a subject can name the part that is graded (`focusPartSize`): the fern's 12 cm crown patch (fill = 0.48 / d: Good
  within 2.4 m, Excellent within 1.4 m) and the cattail's 10 cm head (0.4 / d: Good within 2.0 m, Excellent within 1.25 m).
* **Upgrade of old subject assets** (`UpgradeToV2`, subject data version 2): the new fields (generated model, condition,
  second tier) come from the defaults; a count still at the version-1 value moves to the new default; a fill still at one of
  the two version-1 values moves to the new default; the generated dragonfly's hover, the luna moth's habitat and the lady
  beetle's habitat move only when never changed and no pack model is in use. Anything tuned is left alone.
* **Console**: `[WildLens] Small life: N macro subjects placed (G generated, P from your packs; E of them in second tiers…)`
  and one line per subject (`Monarch Butterfly: 8 (3 in the second tier), generated`).

### The screen

* **Empty by default.** With the camera lowered: a faint centre dot. Nothing else is always on. While riding, speed; while
  waiting (T) or crouched or hidden, small quiet lines at the bottom-left. The old hint block and the top-right status line
  (with the species counter) are gone. The whole canvas is hidden for the capture frame, as before.
* **`UiStyle`** (`Scripts/UI/UiStyle.cs`): one look. Colours: text warm off-white (0.97, 0.95, 0.89) at 100%, 72% (soft) and
  45% (faint); grades quiet (Good 0.67/0.89/0.60, Excellent 0.97/0.81/0.45; Poor stays white); alert amber; pill fill a faint
  dark (0.05, 0.07, 0.05) at 40% (62% for an alert). Sizes in reference pixels: margin 56, small 18, body 22, title 26, pill height
  48. Fade 0.25 s (unscaled). Canvas reference 1920 × 1080, match 0.5 (nothing important beyond the 56 px margin from 4:3 to
  21:9). `SoftShadow`, `CreatePill`, `PillSprite` (a 9-sliced rounded rectangle made once).
* **Prompts** (`PromptView`; at most two, bottom-centre, a bold key and one to three words on a pill; lifted clear of the
  viewfinder's data row while the camera is up; hidden when a menu is open or the keeper print is up). In priority order:

  | Prompt | Shown when | Text |
  |---|---|---|
  | Park | riding, and stopped or inside a pull-off | R Park here (+ the pull-off's name) |
  | Mount | on foot, the bike in reach, camera lowered | R Ride the bike |
  | Macro | camera up, not in macro, a small subject within 3 m | M Macro lens |
  | Shutter | camera up, a subject in frame (or in macro, one near) | LMB Shutter |
  | Zoom | camera up, not in macro | Scroll Zoom |
  | Raise | on foot, camera lowered, an animal (or small life) ahead, after 14 s | Hold RMB Raise camera |
  | Journal | once, 6 to 600 s after the first photograph | J Journal |
  | Intro | once, between 2.5 s and 9.5 s, camera lowered, on foot | H Controls |

  A prompt gives up after 8 s (7 s for Macro and Raise) if ignored, and is **retired for the session** after three uses (one for
  Journal and Intro), or after six ignored showings. Opening the controls card retires Intro. Nothing is saved.
* **Notices** (`NoticeView`): a small title and one line on a pill under the top edge, at most **two**; a third replaces the
  older one (a priority notice, such as the stale-scene warning, only yields to another priority notice). Text shrinks to
  fit (22/18, 20/16, 18/14 px) and is never wrapped. A **hidden place's name** is the one large thing: 58 px italic, centre-top
  beneath the notice stack, in 1.0 s, held 4.2 s, out in 1.6 s, with a one-line note. Never counted.
* **Viewfinder** (`ViewfinderView`): one thin line weight and one warm white; the grade is a word beside the name in a quiet
  colour; a **data row** along the bottom like an EVF: [focus dot and MACRO] ... [1× 2× 4× 8× 16×] ... [focal length and
  f-number], whose side items start 136 px in from each edge, clear of the corner arms (checked at 16:9, 16:10, 21:9 and
  4:3). The keeper print rests above the row and scales down on short canvases.
* **Controls card** (`ControlsCardView`, **H**; Esc closes it): "Field Guide: Controls", one paper card (1480 × 800) in four
  groups (On foot, Camera, Trail bike, Journal). The key names come from `GameInput.Labels`, so a rebinding shows up. It pauses
  the world like the journal.
* **Journal** (`JournalUI`): two pages, **A / D** (or the arrow keys) to switch, W / S or the wheel to select, J / Tab / Esc to
  close. **Wildlife** is as before (including unseen species as "???" and "Species X of N", an existing behaviour that sits
  uneasily with "no checklists"). **Field notebook** lists only the subjects found, grouped by kind then name, with kind, photographs,
  first found, best photograph and where; an empty notebook says one gentle line. Never a total, a "?" row or a completion
  figure. Long names shrink to fit a row (down to 16 px). Subject text comes from the scene's subjects, else the shipped defaults.
* **Keys**: `GameInput` gained `ControlsTogglePressed` (H, the old hints key), `NavigateLeftPressed` (A, left arrow),
  `NavigateRightPressed` (D, right arrow) and `GameInput.Labels` (on-screen key names).

### The reserve's geography

* **A late step.** `VegetationBuilder.BuildGeography` (`Editor/GeographyBuilder.cs`, `Editor/HiddenPlacesBuilder.cs`) runs
  **after every pass that draws from the shared random sequence** (trees, rocks, logs, bushes, hotspots, viewpoints,
  destinations, ecosystem, discoveries, undergrowth) and **before** small life, terrain paint and grass. Why: for a given seed the
  reserve that already exists must not move. Every earlier pass decides from the terrain; carving first would change those
  decisions and, through the shared sequence, every tree after the first one affected. So the step has a **private random stream**
  (seed × 7919 + 1901), carves, then **settles** what stood on ground that moved (posts, rocks and anchors follow the ground;
  vegetation in the new channel, footpath, pond water or crag is lifted out), then builds. The water-distance field is rebuilt
  with the new run so paint, grass and small life see it. The step needs "Shape terrain at destinations"; a failure is a
  Console error and a rebuild retries.
* **Hemlock Ravine.** A steep, shaded side valley cut by **Hemlock Run**, a small tributary of Willow Creek, with a stepped
  cascade at its head. Selection, in maintainer terms: a reverse Dijkstra from Willow Creek, costed the way water runs, gives
  every cell its way down; heads on ground well above the creek and away from paths are routed and scored; the run below the
  cascade keeps a gentle, even gradient from the mouth (it cuts deeper the further upstream it goes); the cascade is the drop
  from the upper channel to that, in two or three steps over ledges into a pool. The best of up to ten routes is kept if it is
  dramatic enough (cascade plus depth); otherwise there is **no ravine and one Console line says why**. The carve
  only cuts (so a rebuild gives the same ravine) and never touches a path, road, destination, keep-out or hidden place. It
  exists on seeds where the land falls enough toward the creek, **including the default seed 7**. Scenery: hemlocks on the
  walls and rim (through kit roles), ferns, mossy stones in the run, one fallen trunk across it. One **footpath** (`Track:
  Hemlock Ravine`, a footpath like the others) with one sign (HEMLOCK RAVINE, "to the cascade") and a **log seat** at its
  end that looks up at the cascade. Residents: a Louisiana waterthrush by the run, an eastern phoebe on the cascade's ledge.
* **The four hidden places** (like the Hidden Overlook, Fern Glade and Creek Alcove: unmarked, on no sign or map, found and
  named once, never counted; **seven in all**). Each is chosen from the land and skipped cleanly (a Console note) where there
  is no site:
  * **Cascade Overlook**: a ledge on the ravine's rim above the cascade, 8 to 24 m from its middle, on gentle ground, with a
    clear line of sight to it; a log seat and a boulder. Needs the ravine.
  * **Hidden Pond**: a small still pond (radius about 8.5 m) in a wooded hollow, gentle ground, at least 45 m from every path and
    from the run, away from the Cattail Pond and other places; the basin is shaped and the water sits below the land all round.
    Reeds, a half-sunk log; a water hotspot and anchor; residents: great blue heron, red-winged blackbird, raccoon.
  * **Rock Shelter**: an overhanging crag (`ProceduralMeshes.Crag`: a block of rock with an undercut alcove) set into an upland
    hillside (slopes of about 7° to 24°, preferring higher ground, a view and the ground under the ridge), its alcove dry
    underfoot (at least 2.2 m of clearance), a long-cold fire ring and a view out; the floor is bare (no terrain grass, a
    dry tread). Residents: tufted titmouse and gray squirrel in the woods in front. **Rain still falls through its roof.**
  * **Old Cabin Ruins**: a stone foundation and chimney stub in a small clearing at a woods' edge on level ground, taken back by
    ferns and young trees, rotted sills, and the last posts of a split-rail fence. Residents: eastern bluebird (the fence),
    eastern cottontail and groundhog (the clearing).
* **Measured first.** All of it was prototyped and measured in Python on eight seeds before it was written: the existing
  paths and destinations changed by 0.00 m of height; all four places were found on every seed, 42 to 130 m from the nearest
  path, reachable on foot by a slope no steeper than 15° on the gentlest approach.
* **Settings** (`SliceBuildSettings`, "Reserve geography (1.9)"): `hemlockRavine`, `moreHiddenPlaces`, both on; and
  `generatedSmallLife` (on). **Environment version 21.**
* **Console**: `[WildLens] Reserve: Hemlock Ravine N m (cascade H m in k drops, up to D m deep, footpath L m), hidden places X of 7;
  settled N objects onto the new ground, removed M. Notes: ...` (or `no ravine`, with the reasons in the notes).
* **World size and relief** (measured): the world is 900 × 900 m; relief about 26 to 30 m; mean slope about 4°. More relief,
  not more distance, is the next lever.

### Corrections to earlier sections

* **Plants and fungi never vanish** (1.7 note): since 1.9 that covers ferns and moss too (`NaturalistSubject.IsPlantOrFungus`).
* **"Recently rained"** is no longer tracked by `MacroSubject`: it reads `NaturalConditions`, which reads `WeatherCycle`'s one
  memory (still 3 game hours for small life).
* **Species**: twenty-seven, not twenty-four; subjects: 29, not fourteen; hidden places: seven, not three.
* **The HUD** no longer has a hint block, a status line or a species counter; H opens the controls card (it used to toggle hints).
* **Animals' rain rule** (1.4: open ground empties, hawks perch) is now the **general rule** inside `WeatherHabitRules`; species with
  habits replace it with their own.
* **Journal**: two pages. Its Wildlife header no longer shows the notebook count.

## New neighbours, real sizes and pack models (1.8.0)

From the user's 1.7 Asset Audit. Nothing here has run in Unity beyond what that audit showed (the automatic setup ran;
deer, turkey, coyote and hawk models were in use). It supersedes the 1.7.0 and 1.6.0 notes below where they differ.

* **Real sizes** (`AnimalPrefabFactory.RealSize`, read by `DefaultSize`). The size a model is scaled to is a real
  measurement, not worked out from the placeholder's body plan (that gave every songbird 0.67 m). Birds: beak to tail,
  measured along the model's +Z. Mammals: the larger of nose-to-tail and height. The heron is measured by its standing
  **height** (`MeasuredByHeight`). The unlisted five originals and the hawk keep their older constants (deer 1.9 m, fox 1.0,
  rabbit 0.42, squirrel 0.45, hawk 0.55); anything else falls back to the body plan.

  | Species | Size (m) | Species | Size (m) |
  |---|---|---|---|
  | Northern cardinal | 0.22 | Barred owl | 0.50 |
  | Blue jay | 0.28 | Wild turkey | 1.0 |
  | Red-winged blackbird | 0.21 | Great blue heron (height) | 1.2 |
  | Belted kingfisher | 0.32 | Spotted sandpiper | 0.19 |
  | Eastern phoebe | 0.17 | American black bear | 1.6 |
  | American crow | 0.45 | Coyote | 1.25 |
  | American robin | 0.25 | Raccoon | 0.80 |
  | Black-capped chickadee | 0.13 | Virginia opossum | 0.75 |
  | American goldfinch | 0.12 | Groundhog | 0.60 |
  | Song sparrow | 0.15 | | |

  The placeholder bodies of the six new species were also too big (their `length` was the whole bird, but the songbird
  builder wants the body, about 0.45 of it): fixed in `SpeciesDefaults`.
* **Looping clips** (`LoopedCopy`). Pack clips usually have Loop Time off. Every clip that is not already looping gets a
  looped copy stored inside the animal's Animator Controller (named "clip (looped)"); landing and take-off stay as they
  are; the pack's clips and import settings are untouched. One copy per clip, however many states use it.
* **URP material conversion** (`ConvertMaterialsForPipeline`; only when URP is the active pipeline, else nothing changes).
  Any material whose shader fails `BuiltinShaderIssue` (Standard, Legacy Shaders/*, Mobile/*, Nature/*, Autodesk
  Interactive, or a missing or error shader; built-in Unlit is flagged as "flat") is replaced by a **copy** on
  Universal Render Pipeline/Lit (Simple Lit if Lit is missing), in `Assets/WildLens/Animals`, named
  `{speciesId}_{nn}_{material}_URP.mat`, one per source material. Carried over: the main texture (first of `_MainTex`,
  `_BaseMap`, `_BaseColorMap`, `_DiffuseMap`, `_Diffuse`, `_AlbedoTex`, `_Albedo`) with its tiling and offset, the colour
  (`_Color`, `_BaseColor`, `_MainColor`, `_TintColor`, `_DiffuseColor`), the normal map (`_BumpMap`, `_NormalMap`,
  `_NormalTex`, `_Normal`), emission, metallic (0 when the source has none), smoothness (`_Glossiness` or `_Smoothness`,
  else 0.1), alpha clip (shader name "Cutout", `_Mode` 1, or the keyword) and transparency (name "Transparent"/"Fade",
  `_Mode` 2 to 3), and two-sided drawing (cutout, "Double", "TwoSided" or "2Sided" shaders). The pack's own materials
  are never edited. Copies the tint then replaces are deleted again (this species' own only). The Console line says how
  many were converted. Small life uses the same function (below).
* **Component stripping** (`StripPackComponents`, on an unpacked copy of the model, never the pack's asset). Removed:
  every MonoBehaviour that is not in a `WildLens` namespace (up to three passes, since one may require another; missing
  scripts too), then joints, colliders, rigid bodies, audio sources, and by type name NavMeshAgent, NavMeshObstacle,
  ParticleSystemRenderer, ParticleSystem, Light, AudioListener, FlareLayer and Camera. The Animator, renderers and bones
  stay. WildLens adds its own collider, awareness and body at spawn.
* **Clip choice**, in order (`ScoreName`, lower is better): (1) fewer disfavoured words (left, right, back, backward,
  backwards, reverse, strafe, turn, combat, battle, aggressive, angry, rare, alt, variant; at most 9 count);
  (2) an earlier keyword of the role in its list (walk before hop before jump; "Bear_WalkForward" beats "Bear_Jump");
  (3) fewer words, a straight-ahead word (forward, straight, fwd) counting as one fewer; a head-down Idle adds 100.
  **Keyword changes:** Groom gained ruffle; Look gained worried, sing, call, chirp, tweet and song at the END of its list
  (singing is the last choice for looking around). **Ignored** (never mapped, in addition to the 1.7 list) stun,
  stunned, buff, gethit, knockback, dodge, block, taunt, spawn, despawn. With no Idle clip the animal uses its **Look**
  clip, else the first clip that is not ignored (before: the first clip in the file, which could be pecking). Pecking
  itself stays Eat.
* **Ranking models for a species** (`RankFor`), in order: the real animal before a stand-in; a model that is another
  species' real animal after one that is not ("owned"); the keyword nearer the front of the list; more recognised clips;
  the `Prefer` words (black bear: "black", then "dark", in the model name, then the folder); a high-quality "HQ" name; a
  prefab before a raw model; then path order. **Tinting**: stand-ins are tinted; `TintExact` species (the black bear) are
  tinted even when exact. A tint colour with luminance under 0.12 is raised to (0.16, 0.15, 0.15), because a near-black
  multiplier turns a textured model into a black void. Models whose own name has controller, manager, spawner, system or
  demo (added to `NeverAnimals`) are not animals. `SpeciesNever` rules a word out for one species only (bear: polar, panda,
  teddy, koala; the folder counts too).
* **Name table additions** (`WildlifePackSetup.Names`): red fox gained "dog" as its last stand-in (the coyote still takes
  wolf first). The blackbird's stand-ins are grackle, starling, sparrow, songbird, crow, raven, thrush, bird (sparrow
  ahead of crow, since a small sparrow tinted black reads better than a shrunken crow).

  | Species | Exact | Stand-ins |
  |---|---|---|
  | American crow | crow, raven | |
  | American robin | robin | |
  | Black-capped chickadee | chickadee, tit (whole word) | |
  | American goldfinch | goldfinch, finch, canary | |
  | Song sparrow | sparrow | wren |
  | American black bear | blackbear, bear (whole word) | |
* **The import watcher** (`WildlifePackImportWatcher`). "An import is pending" is kept in `SessionState`
  (`WildLens.WildlifeImportPending`), so it survives the script reload a pack with scripts causes (before, the queued work
  was lost and the cardinal and blue jay were missed). Only `.fbx`, `.prefab`, `.blend`, `.gltf` and `.glb` files whose name
  or folder has a species keyword, a reserved word or an animal word (animal, creature, wildlife, bird, fauna) start it, so
  a tree pack does nothing. It waits while Unity is compiling or importing and while playing, resumes at editor load and
  when Play stops, and runs `AutoSetUp` once.
* **Factory version and refresh** (`AnimalPrefabFactory.FactoryVersion` = 2; each build is recorded in
  `SliceBuildSettings.animalBuilds` as an `AnimalBuildRecord`: source model, factory version, size, yaw, tint, whether a
  person chose them). `AutoSetUp` first calls `RefreshOldBuilds`, which rebuilds a species from its same model with
  today's defaults only if **all** hold: it has a prefab; it is not kept as a placeholder; its record is missing or older
  than the factory version; the record is not `manual`; and the prefab is the factory's own file
  (`Assets/WildLens/Animals/{id}.prefab`). The model is the recorded one, else traced back from the prefab's meshes; one
  that cannot be traced is left alone and recorded as current. The recorded yaw and tint are kept; the size is the
  current default. The Console line: "Wildlife models refreshed for 1.8 (sizes, looping clips, materials): ...".
  **Bump `FactoryVersion` whenever the factory's output improves.** A build made in Wildlife Pack Setup or Animal Model
  Setup is recorded as manual and never refreshed.
* **Keep as placeholder** (`SliceBuildSettings.keepPlaceholder`). The **Placeholder** button (and Animal Model Setup's
  revert) adds the species; automatic set-up then skips it. Setting a model for it by hand removes it. Settings from
  before 1.8 load with the list empty.
* **Six new species** (`SpeciesDefaults`, appended after the phoebe, so ids and order never move). Awareness is
  aware / alert / flee in metres. Rhythm order is dawn, morning, midday, golden hour, dusk, night.

  | Species | Real size | Aware / alert / flee | Group | Rhythm | Where (residence radius) |
  |---|---|---|---|---|---|
  | American crow | 0.45 m | 26 / 15 / 7 | 2 to 3 | Home all day | Trailhead (8 m), a Reserve Road pull-off (8 m) |
  | American robin | 0.25 m | 18 / 10 / 5 | 2; plus 1 loop group | Meadow, Meadow, Woodland, Meadow, Brush, Home | Trailhead (8 m), loop meadow hotspots |
  | Black-capped chickadee | 0.13 m | 10 / 5.5 / 2.5 | 2 to 3 | Home all day | Fern Glade (8 m), Old-Growth Grove |
  | American goldfinch | 0.12 m | 15 / 8.5 / 4 | 3 to 4 | Brush, Meadow, Meadow, Meadow, Brush, Home | Wildflower Meadow Blind |
  | Song sparrow | 0.15 m | 16 / 9 / 4.5 | 1; plus 2 loop groups | Home all day | Pond brush (7 m), Creek Alcove (8 m), loop brush piles |
  | American black bear | 1.6 m | 75 / 50 / 28 | exactly 1 | MeadowEdge, Woodland, Home, Woodland, Water, Home | Deep woods (10 m) |

  The chickadee has the smallest distances in the reserve and an alert patience of 8 s (the others 3 to 5), so it accepts
  you soonest. The bear has the widest: it only ever moves away (flee distance 45 to 75 m, walk 1.1 m/s, run 9), has no
  group, spawns 45 to 110 m from the trail and keeps to forest edge. Crow: flight 4 to 10 m. Robin: perches long,
  flits short (the flying brain has no ground foraging). Habitat hotspots: crow mature trees and perches; robin meadow;
  chickadee mature trees; goldfinch meadow; song sparrow brush piles.
  **Positions come from existing site data only, with no new random draws**, so nothing else in a build moves:
  `EcosystemBuilder.BuildNewResidents` puts the **Trailhead** residence midway between the Reserve Board and the bike
  bay (birds only fly and perch in nearby trees: the board and the bike are not perches), the **Road pull-off** at the middle
  one of the destination pads, and the **Deep woods** bear at the woodland anchor that scores highest on distance from
  the trail's start plus its distance from any path (capped at 40 m); with no woodland anchor, the far side of the
  Old-Growth Grove at half its radius. The Fern Glade (`DiscoveryBuilder`), the pond brush (`ReserveBuilder`), the
  Creek Alcove (beside the raccoon) get a `Residence`; the grove and the blind list the new species in their site
  residents.
* **Spawner own streams** (`WildlifeSpawner.OwnStreamSpecies`). The six new ids draw their groups and residents from
  their own `System.Random` (seed XOR a hash of the id, plus a salt per place) and their individuals' seeds count from
  100000, so the shared stream and every earlier animal's seed are exactly as before. **A new species' id must be added
  to this set.**
* **Small life** (`MacroPackSetup`, `MacroBuilder`). A subject takes its `prefab` and up to **five `variants`** of the
  same match kind (so up to six models). Matching reads **every folder above a model**, not just the nearest: a folder
  can name a subject only as the lowest rank, `ByFolder` (order None, ByFolder, StandIn, Exact; a candidate's rank is
  kind × 4, plus 2 if animated, plus 1 if a prefab). A folder that is itself an excluded thing ("Mushroom_Houses") says
  nothing. **Among equally ranked models** the simplest names win (`NameScore`: fewer words; 10 per word such as big,
  large, huge, giant, small, tiny, mini, broken, dead, damaged, old, lod, collider, base, stem), then the one of **middle
  size**, not the smallest or biggest. A folder-matched variant must stay in the pick's own top-level pack folder, have
  the same rank and be 0.25 to 4 times its size. Exclusions gained ground, terrain, demo, scene, plane, floor, rock,
  stump, log, grass, material, sphere, cube, sky, camera, water, background, leaf, leaves, branch, twig, pebble, stone;
  but a mushroom may have stump or log in its name ("Mushroom_On_Stump" stays). The Console lists "equally good, not
  used" models so one can be dragged in by hand.
  **Placement:** each instance picks a variant from the subject's seeded generator (the build repeats); **mushrooms are
  scaled 0.8 to 1.6 times** the subject size (a pack's mushrooms are often clusters); **at most 2 of a species on one log
  or stump** (`MaxPerLog`), and when every log is full the rest go to the woodland floor. The woodland mushroom's count is
  8 (was 5). Pack materials are converted for URP once per source model and shared by its instances, in
  `Assets/WildLens/Generated/SmallLife/Materials`; a model that still uses a shader URP cannot draw is warned about once.
* **Asset Audit additions.** An **Other static models** section lists everything nothing else recognised, grouped by
  pack, at most 30 per pack and 150 in all, with triangles and size (read from the file for the rows listed). Vehicle
  rows list up to 12 child node names, and "(by folder)" marks a vehicle or macro subject named only by a folder above it.
  Animation-only files (no mesh, folder name starting "anim") are one line per folder; rigs, spawners and demos (no mesh,
  a name word controller, manager, spawner, system or demo) are left out of the tables and counted in the Summary.
  Cat, kitty and kitten are a "pet cat"; "pinguin" is exotic; "shroom" is a fungus; a bear is no reserve species unless it
  could be the black bear. The audit and the converter share one shader test.
* **Journal list paging** (`JournalUI`). The list is a window of **7 rows** (row pitch 92 px, row height 84 px, first row
  136 px below the book's top edge; the list column ends at 810 px and the footer starts at 826 px) that follows the
  selection, with "^ more" and "v more" hints. W/S and the mouse wheel move one species at a time (wheel: direction only,
  at least **0.12 s** apart on unscaled time). The footer reads "W / S / wheel  select species".

## Asset Audit, species matching and small life (1.7.0)

* **Asset Audit Report** (`Editor/AssetAudit.cs`; menu WildLens ▸ Asset Audit Report, and a button in the builder
  window). Read-only: it walks every `GameObject` asset under `Assets/` (skipping `Assets/WildLens` except the bundled
  animals, and `Editor` and `Gizmos` folders; `Name@Clip` files are counted as clip sources, not models), classifies each
  by its name, then by its folder, and writes `WildLensAssetAudit.md` beside the `Assets` folder and to the clipboard
  (the clipboard gets the full report when it is under 60,000 characters, otherwise a short version: Summary, Animals
  master table with a **Clip names** column (up to 12 `name→Role`), Species coverage, Macro subjects, Vehicles and
  Unrecognised animated models, long tables trimmed with "+N more rows in the file"; the dialog says which was copied;
  the per-clip detail is in the file only). Models whose path shows only scenery or prop words and no animal-ish folder
  (animals, creatures, birds, insects...) are counted from their names without being opened (the Summary says how many);
  "DragonFly" and other compounds (butterfly, firefly, ladybug...) are macro subjects before the animal census, and a
  static model with bat, cricket, pot, net, house, hive, lamp or box plus an animal word is "other static".
  Categories: (a) reserve species, exact or stand-in; (b) other animals, each with a suggested reserve role and a flag
  for birds and for animals that are not Willow Creek's (lion, zebra...); (c) macro subjects (fungi, plants, small
  animals); (d) vehicles; (e) scenery (counts only: trees, rocks, plants, props); (f) animated models nothing
  recognised. Sections: Summary (pack table), Animals master table (triangles, size, skinned, clip count, roles
  recognised and missing, shader and pipeline check, which species prefab was built from it), Species coverage (real
  model, stand-in available, or placeholder, for all twenty-four since 1.8), Per-clip detail, Macro subjects, Vehicles, Animated
  models nothing recognised, Scenery noticed. A static model with a scenery word and an animal word ("Butterfly Bush")
  is filed as scenery. Rows are capped at 400 a table, clips at 40 a model.
* **Vehicles in the audit**: strong words (motorcycle, motorbike, dirtbike, scooter, atv, utv, jeep, truck, car...); the
  bare word "bike" counts only with a qualifier (trail, dirt, motor, moto, enduro, offroad, cross, mountain, mtb) or two
  or more wheel nodes. The report gives length, wheel nodes (names with wheel, tire or tyre), whether a steering part
  (handlebar, fork, steer) exists, and which of the bike's pivot names (`Lean`, `Steer`, `Front Wheel`, `Rear Wheel`,
  `Front Axle`, `Swingarm`, `Kickstand`, `Seat Anchor`) it already has.
* **Species name tables** (`WildlifePackSetup.Names`; exact names, then stand-ins **in ranking order**: earlier is
  closer, so "wolf" beats "dog" for the coyote):

  | Species | Exact | Stand-ins |
  |---|---|---|
  | White-tailed deer | whitetail, deer, doe, stag, buck, fawn | roe, elk, reindeer, caribou, moose, antelope, gazelle, impala |
  | Red fox | fox | shiba, fennec, jackal, dog (1.8) |
  | Coyote | coyote | wolf, dingo, dhole, jackal, husky, shepherd, dog |
  | Eastern cottontail | cottontail, rabbit, bunny, hare |   |
  | Eastern gray squirrel | squirrel, squirel | chipmunk |
  | Raccoon | raccoon, racoon | badger, redpanda, coati |
  | Virginia opossum | opossum, possum | rat, mouse |
  | Groundhog | groundhog, woodchuck, marmot | beaver, gopher, prairiedog, capybara, hamster, guineapig |
  | Wild turkey | turkey | pheasant, grouse, partridge, quail, guineafowl, chicken, rooster, hen, peacock |
  | Red-tailed hawk | hawk, buteo | eagle, falcon, buzzard, harrier, osprey, kite, vulture, raptor |
  | Barred owl | owl |   |
  | Great blue heron | heron | egret, bittern, ibis, spoonbill, pelican, crane, stork, flamingo |
  | Spotted sandpiper | sandpiper | killdeer, plover, dunlin, snipe, shorebird, wader, tern, seagull, gull |
  | Northern cardinal | cardinal, redbird | songbird, finch, sparrow, robin, oriole, canary, chickadee, wren, tit, thrush, warbler, swallow, bird |
  | Blue jay | bluejay, jay | bluebird, songbird, finch, sparrow, robin, chickadee, tit, wren, thrush, warbler, swallow, bird |
  | Red-winged blackbird | blackbird | grackle, starling, sparrow, songbird, crow, raven, thrush, bird (1.8 order) |
  | Belted kingfisher | kingfisher | songbird, sparrow, bird |
  | Eastern phoebe | phoebe, flycatcher | sparrow, songbird, finch, wren, chickadee, bird |
  | (1.8) crow, robin, chickadee, goldfinch, song sparrow, black bear | see the 1.8.0 section | |

  A name is split into lower-case words (non-letters and camel case: "RedFox_Idle01" is red, fox, idle) plus the whole
  name squashed ("WhiteTailDeer" gives whitetaildeer). A keyword of four letters or fewer ("fox", "owl", "jay", "cat")
  must be a whole word or its plural, so "Foxglove", "Bowl" and "Cattail" do not match; longer ones may sit inside a
  word ("RedSquirrel01"). A key ending in `=` is always whole-word.
* **Reserved list** (`WildlifePackSetup.Reserved`): woodpecker, hummingbird, duck, mallard, goose, swan. A model with one
  of these words in its name matches **no** species, not even as a stand-in, and the folder name is not tried for it
  ("Birds/Woodpecker" must not become the cardinal). They are kept for future species (see WildlifeCampaign.md).
* **NeverAnimals** (`WildlifePackSetup.NeverAnimals`): a model whose own name has one of these whole words (beetle, moth,
  bee, spider, bug, ant, butterfly, dragonfly, firefly, mantis, snail, fish, net, statue, toy, hat, mask, costume,
  skeleton, bone, skull, meat, trophy, rug, pelt...) matches no species ("Stag_Beetle", "Deer_Skull"). "fly" is not on it
  ("Eagle_Fly" is a model with a clip in its name).
* **Match order**: the model's own name first; only when that says nothing, the name of its folder ("Deer/Model01"),
  and a folder gives a **stand-in at most** (never exact), ranked after every name-based stand-in.
  Candidates for a species sort: the animal itself before a stand-in; a stand-in that is some other species' real model
  after one that is not ("owned elsewhere"); the keyword nearer the front of the list; more recognised clips; then by
  path, so every machine picks the same model. A tie is never left to the order Unity lists files in. The Console line
  after an automatic setup lists the species set up, then **those with no match** (placeholders), then points to the
  audit.
* **Clip classification** (`AnimalPrefabFactory.TryClassifyName`): the clip name is split into words (non-letters, digits
  and camel case: "Armature|Walk_01" is armature, walk). A keyword matches a **whole word**, or the word plus a common
  suffix (s, es, d, ed, er, ers, ing, ning, ting, ping, ming, ging, ned, ted, ped, loop, cycle, anim), so "Threat" is not
  `Eat`, "Forest" is not `Lie`, "Trunk" is not `Run`, "Island" is not `Land`. The first role that hits wins, in this
  order:

  | Role | Words |
  |---|---|
  | TakeOff | takeoff, liftoff (matched against the squashed name, so "Take Off" works) |
  | Land | landing, land |
  | Glide | glide, gliding, soar |
  | Fly | fly, flies, flap, flight, hover, wing |
  | Eat | eat, graze, grazing, feed, drink, sniff, forage, foraging, peck, chew, nibble |
  | Lie | lie, lying, laying, laydown, rest, sleep, nap |
  | Sit | sit |
  | Groom | groom, lick, scratch, shake, shaking, preen, ruffle (1.8) |
  | Look | look, alert, listen, attention, turnhead, headturn, watch, search, then worried, sing, call, chirp, tweet, song (1.8) |
  | Run | run, gallop, sprint, flee, dash, chase |
  | Trot | trot, canter, jog |
  | Walk | walk, hop, jump, move, crawl, amble |
  | Idle | idle, stand, breath, perch, stay, wait |

  **Ignored clips**: any clip with a word from death, dead, die, dying, attack, attacking, hit, hurt, damage, roar, bite,
  biting, kill is never mapped (it would put a death pose in an idle slot); 1.8 adds stun, stunned, buff, gethit, knockback,
  dodge, block, taunt, spawn and despawn. With no Idle clip the Look clip is used (1.8; before, the first clip), so the
  animal is never in a T-pose.
* **Small-life subjects** (`SubjectDefaults`, data in `Assets/WildLens/Data/Subjects`, created once and then yours to
  tune). Hours use the DayPeriod order Night, Dawn, Morning, Midday, Golden hour, Dusk. "Daytime" is Morning, Midday and
  Golden hour. **Rain**: "hides" means not showing while it rains (`Rain` > 0.25); "after rain" means showing only while
  it rains and for three game hours after.

  | Id | Name | Group | Behaviour | Habitat | Hours | Rain | Size (m) | Count | Hover (m) |
  |---|---|---|---|---|---|---|---|---|---|
  | `monarch_butterfly` | Monarch Butterfly | Butterfly | Flits | Flowers | Daytime | hides | 0.09 | 6 | 0 |
  | `luna_moth` | Luna Moth | Moth | Still | DeadWood | Night, Dawn, Dusk | | 0.10 | 4 | 0 |
  | `common_whitetail` | Common Whitetail | Dragonfly | Perches | WaterEdge | Daytime | hides | 0.06 | 5 | 0.4 |
  | `bumble_bee` | Common Eastern Bumble Bee | Bee | Flits | Flowers | Daytime | hides | 0.02 | 6 | 0 |
  | `ladybird_beetle` | Spotted Lady Beetle | Beetle | Still | MeadowVerge | Daytime | | 0.012 | 6 | 0 |
  | `praying_mantis` | Carolina Mantis | Mantis | Still | MeadowVerge | Morning to Dusk | | 0.06 | 4 | 0 |
  | `northern_leopard_frog` | Northern Leopard Frog | Frog | Hops | WaterEdge | all day and night | | 0.08 | 5 | 0 |
  | `red_spotted_newt` | Eastern Newt (red eft) | Salamander | Still | WoodlandFloor | Daytime | after rain | 0.07 | 4 | 0 |
  | `woodland_snail` | Whitelip Snail | Snail | Still | DeadWood | Night, Dawn, Morning, Dusk | after rain | 0.03 | 4 | 0 |
  | `firefly` | Common Eastern Firefly | Firefly | Glows | MeadowVerge | Night, Dusk | | 0.012 | 6 | 0.8 |
  | `woodland_mushroom` | Honey Mushroom | Fungus | Still | DeadWood | all day and night | | 0.07 | 8 (1.8; was 5) | 0 |
  | `oxeye_daisy` | Oxeye Daisy | Wildflower | Still | MeadowVerge | Daytime | | 0.05 | 6 | 0 |

  The two bundled plants (`common_dandelion`, `lesser_periwinkle`) are unchanged. `SubjectBehaviour` now has `Flits`,
  `Perches`, `Hops` and `Glows` implemented (`Still` as before). Bees, beetles and fireflies use a smaller frame-fill
  grade (Good 0.10, Excellent 0.22; the default is 0.18 and 0.35): a 2 cm bee reaches 0.22 at 0.36 m, and a 1.2 cm beetle
  at 0.22 m, both beyond the 8 cm minimum focus.
* **Finding models** (`Editor/MacroPackSetup.cs`; menu WildLens ▸ Macro Subjects From Packs; run on every rebuild when
  `SliceBuildSettings.autoMacroFromPacks` is on). It looks at every model **and prefab** under `Assets/` outside
  `Assets/WildLens` and `Editor` folders (static meshes are welcome: a mushroom is not animated), skipping `@` files,
  names with collider, collision or LOD 1 to 9, anything larger than 3 m, and anything with no mesh (a MeshFilter mesh or a
  skinned mesh). Only files whose name or folder matches a still-wanted subject are loaded; with none wanted, nothing is. Only a subject whose `prefab` is empty is
  touched; the choice is never overwritten. Names (same word rules as the animals; a four-letter key such as "bee" must
  be a whole word):

  | Subject | Exact | Stand-ins |
  |---|---|---|
  | Monarch Butterfly | monarch, butterfly | |
  | Luna Moth | luna, moth | |
  | Common Whitetail | dragonfly, damselfly | |
  | Bumble Bee | bumblebee, bee | wasp |
  | Lady Beetle | ladybug, ladybird, ladybeetle | beetle, bug |
  | Mantis | mantis | grasshopper, cricket, stickinsect |
  | Leopard Frog | frog | toad |
  | Newt | newt, salamander, eft | lizard, gecko |
  | Snail | snail | slug |
  | Firefly | firefly, lightningbug | |
  | Mushroom | mushroom, toadstool, fungus, fungi, shroom | |
  | Oxeye Daisy | daisy, oxeye | flower, wildflower |

  A folder name ("Frogs/Model_03.fbx") can name a model, but only as a stand-in. **Exclusions** (one list for every
  subject, whole words or plurals): hive, house, hut, home, box, nest, lamp, light, lantern, statue, toy, pot, vase,
  bouquet, bed, bush, net, fish, soup, sign, icon, swarm, particle(s), vfx, fx, ui, sprite, decal, card, tile, pattern,
  print, cloud, tree, costume, hat, mask ("Spotted_Daisy" is fine, "Flower_Pot" is not). **Ranking**: exact before
  stand-in; then animated (skinned or with clips) before static; then prefab before raw model; then (1.8) the simplest names and the middle size, with a folder-only match ranked lowest; then path order. The
  automatic run creates no Subjects folder or assets until some pack model matches a subject. Subjects are served in id order. The
  Console lists each subject with its model, "(stand-in)" where it is one, and "no model found: stays out of the
  reserve" where not.
* **Placement** (`Editor/MacroBuilder.cs`, `BuildSmallLife`, after the dandelions and periwinkles). If no bundled plant and
  no pack subject has a model, nothing is created, as before. Each subject uses **its own** `System.Random` seeded from
  `seed × 613 + 29` plus a stable hash of its id (not `string.GetHashCode`, which differs between machines), so adding
  one subject moves no other, and the builder's shared sequence is untouched. Subjects are placed in id order. Where a
  spot cannot be found the subject quietly falls back, then skips that one. Every ground-level subject adds a 0.7 m
  keep-out so terrain grass does not grow over it.

  | Habitat | Where | Rules |
  |---|---|---|
  | Flowers | the head of an unused dandelion (one subject per flower, 1 cm up, up to 1.5 cm off-centre) | Falls back to MeadowVerge if no dandelion is left |
  | MeadowVerge | beside the Meadow Loop, 0.7 to 2.6 m beyond the trail's edge, either side | not in a keep-out, forest field ≤ 0.4, slope ≤ 18°, 3 m or more from the creek; 60 tries |
  | WaterEdge | Cattail Pond's shoreline (0.3 to 1.5 m beyond its own edge, not under water), each creek water spot (ford bar, plunge pool, alcove: inside its radius), and along the creek banks | 1.7 to 3.4 m from the creek line for creek spots, slope ≤ 20°, 1 m or more off any path; 40 tries |
  | WoodlandFloor | the Fern Glade (1.5 to 5 m from its centre) and the Old-Growth Grove (6 to 18 m); else forest 4 to 12 m off the trail | 1.5 m or more off any path, not in a keep-out, 2.5 m or more from the creek, slope ≤ 30°; 60 tries |
  | DeadWood | on top of a fallen log or stump the builder placed (a ray onto its collider; else the top centre) | Falls back to WoodlandFloor if there are none |

  Each model is scaled so its largest dimension equals the subject's `size`, turned to a random heading, and set on the
  surface (plus `hoverHeight`). Pack scripts, joints, colliders, rigid bodies and audio sources are removed. A model with
  clips but no controller gets a one-state looping controller (idle, fly, flap, hover or wing clip if there is one, else
  the first), saved in `Assets/WildLens/Generated/SmallLife`; the pack's own import settings are untouched. Each instance
  gets a `MacroSubject` with a focus point at the centre of its bounds and a focus size of 1.4 × the subject's size.
* **Movement** (`Scripts/Photography/MacroMotion.cs`, added only to subjects that are not `Still`; purely cosmetic).
  Nothing moves farther than 25 m from the camera (a once-a-second distance check), or while hidden.
  **Flits**: sits with a slow 4° wing-settle roll; every 6 to 14 s (the first after 2 to 8 s) it flutters over 1.2 to 2 s
  on a 0.08 to 0.16 m arc, half the time back to its flower, otherwise to within 0.15 to 0.5 m of it. **Perches**: still,
  then every 8 to 20 s a loop 1.2 to 2 s long (radius 0.16 to 0.32 m, up to 0.15 m of height) that ends exactly on its
  perch. **Hops**: still until the camera comes within 0.9 m **along the ground** (not 3-D: the camera is at eye height),
  then one hop of 0.4 m away from you, 0.1 m high, over 0.35 s, keeping its height above the ground; it will not hop
  again for 6 s. A 1.2 m leash on where it started: a hop that would land farther goes back toward the start instead. **Glows**: drifts about 0.25 m round its
  point, and every 2.5 to 5.5 s flashes a soft yellow-green emission for half a second.
* **Visibility** (`MacroSubject`): `IsShowing` combines the subject's hours, `hidesInRain` and `afterRain`. **Recently
  rained** (`MacroSubject.RecentlyRained`) is true while `WeatherCycle.Rain` > 0.25 and for **3 game hours** after (the
  day counter handles midnight; setting the clock back forgets it). Animals from packs hide their renderers when not
  showing (checked once a second, each with its own phase). **Plants and fungi never vanish** (`hideWhenNotShowing` is off
  for Wildflower and Fungus groups), including the daisy and mushroom (and, since 1.9, ferns and moss); they stay where they grow. `MacroAnalyzer` still
  skips any subject that is not showing, so a dandelion closed in rain stays in the grass but cannot be focused on. The
  macro lens, the analyser and the journal needed no other changes.

## Trailhead, signs and wildlife packs (1.6.0)

* **Reserve Board** (`Editor/PropBuilder.cs`, `BuildKiosk`): one roofed, three-sided kiosk at loop distance 7 m,
  facing the spawn. Faces stand on an equilateral triangle (side 2.8 m, inradius 0.81 m); corner posts (named `Post`,
  grounded) carry a three-sided pitched roof (`ProceduralMeshes.Cone(3)`, radius = circumradius + 0.9 m, so the eaves
  stand ~0.45 m past each face). **Face A** (to the visitor): header plank and the live `ReserveInfoBoard`. **Face B**:
  PLEASE with three pictogram lines (2.4 cm character size, ~10 cm capitals). **Face C** (whichever back face looks
  towards the bike bay): RESERVE ROAD, ride slowly / park at pull-offs / walk the last stretch, and the trail
  register box. Replaces the separate guidelines post, register post and the road's trailhead plank.
* **Bike bay** (`BuildBikeBay`): the bike at loop distance 12 m, 3.2 m outside the trail edge, angled 0.6 out towards
  the trail, with a hitching rail and a `ReserveParking` pad ("Trailhead"). `BikeParking` (where the road starts) is
  the bay.
* **Sign sizes:** `SignKit.LineSize` 0.019 (~8 cm capitals; was 0.015), sign pictograms 0.16 m on a 0.19 m step,
  sign boards 0.66 m tall with a line. LOOKOUT markers fit their title to 0.55 m. Interpretive panels: face 1.5 ×
  0.88 m, body text column 0.98 m at up to 0.016 character size, **three short lines of ≤ 21 characters** (so the body
  stays at ~5 cm capitals instead of being fitted down to 3.5 cm).
* **Reserve board text:** conditions line 34 px with best-fit down to 26 (never spills off the header); distances 34
  px; footer "Walk quietly · wildlife keeps hours" at 32 px (riding advice moved to face C).
* **Seat logs** (`GroundingPass.SeatLogs`): any renderer named `Seat Log` (not on a deck) is tilted along its length
  to follow the ground (clamped to 12°) and bedded 6 cm in. Logged with the posts and legs count.
* **Wildlife Pack Setup** (`Editor/WildlifePackSetup.cs`, menu WildLens ▸ Wildlife Pack Setup): scans `Assets/`
  (outside `Assets/WildLens`) for GameObjects with a `SkinnedMeshRenderer` and at least one clip (own clips, the
  Animator controller's, the skinned mesh's source model, and `Name@Clip`/`Name_Clip` files beside it). Matches by
  name words (camel-case and underscores split; words ≤ 4 letters must be whole words or plurals, longer ones may sit
  inside a word; the folder name is tried too, as a stand-in only) against `Names`: exact words, then stand-ins. Rows sort
  exact before stand-in, then by recognised clip count (1.8 ranking: see the 1.8.0 section). Clips: names with to, transition, pose, tpose, bind or from are
  ignored ("Jump_ToIdle", "Rest_Pose"); "Wings_Folded" is not Fly; "Idle_HeadLow" is Eat; per role the clip with the fewest
  words wins (an Idle with its head down loses), ties keep the earlier clip. Size defaults from `AnimalPrefabFactory.DefaultSize` (since 1.8 a table of real sizes; before, the body plan); birds are
  scaled by beak-to-tail length (+Z bounds), mammals by their largest side.
* **Automatic setup**: `WildlifePackSetup.AutoSetUp` runs at the end of every build (before the player is built) and,
  through `WildlifePackImportWatcher` (an `AssetPostprocessor`), after any `.fbx/.prefab/.blend/.gltf/.glb` import
  outside `Assets/WildLens`, deferred until the import finishes. It only fills species whose `prefab` is empty, with
  the top row's model, default size and stand-in tint. Setting: `SliceBuildSettings.autoWildlifeFromPacks` (on).
* **Bundled animals (1.6.1)**: `Art~/Animals/Wolf.fbx`, `Eagle.fbx` (CC0, Quaternius) are copied to
  `Assets/WildLens/Art/Animals` (`ArtLibrary.AnimalsFolder`) with materials, Generic rig and looping clips (names after
  the last `|`). `FindAnimatedModels` includes that folder, so they enter matching as stand-ins like any pack.
* **Automatic Environment Kit (1.6.1)**: at the start of a build, while `settings.environmentKit` is missing or empty and
  `autoEnvironmentKit` is on, `EnvironmentKitScanner.ScanQuietly` scans without dialogs and assigns the kit.
* **Posed feet**: the factory samples the idle clip at t = 0 and measures baked skinned meshes for the feet and centre;
  it multiplies the yaw into the model's own root rotation instead of replacing it (Blender files carry an axis fix).
* **AnimalPrefabFactory** (`Editor/AnimalPrefabFactory.cs`): the shared classify → controller → prefab code (moved
  from Animal Model Setup). `Tint` copies a model's materials to `Assets/WildLens/Animals/{id}_Tint{n}.mat`, ordered
  by triangle coverage, coloured primary / secondary / accent from the species asset (`_BaseColor` and `_Color`).

## Willow Falls, hidden places, macro, bike, grounding (1.5.0)
* **Falls plan** (`Editor/FallsBuilder.cs`, `PlanFalls` inside `PlanCreek` after `ComputeBed`): over creek samples
  n/6 … n−26 (not within pond radius ×1.4 + 40 m, not inside the loop), the largest `bed[i+1] − bed[i+20]`; needs ≥ 2.2 m.
  Drop D = clamp(drop − 0.4, 2, 7). Below the lip: bed = min(original, lerp(bed[L] − D, bed[L+20], t)); pool at
  L+1…L+3 (bed −0.8, water level); `_carveExtra` widens the carve by 6·(1−t)^1.5 m and raises the wall profile by
  0.95 per metre of extra width (`CarveCreek`). The water strip skips the lip quad. `PlanFallsSite` (after `PlanCreek`)
  adds a `DestinationKind.Waterfall` site: deck on the gentler rim 6 samples below the lip, 4.6 + extra + 3.5 m out.
* **Falls build**: rock face (scaled to D + 1.6 m) set into the lip facing downstream, two ravine walls, curtain mesh
  (11 rows, arcing out up to 1.1 m, 3.2→4 m wide) with streak material (pipeline particle shader) scrolled by
  `WaterFlow.scrollBase`; `Waterfall` (runtime): spray particles (mist material), `Spate` rises at 0.25/s while it
  rains and falls over ~3 game hours; flow = 1 + 0.9·spate scales curtain speed, spray rate (16·flow) and roar volume
  (`AmbientKind.Falls`, radius 90 m). Deck, top lookout, sign (best times "morning light · after rain" → sun + rain
  icons), panel, Perch and Water hotspots, Water anchor, residents (phoebe, squirrel). Roads avoid 28 m around it.
* **Hidden places** (`Editor/DiscoveryBuilder.cs`, runtime `ReserveDiscovery`): overlook = highest (prominence×2 +
  height×0.1) point with prominence ≥ 2.5 m over a 40 m ring, ≥ 160 m from the ridge, ≥ 35 m from paths, slope ≤ 12°;
  glade = most trees within 14 m (≥ 7), ≥ 30 m from paths, cleared to 6 m, 34 ferns, log, stump; alcove = creek sample
  maximising forest×30 + bend×0.4, ≥ 40 m from paths, ≥ 70 m from the ford, ≥ 30 samples from the falls. HUD shows a
  note on first entry per session (`ReserveDiscovery.Found`). Not on the board or map.
* **Macro**: `NaturalistSubject` (data: group, size, behaviour, periods, rain), `MacroSubject` (world, optional focus
  point), `MacroAnalyzer` (≤ 2.5 m, fill = largest projected side, sharp if |focus − distance|/distance < 0.12, centred
  within 0.22; Excellent at fill ≥ 0.35 + sharp + centred, Good at ≥ 0.18 + sharp). `ZoomLens.MacroMode` (M): FOV 24°
  / 1.7×; zoom out at the first step leaves macro; lowering the camera leaves macro. `FocusController` focuses on
  `MacroAnalyzer.Centred` down to 8 cm. `PhotoCamera` records the best macro hit in `JournalSaveData.notebook`
  (`JournalService.RecordNotebook`). Plants: `ArtKind.MacroPlant` (split, LOD0 only, `minPiece` 0.04 m);
  `MacroBuilder` places 12 dandelions on loop verges (grass kept off) and periwinkle in the glade and grove.
* **Bike** (`TrailBikeModel`, `TrailBike`): parts add `frontAxle` (slides along the fork) and `swingarm` (pivots);
  `Suspension()` sets travel = bike-local height of the ground under each axle + sag (0.025 ridden) + dive (0.035
  braking) / squat (≤ 0.02 throttle), clamped −0.07…+0.08, SmoothDamp 0.035/0.045 s. Parked steer −24°. `PullOver()`
  on dismount when within 0.3 m of a road's edge distance and not at a pull-off. Road top speed 12.5 m/s; acceleration
  × (1 − 0.55·(v/top)²).
* **Grounding** (`Editor/GroundingPass.cs`): upright renderers named Post/Leg/… whose bottom is above the lowest terrain
  under their footprint − 0.18 m are scaled down to it (≤ 3.5 m), unless a built collider is just below (decks,
  bridges). **Reflections** (`ReflectionRefresh`): pond probe re-renders on period/weather change and every 20 s,
  time-sliced.

## Weather, landforms, Creek Ford (1.4.0)
* **Weather** (`Scripts/Environment/WeatherCycle.cs`, execution order -60): four forecast days of (hour, cover, rain)
  keys, chosen by `(firstDay + DayCycle.Day) % 4`; values eased (rate 0.8/s). `Condition` (Fair / Cloudy > 0.4 /
  Overcast > 0.68 / LightRain rain > 0.2) raises `Changed`. `OvercastSky` with hysteresis (on > 0.66, off < 0.55).
  `DayCycle.Apply` applies it: sun × lerp(1, 0.3, cover), shadow strength × lerp(1, 0.3, cover), sun colour toward
  grey, sky exposure × lerp(1, 0.78, cover) and tint toward grey, the overcast sky material (a Play-mode copy), ambient
  × lerp(1, 0.9, cover), fog colour toward grey haze and density × (1 + 1.2 cover + 1.6 rain), URP temperature −6 cover,
  exposure +0.12 cover. Rain: a world-space stretched-billboard particle box 36 × 36 m, 11 m above the camera, up to
  3800/s, builder-made streak material (URP Particles/Unlit or legacy alpha blended), procedural rain loop.
  `WildLensWind.WeatherMultiplier` = 1 + 0.35 cover + 0.6 rain. Test looks with *Override Weather* in Play mode.
* **Shelter** (`WildlifeSchedule`): re-applies on period change *or* when `Raining` (rain > 0.25) flips. In rain,
  Meadow/MeadowEdge → Woodland, Ridge → Perch; activity bias Resting/Sitting ×2, Grazing/Foraging ×0.6.
* **Landforms v2** (`WildLensSliceBuilder.GenerateFallbackHeights`): in metres on a 60 m design scale, terrain height
  ≥ 90 m. `upland` = smoothstep along a seed-chosen direction; `ridge` = (1 − |2·perlin − 1|)^1.6 at 1.5×scale, weighted
  0.07 + 0.22·upland; `hills` ±0.045; terracing 10 levels at 30% on the upland; all ridge/hill/shelf terms masked to
  zero inside 0.13 of the centre. `regenerateWildLensTerrain` rebuilds heights every build for WildLens terrains.
* **Woods field** (`TerrainPainter.WoodsField`): 3 m grid of distance to the nearest placed tree; `Woods(p)` is 1 within
  6 m and 0 beyond 12 m, and gates the forest field in splat painting (forest floor) and details (ferns, open grass).
* **Creek Ford** (`Editor/FordBuilder.cs`): `PlanFord` after roads (own RNG): creek samples in the middle 60%, path
  distance 18–70 m, water drop < 0.8 m over 8 samples, lowest score of |d − 32|·0.6 + drop·20 + forest·25; spur
  footpath (1.3 m) from the nearest path point to 4.6 m from the centreline; keep-outs. `BuildFord`: stepping stones
  every 0.75 m at water + 0.06 with box colliders, gravel bar (painted rock, no grass, `FordBar`), snag + Perch hotspot,
  Water hotspot, seat log, `SignKit` sign and panel, brook voice, Water anchor, residence (kingfisher, sandpiper,
  heron), viewpoint.
* **Sign check** (`Editor/SignCheck.cs`): per TextMesh, nearest sibling named Board/Panel/Finger/Plate/Header Plank up
  to two levels; glyph vertices in board space must lie within ±0.5 (2% slack), stand proud of a face, read outward,
  and have capitals ≥ 3 cm. Runs three editor frames after a build and from the menu.

## Signage, woodwork and water (1.3.0)
* **Sign standard** (`Editor/SignKit.cs`): `Sign(parent, pos, rot, title, line, icons, number, post)` builds a dark
  stained board sized to its words: number disc (destinations), bold title (`TitleSize` 0.024 ≈ 10 cm capitals), one
  row of pictograms plus a short line (`LineSize` 0.015). `Panel(...)` builds the interpretive lectern: pictogram,
  title on a green band, two short lines. `TextWidth` sums the font's glyph advances (TextMesh: one pixel at size 64 is
  0.1 × characterSize m); `Fit` shrinks text to a width. **Positions passed to `SignKit.Label`/`Icon` are in reader
  space** (x to the reader's right); `SignKit.Reader` converts (the sign's -X is the reader's right). `TimeIcons(bestTimes)` turns best-times words into sunrise /
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
* **Animal Model Setup** (`Editor/AnimalModelSetup.cs`, menu WildLens ▸ Animal Model Setup; the code is in
  `AnimalPrefabFactory` since 1.6): turns an animated model into a species prefab: classifies clips by name, builds an Animator Controller (Speed blend tree; any-state
  transitions on Activity), scales the model to real size, root motion off, assigns `species.prefab`.

## Data migrations

* Species assets: `dataVersion` 4 (1.9: weather habits). `SpeciesAssetMigrator` applies current gameplay tuning to the
  built-in species after script reload (**WildLens ▸ Upgrade Species Assets** to run manually); an asset already at 3 gets only
  the natural-events data (`SpeciesDefaults.ApplyHabits`).
* Small-subject assets: `dataVersion` 2 (1.9: generated model, condition, second tier; `NaturalistSubject.UpgradeToV2`).
* Generated small-life models: `SmallLifeModels.ModelVersion` 2. Animal prefabs: `AnimalPrefabFactory.FactoryVersion` 3.
* Journal save: version 3 (adds `identifiedBy`, `observedBehaviors`); version-1 scores were converted in 0.2.0.
