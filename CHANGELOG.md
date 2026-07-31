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
- P2 datalink quality for DL clones (mesh range / LoS degrade / jam break); SATCOM ignores terrain and jam.
- P3 FS missile picker (`L`) and host→client afterburner VFX sync.
- MP server presence handshake: disable RC on clients when the server lacks this addon.

### Changed

- Default keybinds: `T` take/release RC, `L` missile list, Left Shift afterburner, Right Shift / Right Ctrl throttle; mouse aim only. Cycle, choke, retarget, and airburst binds removed.
- Upright preference boost / roll assist under RC disabled (stock Steering only).

### Fixed

- Terminal seeker takeover near target (~`terminalRange`): Seek skip uses session ownership; cruise `guidance`/`terminalMode` suppressed while RC; mouse `SetAimpoint` reinforced before Steering; Select prepare no longer enables autonomous guidance until Release.
