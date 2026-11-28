# Roadmap

What to build next, in order, and what not to build. Each step names the document with the detail. Reorder only on
playtest evidence (screenshots). Every item must pass the questions in [AGENTS.md](../AGENTS.md): *would this make
the place feel more real?* and *why would a photographer come here?*

## Next, in order

1. **Run the Asset Audit Report and paste it** ([WildlifeCampaign.md](WildlifeCampaign.md)). **WildLens ▸ Asset Audit
   Report** lists what is really in the imported packs: which animals, which reserve species they can be, clips, shader
   problems, small-life models and vehicles. Every animal, small-life and bike decision after this is made from it. The
   tool has never run in Unity: expect fixes on first use.
2. **Playtest 1.6 and 1.7.** The Reserve Board (all three faces, at dawn and dusk), the bike bay, signs and
   interpretive panels read from the trail, the ford's log seat, Willow Falls after the day-3 shower, the hidden places,
   and macro on a dandelion and the periwinkle (and on whatever small life the audit let in).
3. **Animals from free packs** ([WildlifeAssetStrategy.md](WildlifeAssetStrategy.md), order in
   [WildlifeCampaign.md](WildlifeCampaign.md) §3). The biggest visual gain available, and an afternoon rather than a
   purchase. With the packs imported, the automatic setup does most of it: read the Console line (which species got a
   model, which did not), photograph each species at 4× and 16×, and fix sizes and yaws in **Wildlife Pack Setup**. Then
   fill gaps one species at a time.
4. **Prove the Environment Kit in Unity** ([EnvironmentMigration.md](EnvironmentMigration.md) §5). Import Unity's free
   *Terrain URP Demo Scene* / *Terrain Sample Asset Pack*, scan, rebuild, send comparison captures; tune `treeScale`,
   grass densities and role assignments. Trees are the second-biggest visual weakness.
5. **Macro, round two: the rest** ([Systems.md](Systems.md), Macro). The first step is done in 1.7: twelve small-life
   subjects, found in your packs and placed in their habitats, with `Flits`, `Perches`, `Hops` and `Glows` working.
   What remains: a fallback for subjects with no model (CC0 small-life scans and models, Smithsonian 3D and Poly Haven
   first), real perches for dragonflies (a stem or reed instead of a point in the air), a frog chorus at dusk (with item 9), the
   *Next* plants and fungi in [WildlifeCampaign.md](WildlifeCampaign.md) §4 (milkweed, cattails, trillium, goldenrod,
   bracket fungus, a fairy ring), and a check that the movement never breaks "standing still lets it settle".
6. **Playtest-driven tuning of 1.0–1.7.** Road routing and grading, ride comfort, each destination at each time of
   day, schedules being learnable, animals' awareness with the new models (silhouettes change what is "visible").
7. **Props worth looking at** ([VisualRoadmap.md](VisualRoadmap.md)). Bevelled posts, a shingled roof for the Reserve
   Board, a timber blind; lit sign lettering (TextMeshPro with a CC0 font, if one fits).
8. **Bike looks** ([TrailBike.md](TrailBike.md)). Shaped fender and tank, real wheels, a dusk headlight. Never speed.
9. **Night and wet weather.** A CC0 night sky (Poly Haven), wet surfaces and puddles in rain, rain that stops at roofs,
   a frog chorus at dusk.
10. **Expansion species, one at a time** ([WildlifeExpansion.md](WildlifeExpansion.md); candidates and places in
   [WildlifeCampaign.md](WildlifeCampaign.md) §2). Wood duck, painted turtle, pileated woodpecker, each when the audit
   shows a usable model and the place it would really live exists. Free packs make the model the easy part;
   the niche still comes first.
11. **New places with a reason** ([ReserveExpansion.md](ReserveExpansion.md)). A wetland boardwalk with a second blind
    (its dragonflies and frogs), a forest footpath, a ranger cabin.
12. **A hand-authored reserve terrain** shipped as CC0 data, so the reserve no longer depends on the user's terrain.

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
