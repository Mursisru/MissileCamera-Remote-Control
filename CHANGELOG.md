# Changelog

All notable changes to **MissileCamera: Remote Control** are documented in this file.

## [0.0.0] - 2026-07-31

### Added

- Initial BepInEx 5 addon scaffold (hard dependency on MissileCamera).
- Selective `[DL]` / `[SATCOM]` weapon mount clones with hardpoint-compatible injection.
- Host/SP remote control: mouse aim, jet/solid throttle & afterburner.
- Encyclopedia missile definition entries for RC variants.
- Launch path stamps RC identity onto stock Mirage-registered missile prefabs.
- Warhead safety while RC is active (fins / tangible / arm / proxy fuse).

### Changed

- Default keybinds: `T` take/release RC, Left Shift afterburner, Right Shift / Right Ctrl throttle; mouse aim only. Cycle, choke, retarget, and airburst binds removed.
