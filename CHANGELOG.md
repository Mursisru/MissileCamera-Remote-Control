# Changelog

All notable changes to **MissileCamera: Remote Control** are documented in this file.

## [0.0.0] - 2026-08-01

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
- Spectator / HUD-selected allied RC: `K` injects MissileCamera follow, enters Fullscreen, and Takes RC.
- AI aircraft equip RC clones instead of vanilla whitelist mounts (`AiEquipRcClones`, default on). Bots do not remote-pilot — Seek runs normally.

### Changed

- Default keybinds: `T` take/release RC, `L` missile list, Left Shift afterburner, Right Shift / Right Ctrl throttle; mouse aim only. Cycle, choke, retarget, and airburst binds removed.
- Upright preference boost / roll assist under RC disabled (stock Steering only).
- RC clone munitions cost 10% more than stock (`costPerRound` + `emptyCost`).

### Fixed

- Terminal seeker takeover near target (~`terminalRange`): Seek skip uses session ownership; cruise `guidance`/`terminalMode` suppressed while RC; Select prepare no longer enables autonomous guidance until Release.
- DeployFins one-shot under RC (avoid fin-fold animation jerks); dual Update/Fixed `SetAimpoint` reinforce removed.
- RC display names across HUD, map, and killfeed (rename before Mirage Spawn + PersistentUnit sync).
- MP presence gate: detect clients via `NetworkMode.Client`, fail-closed until reply, strip RC mounts while disabled.
