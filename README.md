# WildLens — Vertical Slice (Milestone 1)

A first-person nature-photography prototype: walk a trail loop through a reserve, observe five species
behaving naturally, photograph them, and build a field journal.

## Install & run

1. Copy `Assets/WildLens/` into your Unity project's `Assets/` folder (Unity 2021.3 LTS or newer, incl. Unity 6;
   Built-in, URP or HDRP; legacy Input Manager or Input System).
2. Open your terrain scene.
3. Menu **WildLens ▸ Vertical Slice Builder** → **Build Slice In Open Scene**
   (or **WildLens ▸ Build Vertical Slice In Open Scene** for defaults).
4. Save the scene and press **Play**. Click the Game view to capture the mouse.

The builder puts everything under one root object, **`WildLens Vertical Slice`**, so you can rebuild or remove
it at any time. It does **not** modify your Terrain's data. If the scene has no Terrain, it creates a fallback.
It also disables other enabled scene cameras in favour of the player camera (this is undoable, and you can
turn it off in the settings).

## Controls

| Input | Action |
|---|---|
| WASD / Shift | Walk / sprint |
| Mouse | Look |
| Hold RMB | Raise camera (viewfinder, slower careful walk) |
| Scroll (camera raised) | Zoom (about 1.4× to 7.5×) |
| LMB or F | Take photo |
| J or Tab | Field journal (pauses the world) · W/S to browse |
| H | Toggle control hints |
| Esc | Release mouse / close journal |

Moving slowly or standing still lets animals come closer. Sprinting spooks them from much farther away.

## Architecture

```
Assets/WildLens/
  Scripts/  (WildLens.Runtime.asmdef)
    Core/          WildLensGame (composition root, UI pause state), GameInput (input backend abstraction),
                   PlayerPresence (what wildlife can perceive), TerrainUtil, MaterialLibrary, PrimitiveMeshes, SceneQuery
    Data/          SpeciesDefinition (ScriptableObject), SpeciesDatabase, SpeciesDefaults, AnimalActivity
    Environment/   TrailPath (loop centre-line, location labels), EnvironmentFeature (tags trees/rocks for wildlife)
    Player/        FirstPersonController, PlayerCameraRig (FOV zoom, head-bob, raised mode)
    Wildlife/      AnimalAgent (base brain) → GroundAnimal, FlyingAnimal
                   WildlifeWorld (spatial queries: trees, perches), WildlifeSpawner,
                   PlaceholderAnimalBuilder, AnimalRig, ProceduralAnimalAnimator, AnimalAnimatorBridge
    Photography/   PhotoCamera (shutter → capture → journal), PhotoAnalyzer (identify + score), Photographable, PhotoStorage
    Journal/       JournalService (plain C#, JSON persistence), JournalData (save model)
    UI/            UIFactory, HudController, JournalUI   (built from code, no prefabs, no EventSystem)
  Editor/   (WildLens.Editor.asmdef)
    WildLensBuilderWindow, WildLensSliceBuilder, TrailBuilder, VegetationBuilder, PropBuilder,
    ProceduralMeshes, GeneratedAssets, SliceBuildSettings
  Data/       (generated) SpeciesDatabase.asset, Species/*.asset, SliceBuildSettings.asset
  Generated/  (generated) placeholder meshes, materials, textures
```

How the systems depend on each other: wildlife reads `PlayerPresence` rather than depending on the player.
Photography reads `Photographable` and writes to `JournalService`. The UI listens to events
(`PhotoCamera.PhotoTaken`, `JournalService.Changed`).

### Adding or tuning a species
- Tune behaviour, senses, population and habitat on the assets in `Data/Species/`. Rebuilds don't overwrite them.
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
