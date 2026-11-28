# AGENTS.md: WildLens

**The handoff for anyone continuing WildLens, person or AI agent.** It says what the project is, why it is built the
way it is, and **what to read next** for each topic. It is a map, not a manual: the detail lives in the linked
documents. If you read nothing else, read §1, §5, §14 and §15.

---

## 1. Project Overview

**What it is:** *Willow Creek Nature Reserve*, a quiet first-person wildlife photography game (v1.10.0). You walk or
ride a small trail bike around a generated reserve (meadow loop, ridge overlook, Cattail Pond, old-growth grove,
meadow blind, Willow Creek with its ford and falls, Reserve Road, footpaths, hidden places), notice animals,
approach without spooking them, and photograph them with a telephoto or, since 1.5, a macro lens (small life from your packs since 1.7, generated small life since 1.9). The day turns, the
weather follows a four-day forecast, and the wildlife keeps hours and **answers the weather** (since 1.9: deer shelter
from rain and come out in the first sun, songbirds bathe after it, the hawk soars only in fair weather; fireflies, frogs
and butterflies come out for their own conditions). Since 1.10 the animals **have voices** (bird song, frogs, crickets, made
in code, heard from the animal itself) and the blind, the Reserve Board and the Rock Shelter are dry. These are natural events: conditions, never scripts. **The place is
the product; photography is the reason to be there.**

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
- **How the team works (since 1.7).** The lead agent acts as director: it plans, makes the art and design judgments,
  reviews and integrates. It hands implementation to compartmentalised subagents that own **disjoint files** (wildlife,
  small life, UI, reserve, assets, docs). Every workstream is compile-checked in a **copy** while the others keep
  editing. An **independent, read-only reviewer who did not write the code** then reads each workstream for what only
  real Unity would show, and its findings are fixed **before** release (this has caught 0.67 m songbirds, clips that did
  not loop, frogs that could not hop, colours that never reached the models, a viewfinder row under the brackets...).
  Terrain is prototyped and **measured outside Unity** before it ships. Then versions and docs, a final compile check, and a
  push to `main`. Do not skip the reviewer: it has paid for itself every time.

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
- **Encounters from placement, rhythms from the clock and the weather.** Hotspots, residences and schedule anchors put
  animals where they'd be; time of day and weather move them, and since 1.9 the weather also brings natural events (a
  bath after rain, a hawk that waits out the grey). Nothing spawns on cue. Patterns are consistent and learnable.
- **Events are conditions, never scripts.** Something may happen because of the **hour**, the **weather now**, the
  **weather lately** or the **forecast**. It never happens because of the **player**, a counter, a marker or a reward, and
  the same hour and forecast always give the same result. A hint belongs in a species' field notes, not on the screen.
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
- **Collectibles**, **scripted or triggered wildlife events** (anything aimed at the player, counted or rewarded), quest
  markers, compass HUDs, and **an always-on HUD** (the screen is empty by default since 1.9: a faint centre dot, quiet
  prompts that retire, the viewfinder when the camera is up). Natural, condition-driven events (§3) are wanted.
- **A faster or more capable bike.** Fix routes, not the vehicle.
- **New systems during presentation milestones.** When the loop works and the look doesn't, fix the look.
- **A bigger map without a reason** to go to the new part.

→ The complete list, with what to build instead: [Documentation~/Roadmap.md](Documentation~/Roadmap.md).

## 6. Current Project State (1.10.0, "Voices")

**Proven in Unity** (the 1.7 audit, the 1.8 playtest, "a major success; the reserve immediately feels more alive", and the
**first 1.9 screenshots** in real Unity, URP, Unity 6000.6): the automatic pack pipeline and the pack animals (deer, turkey as a
tinted chicken, coyote as the shipped wolf, hawk as the shipped eagle, the songbirds and the black bear), the
approach-and-photograph loop, camera feel, legible grading, **the 1.9 viewfinder (data row, brackets, grade word) and the keeper
print**, **the generated wildflowers in the meadow**, the pull-off, the bike, **a distant waterfall**, named places with real
elevation, the day cycle and four-day forecast, Willow Creek with its ford and falls, hidden places, the Reserve Road and
footpaths, one sign standard and the three-sided Reserve Board. From the 1.8 playtest: the fox reads a bit like a dog (a tinted
dog model) and the turkey like a chicken (a tinted chicken).

**What the 1.9 screenshots exposed** (fixed in 1.10, **fixes not yet seen**): the **Northern Cardinal rendered solid magenta**
through the lens (a material URP cannot draw; expected for every Living Birds bird used untinted: cardinal, blue jay, crow, robin,
chickadee, goldfinch, song sparrow); the **turkey was almost black at dusk** (the 1.9 tint was too dark); **Console errors**
`AnimationEvent 'ResetHopInt' … has no receiver` (pack clips carry events for scripts WildLens removes).

**Written in 1.10 and not yet run in Unity** (everything compiles in five configurations; nothing has been heard or seen):
- **Eight free animated models** (a late addition, `Art~/Animals`, import version 8): a red fox (replaces the tinted dog), an
  eastern cottontail, an eastern gray squirrel, a rat standing in for the opossum, and a duck, turtle, snake and skunk that wait for
  species of their own. They swap in on the next Rebuild by the upgrade rule. **Size, facing and colours are all unproven.**
- **Voices**: 22 species with generated calls, three frogs calling in chorus, a bee buzz, a cricket bed; from the animals
  themselves (3D), sparse and budgeted, a dawn chorus, a burst in the first sun after rain, near-silence in rain, an alarm only
  on the Alert transition; one tuning point, `VoiceBank`. **Judged only from spectrograms**; timbre is merely plausible for
  several species. Night lasts about 11 real seconds, so night voices are in practice heard at dusk.
- **Shelter from rain**: `RainShelter` volumes at the Meadow Blind, the Reserve Board's eaves and the Rock Shelter (rain removed
  inside, a quieter and duller rain sound, an F3 `shelter` line).
- **The material repair**: a structural test of what URP can draw, empty slots filled, existing models repaired, a Console
  material summary per model and the Asset Audit's "WildLens animals as built" table. `FactoryVersion` 4; data version 5.

**Written in 1.9 and still mostly unseen**: natural wildlife events (`WeatherHabitRules`, twenty-seven species, the upgrade
rule), generated small life (26 models, 29 subjects; only the wildflowers have been seen), the quieter screen's other parts
(prompts, controls card, field notebook), and the geography (Hemlock Ravine and Run, the cascade, four more hidden places).

**Weakest, in order** (honest):
1. **Five species are placeholder bodies** (heron, raccoon, owl, groundhog, sandpiper), **the turkey is still a tinted chicken**
   and **the opossum is a stand-in rat**. The director relaxed the asset rule (§9: free and redistributable, no longer CC0 only);
   the model search then **finished**: eight free models were added (see above: the fox, cottontail and squirrel now have real
   bodies, unseen), but no free, redistributable model of a turkey, raccoon, owl, heron, groundhog or sandpiper was found. Of the
   hosts, opengameart.org and quaternius.com answer; poly.pizza asks for a browser check, kenney.nl refuses, and Quaternius'
   downloads sit on Google Drive, so the rest is a **browser job for the director**. The upgrade rule swaps a better model in. → [WildlifeCampaign.md](Documentation~/WildlifeCampaign.md).
2. **Voices are unheard.** Contours were checked on spectrograms (convincing for cardinal, chickadee, waterthrush, owl, coyote,
   goldfinch, treefrog, leopard frog, hawk, kingfisher, crickets); timbre is only plausible for jay, crow, heron, turkey, squirrel,
   deer, blackbird, sparrow, bluebird. Loudness, frequency and balance are untested. Ask for ears.
3. **The magenta cause is unproven.** 1.10 replaces any material URP cannot draw and reports what every model has: **read the
   audit's "animals as built" table and the Console's `materials:` lines first** if anything is still pink.
4. **Three unidentified things from one wide screenshot (open, not fixed):** a tan flat block on top of a rock by a viewing
   platform; a purple speck on distant rocks; trees that read as low-poly (possibly a low-poly tree pack pulled in by the
   Environment Kit). Ask for close-ups and the `[WildLens] Environment Kit` Console line.
5. **Trees** read as lollipops at distance: leaf cards lit like paper, no LODs or billboards. The Environment Kit can swap in
   Unity's free SpeedTree trees, **never confirmed in Unity**. → [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md).
6. **The generated small life is mostly unseen**, and the new geography: Willow Falls' curtain in its real place, the Rock
   Shelter's crag and the cascade's ledges are the least proven shapes. The ravine exists only where the land falls far enough
   toward the creek (it does on the default seed 7).
7. **Grass** is flat billboards unless the kit supplies mesh grass.
8. **Props are boxes** (with scanned wood). Sign text is unlit TextMesh in the built-in font.
9. **Sky and water**: three HDRIs re-tinted, no night sky, no moving clouds; **rain still falls under tree canopy** (only three
   roofs stop it); water has no refraction or foam.
10. **The journal's Wildlife page still lists unseen species as "???" rows and "Species X of N".** That sits uneasily with "no
    checklists" (§5); the Field notebook page does it right. A decision for the director.
11. **Untested at scale on low-end hardware.** About 200 generated small-life instances, twenty-seven species, schedules that move
    groups, and ten pooled voices.
12. **The authors of 0.7–1.10 never ran Unity.** Everything compiles in five configurations; everything visual was verified only
    through screenshots, and sound only through spectrograms. Expect tuning.

→ Per-system state and tuning: [Systems.md](Documentation~/Systems.md). Visual weaknesses and fixes:
[VisualRoadmap.md](Documentation~/VisualRoadmap.md). The recommended next milestone, **2.0 "True to Life"**:
[Roadmap.md](Documentation~/Roadmap.md).

## 7. Architecture Overview

- **Two assemblies.** `Scripts/` (runtime, `WildLens.Runtime`) runs the game; `Editor/` (`WildLens.Editor`) builds
  the reserve. Folders ending `~` (`Art~`, `Documentation~`, `Tools~`) are ignored by Unity.
- **The builder pipeline** (`Editor/WildLensSliceBuilder.cs`): trail loop → trailhead → reserve plan (sites chosen
  from the terrain, landforms, creek, falls, ford, tracks by A*, pull-offs) → scenery by density zone and kit role →
  destinations → ecosystem (bridges, creek life, residences, anchors) → discoveries → **late geography** (Hemlock Ravine
  and four hidden places, `GeographyBuilder` / `HiddenPlacesBuilder`; late **on purpose**: it runs after every pass that uses
  the shared random sequence, so ground carved here moves no tree, and it settles what stood where the ground moved) →
  macro subjects (plants, small life from packs, then generated small life) → terrain paint and grass → wind, light, sky →
  **grounding pass** → **sign check**.
- **Runtime:** `WildLensGame` bootstraps; `DayCycle` and `WeatherCycle` drive light and rhythms (`WeatherCycle` also owns the
  one memory of the rain); `WildlifeSpawner` places residents; `WildlifeSchedule` moves them by the pure rules in
  `WeatherHabitRules` (a species' rhythm plus its `WeatherHabits`); small life comes out by `NaturalConditions`;
  `AnimalAgent` → `GroundAnimal`/`FlyingAnimal` run behaviour with `WildlifeAwareness`;
  `CameraModeController` → `ZoomLens`/`FocusController` → `PhotoCamera` → `PhotoAnalyzer`/`MacroAnalyzer` →
  `JournalService`; `TrailBike` rides. The screen is `HudController` composing `ViewfinderView`, `PromptView`, `NoticeView`,
  `ControlsCardView` and the journal, all styled from `UiStyle`. **Since 1.10:** `VoiceDirector` (created at runtime) plays the
  calls in `VoiceBank` (one recipe per species, rendered once by `VoiceSynth`, scheduled by the pure `VoiceRules`; `Scripts/Audio/`),
  and `RainShelter` volumes (`Scripts/Environment/`) stop the rain and muffle its sound under three roofs.
- **Data, not code, for content:** species are `SpeciesDefinition` assets (`SpeciesDefaults`), small subjects are
  `NaturalistSubject` (`SubjectDefaults`, 29 since 1.9, 26 with generated models in `SmallLifeShapes`). Twenty-seven species,
  each with optional `WeatherHabits` (natural events are data too). Enums are serialized as integers: **append, never reorder.** Species
  assets and journal saves are versioned and migrated. A new species must be added to `WildlifeSpawner`'s own-stream set
  and `WildlifePackSetup`'s name table (and given a real size in `AnimalPrefabFactory.RealSize`).
- **Store assets are found, not shipped:** the Environment Kit (scenery by ecological role), Animal Model Setup and
  Wildlife Pack Setup (animals), Macro Subjects From Packs (small life) and the Asset Audit use what the user imported
  into their own project.

**Checklist for every change** (details in [CONTRIBUTING.md](CONTRIBUTING.md)):
- The compile check passes. New file or folder → commit its `.meta`.
- Builder output changed → bump `WildLensVersion.Environment` (old scenes are then flagged stale). `Art~` changed →
  bump `ArtLibrary.ImportVersion` and update `Art~/CREDITS.md`.
- Factory output changed, a species' colours changed, **or anything about how models' materials are made** → bump
  `AnimalPrefabFactory.FactoryVersion` (auto-built models are then refreshed; colours are baked into the model's materials).
- Retune a voice → edit its one line in `VoiceBank` (assets leave `voice` off). A new species gets a voice there too, or is
  silent on purpose (say why in the file).
- A new roofed place → add a `RainShelter` sized from the roof's own numbers, in the builder that makes the roof.
- Changed a generated small-life model → bump `SmallLifeModels.ModelVersion`.
- New terrain shaping → carve it in the **late geography step** and settle what stands there; **prototype and measure
  first** (outside Unity), and use a private random stream.
- New UI → style it from `UiStyle`; **nothing persistent on screen**; any uGUI, Input System or URP member you use must
  exist in real Unity, not just in the compile-check stubs.
- New species habit → data in `SpeciesDefaults` (`WeatherHabits`, one hint sentence in the field notes), then
  `SpeciesDefaults.ApplyHabits` carries it to existing assets; never a trigger.
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
| [Documentation~/WildlifeCampaign.md](Documentation~/WildlifeCampaign.md) | the plan, filled in from the user's pack audit: species to keep, replace and add, small life, the bike, ecology by place |
| [Documentation~/EnvironmentMigration.md](Documentation~/EnvironmentMigration.md) | the environment audit, free scenery packs, the Environment Kit |
| [Documentation~/VisualRoadmap.md](Documentation~/VisualRoadmap.md) | art sourcing, licensing, visual weaknesses |
| [Documentation~/TrailBike.md](Documentation~/TrailBike.md) | the bike model, free models reviewed, dropping one in |
| [Art~/CREDITS.md](Art~/CREDITS.md) | every committed art file, its source and licence |
| `Tools~/CompileCheck/` | compiling without Unity (add a stub member when you use a new uGUI/Input System/URP API) |

## 9. Asset Strategy

- **Committed: free, and free to pass on** (director, November 2026: "assets no longer need to be CC0, just free"). Any
  free licence that allows redistribution is welcome in `Art~`: CC0, and attribution licences such as CC-BY **with the
  author credited** in `Art~/CREDITS.md`. So far: Poly Haven skies, terrain, surfaces, rocks, wood, plants; Quaternius
  animals; and, since 1.10, eight animals from OpenGameArt by several authors (all CC0 but the skunk, CC BY 4.0, credited). Keep sizes modest (2k): `Art~` ships to every user.
- **Store assets, free ones included, are never committed** (the Asset Store EULA forbids redistribution). The user
  imports them locally; WildLens finds and places them: the **Environment Kit** for scenery (by ecological role),
  **Animal Model Setup** and **Wildlife Pack Setup** for animals, a named-pivot model for the bike.
- **Everything else is generated**: meshes, icons, materials, the terrain's landforms.
- Still not committable, because their licences forbid passing the files on: Asset Store packs (free ones included),
  "personal use only" models, re-uploads of paid packs. The user may import any free asset into their own project.
  HDRP-only environments remain unusable.

→ [VisualRoadmap.md](Documentation~/VisualRoadmap.md), [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md).

## 10. Wildlife Direction

Twenty-seven species are in, each with a place first (a niche) and a body defined as data. **Since 1.9 each can also have
weather habits** (`WeatherHabits`: where it goes in rain, after rain, in fair weather only; a livelier window after rain; a
hint sentence in its field notes). Habits are data, the rules are `WeatherHabitRules`, and the day is the same every time.
**The upgrade rule (1.9):** a model WildLens chose itself is replaced when a strictly better one is imported (a real Fox
over the tinted dog); a model chosen by hand in a setup window is never touched. **From 1.6, animals do not
need to be photorealistic or share one style.** Recognisable silhouette → animation → variety → consistency within a
species → realism, in that order. A stylised owl is a fine barred owl; one tinted songbird model can be five species.
Mixed packs are fine; the environment stays photographic.

**Since 1.10 they also have voices** (`VoiceBank`: 22 of the 27 species; the fox, cottontail, raccoon, opossum and black bear are
silent on purpose). A new species should get a voice, or a stated reason for silence.

**Next:** playtest 1.10 (the pack birds through the lens for any magenta; the voices by ear; the habits through the day-3 shower and
the day-4 dusk rain) and fix what the pictures and the director's ears show (including the eight new models: size, facing, colours).
Then **new species for the waiting models** (painted turtle, mallard, striped skunk, a snake, each with a place). Then **real models
for the turkey** and the five species still on placeholder bodies (heron, raccoon, owl, groundhog, sandpiper), plus a truer opossum.
The model search is finished: no free, redistributable model of those was found on the hosts the build machine can reach, so the
director downloads them in a browser and the upgrade rule swaps a better model in when one is imported. The plan in [WildlifeCampaign.md](Documentation~/WildlifeCampaign.md) is filled in from the user's audit. New species still need a place they'd really live, never a count, an entry in
`WildlifePackSetup`'s name table, a real size and a place in the spawner's own-stream set.
→ [WildlifeAssetStrategy.md](Documentation~/WildlifeAssetStrategy.md), [WildlifeExpansion.md](Documentation~/WildlifeExpansion.md).

## 11. Environment Direction

**Photographic and grounded.** Scanned surfaces, real relief (landforms regenerate WildLens's own terrain each
build), density that varies with habitat, water that connects places, light that changes through the day and the
weather. Never flatten land to make placement easy; never mix stylised scenery into it. Ground every prop. The
biggest open gain is **trees** (via the Environment Kit's SpeedTree route, unproven in Unity), then grass, props,
night sky and wet weather. **Since 1.9 geography is a lever too:** Hemlock Ravine was carved in a **late** step that moves
nothing built before it (measured on eight seeds first). The world is 900 m with about 26 to 30 m of relief and a mean slope
of about 4°, so more relief is the next lever, never more distance.
→ [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md), [ReserveExpansion.md](Documentation~/ReserveExpansion.md).

## 12. Photography Direction

Two lenses, one ritual. **Telephoto** (1–16×, FOV 50° → 3.34°) for wildlife at a distance, graded on frame fill,
focus, behaviour and composition, with live brackets. **Macro** (M, since 1.5) for small life at your feet, graded on
frame fill, focus within ~12% of distance, and centring, recorded in the journal's field notebook (its own page, A / D, since
1.9: only what you found). 29 subjects since 1.9, 26 with models WildLens generates (a pack's model always wins); they come
out for conditions (sun, sun after rain, warm hours, fair evenings, wet nights). Keep new modes
*beside* the telephoto pipeline, not inside it. Grades stay words (Poor/Good/Excellent). The journal is the only
record: no scores, no completion.
→ [Systems.md](Documentation~/Systems.md) (Camera, Photography, Macro).

## 13. Future Opportunities

**Recommended next milestone: 2.0 "True to Life"** (detail in [Roadmap.md](Documentation~/Roadmap.md)), in order: **tuning from
the screenshots and the director's sound feedback** (1.9 and 1.10 are both unproven; no new features) → **real models** for the fox
and turkey, then the five remaining placeholders, and new species where a place waits (the duck, turtle, snake and skunk models are already in) → **more relief** and the remaining hidden places →
**trees** via the Environment Kit → **night** (note: a game night lasts about 11 real seconds, so night work must first decide
whether nights should be playable) → the journal's "???" rows. **Sound is written (1.10) and awaiting ears.** After that: real prop
shapes and lit sign text, bike bodywork, wet weather, a hand-authored free-licence terrain.

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
- **Guessing an inventory you cannot see.** The user's packs live in their project, not the repository (1.7): build the
  probe that reports them (the Asset Audit) instead of assuming what they contain.
- **Building what's already there.** Players asked for species added a release earlier: check before building more.
- **Applying a rule beyond its reason.** "Don't mix fidelities" was right for scenery and wrong for animals.
- **Sizes from proportions instead of real measurements** (1.8: every songbird came out 0.67 m): give a species its real size.
- **Old pack scripts that do not compile block every update** (1.8: the Living Birds pack's `scripts` and `demo` folders on
  Unity 6; WildLens needs only its models and clips). If a pasted audit shows an older WildLens version than the one
  released, **ask for the Console's red lines first**.
- **Baked colours.** A species' colours are baked into its model's materials when the model is built: bump
  `AnimalPrefabFactory.FactoryVersion` whenever colours or factory output change, or existing models never change.
- **Carving terrain early.** It moves every tree after it through the shared random sequence and changes every earlier
  decision made from slope and path: **carve late and settle** (1.9).
- **Assuming a ground subject can be approached to 8 cm.** The camera is 1.65 m up standing and 0.95 m crouched, so a
  subject on the ground is never nearer than about a metre. Set grading thresholds from that arithmetic (1.9), and grade a
  tall plant on one part.
- **Events that react to the player.** A natural event answers the hour, the weather and the forecast; if it needs the
  player to be present, near or watching, it is a script (§3).
- **World-coordinate meshes under a moved parent (1.9):** a mesh built in world coordinates must sit on an identity transform, and `MeshPart` takes a LOCAL position. Willow Falls' water and the pond's lily pads were drawn far away for four releases because nobody could see they were missing: ask for a screenshot of each new thing, not just of the place.
- **A shader's NAME does not tell you whether URP can draw it** (1.10: magenta birds). Test its passes, fill empty slots, and print
  what each model ended up with.
- **Sounds and animation events from a pack's clips call the pack's scripts:** turn `Animator.fireEvents` off on models whose
  scripts were removed.
- **Version strings:** 1.10 comes after 1.9; never compare them as text (nothing in WildLens does: scenes compare the integer
  `Environment`).
- **Committing store assets** (free ones included), secrets or generated `Assets/` content.

## 15. Recommended Reading Order For New Agents

1. **This file**, all of it (15 minutes).
2. [CONTRIBUTING.md](CONTRIBUTING.md): how to build, check and release. Run the compile check once.
3. [Documentation~/Roadmap.md](Documentation~/Roadmap.md): what's next, and what's off-limits.
4. [Documentation~/Architecture.md](Documentation~/Architecture.md): find your way around the code.
5. The topic you're working on:
   - animals → [WildlifeCampaign.md](Documentation~/WildlifeCampaign.md), [WildlifeAssetStrategy.md](Documentation~/WildlifeAssetStrategy.md), [WildlifeExpansion.md](Documentation~/WildlifeExpansion.md)
   - natural events (weather habits, the schedule) → [Systems.md](Documentation~/Systems.md) 1.9.0, then [WildlifeExpansion.md](Documentation~/WildlifeExpansion.md) ("Events as data")
   - small life and the macro lens → [Systems.md](Documentation~/Systems.md) 1.9.0 and Macro, [WildlifeCampaign.md](Documentation~/WildlifeCampaign.md) §4
   - the screen, prompts and journal → [Systems.md](Documentation~/Systems.md) 1.9.0 (UI), then `Scripts/UI/UiStyle.cs`
   - scenery, trees, terrain → [EnvironmentMigration.md](Documentation~/EnvironmentMigration.md), [VisualRoadmap.md](Documentation~/VisualRoadmap.md)
   - places, the ravine, hidden places → [ReserveExpansion.md](Documentation~/ReserveExpansion.md), then [Systems.md](Documentation~/Systems.md) 1.9.0 (geography)
   - any system's behaviour or numbers → [Systems.md](Documentation~/Systems.md)
   - the bike → [TrailBike.md](Documentation~/TrailBike.md)
6. [Documentation~/History.md](Documentation~/History.md) for the part you're changing, **before** you change it: most
   odd-looking decisions have a playtest behind them.
7. The latest [CHANGELOG.md](CHANGELOG.md) entry, then ask the user for current screenshots.

**What should I read next?** If you're about to build something, Roadmap. If you're about to change something,
History. If you're lost in the code, Architecture. If a number looks wrong, Systems.
