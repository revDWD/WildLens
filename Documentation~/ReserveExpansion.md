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

## Next steps (proposed, in order)

1. **Creek corridor.** A stream from the ridge down to the pond, carved into the terrain (spline, bed and bank
   shaping, a water ribbon mesh, stepping stones and a footbridge where a track crosses). Gives the reserve a spine,
   and gives deer and foxes a travel corridor that players can learn.
2. **Wetland edge.** Widen the pond into a marsh: boardwalk loop, sedges, a second blind. Natural home for a
   future heron or wood duck (new species are data plus a model).
3. **Forest interior trail.** A narrow footpath (bikes not allowed, marked by a gate) through the grove and on to
   a hidden glade: intimacy and darkness as a contrast to the open ridge.
4. **Ranger cabin.** A small building near the loop where the journal lives, with a porch to sit on at golden hour.
   Environmental storytelling: a desk with field notes, maps pinned to the wall. No NPCs needed.
5. **Bigger terrain authoring.** A hand-shaped 1–2 km heightmap (ridge, valley, creek line) shipped as CC0 data,
   so the reserve doesn't depend on the user's terrain. Terrain streaming isn't needed at this size.
6. **Time of day.** Morning mist on the pond, golden hour on the ridge; animals' schedules follow (deer at the pond
   at dusk). This turns "where" into "where and when", the real naturalist's question.

## Scale and performance notes

* At 900 m with four destinations: about 2× the trees of the 0.6 loop. Each procedural tree is several renderers
  (core plus leaf cards) and they sway, so they can't be static-batched. Distance culling (`LODGroup`) bounds the
  cost. Game-ready trees with real LODs and billboards remain the long-term fix (see VisualRoadmap.md).
* Terrain grass draws to 150 m regardless of reserve size.
* Wildlife: residents add roughly 6–10 animals. All simulate continuously; if counts grow a lot, add
  distance-based AI throttling.
