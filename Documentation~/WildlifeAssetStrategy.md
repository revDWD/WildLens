# Wildlife asset strategy (1.6)

**Direction (from the director, 1.6):** wildlife does **not** need to be photorealistic, and the animals don't all need
to share a style or come from one pack. A colourful stylised bird can be a blue jay; a stylised owl can be a barred
owl. **A stylised, varied reserve built from free assets beats a realistic reserve full of blocky placeholder
animals.** This page replaces the 1.5 strategy, which recommended buying semi-realistic packs to match the
photographic environment.

## What matters, in order

1. **A recognisable silhouette.** At 16× through a lens you read an animal by its outline and posture: deer legs and
   ears, a fox's brush tail, an owl's round head, a heron's neck. If the outline is right, the style barely matters.
2. **Animation.** A model that walks, grazes, looks up and flies sells life; a beautiful statue doesn't. Idle, walk,
   run, eat and look for mammals; perch, take-off, fly and land for birds.
3. **Variety.** Eighteen species that each look like *something* is the goal; two perfect ones is not.
4. **Consistency within a species.** Every deer in the reserve uses the same model. Styles may differ *between*
   species, never *within* one.
5. **Realism**, last.

What this changes: the old rule "don't mix fidelities" now applies to the **environment** (scenery stays
photographic and CC0 or kit-sourced). Animals are exempt: they can be stylised and come from several packs.

## Automatic: just import the packs (1.6)

Import free animal packs **into the Unity project** (Asset Store: Package Manager ▸ My Assets ▸ Download ▸ Import;
a zip from itch.io: unzip it and drag the folder into the Project window's Assets). That's all. As soon as an import
finishes, and again on every rebuild, WildLens gives each species that has no model yet its best match, as below,
and the Console lists what it did. Species that already have a model are never changed. Untick **Auto Wildlife From
Packs** in the builder settings to turn this off.

## The window: WildLens ▸ Wildlife Pack Setup (1.6)

To change a choice (another model, a size, a yaw), open **WildLens ▸ Wildlife Pack Setup** and press **Scan
project**. It:

- finds every animated model (a skinned mesh with at least one clip) outside `Assets/WildLens`;
- matches each to the species it *is* (Deer → white-tailed deer) or can **stand in** for, by name:

  | Species | Stand-ins it will accept |
  |---|---|
  | Coyote | wolf, jackal, husky, dog |
  | Red-tailed hawk | eagle, falcon, buzzard, kite |
  | Great blue heron | egret, crane, stork, flamingo |
  | Wild turkey | pheasant, chicken, hen, peacock |
  | Spotted sandpiper | plover, shorebird, gull |
  | Groundhog | beaver, gopher, prairie dog |
  | Cardinal, blue jay, blackbird, kingfisher, phoebe | any songbird, finch, sparrow, robin, "bird" (one model, tinted per species) |
  | Deer, fox, squirrel | roe/elk/antelope; shiba/jackal; chipmunk |

- shows one row per species (best match first: the real animal before a stand-in, then the model with the most
  recognised clips), with a size, a yaw correction and **tint** (on by default for stand-ins);
- **Set up ticked species** runs the same factory as Animal Model Setup for each row. It classifies clips by name,
  builds the Animator Controller, scales the model to the species' real size (birds by beak-to-tail length), puts its
  feet on the ground and assigns the prefab.

Tint copies the model's materials (the pack's own are never changed) and colours them by coverage: largest area →
the species' primary colour, next → secondary, third → accent. This is how one songbird model becomes a red
cardinal, a blue jay and a black, red-shouldered blackbird. It works best on flat-coloured stylised packs; on a
photographic texture it tints rather than replaces.

Species with no match keep their placeholder bodies, so the reserve is never missing an animal. The **Placeholder**
button on a row reverts that species. Animals spawn on Play, so no rebuild is needed.

## Recommended free sources (check each page before downloading)

Research was done from search results (most store and CC0 hosts can't be opened from the build machine), so
**confirm the species list and licence on each page.**

| Source | Licence | What it gives the reserve | Notes |
|---|---|---|---|
| **Quaternius** animal packs ([itch.io](https://quaternius.itch.io/lowpoly-animated-animals), quaternius.com) | **CC0** | The "Ultimate Animated Animal Pack" is reported to include deer/stag, fox, wolf (coyote), husky; an older pack has wolf, eagle (hawk), dog, cat ([OpenGameArt](https://opengameart.org/content/animated-animales-low-poly)) | Low-poly, flat colour, good clips. CC0, so a future release *could* commit an opt-in copy |
| **ithappy: Animals FREE** ([Asset Store](https://assetstore.unity.com/packages/3d/characters/animals/animals-free-260727)) | Asset Store EULA, free | Animated, cartoon-style; species list to confirm | Import locally, never commit |
| **Dinopunch: Living Birds** ([Asset Store](https://assetstore.unity.com/packages/3d/characters/animals/birds/living-birds-15649)) | EULA, free | A variety of animated perching/flying birds (crows confirmed; songbirds likely) | Old (2018, Unity 4.5): materials may need the URP converter |
| Quaternius / other CC0 bird packs | CC0 | Songbirds, owls | Search Poly Pizza and itch.io filtered to CC0 |
| Smithsonian 3D (3d.si.edu) | CC0 | Insect scans for macro | Static: needs the reserved macro behaviours |

Paid, if a gap remains after the free route: **BundleSmith Animated Forest Animals** (~$13–33: deer, fox, rabbit,
squirrel, raccoon, wolf, with clips that match WildLens's states exactly ([itch.io](https://bundlesmith.itch.io/animated-forest-animals-pack-low-poly-stylized-models)));
**Low Poly Bird: Predator Pack** (eagles, barn owl, falcon) for the hawk and owl. Not usable: CC-BY rigs (Khronos
Fox), "personal use" Sketchfab models, free-plan AI generator output (CC-BY), Unreal-only packs, re-uploads of paid
packs.

## Suggested order

1. **Deer** (the most photographed animal), **fox**, **coyote** (wolf stand-in), **hawk** (eagle stand-in): one
   Quaternius download covers most of them.
2. **One songbird model**, tinted into five species. The biggest gain in variety for the least work.
3. **Owl, heron, turkey**: the reserve's distinctive silhouettes; any stylised version reads.
4. Rabbit, squirrel, raccoon, groundhog, opossum, sandpiper, as packs turn up.

After each step, playtest: photograph the animal at 4× and 16× at dawn and golden hour, and send screenshots. Note
any that are the wrong size (fix with the row's size field) or face backwards (yaw 180).

## Licence rules (unchanged)

Only CC0 is committed to the repository. Store packs, free ones included, are imported into your own project and never
committed; Wildlife Pack Setup and Animal Model Setup use them from there and write prefabs to your own
`Assets/WildLens/Animals`.
