# WildLens visual roadmap & asset recommendations

Updated for 0.5.0. Licensing notes checked November 2026; **re-check each listing before buying or importing**.

## Integrated in 0.5.0 (all CC0, Poly Haven, see `Art~/CREDITS.md`)

| Area | Asset(s) | Result |
|---|---|---|
| Sky | Kloofendal 48d Partly Cloudy, Kloppenheim 06 (pure-sky HDRIs, 2k) | Real cumulus skies; image-based ambient and reflections; sun light aimed at the sun in the image; fog = measured horizon colour |
| Ground | Leafy Grass, Forrest Ground 01, Forest Leaves 02, Dirt Floor, Aerial Rocks 02 | Photographed terrain layers with normal maps, painted by ecology (same rules as before) |
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

**Integration (any pack):** set the prefab on the species asset (`Assets/WildLens/Data/Species/…`), add an Animator
with `Speed` (float), `Activity` (int, AnimalActivity value) and `GaitPhase` (float); `AnimalAnimatorBridge` drives
them. Map activities to clips: Grazing → eat, Alert → look/alert, Walking/Running/Hopping → locomotion blend on
Speed, Perched/Flying/Landing/TakingOff for the hawk. Expect 1–2 days per species. **One beautiful deer first.**

## Trees (highest remaining environment weakness)
* Paid, game-ready, URP, with LODs and wind: SpeedTree (Unity integration, best wind/LOD) or Asset Store nature packs
  that ship URP shaders and LODs. Assign as tree prefabs in the builder; placement, wildlife data and sway are automatic
  (prefab trees use the WindZone instead of FoliageSway).
* The free Unity *Terrain Sample Asset Pack* provides URP terrain materials/details (ODbL height data).

## Remaining visual weaknesses (honest list)
1. **Animals** are primitive placeholders: the biggest gap.
2. **Trees** are stylised primitives with scanned bark; canopies are flat-coloured blobs next to photographic ground.
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
