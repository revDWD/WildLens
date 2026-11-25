# WildLens systems guide

How the gameplay systems fit together, and where to tune them. Values live on components (Inspector) or on
the species assets in `Assets/WildLens/Data/Species/`.

```
Input ─► CameraModeController ─► ZoomLens (FOV)        ─► PlayerCameraRig (bob, raise dip, handheld sway)
               │                    │
               ▼                    ▼
        FirstPersonController   HudController ─► ViewfinderView / ShutterEffectView / PhotoPreviewView
               │                    ▲
               ▼                    │ events
        PlayerPresence ──► WildlifeAwareness ──► GroundAnimal / FlyingAnimal ──► ProceduralAnimalAnimator
                                                         ▲
        PhotoCamera ──► PhotoAnalyzer ──► PhotoQualityEvaluator ──► JournalService
            └──── shutter sound ──► WildlifeAwareness.EmitSound
```

## Camera Mode — `Scripts/CameraSystem/`

| Component | Responsibility |
|---|---|
| `CameraModeController` | Hold **RMB** to enter, release to exit. Exposes `IsActive`, an eased `Blend` (0 lowered → 1 at the eye, ~0.22 s), `IsReady` (raised enough to shoot), `MovementSpeedMultiplier` (0.45) and `LookSensitivityScale` (0.8, then scaled by zoom). Raises `Entered` / `Exited`. |
| `ZoomLens` | The only thing that writes the camera's FOV. Walking 65°; camera mode 1× = 50°. Scroll steps through `zoomLevels` (1×, 2×, 4×, 8×), smoothed in log space so each step feels equally quick. The level is remembered between raises. Raises `LevelChanged`. |
| `CameraAudio` | Shutter (on the press, so it feels instant), zoom detent tick, raise rustle. Clips are synthesised at start; assign authored clips in the Inspector to replace them. |
| `PlayerCameraRig` (Player) | Physical motion only: head-bob (suppressed in camera mode), a small spring dip when raising, and subtle handheld sway that grows with zoom and settles after ~1.5 s of standing still. Adds any missing camera components at start-up (keeps older scenes working). |

Photos can only be taken in camera mode. That's deliberate: framing is the action, the shutter confirms it.

## Photography — `Scripts/Photography/`

1. **`PhotoCamera`**: on LMB/F while `IsReady`, it analyses the frame, raises `CaptureStarting` (sound plays, HUD hides),
   grabs the screen at end of frame, raises `CaptureFinished` (shutter blackout), saves the JPG, registers it in
   the journal and raises `PhotoTaken` (preview card). The shutter is audible to animals within 12 m.
   While raised it also runs a 10 Hz focus check (`SubjectInFocus`) that tints the viewfinder brackets green.
   The check says nothing about species.
2. **`PhotoAnalyzer`**: detection. A subject counts if it is in front of the camera, at least 30% of its sample points
   are in frame and unobstructed (trees, bushes and rocks occlude; the player never does), and it covers at least 2% of
   its species' ideal frame size. Produces `SubjectMeasurements`.
3. **`PhotoQualityEvaluator`**: classification, with explicit rules (first failing factor becomes the hint):

   | Class | Visibility | Size vs ideal | Centre offset | Distance vs clear distance |
   |---|---|---|---|---|
   | Excellent | ≥ 85% | ≥ 55% | ≤ 0.22 | ≤ 1.0× |
   | Good | ≥ 50% | ≥ 22% | ≤ 0.45 | ≤ 1.8× |
   | Poor | anything else identified | | | |

   *Size* comes from zoom and distance; *distance* is about clarity: beyond a species' `clearPhotoDistance` haze
   softens detail even at 8×, so getting physically closer still matters. There is no score shown to the player;
   an internal rating only breaks ties when choosing a species' best photo.

## Wildlife Awareness — `Scripts/Wildlife/Awareness/WildlifeAwareness.cs`

Each animal accumulates **suspicion** (0–1) while the player is within its **awareness radius**:

`gain/s = suspicionGain × playerNoise × lineOfSight × shelter × memory × (0.25 + 1.75 × proximity²)`

* **playerNoise** (`PlayerPresence.Noise`): still 0.15 · creeping in camera mode 0.3–0.5 · walking 0.5–1 · sprinting 2.2
* **lineOfSight**: 1, or 0.35 when trees/bushes/rocks block the view between animal and player (checked ~3×/s)
* **shelter**: 0.4 while on a trunk or in the air
* **memory**: ×1.6 for 30 s after the animal last fled
* **proximity**: 0 at the awareness radius → 1 at the flee radius
* Standing still lets suspicion **decay**, so you can wait an animal out.

| State | Enter when | Animal does |
|---|---|---|
| Calm | default | species behaviours |
| Aware | suspicion ≥ 0.25 inside awareness radius | glances at you every few seconds, won't lie down, drifts away from you |
| Alert | suspicion ≥ 0.6 inside **alert radius** | stops and stares (rabbits/squirrels sit up; deer flag the tail; perched hawks turn to face you). Suspicion now grows at 1/`alertPatience` |
| Fleeing | suspicion reaches 1 (only possible while Alert), approaching for longer than `alertPatience` while Alert, moving inside the **flee radius**, sprinting inside `alertRadius × startleSensitivity`, or a shutter too close | runs (rabbits zig-zag), squirrels climb, hawks take flight away from you |

Escalation always passes through Alert, except the flee-radius and sprint-startle shortcuts. That gives the
player a readable warning and a chance to freeze. Brains call `EndFlee()` when safe and the animal returns as
Aware with heightened memory. Select an animal in the Scene view during Play to see its three radii.

Tuning (species assets → *Awareness*): `awarenessRadius`, `alertRadius`, `fleeRadius`, `suspicionGain`,
`suspicionDecay`, `alertPatience`, `startleSensitivity`.

## Wildlife AI — `Scripts/Wildlife/`

Brains react to awareness events; they never sense the player directly.

* **`GroundAnimal`**: modes Moving (Walking / Hopping, or an occasional Running leg via `runLegChance`), Behaving
  (weighted species behaviours), Watching (aware glance / alert stare), Fleeing, Climbing.
  * Rabbit: Grazing, Sitting, Grooming, Sniffing, LookingAround · moves by Hopping · sits up when Alert
  * Deer: Grazing, LookingAround, Grooming, Resting · Walking, occasional Running bounds · long Alert stare
* **`FlyingAnimal`**: Flying (circling thermals / gliding to new ones) → Landing (slow, flare, braking flaps) →
  Perched (scans; watches you when Aware/Alert) → TakingOff (hard flaps; away from you when disturbed) → Flying.
* **`ProceduralAnimalAnimator`**: poses per activity (sit-up, grazing reach, gaze tracking, landing flare, wing fold).

## UI — `Scripts/UI/`

`HudController` composes `ViewfinderView` (vignette, frame, thirds, focus brackets, zoom row),
`ShutterEffectView` (0.17 s curtain blackout after capture) and `PhotoPreviewView` (print-style card with
species, quality class and a short hint for anything below Excellent; fades after ~4 s). Walking HUD is just a
dot, counters and hints, which dim while the camera is raised.

## Data migrations

* Species assets: `SpeciesDefinition.dataVersion`. `SpeciesAssetMigrator` (editor) upgrades the built-in species to
  the current tuning after script reload; also **WildLens ▸ Upgrade Species Assets**.
* Journal save: `JournalSaveData.version` 2. Version-1 scores are converted to quality classes on load.
