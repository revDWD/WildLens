# WildLens: Willow Creek Nature Reserve

A quiet, first-person wildlife photography game set in a nature reserve. Walk the Meadow Loop or ride the trail bike
along the Reserve Road to a ridge overlook, a reedy pond, an old-growth grove and a meadow blind; learn where and
*when* the deer, foxes, hawks, cottontails and squirrels are; approach without spooking them; and take photographs
worth keeping. The reserve changes from dawn mist to golden hour to dusk, and its wildlife keeps hours.

**New here?** Players: read *Install & run* and *Controls* below. Contributors (people or AI agents): start with
[`AGENTS.md`](AGENTS.md), then [`CONTRIBUTING.md`](CONTRIBUTING.md).

| Document | What's in it |
|---|---|
| [`AGENTS.md`](AGENTS.md) | The handoff: what WildLens is, its philosophy, current state, common mistakes, and what to read next for every topic |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Setup, the build → playtest loop, compile checks without Unity, release checklist |
| [`CHANGELOG.md`](CHANGELOG.md) | Every release |
| [`Documentation~/Architecture.md`](Documentation~/Architecture.md) | Packaging, the file map and the key flows through the code |
| [`Documentation~/Systems.md`](Documentation~/Systems.md) | How each system works and where to tune it |
| [`Documentation~/History.md`](Documentation~/History.md) | Every release, why it was made and what it taught |
| [`Documentation~/Roadmap.md`](Documentation~/Roadmap.md) | What to build next, in order, and what not to build |
| [`Documentation~/ReserveExpansion.md`](Documentation~/ReserveExpansion.md) | Reserve design principles and next places |
| [`Documentation~/WildlifeExpansion.md`](Documentation~/WildlifeExpansion.md) | Adding species: the framework, ten planned species, assets |
| [`Documentation~/VisualRoadmap.md`](Documentation~/VisualRoadmap.md) | Art sources, licences, visual weaknesses, Unity 6 notes |
| [`Documentation~/EnvironmentMigration.md`](Documentation~/EnvironmentMigration.md) | Environment audit, free asset list, migration plan, before/after |
| [`Documentation~/WildlifeAssetStrategy.md`](Documentation~/WildlifeAssetStrategy.md) | Animal models: stylised and mixed packs welcome, free sources, Wildlife Pack Setup |
| [`Documentation~/TrailBike.md`](Documentation~/TrailBike.md) | The bike model, free models reviewed, dropping one in, next improvements |

## Install & run

WildLens is a Unity package installed straight from GitHub (Unity 2021.3 LTS or newer, including Unity 6;
Built-in, URP or HDRP; legacy Input Manager or Input System).

1. Install [Git](https://git-scm.com/downloads) if it isn't already installed, then restart Unity Hub. Unity uses
   it to download packages.
2. In Unity, open **Window ▸ Package Manager**, click **+ ▸ Add package from git URL…** and enter
   `https://github.com/revDWD/WildLens.git`
3. Open a scene (ideally one with a Terrain) and choose **WildLens ▸ Build Vertical Slice In Open Scene**.
4. Save the scene and press **Play**. Click the Game view to capture the mouse.

**Updating:** in Package Manager, select WildLens and click **Update**. If no Update button appears, remove the
package and add the URL again. Updates that change the environment (0.4.0, 0.5.0, 0.6.0) need a **rebuild** of the
slice to take effect: **WildLens ▸ Vertical Slice Builder ▸ Rebuild Slice In Open Scene**. Since 0.6.0 an out-of-date
scene says so: a notice when you press Play, a warning in the builder window, and a line in the F3 overlay. Run **WildLens ▸ Capture Comparison Shots** before and after to compare.

The builder puts everything under one root object, **`WildLens Vertical Slice`**, so you can rebuild or remove
it at any time. Painting the ground and shaping the reserve (a pond basin, an overlook hill) change your Terrain's
data, so it is **backed up first** to `Assets/WildLens/Generated/Backup`; rebuilding gives the same shape rather than
digging deeper. If the scene has no Terrain, it creates a 900 m fallback with room for the reserve.
It also disables other enabled scene cameras in favour of the player camera (this is undoable, and you can
turn it off in the settings). Files you can edit (species tuning, builder settings, generated meshes and
materials) are written to your project's `Assets/WildLens/` folder, because the package itself is read-only.

## Controls

| Input | Action |
|---|---|
| WASD / Shift | Walk / sprint |
| **C** | Crouch (toggle; Left Ctrl hold also works in builds) |
| Mouse | Look |
| **Hold RMB** | Camera mode: raise the camera (slower, steadier, viewfinder) |
| Scroll, or E / Q | Zoom 1× / 2× / 4× / 8× / 16×. Zooming while lowered raises the camera; zoom out past 1× to lower it |
| LMB or F (in camera mode) | Take photo |
| J or Tab | Field journal (pauses the world) · W/S to browse |
| **R** | Ride the trail bike (next to it) · park and step off (while riding) |
| W / S · A / D (riding) | Throttle / brake (and slow reverse) · steer |
| **Hold T** | Let time pass (standing still): wait for dawn, golden hour, dusk |
| H | Toggle control hints |
| F3 / F4 | Camera diagnostics overlay / zoom self-test |
| Esc | Release mouse / close journal |

**Finding and approaching wildlife:** deer graze in flower meadows, rabbits and foxes stay near brush piles,
squirrels live around big old trees, and hawks perch on dead snags at meadow edges. Raise the camera and hold an
animal in the viewfinder to **identify** it (no photo needed). Then **crouch** and creep closer. When it stops and
stares, freeze until it relaxes. The viewfinder brackets turn green (Good) or gold (Excellent) when the shot is
right.

**Time of day (0.9):** the reserve runs from dawn to dusk (about sixteen minutes of daylight; hold T to let time
pass). Mist lies on the pond at dawn, the ridge glows at golden hour, deer move to the meadows in the morning, bed
down in the woods at midday and come to the water at dusk. The trailhead board says what's happening now and when
each destination is best. Ride the **Reserve Road** between destinations; footpaths are for walking.

**Willow Creek (0.8)** runs from the ridge to the pond; cross it on footbridges, listen for it, and look for deer
drinking where it runs through open ground. Riding into a **pull-off** the HUD offers to park; the walk from there
keeps your engine out of the animals' earshot. In the **Meadow Blind** the HUD shows *HIDDEN*: sit at the window.

**The reserve (0.7):** the trailhead board's map shows four destinations beyond the Meadow Loop: **Ridge Overlook**
(hawks, the widest view), **Cattail Pond** (deer drink at the reed edge), **Old-Growth Grove** (squirrels, a fox) and
the **Wildflower Meadow Blind** (step inside the hide and wildlife can't see you). Fingerposts mark where each reserve
track leaves the loop. The trail bike at the trailhead gets you there; its engine carries, so park short and walk
in. See [`Documentation~/ReserveExpansion.md`](Documentation~/ReserveExpansion.md) for the plan beyond this.

See [`Documentation~/Systems.md`](Documentation~/Systems.md) for how every system works and where to
tune it.

## Architecture

Two assemblies: `Scripts/` (runtime, `WildLens.Runtime`) and `Editor/` (scene builder and tools, `WildLens.Editor`).
The full file map, data flow and build pipeline are in [`Documentation~/Architecture.md`](Documentation~/Architecture.md); per-system detail is in
`Documentation~/Systems.md`. Written into your project (editable): `Assets/WildLens/Data/` (species assets, species
database, builder settings) and `Assets/WildLens/Generated/` (meshes, materials, textures, terrain backups).

### Adding or tuning a species
- Tune behaviour, senses, population, habitat and daily rhythm on the assets in `Assets/WildLens/Data/Species/`.
  Rebuilds don't overwrite them.
- New species need **data, not code**: a Species Definition with a generic placeholder body (`Placeholder Body:
  Generic` + `Body Plan`), a `Daily Rhythm`, and habitat hotspots. See
  [`Documentation~/WildlifeExpansion.md`](Documentation~/WildlifeExpansion.md). Tick **Preview expansion species** in
  the builder settings to see the worked examples (Raccoon, Great Blue Heron).
- Real art (1.6): import free animal packs (stylised and mixed styles are fine), then **WildLens ▸ Wildlife Pack Setup ▸
  Scan project ▸ Set up ticked species** matches models to species (with stand-ins such as a wolf for the coyote) and
  makes their prefabs. **WildLens ▸ Animal Model Setup** does one species by hand. See
  [`Documentation~/WildlifeAssetStrategy.md`](Documentation~/WildlifeAssetStrategy.md).

### Using your own scenery assets (Environment Kit, 1.2)
Import free nature packs into your project (Unity's free **Terrain URP Demo Scene** and **Terrain Sample Asset Pack**
are the best start), then **WildLens ▸ Vertical Slice Builder ▸ Scan Project for Nature Assets** and Rebuild. The
kit sorts trees, shrubs, rocks, terrain layers and grass by ecological role, and the builder places each where it
belongs with the colliders and wildlife data gameplay needs. Store assets are never committed to this repository.
Details: [`Documentation~/EnvironmentMigration.md`](Documentation~/EnvironmentMigration.md). (The older **Tree / Bush
/ Rock prefabs** lists in the builder settings still work, and place prefabs at random.)

## Save data
- Journal: `Application.persistentDataPath/WildLens/journal.json`
- Photos (JPG): `Application.persistentDataPath/WildLens/Photos/`
- Reset: **WildLens ▸ Reset Journal Save Data**, or the **Reset Journal** context menu on `WildLensGame` in Play mode.
