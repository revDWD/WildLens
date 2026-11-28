# Art credits

Every file in this folder is free and may be passed on. Most are **CC0 1.0 (public domain)**: the skies, terrain,
surfaces, rocks, wood and plants from **Poly Haven** (https://polyhaven.com/license) and most of the animals. A file under
an attribution licence (CC BY) is marked as such in its row and its author is credited there, as the licence requires.

The folder name ends in `~`, so Unity ignores it; **WildLens ▸ Build Vertical Slice** copies it into
`Assets/WildLens/Art` and sets import settings.

| File(s) | Poly Haven asset | Used for |
|---|---|---|
| Sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr | Kloofendal 48d Partly Cloudy (Pure Sky) | Late-morning sky, ambient light, reflections |
| Sky/kloppenheim_06_puresky_2k.hdr | Kloppenheim 06 (Pure Sky) | Golden-hour sky |
| Sky/kloofendal_overcast_puresky_2k.hdr | Kloofendal Overcast (Pure Sky) | Overcast weather (1.4) |
| Terrain/leafy_grass_* | Leafy Grass | Terrain: grass and meadow (two tints) |
| Terrain/forest_leaves_02_* | Forest Leaves 02 | Terrain: forest floor |
| Terrain/dirt_floor_* | Dirt Floor | Terrain: trail |
| Terrain/aerial_rocks_02_* | Aerial Rocks 02 | Terrain: rocky slopes |
| Surfaces/bark_brown_02_* | Bark Brown 02 | Broadleaf trunks |
| Surfaces/pine_bark_* | Pine Bark | Conifer trunks |
| Surfaces/mossy_rock_* | Mossy Rock | Rock material |
| Surfaces/weathered_brown_planks_* | Weathered Brown Planks | Woodwork: kiosk, signs, decks, the blind, benches, bridges (1.3) |
| Models/fern_02 | Fern 02 | Undergrowth |
| Models/shrub_02, shrub_04 | Shrub 02, Shrub 04 | Trailside shrubs |
| Models/rock_moss_set_01, rock_moss_set_02 | Rock Moss Set 01 / 02 | Outcrop rocks (split into pieces) |
| Models/boulder_01 | Boulder 01 | Landmark boulders |
| Models/tree_stump_01 | Tree Stump 01 | Stumps |
| Models/dead_tree_trunk_02 | Dead Tree Trunk 02 | Fallen logs |
| Models/dandelion_01, periwinkle_plant | Dandelion 01, Periwinkle Plant | Macro photography subjects, split into single plants (1.5) |
| Models/rock_face_01, rock_face_02 | Rock Face 01 / Rock Face 02 (Verdant Trail collection) | Outcrops: bedrock on steep slopes and the ridge's flanks (1.2) |
| Foliage/leaf_cluster_rgba.png | Island Tree 01 (leaf atlas) | Broadleaf canopy cards: 420 leaves recomposed into one cluster |
| Foliage/conifer_branch_rgba.png | Fir Tree 01 (twig atlas) | Conifer branch cards: 70 needle twigs recomposed into one branch spray |

Processing done when the assets were added: HDRIs analysed for sun direction/colour and horizon colour
(`Sky/*.json`); rock faces (1.2) taken at 1k (JPG diffuse and OpenGL normal), 20k and 30k triangles; foliage alpha maps merged into RGBA diffuse PNGs; JPG OpenGL normal maps used in place of EXR.
The Foliage textures (0.6.0) were composed from the trees' CC0 leaf/twig atlases; the tree meshes themselves are not
shipped (2–17 M polygons each).

## Animals (1.6.1)

| File | Source | Author | Licence | Use |
|---|---|---|---|---|
| Animals/Wolf.fbx | "Animated Wolf, Eagle, Dog, Cat and Piranha" (OpenGameArt) | Quaternius | CC0 1.0 | Stand-in for the coyote (tinted to coyote colours) |
| Animals/Eagle.fbx | same pack | Quaternius | CC0 1.0 | Stand-in for the red-tailed hawk (tinted) |

Converted from the original `.blend` files with Blender 4.0: Diffuse materials replaced by Principled with the same
colours, the armature turned to face Blender's front (−Y, so +Z in Unity), exported to FBX with all actions (Wolf: Idle,
Walking; Eagle: Flying, Idle). The pack's dog, cat and piranha are not shipped (no species for them).

## Animals added after 1.9.0 (free licences: CC0, and one CC-BY 4.0)

Every model was downloaded from the OpenGameArt page named below, where the licence was read on the page itself, and
converted from `.blend` to FBX with Blender 4.0.2 (headless): armature turned to face Blender's front (−Y, so +Z in
Unity, as Wolf/Eagle), unwanted actions removed, the rest renamed to plain role words (Idle, Walk, Run, Hop, Forage,
Peck), exported with all actions baked. Where a model had no idle, a one-frame Idle was made from its first pose.

| File | Title and source page | Author | Licence | Use |
|---|---|---|---|---|
| Animals/Fox.fbx | "Fox" https://opengameart.org/content/fox-0 (clips Idle, Walk, Run, Forage = the source's DIGGING; BITE and DEATH removed) | br-n518 | CC0 https://creativecommons.org/publicdomain/zero/1.0/ | Red fox: replaces the tinted dog |
| Animals/Rabbit.fbx | "Rabbit" https://opengameart.org/content/rabbit-0 (clips Idle (pose), Hop, Run) | CDmir (collaborator TinyWorlds) | CC0 | Eastern cottontail |
| Animals/Squirrel.fbx | "Undead Squirrel (ANIMATED)" https://opengameart.org/content/undead-squirrel-animated (clips Idle, Run, Hop; the pack's red textures and blood marks are NOT used: flat grey-brown materials instead) | CDmir (collaborator TinyWorlds) | CC0 | Eastern gray squirrel |
| Animals/Rat.fbx | "Rat" https://opengameart.org/content/rat-0 (clips Idle, Walk, Run; attack and death removed) | br-n518 | CC0 | Stand-in for the Virginia opossum (the matcher lists "rat" as a stand-in) |
| Animals/Turtle.fbx | "Turtle" https://opengameart.org/content/turtle-0 (clips Walk, and an Idle pose) | Heathal | CC0 | Waits for a turtle species |
| Animals/Duck.fbx | "Rigged Duck" https://opengameart.org/content/rigged-duck (clips Walk, Peck, and an Idle pose; turned 90° to face front) | weirdybeardyman | CC0 | Waits for a duck species |
| Animals/Skunk.fbx | "Skunk" https://opengameart.org/content/skunk (clips Idle, Walk; the 4096 px texture was scaled to 1024 px and embedded in the FBX) | magicink | CC-BY 4.0 https://creativecommons.org/licenses/by/4.0/ | Waits for a skunk species. **Attribution required: "Skunk" by magicink (OpenGameArt), CC BY 4.0, converted to FBX, texture scaled.** |
| Animals/Snake.fbx | "Snake low poly animated 3d model" https://opengameart.org/content/snake-low-poly-animated-3d-model (clips Idle, Walk; 256 px texture embedded) | methodical pixel | CC0 | Waits for a snake species |
