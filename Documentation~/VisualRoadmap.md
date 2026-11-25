# WildLens visual roadmap & asset recommendations

What 0.4.0 does procedurally, what it can't do without real art, and the best ways to close that gap.
Licensing notes were checked in November 2026. **Re-check the licence on each asset's page before importing:
terms change and differ per item.**

## Where the procedural approach tops out

0.4.0 gets lighting, haze, grading, terrain texturing, grass with wind, foliage sway and composition as far as
generated content reasonably can. The remaining gap to "beautiful" is almost entirely **art assets**:

| Gap | Why it can't be generated well | What fixes it |
|---|---|---|
| Animals look like primitives | Silhouette and fur need modelled, rigged, animated meshes | Animated wildlife models (prefab hook already exists) |
| Trees read as cones and blobs | Real foliage needs alpha-cut leaf cards and bark textures | Tree prefabs or SpeedTree assets (builder accepts them) |
| Terrain textures are clean noise | Photo-scanned ground has real structure | Scanned PBR ground textures |
| Rocks are smooth blobs | Scanned rock geometry | Scanned rock models |
| Sky is Unity's procedural sky | Clouds, real sky gradients | An HDRI sky |

The pipeline is ready for each: assign prefabs in **WildLens ▸ Vertical Slice Builder** (trees, bushes, rocks), set
`prefab` on a species asset, swap layer textures in `Assets/WildLens/Generated/Terrain/TL_*.terrainlayer`.

## Recommended sources

### 1. Poly Haven: textures, HDRIs, scanned models. **Highest impact per hour.**
* **Licence: CC0.** Commercial use allowed, no attribution required (credit appreciated). You can't claim authorship or
  relicense the assets themselves. [License](https://polyhaven.com/license) · [FAQ](https://docs.polyhaven.com/en/faq)
* **Use for:** ground textures (forest leaves, grass/soil, gravel path, mossy rock) to replace the five generated
  terrain layers; an outdoor **HDRI** for the sky and ambient light; scanned rocks, logs and stumps.
* **Integration effort: low.** Download 1k–2k diffuse + normal maps, assign them to the existing `TL_*.terrainlayer`
  assets (keep the tile sizes). For the sky: import the `.hdr`, make a `Skybox/Panoramic` material, assign it under
  Lighting ▸ Environment, then rebuild with *Configure Lighting* off so it isn't replaced.
* **Visual impact: very high.** Scanned ground textures and a real sky change the whole frame.

### 2. ambientCG: PBR materials
* **Licence: CC0.** Same practical terms as Poly Haven.
* **Use for:** alternative or additional ground, bark and rock materials, and more variety for terrain layers.
* **Effort / impact:** low / high.

### 3. Unity Terrain Sample Asset Pack (Unity Asset Store, free)
* **Licence:** Standard Unity Asset Store EULA (fine for use in your game; not for redistribution as an asset). The
  store page notes its *terrain height data* is under the **Open Data Commons ODbL**; see its Third-Party Notices.
* **Use for:** PBR terrain layers, terrain stamps, URP vegetation details. Supports Built-in, URP and HDRP; recent
  versions list Unity 6 compatibility. About 1.6 GB.
  [Store page](https://assetstore.unity.com/packages/2d/textures-materials/terrain-tools-sample-asset-pack-145808) ·
  [Unity blog](https://unity.com/blog/games/the-latest-unity-terrain-sample-pack-is-here)
* **Effort: low–medium.** Vegetation materials need Shader Graph installed.
* **Visual impact: high** for the ground.

### 4. Kenney: stylised props and nature kits
* **Licence: CC0.**
* **Use for:** only if you choose a deliberately stylised look. Clean, cohesive, light on performance. Mixing it
  with photo-scanned textures clashes, so pick one direction.
* **Effort / impact:** very low / medium (stylised).

### 5. Wildlife models
This is the hardest category to source well under permissive licences:
* **Sketchfab:** many good animal models, mostly **CC-BY** (commercial use allowed **with attribution**; keep a credits
  file). Avoid **CC-BY-NC** (non-commercial) and **CC-BY-ND** for anything you might ship. Check that the model is rigged
  and has the animations you need (walk, run, idle, graze). Many good-looking ones aren't animated.
* **Unity Asset Store / Fab:** paid animated wildlife packs exist (e.g. dedicated animated fox or forest-animal
  packs). The licence is clear (standard EULA) and they usually come rigged with idle/walk/run/eat animations.
  **For deer and fox, this is the most reliable route.**
* **Quaternius (CC0):** its free animated animal pack is *farm* animals, not deer or fox. Useful as a reference
  for rig and animation set-up, not for this game's species.
* **OpenGameArt:** licences vary per item (CC0, CC-BY, CC-BY-SA, GPL). **Avoid GPL and CC-BY-SA for game assets**
  unless you accept their share-alike terms. Most wildlife there is 2D.
* **Integration effort: medium.** Set the prefab on the species asset, add an Animator with `Speed` (float), `Activity`
  (int) and `GaitPhase` (float) parameters (the existing `AnimalAnimatorBridge` drives them), and size the trigger
  collider. Expect one to two days per species for a clean controller.
* **Visual impact: the single biggest jump**: wildlife is what players photograph.

### 6. Trees and vegetation
* **Asset Store:** Unity's free "Book of the Dead" environment is **HDRP-only** (not suitable for a URP project).
  Free and low-cost nature packs exist for URP; prefer ones with LODs and wind-ready shaders.
* **SpeedTree (paid):** best wind and LOD quality; responds to the WindZone the builder creates.
* **Effort: low** (assign as tree prefabs; the builder adds wildlife data and placement automatically).
* **Impact: very high**, second only to wildlife.

### Avoid
* Assets ripped from games, or "free" uploads of commercial models (common on aggregator sites).
* Anything without a clear licence page. "Free download" is not a licence.
* Code or shaders copied from unrelated GitHub repositories.

## Suggested order
1. HDRI sky + scanned ground textures (Poly Haven / ambientCG), about half a day: biggest change for the time.
2. Tree prefabs with wind (Asset Store / SpeedTree), about a day.
3. Deer + fox animated models (Asset Store), a few days, done species by species.
4. Rocks/logs/stumps (Poly Haven scans), half a day.

## Engineering improvements not implemented yet
* **Grass and flower wind for mesh grass** (HDRP or detail grass disabled) needs a vertex-animated grass shader
  (Shader Graph, per pipeline).
* **Grass occlusion in photos**: grass has no colliders, so a rabbit in long grass can count as visible.
* **Volumetric light / god rays and volumetric fog**: HDRP has them built in; URP needs a third-party solution.
* **Time of day**: dawn and dusk light would suit wildlife (deer at dawn) but is a gameplay-touching change.
* **Terrain height sculpting**: the fallback terrain is gentle rolling noise. A hand-shaped valley, ridge and
  stream would create stronger vistas; that's art direction, not code.
* **Exposure adaptation** when looking from forest shade into a bright meadow (URP lacks built-in auto-exposure).
* **Imposters / LODs** for placeholder trees, if tree counts go up.
