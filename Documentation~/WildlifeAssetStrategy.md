# Wildlife asset strategy (1.5)

Animals are now the reserve's largest visual weakness. 1.5 re-examined the old assumption that every animal must be
realistic: the director's guidance is that **a coherent style with recognisable silhouettes, animation variety and
species breadth matters more than photorealism**. This page sets out the options and recommends a route. Research was
done in November 2026 from search results (several store pages could not be opened from the build machine), so
**check each listing before buying**.

## What the game needs from an animal

The behaviour code already drives Mecanim through `AnimalAnimatorBridge` (parameters `Speed`, `Activity`,
`GaitPhase`), and **WildLens ▸ Animal Model Setup** turns any rigged, animated model into a species prefab in minutes.
The clips that matter, in order: idle, walk, run, graze/eat, look/alert, rest/lie down; birds: perch idle, take-off,
fly loop, glide, land. A pack that has these for many species is worth more than a beautiful model of one.

## Options found

| Asset | Licence / price | Covers (of our 18) | Animation | Style |
|---|---|---|---|---|
| **Quaternius Ultimate Animated Animal Pack** | **CC0**, free (can be committed) | Deer (doe and stag), fox, wolf as a coyote stand-in | 12+ clips: idle, walk, gallop, eat, jump… (no sit/sleep) | Low-poly, flat shaded |
| **BundleSmith Animated Forest Animals Pack** | Asset Store EULA, ~$33 | Deer, fox, rabbit, squirrel, raccoon, wolf (coyote) | Idle, graze, jump, walk, run, rest, sleep, look: an exact match for our states | Stylised low-poly |
| **Polyperfect Low Poly Animated Animals** | EULA, $50–100 | Deer, fox, rabbit, squirrel, eagle (hawk stand-in), wolf, many more | Animated (clip list unverified); Unity 6 + URP package, updated Aug 2025 | Low-poly |
| **Malbers Poly Art: Animal Forest Set** | EULA, $90–180 | Deer, fox, rabbit, raccoon, wolf | ~62 clips per animal | Faceted "poly art" |
| **Protofactor Forest Animals + Birds packs** | EULA, ~$50 + birds | Deer, fox, rabbit, wolf; owl, eagle | Mecanim, in-place and root motion, LODs | Semi-realistic (older PBR) |
| **Mixall bird packs** (Soaring Birds ~$30; 60 Birds ~$300) | EULA | Hawk, many songbirds (species list unverified) | Perch, take-off, fly, soar | Realistic, URP |
| Smithsonian 3D (CC0) | CC0 | Static scans of insects: future macro subjects | none | Photographic |

Not usable here: Khronos sample Fox (its rig is CC-BY, but only CC0 is committed); Sketchfab "free" animals with
personal-use notices; Meshy free-plan outputs (CC-BY); Unreal-only packs. Nothing found covers the heron, turkey,
groundhog, songbirds or sandpiper in a style matching any mammal pack: **birds will need a second pack whatever route is
chosen.**

## The style decision (the director's call)

The environment is photographic (scanned ground, rocks, skies, wood). AGENTS.md has held that "mixing fidelities is
worse than either alone". Low-poly flat-shaded animals in front of scanned rocks will look like toys, the same problem
the primitive placeholders have. So there are three coherent routes:

1. **Semi-realistic animals, photographic reserve (recommended).** Protofactor Forest + Birds covers 6 of the 18
   (deer, fox, rabbit, coyote via wolf, owl, hawk via eagle) in one PBR style that sits with the scanned environment;
   add Mixall Soaring Birds for the hawk if the eagle reads wrong. Placeholders remain for the rest until more of
   the same style is found. Cost ≈ $80–110.
2. **Stylised animals, restyled reserve.** BundleSmith (6 species, the best clip match) or Polyperfect (widest
   catalogue), with the environment pushed toward a painterly *Firewatch* look to match (colour grading, simpler
   ground, stylised foliage). Most species fastest, but it is an art-direction change, not an asset drop.
3. **CC0 now, decide later.** Commit the Quaternius pack for deer, fox and coyote as an *opt-in* style in the builder,
   so anyone can try it without buying anything. Cheapest; only 3 species, and stylised.

**Recommendation:** route 1, deer first (the most-photographed animal), then fox and hawk, as AGENTS.md has long
said; it keeps the photographic identity the playtests praised. Choose route 2 only together with an environment
restyle. In either case, the next tooling step is a **pack setup** (one action that maps a whole pack's models to
species by name and runs Animal Model Setup on each), worth building once a pack is chosen.

## Licence rules (unchanged)

Only CC0 is committed to the repository. Store packs are imported into your own project and never committed; the
builder and Animal Model Setup use them from there. Don't use "free download" re-uploads of paid packs.
