# Wildlife enhancement campaign (1.7)

The plan for filling Willow Creek with life from the animal packs imported into the Unity project. It covers the
audit, which species to keep, replace and add, the order to do it in, small life for the macro lens, the bike, and
where everything belongs in the reserve. Rules it rests on: [WildlifeAssetStrategy.md](WildlifeAssetStrategy.md)
(style), [WildlifeExpansion.md](WildlifeExpansion.md) (a species needs a place), [Roadmap.md](Roadmap.md) (what never
to build).

**Art direction (director, 1.7):** recognisable wildlife → biodiversity → animation quality → environmental
storytelling → realism. A good stylised animal beats a placeholder. The environment stays photographic.

## 1. The audit: what is known, and what is not

**The imported packs have not been inspected.** Store packs live only in the Unity project (they may never be
committed), and the work on 1.7 was done without access to that project. Nothing below claims to know what is in
them. Instead, 1.7 ships the probe that finds out:

> **WildLens ▸ Asset Audit Report.** One click. It lists every imported model by pack: what animal it is, which
> reserve species it can be, its triangle count, size, clips and which behaviours those clips cover, shader problems,
> plus every butterfly, frog, mushroom, flower and vehicle it finds. It saves `WildLensAssetAudit.md` in the project
> folder and copies it to the clipboard. **Paste it into the chat**: sections 2 to 6 are then filled in from facts.

The master table the report produces has these columns, and is judged like this:

| Column | Source | How to judge it |
|---|---|---|
| Species / represents | name matching (exact, stand-in, or "new") | Would you name the animal from its outline at 16×? |
| Asset pack | folder | One model per species; packs may be mixed between species |
| Model quality | triangles, size, shader check, then a screenshot | Silhouette first. Pink = wrong render pipeline: run Unity's material converter |
| Animation quality | clip list and recognised roles | Ground animals want Idle, Walk, Run, Eat, Look. Birds want Idle, Fly, and ideally Glide, TakeOff, Land. Idle + Walk (or Idle + Fly) is the minimum worth using |
| Reserve habitat | section 6 | Where it would really live |
| Ready for integration | exact or stand-in match + minimum clips + shaders fine | Then it sets itself up on import or rebuild |
| Needs work | missing roles, wrong pipeline, odd scale, faces backwards | Size and yaw are one field each in **Wildlife Pack Setup** |

### Known today (from the code, not the packs)

Eighteen species are implemented. Two have shipped CC0 models; sixteen are placeholders waiting for a pack model.

| Species | Home | Model today | A pack model called… sets it up automatically (exact names; then stand-ins, closest first) |
|---|---|---|---|
| White-tailed deer | meadows, pond at dusk | placeholder | whitetail, deer, doe, stag, buck, fawn; else roe, elk, reindeer, caribou, moose, antelope, gazelle, impala |
| Eastern cottontail | brush, meadow edge | placeholder | cottontail, rabbit, bunny, hare |
| Red fox | meadow edge, grove | placeholder | fox; else shiba, fennec, jackal |
| Coyote | open grass, ridge | **CC0 wolf (shipped)** | coyote; else wolf, dingo, dhole, jackal, husky, shepherd, dog |
| Eastern gray squirrel | mature trees, falls | placeholder | squirrel, squirel; else chipmunk |
| Raccoon | creek, pond at dusk | placeholder | raccoon, racoon; else badger, red panda, coati |
| Virginia opossum | creek at night | placeholder | opossum, possum; else rat, mouse |
| Groundhog | meadow edge | placeholder | groundhog, woodchuck, marmot; else beaver, gopher, prairie dog, capybara, hamster, guinea pig |
| Wild turkey | blind meadow | placeholder | turkey; else pheasant, grouse, partridge, quail, guinea fowl, chicken, rooster, hen, peacock |
| Red-tailed hawk | ridge | **CC0 eagle (shipped)** | hawk, buteo; else eagle, falcon, buzzard, harrier, osprey, kite, vulture, raptor |
| Barred owl | Old-Growth Grove | placeholder | owl |
| Great blue heron | pond, ford | placeholder | heron; else egret, bittern, ibis, spoonbill, pelican, crane, stork, flamingo |
| Spotted sandpiper | ford gravel bar | placeholder | sandpiper; else killdeer, plover, dunlin, snipe, shorebird, wader, tern, seagull, gull |
| Northern cardinal | blind brush | placeholder | cardinal, redbird; else songbird, finch, sparrow, robin, oriole, canary, chickadee, wren, tit, thrush, warbler, swallow, bird |
| Blue jay | grove | placeholder | blue jay, jay; else bluebird, songbird, finch, sparrow, robin, chickadee, tit, wren, thrush, warbler, swallow, bird |
| Red-winged blackbird | pond reeds | placeholder | blackbird; else grackle, starling, crow, raven, songbird, sparrow, thrush, bird |
| Belted kingfisher | Creek Ford | placeholder | kingfisher; else songbird, sparrow, bird |
| Eastern phoebe | Willow Falls | placeholder | phoebe, flycatcher; else sparrow, songbird, finch, wren, chickadee, bird |

Words of four letters or fewer ("fox", "owl", "jay", "bee") must be a whole word in the model's name, so "Foxglove" is not
a fox; longer ones may sit inside a word. **Reserved:** woodpecker, hummingbird, duck, mallard, goose and swan are held
for future species, so a model with one of those words in its name is **never** used as a stand-in, not even through a
folder called "Birds". Crows and ravens do stand in for the blackbird, vultures and eagles for the hawk, and robins for
the cardinal and jay, until each gets a species of its own. The tables with the ranking rules are in
[Systems.md](Systems.md) (1.7.0).

## 2. Recommended species list

### Existing: keep all eighteen
Every one has a place and hours. None should be cut. Their bodies change; their ecology does not.

### Replacement: every placeholder, as soon as a model matches
Replace on sight, in the order of section 3. Rules for choosing when the audit shows several candidates:
- The real animal beats a stand-in; among stand-ins, the closer silhouette wins (wolf over dog, egret over flamingo).
- Then the one with more recognised clips. **An animated stylised model beats a realistic statue.**
- One model per species. One songbird model, tinted, may be five species: that is intended.
- Keep the shipped wolf and eagle only until a real coyote or hawk turns up.

### New: only where a place is waiting
Free packs make the model the easy part, so the list is ordered by **place**, not by what is in the pack. Each of
these fits the existing ground or flying brain: no new systems. Add one only when the audit shows a usable model.

| Candidate | Pack names that would serve | Where and when | Why it earns a place |
|---|---|---|---|
| **American crow** | crow, raven (today a stand-in for the blackbird) | trailhead, Reserve Road verges, pull-offs; all day | The trailhead and road have no wildlife of their own. A crow on the Reserve Board is life in the first ten seconds |
| **Eastern chipmunk** | chipmunk (today a squirrel stand-in) | Fern Glade and Hidden Overlook rocks; morning | Both hidden places have hotspots but no residents: a reason to sit there |
| **Canada goose** | goose (reserved: never a stand-in) | pond bank and pond meadow, grazing; dawn to dusk | A flock on the grass by the water; needs no swimming |
| **Mallard / wood duck** | duck, mallard (reserved: never a stand-in) | pond bank and ford gravel, loafing | Pond variety. On land only, until a marsh makes swimming worth building |
| **Painted turtle** | turtle, tortoise | a basking log at the pond; midday, fair weather only | Fills the pond's quiet midday; slides off (flees) if rushed |
| **Pileated woodpecker** | woodpecker (reserved: never a stand-in) | Old-Growth Grove snags; morning | The grove's missing voice; needs big dead trees, which the grove has |
| **Turkey vulture** | vulture, condor (today a vulture is a hawk stand-in) | soaring above the ridge at midday | Shares the hawk's brain; gives the telescope something to find |
| **Striped skunk** | skunk | meadow edge at dusk | A second dusk animal on the loop; slow and unbothered |
| **Mourning dove / American robin** | dove, pigeon, robin (a robin is today a songbird stand-in) | road verges, blind feeder, trailhead grass | Common, close, easy first photographs |
| **Little brown bat** | bat | over the pond at dusk and night | A dusk silhouette against the sky; wait for the night sky work |
| **Black bear** | bear | far side of the ridge, rare, dawn only | **Director's decision.** Memorable, but changes the reserve's mood: ask first |

Not for Willow Creek, whatever the packs contain: farm animals, pets as themselves, and anything from another
continent (lions, zebras, penguins). They may still serve as *stand-ins* when the outline fits (a husky as a coyote).

## 3. Integration plan, in order

1. **Run the Asset Audit Report and paste it into the chat.** Everything after this is decided from it.
2. **Let the automatic setup run** (import, or rebuild). Read the Console line: which species got a model, which did not.
3. **The most photographed first:** deer, fox, cottontail, squirrel. Photograph each at 4× and 16×, dawn and golden
   hour. Fix size and yaw in **Wildlife Pack Setup**. Check walk, graze and alert all animate.
4. **One songbird model into five species** (cardinal, jay, blackbird, kingfisher, phoebe). The biggest gain in
   variety for one decision. Check the tints read at 8×.
5. **The distinctive outlines:** owl, heron, turkey. Each makes a place (grove, pond, blind meadow).
6. **The rest:** raccoon, opossum, groundhog, sandpiper; real coyote and hawk if the packs have them.
7. **Small life** (section 4): let **Macro Subjects From Packs** run, rebuild, walk the loop verge and the pond edge
   with the macro lens (M).
8. **New species, one at a time, in the order of the table above**, each only when its model is in the audit.
9. **Awareness retune.** New bodies change what "visible" means; walk up to each species once and confirm that
   standing still always lets it settle.

Screenshots are the test at every step. An animal that looks wrong at 16× is not ready, whatever its clip list says.

## 4. Macro wildlife opportunities

1.7 turns the macro groundwork into a pipeline: a small-life model found in your packs is given to a field-notebook
subject and placed in its habitat on the next rebuild (models over 3 m are ignored). Subjects with no model stay out of the reserve; nothing is
faked. Behaviour is deliberately simple and patience always works.

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

### Plants
| Subject | Source | Where |
|---|---|---|
| Common dandelion | shipped (CC0 scan) | loop verges |
| Lesser periwinkle | shipped (CC0 scan) | Fern Glade, grove |
| Oxeye Daisy | pack: daisy, oxeye; else flower, wildflower (not a pot, vase, bouquet, tree, bush or bed) | loop verges |
| *Next:* milkweed (the monarch's plant), cattail heads at the pond, trillium in the grove, goldenrod at the blind meadow | pack or CC0 scan | as named |

### Fungi
| Subject | Source | Where |
|---|---|---|
| Honey Mushroom | pack: mushroom, toadstool, fungus, fungi, shroom | on logs and stumps in the woods |
| *Next:* bracket fungus on the Grandmother Oak, a fairy ring in the Fern Glade after rain | pack or CC0 scan (Poly Haven) | as named |

**The pairings are the point:** a monarch *on* a dandelion, a dragonfly *at* the ford where the kingfisher fishes, a
newt in the glade only after the day-3 shower. Each gives a reason to be in a place at a time, the same way the
heron does. No counts, no completion: the field notebook just remembers what you found.

## 5. Motorcycle recommendations

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

## 6. Reserve ecology: where the new life belongs

| Place | Has today | Add | The picture it makes |
|---|---|---|---|
| **Trailhead and bike bay** | nothing | crow on the board; robin or dove on the grass | Life before you have started the bike |
| **Meadow Loop** | deer, cottontail, fox, hawk, squirrel, groundhog | monarch and bee on dandelions, mantis and lady beetle on the verge, fireflies at dusk; skunk at dusk | Look far, then look down |
| **Reserve Road and pull-offs** | deer crossing, turkeys | crows, doves on the verge | Reasons to slow down |
| **Ridge Overlook** | hawks, coyote | vulture soaring at midday | Something for the telescope |
| **Cattail Pond** | deer, heron, blackbird, raccoon | dragonflies and frogs at the edge; geese on the bank; a turtle on a log | The busiest place in the reserve, at every hour |
| **Old-Growth Grove** | squirrel, fox, owl, jay | mushrooms, snails and a luna moth on the logs; newts after rain; woodpecker | The grove rewards a slow walk as well as a long lens |
| **Wildflower Meadow Blind** | deer, cottontail, cardinal, turkey | daisies with bees and butterflies in front of the blind | Macro subjects within reach of the bench |
| **Willow Creek and the Ford** | kingfisher, sandpiper, heron, opossum | dragonflies on the gravel bar, frogs on the bank | Sit on the log seat; everything comes to the water |
| **Willow Falls** | phoebe, squirrel | frogs at the plunge pool; newts after the spate | Wet rock, small life |
| **Fern Glade (hidden)** | periwinkle only | chipmunk; newts, snails, mushrooms | A hidden place with something living in it |
| **Hidden Overlook** | a perch only | chipmunk on the rocks | A companion on the log seat |
| **Creek Alcove (hidden)** | raccoon | frogs | Unchanged in spirit |

Principles for all of it: residents make a place *about* its animal; commuters make the clock matter; small life
makes the weather matter. Nothing spawns on cue, nothing is counted, and every animal still settles if you stand
still.
