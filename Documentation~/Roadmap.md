# Roadmap

What to build next, in order, and what not to build. Each step names the document with the detail. Reorder only on
playtest evidence (screenshots). Every item must pass the questions in [AGENTS.md](../AGENTS.md): *would this make
the place feel more real?* and *why would a photographer come here?*

## Next, in order

1. **Playtest 1.6.** The Reserve Board (all three faces, at dawn and dusk), the bike bay, signs and interpretive panels
   read from the trail, the ford's log seat, Willow Falls after the day-3 shower, the hidden places, macro on a
   dandelion and the periwinkle.
2. **Animals from free packs** ([WildlifeAssetStrategy.md](WildlifeAssetStrategy.md)). The biggest visual gain
   available, and now an afternoon rather than a purchase. Download Quaternius (CC0) and one free bird pack, run
   **WildLens ▸ Wildlife Pack Setup**, photograph each species at 4× and 16×, and fix sizes and yaws. Then fill gaps
   one species at a time. Expect fixes on first use: the tool has never run in Unity.
3. **Prove the Environment Kit in Unity** ([EnvironmentMigration.md](EnvironmentMigration.md) §5). Import Unity's free
   *Terrain URP Demo Scene* / *Terrain Sample Asset Pack*, scan, rebuild, send comparison captures; tune `treeScale`,
   grass densities and role assignments. Trees are the second-biggest visual weakness.
4. **Macro, round two** ([Systems.md](Systems.md), Macro). Animated small subjects using the reserved behaviours
   (`Flits`, `Perches`, `Hops`, `Glows`): a butterfly on meadow flowers, a dragonfly at the ford, a frog at the pond
   after rain, fireflies at dusk, mushrooms after rain. CC0 scans first (Smithsonian 3D, Poly Haven).
5. **Playtest-driven tuning of 1.0–1.6.** Road routing and grading, ride comfort, each destination at each time of
   day, schedules being learnable, animals' awareness with the new models (silhouettes change what is "visible").
6. **Props worth looking at** ([VisualRoadmap.md](VisualRoadmap.md)). Bevelled posts, a shingled roof for the Reserve
   Board, a timber blind; lit sign lettering (TextMeshPro with a CC0 font, if one fits).
7. **Bike looks** ([TrailBike.md](TrailBike.md)). Shaped fender and tank, real wheels, a dusk headlight. Never speed.
8. **Night and wet weather.** A CC0 night sky (Poly Haven), wet surfaces and puddles in rain, rain that stops at roofs,
   a frog chorus at dusk.
9. **Expansion species, one at a time** ([WildlifeExpansion.md](WildlifeExpansion.md)). Wood duck, painted turtle,
   pileated woodpecker, each when the place it would really live exists. Free packs make the model the easy part;
   the niche still comes first.
10. **New places with a reason** ([ReserveExpansion.md](ReserveExpansion.md)). A wetland boardwalk with a second blind
    (its dragonflies and frogs), a forest footpath, a ranger cabin.
11. **A hand-authored reserve terrain** shipped as CC0 data, so the reserve no longer depends on the user's terrain.

## Not to build

- **No progression, XP, currency, unlocks, missions, quests, achievements, checklists, inventory, crafting, survival,
  combat or hunting.** They turn attention into accumulation. The journal is the only record.
- **No collectibles.** Hidden places are found and named once, never counted, marked or rewarded.
- **No faster or more capable bike** (no boost, upgrades, fuel, damage, stunts). Fix routes and infrastructure.
- **No scripted wildlife events, spawns on cue, quest markers or compass HUDs.** Rhythms come from the clock, the
  weather and placement; players find animals by learning the place.
- **No predator–prey hunting, breeding or population simulation.** Simulation depth with little photographic payoff.
- **No bigger map without a reason to go to the new part** (destinations, not distance).
- **No mixed fidelity in the environment.** Scenery stays photographic: don't add stylised or low-poly trees, rocks or
  props beside the scanned reserve. (Animals are exempt since 1.6: see the wildlife strategy.)
- **No non-CC0 files in the repository**, free store packs included. Don't adopt HDRP-only environments.
- **No hand-edited scenes.** Everything the player sees is generated; improvements land by rebuilding.
- **No technology for its own sake.** Unity features earn their place only with a visible gain.
