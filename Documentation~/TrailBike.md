# The trail bike: model review and next steps (1.6)

The bike is a Trail 125-style step-through trail bike built from primitives (`Scripts/Vehicles/TrailBikeModel.cs`):
upswept exhaust, rack and cargo box, visible fork and twin rear shocks that move (1.5), a parked lean onto the
kickstand. Its job is set by AGENTS.md: friendly, quiet-ish, practical, never racing. **Design rules don't change
here; only the model's looks do.**

## Free and open models reviewed (November 2026)

| Model | Licence | Fit for WildLens | Verdict |
|---|---|---|---|
| Duhgless PSX-style low-poly bikes ([1](https://duhgless.itch.io/psx-style-low-poly-bike), [2](https://duhgless.itch.io/another-psx-style-low-poly-bike), [3](https://duhgless.itch.io/yet-another-psx-style-low-poly-bike)) | CC0 (per listing), GLB | Cruiser/retro road bikes in PS1 style | Wrong type (Harley/Yamaha road bikes) and a deliberately retro look. Not better |
| [Motorcycle Pack: Naked, Chopper, Police Bikes (Free)](https://assetstore.unity.com/packages/3d/vehicles/land/motorcycle-pack-naked-chopper-police-bikes-free-290881) | Asset Store EULA, free, URP + Built-in | Road bikes | Good quality, wrong character: a naked or police bike says "speed". Not for the reserve |
| Same series: [Sport, Naked, Dirt Bikes](https://assetstore.unity.com/packages/3d/vehicles/land/motorcycle-pack-sport-naked-dirt-bikes-290628) | EULA, paid (~$20) | Includes a dirt bike | The closest store match: a dirt bike, but a racing one. Usable if toned down (no number boards) |
| [Dirtbike Low Poly](https://www.cgtrader.com/free-3d-models/vehicle/motorcycle/dirtbike-low-poly) (CGTrader) | Royalty-free, not CC0 | Motocross bike | Licence not CC0 (can't be committed); motocross styling |

**Conclusion:** no free model beats the current one for *this* bike. The free options are road bikes or racers, which
is the opposite of the design brief. The generated bike stays. A purchased or free model can still be dropped in
whenever one fits (below).

## Dropping in a model (no code needed)

`TrailBike` finds its moving parts by name under the bike's **Model** child, so any model works if its hierarchy
has empty pivots named:

`Lean` (root of everything that leans) → `Steer` (fork and bars; yaws) → `Front Axle` (slides up the fork) →
`Front Wheel` (spins on X); `Swingarm` (pivots on X) → `Rear Wheel`; `Kickstand`; `Seat Anchor` (rider position,
outside `Lean`). `TrailBike` finds each of these by name anywhere under `Model`. Only `Seat Anchor` is made for you:
if the model has none, one is created under the bike's root, 0.3 m behind its centre.

Missing pivots are skipped (no suspension without `Front Axle`/`Swingarm`; no lean without `Lean`). Keep the model
under ~2.0 m long, wheels ~0.3 m radius, facing +Z. Store models stay in your own project, never in the package.

## Next improvements to the generated bike (in order)

1. **Shaped bodywork**: a curved front fender and fuel tank from `ProceduralMeshes` (swept profiles) instead of
   boxes; this is the biggest step from "toy" to "machine" in a close shot.
2. **Real wheels**: a torus tyre with tread blocks and a thin-rim mesh; the current spokes are four boxes.
3. **A smaller round headlight with an emissive lens that lights at dusk** (`DayCycle` period), plus a tail light.
4. **Rider hands on the grips** in first person while riding, so the bars read as held.
5. **Mud and wear**: a dirt mask on the lower parts and tyres (from the trail's own colour).

None of these changes speed, handling or anything wildlife hears.

## Finding a candidate (1.7)

**WildLens ▸ Asset Audit Report** lists every vehicle model it finds in your imported packs, with its length, how many
wheel parts it has, whether it has a separate steering part (handlebars or fork), and which of the pivot names above it
already uses. The acceptance criteria for a replacement (small, upright, rack, separate wheel and fork objects; leave
sport bikes and fused meshes; a utility vehicle is scenery, never the bike) are in
[WildlifeCampaign.md](WildlifeCampaign.md) §5.

**A motorcycle pack is now imported in the director's project** (18 static models). It has not been judged: the 1.8 audit
lists its models by name, and the criteria above and in WildlifeCampaign.md §5 then decide whether any is usable.
