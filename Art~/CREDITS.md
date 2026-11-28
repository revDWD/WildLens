# Art credits

All assets in this folder are from **Poly Haven** (https://polyhaven.com) and are released under
**CC0 1.0 (public domain)**: free for any use, including commercial, with no attribution required.
Credit is given here as a courtesy. License: https://polyhaven.com/license

The folder name ends in `~`, so Unity ignores it; **WildLens ▸ Build Vertical Slice** copies it into
`Assets/WildLens/Art` and sets import settings.

| File(s) | Poly Haven asset | Used for |
|---|---|---|
| Sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr | Kloofendal 48d Partly Cloudy (Pure Sky) | Late-morning sky, ambient light, reflections |
| Sky/kloppenheim_06_puresky_2k.hdr | Kloppenheim 06 (Pure Sky) | Golden-hour sky |
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
| Models/rock_face_01, rock_face_02 | Rock Face 01 / Rock Face 02 (Verdant Trail collection) | Outcrops: bedrock on steep slopes and the ridge's flanks (1.2) |
| Foliage/leaf_cluster_rgba.png | Island Tree 01 (leaf atlas) | Broadleaf canopy cards: 420 leaves recomposed into one cluster |
| Foliage/conifer_branch_rgba.png | Fir Tree 01 (twig atlas) | Conifer branch cards: 70 needle twigs recomposed into one branch spray |

Processing done when the assets were added: HDRIs analysed for sun direction/colour and horizon colour
(`Sky/*.json`); rock faces (1.2) taken at 1k (JPG diffuse and OpenGL normal), 20k and 30k triangles; foliage alpha maps merged into RGBA diffuse PNGs; JPG OpenGL normal maps used in place of EXR.
The Foliage textures (0.6.0) were composed from the trees' CC0 leaf/twig atlases; the tree meshes themselves are not
shipped (2–17 M polygons each).
