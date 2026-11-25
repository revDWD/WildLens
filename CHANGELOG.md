# Changelog

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
