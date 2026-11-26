# WildLens visual roadmap & asset recommendations

Updated for 1.2.0. Licensing notes checked November 2026; **re-check each listing before buying or importing**.
For the environment audit, the free asset list and the migration plan, see **`EnvironmentMigration.md`**.

## Integrated in 1.2.0
| Area | Asset(s) | Result |
|---|---|---|
| Geology | Poly Haven *Rock Face 01* and *Rock Face 02* (CC0, 20k/30k triangles, 1k maps, 5.6 MB) | Bedrock outcrops on steep slopes and the ridge's flanks |
| Trees, grass, ground (user's project) | **Environment Kit**: Unity's free *Terrain URP Demo Scene* (SpeedTree trees) and *Terrain Sample Asset Pack* (instanced detail vegetation) are the recommended start | Placed by ecological role; never committed (Asset Store EULA) |

## Integrated in 0.6.0
| Area | Asset(s) | Result |
|---|---|---|
| Tree canopies | Leaf and twig atlases of Poly Haven *Island Tree 01* and *Fir Tree 01* (CC0), recomposed into two card textures | Leaf-card broadleaf crowns and needle-branch conifer tiers replace blobs and cones |
| Ground colour | Existing *Leafy Grass* scan, two tints | Green/golden grass instead of soil-coloured ground; fewer visible repeats |

## Integrated in 0.5.0 (all CC0, Poly Haven, see `Art~/CREDITS.md`)

| Area | Asset(s) | Result |
|---|---|---|
| Sky | Kloofendal 48d Partly Cloudy, Kloppenheim 06 (pure-sky HDRIs, 2k) | Real cumulus skies; image-based ambient and reflections; sun light aimed at the sun in the image; fog = measured horizon colour |
| Ground | Leafy Grass, Forrest Ground 01 (removed in 0.6), Forest Leaves 02, Dirt Floor, Aerial Rocks 02 | Photographed terrain layers with normal maps, painted by ecology (same rules as before) |
| Trunks & rocks | Bark Brown 02, Pine Bark, Mossy Rock | Scanned bark on every trunk; scanned rock surface on generated rocks |
| Undergrowth | Fern 02, Shrub 02/04 | ~160 scanned ferns under woodland near the trail; scanned shrubs replace placeholder bushes within 30 m of the trail |
| Geology & deadwood | Rock Moss Set 01/02 (split into pieces), Boulder 01, Tree Stump 01, Dead Tree Trunk 02 | Outcrops, landmark boulders, stumps and fallen logs |

Package size grew by ~72 MB (2k skies and ground, high-poly logs/boulder). If that's a problem, drop the boulder
and fallen trunk (13 MB) or ship 1k ground diffuse maps.

### Assessed and **not** used
* **Poly Haven trees** (Fir Tree 01, Pine Tree 01, Tree Small 02, Island Tree 01, saplings): film-quality, **2–17 million
  polygons each** (Pine Tree 01 is a 650 MB FBX). Unusable as-is in a game: they'd need decimation plus leaf-card
  rebuilds and LODs, which is an art job. Trees remain the biggest visual weakness (see below).
* **Grass Medium 01**: 1.6 M polygons; the terrain grass system is far cheaper.

## Wildlife models (highest impact remaining). Purchases are yours to make.

### Free options assessed for 0.6.0 (none integrated)
| Option | Licence | Verdict |
|---|---|---|
| OpenGameArt *Deer Low Poly rigged* ([link](https://opengameart.org/content/deer-low-poly-rigged)) | CC0 | Safe, but low-poly and lightly animated: clashes with photographic ground and sky; would look worse in a photo than a fixed-up placeholder. |
| Quaternius *Animated Animals* ([link](https://quaternius.itch.io/lowpoly-animated-animals)) | CC0 | Farm animals only; no target species. |
| Asset Store *Animals FREE* by ithappy ([link](https://assetstore.unity.com/packages/3d/characters/animals/animals-free-260727)) | Asset Store EULA | Includes a deer, but stylised; EULA allows use in your game, **not** redistribution, so it cannot be committed to this public repo. You may import it locally. |
| Sketchfab toon deer ([example](https://sketchfab.com/3d-models/toon-deer-animated-70d87082ee584e9896aa6b06cd764e77)) | varies (often CC-BY / non-commercial) | Licences vary per model; stylised. Not used. |

**Conclusion:** there is no free, licence-safe, realistic, animated deer/fox/hawk/rabbit/squirrel for Unity. Free
low-poly animals would make the game look *less* coherent next to the scanned environment. Licensed realistic
models (below) are the route; `WildLens ▸ Animal Model Setup` makes integrating one a few minutes' work.

Budget low-poly alternative (consistent style for all five, if you prefer stylised): *Animated Forest Animals Pack*
(itch.io, deer/fox/rabbit/squirrel, ~$13) ([link](https://bundlesmith.itch.io/animated-forest-animals-pack-low-poly-stylized-models)),
or *Lowpoly Forest Animal Pack* (deer stag/doe/calf, 21 animations, ~$20) ([link](https://assetstore.unity.com/packages/slug/104338)).
Going stylised would mean restyling the environment too; not recommended.

Nothing suitable is CC0. The licensed routes, in priority order. **Before buying, check on the store page:** species,
render pipeline (URP), Unity 6 compatibility, and an explicit animation list (idle, walk, trot/run, graze/eat, look,
alert; for birds: fly loop, glide, take-off, land, perch idle).

**1. Deer**
* Asset Store *Realistic Deer* (~$70): Unity 6 / URP compatible per its listing; confirm species and animations.
  [listing](https://assetstore.unity.com/packages/3d/characters/animals/realistic-deer-214765)
* A realistic rigged deer on Fab with 22 actions, made in Unity 6 (URP/HDRP); confirm species and licence terms.
  [listing](https://www.fab.com/listings/34f1225e-18aa-4ed0-94bd-0c6eb3f3327a)
* KasitStudio *Deer Pack*: red deer (not white-tailed) with separate Built-in/URP/HDRP Unity packages.
  [forum post](https://forums.unrealengine.com/t/kasitstudio-deer-pack-realistic-rigged-animated-red-deer-stag-7-variants/2856904)
* If you switch to red deer, rename the species and field notes in its species asset; that's data, not code.

**2. Fox**
* *Animalia – Fox* (~$100): realistic male red fox, 60+ animations (root-motion and in-place), PBR, all pipelines.
  **Best fit.** [listing](https://assetstore.unity.com/packages/3d/characters/animals/animalia-fox-63884)
* *Fully Animated Foxes* (~$90): URP/HDRP only, includes red variants with real-time fur.
  [listing](https://assetstore.unity.com/packages/3d/characters/animals/mammals/fully-animated-foxes-188329)

**3. Hawk**
* No confirmed red-tailed hawk for Unity was found. Closest: Asset Store *Animated Eagles Pack v3* / *Animated Bald Eagle*
  (Standard EULA; flight loops). [pack](https://marketplace.unity.com/packages/3d/characters/animals/birds/animated-eagles-pack-v3-92571)
* A Eurasian goshawk with 28 animations exists on CGTrader/Fab but is packaged for Unreal; confirm the licence
  allows Unity use and that an FBX is included before buying.
  [CGTrader](https://www.cgtrader.com/3d-models/animal/bird/eurasian-goshawk-bird)
* Retexturing an eagle or buzzard-type raptor toward red-tailed colouring is a reasonable stopgap.

**Integration (any pack), 0.6+:** import the pack locally (don't commit it to this repo), open **WildLens ▸ Animal
Model Setup**, pick the species asset and the model, check the auto-classified clips, set the real size, Create. It
builds an Animator Controller (`Speed` blend tree, `Activity` transitions, `GaitPhase`), wraps the model in a
correctly scaled prefab with root motion off, and assigns it to the species. Expect an hour of tuning per species
(clip choice, yaw, size). **One beautiful deer first.**

## Trees (highest remaining environment weakness)
* **Free first (1.2):** Unity's *Terrain URP Demo Scene* ships custom SpeedTree trees with LODs and wind. Import it,
  then **Scan Project for Nature Assets**; the Environment Kit places them by role with colliders and wildlife data.
* Paid, game-ready, URP, with LODs and wind: SpeedTree or Asset Store nature packs that ship URP shaders and LODs.
  Drop them into the kit's roles (or scan); placement, wildlife data and wind are automatic (the WindZone drives them).
* The free Unity *Terrain Sample Asset Pack* provides URP terrain materials/details (ODbL height data).

## Remaining visual weaknesses (honest list)
1. **Animals** are primitive placeholders: the biggest gap.
2. **Trees.** 0.9 rebuilt them with structure (conifer branch whorls on tapering trunks; spreading, ovate and
   birch broadleaf forms with visible limbs). Remaining: bark detail on limbs, leaf-card lighting (translucency), real
   LODs/billboards for distance, and more species silhouettes (oak vs maple vs aspen). A game-ready tree pack would
   still be a step up.
3. **Texture tiling** is visible on large open ground at distance (Unity terrain has no anti-tiling; a terrain shader
   with stochastic sampling or macro variation would fix it).
4. **Grass** doesn't hide animals from the photo check (no colliders).
5. No volumetric light/fog in URP; no time-of-day transition between the two moods.
6. Terrain shape on the fallback terrain is gentle noise; a hand-sculpted valley/ridge would make stronger vistas.

## Suggested order
1. Deer model (1–2 days) → fox → hawk.
2. Game-ready trees with LODs (1 day once purchased).
3. Anti-tiling terrain shader or macro variation texture.
4. Hand-sculpt the terrain around the overlook and lookout.

## Unity 6 investigation (0.8)

Looked for Unity 6 features with a *visible* payoff for WildLens. Findings:

| Area | Unity 6 option | Verdict |
|---|---|---|
| Water | URP has no built-in water system (HDRP's Water System is HDRP-only) | Kept custom water; added a **realtime reflection probe** (rendered once on load) at the pond so it reflects banks and trees, not just the sky. Works on every version. **Done.** |
| Terrain details | `DetailPrototype.alignToGround`, `positionJitter` (2022.2+) | Grass leans with slopes and scatters less grid-like, most visible on creek banks and hills. Set by reflection so 2021.3 still compiles. **Done.** |
| Draw-call cost | **GPU Resident Drawer** (URP, Forward+) | The biggest *performance* win for the reserve's ~2× trees (thousands of foliage renderers). Not enabled automatically because it changes project-wide settings: URP asset → GPU Resident Drawer: *Instanced Drawing*; renderer → *Forward+*; Project Settings → Graphics → *BatchRendererGroup Variants: Keep All*. **Recommended manual step** if frame rate drops. |
| GI | **Adaptive Probe Volumes** (URP 17) | Would add soft bounce light under the grove canopy, but needs a bake, which conflicts with "rebuild, don't hand-edit". Revisit with time of day. |
| Upscaling | **STP** spatial-temporal upscaler | Performance on high-res displays; no visual change at native. Optional. |
| Volumetrics | Not in URP 17 | Light shafts would need a custom renderer feature. Deferred. |
