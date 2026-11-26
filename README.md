# WildLens

A first-person nature-photography prototype: walk a trail loop through a reserve, observe five species
behaving naturally, photograph them, and build a field journal.

## Install & run

WildLens is a Unity package installed straight from GitHub (Unity 2021.3 LTS or newer, including Unity 6;
Built-in, URP or HDRP; legacy Input Manager or Input System).

1. Install [Git](https://git-scm.com/downloads) if it isn't already installed, then restart Unity Hub. Unity uses
   it to download packages.
2. In Unity, open **Window ▸ Package Manager**, click **+ ▸ Add package from git URL…** and enter
   `https://github.com/revDWD/WildLens.git`
3. Open a scene (ideally one with a Terrain) and choose **WildLens ▸ Build Vertical Slice In Open Scene**.
4. Save the scene and press **Play**. Click the Game view to capture the mouse.

**Updating:** in Package Manager, select WildLens and click **Update**. If no Update button appears, remove the
package and add the URL again. Updates that change the environment (0.4.0, 0.5.0, 0.6.0) need a **rebuild** of the
slice to take effect: **WildLens ▸ Vertical Slice Builder ▸ Rebuild Slice In Open Scene**. Since 0.6.0 an out-of-date
scene says so: a notice when you press Play, a warning in the builder window, and a line in the F3 overlay. Run **WildLens ▸ Capture Comparison Shots** before and after to compare.

The builder puts everything under one root object, **`WildLens Vertical Slice`**, so you can rebuild or remove
it at any time. Painting the ground and shaping the reserve (a pond basin, an overlook hill) change your Terrain's
data, so it is **backed up first** to `Assets/WildLens/Generated/Backup`; rebuilding gives the same shape rather than
digging deeper. If the scene has no Terrain, it creates a 900 m fallback with room for the reserve.
It also disables other enabled scene cameras in favour of the player camera (this is undoable, and you can
turn it off in the settings). Files you can edit (species tuning, builder settings, generated meshes and
materials) are written to your project's `Assets/WildLens/` folder, because the package itself is read-only.

## Controls

| Input | Action |
|---|---|
| WASD / Shift | Walk / sprint |
| **C** | Crouch (toggle; Left Ctrl hold also works in builds) |
| Mouse | Look |
| **Hold RMB** | Camera mode: raise the camera (slower, steadier, viewfinder) |
| Scroll, or E / Q | Zoom 1× / 2× / 4× / 8× / 16×. Zooming while lowered raises the camera; zoom out past 1× to lower it |
| LMB or F (in camera mode) | Take photo |
| J or Tab | Field journal (pauses the world) · W/S to browse |
| **R** | Ride the trail bike (next to it) · park and step off (while riding) |
| W / S · A / D (riding) | Throttle / brake (and slow reverse) · steer |
| H | Toggle control hints |
| F3 / F4 | Camera diagnostics overlay / zoom self-test |
| Esc | Release mouse / close journal |

**Finding and approaching wildlife:** deer graze in flower meadows, rabbits and foxes stay near brush piles,
squirrels live around big old trees, and hawks perch on dead snags at meadow edges. Raise the camera and hold an
animal in the viewfinder to **identify** it (no photo needed). Then **crouch** and creep closer. When it stops and
stares, freeze until it relaxes. The viewfinder brackets turn green (Good) or gold (Excellent) when the shot is
right.

**Willow Creek (0.8)** runs from the ridge to the pond; cross it on footbridges, listen for it, and look for deer
drinking where it runs through open ground. Riding into a **pull-off** the HUD offers to park; the walk from there
keeps your engine out of the animals' earshot. In the **Meadow Blind** the HUD shows *HIDDEN*: sit at the window.

**The reserve (0.7):** the trailhead board's map shows four destinations beyond the Meadow Loop: **Ridge Overlook**
(hawks, the widest view), **Cattail Pond** (deer drink at the reed edge), **Old-Growth Grove** (squirrels, a fox) and
the **Wildflower Meadow Blind** (step inside the hide and wildlife can't see you). Fingerposts mark where each reserve
track leaves the loop. The trail bike at the trailhead gets you there; its engine carries, so park short and walk
in. See [`Documentation~/ReserveExpansion.md`](Documentation~/ReserveExpansion.md) for the plan beyond this.

See [`Documentation~/Systems.md`](Documentation~/Systems.md) for how every system works and where to
tune it.

## Architecture

```
(package root)
  package.json
  Scripts/  (WildLens.Runtime.asmdef)
    CameraSystem/  CameraModeController (RMB camera mode), ZoomLens (FOV + zoom levels), CameraAudio
    Observation/   ObservationTracker (identify species by watching)
    Core/          WildLensGame (composition root, UI pause state), GameInput (input backend abstraction),
                   PlayerPresence (what wildlife can perceive), TerrainUtil, MaterialLibrary, PrimitiveMeshes, SceneQuery
    Data/          SpeciesDefinition (ScriptableObject), SpeciesDatabase, SpeciesDefaults, AnimalActivity
    Environment/   TrailPath (loop centre-line, location labels), EnvironmentFeature (tags trees/rocks for wildlife),
                   HabitatHotspot (meadow / brush pile / mature tree / perch)
    Player/        FirstPersonController, PlayerCameraRig (head-bob, raise dip, handheld sway)
    Wildlife/      AnimalAgent (base brain) → GroundAnimal, FlyingAnimal
                   Awareness/WildlifeAwareness (Calm → Aware → Alert → Fleeing),
                   WildlifeWorld (spatial queries: trees, perches), WildlifeSpawner,
                   PlaceholderAnimalBuilder, AnimalRig, ProceduralAnimalAnimator, AnimalAnimatorBridge
    Photography/   PhotoCamera (shutter → capture → journal), PhotoAnalyzer (detection),
                   PhotoQualityEvaluator (Poor/Good/Excellent), FramingAssistant (subject tracking),
                   Photographable, PhotoStorage
    Journal/       JournalService (plain C#, JSON persistence), JournalData (save model)
    UI/            HudController → ViewfinderView, ShutterEffectView, PhotoPreviewView, NoticeView; JournalUI; UIFactory
                   (built from code, no prefabs, no EventSystem)
  Editor/   (WildLens.Editor.asmdef)
    WildLensBuilderWindow, WildLensSliceBuilder, SpeciesAssetMigrator, HabitatHotspotBuilder, TrailBuilder, VegetationBuilder, PropBuilder,
    ProceduralMeshes, GeneratedAssets, SliceBuildSettings

Written into your project (editable):
  Assets/WildLens/Data/       SpeciesDatabase.asset, Species/*.asset, SliceBuildSettings.asset
  Assets/WildLens/Generated/  placeholder meshes, materials, textures
```

How the systems depend on each other: `WildlifeAwareness` reads `PlayerPresence` (not the player itself) and brains
react to its events. Photography reads `Photographable` and `CameraModeController` and writes to `JournalService`.
The UI only listens to events (`PhotoCamera`, `ZoomLens`, `JournalService`). Full detail is in
`Documentation~/Systems.md`.

### Adding or tuning a species
- Tune behaviour, senses, population and habitat on the assets in `Assets/WildLens/Data/Species/`. Rebuilds don't overwrite them.
- New species: **Create ▸ WildLens ▸ Species Definition**, then add it to `SpeciesDatabase`. It appears in the
  journal automatically.
- Real art: assign `prefab` on the definition. Add an `AnimalRig` to get procedural motion, or add an Animator
  with `Speed` (float), `Activity` (int) and `GaitPhase` (float) parameters. `AnimalAnimatorBridge` drives them.

### Using your own scenery assets
In the builder settings, assign **Tree / Bush / Rock prefabs**. If there are no tree prefabs, the builder uses
the terrain's own tree prototypes, and only then falls back to placeholders. Add `EnvironmentFeature` to any
prefab so squirrels can climb it and hawks can perch on it.

## Save data
- Journal: `Application.persistentDataPath/WildLens/journal.json`
- Photos (JPG): `Application.persistentDataPath/WildLens/Photos/`
- Reset: **WildLens ▸ Reset Journal Save Data**, or the **Reset Journal** context menu on `WildLensGame` in Play mode.
