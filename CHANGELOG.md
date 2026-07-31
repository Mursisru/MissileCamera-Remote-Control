# Changelog

All notable changes to **MissileCamera: Remote Control** are documented in this file.

## [0.0.0] - 2026-07-31

### Added

- Initial BepInEx 5 addon scaffold (hard dependency on MissileCamera).
- Selective `[DL]` / `[SATCOM]` weapon mount clones with hardpoint-compatible injection.
- Host/SP remote control gated to MissileCamera fullscreen.
- War Thunder-style world-space aim reticle (projection follows missile turn; mouse only moves aim).
- Soft command-angle clamp so stock `gLimit` / Steering are not permanently saturated.
- Custom vanilla-tone encyclopedia descriptions for RC variants.
- Jet / solid throttle & boost, retarget, airburst.
