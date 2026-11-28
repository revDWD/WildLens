# Wildlife enhancement campaign (1.7 plan, filled in from the audit in 1.8, status as of 1.9)

The plan for filling Willow Creek with life from the animal packs imported into the Unity project. Since 1.8 it is
filled in from the director's real Asset Audit. It covers the audit, which species to keep, replace and add, the order
to do it in, small life for the macro lens, the bike, and where everything belongs in the reserve. Rules it rests on: [WildlifeAssetStrategy.md](WildlifeAssetStrategy.md)
(style), [WildlifeExpansion.md](WildlifeExpansion.md) (a species needs a place), [Roadmap.md](Roadmap.md) (what never
to build).

**Status, 1.9.** The 1.8 playtest was a success ("the reserve immediately feels more alive"): the pack pipeline works in Unity.
Twenty-seven species now; sixteen of the older twenty-four have pack or shipped models, three new birds stand in on tinted Living
Birds models, and **eight are still placeholder bodies**. The fox (a tinted dog) and the turkey (a tinted chicken) read as
what they are; 1.9 re-tints them (factory version 3), and the **upgrade rule** swaps in a truer model by itself when one is
imported. The build machine cannot reach any CC0 model host, so **the director fetches models** from the shopping list in
section 3. Natural events (weather habits) are in [Systems.md](Systems.md) 1.9.0; small life no longer needs packs (section 4).

**Art direction (director, 1.7):** recognisable wildlife → biodiversity → animation quality → environmental
storytelling → realism. A good stylised animal beats a placeholder. The environment stays photographic.

## 1. The audit: what is known, and what is not

Store packs live only in the Unity project (they may never be committed), so the work on 1.7 could not see them. 1.7
shipped the probe that finds out, and **the director has now run it and pasted the result** (the first report from
inside Unity). What it found is below. The probe is still the way to check anything new:

> **WildLens ▸ Asset Audit Report.** One click. It lists every imported model by pack: what animal it is, which
> reserve species it can be, its triangle count, size, clips and which behaviours those clips cover, shader problems,
> plus every butterfly, frog, mushroom, flower and vehicle it finds. It saves `WildLensAssetAudit.md` in the project
> folder and copies it to the clipboard. 1.8 adds an **Other static models** list, so a pack of unfamiliar models can
> be read by name. Run it again after any new import and paste it into the chat.

The master table the report produces has these columns, and is judged like this:

| Column | Source | How to judge it |
|---|---|---|
| Species / represents | name matching (exact, stand-in, or "new") | Would you name the animal from its outline at 16×? |
| Asset pack | folder | One model per species; packs may be mixed between species |
| Model quality | triangles, size, shader check, then a screenshot | Silhouette first. Pink = a built-in shader in URP: since 1.8 the factory makes converted copies, so pink now means that conversion failed |
| Animation quality | clip list and recognised roles | Ground animals want Idle, Walk, Run, Eat, Look. Birds want Idle, Fly, and ideally Glide, TakeOff, Land. Idle + Walk (or Idle + Fly) is the minimum worth using |
| Reserve habitat | section 6 | Where it would really live |
| Ready for integration | exact or stand-in match + minimum clips + shaders fine | Then it sets itself up on import or rebuild |
| Needs work | missing roles, wrong pipeline, odd scale, faces backwards | Size and yaw are one field each in **Wildlife Pack Setup** |

### What the director's packs contain (from the 1.7 audit)

| Pack | Contents | Verdict |
|---|---|---|
| **ithappy "Animals FREE"** | deer, chicken, dog, horse, tiger, kitty, penguin; idle, walk and run clips only | **Use:** deer (the real thing), chicken (stand-in for the turkey), dog (tinted stand-in for the red fox). **Leave out:** horse, tiger, kitty, penguin (not Willow Creek animals) |
| **Dinopunch "Living Birds"** | seven birds: blue jay, cardinal, chickadee, crow, goldfinch, robin, sparrow; 16 clips; built-in shaders; ships its own scripts | **Use all seven.** Needed 1.8's URP material copies and script stripping. One songbird (the sparrow) also stands in, tinted, for the blackbird, kingfisher and phoebe |
| **Blink stylised bear** | one bear; 18 clips; built-in shader | **Use** as the black bear (tinted black) |
| **SinuousShrooms** | 11 static mushrooms | **Use** for the mushroom subject (up to six variants); the only small life the packs gave |
| **A motorcycle pack** | 18 static models | **Not yet judged.** The 1.8 audit lists the models by name (section 5) |
| **BrokenVector tree packs** | low-poly trees | **Keep out** of the photographic reserve |
| **Darth_Artisan** (trees) | tree models | Scenery, not wildlife; judged with the Environment Kit ([EnvironmentMigration.md](EnvironmentMigration.md)) |
| **Unity Terrain Sample Assets** | trees, grass, rocks, ground | Scenery: the Environment Kit's source |

What the audit proved in Unity: the automatic setup ran, and the deer (ithappy), the turkey (chicken stand-in), the coyote
(the shipped wolf) and the hawk (the shipped eagle) were in use. What it exposed, and 1.8 fixes: built-in-shader models
would be pink in URP; the cardinal and blue jay were not set up after a pack with scripts was imported; the wrong clips were
chosen (the bear's jump as its walk, fly-left as fly, pecking as idle); pack scripts were left on the models; bird sizes
were 2 to 5 times too big; pack clips did not loop; and the journal showed only about eight species rows. **1.8's fixes
have not run in Unity.**

No butterflies, frogs, newts, snails or insects were found in any pack.

### The 27 species and the model each uses today

Exact = the real animal; stand-in = a look-alike; tinted = recoloured to the species. The pack-name column says what the
automatic setup looks for (exact names; then stand-ins, closest first).

| Species | Home | Model today | A pack model called… sets it up automatically |
|---|---|---|---|
| White-tailed deer | meadows, pond at dusk | **ithappy deer** (exact) | whitetail, deer, doe, stag, buck, fawn; else roe, elk, reindeer, caribou, moose, antelope, gazelle, impala |
| Eastern cottontail | brush, meadow edge | **placeholder: no model in the packs** | cottontail, rabbit, bunny, hare |
| Red fox | meadow edge, grove | **ithappy dog**, tinted (stand-in) | fox; else shiba, fennec, jackal, dog |
| Coyote | open grass, ridge | **shipped CC0 wolf** (stand-in) | coyote; else wolf, dingo, dhole, jackal, husky, shepherd, dog |
| Eastern gray squirrel | mature trees, falls | **placeholder: no model in the packs** | squirrel, squirel; else chipmunk |
| Raccoon | creek, pond at dusk | **placeholder: no model in the packs** | raccoon, racoon; else badger, red panda, coati |
| Virginia opossum | creek at night | **placeholder: no model in the packs** | opossum, possum; else rat, mouse |
| Groundhog | meadow edge | **placeholder: no model in the packs** | groundhog, woodchuck, marmot; else beaver, gopher, prairie dog, capybara, hamster, guinea pig |
| Wild turkey | blind meadow | **ithappy chicken**, tinted (stand-in) | turkey; else pheasant, grouse, partridge, quail, guinea fowl, chicken, rooster, hen, peacock |
| Red-tailed hawk | ridge | **shipped CC0 eagle** (stand-in) | hawk, buteo; else eagle, falcon, buzzard, harrier, osprey, kite, vulture, raptor |
| Barred owl | Old-Growth Grove | **placeholder: no model in the packs** | owl |
| Great blue heron | pond, ford | **placeholder: no model in the packs** | heron; else egret, bittern, ibis, spoonbill, pelican, crane, stork, flamingo |
| Spotted sandpiper | ford gravel bar | **placeholder: no model in the packs** | sandpiper; else killdeer, plover, dunlin, snipe, shorebird, wader, tern, seagull, gull |
| Northern cardinal | blind brush | **Living Birds cardinal** (exact) | cardinal, redbird; else songbird, finch, sparrow, robin, oriole, canary, chickadee, wren, tit, thrush, warbler, swallow, bird |
| Blue jay | grove | **Living Birds blue jay** (exact) | blue jay, jay; else songbird, finch, sparrow, robin, chickadee, tit, wren, thrush, warbler, swallow, bird |
| Red-winged blackbird | pond reeds | **Living Birds sparrow**, tinted (stand-in) | blackbird; else grackle, starling, sparrow, songbird, crow, raven, thrush, bird |
| Belted kingfisher | Creek Ford | **Living Birds sparrow**, tinted (stand-in) | kingfisher; else songbird, sparrow, bird |
| Eastern phoebe | Willow Falls | **Living Birds sparrow**, tinted (stand-in) | phoebe, flycatcher; else sparrow, songbird, finch, wren, chickadee, bird |
| **American crow** (1.8) | trailhead, a road pull-off | **Living Birds crow** (exact) | crow, raven |
| **American robin** (1.8) | trailhead grass, loop meadow edge | **Living Birds robin** (exact) | robin |
| **Black-capped chickadee** (1.8) | Fern Glade, Old-Growth Grove | **Living Birds chickadee** (exact) | chickadee, tit |
| **American goldfinch** (1.8) | the blind's meadow | **Living Birds goldfinch** (exact) | goldfinch, finch, canary |
| **Song sparrow** (1.8) | pond brush, Creek Alcove, loop brush piles | **Living Birds sparrow** (exact) | sparrow; else wren |
| **American black bear** (1.8) | deep woods | **Blink stylised bear**, tinted black (exact, tinted anyway) | blackbear, bear (a model named black or dark first; never polar, panda, teddy or koala) |
| **Eastern bluebird** (1.9) | Old Cabin Ruins' fence | **Living Birds sparrow**, tinted (stand-in) | bluebird; else sparrow, finch, songbird, bird |
| **Tufted titmouse** (1.9) | the Rock Shelter's woods | **Living Birds chickadee**, tinted (stand-in) | titmouse; else chickadee, tit, sparrow, songbird, bird |
| **Louisiana waterthrush** (1.9) | Hemlock Run | **Living Birds sparrow**, tinted (stand-in) | waterthrush, thrush; else sparrow, wren, songbird, bird |

Nineteen species have a model (sixteen pack or shipped, three tinted stand-ins from Living Birds added in 1.9); **eight are
still placeholder bodies**. The fox and the turkey are the two most worth a truer model. **Weather habits** (1.9) are
on every one that has them, whatever its model (table in [Systems.md](Systems.md) 1.9.0); the table's last column is unchanged. Words of four letters or fewer ("fox", "owl", "jay",
"tit") must be a whole word in the model's name, so "Foxglove" is not a fox; longer ones may sit inside a word.
**Reserved:** woodpecker, hummingbird, duck, mallard, goose and swan are held for future species, so a model with one of
those words in its name is **never** used as a stand-in, not even through a folder called "Birds". The tables with the
ranking rules are in [Systems.md](Systems.md) (1.7.0 and 1.8.0).

## 2. Species list

### Existing: all eighteen kept (and nine more since)
Every one has a place and hours. None should be cut. Their bodies change; their ecology does not. With models today:
deer, red fox (dog, tinted), coyote (wolf), turkey (chicken), hawk (eagle), cardinal, blue jay, blackbird, kingfisher and
phoebe (the last three the tinted sparrow). Without: the eight below.

### Replacement
**Done:** every species a pack could fill (the sixteen above, with the six new ones).
**Still needed, with no model in the packs:** eastern cottontail, eastern gray squirrel, great blue heron, raccoon, barred
owl, groundhog, Virginia opossum, spotted sandpiper. Their placeholder bodies stay until a model turns up. Rules for
choosing when a new audit shows several candidates:
- The real animal beats a stand-in; among stand-ins, the closer silhouette wins (wolf over dog, egret over flamingo).
- Then the one with more recognised clips. **An animated stylised model beats a realistic statue.**
- One model per species. One songbird model, tinted, may be several species: that is intended.
- Keep the shipped wolf and eagle only until a real coyote or hawk turns up.

### New: the six added in 1.8, each for a place
| Species | Where | Why it earns its place |
|---|---|---|
| **American crow** | the trailhead between the Reserve Board and the bike bay; one road pull-off | The trailhead and road had no wildlife of their own. A crow near the Reserve Board is life in the first ten seconds |
| **American robin** | trailhead grass; the Meadow Loop's edge | Common, close, an easy first photograph |
| **Black-capped chickadee** | Fern Glade (hidden); Old-Growth Grove | A hidden place with something living in it; the **tamest animal in the reserve**: stand still and it comes close |
| **American goldfinch** | the Wildflower Meadow Blind's meadow | Yellow in the wildflowers in front of the hide |
| **Song sparrow** | Cattail Pond's brush; Creek Alcove; loop brush piles | A small, low, wary subject at the water's edge |
| **American black bear** | one, in the deep woods; woodland edge at dawn, the creek at dusk | **Director's decision (1.8).** One animal, the widest awareness radius, always moves away: no chase, no failure. A reason to scan the woodland edge at dawn |

### Waiting for a model
Each fits the existing ground or flying brain: no new systems. Add one only when an audit shows a usable model and its
place exists.

| Candidate | Pack names that would serve | Where and when | Why it earns a place |
|---|---|---|---|
| **Eastern chipmunk** | chipmunk (today a squirrel stand-in) | Fern Glade and Hidden Overlook rocks; morning | Both hidden places have hotspots; the Fern Glade now has chickadees, the Hidden Overlook still has no resident |
| **Canada goose** | goose (reserved: never a stand-in) | pond bank and pond meadow, grazing; dawn to dusk | A flock on the grass by the water; needs no swimming |
| **Mallard / wood duck** | duck, mallard (reserved) | pond bank and ford gravel, loafing | Pond variety. On land only, until a marsh makes swimming worth building |
| **Painted turtle** | turtle, tortoise | a basking log at the pond; midday, fair weather only | Fills the pond's quiet midday; slides off (flees) if rushed |
| **Pileated woodpecker** | woodpecker (reserved) | Old-Growth Grove snags; morning | The grove's missing voice; needs big dead trees, which the grove has |
| **Turkey vulture** | vulture, condor (today a vulture is a hawk stand-in) | soaring above the ridge at midday | Shares the hawk's brain; gives the telescope something to find |
| **Striped skunk** | skunk | meadow edge at dusk | A second dusk animal on the loop; slow and unbothered |
| **Mourning dove** | dove, pigeon | road verges, blind feeder | Common, close, easy |
| **Little brown bat** | bat | over the pond at dusk and night | A dusk silhouette against the sky; wait for the night sky work |

Not for Willow Creek, whatever the packs contain: farm animals, pets as themselves, and anything from another
continent (lions, tigers, zebras, penguins; the director's horse, tiger, kitty and penguin are left unused). They may
still serve as *stand-ins* when the outline fits (a dog tinted as a fox, a husky as a coyote).

## 3. Integration plan, in order

1. **Playtest 1.9** (1.8's fixes were proven in the 1.8 playtest: it worked). Update, rebuild (the first rebuild re-tints the
   fox and turkey and sets up the three new birds), read the Console lines (`Wildlife models`), and photograph the fox and
   turkey at 4× after re-tinting, and the new birds. Watch the habits on forecast day 3 through the shower and day 4's dusk.
   **Send screenshots** (the full list is in the 1.9.0 changelog entry).
2. **Fix sizes and facing from the screenshots.** Size and yaw are one field each in **Wildlife Pack Setup**; a wrong
   model choice is a pick in the same row. Check walk, graze, alert and fly all animate and loop.
3. **Run the Asset Audit again and paste it.**
4. **Find free models, the fox and the turkey first, then the eight placeholders** (cottontail, squirrel, heron, raccoon, owl,
   groundhog, opossum, sandpiper), most photographed first. **The build machine cannot open any of the sites below** (1.9:
   quaternius.com, poly.pizza, opengameart.org, itch.io and kenney.nl are all blocked from it), so this is the director's
   job. The upgrade rule means an imported `Fox.fbx` or `Turkey.fbx` takes over from the dog and the chicken on the next
   Rebuild; a model chosen by hand in a setup window is never replaced.
   **Shopping list** (from search snippets only; **check each page for "CC0" before use**): a fox with idle, walk and run on
   OpenGameArt, `https://opengameart.org/node/90146`; Quaternius packs at `https://quaternius.com` and
   `https://quaternius.itch.io` (their farm-animal pack has cow, horse, llama, pig and pug, **no fox**). **Not usable:** the
   glTF sample "Fox" (its rig and animation are CC-BY) and Sketchfab personal-use foxes. Steps: download, unzip, drag the
   `.fbx` into the project's `Assets` with a plain name (`Fox.fbx`, `Turkey.fbx`, `Rabbit.fbx`, `Owl.fbx`...). Good places to look, **checking each listing for its licence and contents
   before importing** (do not assume what a pack holds): the Quaternius CC0 animal packs (itch.io and quaternius.com), the
   Asset Store's free animal packs, and Poly Pizza filtered to CC0. Fox, rabbit and squirrel are the likeliest to turn up
   in a stylised forest-animals pack. Import, let the automatic setup run, and read the Console.
5. **Small life models** (section 4): the packs gave mushrooms only. Look for butterflies, bees, dragonflies, frogs and
   snails the same way.
6. **The motorcycle pack** (section 5): read the audit's list, judge by the criteria.
7. **New species, one at a time, from the waiting list above**, each only when its model and place exist.
8. **Awareness retune.** New bodies change what "visible" means; walk up to each species once and confirm that
   standing still always lets it settle (the bear and the chickadee most of all).

Screenshots are the test at every step. An animal that looks wrong at 16× is not ready, whatever its clip list says.

## 4. Macro wildlife opportunities

1.7 turned the macro groundwork into a pipeline: a small-life model found in your packs is given to a field-notebook
subject and placed in its habitat on the next rebuild (models over 3 m are ignored). Behaviour is deliberately simple and
patience always works.

**Since 1.9 small life no longer needs packs.** Twenty-six stylised models are **generated in code** (and saved in the user's
project), so all 29 subjects appear with nothing imported; a pack model, if one is found, always wins; the builder setting
`generatedSmallLife` turns the generated ones off. They are unseen in the engine. Subjects, hours and conditions are in
[Systems.md](Systems.md) 1.9.0. **What the director's packs gave (1.8):** the mushrooms only; no butterflies, frogs, insects, newts
or snails. The "no model in the packs yet" notes below describe packs, not the reserve.

### Animals
| Subject | Pack names | Where | When | Behaviour |
|---|---|---|---|---|
| Monarch Butterfly | monarch, butterfly | on the loop's dandelions | morning to golden hour, not in rain | settles on a flower, flutters to the next |
| Common Eastern Bumble Bee | bumblebee, bee; else wasp | on flowers | daytime, not in rain | works a flower, moves on |
| Common Whitetail (dragonfly) | dragonfly, damselfly | pond, ford, creek banks | daytime, not in rain | holds a perch, darts out, returns to it |
| Northern Leopard Frog | frog; else toad | water's edge | any hour, rain or shine | sits; one hop away if you rush it |
| Carolina Mantis | mantis; else grasshopper, cricket, stick insect | loop verges | morning to dusk | still |
| Spotted Lady Beetle | ladybug, ladybird, lady beetle; else beetle, bug | loop verges | daytime | still |
| Luna Moth | luna, moth | on logs in the woods | dusk, night, dawn | still |
| Eastern Newt (red eft) | newt, salamander, eft; else lizard, gecko | grove and glade floor | after rain | still |
| Whitelip Snail | snail; else slug | on logs and stumps | after rain | still |
| Common Eastern Firefly | firefly, lightning bug | loop verges | dusk and night | glows and drifts |

(Every animal row above: **no model in the packs yet**.)

**Added in 1.9** (all generated): eastern tiger swallowtail and cabbage white (butterflies on flowers in sun), ebony jewelwing
(damselfly, creek only), western honey bee, green frog, gray treefrog (trunks, wet evenings), eastern red-backed salamander
(after rain), whose conditions are in Systems.md. Monarchs sit on milkweed first; one insect per flower.

### Plants
| Subject | Source | Where |
|---|---|---|
| Common dandelion | shipped (CC0 scan) | loop verges |
| Lesser periwinkle | shipped (CC0 scan) | Fern Glade, grove |
| Woodland fern (1.9) | shipped fern scan, graded on its crown | shady woodland floor |
| Common milkweed, black-eyed Susan, common blue violet, haircap moss (on boulders), common cattail (ponds only) (1.9) | generated | meadow verges, woods, boulders, pond edge |
| Oxeye Daisy | pack: daisy, oxeye; else flower, wildflower (not a pot, vase, bouquet, tree, bush or bed). **No model in the packs yet** | loop verges |
| *Next:* trillium in the grove, goldenrod at the blind meadow | pack or CC0 scan | as named |

### Fungi
| Subject | Source | Where |
|---|---|---|
| Yellow fly agaric, turkey tail (1.9) | generated | woodland floor under old trees; logs and stumps |
| Honey Mushroom | **found: SinuousShrooms** (11 static mushrooms; up to six variants used, sizes varied, at most two on one log or stump; the rest on the woodland floor) | on logs and stumps in the woods |
| *Next:* bracket fungus on the Grandmother Oak, a fairy ring in the Fern Glade after rain | pack or CC0 scan (Poly Haven) | as named |

**The pairings are the point:** a monarch *on* a dandelion, a dragonfly *at* the ford where the kingfisher fishes, a
newt in the glade only after the day-3 shower. Each gives a reason to be in a place at a time, the same way the
heron does. No counts, no completion: the field notebook just remembers what you found.

## 5. Motorcycle recommendations

**The director's motorcycle pack (18 static models) is imported but not yet judged.** The 1.8 audit lists those models by
name (an **Other static models** list, plus the vehicle table with up to 12 child part names each). Paste it and the
candidates are judged by the criteria below, which are unchanged.

The audit lists every vehicle model it finds, with its length and whether it has separate wheels and a steering
part. Judge a candidate against the bike we are heading towards, **a Honda Trail 125 / CT125: a small step-through
dual-sport**:

- **Take it** if it is small (under about 2.0 m), upright, has a rear rack or room for one, spoked or plain wheels of
  about 0.3 m radius, a high exhaust or skid plate, and **separate wheel and fork objects** (needed for steering,
  spin and suspension).
- **Leave it** if it is a sport bike, a chopper, a motocross racer with number plates, or one fused mesh. A static,
  fused model would lose the suspension and steering the generated bike already has.
- **A utility vehicle (ATV, UTV, pickup)** is not a replacement for the bike. It is scenery: parked at the trailhead
  or a future ranger cabin it says "someone looks after this place". Never drivable.

If one passes: put it under the bike's **Model** child and name its parts `Lean`, `Steer`, `Front Axle`, `Front
Wheel`, `Swingarm`, `Rear Wheel`, `Kickstand`, `Seat Anchor` ([TrailBike.md](TrailBike.md); a missing `Seat Anchor` is made for you). Speed, sound and
handling do not change. If none passes, the generated bike stays and its planned looks work (shaped tank and
fender, real wheels, a dusk headlight) remains the route.

## 6. Reserve ecology: where the life belongs

"Has today" is as of 1.8 (the three 1.9 birds and the new hidden places are added in the last rows). **Since 1.9 the table's
animals also answer the weather** (deer under the trees in rain; songbirds bathing after it; the owl on a snag at a dry dusk; crows
roosting at the trailhead at dusk): Systems.md 1.9.0. Species with no model yet are placeholder bodies, but they are there.

| Place | Has today | Add | The picture it makes |
|---|---|---|---|
| **Trailhead and bike bay** | crows and robins (1.8) | a dove | Life before you have started the bike |
| **Meadow Loop** | deer, cottontail, fox, hawk, squirrel, groundhog, robins and song sparrows (1.8) | monarch and bee on dandelions, mantis and lady beetle on the verge, fireflies at dusk; skunk at dusk | Look far, then look down |
| **Reserve Road and pull-offs** | deer crossing, turkeys, a crow group at one pull-off (1.8) | doves on the verge | Reasons to slow down |
| **Ridge Overlook** | hawks, coyote | vulture soaring at midday | Something for the telescope |
| **Cattail Pond** | deer, heron, blackbird, raccoon, song sparrows in the brush (1.8) | dragonflies and frogs at the edge; geese on the bank; a turtle on a log | The busiest place in the reserve, at every hour |
| **Old-Growth Grove** | squirrel, fox, owl, jay, chickadees (1.8) | mushrooms, snails and a luna moth on the logs (mushrooms from SinuousShrooms if a rebuild finds them); newts after rain; woodpecker | The grove rewards a slow walk as well as a long lens |
| **Wildflower Meadow Blind** | deer, cottontail, cardinal, turkey, goldfinches (1.8) | daisies with bees and butterflies in front of the blind | Macro subjects within reach of the bench |
| **Willow Creek and the Ford** | kingfisher, sandpiper, heron, opossum | dragonflies on the gravel bar, frogs on the bank | Sit on the log seat; everything comes to the water |
| **Willow Falls** | phoebe, squirrel | frogs at the plunge pool; newts after the spate | Wet rock, small life |
| **Fern Glade (hidden)** | periwinkle, chickadees (1.8) | chipmunk; newts, snails, mushrooms | A hidden place with something living in it |
| **Hidden Overlook** | a perch only | chipmunk on the rocks | A companion on the log seat |
| **Creek Alcove (hidden)** | raccoon, song sparrow (1.8) | frogs | A quiet bend with a small voice |
| **Hemlock Ravine** (1.9, where the land allows) | waterthrush at the run, phoebe on the cascade's ledge | frogs and salamanders after rain (generated small life near water and on the wet floor) | A cascade in shade |
| **Cascade Overlook (hidden, 1.9)** | the ravine's life below | | The cascade from above |
| **Hidden Pond (hidden, 1.9)** | heron, blackbird, raccoon | frogs, dragonflies | Still water far from anyone |
| **Rock Shelter (hidden, 1.9)** | titmouse, gray squirrel (a placeholder body) | moss on boulders | A dry hollow with a long view |
| **Old Cabin Ruins (hidden, 1.9)** | bluebird on the fence, cottontail, groundhog (placeholder bodies) | milkweed and black-eyed Susan in the clearing | A homestead taken back by the woods |
| **Deep woods** (no sign, no path) | one black bear (1.8): woodland edge at dawn, the creek at dusk | nothing: it is meant to be rare | Something found by patience, never promised |

Principles for all of it: residents make a place *about* its animal; commuters make the clock matter; small life
makes the weather matter. Nothing spawns on cue, nothing is counted, and every animal still settles if you stand
still.
