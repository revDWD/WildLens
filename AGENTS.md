# AGENTS.md: WildLens

**The handoff for anyone continuing WildLens, person or AI agent.** It says what the project is, why it is built the
way it is, and **what to read next** for each topic. It is a map, not a manual: the detail lives in the linked
documents. If you read nothing else, read §1, §5, §14 and §15.

---

## 1. Project Overview

**What it is:** *Willow Creek Nature Reserve*, a quiet first-person wildlife photography game (v1.6). You walk or
ride a small trail bike around a generated reserve (meadow loop, ridge overlook, Cattail Pond, old-growth grove,
meadow blind, Willow Creek with its ford and falls, Reserve Road, footpaths, hidden places), notice animals,
approach without spooking them, and photograph them with a telephoto or, since 1.5, a macro lens. The day turns, the
weather follows a four-day forecast, and the wildlife keeps hours. **The place is the product; photography is the
reason to be there.**

**How it's built:** a Unity UPM package (`com.revdwd.wildlens`, Unity 2021.3+, Unity 6 tested, URP or Built-in).
An editor builder (**WildLens ▸ Vertical Slice Builder ▸ (Re)build Slice In Open Scene**) generates the whole reserve
into a scene; runtime scripts run the game. Nothing in the scene is hand-edited.

**How work gets done:**
- **You probably can't run Unity.** After every change run `Tools~/CompileCheck/compile-check.sh` (five
  configurations, no Unity; needs the .NET SDK 6+, e.g. `apt-get install dotnet-sdk-8.0`). Then the user rebuilds and
  playtests. **Their screenshots are the truth.**
- **The user** is a non-programmer playtester and director. They install by Package Manager git URL
  (`https://github.com/revDWD/WildLens.git`), use GitHub Desktop, and send screenshots with feedback. Give plain
  steps ("Update the package, then Rebuild"), commit and push finished work to `main`, and always say what to look
  at and screenshot next.
- Release bookkeeping, the checklist for changes and the build → playtest loop: [CONTRIBUTING.md](CONTRIBUTING.md)
  and §7 below.

## 2. Vision

A **naturalist's reserve**: somewhere people would happily spend an afternoon walking trails, riding to an
overlook, watching wildlife and taking photographs. The direction has grown in steps, each from a playtest:

- **0.7, a reserve, not a loop:** places worth travelling to, each with a reason to go.
- **0.8, a simulation of a place, not a feature-heavy game:** ecological structure (dense, open, sparse), water that
  connects places, habitats with identities, quiet stewardship, wildlife where it would really be.
- **0.9, a place with rhythms:** dawn mist, golden hour, dusk; "I want the pond at dawn" should emerge from the world.
- **1.4–1.5, elevation, weather and small life:** ridges and valleys, a forecast to plan around, a macro lens for
  wildflowers, insects, frogs and fungi (as models arrive).

The question is never "what system is missing?" but **"what would make this place feel more real?"** Every playtest
since 0.6 has rewarded realism of place (light, habitat, water, height, signs) and been indifferent to mechanics.

Inspirations: *Pokémon Snap* (the photograph is the reward), *Firewatch* (a place that feels real, a calm pace), and
real nature observation (patience, and the moment an animal accepts you).
→ The full story of each step: [Documentation~/History.md](Documentation~/History.md).

## 3. Design Philosophy

**Priorities, in order:** feel of the encounter → camera feel → visual credibility → content breadth.

**The loop:** ride → spot something → stop → park (R) → raise the camera → walk the last stretch → back to the bike.
Anything that adds friction to it is a bug.

**Working rules that have paid off** (each was learnt the hard way; see History):
- **Approachable realism.** Behaviour is grounded in real animals but tuned so patience always works: standing still
  always lets an animal settle.
- **Encounters from placement, rhythms from the clock.** Hotspots, residences and schedule anchors put animals where
  they'd be; time of day and weather move them. Nothing spawns on cue. Patterns are consistent and learnable.
- **Destinations, not distance; ecology over geography; elevation first.** Never grow the world without a reason to
  visit; vary it by habitat; ask what the ground does before building anything.
- **"Why would a photographer come here?"** Every landmark answers it with a subject, light or a view.
- **Trail hierarchy.** The Reserve Road is for getting between places (riding); footpaths are for being somewhere
  (walking). Roads stop at pull-offs.
- **Stewardship, lightly.** Signs, bridges, benches and a register say "someone looks after this place", never a
  theme park.
- **A sign is read in two seconds from the trail:** big title, one short line, a pictogram, a number. Paragraphs go.
- **Consolidate at entrances** (1.6). One deliberate structure says more than four loose signs.
- **Hidden, not collected.** Optional places are found and named once, never counted or rewarded.
- **Prove it, don't assume it.** When the player says something doesn't work, build a probe (F3 overlay, F4 zoom
  self-test, stale-scene notice, `SignCheck`) before changing anything else.
- **Rebuild, don't hand-edit.** Licence safety is absolute (§9).

**The trail bike:** a small trail bike (Trail 125-like), quiet-ish, friendly, practical. Modest speed (≈45 km/h on
the Reserve Road, ≈22 on footpaths, ≈20 across country), gentle acceleration, loud to wildlife on purpose so the last
stretch is always on foot. No stunts, damage, fuel or upgrades. → [Documentation~/TrailBike.md](Documentation~/TrailBike.md).

## 4. What Makes WildLens Successful

From every playtest so far, the things players respond to:
1. **The approach.** Animals notice you (Calm → Aware → Alert → Fleeing); you freeze, they settle, you creep closer.
2. **A camera that feels like a camera.** Raise, zoom in steps, autofocus hunting and locking, the shutter
   blackout, the print developing.
3. **Fair, legible grading.** Frame fill, focus, behaviour and composition, with viewfinder brackets that show the
   grade *before* you press the shutter. Poor / Good / Excellent, not numbers.
4. **A place worth looking at through a 16× lens**: scanned ground, rock and wood, HDRI skies, real relief.
5. **A place worth travelling through**: named destinations with signature details (the ridge telescope, the
   canoe, the Grandmother Oak, the falls), connected by the creek and the road.
6. **Time and weather that change the place**: the same spot is different at dawn, at dusk and after rain.

## 5. Things We Deliberately Avoid

Left out on purpose. Don't add them unless the director asks:
- **Progression, XP, currency, unlocks, missions, quests, achievements, checklists, inventory, crafting.** WildLens
  is about attention, not accumulation. The journal (species seen, best photo each, the field notebook) is the only
  record.
- **Combat, hunting, survival, failure states.** A fleeing animal is the only "fail", and it comes back.
- **Collectibles**, scripted wildlife events, quest markers, compass HUDs.
- **A faster or more capable bike.** Fix routes, not the vehicle.
- **New systems during presentation milestones.** When the loop works and the look doesn't, fix the look.
- **A bigger map without a reason** to go to the new part.

→ The complete list, with what to build instead: [Documentation~/Roadmap.md](Documentation~/Roadmap.md).

## 6. Current Project State (1.6.0, "Trailhead & Wildlife Packs")

**Working well:** the approach-and-photograph loop; camera feel; legible grading; named places with distinct
identities and signature details; real elevation; a day cycle and a learnable four-day weather forecast; Willow Creek
with its ford and falls; hidden places; macro groundwork with a field notebook; eighteen species from data; the Reserve
Road and footpaths; a Trail 125-style bike with suspension; one sign standard with a lettering probe; a three-sided
Reserve Board at the trailhead with the bike bay beside it; the Environment Kit and Wildlife Pack Setup for using
free store assets locally; a compile check that runs without Unity.

**Weakest, in order** (honest):
1. **Animals are primitive placeholders.** The tool to replace them from free packs exists (1.6) but has never run in
   Unity. → [WildlifeAssetStrategy.md](Documentation~/WildlifeAssetStrategy.md).
2. **Trees** read as lollipops at distance: leaf cards lit like paper, no LODs or billboards. The Environment Kit can
   swap in Unity's free SpeedTree trees, also **never run in Unity**. → [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md).
3. **Grass** is flat billboards unless the kit supplies mesh grass.
4. **Props are boxes** (with scanned wood). Sign text is unlit TextMesh in the built-in font.
5. **Sky and water:** three HDRIs re-tinted, no night sky, no moving clouds; rain has no wet surfaces and falls
   through roofs; water has no refraction or foam.
6. **Macro has two subjects** (plants); insects, frogs and fungi wait for models and the reserved behaviours.
7. **Untested at scale on low-end hardware.** Schedules move whole groups at once.
8. **The authors of 0.7–1.6 never ran Unity.** Everything compiles in five configurations; everything visual was
   verified only through screenshots. Expect tuning.

→ Per-system state and tuning: [Systems.md](Documentation~/Systems.md). Visual weaknesses and fixes:
[VisualRoadmap.md](Documentation~/VisualRoadmap.md).

## 7. Architecture Overview

- **Two assemblies.** `Scripts/` (runtime, `WildLens.Runtime`) runs the game; `Editor/` (`WildLens.Editor`) builds
  the reserve. Folders ending `~` (`Art~`, `Documentation~`, `Tools~`) are ignored by Unity.
- **The builder pipeline** (`Editor/WildLensSliceBuilder.cs`): trail loop → trailhead → reserve plan (sites chosen
  from the terrain, landforms, creek, falls, ford, tracks by A*, pull-offs) → scenery by density zone and kit role →
  destinations → ecosystem (bridges, creek life, residences, anchors) → discoveries, macro subjects → terrain paint
  and grass → wind, light, sky → **grounding pass** → **sign check**.
- **Runtime:** `WildLensGame` bootstraps; `DayCycle` and `WeatherCycle` drive light and rhythms; `WildlifeSpawner`
  places residents; `AnimalAgent` → `GroundAnimal`/`FlyingAnimal` run behaviour with `WildlifeAwareness`;
  `CameraModeController` → `ZoomLens`/`FocusController` → `PhotoCamera` → `PhotoAnalyzer`/`MacroAnalyzer` →
  `JournalService`; `TrailBike` rides.
- **Data, not code, for content:** species are `SpeciesDefinition` assets (`SpeciesDefaults`), small subjects are
  `NaturalistSubject` (`SubjectDefaults`). Enums are serialized as integers: **append, never reorder.** Species
  assets and journal saves are versioned and migrated.
- **Store assets are found, not shipped:** the Environment Kit (scenery by ecological role), Animal Model Setup and
  Wildlife Pack Setup (animals) use what the user imported into their own project.

**Checklist for every change** (details in [CONTRIBUTING.md](CONTRIBUTING.md)):
- The compile check passes. New file or folder → commit its `.meta`.
- Builder output changed → bump `WildLensVersion.Environment` (old scenes are then flagged stale). `Art~` changed →
  bump `ArtLibrary.ImportVersion` and update `Art~/CREDITS.md`.
- Release → bump `package.json` and `WildLensVersion.Package`; update `Features`, `RecentChanges`, `ReleaseName`,
  `ReleaseDate` (the Reserve Board shows them); `CHANGELOG.md`; `Systems.md`; and this file's §6.
- New sign → `SignKit.Sign`/`SignKit.Panel`, positions in **reader space**; check the `SignCheck` Console line.
- New prop on legs or posts → name supports `Post`/`Leg` (log seats `Seat Log`) so `GroundingPass` grounds them.
- Anything walked into → doorways ≥ 1.0 m × 1.95 m, steps ≤ 0.4 m (CharacterController r 0.35, skin 0.05, h 1.8).
- New scenery → route through kit roles (`KitPlacement`); keep the random sequence unchanged when the kit is empty.

→ File map, key flows and packaging: [Documentation~/Architecture.md](Documentation~/Architecture.md).

## 8. File and Documentation Map

| Read | When you need… |
|---|---|
| [README.md](README.md) | the player-facing introduction, install steps, controls |
| [CONTRIBUTING.md](CONTRIBUTING.md) | setup, the build → playtest loop, the release checklist |
| [CHANGELOG.md](CHANGELOG.md) | what changed in each version |
| [Documentation~/Architecture.md](Documentation~/Architecture.md) | where code lives, the file map, key flows through the code |
| [Documentation~/Systems.md](Documentation~/Systems.md) | how each system works, with tuning numbers (camera, awareness, day, weather, macro, signs, terrain…) |
| [Documentation~/History.md](Documentation~/History.md) | why things are the way they are: every release and its lessons |
| [Documentation~/Roadmap.md](Documentation~/Roadmap.md) | what to build next, in order, and what never to build |
| [Documentation~/ReserveExpansion.md](Documentation~/ReserveExpansion.md) | the reserve's design: destinations, tracks, future places |
| [Documentation~/WildlifeExpansion.md](Documentation~/WildlifeExpansion.md) | the species framework and adding a species |
| [Documentation~/WildlifeAssetStrategy.md](Documentation~/WildlifeAssetStrategy.md) | animal models: direction, free sources, Wildlife Pack Setup |
| [Documentation~/EnvironmentMigration.md](Documentation~/EnvironmentMigration.md) | the environment audit, free scenery packs, the Environment Kit |
| [Documentation~/VisualRoadmap.md](Documentation~/VisualRoadmap.md) | art sourcing, licensing, visual weaknesses |
| [Documentation~/TrailBike.md](Documentation~/TrailBike.md) | the bike model, free models reviewed, dropping one in |
| [Art~/CREDITS.md](Art~/CREDITS.md) | every committed art file and its CC0 source |
| `Tools~/CompileCheck/` | compiling without Unity (add a stub member when you use a new uGUI/Input System/URP API) |

## 9. Asset Strategy

- **Committed: CC0 only.** Poly Haven skies, terrain, surfaces, rocks, wood, plants (`Art~`, credited in
  `Art~/CREDITS.md`). Keep sizes modest (2k): `Art~` ships to every user.
- **Store assets, free ones included, are never committed** (the Asset Store EULA forbids redistribution). The user
  imports them locally; WildLens finds and places them: the **Environment Kit** for scenery (by ecological role),
  **Animal Model Setup** and **Wildlife Pack Setup** for animals, a named-pivot model for the bike.
- **Everything else is generated**: meshes, icons, materials, the terrain's landforms.
- Not usable: CC-BY or "personal use" models, AI-generator free-plan output (CC-BY), re-uploads of paid packs, HDRP-only
  environments.

→ [VisualRoadmap.md](Documentation~/VisualRoadmap.md), [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md).

## 10. Wildlife Direction

Eighteen species are in, each with a place first (a niche) and a body defined as data. **From 1.6, animals do not
need to be photorealistic or share one style.** Recognisable silhouette → animation → variety → consistency within a
species → realism, in that order. A stylised owl is a fine barred owl; one tinted songbird model can be five species.
Mixed packs are fine; the environment stays photographic.

**Next:** download free packs (Quaternius CC0 first), run **WildLens ▸ Wildlife Pack Setup**, playtest. New species
still need a place they'd really live, never a count.
→ [WildlifeAssetStrategy.md](Documentation~/WildlifeAssetStrategy.md), [WildlifeExpansion.md](Documentation~/WildlifeExpansion.md).

## 11. Environment Direction

**Photographic and grounded.** Scanned surfaces, real relief (landforms regenerate WildLens's own terrain each
build), density that varies with habitat, water that connects places, light that changes through the day and the
weather. Never flatten land to make placement easy; never mix stylised scenery into it. Ground every prop. The
biggest open gain is **trees** (via the Environment Kit's SpeedTree route, unproven in Unity), then grass, props,
night sky and wet weather.
→ [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md), [ReserveExpansion.md](Documentation~/ReserveExpansion.md).

## 12. Photography Direction

Two lenses, one ritual. **Telephoto** (1–16×, FOV 50° → 3.34°) for wildlife at a distance, graded on frame fill,
focus, behaviour and composition, with live brackets. **Macro** (M, since 1.5) for small life at your feet, graded on
frame fill, focus within ~12% of distance, and centring, recorded in the journal's field notebook. Keep new modes
*beside* the telephoto pipeline, not inside it. Grades stay words (Poor/Good/Excellent). The journal is the only
record: no scores, no completion.
→ [Systems.md](Documentation~/Systems.md) (Camera, Photography, Macro).

## 13. Future Opportunities

In priority order (detail in [Roadmap.md](Documentation~/Roadmap.md)): playtest 1.6 → **animals from free packs** →
prove the Environment Kit's trees in Unity → animated macro subjects (butterflies, dragonflies, frogs, fireflies,
mushrooms after rain) → tuning → real prop shapes and lit sign text → bike bodywork → night sky and wet weather →
new species with their places → a wetland boardwalk and second blind → a hand-authored CC0 terrain.

## 14. Common Mistakes

Each of these has cost at least one milestone. Details and stories in [History.md](Documentation~/History.md).
- **Not rebuilding.** A scene not rebuilt since an update shows none of it (0.6: two milestones of art were
  invisible). The stale-scene notice exists for this; bump `WildLensVersion.Environment` when builder output changes.
- **Trusting code over screenshots.** "It should work" is not evidence. Build a probe, then ask for a screenshot.
- **Input magnitudes.** The two input backends report the scroll wheel on different scales (0.4 divided ±1 by 120, so
  zoom did nothing on Unity 6): use direction only.
- **Signs in the wrong space.** `SignKit` positions are in **reader space** (x = the reader's right; a sign's front
  faces +Z, so the reader's right is its −X). 1.3 shipped mirrored lettering that only a screenshot caught.
- **Fitting text instead of shortening it.** Long lines shrink to unreadable capitals. Two seconds, from the trail.
- **Placing from a single point on a slope.** Posts, legs and seats float. Name supports for the grounding pass.
- **Consuming the shared random sequence.** New detail from the builder's RNG moves every tree after it; use a
  separate, seeded `System.Random`, and never change the sequence for users who haven't opted in.
- **Openings sized to the model, not the player.** Doorways narrower than the CharacterController (0.7 m blind door).
- **Probes rendered once.** A reflection probe baked at build time is a photograph of the wrong sky.
- **Mirror-smooth water.** It reflects the sky's most saturated colour exactly where players look.
- **The noise "forest field" is not the forest.** Paint and plant from where trees actually stand.
- **Shipping terrain changes unmeasured.** Prototype landforms outside Unity and measure relief and slope first.
- **A convention used in one direction only** (centred text hid the sign mirroring for releases).
- **Building what's already there.** Players asked for species added a release earlier: check before building more.
- **Applying a rule beyond its reason.** "Don't mix fidelities" was right for scenery and wrong for animals.
- **Committing store assets** (free ones included), secrets or generated `Assets/` content.

## 15. Recommended Reading Order For New Agents

1. **This file**, all of it (15 minutes).
2. [CONTRIBUTING.md](CONTRIBUTING.md): how to build, check and release. Run the compile check once.
3. [Documentation~/Roadmap.md](Documentation~/Roadmap.md): what's next, and what's off-limits.
4. [Documentation~/Architecture.md](Documentation~/Architecture.md): find your way around the code.
5. The topic you're working on:
   - animals → [WildlifeAssetStrategy.md](Documentation~/WildlifeAssetStrategy.md), [WildlifeExpansion.md](Documentation~/WildlifeExpansion.md)
   - scenery, trees, terrain → [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md), [VisualRoadmap.md](Documentation~/VisualRoadmap.md)
   - places → [ReserveExpansion.md](Documentation~/ReserveExpansion.md)
   - any system's behaviour or numbers → [Systems.md](Documentation~/Systems.md)
   - the bike → [TrailBike.md](Documentation~/TrailBike.md)
6. [Documentation~/History.md](Documentation~/History.md) for the part you're changing, **before** you change it: most
   odd-looking decisions have a playtest behind them.
7. The latest [CHANGELOG.md](CHANGELOG.md) entry, then ask the user for current screenshots.

**What should I read next?** If you're about to build something, Roadmap. If you're about to change something,
History. If you're lost in the code, Architecture. If a number looks wrong, Systems.
