# AGENTS.md: WildLens

Read this first if you are working on WildLens, whether you're a person or an AI agent. It explains what the game
is for, why it is built the way it is, and what was learned getting here. Details of each system are in
`Documentation~/Systems.md`; art sourcing and licensing are in `Documentation~/VisualRoadmap.md`; the plan for the
reserve is in `Documentation~/ReserveExpansion.md`.

---

## 1. Vision

WildLens is a quiet, first-person **wildlife photography** game set in a nature reserve. You walk, or ride a small
trail bike, through meadow, woodland, ridge and wetland; notice animals; approach without spooking them; and take a
photograph worth keeping.

**Direction (from 0.7):** WildLens is growing from a single trail loop into a **wildlife reserve**: somewhere
people would happily spend an afternoon exploring. The heart stays the same: observation, photography, exploration,
atmosphere. Bigger and more interesting places to visit, better exploration, environmental storytelling, better
observation opportunities. Not a bigger empty map: every place has a reason to go there.

**Direction (from 0.8): a wildlife reserve simulation, not a feature-heavy game.** The world itself is the main
feature. The question to ask is never "what system is missing?" but **"what would make this place feel real?"**
Ecological structure (dense, open, sparse), water that connects places, habitats with distinct identities, quiet
evidence of stewardship, and wildlife found where it would really be. Someone should want to spend an afternoon here:
walking trails, riding the trail bike, stopping at overlooks, watching wildlife, taking photographs.

Inspirations:
- *Pokémon Snap*: the photograph is the reward, and grading it is the game.
- *Firewatch*: a place that feels real, painterly light, and a calm pace.
- **Real nature observation**: patience, reading animal behaviour, and the moment an animal accepts you are there.

Target species: white-tailed deer, red fox, red-tailed hawk, eastern cottontail and eastern grey squirrel.

## 2. Design philosophy

**What makes it fun**
1. **The approach.** Animals notice you (Calm → Aware → Alert → Fleeing). You freeze, they settle, you creep
   closer. The tension of that loop is the core of the game.
2. **The camera feels like a camera.** You raise it, zoom in steps, hear autofocus hunt and lock, see a shutter
   blackout, and the print develops. The physical ritual matters as much as the grade.
3. **The photo is judged fairly and legibly.** Frame fill, focus, behaviour and composition are scored, and the
   viewfinder brackets show the grade *before* you press the shutter.
4. **The place is worth looking at.** You spend the whole game looking through a lens; every visual weakness is
   magnified 16×.
5. **The place is worth travelling through** (0.7). Playtesters asked to go farther. Destinations each have a
   purpose (a view, water, big trees, concealment) and the wildlife that goes with it, so travel is rewarded.

**The exploration loop:** ride → spot something → stop → park (R) → raise the camera → explore on foot → back to
the bike (R). It must feel seamless; anything that adds friction to it is a bug.

**The trail bike: design rules.** Inspired by small-displacement trail bikes and reserve service vehicles.
It is quiet-ish, friendly and practical, never racing, aggressive or arcade. It exists only to make a larger
reserve explorable. So: modest top speed (≈40 km/h on tracks, slower across country); gentle acceleration; no
stunts, damage, fuel, upgrades or progression. Its engine is loud to wildlife on purpose, so the last stretch to
an animal is always on foot. You can raise the camera stopped in the saddle, but not while moving.

**Priorities, in order:** feel of the encounter → camera feel → visual credibility → content breadth.

**Deliberate non-priorities.** These were left out on purpose; don't add them unless asked:
- **No progression, XP, currency, missions, quests, achievements, inventory or crafting.** WildLens is about
  attention, not accumulation. Each meta-loop added would pull the player's eyes from the animal to a counter.
  The journal (species seen, best photo per species) is the only record, and it's enough.
- **No combat, hunting, survival or failure states.** A fleeing animal is the only "fail", and it comes back.
- **No resource collection, vehicle progression or skill trees.** The reserve stays peaceful; the reward for
  exploring is what you see and photograph.
- **No new systems during presentation milestones.** 0.4–0.6 were deliberately spent on making existing systems
  look and feel right, because playtests showed the loop already worked and the look didn't.

**Working rules that have paid off**
- **Approachable realism.** Behaviour is grounded in real animals but tuned so patience *always* works: standing
  still always lets animals settle.
- **Prove it, don't assume it.** When something "should" work and the player says it doesn't, add a diagnostic
  that shows the truth (F3 overlay, F4 self-test, stale-scene notice) before changing anything else.
- **Screenshots are the source of truth.** Critique what's on screen, not what the code intends.
- **Licence safety is absolute.** Only CC0 art is committed (`Art~/`, see `Art~/CREDITS.md`). Store assets are
  imported locally by the user, never committed to this public repo.
- **Rebuild, don't hand-edit.** The scene is generated by the builder; improvements land by rebuilding.
- **Destinations, not distance.** Never grow the world without a reason to visit the new part.
- **Ecology over geography.** Variation should come from habitat (dense woodland, open grassland, quiet groves,
  riparian corridor, wetland edge), not just from distance. Avoid uniform density anywhere.
- **Encounters from placement, not mechanics.** Deer at water, deer crossing a track, rabbits on brushy edges,
  hawks on the ridge snag, squirrels in mature forest: achieved with hotspots and residences, no new AI.
- **Stewardship, lightly.** Signs, bridges, benches, marker posts, a ranger's log: enough to say "someone looks
  after this place", never enough to feel like a theme park.
- **The trailhead board is the dashboard.** Players (and testers) see the version, build, features and recent
  changes in-world. Keep `WildLensVersion` current every release; it must never feel like a debug screen.

## 3. Packaging and workflow

- A Unity **UPM git package** (`package.json`, name `com.revdwd.wildlens`), installed by Package Manager git URL
  `https://github.com/revDWD/WildLens.git`. Unity 2021.3+, with Unity 6 tested. URP is supported (`WILDLENS_URP`);
  the Built-in pipeline is the fallback. Both the Input System (`WILDLENS_INPUT_SYSTEM`, `_1_9`) and legacy input work.
- Assemblies: `Scripts/WildLens.Runtime.asmdef` and `Editor/WildLens.Editor.asmdef` (version defines in each).
- Every file outside `~` folders needs a committed `.meta`. `Art~` and `Documentation~` are ignored by Unity.
- **The player's flow:** WildLens ▸ Vertical Slice Builder ▸ (Re)build Slice In Open Scene, then Play.
- **When the builder's output changes, bump `WildLensVersion.Environment`** (`Scripts/Core/WildLensVersion.cs`)
  so old scenes are flagged as stale. Also bump `WildLensVersion.Package` and `package.json` each release.
- **When `Art~` changes, bump `ArtLibrary.ImportVersion`** (`Editor/ArtLibrary.cs`) so the art is re-copied.
- **Each release, update `WildLensVersion.Features`, `RecentChanges`, `ReleaseName` and `ReleaseDate`.** The
  trailhead board shows them.
- Species assets and journal saves carry data versions and are migrated automatically
  (`Editor/SpeciesAssetMigrator.cs`, `Scripts/Journal/JournalData.cs`). Enum values are serialized as integers:
  append to them, never reorder.

## 4. Architecture (file map)

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
                 (Poor/Good/Excellent), FramingAssistant (live brackets), Photographable, PhotoStorage
  Observation/   ObservationTracker (hold in view → identified → journal)
  Wildlife/      AnimalAgent → GroundAnimal / FlyingAnimal (behaviour states), Awareness/WildlifeAwareness
                 (Calm/Aware/Alert/Fleeing), WildlifeSpawner, WildlifeWorld (trees, perches, hotspots),
                 PlaceholderAnimalBuilder + AnimalRig + ProceduralAnimalAnimator (placeholder bodies),
                 AnimalAnimatorBridge (drives Mecanim on authored prefabs)
  Environment/   HabitatHotspot, ScenicViewpoint, EnvironmentFeature, TrailPath, FoliageSway, WildLensWind,
                 SliceInfo (build version marker), ReserveDestination (named place, residents, arrival note),
                 ReserveTrack (track polyline), ObservationBlind (concealment), ReserveInfoBoard (trailhead board + map),
                 ReserveCreek (creek line), ReserveParking (bike pull-offs), WildlifeResidence (species at a place),
                 RideableSurface (bridge decks the bike rides), AmbientSound (procedural wind/brook)
  Journal/       JournalService, JournalData (save + migration)
  UI/            HudController (owns views), ViewfinderView, PhotoPreviewView (prints, keeper), ShutterEffectView,
                 NoticeView, JournalUI, UIFactory
  Data/          SpeciesDefinition (all per-species tuning), SpeciesDefaults, SpeciesDatabase, AnimalActivity
  Debugging/     CameraDiagnostics (F3 overlay, F4 zoom self-test)
Editor/
  WildLensSliceBuilder (pipeline), WildLensBuilderWindow, SliceBuildSettings, TrailBuilder, PropBuilder,
  VegetationBuilder (+ partials TerrainPainter, HabitatHotspotBuilder, ViewpointBuilder, ReserveBuilder,
  EcosystemBuilder: creek, pull-offs, storytelling, residences), FoliageCards,
  ArtLibrary (CC0 art import, prefabs, sky), GeneratedAssets, ProceduralMeshes, ComparisonCapture,
  SpeciesAssetMigrator, AnimalModelSetup (bought model → species prefab)
Art~/            CC0 Poly Haven skies, terrain, surfaces, models, foliage cards (CREDITS.md)
Documentation~/  Systems.md (systems and tuning), VisualRoadmap.md (assets, licensing, weaknesses),
                 ReserveExpansion.md (reserve design and next steps)
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
  tracks) → trees → rocks/logs → bushes → hotspots → viewpoints/compositions → **destinations** → undergrowth →
  terrain paint and grass → wind → lighting (HDRI sky with sun alignment) → render quality → URP post → player.
- **Reserve:** destinations chosen from the terrain (ridge = prominence, pond = basin, grove = forest density,
  blind = open flat ground), tracks by A*, residents spawned by `WildlifeSpawner.SpawnResidents`. Everything near a
  path uses "distance to any path" (loop or track) for clearance and painting.
- **Ecosystem (0.8):** plan order is sites → terrain shaping (pond, ridge) → arrivals → **creek** (route downhill,
  carve, water) → blind pad → tracks (cross the creek squarely) → **pull-offs** → scenery (density zones; riparian
  bias; creek and keep-out exclusion) → destinations → **ecosystem build** (bridges, creek life, markers,
  residences) → painting (creek bed/banks; asters; no grass in keep-outs).
- **Riding:** `TrailBike` disables the walking controller and parents the player to the seat; it reports presence
  (with `EngineNoise`) and blocks camera raise while moving; parking puts the player back on foot beside it.

## 5. History and lessons

**0.1.0 Vertical slice.** A trail loop builder, five species, photography and a journal. *Lesson:* the loop
was fun immediately; everything after has been about trust and presentation. Installing as a package (not copying
files) mattered to the user from day one, so do things the proper way.

**0.2.0 Camera & awareness.** A camera mode with raise and zoom steps, shutter feedback, the Calm/Aware/Alert/Fleeing
awareness model, behaviour states and quality classes in place of numeric scores. *Why:* the camera had to feel
like an instrument, and animals had to react to *you*, not to a timer. Numbers were replaced by Poor/Good/Excellent
because a grade you can read beats a score you can't interpret.

**0.3.0 Encounters.** Grading switched to linear frame fill with clear thresholds; crouch; identification by
observation; framing brackets; a 16× step; habitat hotspots; awareness retuned. *Why hotspots:* animals wandering
randomly over a big map meant long empty walks. Hotspots give the player readable places to look, which is also
how real wildlife watching works. *Why awareness tuning:* the first tuning was realistic and miserable. Animals fled
too far and stayed wary. "Approachable realism" (patience always works) made encounters enjoyable.

**0.4.0 Visual quality & first zoom fix.** Zoom did nothing on Unity 6: the Input System reports ±1 per notch, and
the code divided by 120. The fix was to use the wheel direction only. Also added: painted terrain, terrain grass,
lighting, post-processing and the F3 diagnostics. *Lesson:* never trust input magnitudes across backends.

**0.5.0 Zoom you can see, plus scanned art.** The player still saw no zoom. The causes were init order (ZoomLens
cached a null controller in scenes upgraded at runtime) and "preselect while lowered" by design (the UI changed but
the view didn't). The fix: zoom input raises the camera, references resolve lazily, and the FOV is applied late and
re-asserted before rendering. The F4 self-test now *measures* rendered magnification and saves screenshots. *Why
redesigned:* a feature the player can't perceive is broken, whatever the code says. *Then* CC0 scanned art: HDRI
skies with sun alignment, photographed terrain, bark, rocks, ferns and deadwood.

**0.6.0 Presentation pass.** The playtest showed zoom, photography and encounters working, and art as
the weakness. Screenshot critique found the player's scene had **not been rebuilt** since 0.3, so two milestones
of art were invisible. Hence the stale-scene detector. Then: leaf-card trees, grass-coloured terrain, a deer
silhouette fix, photo polish and the animal-model setup tool. Researched free wildlife art: none is both licence-safe
and realistic. *Why visuals became the focus:* once the loop worked, every playtest comment was about how it looked,
and a photography game is judged through a 16× lens.

**0.7.0 The Reserve.** After the rebuild, playtesters found the meadows believable and the world
pleasant to walk through, and asked to **go farther**. So: four destinations with purpose and residents, routed
reserve tracks, a gentle trail bike to cover distance (loud to wildlife, so you still walk the last stretch), and the
trailhead kiosk board as an in-world dashboard. The "black slab" in a screenshot turned out to be the solid
underside of a conifer tier hanging at head height; tiers are now open and higher. *Lesson:* when the world is good
enough that exploring is its own reward, add places, not square metres. *Lesson:* ask what an odd shape in a
screenshot *is* before theorising; usually it's geometry seen from an angle nobody designed for.
*0.7.1:* the first reserve playtest couldn't get into the Meadow Blind: its doorway (0.7 m) was narrower than the
player's capsule (0.7 m + skin). *Lesson:* size every walkable opening against the CharacterController.

**0.8.0 Ecosystem Pass** (current). The 0.7 playtest confirmed the foundations (zoom, photography, destinations,
kiosk, bike) and said the reserve felt like isolated destinations with too-even density, and the blind didn't work
as a space. So: Willow Creek as the backbone (carved, bridged, alive), density zones, a distinct identity for each
destination, light-touch stewardship, pull-offs that make "ride → stop → observe" natural, residences that put
animals where they'd really be, and a rebuilt blind. *Lessons from 0.7 playtesting:* a destination alone is not a
place; it needs surroundings that lead up to it. A perfect circle (the first pond) reads as man-made instantly.
A "grove" is defined by shade and enclosure, not by three big trees. A space the player must enter needs designing
from the inside: door, floor, sightline, and a message that says what it's for.

**Cross-cutting lessons**
- When the player says something doesn't work, believe them and **build a probe** before theorising.
- Make invisible state visible: stale scenes, ignored inputs and FOV overrides each cost a milestone before they
  were surfaced.
- Primitive placeholders read as toys next to photographic surroundings; mixing fidelity levels is worse than
  either alone.
- Keep package size in mind: `Art~` ships to every user. Use 2k skies and ground, and drop unused files.

## 6. Current state (0.8.0)

**Strengths:** the encounter loop; camera feel; legible grading; real skies and light; believable meadows; a reserve
of distinct places joined by tracks and a creek; a relaxed trail bike with natural places to stop; the trailhead
board; tooling (F3/F4, comparison capture, stale-scene detection, model setup).

**Weaknesses and known limitations (honest):**
1. **Animals are primitive placeholders.** Still the biggest gap; the Animal Model Setup tool is ready.
2. **Trees read as procedural** (cones dressed in cards; lumpy broadleaf crowns; bare trunks). Now the strongest
   "generated" signal, more so as the world improves around them. Top visual priority after animals.
3. **The reserve depends on the terrain it's built on.** Flat ground yields a raised hill and dug pond; the creek is
   only built when terrain shaping is on. A hand-authored heightmap would fix this.
4. **Creek water is static** (no flow animation or foam); the creek has no ford or cascade yet.
5. Props (kiosk, decks, hide, bridges, signs) are flat-coloured boxes; text is TextMesh.
6. Trail bike: placeholder model, simple kinematic handling, no rider body.
7. Performance at reserve scale untested on low-end hardware; see the Unity 6 GPU Resident Drawer note in
   VisualRoadmap.md.
8. **Unverified in Unity by the author of 0.8**: compiled everywhere, but creek routing/carving, bridges, density
   zones and the new blind need a playtest pass.

**Visual priorities (in order):** believable animals → believable trees → water that moves → textured props →
light (golden hour, shafts under the canopy).

**Top future improvements (in order):**
1. Playtest 0.8: creek course and bridges, blind usability, density feel, pull-off placement.
2. One realistic deer through **WildLens ▸ Animal Model Setup**; then fox and hawk.
3. Trees: rebuilt procedural conifers (branch whorls) and broadleaf crowns, or a game-ready pack with LODs.
4. Time of day (dawn mist, golden hour) with animal schedules following it.
5. Creek part two (ford, cascade), wetland boardwalk, forest footpath, ranger cabin (ReserveExpansion.md).

## 7. Checklist for changes

- Compile on Built-in + legacy input, Input System, and Unity 6 + URP + Input System 1.9 configurations.
- New file or folder → commit its `.meta`.
- Builder output changed → bump `WildLensVersion.Environment`; `Art~` changed → bump `ArtLibrary.ImportVersion`
  and update `Art~/CREDITS.md`.
- Release → bump `package.json` and `WildLensVersion.Package`; update `Features`, `RecentChanges`, `ReleaseName`,
  `ReleaseDate` (the trailhead board shows them).
- New gameplay idea? Check it against the non-priorities in section 2 first, then ask "would this make the place
  feel more real?" If not, it probably doesn't belong.
- Anything the player walks into or onto (doors, decks, steps): doorways ≥ 1.0 m wide and ≥ 1.95 m tall, steps
  ≤ 0.4 m (CharacterController radius 0.35, skin 0.05, height 1.8, step offset 0.4).
- Update `CHANGELOG.md`, `Documentation~/Systems.md` (and `VisualRoadmap.md` for art).
- Don't commit store assets, secrets or generated `Assets/` content.
