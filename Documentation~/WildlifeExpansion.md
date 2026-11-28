# Wildlife expansion

WildLens 1.0 ships five species and a framework for more. A new species is **data, not code**: a Species Definition
asset (behaviour, senses, habitat, daily rhythm, generic placeholder body), optionally replaced by a purchased model.
This document is the plan for growing the reserve's ecosystem without growing its mechanics. The 1.7 candidate list
(crow, chipmunk, goose, duck, turtle, woodpecker, vulture, skunk, dove and robin, bat, bear), each with its place and hours,
is in [WildlifeCampaign.md](WildlifeCampaign.md) §2.

## Principles
1. **Every species earns its place in the reserve's story.** It should live somewhere specific and be active at a
   specific time, so it gives the player a new reason to be in a place at an hour ("herons at the pond at dawn",
   "the owl calls from the grove at dusk").
2. **Encounters from placement and rhythm, not new AI.** Use the existing brains (`GroundAnimal`, `FlyingAnimal`),
   awareness, hotspots, residences and `DailyRhythm`. If a species seems to need new behaviour code, first ask whether
   a hotspot, a rhythm or behaviour weights can express it.
3. **Recognisable at a distance, even as a placeholder.** Silhouette, colour blocks and field marks (mask, ringed tail,
   red epaulettes) matter more than detail.
4. **Licence safety is absolute.** Only CC0 art in the repo; store assets are imported locally (see below).

## The framework (implemented in 1.0)

| Piece | Where | What it gives a new species |
|---|---|---|
| `PlaceholderBody.Generic` + `BodyPlan` | `Scripts/Data/SpeciesDefinition.cs`, `PlaceholderAnimalBuilder.BuildGeneric` | A recognisable body from proportions: **Quadruped**, **WadingBird**, **GroundBird**, **PerchingBird** (flight-capable, scaled), with options (face mask, ringed tail, tail length/thickness, beak/snout, ears) |
| `DailyRhythm` | `SpeciesDefinition.rhythm`, read by `WildlifeSchedule` | Where it spends dawn / morning / midday / golden hour / dusk / night: Home, Meadow, MeadowEdge, Brush, Woodland, Water, Ridge, Perch |
| `HotspotType.Water` | pond shallows (builder) | Water-edge homes (herons, raccoons); combine with Meadow/BrushPile/MatureTree/Perch |
| Behaviour weights | `SpeciesDefinition.behaviors` + `WildlifeSchedule.Bias` | Grazing, foraging, resting, sitting, perching, climbing... weighted by time of day |
| Residences | `WildlifeResidence` (builder) | Pin a species group to a specific place (a log pile, a snag) |
| Real models | **WildLens ▸ Wildlife Pack Setup** (many species, 1.6) / **Animal Model Setup** (one) | Animator Controller + correctly scaled prefab from any animated model; stand-ins and tints |

**Worked examples:** tick **Preview expansion species** in the builder settings and rebuild. `Raccoon` (generic
quadruped with mask and ringed tail; woodland by day, creek and pond at dusk and night) and `Great Blue Heron` (wading
bird; always at the water, wary, motionless hunting) are defined in `SpeciesDefaults.CreateExpansionPreview()`. Copy
their pattern.

### Adding a species: steps
1. Create a definition (copy an example in `SpeciesDefaults`, or **Create ▸ WildLens ▸ Species Definition**). Give it
   a stable, never-changing `speciesId` (save files use it).
2. Body: `Placeholder Body = Generic`, fill the `Body Plan`. Check the silhouette at 8× zoom.
3. Movement: `Ground` (mammals, waders, turkeys) or `Flying` (songbirds, owls; with a `PerchingBird` body).
4. Senses: start from the most similar species' awareness values; tune so patience always works.
5. Habitat: `habitat`, `preferredHotspots`, `hotspotAffinity`; add a `WildlifeResidence` in the builder if it should
   live somewhere specific.
6. Rhythm: enable `Daily Rhythm` and choose a place per period.
7. Photography: `photoSizeScale` (small animals < 1), `clearPhotoDistance`.
8. Add it to `SpeciesDatabase` (the journal picks it up automatically), rebuild, play a whole day.
9. Later: import a free pack and run Wildlife Pack Setup (or Animal Model Setup for one model). **A species also needs
   an entry in `WildlifePackSetup.Names`** (its exact names and stand-ins) or no pack model will ever be matched to it;
   check `Reserved` too (woodpecker, hummingbird, duck, mallard, goose and swan are held back for their own species).

## Planned species (roadmap)

Ordered by how much each adds to the reserve's story for the effort. All fit the existing brains.

| # | Species | Body | Habitat & hotspots | Rhythm | Story it adds | Notes |
|---|---|---|---|---|---|---|
| 1 | **Great Blue Heron** | WadingBird (preview) | Pond shallows, creek mouth (Water) | Water all day; most active dawn | "The pond at dawn" gains a centrepiece | Flushes by walking for now; a take-off/flight-away would need FlyingAnimal support for ground birds |
| 2 | **Raccoon** | Quadruped, mask, ringed tail (preview) | Creek banks, mature trees (Water, MatureTree) | Woodland by day; water at dusk and night | Gives dusk at the creek a reason | Climbing behaviour exists (squirrel code); test with raccoon scale |
| 3 | **Wild Turkey** | GroundBird | Meadow edges, open woodland (Meadow, BrushPile) | Meadows morning and evening; woodland midday | Flocks (group size 4–8) crossing the Reserve Road | Fan tail via `tailLength`; strut display is a future behaviour weight |
| 4 | **Barred Owl** | PerchingBird (round head: add `headSize`) | Old-Growth Grove (MatureTree, Perch) | Perched by day (Resting), hunting meadow edges at dusk | The grove at dusk; a reason to stay late | A call (procedural hoot, like `AmbientSound`) would sell it |
| 5 | **Groundhog** | Quadruped (short legs, chunky) | Meadow edges by the road (BrushPile) | Meadow edge morning and late afternoon; burrow (Home) otherwise | Sentinel posture (Sitting weight) beside the road | A burrow mound prop at its residence |
| 6 | **Red-winged Blackbird** | PerchingBird (small) | Pond and creek reeds (Water, Perch) | Reeds all day; noisy at dawn | Life in the reeds; the first songbird | Needs perch points on reeds (`EnvironmentFeature` on reed clumps) |
| 7 | **Northern Cardinal** | PerchingBird (small, red) | Brush and the blind's feeder (BrushPile) | Feeder at dawn and dusk | Colour, and the blind feeder pays off | Add an `EnvironmentFeature` perch to the feeder |
| 8 | **Blue Jay** | PerchingBird (small, blue) | Mature trees, grove (MatureTree) | Active morning | Noise and colour in the woods | |
| 9 | **Coyote** | Quadruped (like fox, larger, grey) | Open grassland, far from paths (Meadow) | Dawn and dusk only; distant otherwise | Rare, wary, memorable | Very large awareness radius; one individual |
| 10 | **Virginia Opossum** | Quadruped (pale face, bare tail) | Woodland, creek (Woodland, Water) | Night and dusk | Night photography, if night becomes playable | Low priority until night is more than a time-lapse |

All ten above shipped in 1.1. **1.4 added two creek specialists**, because the creek itself (not the pond, not the
woods) had no wildlife of its own:

| Species | Body | Where | Rhythm | Why it earns its place |
|---|---|---|---|---|
| **Belted Kingfisher** | PerchingBird (big head, crest), low flight 1.5–4 m, patrols 8–16 m | Creek Ford only (residence; `groupCount` 0) | Home all day | Moving water plus a fast, wary bird on a snag: a hard, rewarding photograph |
| **Spotted Sandpiper** | WadingBird (small, short legs) | The ford's gravel bar | Home all day | A patient, close subject on stones and gravel; a reason to sit at the ford |

**What worked in the expansions (lessons):** each species was justified by a *place* (heron → pond shallows, owl →
grove, kingfisher → riffle), not by a count. Data-only bodies kept the cost to an afternoon each. Residents that
stay home (`RhythmPlace.Home`) make a place reliably "about" its animal; species that commute make the clock matter.
Use both deliberately.

**Next candidates, only with a place:** wood duck and painted turtle need a marsh with loafing logs (the wetland
boardwalk); a green frog chorus needs evening audio, not a body; a pileated woodpecker needs big dead trees in the
grove. Swimming locomotion would be a new system: avoid until a marsh makes it essential.

**Not planned:** predators actively hunting other species, nests and breeding, population dynamics. These are
simulation systems with little photographic payoff (see AGENTS.md: what not to build).

## Assets: what exists and how to use it

Researched November 2026. **Re-check licences on each listing before buying or importing.**

**CC0 (could be committed):** none found that are realistic, rigged and animated for these species. Generic
placeholder bodies are the committed baseline.

**Paid / royalty-free (import locally, never commit):**
* Raccoon: Asset Store *Raccoon Animated* (~$25, many animations).
  [listing](https://assetstore.unity.com/packages/3d/characters/animals/raccoon-animated-176750);
  *Realistic Raccoons and Red Panda*.
  [listing](https://assetstore.unity.com/packages/3d/characters/animals/realistic-raccoons-and-red-panda-126443)
* Heron: Asset Store *Heron* (low-poly, 13 animations; check licence and style).
  [listing](https://assetstore-fallback.unity.com/packages/3d/characters/animals/birds/heron-298519);
  Blender-rigged grey heron walk/flight models on Superhive (Blender format; check Unity export and licence).
  [walk](https://superhivemarket.com/products/grey-heron-bird-walk-animated-rigged-for-blender/docs)
* Owls: *Low Poly Bird: Predator Pack* (owls, ~$25).
  [listing](https://assetstore-fallback.unity.com/packages/3d/characters/animals/birds/low-poly-bird-predator-pack-204079)
* Broad packs (raccoon, owl, coyote, turkey): *Quirky Series* (stylised; large bundle).
  [listing](https://marketplace.unity.com/packages/3d/characters/animals/quirky-series-209771). Stylised art would
  clash with the photographic reserve; only consider it if the whole cast goes stylised.
* Budget: *Low Poly Wild Animals Pack (Animated)* includes a raccoon (~$5).
  [listing](https://assetstore-fallback.unity.com/packages/3d/characters/animals/mammals/low-poly-wild-animals-pack-animated-296252)
* Free-to-download but **not CC0** (custom royalty-free licences; fine for a local project, not for this repo):
  a Free3D animated game raccoon. [listing](https://free3d.com/3d-model/animated-game-raccoon-1556.html)

**Recommendation:** keep realism consistent. Buy realistic models species by species, starting with the deer (still
the most-photographed animal), then heron and raccoon (the expansion's anchors). Integrate each with Animal Model
Setup in an afternoon; the species asset, rhythm and habitat stay the same.
