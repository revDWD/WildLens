# History and lessons

Every release, why it was made and what it taught. Read this when you want to know *why* something is the way it
is before changing it. The short form of the lessons is in [AGENTS.md](../AGENTS.md) ("Common mistakes").

## Release history and lessons

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

**1.4.0 Changing Skies**. The 1.3 playtest liked the reserve a lot (biodiversity, the road, the
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

**1.5.0 Falls & Field Notes**. The 1.4 playtest loved the skies, the elevation and the weather, and
reported floating signs. So: a grounding pass for every post and leg; Willow Falls, made by concentrating the creek's
own fall (from the new landforms) into one lip with a ravine below, and tied to the weather (spate after rain); hidden
places (overlook, fern glade, creek alcove) found and named once, never counted; groundwork for macro photography
(a lens mode, a data type for small subjects, close-range grading, a field notebook, two real plant scans); a Trail
125-style bike with visible suspension and better parking; the pond's reflections following the sky; and a wildlife
asset strategy. *Lessons:* a destination built from what the world already does (the creek's fall, the weather) beats
one dropped in. Placing from a single point is a slope bug waiting to happen: ground supports per footprint. A
realtime probe rendered once is a photograph of the wrong sky. Keep new photography modes beside the old ones
(macro sits next to the telephoto pipeline, not inside it). Animal style is a direction decision, not an asset drop:
present the routes, don't impose one.

**Cross-cutting lessons**
- When the player says something doesn't work, believe them and **build a probe** before theorising.
- Make invisible state visible: stale scenes, ignored inputs and FOV overrides each cost a milestone before they
  were surfaced.
- Primitive placeholders read as toys next to photographic surroundings; mixing fidelity levels is worse than
  either alone.
- Keep package size in mind: `Art~` ships to every user. Use 2k skies and ground, and drop unused files.

**1.6.0 Trailhead & Wildlife Packs**. The 1.5 playtest found signs readable at last, and four things:
the trailhead was a cluster of separate signs and posts, secondary sign text was too small (interpretive panels had
long lines fitted down to 3.5 cm capitals), a log seat floated at the ford, and the animals were still the weakest
thing in every photograph. The director then changed the wildlife direction: **stylised and mixed free assets are
welcome**; silhouettes, animation and variety matter more than realism. So: one three-sided Reserve Board under a
single roof (live board; etiquette; the road and register) with the bike in a bay beside it; larger sign lines,
pictograms, panels and board text; log seats bedded on slopes; and Wildlife Pack Setup, which matches any free
animal packs to all eighteen species with stand-ins and per-species tints. AGENTS.md became a navigation guide, with
history, architecture and roadmap in their own documents. *Lessons:* consolidation beats addition at an entrance:
four signs said less than one. Text that has to be fitted down to fit is a text problem, not a size problem: shorten
the words. A style rule that made sense for scenery ("don't mix fidelities") was blocking the biggest improvement
available for animals; check whether a rule's reason still applies to every place it's used.

**1.7.0 Field Audit & Small Life**. 1.6.1 shipped two animals and a way to fill the rest from free packs, but the
director had imported animal, nature and vehicle packs into the Unity project and nobody working on the code could see
them: they live only in that project and may never be committed. So 1.7 starts with a probe: **WildLens ▸ Asset Audit
Report** lists what is there, which reserve species each model can be, its clips and its shader problems, and puts it on the
clipboard to paste into the chat. The matching was widened and made deterministic (more stand-ins, the closer look-alike
first, names held for future species never used as stand-ins, ties broken by path), and clip names are read word by word
so "Threat" is no longer "eat" and death clips are ignored. The macro lens grew its second round, first step: twelve
small-life subjects (butterfly, moth, dragonfly, bee, beetle, mantis, frog, newt, snail, firefly, mushroom, daisy) as
data, found in the user's packs, placed in their habitats on their own random sequences, with simple movement and
hours and weather ("after rain" is three game hours). Nothing is faked: no model, no subject. The director also wrote
`WildlifeCampaign.md`, the plan the audit's answers will fill in. No new animal species were added: they wait for the
audit. *Lessons:* **the packs were in the user's project, not the repository. When you cannot see the thing, build the
probe that reports it (the Asset Audit) instead of guessing an inventory.** Name matching that is too loose is as
harmful as too tight: "Foxglove" is not a fox, "Threat" is not "eat", and a woodpecker is not a cardinal. Give every
placement its own seeded random source so adding one subject moves nothing else, and never use `string.GetHashCode` for a
seed (it differs between machines). None of this has run in Unity yet.

**1.8.0 New Neighbours** (current). The director imported animal, bird, bear, mushroom, tree and motorcycle packs, ran the
1.7 Asset Audit and pasted it: the first report from inside Unity. It proved the automatic setup worked (the deer from
ithappy, the chicken as the turkey, the shipped wolf and eagle), and showed what else the packs held (seven Living Birds,
a stylised bear, eleven mushrooms, eighteen motorcycle models) and what was wrong. Built-in-shader models would be pink in
URP, so the factory now makes converted copies of the materials. A pack with scripts reloaded Unity's scripts and the
queued set-up was lost, so the watcher keeps its state across reloads. The wrong clips were chosen (a bear's jump as its
walk, fly-left as fly, pecking as idle), so clip choice now prefers plain names and earlier keywords. Pack scripts stayed on
the models, so they are stripped. Bird sizes were two to five times too big because sizes came from proportions, so there is
now a table of real sizes (and the old auto-built models are refreshed once, from the same models). Pack clips did not
loop, so the factory loops copies. The journal showed only about eight species rows, so it scrolls. Six species a place had
been waiting for were added: crow and robin (trailhead), chickadee (Fern Glade, grove), goldfinch (the blind), song
sparrow (pond brush, Creek Alcove) and one black bear (deep woods). Mushrooms from SinuousShrooms are the first small life
(up to six variants, sizes varied, two to a log). *Lessons:* **the audit paid for itself: it showed real clip names,
shaders and sizes, and each one broke an assumption.** A review by someone who did not write the code caught the 0.67 m
songbirds; the author had checked that things compiled, not that the numbers were true. Give every species a real size
from a real measurement. Add new species on their own random streams, so a build moves nothing else. A factory that
improves must be able to refresh what it made before, without touching what a person tuned. None of 1.8 has run in Unity
yet; its first test is the user's screenshots.
