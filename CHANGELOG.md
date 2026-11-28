# Changelog

## [1.10.1] A compile error on Unity 6000.6
1.10.0 did not compile on Unity 6000.6: the Asset Audit used a Unity command (`objectReferenceInstanceIDValue`) that this
Unity version has turned into an error. WildLens's compile check cannot see such errors, because it checks against older
Unity libraries. The audit now reads that value in a way that works on every version. Nothing else changed.
**What to do:** Update the package, wait for the Console to clear, then Rebuild. If any red error remains, send it.

## [1.10.0] Voices
Your first 1.9 screenshots, taken in real Unity (URP, Unity 6000.6), were the first time anyone saw 1.9 running. They found in
minutes what reading code could not. 1.10 answers them, and adds what the reserve has been missing: **sound from the animals
themselves**, and **somewhere dry to stand when it rains**. **None of 1.10 has been heard or run in Unity yet.** Everything
compiles in five configurations; the voices were judged only from pictures of their sound (spectrograms).
### New models (a late addition, found the same day)
With the asset rule relaxed and four model sites opened to the build machine, eight free animated models were found, checked
for a licence that allows passing them on, converted and added. They ship with WildLens and **swap in by themselves on the next
Rebuild**. None has been seen in Unity: their size, facing and colours all need a screenshot.
- **Red fox**: a real fox (pointed muzzle, brush tail; idle, walk, run and a foraging clip). It replaces the tinted dog.
- **Eastern cottontail**: a rabbit (idle, hop, run). Its ears are long, so it leans towards a hare.
- **Eastern gray squirrel**: an upright squirrel with a curled tail (idle, hop, run).
- **Virginia opossum**: a rat stands in for it (idle, walk, run). Crude, but the outline is close.
- **Waiting for species of their own** (the next step): a duck, a turtle, a snake and a skunk.
- Sources and authors are in `Art~/CREDITS.md`. All are CC0 from OpenGameArt except the skunk, which is CC BY 4.0 (credited).
- **Not found:** a turkey, a raccoon, an owl, a heron, a groundhog or a sandpiper that may be passed on. Poly Pizza, Kenney and
  the Quaternius downloads could not be reached even after the sites were allowed (a browser check, a refusal, and files kept
  on Google Drive). Those remain a job for a browser: download, drop into `Assets`, and WildLens swaps them in.
### From your first 1.9 screenshots
**What worked**, seen for the first time: the 1.9 viewfinder (data row, brackets, grade word), the keeper print, the generated
wildflowers in the meadow, the road pull-off, the bike, and a distant waterfall.
**What was wrong:**
1. **The Northern Cardinal rendered solid magenta through the lens.** Magenta means a material the render pipeline cannot draw.
   The same is expected of every Living Birds bird used without a tint: cardinal, blue jay, crow, robin, chickadee, goldfinch and
   song sparrow. **The cause could not be proven from code**, so 1.10 does not guess at it. It makes the problem impossible
   instead (see "Magenta birds" below) and prints what every animal's materials really are, so the cause shows itself.
2. **The Wild Turkey was almost black at dusk.** A tint replaces the model's colour, and the 1.9 "darker bronze" was too dark.
   The colours are lighter, and tints on textured materials are now brightened a little.
3. **Console errors** `AnimationEvent 'ResetHopInt' on animation 'forwardHop (looped)' has no receiver` for Living Birds models.
   The pack's clips carry events for the pack's own scripts, which WildLens removes. Animal animators now ignore animation events.
4. **Three things in one wide screenshot are not yet identified.** They are open questions, **not fixed**: a tan flat block on top
   of a rock by a viewing platform; a purple speck on distant rocks; and trees that read as low-poly (which may mean the
   Environment Kit pulled in a low-poly tree pack). Close-up screenshots are asked for below.
### Voices (new)
Twenty-two of the twenty-seven species now have a voice, made in code (nothing is downloaded or committed). A call comes **from
the animal that made it**, in 3D, so hearing a cardinal means a cardinal is there.
- **Songbirds and small birds:** northern cardinal (a whistled *cheer, cheer, cheer*, then *birdie-birdie*), blue jay (a harsh
  *jay!*), black-capped chickadee (*fee-bee* and *chick-a-dee-dee-dee*), tufted titmouse (*peter-peter-peter*), American robin
  (a carolled *cheerily, cheer-up*), American goldfinch (a canary-like twitter), song sparrow (three clear notes and a buzzy
  trill), red-winged blackbird (*conk-la-REE*), eastern phoebe (a raspy *fee-bee*), Louisiana waterthrush (falling whistles over
  the water), eastern bluebird (a soft, low warble).
- **Water and big birds:** belted kingfisher (a dry rattle), spotted sandpiper (a thin *peet-weet*), great blue heron (a hoarse
  *fraaank*, now and then), American crow (*caw, caw, caw*), wild turkey (soft clucks and yelps), red-tailed hawk (a falling
  scream from high up, fair weather only).
- **At dusk and after:** barred owl (*who-cooks-for-you*, at dry dusks), coyote (one long howl, then yips, at dusk).
- **Mammals, mostly only alarms:** white-tailed deer (a breathy snort), gray squirrel (a stuttering scold), groundhog (a sharp
  whistle).
- **Silent on purpose:** the red fox, cottontail, raccoon, opossum and black bear. A fox's scream or a bear's huff is either
  startling or impossible to make believable from code, and in a quiet game their silence is how you meet them.
- **Frogs** (green frog, northern leopard frog, gray treefrog) call from the small-life frogs themselves, in a chorus by their
  own conditions: more in the hours after rain, the gray treefrogs only on wet nights.
- **A soft bee buzz** when a visible bee is within 3 m (never in rain), and **a cricket bed** from dusk (about 18:30, full by 19:30)
  through the night, thinner just after rain and silent in it.
- **Sparse and budgeted:** at most six animal voices and four frog voices sound at once; nothing is heard beyond its range, and
  it fades over the last third of it. Songbirds are loudest at **dawn** (the chorus) and burst out in the **first sun after
  rain**; in rain nearly everything falls silent; crows are loudest at dusk (the roost); the owl calls at dry dusks. Neighbours
  of the same species sometimes **answer** each other (counter-singing).
- **The one sound tied to you:** an animal may give an **alarm call**, only at the moment it first turns Alert (a snort, a scold),
  rarely, with a cooldown. Everything else follows the hour, the weather and the animal's own timer, never where you are.
- **Night is short.** A game night lasts about 11 real seconds, so the owl, the coyote and the treefrogs are in practice heard at
  **dusk**. While time runs fast (the night, or holding T) the animals thin to a fifth and only the beds (crickets, frogs) stay.
- **Cost:** every call is rendered once in code, cached (about 4 MB in all) and warmed gently after you press Play (one clip
  every tenth frame, never two in one frame), so no chorus stalls.
- **One tuning point:** each species' voice is one entry in `VoiceBank`. Assets leave their `voice` disabled; ticking **Enabled**
  on a species asset overrides that one species by hand. The journal's field notes for each voiced species gain one sentence
  describing the voice.
- **Settings:** `wildlifeVoices` (on) and `masterVolume` (0.8) on the Voices object; saved as the PlayerPrefs keys
  `WildLens.Voices` and `WildLens.VoiceVolume`. There is no on-screen setting yet.
- **How it was judged:** only from spectrograms. The contours look right for cardinal, chickadee, waterthrush, owl, coyote,
  goldfinch, treefrog, leopard frog, hawk, kingfisher and crickets. The timbre is only plausible for jay, crow, heron, turkey,
  squirrel, deer, blackbird, sparrow and bluebird. **Your ears decide.**
### Shelter from rain (new)
- **The Meadow Blind, the Reserve Board's eaves and the Rock Shelter are now dry.** Under their roofs the rain is removed while it
  keeps falling outside; the rain sound goes **quieter and duller** under cover (like drumming on a roof) and eases back when you
  step out.
- Press **F3**: a new line reads `shelter 0.00` out in the open and up to `shelter 1.00 (place)` under a roof.
- **Rain still falls under tree canopy.** The game has no runtime canopy data, so only these three roofs stop it.
### Magenta birds: how 1.10 makes it impossible
- **A shader's name is not evidence.** 1.9 decided "can URP draw this?" mostly from the shader's name. 1.10 tests the shader
  itself: no material, no shader, the error shader, a shader not supported here or with compile errors, or a shader none of whose
  passes URP draws (a pack's own surface shader keeps built-in passes whatever it is called).
- **Every material URP cannot draw is replaced**, including empty slots (a plain URP material stands in), after the tint too.
  Materials are updated in place, never deleted and recreated, so saved prefabs keep their references.
- **Models already built are repaired** on the next Rebuild without being rebuilt (their size, facing and tint stay).
- **The Console now says what each animal got**: `materials: 2 (Universal Render Pipeline/Lit ×2)` on every model line, and the
  `[WildLens] Wildlife models refreshed …` line lists each species. Anything repaired is reported in its own warning.
- **Asset Audit Report has a new section, "WildLens animals as built":** one row per material slot of each species prefab: the
  material file, shader, whether URP draws it, the main texture and the colour.
- **Factory version 4:** every model WildLens built itself is rebuilt on the next Rebuild.
### Other fixes
- **The turkey** is lighter: bronze-brown overall, a lighter bronze second colour and a red head and legs.
- **Tints on textured materials are brightened** (up to 1.5× when the texture's brightness is known, otherwise 25% toward white)
  and never go below a luminance of 0.16. Only the crow, the blackbird and the bear, which really are black, keep the old rule.
- **Pack animation events** are ignored by every animal and by the small-life animators the builder makes.
- **Species assets: data version 5.** A species asset below 5 is upgraded gently: weather habits arrive
  **only where the asset has none of its own**; field notes that are empty or still the default take the new text (with the voice
  sentence), and a note you edited keeps your words and only gains the missing sentences; the owl's and crow's rhythm change only
  if still exactly the pre-1.9 default; the **fox** takes its 1.9 colours only if still exactly the pre-1.9 default; the
  **turkey** takes the new colours only if it is still the 1.8 default or darker than a luminance of 0.2 (a turkey you tuned
  brighter is left alone). Voices are never copied onto assets.
### What to do
1. Update the package in Package Manager and wait for Unity to finish compiling. **Nothing to import.**
2. **Rebuild** (WildLens ▸ Vertical Slice Builder ▸ Rebuild Slice In Open Scene). **This one is required:** every animal model is
   rebuilt (factory version 4), and the shelters are added to the scene.
3. Run **WildLens ▸ Asset Audit Report** and **paste it into the chat.** Its new "WildLens animals as built" table shows each
   bird's materials, which is how we find the cause of the magenta.
4. Press Play and take the screenshots and notes below.
### What to look at, listen for and screenshot next
**Look:**
1. **The Northern Cardinal and a blue jay through the lens.** Any magenta?
2. **The Console text starting `[WildLens] Wildlife models refreshed`.** Copy the full text, every line.
3. **The turkey in daylight**, and again at dusk.
4. **Close-ups of the three unidentified things:** the tan block on the rock by the viewing platform, the purple speck on the
   distant rocks, and the trees, plus the Console line starting `[WildLens] Environment Kit`.
**Listen** (headphones help; say which animal if anything is wrong):
5. **Dawn at the meadow:** robins and cardinals, answering each other.
6. **The first sun after the day-3 shower:** a burst of song.
7. **A wet dusk:** the gray treefrog's trill and the frog chorus at the pond.
8. **A dry dusk near the grove:** the owl. **The crows at the trailhead** at dusk.
9. **Walk towards a deer** until it snorts. **Crickets** at dusk.
10. **Is anything too loud, too frequent, or wrong-sounding?** Name the animal.
**Rain:**
11. **Press F3 inside the Meadow Blind and inside the Rock Shelter** while it rains (and under the Reserve Board's roof). Take a
    screenshot showing the `shelter` line and, if you can, the rain stopping at the roof.
### Not done, honestly
- **Nothing in 1.10 has been heard or run in Unity.** The voices are judged from spectrograms only.
- **The cause of the magenta is unproven.** 1.10 repairs every material URP cannot draw and reports what each model has; read the
  audit's "animals as built" table first.
- **The three unidentified things** (tan block, purple speck, low-poly trees) are open.
- **Rain still falls under tree canopy,** and the cricket bed is not yet muffled under a roof.
- **The turkey still reads as a chicken, and five species are still placeholder bodies** (heron, raccoon, owl, groundhog,
  sandpiper): no free model that may be passed on was found for them. See "New models" below for the ones that were.
- **The asset rule has changed (your decision, same day): "Assets no longer need to be CC0, just free."** Files committed to
  the repository may now carry any free licence that allows passing them on (CC0, or CC-BY with the author credited in
  `Art~/CREDITS.md`). Asset Store packs and "personal use" models still cannot be committed, because their licences forbid
  passing the files on; you may import anything free into your own project. The build machine can now reach opengameart.org,
  quaternius.com, poly.pizza and kenney.nl.
- **The journal's Wildlife page still lists unseen species as "???" rows.** Still your decision.
- Night is about 11 seconds long, so "night" work (a night sky, night-only calls) first needs a decision on whether nights should
  be playable.

## [1.9.0] After the Rain
The 1.8 playtest was the first full run of the pack pipeline in Unity, and it was a success: the reserve "immediately feels
more alive". Two notes came back: the fox reads a little like a dog (it is a tinted dog model) and the turkey a little like a
chicken (a tinted chicken). 1.9 makes the place answer the weather, fills the macro lens with small life that needs no packs,
quiets the screen, and gives the reserve some real geography. **None of 1.9 has run in Unity yet.** Everything compiles in
five configurations; everything you will see is unproven until your screenshots.
### Wildlife events (natural, never scripted)
Where an animal is and what it does now depends on the **hour**, the **weather now**, the **weather lately** and the
**four-day forecast**. Nothing is triggered by you, counted, marked or rewarded; the same day always goes the same way, so
you can learn it. Each species' field notes hint at its habit, in a sentence, never an instruction.
- **Deer and turkeys** wait out rain under the trees, then come out to feed on the meadow in the first sun afterwards.
- **Songbirds** (cardinal, blackbird, goldfinch, song sparrow) sit tight in the brush while it rains and come down to bathe at the
  water's edge for the next two and a half game hours.
- **Robins** work the open grass for worms in the first hour after rain, then bathe at the water.
- **The hawk** soars over the ridge only in fair weather (morning to golden hour); in rain, under grey skies or just after
  rain it waits on a snag.
- **The barred owl** sits on a snag at the meadow's edge at a dry dusk; in rain it stays in the grove.
- **The crows** gather at the trailhead to roost at dusk.
- **The black bear** keeps to deep cover in rain and comes down to the creek at dusk only when the evening is fair.
- **The heron** is livelier after rain; **the cottontail** is out on the meadow edge for hours after a wet night; **the raccoon
  and the opossum** come out early on a grey afternoon.
- **Staggered**: a flock does not move on one frame. A change of weather is answered after a stable delay (up to 14 s for a
  group, a few seconds more for each animal). A change of day period still moves everyone at once, as before. The delay
  counts faster while you hold T.
- **Seeded rain memory**: the weather now remembers when it last rained, and at the start of a game it works that out from
  its own forecast, so the morning after the dusk rain of forecast day 4 is damp even if you were not there to see it fall.
- **Existing species assets** are upgraded gently (data version 4): they receive the habits and the field-note hints, plus
  the new rhythms for the owl and the crow and the new colours for the fox and turkey, only where you have not changed
  those. Awareness, behaviours, groups and anything you tuned stay.
### Three new birds
- **Eastern bluebird**: on the fence posts of the Old Cabin Ruins; out on the meadow posts in the first sun after rain.
- **Tufted titmouse**: the woods round the Rock Shelter; nearly as tame as the chickadee.
- **Louisiana waterthrush**: Hemlock Run, below the cascade; wary, and busiest by the water just after rain.
- All three are tinted stand-ins from your Living Birds pack until a truer model turns up. **Twenty-seven species** in all.
  The new ones are residents of their places only.
### Better models replace stand-ins by themselves
- **The upgrade rule.** A model WildLens chose for a species is replaced automatically when a strictly better one is
  imported: the real animal over any stand-in, a stand-in that is nobody else's real animal over one that is, a closer name
  over a farther one. Import a model named Fox or Turkey and it takes over on the next rebuild. A model you chose by hand in
  a setup window is never touched, and a species you sent back to a placeholder stays one. One Console line lists each
  change.
- **Factory version 3** re-tints the animals WildLens built itself: the fox is now a brighter orange-red with a clean white
  tail tip and dark socks, the turkey a darker bronze-brown with a red head and legs. The colours are baked into each model's
  materials when it is built, so the first rebuild after updating refreshes them.
- Real sizes for the three new birds (bluebird 18 cm, titmouse 16 cm, waterthrush 15 cm).
- "Bluebird" is no longer a stand-in word for the blue jay.
### Small life without packs
- **26 generated models**, made in code and saved in your project (`Assets/WildLens/Generated/SmallLife/Models`; nothing is
  downloaded or committed): monarch, tiger swallowtail, cabbage white, luna moth, common whitetail, ebony jewelwing, bumble
  bee, honey bee, firefly, lady beetle, Carolina mantis, leopard frog, green frog, gray treefrog, red eft, red-backed
  salamander, whitelip snail, honey mushroom, yellow fly agaric, turkey tail, oxeye daisy, black-eyed Susan, common
  milkweed, common blue violet, haircap moss and common cattail. They are stylised and **have never been seen in the
  engine**. A model from your packs always wins; the builder setting **Generated Small Life** (on by default) turns them off.
- **29 subjects in all**: the 2 bundled plants, the 12 of 1.7 and 15 new ones: eastern tiger swallowtail (*Papilio glaucus*),
  cabbage white (*Pieris rapae*), ebony jewelwing (*Calopteryx maculata*), western honey bee (*Apis mellifera*), green frog
  (*Lithobates clamitans*), gray treefrog (*Dryophytes versicolor*), eastern red-backed salamander (*Plethodon cinereus*),
  yellow fly agaric (*Amanita muscaria* var. *guessowii*), turkey tail (*Trametes versicolor*), woodland fern (*Dryopteris*
  sp., the bundled fern scan), common milkweed (*Asclepias syriaca*), black-eyed Susan (*Rudbeckia hirta*), common blue
  violet (*Viola sororia*), haircap moss (*Polytrichum commune*) and common cattail (*Typha latifolia*).
- **They come out for the conditions, not the clock alone**: butterflies and bees in sunshine, with a second wave in the
  first sun after a shower; dragonflies and the jewelwing on warm, dry days; fireflies on fair evenings (after a dry
  afternoon, from about 18:45, each a little later, over about twenty game minutes); gray treefrogs on wet evenings and
  nights; frogs in greater numbers after rain and at dusk; newts and salamanders and snails after rain; more mushrooms
  the day after rain. All of it reads the weather and the clock only, so it follows the forecast.
- **Movement**: wings (butterflies rest with them closed and now and then open them flat; a jewelwing opens and shuts its black
  wings; a bee's buzz and a dragonfly's shimmer), a dragonfly keeps its own perch, fireflies **rise from the grass** at dusk
  and flash.
- **New habitats**: tree trunks (luna moths, treefrogs), boulders and old logs (moss), pond edge only (cattails), the creek
  only (the jewelwing). **One insect per flower**, and monarchs choose milkweed first.
- **Fireflies in clusters**: three on the meadow verge and one at the pond.
- **Every subject can be graded.** Last time a plant half a metre tall was Excellent from anywhere, and a ground subject
  could need you closer than the camera can get (it is 1.65 m up standing, 0.95 m crouched). Frame-fill thresholds are
  reworked from that arithmetic, and the tall ones (the fern, the cattail) are graded on one part: a frond's crown, a seed
  head.
- Existing subject assets keep your tuning and gain only the new fields (subject data version 2).
- **First rebuild**: making the models takes a few seconds and shows a progress bar; one Console line reports it.
### A quieter screen
- **Empty by default.** With the camera lowered the screen shows a faint centre dot and nothing else. The bottom-left hint
  block is gone, and so is the top-right status line (with its `Species 3/24` counter).
- **Context prompts**: a key and one to three words on a faint pill, bottom-centre, at most two. Each appears only when it
  is possible and useful (park at a pull-off, the camera when an animal is ahead, macro near a small subject, zoom and
  shutter with the camera up, the journal after your first photograph), fades after a few ignored seconds, and **retires for
  the session** once you have done the thing three times. Nothing is saved.
- **H opens "Field Guide: Controls"**: one paper card with every key in four groups. It pauses the world like the journal.
- **One notice style**: a small title and one line on a faint pill, under the top edge, never more than two at once. A hidden
  place still gets its name, large and calm, once.
- **The viewfinder is tidier**: one thin line weight, quiet grade colours, and a data row along the bottom (focus dot and
  MACRO, the zoom steps, focal length and f-number).
- **The journal gains a Field notebook page** (A / D to switch pages). It lists only what you have found, grouped by kind,
  with your best photograph: never a total, never an unfound row. The Wildlife page is as before.
### The reserve
- The world is still 900 by 900 m. It gains **geography, not distance**.
- **Hemlock Ravine** with its tributary **Hemlock Run** and a stepped cascade at its head, joining Willow Creek. It is carved
  where the land falls far enough toward the creek: that includes the default seed 7. On a seed or terrain without the
  relief it is skipped and one Console line says why. One footpath leads in, with one sign and a log seat that looks up at
  the cascade. A Louisiana waterthrush lives by the run and an eastern phoebe on the cascade's ledge.
- **Four more hidden places** (seven in all; found and named once, never counted, on no sign or map):
  **Cascade Overlook** (a ledge above the cascade, with a seat), **Hidden Pond** (still water in a wooded hollow, far from
  any path; herons, blackbirds, raccoons), **Rock Shelter** (an overhanging crag with a dry floor, a cold fire ring and
  a view) and **Old Cabin Ruins** (a stone foundation, a chimney stub and the last fence posts, where the bluebird sits).
  Each is skipped cleanly where the land has no site.
- **Carved late, so nothing else moves.** The ravine and the new places are cut after every other scenery pass and use their
  own random stream, so for a given seed the existing trees, rocks, paths and destinations stay exactly where they were
  (measured on eight seeds in a prototype before it was written: 0.00 m of height change under every existing path and
  destination). What stood where the ground moved is settled onto the new surface or lifted out.
- Measured on the default seed 7: the ravine is about 186 m long and up to 8.2 m deep, the cascade about 8.5 m in three drops, and the footpath about 294 m with a gentlest-to-steepest grade under 8°. A ravine appears on 4 of 8 test seeds, cascades range from 3.5 to 10.4 m across seeds, and no wall is steeper than 50° (the player's limit).
- The Hidden Pond has two shore water spots and a bank residence, the Old Cabin clearing a small meadow spot (so the bluebird stays by the fence) and the Rock Shelter's woods a woodland anchor (the titmouse). Small life at the Hidden Pond now sits at its shoreline.
- Builder settings **Hemlock Ravine** and **More Hidden Places** (both on).
### Fixes found before release
- **Willow Falls' falling water was drawn in the wrong place since 1.5** (the curtain was offset by the site's position; what you saw was the rock, pool, spray and sound). It now falls from the lip. Its shape has never been seen in the right place: screenshot it first.
- **Cattail Pond's lily pads were drawn far from the pond for the same reason**; they are now on the water.
- One Console line, `[WildLens] Reserve: …`, says what was built (and why anything was skipped).
### Under the hood
- Environment version 21 (**rebuild the slice**). Species data version 4, subject data version 2, factory version 3, small
  life model version 2 (wings, petals, leaves and blades are two-sided in the mesh itself).
- The rain memory lives in one place (`WeatherCycle`) and both the animals and small life read it. Their "sun after rain"
  windows differ on purpose (animals two game hours, small life three).
- The Console line for the small life reads `[WildLens] Small life: N macro subjects placed (… generated, … from your packs;
  … in second tiers…)` with one line per subject.
- **Old pack scripts can block every update.** The Living Birds pack's `scripts` and `demo` folders do not compile on
  Unity 6. Compile errors in your project stop **all** package updates, and WildLens does not need those folders (only the
  models and clips). Delete them. If a pasted Asset Audit shows an older WildLens version than the one released, a
  compile error in your project is the likeliest reason: look for red lines in the Console first.
### What to do
1. Update the package in Package Manager and wait for Unity to finish compiling. **Nothing to import.** If the update does
   not appear, or the Console shows red errors, see "Old pack scripts" above.
2. **Rebuild** (WildLens ▸ Vertical Slice Builder ▸ Rebuild Slice In Open Scene). The first rebuild makes the small-life
   models and re-tints the fox and the turkey.
3. Press Play and **watch forecast day 3 through the afternoon shower and into dusk** (hold T to let time pass). Then the
   dusk rain of day 4. Take the screenshots below.
### What to look at and screenshot next
1. **The Console lines**: `[WildLens] Reserve: …` and `[WildLens] Small life: …` (and any `Wildlife models` line). Paste them.
2. **The empty screen**, camera lowered: only a faint dot.
3. **The prompts**: the park prompt at a pull-off, "Raise camera" with an animal ahead, then macro near a flower.
4. **The controls card** (H) and **the field notebook page** (J, then D) after a few macro photographs.
5. **A monarch on milkweed** at midday, wings open if you can wait for it, in sun.
6. **Fireflies** on the meadow verge at a fair dusk (forecast day 1 or 2), and none on day 3 or 4.
7. **Frogs at the pond** after the afternoon shower, and the gray treefrogs on trunks that evening.
8. **Songbirds bathing** at the water's edge in the hours after the shower, and a robin on the open grass in the first hour.
9. **The owl on a snag** at a dry dusk, and the crows at the trailhead at dusk.
10. **Willow Falls' falling water** from the front and from the ford, and **Cattail Pond's lily pads** on the water.
10b. **Hemlock Ravine** from the log seat at the end of its footpath, and from the rim.
11. **Each new hidden place**: Cascade Overlook, Hidden Pond, Rock Shelter, Old Cabin Ruins. The Rock Shelter's crag and
    the cascade's ledges are the least proven shapes: look for gaps, floating rock or flat-looking faces.
12. **The fox and the turkey** after re-tinting, at 4×.
### Not done, honestly
- **Nothing from 1.9 has run in Unity.** The generated small-life models are stylised and unseen in the engine; sizes,
  colours and wing motion may need work. The Rock Shelter's crag and the cascade's ledges are the least proven shapes.
- **No real models for the fox, the turkey or the eight placeholder species** (cottontail, squirrel, heron, raccoon, owl,
  groundhog, opossum, sandpiper). A search for free models failed from the build machine: every CC0 model site it tried
  (quaternius.com, poly.pizza, opengameart.org, itch.io, kenney.nl) is blocked there, so nothing could be checked or shipped.
  This is a job for you, and the rules are below.
- **Animals may stand in different places than in 1.8 for the same seed** (new hotspots change how homes are chosen).
- **Rain still falls through the Rock Shelter's roof** (and every roof).
- **The journal's Wildlife page still lists unseen species as "???" rows with "Species X of N".** That is old behaviour, and it
  sits uneasily with "no checklists". It is a decision for you (see the roadmap).
- Scale on low-end hardware is untested: about 200 small-life instances (generated) on top of 27 species.
### A shopping list for models (your job; check each page for "CC0" before use)
The build machine could not open these pages, so each entry comes from search results only. **Open each page and check that
the licence is CC0 (or another one that allows free use without credit) before you use it.**
- **A fox with idle, walk and run** on OpenGameArt: `https://opengameart.org/node/90146`.
- **Quaternius** packs: `https://quaternius.com` and `https://quaternius.itch.io`. Their farm-animal pack has a cow, horse,
  llama, pig and pug, and no fox. Look for animal packs with a fox, rabbit, owl, raccoon, squirrel or heron.
- **Not usable**: the glTF sample "Fox" (its rig and animations are CC-BY), and Sketchfab "personal use" foxes.
- **Steps**: download, unzip, drag the `.fbx` into your project's `Assets` folder with a **plain name** (`Fox.fbx`, `Turkey.fbx`,
  `Rabbit.fbx`, `Owl.fbx`, `Raccoon.fbx`, `Squirrel.fbx`, `Heron.fbx`…). WildLens swaps it in on the next **Rebuild**. Start
  with the fox and the turkey, then the eight.
- Do not commit any of them to the repository.


## [1.8.0] New Neighbours
Made from your 1.7 Asset Audit, the first report from inside Unity. It proved the automatic setup works (the deer from
ithappy, the chicken as the turkey, the shipped wolf as the coyote, the shipped eagle as the hawk) and showed what was
wrong with everything else. **Nothing below has run in Unity yet** apart from what the audit showed.
### Six new species, each for a place
- **American crow**: the trailhead (between the Reserve Board and the bike bay) and one road pull-off; a group of 2 to 3
  at each. Bold: it notices you at 26 m, goes alert at 15 m and flies at 7 m.
- **American robin**: the trailhead grass and the Meadow Loop's edge; a pair. Meadow at dawn, morning and golden hour,
  woodland at midday, brush at dusk.
- **Black-capped chickadee**: the Fern Glade (a hidden place) and the Old-Growth Grove; 2 to 3 together. **The tamest
  animal in the reserve**: it notices you at 10 m, goes alert at 5.5 m and flies only at 2.5 m, and waits 8 s before
  deciding. Stand still and it comes close.
- **American goldfinch**: the Wildflower Meadow Blind's meadow; 3 to 4 together. Brush at dawn and dusk, meadow by day.
- **Song sparrow**: the Cattail Pond's brush, the Creek Alcove and two spots among the loop's brush piles. Single birds.
- **American black bear**: **one**, in the deep woods (the woodland spot farthest from the trailhead). Meadow edge at dawn,
  woodland in the morning and golden hour, rests at midday, the creek at dusk. The widest berth in the reserve: it
  notices you at 75 m, goes alert at 50 m and moves off at 28 m, and when it does it only ever walks away 45 to 75 m.
  There is no chase and no failure: keep still and far back and it settles. Placed from the existing site data with no
  random draws, so nothing else in a rebuilt reserve moves.
- Twenty-four species in all. The new ones draw from their own random streams, so adding them moves no animal that was
  already there.
### Pack animals that render and move properly
- **Built-in-shader models are no longer pink in URP.** The two bird and bear packs use built-in shaders. The factory now
  makes a **converted copy** of each such material (texture, colour, normal map, emission, metallic and smoothness, alpha
  cut-out or transparency and two-sided drawing carried over). Your packs' own materials are never touched. Written blind:
  the audit could name the shaders but not show the result.
- **Pack scripts are removed from the animals** (their own scripts, colliders, rigid bodies, audio, navigation, effects,
  lights and cameras), so nothing in a pack fights WildLens's own movement.
- **Real sizes.** A table of real animal sizes (cardinal 22 cm, blue jay 28 cm, crow 45 cm, robin 25 cm, chickadee 13 cm,
  goldfinch 12 cm, song sparrow 15 cm, barred owl 50 cm, turkey 1.0 m, black bear 1.6 m, coyote 1.25 m...) replaces
  sizes worked out from proportions. The heron is measured by its standing height (1.2 m). The 1.7 chicken-as-turkey was
  about 1.45 m; the five songbirds were 2 to 5 times too big.
- **Animations loop.** Pack clips usually have Loop Time off, so a walk played once and froze. Every clip that is not
  already looping now gets a looped copy inside the animal's controller (landing and take-off stay one-shot). The pack's
  clips are not touched.
- **Better clips chosen.** In a clip name, an earlier keyword of a role beats a later one (walk before jump: the bear's
  jump is no longer its walk). Clips with left, right, back, turn, combat or alternative in the name lose to plain ones
  (fly-straight over fly-left). With no idle clip a bird now stands in its "look" clip (it used to take the first clip in
  the file, which could be pecking for ever). Singing and calling are only ever the last choice for "looking around".
  Hit, stun, dodge, block, taunt, spawn and similar clips are never used.
- **Better models chosen.** A model called "controller", "manager", "spawner", "system" or "demo" is not an animal.
  Animation-only files and rigs are left out of the audit's animal tables.
- **Black bear**: a model named black or dark is preferred over the brown ones, and it is tinted black even though it is
  the real thing. A near-black tint is lifted slightly so a textured model does not turn into a black hole.
- **Old auto-built models are refreshed once.** On the first rebuild after updating, every species the automatic setup
  built with the old factory is rebuilt from the same model with the new sizes, loops and materials. A model you set by
  hand in a setup window is never rebuilt, and the **Placeholder** button now sticks (the species stays a placeholder).
- **The import watcher survives script reloads.** Importing a pack that carries scripts reloads Unity's scripts, which
  threw the waiting set-up away (so the cardinal and blue jay were missed). It is now remembered across the reload.
### Small life
- **Mushrooms from SinuousShrooms.** Up to **six models** of one subject are used (the best match and up to five
  variants). A folder name anywhere above a model can now name it (as a stand-in, at the lowest rank), so
  `SinuousShrooms/...` counts. Among equally good models the plainest names win and the middle size is picked, not the
  smallest scrap or the largest. Mushroom count is 8 (from 5), each 0.8 to 1.6 times its size, **at most 2 on one log or
  stump**; the rest go to the woodland floor.
- Pack materials for small life are converted for URP the same way (one set of copies per model, in
  `Assets/WildLens/Generated/SmallLife/Materials`).
- The audit and the macro matching ignore grounds, rocks, spheres, cubes, leaves, branches, pebbles, stones and demo
  scenes beside a pack's models ("Mushroom_On_Stump" is still a mushroom).
### Journal
- **The species list scrolls.** Seven rows show at once and the window follows the selection, with "more" hints above and
  below. W/S and the mouse wheel both move it (one species per 0.12 s, so a free-spinning wheel is not a blur).
### Asset Audit
- **Other static models** section: every model nothing else recognised, by pack and name (30 per pack, 150 in all), so
  the **motorcycle pack's 18 models** can be read by name. Vehicle rows list up to 12 child part names and say "by
  folder" when only a folder named them.
- Animation-only files are one line per folder. Rigs and demos are left out. Cat, kitty and penguin ("pinguin") are named
  as not-Willow-Creek animals; a bear that is not a black bear is no reserve species.
- The audit now uses the same shader test as the converter, so the report and the fix agree.
### Documentation
- `WildlifeCampaign.md` is filled in from your audit (the real pack inventory, the 24 species and their models).
- Environment version 20 (**rebuild the slice**).
### What to do
1. Update the package in Package Manager (wait for Unity to finish compiling).
2. **Rebuild** (WildLens ▸ Vertical Slice Builder ▸ Rebuild Slice In Open Scene). The first rebuild refreshes your
   existing pack animals and sets up the new species.
3. Run **WildLens ▸ Asset Audit Report** again and **paste it into the chat** (it now names the motorcycle models).
4. Press Play and take screenshots (below).
### What to look at and screenshot next
- **The Console lines** that start `[WildLens] Wildlife models`: the "refreshed for 1.8" line (which animals were rebuilt),
  and the "now use animal models from your packs" line with the species it lists and the "No match yet" list. Paste both.
- **Each pack animal at 4× and 16×**: the deer, the chicken-turkey, the cardinal, the blue jay, a crow at the trailhead,
  chickadees at the Fern Glade or the grove, goldfinches near the blind, a robin on the trailhead grass, and the bear at
  dawn at the woodland edge. Look for **pink, frozen, the wrong size or facing backwards**; send a screenshot of any.
  A wrong size or facing is one field each in **Wildlife Pack Setup**.
- **Mushrooms on the grove's logs** with the macro lens (M).
- **The journal**: scroll the species list with W/S and the wheel.
### Not done, honestly
- **Nothing here has run in Unity.** The URP conversion of the two packs' real shaders was written without seeing the
  result. Sizes and facing of every model need a screenshot, and pack animals may need a yaw.
- **Eight species are still placeholder bodies** because no imported pack has them: cottontail, squirrel, heron, raccoon,
  owl, groundhog, opossum and sandpiper.
- The red fox is the pack's dog, tinted: a stand-in. Red-winged blackbird, kingfisher and phoebe share the Living Birds
  sparrow, tinted.
- No butterflies, frogs or insects were found in your packs, so only the mushrooms and the two bundled plants are small
  life. There is no fallback model.
- The motorcycle pack is still unjudged. Horse, tiger, kitty and penguin are not used (not Willow Creek animals).

## [1.7.0] Field Audit & Small Life
### Find out what is in your packs
- **WildLens ▸ Asset Audit Report** (also a button in the builder window). One click lists every model in your project
  by pack: which animal it is and which reserve species it can be (the real thing or a stand-in), its triangle count and
  size, its animation clips and which behaviours they cover (idle, walk, run, eat, look, fly...), and any shader that
  will show pink in your render pipeline. It also lists small-life models (butterflies, frogs, mushrooms, flowers),
  vehicles (with whether they have separate wheels and a steering part), and animated models it could not name.
- It writes `WildLensAssetAudit.md` in your project folder and **copies it to the clipboard**: paste it into the chat and
  the next steps can be decided from facts, not guesses. It changes nothing in your project. If the full report is too
  long for the clipboard, a short version (with each animal's clip names) is copied and the dialog says so.
### Better automatic animal matching
- **More stand-in names**, with the closer look-alike preferred (a wolf before a dog for the coyote). Ties are broken
  the same way on every machine.
- **Names kept for future species** (woodpecker, hummingbird, duck, mallard, goose, swan) are never used as stand-ins,
  so a woodpecker cannot become your cardinal.
- **Clip names are read word by word.** "Threat" is no longer taken for "eat"; death, attack and hurt clips are ignored.
- The Console line after an automatic setup now also lists the species that found **no match**.
### Small life from your packs (macro lens, round two: first step)
- **Twelve new field-notebook subjects**, written as data: monarch butterfly, luna moth, common whitetail (dragonfly),
  bumble bee, spotted lady beetle, Carolina mantis, northern leopard frog, eastern newt, whitelip snail, common eastern
  firefly, honey mushroom and oxeye daisy. Each has its own hours, weather and place.
- **A subject appears in the reserve only if a model for it is found in your packs.** **WildLens ▸ Macro Subjects From
  Packs** finds the models; every rebuild does it too (builder setting **Auto Macro From Packs**). Nothing is faked: a
  subject with no model stays out, and with no packs imported the reserve is exactly as in 1.6.1.
- **Placed where they would be**: butterflies and bees on the loop's dandelions, dragonflies and frogs at the water's
  edge, mantises, beetles and fireflies on the meadow verges, newts on the woodland floor, snails, moths and mushrooms
  on logs and stumps.
- **Simple movement, patience always works**: butterflies and bees sit, then flutter a short way; a dragonfly keeps a
  perch and darts out and back; a frog sits until you come within 0.9 m (along the ground), then makes one small hop, never more than 1.2 m from where it started; fireflies drift and
  flash.
- **Hours and weather**: butterflies, bees and dragonflies shelter in rain; newts and snails come out only in rain and
  for three game hours after; moths and fireflies are out at dusk, night and dawn (fireflies dusk and night). Plants and
  mushrooms never vanish.
### Documentation
- New `Documentation~/WildlifeCampaign.md`: the director's plan for the audit, which species to keep, replace and add,
  small life, the bike, and where each new animal belongs in the reserve.
- Environment version 19 (**rebuild the slice**).
### What to do
1. Update the package in Package Manager.
2. Import the animal and nature packs you have (if you have not already), then run **WildLens ▸ Asset Audit Report**
   and **paste the result into the chat**.
3. **Rebuild** (WildLens ▸ Vertical Slice Builder ▸ Rebuild Slice In Open Scene). Read the Console: it lists which species
   found a model and which did not, and which small-life subjects were placed.
4. Look at and screenshot next: the Console lines (animals and small life), the Meadow Loop verge and the pond edge with
   the macro lens (M), the Old-Growth Grove's logs, and the notebook count in the journal.
### Not done, honestly
- **The imported packs themselves were not inspected** (they live only in your project). Nothing here has run in Unity
  yet, so expect fixes on first use.
- No new animal species were added; they wait for the audit.

## [1.6.1] Animals and scenery without the scan step
- **Two animated animals ship with WildLens** (CC0, Quaternius): a **wolf as the coyote** and an **eagle as the
  red-tailed hawk**, tinted to each species' colours, with idle, walking, flying and perched animation. No download.
- **Animal packs set themselves up**: importing a pack, or rebuilding, gives every species without a model its best
  match. Your own packs' real animals win over the bundled stand-ins.
- **Nature packs set themselves up**: while the Environment Kit is empty, each rebuild scans the project for free
  nature packs (Unity's Terrain demo trees, shrubs, rocks, ground and grass) and uses them. Builder settings: **Auto
  Wildlife From Packs**, **Auto Environment Kit**.
- Animal models keep their own axis fix and stand with their feet measured in the idle pose (not a T-pose or flight pose).

## [1.6.0] Trailhead & Wildlife Packs
### The trailhead
- **One Reserve Board** replaces the kiosk and the separate guidelines post, trail register post and Reserve Road sign:
  a roofed, **three-sided** kiosk. Facing you as you arrive: the reserve name, time, conditions, map and destinations.
  Round the side: **PLEASE** (stay on the trails, give wildlife space, watch quietly) in big pictogram lines. On the
  side facing the bike: **RESERVE ROAD** (ride slowly, park at the pull-offs, walk the last stretch) with the trail
  register below.
- The **trail bike** stands in a **bay beside the board**, angled out to the trail, with a hitching rail. The Reserve
  Road starts there.
### Readability and polish
- Sign sub-lines are larger (about 8 cm capitals, from 6) with bigger pictograms; the LOOKOUT marker is smaller.
- **Interpretive panels** are larger with three short lines, so their text is about 5 cm tall instead of 3.5 cm.
- The Reserve Board's conditions line and distances are larger, and its footer is shorter.
- **Log seats** (the Creek Ford's, the lookouts', the hidden places') follow the slope instead of floating at one end.
### Wildlife: free packs, any style
- New direction: animals **don't need to be photorealistic or match**. Silhouette, animation and variety come first.
- **WildLens ▸ Wildlife Pack Setup**: import any free animal packs (several at once), Scan, and it matches models to
  all 18 species, the real animal first, then a **stand-in** (a wolf as the coyote, an eagle as the hawk, one songbird
  **tinted** into the cardinal, blue jay, blackbird, kingfisher and phoebe), and sets up every ticked species in one go.
  Species with no match keep their placeholder bodies. **No scanning needed:** importing a pack, or rebuilding, sets
  up every species that has no model yet automatically.
- Animal Model Setup now shares its code with the pack tool (and can tint too); birds scale by beak-to-tail length.
### Documentation
- AGENTS.md is now a navigation guide (overview, philosophy, state, directions, common mistakes, reading order);
  new `Documentation~/History.md`, `Architecture.md`, `Roadmap.md` and `TrailBike.md` (free bike models reviewed);
  `WildlifeAssetStrategy.md` rewritten for the new direction.

## [1.5.0] Falls & Field Notes
### Willow Falls (new destination, number on the board and map)
- Where Willow Creek naturally loses the most height, its fall is concentrated into a **2–7 m waterfall** over a scanned
  rock face, with a plunge pool and a small **ravine** below (rock walls, ferns, boulders). A falling-water curtain,
  **spray** and a **roar** you hear from 90 m.
- **After rain the falls run faster, throw more spray and roar louder**, settling back over a few hours: a learnable
  reason to come after the afternoon shower.
- A **viewing deck** on the ravine rim and a **lookout at the top** of the falls, with a sign and a panel; footpath,
  fingerpost and bike pull-off come with it. Resident: the **Eastern Phoebe** (new species, 18 in all).
### Hidden places (no markers, no counters)
- A **Hidden Overlook** on the upland, a **Fern Glade** in deep woods and a **Creek Alcove** out of sight of the trails,
  each with a seat or rocks and a view; the first time you walk into one, a quiet field note names it.
### Macro photography (groundwork)
- **M** switches to a macro lens with the camera raised: close focus (to 8 cm) on the small subject nearest the centre,
  graded on frame fill, focus and centring; "MACRO" in the viewfinder.
- **Field notebook**: small subjects you photograph are recorded in the journal (count in the journal header).
- First subjects: real scanned **Common Dandelion** (sunny loop verges; closes in rain and at dusk) and **Lesser
  Periwinkle** (fern glade and grove floor). Data for butterflies, dragonflies, bees, beetles, fireflies, mantises, frogs,
  salamanders and fungi is ready to be written as `NaturalistSubject` assets once models exist.
### Trail bike
- Reshaped after small trail bikes of the Trail 125 kind: exposed frame, horizontal single and skid plate, raised air
  intake, upswept exhaust with heat shield, long seat, big rear carrier, twin shocks, high fender, round headlight,
  mirrors, chunkier tyres.
- **Visible suspension**: wheels follow the real ground under a body smoothed for the rider; the fork dives under
  braking and the rear squats under throttle; faint idle shake.
- Parked: front wheel turned to the lock; parked **on the Reserve Road** it is wheeled onto the shoulder, lined up.
- Road cruise **45 km/h** (from 40) on the graded Reserve Road only; footpaths and open ground unchanged; gentler
  approach to top speed.
### Fixes
- **Signs and props no longer float on slopes**: every post and leg is lengthened to reach the ground under its own
  footprint (grounding pass after each build).
- **The pond reflects the sky it's under**: its reflection probe re-renders with the light and weather (it mirrored a
  blue morning under an overcast sky).
### Wildlife assets
- `Documentation~/WildlifeAssetStrategy.md`: options from CC0 to paid packs, the style decision, a recommended route.
- Environment version 16; art import version 6 (**rebuild the slice**).

## [1.4.0] Changing Skies
### Signs fixed
- **Sign lettering was mirrored** in 1.3: every left-aligned title, line and pictogram was placed on the reader's
  right, so it hung off the board's far edge or floated in the air (the boards themselves looked fine). A sign's front
  faces +Z, so its reader's right is the sign's -X. `SignKit` now takes positions in reader space and converts them;
  fingerposts were re-derived for both faces. Stained boards are lighter (they read as black slabs in shade).
- **WildLens ▸ Check Sign Lettering**: a probe that measures every label's glyphs against its board (off the edge,
  buried, facing the wrong way, too small) and lists problems in the Console. It also runs after every build.
### Elevation
- **Landforms v2** for WildLens's own terrain: a lowland (where the creek and pond settle) rising to an upland of broad
  ridges, spurs, small valleys and soft shelves; the meadow heart stays gentle. About 30 m of interior relief instead of
  7 m, slopes mostly under 8° (at most ~20°). Prototyped and measured before shipping.
- WildLens's own terrain is regenerated from the seed on every build (setting: *Regenerate WildLens Terrain*), so
  rebuilds are repeatable and terrain improvements arrive. Terrains of your own are never touched.
### Weather
- **A four-day forecast that repeats**, so it can be learned: fair; grey morning clearing by midday; an afternoon
  shower clearing for golden hour; overcast with rain at dusk. Cloud dims and softens the sun and its shadows, greys and
  thickens the haze, and cools the grade; when it's truly grey the sky becomes a scanned **overcast sky** (CC0, Poly
  Haven *Kloofendal Overcast*). **Rain** falls around you with its own sound; the wind picks up.
- **Wildlife shelters in rain**: meadow animals move into woodland, hawks to their perches, and everything rests more
  (placement and behaviour weights only). Herons and the creek birds carry on.
- Field notes when the weather turns; the trailhead board's conditions line shows the weather.
- Builder settings: *Weather* (off: always fair) and *First Weather Day* (for screenshots of a given weather).
### The Creek Ford
- A new landmark where you can watch the creek itself: the calmest open reach of Willow Creek near a path, with a short
  footpath spur, **stepping stones you can cross**, a gravel bar, a dead snag over the water, a log seat facing
  upstream, a sign and a panel ("THE RIFFLE"), and a louder brook.
- **Belted Kingfisher** (fishes from the snag, patrols low along the water) and **Spotted Sandpiper** (teeters on the
  gravel bar) live there, with a second Great Blue Heron. Seventeen species.
### Looks
- Forest floor and ferns only where trees actually stand (the forest field had painted bare leaf litter and ferns on
  open, sunny ground where the tree budget had run out).
- Ovate broadleaf crowns start lower (no bare-pole lollipops); crowns with few limbs are filled on every limb.
- Environment version 15; art import version 5 (**rebuild the slice**).

## [1.3.0] Trailhead
A presentation and readability pass from playtest screenshots. No new systems.
### Signs you can read
- **Trailhead board redesigned** like a national-park trailhead: WILLOW CREEK in big letters, the time of day and
  conditions ("MORNING 09:44 · Fair · deer in the meadows"), a large map with big numbered markers, and the four
  destinations with number, distance and best times as pictograms. The feature list, release notes and statistics are
  gone; the version is one small line in the footer (a stale scene is still flagged there).
- **One sign standard, round two** (`SignKit`): big bold titles (about 10 cm capitals, readable from ~15 m), at most
  one short line, the destination's number disc (matching the board's map), and **pictograms** for best times
  (sunrise, sun, sunset, moon) and for meaning (binoculars, paw, bird, water, tree, bike, walker). Boards are sized
  to their words from the font's real glyph widths, so nothing spills over.
- Every sign rewritten short: destination signs, fingerposts (bigger, numbered, readable from both sides), bike
  parking and scenic pull-offs, Reserve Road, deer crossing, viewpoint signs (now dark boards with cream lettering),
  the Grandmother Oak plaque, the ranger's log, and the reserve guidelines (three pictogram lines).
- **Interpretive panels**: a large pictogram, a bold title on a green band and two short lines instead of a paragraph.
### Looks
- **Real wood**: a scanned CC0 plank texture (Poly Haven *Weathered Brown Planks*) on the kiosk, signs, decks, the blind,
  benches and bridges, with real-scale UVs so grain runs along each piece (posts vertical, boards horizontal).
- **Water that reads as water**: Willow Creek and Cattail Pond are dark and tea-coloured with ripples; the creek's
  ripples **flow downstream** and the pond's drift. Before, a near-mirror surface reflected the sky's blue zenith.
- **Creek banks** in the open grow tall grass and sedge; ferns keep to shade (they carpeted sunny banks before). Cut
  banks and earth paths are brown rather than orange; short grass tufts are narrower and taller.
- **Broadleaf crowns** are rounder and fuller (steeper limbs, round clusters, crown filled between the limb tips, a
  raised top), so distant woods read as temperate broadleaf, not savanna. Tree positions are unchanged.
- **Birch** trunks are grey-white instead of glowing.
- A **trail register** on a post beside the kiosk.
- Environment version 14; art import version 4 (**rebuild the slice**).

## [1.2.0] Habitat Kit
An environment pass that keeps every gameplay system as it is: better rocks now, and a supported route to
high-quality free trees, grass and ground. Store assets can't be committed to this public repository, so they are
imported into your own project and WildLens places them. See `Documentation~/EnvironmentMigration.md` (audit, free
asset list, plan, before/after).
- **Bedrock outcrops**: two scanned CC0 rock faces (Poly Haven *Rock Face 01/02*) break the surface on the steepest
  slopes and on the ridge's flanks, turned to face downhill and set into the ground.
- **Environment Kit** (`WildLens ▸ Environment Kit`, or the builder window): free nature assets in your project sorted
  by ecological role (conifer, broadleaf, creek-side, old-growth, young trees; shrubs, ferns, rocks, boulders, logs,
  stumps; five terrain layers; six grass and flower slots). The builder puts each where it belongs: creek-side trees
  along Willow Creek, giants in the grove, saplings at woodland edges, layers and grass in their painted zones. Kit
  trees get trunk and crown colliders and wildlife data automatically, keep their own LODs and wind, and never mix
  with generated trees.
- **Scan Project for Nature Assets** fills the kit from what you've imported, including the trees, grass and layers
  inside a demo scene's terrain (Unity's free *Terrain URP Demo Scene* and *Terrain Sample Asset Pack* are the
  recommended start). It skips stylised, low-poly and wrong-biome art and anything that would render pink in your
  render pipeline, and says why.
- An empty kit changes nothing: the reserve rebuilds exactly as before.
- **Compile check fixed**: `Tools~/CompileCheck/Check.csproj` had never been committed (ignored by `*.csproj`).
- Environment version 13; art import version 3 (**rebuild the slice**).

## [1.1.0] More Life
Biodiversity pass: the reserve goes from five species to fifteen, all as data on the 1.0 species framework. No new AI.
- **New species** (generic placeholder bodies, data rhythms): Great Blue Heron and Raccoon (now always on), Wild Turkey,
  Barred Owl, Groundhog, Northern Cardinal, Blue Jay, Red-winged Blackbird, Virginia Opossum, Coyote.
- **Where they live** (residents): Cattail Pond gains a heron, red-winged blackbirds and a raccoon; the Old-Growth Grove
  a barred owl and blue jays; the Meadow Blind cardinals and a turkey flock; the Ridge Overlook a lone coyote; the
  creek-side residence an opossum; the meadow-edge residence a groundhog.
- **Small-bird flight**: flight altitude and circle radius are now species data (`flightAltitude`, `circleRadius`), so
  songbirds flit 3-7 m up between perches and the owl glides low, while the hawk still soars at 20-38 m.
- **Field marks** on generic bodies: crests (cardinal, jay), face masks on birds, red epaulettes (blackbird), a coloured
  head (turkey's bare head with wattle, opossum's pale face), owl-sized heads, tails in body colour.
- Pond and grove arrival notes mention their new residents. Environment version 12 (rebuild the slice).

## [1.0.0] Field Season
The first complete release of Willow Creek Nature Reserve: polish, consolidation and a handoff for future contributors.
No new gameplay systems.
### Reserve Road & riding
- Road widened to 4.2 m with smoother, longer curves (relaxed after routing) and **graded**: the road bed follows a
  smoothed ground profile (cut and fill, blended into the land; skipped at creek crossings; backed up, repeatable).
- **Sight lines**: trees stay 7 m and shrubs 3.5 m back from the road edge.
- **Road posts** (white with the reserve's green band) every ~60 m, alternating sides; wider road bridges.
- **Riding comfort**: the bike's height and pitch are damped like soft suspension, so bumps no longer jolt the view.
  Top speed unchanged.
### Destinations
- Planked decks on the jetty, the overlook and the blind floor.
- Signature details: a viewing telescope and a summit cairn on the ridge; the rangers' canoe by the jetty; the roped
  **Grandmother Oak** with its plaque in the grove; a bird feeder in front of the blind's window.
### Trees & vegetation
- Young trees where woodland thins into grassland (softer edges); a **pine** form (tall bare trunk, broad crown) beside
  the firs; two more leaf tints (blue-green, fresh yellow-green) for canopy variety.
### Wildlife expansion framework
- **Generic placeholder bodies** from data (`BodyPlan`: quadruped, wading bird, ground bird, perching bird).
- **Data-driven daily rhythms** (`DailyRhythm` on each species).
- **Water** habitat hotspots at the pond.
- Worked examples behind **Preview expansion species**: Raccoon and Great Blue Heron.
- `Documentation~/WildlifeExpansion.md`: framework, ten planned species, asset research.
### Release readiness
- `Tools~/CompileCheck/`: compile the package in five configurations without Unity.
- `CONTRIBUTING.md`; README rewritten as an index; AGENTS.md reorganised as a handoff (what to build next, what not).

**Rebuild the slice after updating.**

## [0.9.0] Time of Day
### Added
- **A day in the reserve**: dawn, morning, midday, golden hour and dusk, about sixteen real minutes of daylight
  (configurable), with night passing as a short time-lapse. The sun crosses from east to west; the two sky images swap
  and rotate to match it; sun colour, ambient light, fog (thick at dawn), sky tint and URP grading follow hand-tuned
  keys. **Hold T** (standing still) to let time pass. The HUD shows the period and time; each change of light brings a
  short field note about what the reserve is doing.
- **Dawn mist** over the pond and the creek's low reaches, burning off by mid-morning; a faint haze at dusk.
- **Wildlife rhythms by placement**: when the period changes, animals' home ranges move to where their species is
  at that time, and they travel there themselves. Deer: meadows morning and golden hour, woodland cover at midday,
  water at dusk. Cottontails: meadow edges at dawn and dusk. Foxes: hunting meadow edges at dawn and dusk. Hawks:
  over the ridge in daylight, on meadow snags at the ends of the day. Squirrels busiest morning and late afternoon.
  Behaviour weights shift too (deer bed down at midday).
- **Trail hierarchy**: the **Reserve Road**, a 3.4 m gravel ring from the trailhead through every destination's
  pull-off, gently graded and kept outside the Meadow Loop; destination paths become narrow **footpaths**. The bike
  rides at its usual pace on the road, gently on footpaths (≈22 km/h) and across country (≈20 km/h). Road bridges
  are wider. The HUD says what you're riding on.
- **Reserve identity**: it is now **Willow Creek Nature Reserve** (Willow Creek Conservation Trust, est. 1962). One
  sign standard everywhere (stained boards, cream lettering, green rule), reserve guidelines at the trailhead, a
  Reserve Road sign, and best times on every destination sign.
- Trailhead board: a live **"Now"** line (period, time, what the wildlife is doing), each destination's **best
  times**, the road on the map, and a taller board to fit them.
### Changed
- **Trees rebuilt with structure**: conifers grow branch whorls up a tapering trunk (ragged, cone-shaped crowns,
  a slim dark core, dead lower branches); broadleaf trees come in three forms: spreading (short trunk, 3–5 limbs,
  wide low crown), ovate (taller, steeper limbs), and multi-stem birch (pale stems, light crowns). Limbs show under
  and between the leaf clusters.
- The Old-Growth Grove is twice as dense, for real shade.
- The Ridge Overlook prefers a view toward the sunset.

**Rebuild the slice after updating.**

## [0.8.0] Ecosystem Pass: a living landscape
### Added
- **Willow Creek**, the reserve's backbone: a stream from the ridge's flank to Cattail Pond, routed downhill and
  carved into the terrain (repeatably, backed up), with flowing water, footbridges wherever the loop or a track
  crosses it, stones in the bed and rocks in the cut banks, a fallen log, reeds, ferns crowding the banks,
  riparian woodland, brook sounds and creek-side deer. Drawn on the trailhead map.
- **Bike pull-offs**: gravel parking short of each destination (out of the residents' earshot) and scenic pull-offs
  with a bench on longer tracks. Riding into one, the HUD suggests parking and says how far the walk is.
- **Environmental storytelling**: interpretive panels (ridge, pond, grove, meadow, creek), numbered track marker
  posts, a deer-crossing sign, nest boxes in the grove, a ranger's log in the blind.
- **Encounter variety by placement**: deer that drink at the creek, deer whose range straddles a track, cottontails
  on the meadow's brushy edge, two hawks on the ridge, more squirrels and a fox in the grove.
- Ridge wind ambience; purple asters in the meadows; a realtime reflection probe so the pond reflects its banks.
### Changed
- **Meadow Blind rebuilt**: roomier (4.2 × 3.2 m), levelled pad and plank floor, centred 1.2 m doorway reached by
  duckboards, a long window usable standing *and* crouched, brush screening, grass kept out. It sits inside the
  meadow edge with the wildlife hotspot 14 m in front, so animals come close. Inside, the HUD shows **HIDDEN** and
  explains the blind on first entry.
- **Habitat density**: trees now form dense woodland, clumped stands, quiet transition groves and open grassland
  with the odd lone tree, instead of an even scatter. Ferns concentrate under dense canopy and along the creek.
- **Destination identity**: the pond has an irregular shoreline with coves, thick reeds and lily pads; the grove has
  a closed canopy of big trees, mossy rocks, fallen timber and ferns; the ridge is a rocky, windy crest.
- Terrain grass leans with slopes and scatters less evenly on Unity 2022.2+ / 6.
- The trail bike rides over bridge decks.

**Rebuild the slice after updating.**

## [0.7.1] Fixes from the first reserve playtest
### Fixed
- **Couldn't get into the Meadow Blind:** its doorway was 0.7 m, narrower than the player's collision capsule.
  Now 1.1 m.
- The "R Ride the trail bike" prompt sat on top of the viewfinder's zoom scale; it now hides while the camera is up.
- The bike's headlight blocked the middle of the riding view; it is smaller and lower.

**Rebuild the slice after updating** (the blind is built by the scene builder).

## [0.7.0] The Reserve
### Added
- **Reserve destinations** beyond the Meadow Loop, chosen from the terrain: Ridge Overlook (deck, perch snag;
  hawks), Cattail Pond (water, reeds, jetty; deer), Old-Growth Grove (giant trees, clearing; squirrels and a fox),
  Wildflower Meadow Blind (a wooden hide that conceals you; deer and rabbits). Each has resident wildlife, a sign,
  a viewpoint and an arrival field note.
- **Reserve tracks** routed around steep ground from the loop to each destination, with fingerposts; scenery
  extends along them. The pond basin and overlook hill are shaped when the land doesn't provide them (backed up,
  repeatable).
- **Trail bike** at the trailhead: R to ride and to park; gentle handling, fastest on tracks; stopped in the saddle
  you can still raise the camera. Its engine is audible to wildlife from far off.
- **Trailhead kiosk** with a live information board: version, release, features, recent changes, reserve status
  (species, journal progress, trails, viewpoints, destinations with distances) and a heads-up topographic map.
- `Documentation~/ReserveExpansion.md`: the plan for growing the reserve.
### Changed
- The WildLens fallback terrain grows to 900 m (your own terrain is never resized).
- Procedural trees stop drawing when tiny on screen (performance for the larger world).
### Fixed
- The black slab overhead near some trees was the solid underside of a conifer's lowest tier: tiers are now open
  underneath and the lowest one clears head height.

**Rebuild the slice after updating.**

## [0.6.0] Presentation pass
### Added
- **Leaf-card trees:** broadleaf canopies and conifer tiers are now built from alpha-cut leaf and needle-branch cards
  (CC0, composed from Poly Haven tree atlases) over darker core volumes, with wind sway. Replaces smooth blobs/cones.
- **Stale-scene detection:** scenes built by an older package show a notice on Play, a warning in the builder window
  and a line in the F3 overlay.
- **Animal Model Setup** (WildLens ▸ Animal Model Setup): one-click Animator Controller and prefab for an animated
  animal model, assigned to the species.
- `AGENTS.md`: project vision, design philosophy, architecture and history for anyone (human or AI) working on it.
### Changed
- Terrain reads as grass instead of bare soil at distance: green and golden-olive grass tints in ~20 m patches at
  unrelated tile sizes (less visible tiling); terrain grass draws to 150 m.
- Deer placeholder silhouette (chest, haunch, forward neck, short broad ears, dark shins); lying deer tuck their legs.
- Camera mode hides the control hints (they overlapped the viewfinder); stronger focus-lock snap; photo prints have
  a drop shadow and a slight tilt.
### Removed
- Forest Ground 01 terrain texture (no longer used; −4.4 MB).

**Rebuild the slice after updating.**

## [0.5.0] Zoom you can see · Art direction
### Fixed
- **Zoom readouts changed but the view did not.** Two causes: (1) in scenes upgraded at runtime, ZoomLens cached a
  null CameraModeController in Awake (added before it), so it never applied camera-mode FOVs; (2) scrolling while the
  camera was lowered only "preselected" a level by design, changing the UI but not the view. Zoom input now raises
  the camera, references resolve lazily, and the FOV is applied late and re-asserted right before rendering, with
  physical-camera, custom-projection, orthographic and Cinemachine overrides detected and corrected.
### Added
- Render-time camera probe in the F3 overlay; F4 zoom self-test (measures rendered projection and subject size at
  every level, saves screenshots, PASS/FAIL).
- Scanned CC0 art from Poly Haven shipped in `Art~`: two HDRI skies with sun alignment, five terrain materials,
  bark and rock surfaces, ferns, shrubs, mossy rocks, boulder, stump, fallen trunk.
- Sky moods (late morning / golden hour), HDRI ambient and reflections, fog matched to the sky's horizon.
- Camera presentation: autofocus hunting and lock, focus-confirm dot, focal/aperture line, "keeper" prints and chime
  for Excellent photos, photos develop in.
- Compositions: Forest Glade clearing and Trail Bend sight line.

## [0.4.0] Visual quality & camera
### Fixed
- **Zoom did nothing with Input System 1.9+ (Unity 6).** Scroll was divided by 120, but the Input System now reports
  ±1 per notch by default. Zoom now uses wheel direction only, so every backend and setting works. Added E/Q keys.
### Added
- F3 camera diagnostics overlay (scroll values, zoom inputs and why any were ignored, FOV, competing cameras/FOV writers).
- Lens feel: depth of field with focus pulls (URP), focus-lock beep, big zoom readout per step, breathing, pan
  inertia, raise roll.
- Painted terrain (5 generated PBR-style layers), terrain grass/flowers/ferns with wind, foliage wind sway.
- Scenic viewpoints, rock outcrops, stumps, richer tree silhouettes.
- Lighting/sky/haze rework, shadow quality, URP colour grading.
- Comparison-shot capture tool; `Documentation~/VisualRoadmap.md` (asset sources and licensing).

## [0.3.0] Encounters
### Added
- Crouch (C toggle; Left Ctrl hold): half speed, half footfall noise, smaller silhouette, lower head hidden by
  cover, smooth camera transition, headroom check.
- Observation identification: hold an animal in the viewfinder to identify it and add it to the journal without
  a photo; chime and notice.
- Framing assistant: viewfinder brackets track the subject nearest centre and show the grade a photo would get.
- 16x zoom step (about 410 mm equivalent), focal-length readout, zoom preselect while the camera is lowered,
  vignette that deepens with zoom.
- Habitat hotspots (meadows, brush piles, mature trees, dead-snag perches) with visible landscape cues; species
  favour them; derived automatically in scenes built without them.
- "Why nothing was identified" feedback on photos.

### Changed
- Photo grading uses linear frame fill with clear thresholds instead of area against per-species ideals; thirds
  composition can be Excellent. Much higher success rate at every zoom level.
- Awareness tuned for "approachable realism": standing still always lets animals settle; shorter flee runs and a
  longer look back; softer post-flee memory; per-species tuning (see Documentation~/Systems.md).
- Species assets upgrade automatically (data version 3); journal save version 3.

## [0.2.0] Camera & Wildlife Awareness
### Added
- Camera Mode (hold RMB): reduced movement and look speed, narrower FOV, viewfinder overlay, raise transition with
  sound and a small camera dip, subtle handheld sway. Photos can only be taken in camera mode.
- Zoom lens: 1× / 2× / 4× / 8× steps on the mouse wheel with smooth transitions and a level indicator.
- Photo feedback: synthesised shutter sound, shutter blackout effect, preview card with species and quality,
  green focus confirmation in the viewfinder.
- Wildlife awareness: Calm → Aware → Alert → Fleeing driven by distance, player noise, line of sight and memory,
  with per-species radii and patience. The shutter is audible to nearby animals.
- Behaviour states: rabbit Sitting/Hopping, deer Walking/Running, hawk Flying/Landing/Perched/TakingOff.
- Photo quality classes Poor / Good / Excellent with a one-line hint.
- `Documentation~/Systems.md`.

### Changed
- Numeric photo scores removed; the journal shows the best photo's quality class.
- Existing species assets and journal saves are upgraded automatically.

## [0.1.0] Vertical slice
- Trail loop builder, five species, photography, field journal.
