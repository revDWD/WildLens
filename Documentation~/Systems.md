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

## Camera — `Scripts/CameraSystem/`

| Component | Responsibility |
|---|---|
| `CameraModeController` | Hold **RMB**. `IsActive`, eased `Blend` (~0.22 s), `IsReady` (can shoot), movement ×0.5, look ×0.8 (then scaled by zoom). Events `Entered` / `Exited`. |
| `ZoomLens` | Sole owner of FOV. Walking 65°; camera 1× = 50° (≈26 mm). Steps **1× 2× 4× 8× 16×** (16× ≈ 410 mm), smoothed in log space. The wheel also works with the camera lowered: it **preselects** the lens (a notice confirms it). Exposes `EquivalentFocalLength` for the readout. |
| `CameraAudio` | Shutter, zoom detent, raise rustle, identification chime (synthesised; assign clips to replace). |
| `PlayerCameraRig` (Player) | Head-bob (reduced in camera mode and crouched), raise dip, handheld sway (+25% per zoom doubling, settles when still or crouched). Adds any missing camera components at start-up. |

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

## Data migrations

* Species assets: `dataVersion` 3. `SpeciesAssetMigrator` applies current gameplay tuning to the built-in species
  after script reload (**WildLens ▸ Upgrade Species Assets** to run manually).
* Journal save: version 3 (adds `identifiedBy`, `observedBehaviors`); version-1 scores were converted in 0.2.0.
