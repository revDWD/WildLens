# From trail loop to wildlife reserve

Playtest, November 2026: *"I wish I could go farther."* The world became beautiful enough that travelling through
it is a reward. This is the plan for growing WildLens from one trail loop into a reserve someone would happily spend
an afternoon in, without losing what makes the loop work: quiet, close encounters and photographs worth keeping.

## Principles

1. **Destinations, not distance.** A bigger map with nothing in it is a longer walk. Every place you can travel
   to has a *reason*: a view, water, big trees, concealment, a species you won't easily find elsewhere.
2. **Every destination teaches something about wildlife.** The pond is where deer drink. The grove is where squirrels
   live. The ridge is where hawks hunt. Players learn the reserve the way naturalists learn a patch: by where the
   animals are.
3. **Travel is part of observation.** Tracks pass through different habitats. The bike makes distance cheap but
   loud, so the last stretch is always on foot.
4. **Legible from the trailhead.** The board's map, numbered destinations and fingerposts make the reserve readable
   before you set off. No quest markers, no compass HUD.
5. **Peaceful.** No combat, survival, crafting, resources, vehicle progression or skill trees. The reward for
   exploring is what you see and photograph.

## What 0.7.0 builds (implemented)

The builder reads the terrain and chooses sites for what the land offers (`Editor/ReserveBuilder.cs`):

| # | Destination | Chosen where | Purpose | Residents |
|---|---|---|---|---|
| – | **Meadow Loop** (existing) | gentlest ground | the intimate core: meadow, forest edge, viewpoints | all five |
| 1–4 | **Ridge Overlook** | highest prominent ground (a hill is raised if the land is flat) | the widest view in the reserve; a deck, bench, perch snag | red-tailed hawk |
| | **Cattail Pond** | lowest flat basin (dug if needed) | open water, reeds and cattails, a jetty | white-tailed deer |
| | **Old-Growth Grove** | densest woodland | three giant trees around a quiet clearing, a bench | squirrels, red fox |
| | **Wildflower Meadow Blind** | most open flat ground | a meadow with flowers and a wooden hide; inside it you're concealed | deer, cottontails |

* Destinations are spread around the compass (no two within 60° of bearing), 1.5× the loop radius or more from
  its centre, and up to `reserveReach` (380 m) away. Numbers run nearest-first.
* **Reserve tracks** (2.4 m, footpath and bike track) run from the loop to each destination, routed by A* on a 5 m
  grid that avoids steep slopes, water, the inner meadow and the blind's meadow. The blind's track arrives from
  *behind* the hide, so you don't walk through the meadow you came to watch.
* Scenery extends along the tracks and around the destinations, at a lower density than the core to protect frame
  rate. Procedural trees stop drawing when tiny on screen.
* Terrain shaping is idempotent and backed up. The WildLens fallback terrain grows to 900 m; your own terrain is
  never resized.
* The **trailhead kiosk** board reads the live game: version, features, recent changes, species and journal
  progress, trails, viewpoints, destinations with distances, and a heads-up topographic map.
* The **trail bike** is parked at the trailhead.

## 0.8.0 Ecosystem Pass (implemented)

From "a collection of destinations" to "a living landscape":
* **Willow Creek** ties the ridge to the pond: a natural landmark, a water source, a wildlife corridor and a reason
  for bridges. It's routed downhill and carved into the land; tracks and the loop cross it on footbridges.
* **Ecological structure**: dense woodland, clumped stands, quiet transition groves and open grassland, so a ride
  passes through noticeably different places. Ferns gather under dense canopy and along the creek.
* **Identity**: rocky, windy ridge with two hawks; irregular reedy pond with lily pads and reflections; a shaded,
  mossy old-growth grove; a colourful meadow with a proper blind.
* **Management, lightly**: interpretive panels, numbered track posts, a deer-crossing sign, nest boxes, a ranger's
  log, bike pull-offs short of each destination.
* **Encounters by placement**: deer at the creek and on a track crossing; cottontails on the meadow edge.

## Next steps (proposed, in order)

1. **Time of day: golden hour and dawn.** Morning mist on the pond and creek, golden light on the ridge; animals'
   schedules follow (deer at the pond at dusk). Turns "where" into "where and when", the naturalist's question.
2. **Creek, part two.** Stepping stones at a ford, a small cascade where it leaves the ridge, a tributary.
3. **Wetland edge.** Widen the pond into a marsh: a boardwalk loop, sedges, a second blind. Natural home for a
   future heron or wood duck (new species are data plus a model).
4. **Forest interior footpath.** A narrow path (no bikes, marked by a gate) through the grove to a hidden glade:
   intimacy and shade as a contrast to the open ridge.
5. **Ranger cabin.** Near the loop: the journal on a desk, maps on the wall, a porch for golden hour. No NPCs.
6. **Hand-authored reserve terrain.** A 1–2 km heightmap (ridge, valley, creek line) shipped as CC0 data, so the
   reserve doesn't depend on the user's terrain.

## Scale and performance notes

* At 900 m with four destinations: about 2× the trees of the 0.6 loop. Each procedural tree is several renderers
  (core plus leaf cards) and they sway, so they can't be static-batched. Distance culling (`LODGroup`) bounds the
  cost. Game-ready trees with real LODs and billboards remain the long-term fix (see VisualRoadmap.md).
* Terrain grass draws to 150 m regardless of reserve size.
* Wildlife: residents add roughly 6–10 animals. All simulate continuously; if counts grow a lot, add
  distance-based AI throttling.
