# Roadmap

What to build next, in order, and what not to build. Each step names the document with the detail. Reorder only on
playtest evidence (screenshots). Every item must pass the questions in [AGENTS.md](../AGENTS.md): *would this make
the place feel more real?* and *why would a photographer come here?*

## Recommended next milestone: 2.0 "True to Life"

1.9 made the place respond to the weather and filled the macro lens; 1.10 gave the animals voices and the blind a dry roof.
The first 1.9 screenshots (real Unity) proved the viewfinder, the print and the generated wildflowers, and found magenta birds, a
black turkey and event errors, all addressed in 1.10 but not yet seen. **The voices have never been heard**, most of 1.9 is
still unseen, the turkey still reads as a chicken, five species are placeholder bodies, and the eight new models are unseen. 2.0 is about
making what exists true to life before adding more. In order:

1. **Tuning from the screenshots and the director's ears (1.9 and 1.10).** A **tuning release: no new features until it looks
   and sounds right.** First the 1.10 list (any magenta on the cardinal and jay, the `[WildLens] Wildlife models refreshed` Console
   text, the Asset Audit's "animals as built" table, the three unidentified things from the wide screenshot, the voices by ear,
   F3 inside the shelters). Then the screenshot list in the 1.9.0 changelog (the `[WildLens] Reserve:` and `Small life:` Console lines; the empty screen, prompts,
   controls card and notebook page; a monarch on milkweed; fireflies at a fair dusk; frogs after the shower; songbirds bathing;
   the owl on a snag; Hemlock Ravine from its seat and rim; each hidden place; the re-tinted fox and turkey). Fix sizes,
   facings, the Rock Shelter's crag, the cascade's ledges and any generated model that does not read.
2. **New species for the waiting models, then real models.** 1.10 added eight free animals (red fox, cottontail, gray squirrel, a rat
   for the opossum, and a duck, turtle, snake and skunk). The next step after tuning is **new species for the four that wait: painted
   turtle, mallard, striped skunk, a snake, each with a place** (and a voice in `VoiceBank`). Then real models for the **turkey** and
   the five placeholders (heron, raccoon, owl, groundhog, sandpiper), and a truer opossum. Since 1.10 the rule is "free", not
   "CC0": anything free that may be passed on can be committed with credit, and anything free can be imported locally
   ([WildlifeAssetStrategy.md](WildlifeAssetStrategy.md)). **The search is finished** (opengameart.org and quaternius.com answer;
   poly.pizza wants a browser check, kenney.nl refuses, Quaternius' downloads are on Google Drive), so the rest is a browser job for the director. The **upgrade rule** swaps models in on the
   next Rebuild (never replacing a model chosen by hand). A new species gets a voice in `VoiceBank` too.
3. **Sound: written in 1.10, awaiting ears.** 22 species, frogs, bees and crickets, from the animals themselves, driven by the
   same natural conditions as the habits. What remains is tuning by ear (loudness, frequency, any call that sounds wrong), an
   on-screen volume setting, a quieter cricket bed under a roof, and the species still silent on purpose.
4. **More relief.** Late carving is proven (1.9): a taller ridge companion and a deeper valley (the world is 900 m with about 26
   to 30 m of relief and a mean slope of about 4°), then the remaining hidden places (fern valley, observation ledge,
   creek-side hollow) and the wetland boardwalk with a second blind ([ReserveExpansion.md](ReserveExpansion.md)). Prototype and
   measure outside Unity first; carve in the late geography step.
5. **Trees.** Prove the Environment Kit's SpeedTree route in Unity ([EnvironmentMigration.md](EnvironmentMigration.md) §5):
   still the largest visual weakness.
6. **Night.** A night sky, and the fireflies, owls and treefrogs worth staying out for. **A game night lasts about 11 real
   seconds, so night voices are in practice heard at dusk:** decide first whether nights should be playable.
7. **The journal.** Decide whether unseen species should stop appearing as "???" rows with "Species X of N" (it sits
   uneasily with "no checklists"; the Field notebook page already shows only what was found).

## Next, after 2.0, in order

1. **Prove the Environment Kit in Unity** if not done in 2.0 ([EnvironmentMigration.md](EnvironmentMigration.md) §5). Import
   Unity's free *Terrain URP Demo Scene* / *Terrain Sample Asset Pack*, scan, rebuild, send comparison captures; tune
   `treeScale`, grass densities and role assignments.
2. **Macro, the rest** ([Systems.md](Systems.md), Macro). 1.9 generated 26 models so every subject appears without packs;
   what remains: dragonfly perches that read, more plants and fungi (trillium, goldenrod, bracket
   fungus, a fairy ring; [WildlifeCampaign.md](WildlifeCampaign.md) §4), and a check that movement never breaks "standing still lets it settle".
3. **Playtest-driven tuning of 1.0–1.10.** Road routing and grading, ride comfort, each destination at each time of day,
   schedules and weather habits being learnable, animals' awareness with new models (silhouettes change what is "visible").
4. **Props worth looking at** ([VisualRoadmap.md](VisualRoadmap.md)). Bevelled posts, a shingled roof for the Reserve
   Board, a timber blind; lit sign lettering (TextMeshPro with a free, redistributable font, if one fits).
5. **Bike looks** ([TrailBike.md](TrailBike.md)). Shaped fender and tank, real wheels, a dusk headlight. Never speed.
6. **Wet weather.** Wet surfaces and puddles in rain, and **rain that stops under tree canopy** (1.10 stops it at three roofs: the
   blind, the Reserve Board and the Rock Shelter; canopy needs data the game does not have at runtime).
7. **Expansion species, one at a time** ([WildlifeExpansion.md](WildlifeExpansion.md); candidates and places in
   [WildlifeCampaign.md](WildlifeCampaign.md) §2). Chipmunk, goose, duck, turtle, woodpecker, vulture, skunk, dove, bat, each when the
   audit shows a usable model and the place it would really live exists. The niche, and its weather habit if it has one, comes first.
8. **A hand-authored reserve terrain** shipped as free, redistributable data, so the reserve no longer depends on the user's terrain.

## Not to build

- **No progression, XP, currency, unlocks, missions, quests, achievements, checklists, inventory, crafting, survival,
  combat or hunting.** They turn attention into accumulation. The journal is the only record.
- **No collectibles.** Hidden places are found and named once, never counted, marked or rewarded.
- **No faster or more capable bike** (no boost, upgrades, fuel, damage, stunts). Fix routes and infrastructure.
- **No scripted or triggered wildlife events, spawns on cue, quest markers, compass HUDs or an always-on HUD.** **Natural,
  condition-driven events are wanted since 1.9** (the hour, the weather now, the weather lately, the forecast: the same
  every time, never aimed at the player, counted or rewarded). Rhythms come from the clock, the weather and placement;
  players find animals by learning the place. The screen is empty by default.
- **No predator–prey hunting, breeding or population simulation.** Simulation depth with little photographic payoff.
- **No bigger map without a reason to go to the new part** (destinations, not distance).
- **No mixed fidelity in the environment.** Scenery stays photographic: don't add stylised or low-poly trees, rocks or
  props beside the scanned reserve. (Animals are exempt since 1.6: see the wildlife strategy.)
- **No files in the repository that may not be passed on**: free Asset Store packs and "personal use" models stay local (since 1.10 CC0 and credited CC-BY files are welcome). Don't adopt HDRP-only environments.
- **No hand-edited scenes.** Everything the player sees is generated; improvements land by rebuilding.
- **No technology for its own sake.** Unity features earn their place only with a visible gain.
