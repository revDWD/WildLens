# AGENTS.md: WildLens

Read this first if you are working on WildLens, whether you're a person or an AI agent. It explains what the game
is for, why it is built the way it is, and what was learned getting here. Details of each system are in
`Documentation~/Systems.md`; art sourcing and licensing are in `Documentation~/VisualRoadmap.md`; the plan for the
reserve is in `Documentation~/ReserveExpansion.md`; the environment audit, free-asset list and migration plan are in
`Documentation~/EnvironmentMigration.md`.

---

## 0. Start here (a fresh session's first ten minutes)

1. **What this is:** Willow Creek Nature Reserve, a quiet wildlife photography game (v1.4). The *place* is the
   product: a generated reserve (meadow loop, ridge, pond, grove, meadow blind, Willow Creek, Reserve Road, footpaths)
   with a day cycle, a weather forecast and wildlife that keeps hours. Photography (zoom, grading, journal) is the reason to be there.
2. **How it's built:** a Unity UPM package. An editor builder (`WildLens ▸ Vertical Slice Builder`) generates the
   whole reserve into a scene; runtime scripts run the game. The user installs from the git URL, updates, rebuilds.
3. **You probably can't run Unity.** Run `Tools~/CompileCheck/compile-check.sh` after every change (five
   configurations, no Unity needed; it needs the .NET SDK 6+, e.g. `apt-get install dotnet-sdk-8.0`). Then ask the user
   to rebuild and playtest. **Their screenshots are the truth.**
4. **Read next:** §2 (philosophy and non-priorities), §6 (current state), §8–§9 (what to build next / what not to).
   Systems detail: `Documentation~/Systems.md`. Environment and assets: `Documentation~/EnvironmentMigration.md`.
   Practical workflow: `CONTRIBUTING.md`.
5. **The user** is a non-programmer playtester-director: they install via Package Manager, use GitHub Desktop, and
   send screenshots with feedback. Give plain instructions ("Update the package, then Rebuild"), commit and push
   finished work to `main`, and always tell them what to look at next.

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

**Direction (from 0.9): a place with rhythms.** The reserve changes through the day (dawn mist, golden hour, dusk),
and so does its wildlife. Players should come to think *"I want the pond at dawn"*, *"I'll ride to the ridge before
sunset"*, *"deer come to the creek in the evening"*, and those decisions should emerge from the world, not from
quests or markers. **Why a believable place instead of more game systems:** every playtest since 0.6 has rewarded
realism of place (light, habitat, water, signs) and been indifferent to mechanics. A photography game's content *is*
what's in front of the lens: a real-feeling reserve produces endless photographs; another system produces menus.

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
  imported locally by the user, never committed to this public repo, **free ones included** (the Asset Store EULA
  forbids redistribution). WildLens *finds and places* them instead: the Environment Kit (1.2) for scenery, Animal
  Model Setup for animals.
- **Rebuild, don't hand-edit.** The scene is generated by the builder; improvements land by rebuilding.
- **Destinations, not distance.** Never grow the world without a reason to visit the new part.
- **Ecology over geography.** Variation should come from habitat (dense woodland, open grassland, quiet groves,
  riparian corridor, wetland edge), not just from distance. Avoid uniform density anywhere.
- **Encounters from placement, not mechanics.** Deer at water, deer crossing a track, rabbits on brushy edges,
  hawks on the ridge snag, squirrels in mature forest: achieved with hotspots and residences, no new AI.
- **Rhythms from the clock, not from scripts.** Time of day moves light, mist and animals' home ranges; it never
  spawns or despawns animals or triggers events. Patterns must be learnable and consistent: deer at water at dusk
  every evening, not "sometimes".
- **Trail hierarchy.** The Reserve Road is for getting between destinations (riding); footpaths and the loop are for
  being somewhere (walking). Roads stop at pull-offs; you always walk the last stretch. Never make the bike faster:
  fix the routes.
- **Stewardship, lightly.** Signs, bridges, benches, marker posts, a ranger's log: enough to say "someone looks
  after this place", never enough to feel like a theme park.
- **Elevation first** (1.4). Playtests found terrain relief the highest-return environment change: the ridge overlook
  was the strongest place because of height and view, and the flat fallback terrain was the weakest thing in every
  wide shot. Before building anything new, ask what the ground does there: a shelf to look out from, a cut the creek
  drops through, a valley that closes the view. Never flatten a landscape to make placement easy.
- **"Why would a photographer come here?"** Every landmark must answer it with a subject, light or view (the ford:
  moving water and a kingfisher on a snag). Species likewise need a place first (a niche), never a count.
- **A sign is read in two seconds from the trail.** Big title, one short line, a number and a pictogram; paragraphs
  go. And a sign that "exists" isn't done until its lettering is checked on the board (`WildLens ▸ Check Sign
  Lettering`): 1.3 shipped mirrored lettering that the code couldn't see and the first screenshot showed.
- **The trailhead board is the dashboard.** Players (and testers) see the version, build, features and recent
  changes in-world. Keep `WildLensVersion` current every release; it must never feel like a debug screen.

## 3. Packaging and workflow

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
                 AnimalAnimatorBridge (drives Mecanim on authored prefabs), WildlifeSchedule (daily rhythms)
  Environment/   HabitatHotspot, ScenicViewpoint, EnvironmentFeature, TrailPath, FoliageSway, WildLensWind,
                 SliceInfo (build version marker), ReserveDestination (named place, residents, arrival note),
                 ReserveTrack (track polyline), ObservationBlind (concealment), ReserveInfoBoard (trailhead board + map),
                 ReserveCreek (creek line), ReserveParking (bike pull-offs), WildlifeResidence (species at a place),
                 RideableSurface (bridge decks the bike rides), AmbientSound (procedural wind/brook),
                 DayCycle (clock, sun, sky, fog, grading), MistPatch (dawn mist), ScheduleAnchor (water/woodland/ridge),
                 WeatherCycle (four-day forecast, cloud cover, rain, overcast sky), WaterFlow (moving ripples)
  Journal/       JournalService, JournalData (save + migration)
  UI/            HudController (owns views), ViewfinderView, PhotoPreviewView (prints, keeper), ShutterEffectView,
                 NoticeView, JournalUI, UIFactory
  Data/          SpeciesDefinition (all per-species tuning), SpeciesDefaults, SpeciesDatabase, AnimalActivity
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
  SignCheck (probe: lettering measured against boards)
Art~/            CC0 Poly Haven skies, terrain, surfaces, models, foliage cards (CREDITS.md)
Documentation~/  Systems.md (systems and tuning), VisualRoadmap.md (assets, licensing, weaknesses),
                 ReserveExpansion.md (reserve design and next steps), WildlifeExpansion.md (species framework & roadmap),
                 EnvironmentMigration.md (environment audit, free assets, migration plan, before/after)
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

**0.8.0 Ecosystem Pass.** The 0.7 playtest confirmed the foundations (zoom, photography, destinations,
kiosk, bike) and said the reserve felt like isolated destinations with too-even density, and the blind didn't work
as a space. So: Willow Creek as the backbone (carved, bridged, alive), density zones, a distinct identity for each
destination, light-touch stewardship, pull-offs that make "ride → stop → observe" natural, residences that put
animals where they'd really be, and a rebuilt blind. *Lessons from 0.7 playtesting:* a destination alone is not a
place; it needs surroundings that lead up to it. A perfect circle (the first pond) reads as man-made instantly.
A "grove" is defined by shade and enclosure, not by three big trees. A space the player must enter needs designing
from the inside: door, floor, sightline, and a message that says what it's for.

**0.9.0 Time of Day.** 0.8 worked: the reserve became enjoyable to simply be in, Willow Creek tied it
together. The reserve was frozen in one moment, so 0.9 gave it a day: periods with distinct light, dawn mist, and
wildlife that keeps hours by placement. Riding exposed a layout problem (every path was a footpath), solved with a
trail hierarchy (Reserve Road + footpaths) rather than a faster bike. Trees got real structure. The reserve got a name
and one sign standard. *Lessons from 0.8 playtesting:* connective tissue (the creek) did more for "realness" than any
destination; density needs to be *felt* along a route, not just measured; the blind worked once designed from inside;
the grove still read as parkland until the canopy actually closed; a perfectly circular feature always reads as
man-made; signs only feel curated when they share one style.

**1.0.0 Field Season**. A consolidation release, no new systems. The Reserve Road became infrastructure
worth riding (wider, smoothed, graded, with sight lines and posts; the bike gained suspension-like smoothing, not
speed). Every destination got a signature detail that says what it's for (telescope and cairn, canoe, Grandmother
Oak, bird feeder). Woodland edges grade through young trees; pines join the firs. The species framework (generic
bodies, data rhythms, water habitat) makes the ecosystem expandable with data. A committed compile check and a
handoff (this file, CONTRIBUTING.md) make the project continuable. *Lessons from 0.9:* the bike exposed every layout
weakness; fixing infrastructure beat tuning vehicles. Time of day multiplied the value of every existing place
without adding one. Players learn patterns only if they're consistent and stated somewhere in-world (the board's
"Now" line, signs' best times).

**1.1.0 More Life.** Ten more species as data on the 1.0 framework (heron, raccoon, turkey, barred owl, groundhog,
cardinal, blue jay, red-winged blackbird, opossum, coyote), each with residents at a destination; small-bird flight
as species data. *Lesson:* the framework held: fifteen species with no new AI.

**1.2.0 Habitat Kit**. Gameplay had outgrown the environment's look, and the brief was to replace as
much generated scenery as possible with free, high-quality assets. *Why a kit, not an import:* every store asset,
free or not, is barred from this public repo, and the scene is rebuilt from code, so assets have to be found in the
user's project and placed by the builder. *Why by ecological role:* a random list of tree prefabs ignores the forest
field, the creek and the grove; roles keep "ecology over geography". Also: two CC0 scanned rock faces as bedrock
outcrops. The compile check was found broken (its project file had never been committed). *Lessons:* Unity's
best free environments (Book of the Dead, Fontainebleau) are HDRP-only; Unity's free *Terrain URP Demo Scene* is the
realistic free source of game-ready trees. Scanned "rock faces" are open shells: measure which way they face before
placing them. Never change the random sequence for users who don't opt in, or every reserve silently changes.

**1.3.0 Trailhead**. The playtest said the reserve was looking good and was now limited by presentation and
readability: the kiosk read like software release notes, signs needed standing next to, the creek was a blue ribbon,
ferns carpeted sunny banks, and distant broadleaf trees looked like acacias. So: a park-style trailhead board (name,
time, conditions, map, destinations), one sign standard with big titles, one short line and pictograms (`SignKit`),
scanned wood on the woodwork, tea-coloured flowing water, sedge banks, rounder crowns. *Lessons:* a sign is read in
two seconds from several metres away, so it gets a title, a number and an icon, and the paragraph goes. Measure text
with the font's own glyph advances instead of guessing board widths. A mirror-smooth water material reflects the sky's
most saturated colour at exactly the angle players look at a creek. Add visual detail from a separate random source,
or every tree after it moves. Players asked for wildlife already added in 1.1: say what's already there before
building more.

**1.4.0 Changing Skies** (current). The 1.3 playtest liked the reserve a lot (biodiversity, the road, the
trailhead, the ridge) and reported two things: sign lettering missing or unreadable, and that **elevation made the
place**. The lettering was there but mirrored: `SignKit` placed left-aligned text on the reader's right, off the board
(a sign's front faces +Z, so its reader's right is its -X; centred labels before 1.3 hid the convention). Fixed with a
reader-space convention and a probe (`SignCheck`) that measures lettering against boards after every build. The
fallback terrain had 7 m of relief and a 2.6° mean slope, so landforms v2 (prototyped and measured in Python first) gives a
lowland-to-upland reserve with ridges, valleys and shelves, regenerated every build. Weather arrived as a learnable
four-day forecast (overcast sky, rain, wildlife sheltering). The Creek Ford gave the creek its own place and its own
species (kingfisher, sandpiper). *Lessons:* a coordinate convention used in one direction only (centred text) can hide
for releases; probe output beats confidence in code you can't run. Prototype terrain numerically before shipping it.
The noise "forest field" is not the forest: paint and plant from where trees actually stand. Weather must be a
rhythm, not dice, or players can't plan around it. *What worked from the biodiversity expansion:* species justified
by places (pond, grove, riffle), data-only bodies, residents that stay home to make a place reliable.

**Cross-cutting lessons**
- When the player says something doesn't work, believe them and **build a probe** before theorising.
- Make invisible state visible: stale scenes, ignored inputs and FOV overrides each cost a milestone before they
  were surfaced.
- Primitive placeholders read as toys next to photographic surroundings; mixing fidelity levels is worse than
  either alone.
- Keep package size in mind: `Art~` ships to every user. Use 2k skies and ground, and drop unused files.

## 6. Current state (1.4.0)

**Strengths:** the approach-and-photograph loop; camera feel; legible grading; a reserve of distinct, connected,
named places with signature details; a day of changing light and learnable wildlife rhythms; a road for riding and
footpaths for walking; one coherent reserve identity; the trailhead board; a data-driven species framework; tooling
(F3/F4, comparison capture, stale-scene detection, model setup, compile check); a supported path to free
high-quality scenery (Environment Kit) and scanned bedrock outcrops; readable park-style signs and a trailhead board
that reads in seconds, with a probe that checks lettering; scanned wood on the woodwork; flowing creek water; real
elevation (lowland to upland ridges); a learnable weather forecast; the Creek Ford and seventeen species.

**Remaining weaknesses (honest):**
1. **Animals are primitive placeholders.** The biggest gap by far. No free licence-safe realistic models exist; buy
   them and use Animal Model Setup (deer first).
2. **Trees**: the generated trees are structured, but leaf cards are lit like paper, with no real LODs or billboards
   and few silhouettes. The Environment Kit can replace them with Unity's free SpeedTree trees, **but it has never been
   run in Unity**: expect fixes on first use (scale, crown colliders, scanner guesses).
3. **Grass** is drawn procedurally (flat billboards) unless the kit supplies mesh grass; the grass terrain layer is a
   tinted leaf-litter scan (no CC0 lush-grass scan exists).
4. **Props are boxes** (now with scanned wood, but box-shaped: no bevels, no real kiosk or blind models). Sign text is
   TextMesh in the built-in font (bold, large and short since 1.3, but unlit, so it glows slightly at dusk; no
   third-party font is committed because free sign fonts are OFL, not CC0).
5. **Sky**: three HDRIs (morning, golden hour, overcast) re-tinted through the day; no night sky, no moving clouds.
   Rain has no wet surfaces, puddles or splashes, and falls through roofs.
6. **Water** has ripples and flow since 1.3 and a ford since 1.4, but no refraction, foam or shoreline blending; no cascade.
7. **The reserve depends on the terrain it's built on.** WildLens's own terrain now has real landforms (1.4), but
   a user's own terrain is used as is. Building on a demo scene's terrain isn't supported (multi-tile terrains).
8. **Schedules move whole groups at period changes**: commuting can look synchronised.
9. **Performance at reserve scale is untested on low-end hardware** (see the GPU Resident Drawer note).
10. **The authors of 0.7–1.4 never ran Unity**: everything compiles in five configurations, but every feature
   since 0.7 was verified only through the user's screenshots. Expect tuning.

**Visual priorities (in order):** believable animals → kit trees and grass proven in Unity → landmarks that use the
new elevation (a cascade, a hidden overlook on an upland shelf) → real prop models (kiosk, blind, benches) → night sky
and wet-weather surfaces → water detail (foam, refraction).

## 7. Checklist for changes

- `Tools~/CompileCheck/compile-check.sh` passes (Built-in + legacy input, Input System, Unity 6 + URP + Input System
  1.9, and both editor builds).
- New file or folder → commit its `.meta`.
- Builder output changed → bump `WildLensVersion.Environment`; `Art~` changed → bump `ArtLibrary.ImportVersion`
  and update `Art~/CREDITS.md`.
- Release → bump `package.json` and `WildLensVersion.Package`; update `Features`, `RecentChanges`, `ReleaseName`,
  `ReleaseDate` (the trailhead board shows them).
- New gameplay idea? Check it against the non-priorities in section 2 first, then ask "would this make the place
  feel more real?" If not, it probably doesn't belong.
- New sign? Use `SignKit.Sign` / `SignKit.Panel`: a title, at most one short line, pictograms, the destination
  number where there is one. Positions are in **reader space** (x = the reader's right). After a rebuild, check the
  Console line from `SignCheck` (or run **WildLens ▸ Check Sign Lettering**). Paragraphs belong in the journal or nowhere.
- New landmark? Answer "why would a photographer come here?" and "what does the ground do here?" first.
- Terrain generation changed? Prototype it outside Unity and measure relief and slopes (see the 1.4 notes in Systems.md).
- Anything the player walks into or onto (doors, decks, steps): doorways ≥ 1.0 m wide and ≥ 1.95 m tall, steps
  ≤ 0.4 m (CharacterController radius 0.35, skin 0.05, height 1.8, step offset 0.4).
- New scenery placement? Route it through the kit roles (`KitPlacement`) so free packs can replace it, give placed
  assets their gameplay components, and keep the random sequence unchanged when the kit is empty.
- Update `CHANGELOG.md`, `Documentation~/Systems.md` (and `VisualRoadmap.md` / `EnvironmentMigration.md` for art).
- Don't commit store assets, secrets or generated `Assets/` content.

## 8. What SHOULD be built next (in order)

1. **Playtest 1.4**: signs at each destination (run Check Sign Lettering), the new landforms (where did the ridge,
   pond, creek and road end up? are any slopes too steep to walk or ride?), each weather day, the Creek Ford at dawn.
2. **Prove the Environment Kit in Unity** (`EnvironmentMigration.md` §5): the user imports Unity's free *Terrain URP
   Demo Scene* and *Terrain Sample Asset Pack* (Unity 6000.0.37+), scans, rebuilds and sends comparison captures.
   Fix what the first run exposes; tune `treeScale`, grass densities and role assignments. Check the outcrops.
3. **Playtest-driven tuning of 1.0–1.4.** Road routing and grading, ride comfort, each destination at each time of
   day, schedules being learnable. Ask for screenshots of specific places at specific times.
4. **One realistic deer**, integrated with Animal Model Setup; then fox and hawk. This is still the single biggest
   improvement available, and it is a purchase plus an afternoon, not a system.
5. **Trees, round three, only if the kit route fails**: translucent leaf cards, crown AO, distance billboards for the
   generated trees. Kit trees (SpeedTree) bring all of this.
6. **Props worth looking at**: the woodwork has scanned wood (1.3); next are proper shapes (bevelled posts, a shingled
   kiosk roof, a timber blind) and lit sign lettering (TextMeshPro with a CC0 font, if one fits).
7. **Landmarks that use the elevation**: a small cascade where the creek leaves the upland, a hidden overlook on an
   upland shelf, then the wetland boardwalk (`ReserveExpansion.md`). Each with its subject and its light.
8. **Night and wet weather**: a CC0 night sky (Poly Haven), wet surfaces and puddles in rain, a frog chorus at dusk.
9. **Expansion species, one at a time** (`Documentation~/WildlifeExpansion.md`). Seventeen are in (1.4); the next
   candidates (wood duck, painted turtle, pileated woodpecker) each wait for the place they would really live.
10. **New places with a reason**: a cascade, wetland boardwalk with a second blind, a forest footpath,
   a ranger cabin (`Documentation~/ReserveExpansion.md`).
11. **A hand-authored reserve terrain** shipped as CC0 data, so the reserve no longer depends on the user's terrain.

## 9. What should NOT be built

- **No progression, XP, currency, unlocks, missions, quests, achievements, checklists, inventory, crafting, survival,
  combat or hunting.** They turn attention into accumulation. The journal is the only record.
- **No faster or more capable bike** (no boost, upgrades, fuel, damage, stunts). Fix routes and infrastructure instead.
- **No scripted wildlife events, spawns on cue, quest markers or compass HUDs.** Rhythms come from the clock and
  placement; players find animals by learning the place.
- **No predator–prey hunting, breeding or population simulation.** Simulation depth with little photographic payoff.
- **No bigger map without a reason to go to the new part** (destinations, not distance).
- **No mixing art fidelities.** Don't add stylised or low-poly assets beside the photographic reserve; don't commit
  non-CC0 assets (free store packs included). Don't adopt HDRP-only environments (Book of the Dead, Fontainebleau).
- **No hand-edited scenes.** Everything the player sees is generated; improvements land by rebuilding.
- **No technology for its own sake.** Unity features earn their place only with a visible gain (see the Unity 6 notes).

