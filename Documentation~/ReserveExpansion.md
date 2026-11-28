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
| | **Old-Growth Grove** | densest woodland | three giant trees around a quiet clearing, a bench | squirrels, red fox, barred owl, blue jay, chickadees (1.8) |
| | **Wildflower Meadow Blind** | most open flat ground | a meadow with flowers and a wooden hide; inside it you're concealed | deer, cottontails, cardinal, turkey, goldfinches (1.8) |

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
* **Residents added in 1.9**: eastern bluebird (Old Cabin Ruins' fence), tufted titmouse (the Rock Shelter's woods), Louisiana
  waterthrush (Hemlock Run), phoebe (the cascade's ledge); the Hidden Pond and the cabin clearing take species that already exist.
* **Residents added in 1.8 (not a destination, but a place):** crows and robins at the trailhead, a crow group at one road
  pull-off, song sparrows in the Cattail Pond's brush and the Creek Alcove, chickadees in the Fern Glade, and one black bear
  in the deep woods (the woodland farthest from the trailhead). The full list per place is in
  [WildlifeCampaign.md](WildlifeCampaign.md) §6.

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

## 0.9.0 Time of Day (implemented)

The reserve now has rhythms. Destinations change with the light (mist on the pond at dawn, the ridge at golden hour,
the grove's shade at midday, the meadow busy early and late) and wildlife moves through the day by placement: deer
to water at dusk, cottontails on the meadow edges early, hawks over the ridge in daylight. The trailhead board says
what's happening now and when each place is best. The **Reserve Road** gives the bike a proper route between
destinations; footpaths stay for walking.

## 1.9.0 Geography, not distance (written, not yet run in Unity)

The world stays 900 × 900 m. Measured: relief about 26 to 30 m, mean slope about 4°. 1.9 spends its effort on **what the
ground does**, not on size.

* **Hemlock Ravine.** A steep, shaded side valley cut by **Hemlock Run**, a small tributary of Willow Creek, with a stepped
  cascade (two or three drops over ledges into a pool) at its head. It exists where the land falls far enough toward the creek
  and away from paths (the default seed 7 does: about 186 m long, up to 8.2 m deep, the cascade about 8.5 m in three drops, the footpath about 294 m and under 8° from gentlest to steepest; across eight test seeds a ravine appears on 4, cascades run 3.5 to 10.4 m and no wall is steeper than 50°); elsewhere it is skipped and one Console line says why. The run's water ribbon is 2.3 m each side of its line, with the bed below both edges, and the plunge pool is an ellipse along the flow. One footpath leads in
  from the existing tracks, with one sign (HEMLOCK RAVINE, "to the cascade") and a log seat that looks up at the cascade.
  *Why a photographer comes:* the cascade in shade, the hemlocks, a waterthrush bobbing on the wet stones, a phoebe on the ledge.
* **Four more hidden places** (seven in all; unmarked, found and named once, never counted, on no sign or map):

  | Place | Chosen where | Why a photographer comes | Residents |
  |---|---|---|---|
  | **Cascade Overlook** | a ledge on the ravine's rim, 8 to 24 m from the cascade, with a line of sight to it | the cascade from above, the run stepping down through the hemlocks | (the ravine's) |
  | **Hidden Pond** | a wooded hollow on gentle ground, at least 45 m from every path | still water far from anyone: herons, blackbirds, a raccoon at dusk, frogs after rain (two shore Water hotspots and a bank residence) | great blue heron, red-winged blackbird, raccoon |
  | **Rock Shelter** | an upland hillside under the ridge, an overhanging crag with a dry floor | shelter, a cold fire ring and a long view; titmice in the woods in front (a Woodland anchor) | tufted titmouse, gray squirrel |
  | **Old Cabin Ruins** | level ground at a woods' edge, 45 to 100 m off a path | fieldstone and a chimney taken back by ferns; a bluebird on the fence (a small Meadow hotspot in the clearing keeps it there) | eastern bluebird, cottontail, groundhog |

  The earlier three (Hidden Overlook, Fern Glade, Creek Alcove, 1.5) are unchanged.
* **Carved late.** The ravine and the places are made after every other scenery pass, with their own random stream, so for a
  given seed everything built before them stays exactly where it was; whatever stood where the ground moved is settled onto the
  new surface or lifted out. It was prototyped and measured in Python on eight seeds first: the existing paths and destinations
  changed by 0.00 m of height. This is now the pattern for any new terrain shaping (see Systems.md, "The reserve's geography").
* The Rock Shelter's crag and the cascade's ledges are the **least proven shapes**; rain still falls through the shelter's roof.

## Next steps (proposed, in order)

1. ~~**Time of day.**~~ Done in 0.9. ~~**Weather**~~ done in 1.4 (a four-day forecast), and wildlife now answers it (1.9).
   Seasons later.
2. **Creek, part two.** ~~Stepping stones at a ford~~ (the Creek Ford, 1.4). ~~A waterfall~~ (Willow Falls, 1.5).
   ~~A tributary and a second, smaller cascade~~ (Hemlock Run and its cascade, 1.9).
3. **Wetland edge.** Widen the pond into a marsh: a boardwalk loop, sedges, a second blind. Natural home for a
   future heron or wood duck (new species are data plus a model).
   ~~Hidden places~~ (1.5): a hidden overlook, a fern glade, a creek alcove, unmarked. (1.9): four more, seven in all.
4. **Forest interior footpath.** A narrow path (no bikes, marked by a gate) through the grove to a hidden glade:
   intimacy and shade as a contrast to the open ridge. (The ravine footpath is the first of these.)
5. **Ranger cabin.** Near the loop: the journal on a desk, maps on the wall, a porch for golden hour. No NPCs.
   (The Old Cabin Ruins are the ruined cousin of this.)
   **Other places still left** (each must answer "why would a photographer come here?"): a **fern valley**, an
   **observation ledge**, a **creek-side hollow**.
6. **Hand-authored reserve terrain.** A 1–2 km heightmap (ridge, valley, creek line) shipped as CC0 data, so the
   reserve doesn't depend on the user's terrain. 1.4's generated landforms (lowland to upland, ridges, valleys,
   shelves) are the interim step and set the bar: about 30 m of interior relief, slopes mostly under 8°.

**Design rule from 1.4 playtesting: elevation is the highest-return environment change.** The ridge overlook was the
strongest place because of height and view. Every new destination should ask what the ground does there (a shelf to
look out from, a cut the creek drops through, a valley that closes the view) before asking what to build.

## Scale and performance notes

* At 900 m with four destinations: about 2× the trees of the 0.6 loop. Each procedural tree is several renderers
  (core plus leaf cards) and they sway, so they can't be static-batched. Distance culling (`LODGroup`) bounds the
  cost. Game-ready trees with real LODs and billboards remain the long-term fix (see VisualRoadmap.md).
* Terrain grass draws to 150 m regardless of reserve size.
* Wildlife: residents add roughly 6–10 animals. All simulate continuously; if counts grow a lot, add
  distance-based AI throttling.
