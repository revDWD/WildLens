# Changelog

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
