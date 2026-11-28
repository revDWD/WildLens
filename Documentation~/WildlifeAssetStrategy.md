# Wildlife asset strategy (1.6, updated 1.9)

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
3. **Variety.** Twenty-seven species that each look like *something* is the goal; two perfect ones is not.
4. **Consistency within a species.** Every deer in the reserve uses the same model. Styles may differ *between*
   species, never *within* one.
5. **Realism**, last.

What this changes: the old rule "don't mix fidelities" now applies to the **environment** (scenery stays
photographic and CC0 or kit-sourced). Animals are exempt: they can be stylised and come from several packs.

## Step zero: run the audit (1.7)

Before choosing packs or models, run **WildLens ▸ Asset Audit Report** (also a button in the builder window). It lists
every model in your project by pack, which reserve species each can be (the real animal or a stand-in), triangles, size,
clips and which behaviours they cover, and any shader that needs Unity's material converter. It saves
`WildLensAssetAudit.md` in the project folder and copies it to the clipboard (a short version when the full one is too long; the file always has everything): paste it into the chat. The 1.7 plan
([WildlifeCampaign.md](WildlifeCampaign.md)) is decided from that report, not from guesses about what a pack contains.

## What the user's packs gave us (1.8, from the real audit)

| Pack | What it gave |
|---|---|
| ithappy "Animals FREE" | deer (the real one), a chicken (the turkey's stand-in), a dog (the fox's, tinted); horse, tiger, kitty and penguin unused |
| Dinopunch "Living Birds" | cardinal, blue jay, crow, robin, chickadee, goldfinch and sparrow (seven exact matches); the sparrow, tinted, also stands in for the blackbird, kingfisher and phoebe |
| Blink stylised bear | the black bear (tinted black) |
| SinuousShrooms | 11 static mushrooms for the macro lens |

Eight species still have no model in any pack: cottontail, squirrel, heron, raccoon, owl, groundhog, opossum, sandpiper.
The full pack-by-pack table is in [WildlifeCampaign.md](WildlifeCampaign.md) §1.

What the packs taught (1.8): a pack's models are **not** ready to use as they come.
- **Built-in shaders are pink in URP.** Living Birds and the bear use built-in shaders. The factory now makes a
  **converted copy** of each such material (texture, colour, normal map, emission, metallic and smoothness, cut-out or
  transparency and two-sided drawing kept) on a URP Lit shader, in `Assets/WildLens/Animals`. The pack's own materials are
  never touched. Written blind: a screenshot decides.
- **Pack scripts are removed** from the animal (the pack's own behaviours, colliders, audio, effects, lights).
- **Real sizes.** The factory scales every model to the species' real size from a table (`AnimalPrefabFactory.RealSize`):
  cardinal 22 cm, blue jay 28 cm, crow 45 cm, robin 25 cm, chickadee 13 cm, goldfinch 12 cm, song sparrow 15 cm, turkey
  1.0 m, black bear 1.6 m, coyote 1.25 m and so on; the heron by its standing height (1.2 m). Birds are measured beak to
  tail, mammals by their larger side. A model that looks wrong is one size field in **Wildlife Pack Setup**.
- **Clips loop.** Pack clips mostly have Loop Time off; the factory loops copies inside the animal's controller.
- **Clip names lie.** A bear's "jump" is its only walk; "fly-left" is not "fly"; a pecking clip is not an idle. The clip
  choice prefers plain names and the earlier keyword of a role.
- **Old models are refreshed.** Animals the automatic setup built before 1.8 are rebuilt once from the same model. A
  species you set up by hand, or sent back to a placeholder, is left alone.

## The upgrade rule (1.9)

A model the **automatic setup** chose for a species is replaced by itself when a strictly better one is imported. "Better" is
compared on three things and nothing finer: the real animal over any stand-in; among stand-ins, one that is nobody else's real
animal over one that is; then the closer keyword (a "shiba" before a "dog"). Equal candidates never swap, two real animals never
swap, and a current model that no longer matches by name is left alone. So importing a model named **Fox** or **Turkey** replaces
the tinted dog and the tinted chicken on the next Rebuild, at the default real size, yaw 0 and the usual tint; one Console line
(`Wildlife models upgraded`) lists each change. **Never touched:** a model you chose by hand in a setup window, and a species you
sent back to a placeholder. (The 1.9 fox and turkey also got new colours: factory version 3 re-tints existing automatic builds,
because a species' colours are baked into the model's materials when it is built.)

## Where to find models, and a blocked-hosts note (1.9)

The **build machine cannot reach any CC0 model host** (quaternius.com, poly.pizza, opengameart.org, itch.io and kenney.nl are all
blocked from it), so it could not look at a page, check a licence or download anything. Sourcing models is **the director's job**,
and every candidate must be opened and its licence read (CC0, or another that allows free use without credit) before use. From
search snippets only:
- **A fox with idle, walk and run** on OpenGameArt: `https://opengameart.org/node/90146`.
- **Quaternius** packs: `https://quaternius.com` and `https://quaternius.itch.io`. Their farm-animal pack has a cow, horse, llama, pig
  and pug: **no fox**.
- **Check the licence first:** the glTF sample "Fox" (its rig and animation are CC-BY) may be committed only with the author credited; Sketchfab "personal use" foxes are not usable.
- Steps: download, unzip, drag the `.fbx` into the project's `Assets` with a **plain name** (`Fox.fbx`, `Turkey.fbx`, `Rabbit.fbx`,
  `Owl.fbx`, `Raccoon.fbx`...). The turkey first, then the five placeholders (heron, raccoon, owl,
  groundhog, sandpiper), and a truer opossum (1.10 brought the fox, cottontail and squirrel). Never commit them.
- **A pack with scripts can block every update.** The Living Birds pack's `scripts` and `demo` folders do not compile on
  Unity 6 and stop **all** package updates until deleted; WildLens needs only the models and clips. If a pasted audit shows an
  older WildLens version than the one released, ask for the Console's red lines first.

## Shipped with WildLens (1.6.1)

Two CC0 Quaternius animals are in the package (`Art~/Animals`, credited in `Art~/CREDITS.md`): the **wolf** stands in
for the coyote and the **eagle** for the red-tailed hawk, tinted to each species. They are used automatically for
species with no model; a real coyote or hawk from a pack you import wins over them.

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

  | Species | Stand-ins it will accept (closest first) |
  |---|---|
  | Deer | roe, elk, reindeer, caribou, moose, antelope, gazelle, impala |
  | Fox | shiba, fennec, jackal, dog (the last resort: the coyote takes a wolf first) |
  | Coyote | wolf, dingo, dhole, jackal, husky, shepherd, dog |
  | Squirrel | chipmunk |
  | Raccoon | badger, red panda, coati |
  | Opossum | rat, mouse |
  | Groundhog | beaver, gopher, prairie dog, capybara, hamster, guinea pig |
  | Wild turkey | pheasant, grouse, partridge, quail, guineafowl, chicken, rooster, hen, peacock |
  | Red-tailed hawk | eagle, falcon, buzzard, harrier, osprey, kite, vulture, raptor |
  | Great blue heron | egret, bittern, ibis, spoonbill, pelican, crane, stork, flamingo |
  | Spotted sandpiper | killdeer, plover, dunlin, snipe, shorebird, wader, tern, seagull, gull |
  | Cardinal | songbird, finch, sparrow, robin, oriole, canary, chickadee, wren, tit, thrush, warbler, swallow, bird (one model, tinted) |
  | Blue jay | bluebird, then the same songbirds (tinted) |
  | Red-winged blackbird | grackle, starling, sparrow, songbird, crow, raven, thrush, bird (tinted) |
  | Kingfisher | songbird, sparrow, bird (tinted) |
  | Phoebe | sparrow, songbird, finch, wren, chickadee, bird (tinted) |
  | American crow | none: crow or raven itself |
  | American robin | none: robin itself |
  | Chickadee | none: chickadee, or tit |
  | American goldfinch | none: goldfinch, finch or canary |
  | Song sparrow | wren (the sparrow itself is the exact match) |
  | Black bear | none: bear itself (never polar, panda, teddy or koala; a model named black or dark first; always tinted black) |
  | Rabbit, owl | none: only the animal itself |

  The full tables, the ranking and the word rules are in [Systems.md](Systems.md) (1.7.0). **Reserved names:** woodpecker,
  hummingbird, duck, mallard, goose and swan are kept for future species, so a model with one of those words in its name
  is **never** used as a stand-in. A species that is not in `WildlifePackSetup`'s name table is never matched.

- shows one row per species (best match first: the real animal before a stand-in, then the closer look-alike, then the
  model with the most recognised clips, then path order so the pick is the same on every machine), with a size, a yaw correction and **tint** (on by default for stand-ins);
- **Set up ticked species** runs the same factory as Animal Model Setup for each row. It classifies clips by name,
  builds the Animator Controller, scales the model to the species' real size (birds by beak-to-tail length), puts its
  feet on the ground and assigns the prefab. Since 1.8 it also converts built-in-shader materials for URP, removes the
  pack's scripts and loops the clips (above).

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
| **ithappy: Animals FREE** ([Asset Store](https://assetstore.unity.com/packages/3d/characters/animals/animals-free-260727)) | Asset Store EULA, free | Deer, chicken, dog, horse, tiger, kitty, penguin (confirmed by the audit); idle, walk and run only | Import locally, never commit |
| **Dinopunch: Living Birds** ([Asset Store](https://assetstore.unity.com/packages/3d/characters/animals/birds/living-birds-15649)) | EULA, free | Seven animated birds, confirmed by the audit: blue jay, cardinal, chickadee, crow, goldfinch, robin, sparrow (16 clips) | Old (2018, Unity 4.5): built-in shaders, and its own scripts; both handled automatically since 1.8 |
| Quaternius / other CC0 bird packs | CC0 | Songbirds, owls | Search Poly Pizza and itch.io filtered to CC0 |
| Smithsonian 3D (3d.si.edu) | CC0 | Insect scans for macro | Static: needs the reserved macro behaviours |

Paid, if a gap remains after the free route: **BundleSmith Animated Forest Animals** (~$13–33: deer, fox, rabbit,
squirrel, raccoon, wolf, with clips that match WildLens's states exactly ([itch.io](https://bundlesmith.itch.io/animated-forest-animals-pack-low-poly-stylized-models)));
**Low Poly Bird: Predator Pack** (eagles, barn owl, falcon) for the hawk and owl. Not usable: "personal use" Sketchfab models, Unreal-only packs, re-uploads of paid
packs (CC-BY rigs such as the Khronos Fox and free-plan AI generator output are usable only with credit, since 1.10).

## Suggested order

Done by the user's packs (1.8): deer, turkey, fox (tinted dog), the songbirds, the bear; coyote and hawk from the shipped
wolf and eagle. What is left, most photographed first:

1. **Cottontail and squirrel**, then **raccoon and groundhog**: a stylised forest-animals pack usually has several.
2. **Owl and heron**: the reserve's distinctive silhouettes; any stylised version reads.
3. **Opossum and sandpiper**, as packs turn up. Check each listing for its licence and its contents before importing.

After each step, playtest: photograph the animal at 4× and 16× at dawn and golden hour, and send screenshots. Note
any that are the wrong size (fix with the row's size field) or face backwards (yaw 180).

## Licence rules (relaxed in 1.10)

Since 1.10 (director: "assets no longer need to be CC0, just free") the repository may hold any free model that **may be passed
on**: CC0, or an attribution licence such as CC-BY with the author credited in `Art~/CREDITS.md`. Store packs (free ones included)
and "personal use" models can never be committed, because their licences forbid passing the files on; you may import anything
free into your own project. **The model search finished within 1.10**: eight free animated models from OpenGameArt (a fox, rabbit, squirrel, rat, duck, turtle,
snake and skunk; all CC0 but the skunk, CC BY 4.0, credited in `Art~/CREDITS.md`) are committed in `Art~/Animals` and swap in on the next Rebuild
by the upgrade rule, unseen in Unity. Nothing free that may be passed on was found for a turkey, raccoon, owl, heron, groundhog or sandpiper
(poly.pizza wants a browser check, kenney.nl refuses, Quaternius' downloads are on Google Drive): that is a browser job for the director.
Wildlife Pack Setup and the factory use whatever you import; Wildlife Pack Setup and Animal Model Setup use them from there and write prefabs to your own
`Assets/WildLens/Animals`.
