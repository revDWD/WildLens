# Changelog

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
