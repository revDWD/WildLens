# Changelog

## [1.4.0] Changing Skies
### Signs fixed
- **Sign lettering was mirrored** in 1.3: every left-aligned title, line and pictogram was placed on the reader's
  right, so it hung off the board's far edge or floated in the air (the boards themselves looked fine). A sign's front
  faces +Z, so its reader's right is the sign's -X. `SignKit` now takes positions in reader space and converts them;
  fingerposts were re-derived for both faces. Stained boards are lighter (they read as black slabs in shade).
- **WildLens ▸ Check Sign Lettering**: a probe that measures every label's glyphs against its board (off the edge,
  buried, facing the wrong way, too small) and lists problems in the Console. It also runs after every build.
### Elevation
- **Landforms v2** for WildLens's own terrain: a lowland (where the creek and pond settle) rising to an upland of broad
  ridges, spurs, small valleys and soft shelves; the meadow heart stays gentle. About 30 m of interior relief instead of
  7 m, slopes mostly under 8° (at most ~20°). Prototyped and measured before shipping.
- WildLens's own terrain is regenerated from the seed on every build (setting: *Regenerate WildLens Terrain*), so
  rebuilds are repeatable and terrain improvements arrive. Terrains of your own are never touched.
### Weather
- **A four-day forecast that repeats**, so it can be learned: fair; grey morning clearing by midday; an afternoon
  shower clearing for golden hour; overcast with rain at dusk. Cloud dims and softens the sun and its shadows, greys and
  thickens the haze, and cools the grade; when it's truly grey the sky becomes a scanned **overcast sky** (CC0, Poly
  Haven *Kloofendal Overcast*). **Rain** falls around you with its own sound; the wind picks up.
- **Wildlife shelters in rain**: meadow animals move into woodland, hawks to their perches, and everything rests more
  (placement and behaviour weights only). Herons and the creek birds carry on.
- Field notes when the weather turns; the trailhead board's conditions line shows the weather.
- Builder settings: *Weather* (off: always fair) and *First Weather Day* (for screenshots of a given weather).
### The Creek Ford
- A new landmark where you can watch the creek itself: the calmest open reach of Willow Creek near a path, with a short
  footpath spur, **stepping stones you can cross**, a gravel bar, a dead snag over the water, a log seat facing
  upstream, a sign and a panel ("THE RIFFLE"), and a louder brook.
- **Belted Kingfisher** (fishes from the snag, patrols low along the water) and **Spotted Sandpiper** (teeters on the
  gravel bar) live there, with a second Great Blue Heron. Seventeen species.
### Looks
- Forest floor and ferns only where trees actually stand (the forest field had painted bare leaf litter and ferns on
  open, sunny ground where the tree budget had run out).
- Ovate broadleaf crowns start lower (no bare-pole lollipops); crowns with few limbs are filled on every limb.
- Environment version 15; art import version 5 (**rebuild the slice**).

## [1.3.0] Trailhead
A presentation and readability pass from playtest screenshots. No new systems.
### Signs you can read
- **Trailhead board redesigned** like a national-park trailhead: WILLOW CREEK in big letters, the time of day and
  conditions ("MORNING 09:44 · Fair · deer in the meadows"), a large map with big numbered markers, and the four
  destinations with number, distance and best times as pictograms. The feature list, release notes and statistics are
  gone; the version is one small line in the footer (a stale scene is still flagged there).
- **One sign standard, round two** (`SignKit`): big bold titles (about 10 cm capitals, readable from ~15 m), at most
  one short line, the destination's number disc (matching the board's map), and **pictograms** for best times
  (sunrise, sun, sunset, moon) and for meaning (binoculars, paw, bird, water, tree, bike, walker). Boards are sized
  to their words from the font's real glyph widths, so nothing spills over.
- Every sign rewritten short: destination signs, fingerposts (bigger, numbered, readable from both sides), bike
  parking and scenic pull-offs, Reserve Road, deer crossing, viewpoint signs (now dark boards with cream lettering),
  the Grandmother Oak plaque, the ranger's log, and the reserve guidelines (three pictogram lines).
- **Interpretive panels**: a large pictogram, a bold title on a green band and two short lines instead of a paragraph.
### Looks
- **Real wood**: a scanned CC0 plank texture (Poly Haven *Weathered Brown Planks*) on the kiosk, signs, decks, the blind,
  benches and bridges, with real-scale UVs so grain runs along each piece (posts vertical, boards horizontal).
- **Water that reads as water**: Willow Creek and Cattail Pond are dark and tea-coloured with ripples; the creek's
  ripples **flow downstream** and the pond's drift. Before, a near-mirror surface reflected the sky's blue zenith.
- **Creek banks** in the open grow tall grass and sedge; ferns keep to shade (they carpeted sunny banks before). Cut
  banks and earth paths are brown rather than orange; short grass tufts are narrower and taller.
- **Broadleaf crowns** are rounder and fuller (steeper limbs, round clusters, crown filled between the limb tips, a
  raised top), so distant woods read as temperate broadleaf, not savanna. Tree positions are unchanged.
- **Birch** trunks are grey-white instead of glowing.
- A **trail register** on a post beside the kiosk.
- Environment version 14; art import version 4 (**rebuild the slice**).

## [1.2.0] Habitat Kit
An environment pass that keeps every gameplay system as it is: better rocks now, and a supported route to
high-quality free trees, grass and ground. Store assets can't be committed to this public repository, so they are
imported into your own project and WildLens places them. See `Documentation~/EnvironmentMigration.md` (audit, free
asset list, plan, before/after).
- **Bedrock outcrops**: two scanned CC0 rock faces (Poly Haven *Rock Face 01/02*) break the surface on the steepest
  slopes and on the ridge's flanks, turned to face downhill and set into the ground.
- **Environment Kit** (`WildLens ▸ Environment Kit`, or the builder window): free nature assets in your project sorted
  by ecological role (conifer, broadleaf, creek-side, old-growth, young trees; shrubs, ferns, rocks, boulders, logs,
  stumps; five terrain layers; six grass and flower slots). The builder puts each where it belongs: creek-side trees
  along Willow Creek, giants in the grove, saplings at woodland edges, layers and grass in their painted zones. Kit
  trees get trunk and crown colliders and wildlife data automatically, keep their own LODs and wind, and never mix
  with generated trees.
- **Scan Project for Nature Assets** fills the kit from what you've imported, including the trees, grass and layers
  inside a demo scene's terrain (Unity's free *Terrain URP Demo Scene* and *Terrain Sample Asset Pack* are the
  recommended start). It skips stylised, low-poly and wrong-biome art and anything that would render pink in your
  render pipeline, and says why.
- An empty kit changes nothing: the reserve rebuilds exactly as before.
- **Compile check fixed**: `Tools~/CompileCheck/Check.csproj` had never been committed (ignored by `*.csproj`).
- Environment version 13; art import version 3 (**rebuild the slice**).

## [1.1.0] More Life
Biodiversity pass: the reserve goes from five species to fifteen, all as data on the 1.0 species framework. No new AI.
- **New species** (generic placeholder bodies, data rhythms): Great Blue Heron and Raccoon (now always on), Wild Turkey,
  Barred Owl, Groundhog, Northern Cardinal, Blue Jay, Red-winged Blackbird, Virginia Opossum, Coyote.
- **Where they live** (residents): Cattail Pond gains a heron, red-winged blackbirds and a raccoon; the Old-Growth Grove
  a barred owl and blue jays; the Meadow Blind cardinals and a turkey flock; the Ridge Overlook a lone coyote; the
  creek-side residence an opossum; the meadow-edge residence a groundhog.
- **Small-bird flight**: flight altitude and circle radius are now species data (`flightAltitude`, `circleRadius`), so
  songbirds flit 3-7 m up between perches and the owl glides low, while the hawk still soars at 20-38 m.
- **Field marks** on generic bodies: crests (cardinal, jay), face masks on birds, red epaulettes (blackbird), a coloured
  head (turkey's bare head with wattle, opossum's pale face), owl-sized heads, tails in body colour.
- Pond and grove arrival notes mention their new residents. Environment version 12 (rebuild the slice).

## [1.0.0] Field Season
The first complete release of Willow Creek Nature Reserve: polish, consolidation and a handoff for future contributors.
No new gameplay systems.
### Reserve Road & riding
- Road widened to 4.2 m with smoother, longer curves (relaxed after routing) and **graded**: the road bed follows a
  smoothed ground profile (cut and fill, blended into the land; skipped at creek crossings; backed up, repeatable).
- **Sight lines**: trees stay 7 m and shrubs 3.5 m back from the road edge.
- **Road posts** (white with the reserve's green band) every ~60 m, alternating sides; wider road bridges.
- **Riding comfort**: the bike's height and pitch are damped like soft suspension, so bumps no longer jolt the view.
  Top speed unchanged.
### Destinations
- Planked decks on the jetty, the overlook and the blind floor.
- Signature details: a viewing telescope and a summit cairn on the ridge; the rangers' canoe by the jetty; the roped
  **Grandmother Oak** with its plaque in the grove; a bird feeder in front of the blind's window.
### Trees & vegetation
- Young trees where woodland thins into grassland (softer edges); a **pine** form (tall bare trunk, broad crown) beside
  the firs; two more leaf tints (blue-green, fresh yellow-green) for canopy variety.
### Wildlife expansion framework
- **Generic placeholder bodies** from data (`BodyPlan`: quadruped, wading bird, ground bird, perching bird).
- **Data-driven daily rhythms** (`DailyRhythm` on each species).
- **Water** habitat hotspots at the pond.
- Worked examples behind **Preview expansion species**: Raccoon and Great Blue Heron.
- `Documentation~/WildlifeExpansion.md`: framework, ten planned species, asset research.
### Release readiness
- `Tools~/CompileCheck/`: compile the package in five configurations without Unity.
- `CONTRIBUTING.md`; README rewritten as an index; AGENTS.md reorganised as a handoff (what to build next, what not).

**Rebuild the slice after updating.**

## [0.9.0] Time of Day
### Added
- **A day in the reserve**: dawn, morning, midday, golden hour and dusk, about sixteen real minutes of daylight
  (configurable), with night passing as a short time-lapse. The sun crosses from east to west; the two sky images swap
  and rotate to match it; sun colour, ambient light, fog (thick at dawn), sky tint and URP grading follow hand-tuned
  keys. **Hold T** (standing still) to let time pass. The HUD shows the period and time; each change of light brings a
  short field note about what the reserve is doing.
- **Dawn mist** over the pond and the creek's low reaches, burning off by mid-morning; a faint haze at dusk.
- **Wildlife rhythms by placement**: when the period changes, animals' home ranges move to where their species is
  at that time, and they travel there themselves. Deer: meadows morning and golden hour, woodland cover at midday,
  water at dusk. Cottontails: meadow edges at dawn and dusk. Foxes: hunting meadow edges at dawn and dusk. Hawks:
  over the ridge in daylight, on meadow snags at the ends of the day. Squirrels busiest morning and late afternoon.
  Behaviour weights shift too (deer bed down at midday).
- **Trail hierarchy**: the **Reserve Road**, a 3.4 m gravel ring from the trailhead through every destination's
  pull-off, gently graded and kept outside the Meadow Loop; destination paths become narrow **footpaths**. The bike
  rides at its usual pace on the road, gently on footpaths (≈22 km/h) and across country (≈20 km/h). Road bridges
  are wider. The HUD says what you're riding on.
- **Reserve identity**: it is now **Willow Creek Nature Reserve** (Willow Creek Conservation Trust, est. 1962). One
  sign standard everywhere (stained boards, cream lettering, green rule), reserve guidelines at the trailhead, a
  Reserve Road sign, and best times on every destination sign.
- Trailhead board: a live **"Now"** line (period, time, what the wildlife is doing), each destination's **best
  times**, the road on the map, and a taller board to fit them.
### Changed
- **Trees rebuilt with structure**: conifers grow branch whorls up a tapering trunk (ragged, cone-shaped crowns,
  a slim dark core, dead lower branches); broadleaf trees come in three forms: spreading (short trunk, 3–5 limbs,
  wide low crown), ovate (taller, steeper limbs), and multi-stem birch (pale stems, light crowns). Limbs show under
  and between the leaf clusters.
- The Old-Growth Grove is twice as dense, for real shade.
- The Ridge Overlook prefers a view toward the sunset.

**Rebuild the slice after updating.**

## [0.8.0] Ecosystem Pass: a living landscape
### Added
- **Willow Creek**, the reserve's backbone: a stream from the ridge's flank to Cattail Pond, routed downhill and
  carved into the terrain (repeatably, backed up), with flowing water, footbridges wherever the loop or a track
  crosses it, stones in the bed and rocks in the cut banks, a fallen log, reeds, ferns crowding the banks,
  riparian woodland, brook sounds and creek-side deer. Drawn on the trailhead map.
- **Bike pull-offs**: gravel parking short of each destination (out of the residents' earshot) and scenic pull-offs
  with a bench on longer tracks. Riding into one, the HUD suggests parking and says how far the walk is.
- **Environmental storytelling**: interpretive panels (ridge, pond, grove, meadow, creek), numbered track marker
  posts, a deer-crossing sign, nest boxes in the grove, a ranger's log in the blind.
- **Encounter variety by placement**: deer that drink at the creek, deer whose range straddles a track, cottontails
  on the meadow's brushy edge, two hawks on the ridge, more squirrels and a fox in the grove.
- Ridge wind ambience; purple asters in the meadows; a realtime reflection probe so the pond reflects its banks.
### Changed
- **Meadow Blind rebuilt**: roomier (4.2 × 3.2 m), levelled pad and plank floor, centred 1.2 m doorway reached by
  duckboards, a long window usable standing *and* crouched, brush screening, grass kept out. It sits inside the
  meadow edge with the wildlife hotspot 14 m in front, so animals come close. Inside, the HUD shows **HIDDEN** and
  explains the blind on first entry.
- **Habitat density**: trees now form dense woodland, clumped stands, quiet transition groves and open grassland
  with the odd lone tree, instead of an even scatter. Ferns concentrate under dense canopy and along the creek.
- **Destination identity**: the pond has an irregular shoreline with coves, thick reeds and lily pads; the grove has
  a closed canopy of big trees, mossy rocks, fallen timber and ferns; the ridge is a rocky, windy crest.
- Terrain grass leans with slopes and scatters less evenly on Unity 2022.2+ / 6.
- The trail bike rides over bridge decks.

**Rebuild the slice after updating.**

## [0.7.1] Fixes from the first reserve playtest
### Fixed
- **Couldn't get into the Meadow Blind:** its doorway was 0.7 m, narrower than the player's collision capsule.
  Now 1.1 m.
- The "R Ride the trail bike" prompt sat on top of the viewfinder's zoom scale; it now hides while the camera is up.
- The bike's headlight blocked the middle of the riding view; it is smaller and lower.

**Rebuild the slice after updating** (the blind is built by the scene builder).

## [0.7.0] The Reserve
### Added
- **Reserve destinations** beyond the Meadow Loop, chosen from the terrain: Ridge Overlook (deck, perch snag;
  hawks), Cattail Pond (water, reeds, jetty; deer), Old-Growth Grove (giant trees, clearing; squirrels and a fox),
  Wildflower Meadow Blind (a wooden hide that conceals you; deer and rabbits). Each has resident wildlife, a sign,
  a viewpoint and an arrival field note.
- **Reserve tracks** routed around steep ground from the loop to each destination, with fingerposts; scenery
  extends along them. The pond basin and overlook hill are shaped when the land doesn't provide them (backed up,
  repeatable).
- **Trail bike** at the trailhead: R to ride and to park; gentle handling, fastest on tracks; stopped in the saddle
  you can still raise the camera. Its engine is audible to wildlife from far off.
- **Trailhead kiosk** with a live information board: version, release, features, recent changes, reserve status
  (species, journal progress, trails, viewpoints, destinations with distances) and a heads-up topographic map.
- `Documentation~/ReserveExpansion.md`: the plan for growing the reserve.
### Changed
- The WildLens fallback terrain grows to 900 m (your own terrain is never resized).
- Procedural trees stop drawing when tiny on screen (performance for the larger world).
### Fixed
- The black slab overhead near some trees was the solid underside of a conifer's lowest tier: tiers are now open
  underneath and the lowest one clears head height.

**Rebuild the slice after updating.**

## [0.6.0] Presentation pass
### Added
- **Leaf-card trees:** broadleaf canopies and conifer tiers are now built from alpha-cut leaf and needle-branch cards
  (CC0, composed from Poly Haven tree atlases) over darker core volumes, with wind sway. Replaces smooth blobs/cones.
- **Stale-scene detection:** scenes built by an older package show a notice on Play, a warning in the builder window
  and a line in the F3 overlay.
- **Animal Model Setup** (WildLens ▸ Animal Model Setup): one-click Animator Controller and prefab for an animated
  animal model, assigned to the species.
- `AGENTS.md`: project vision, design philosophy, architecture and history for anyone (human or AI) working on it.
### Changed
- Terrain reads as grass instead of bare soil at distance: green and golden-olive grass tints in ~20 m patches at
  unrelated tile sizes (less visible tiling); terrain grass draws to 150 m.
- Deer placeholder silhouette (chest, haunch, forward neck, short broad ears, dark shins); lying deer tuck their legs.
- Camera mode hides the control hints (they overlapped the viewfinder); stronger focus-lock snap; photo prints have
  a drop shadow and a slight tilt.
### Removed
- Forest Ground 01 terrain texture (no longer used; −4.4 MB).

**Rebuild the slice after updating.**

## [0.5.0] Zoom you can see · Art direction
### Fixed
- **Zoom readouts changed but the view did not.** Two causes: (1) in scenes upgraded at runtime, ZoomLens cached a
  null CameraModeController in Awake (added before it), so it never applied camera-mode FOVs; (2) scrolling while the
  camera was lowered only "preselected" a level by design, changing the UI but not the view. Zoom input now raises
  the camera, references resolve lazily, and the FOV is applied late and re-asserted right before rendering, with
  physical-camera, custom-projection, orthographic and Cinemachine overrides detected and corrected.
### Added
- Render-time camera probe in the F3 overlay; F4 zoom self-test (measures rendered projection and subject size at
  every level, saves screenshots, PASS/FAIL).
- Scanned CC0 art from Poly Haven shipped in `Art~`: two HDRI skies with sun alignment, five terrain materials,
  bark and rock surfaces, ferns, shrubs, mossy rocks, boulder, stump, fallen trunk.
- Sky moods (late morning / golden hour), HDRI ambient and reflections, fog matched to the sky's horizon.
- Camera presentation: autofocus hunting and lock, focus-confirm dot, focal/aperture line, "keeper" prints and chime
  for Excellent photos, photos develop in.
- Compositions: Forest Glade clearing and Trail Bend sight line.

## [0.4.0] Visual quality & camera
### Fixed
- **Zoom did nothing with Input System 1.9+ (Unity 6).** Scroll was divided by 120, but the Input System now reports
  ±1 per notch by default. Zoom now uses wheel direction only, so every backend and setting works. Added E/Q keys.
### Added
- F3 camera diagnostics overlay (scroll values, zoom inputs and why any were ignored, FOV, competing cameras/FOV writers).
- Lens feel: depth of field with focus pulls (URP), focus-lock beep, big zoom readout per step, breathing, pan
  inertia, raise roll.
- Painted terrain (5 generated PBR-style layers), terrain grass/flowers/ferns with wind, foliage wind sway.
- Scenic viewpoints, rock outcrops, stumps, richer tree silhouettes.
- Lighting/sky/haze rework, shadow quality, URP colour grading.
- Comparison-shot capture tool; `Documentation~/VisualRoadmap.md` (asset sources and licensing).

## [0.3.0] Encounters
### Added
- Crouch (C toggle; Left Ctrl hold): half speed, half footfall noise, smaller silhouette, lower head hidden by
  cover, smooth camera transition, headroom check.
- Observation identification: hold an animal in the viewfinder to identify it and add it to the journal without
  a photo; chime and notice.
- Framing assistant: viewfinder brackets track the subject nearest centre and show the grade a photo would get.
- 16x zoom step (about 410 mm equivalent), focal-length readout, zoom preselect while the camera is lowered,
  vignette that deepens with zoom.
- Habitat hotspots (meadows, brush piles, mature trees, dead-snag perches) with visible landscape cues; species
  favour them; derived automatically in scenes built without them.
- "Why nothing was identified" feedback on photos.

### Changed
- Photo grading uses linear frame fill with clear thresholds instead of area against per-species ideals; thirds
  composition can be Excellent. Much higher success rate at every zoom level.
- Awareness tuned for "approachable realism": standing still always lets animals settle; shorter flee runs and a
  longer look back; softer post-flee memory; per-species tuning (see Documentation~/Systems.md).
- Species assets upgrade automatically (data version 3); journal save version 3.

## [0.2.0] Camera & Wildlife Awareness
### Added
- Camera Mode (hold RMB): reduced movement and look speed, narrower FOV, viewfinder overlay, raise transition with
  sound and a small camera dip, subtle handheld sway. Photos can only be taken in camera mode.
- Zoom lens: 1× / 2× / 4× / 8× steps on the mouse wheel with smooth transitions and a level indicator.
- Photo feedback: synthesised shutter sound, shutter blackout effect, preview card with species and quality,
  green focus confirmation in the viewfinder.
- Wildlife awareness: Calm → Aware → Alert → Fleeing driven by distance, player noise, line of sight and memory,
  with per-species radii and patience. The shutter is audible to nearby animals.
- Behaviour states: rabbit Sitting/Hopping, deer Walking/Running, hawk Flying/Landing/Perched/TakingOff.
- Photo quality classes Poor / Good / Excellent with a one-line hint.
- `Documentation~/Systems.md`.

### Changed
- Numeric photo scores removed; the journal shows the best photo's quality class.
- Existing species assets and journal saves are upgraded automatically.

## [0.1.0] Vertical slice
- Trail loop builder, five species, photography, field journal.
