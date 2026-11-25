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
package and add the URL again.

The builder puts everything under one root object, **`WildLens Vertical Slice`**, so you can rebuild or remove
it at any time. It does **not** modify your Terrain's data. If the scene has no Terrain, it creates a fallback.
It also disables other enabled scene cameras in favour of the player camera (this is undoable, and you can
turn it off in the settings). Files you can edit (species tuning, builder settings, generated meshes and
materials) are written to your project's `Assets/WildLens/` folder, because the package itself is read-only.

## Controls

| Input | Action |
|---|---|
| WASD / Shift | Walk / sprint |
| Mouse | Look |
| **Hold RMB** | Camera mode: raise the camera (slower, steadier, viewfinder) |
| Scroll (in camera mode) | Zoom 1× / 2× / 4× / 8× |
| LMB or F (in camera mode) | Take photo |
| J or Tab | Field journal (pauses the world) · W/S to browse |
| H | Toggle control hints |
| Esc | Release mouse / close journal |

**Approaching wildlife:** animals notice you, then stop and watch you (Alert), then flee. Walking makes noise,
creeping in camera mode is quieter, and standing still is quietest; trees and bushes hide you. When an animal
stops and stares, freeze and let it settle. Photos are graded **Poor / Good / Excellent** on how visible, large,
centred and close the subject is. See [`Documentation~/Systems.md`](Documentation~/Systems.md) for how every
system works and where to tune it.

## Architecture

```
(package root)
  package.json
  Scripts/  (WildLens.Runtime.asmdef)
    CameraSystem/  CameraModeController (RMB camera mode), ZoomLens (FOV + zoom levels), CameraAudio
    Core/          WildLensGame (composition root, UI pause state), GameInput (input backend abstraction),
                   PlayerPresence (what wildlife can perceive), TerrainUtil, MaterialLibrary, PrimitiveMeshes, SceneQuery
    Data/          SpeciesDefinition (ScriptableObject), SpeciesDatabase, SpeciesDefaults, AnimalActivity
    Environment/   TrailPath (loop centre-line, location labels), EnvironmentFeature (tags trees/rocks for wildlife)
    Player/        FirstPersonController, PlayerCameraRig (head-bob, raise dip, handheld sway)
    Wildlife/      AnimalAgent (base brain) → GroundAnimal, FlyingAnimal
                   Awareness/WildlifeAwareness (Calm → Aware → Alert → Fleeing),
                   WildlifeWorld (spatial queries: trees, perches), WildlifeSpawner,
                   PlaceholderAnimalBuilder, AnimalRig, ProceduralAnimalAnimator, AnimalAnimatorBridge
    Photography/   PhotoCamera (shutter → capture → journal), PhotoAnalyzer (detection),
                   PhotoQualityEvaluator (Poor/Good/Excellent), Photographable, PhotoStorage
    Journal/       JournalService (plain C#, JSON persistence), JournalData (save model)
    UI/            HudController → ViewfinderView, ShutterEffectView, PhotoPreviewView; JournalUI; UIFactory
                   (built from code, no prefabs, no EventSystem)
  Editor/   (WildLens.Editor.asmdef)
    WildLensBuilderWindow, WildLensSliceBuilder, SpeciesAssetMigrator, TrailBuilder, VegetationBuilder, PropBuilder,
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
