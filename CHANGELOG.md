# Changelog

All notable changes to **MissileCamera: Remote Control** are documented in this file.

## [1.0.0] - 2026-08-01

First public release.

### Added

- BepInEx 5 addon with hard dependency on [MissileCamera](https://github.com/Mursisru/MissileCamera).
- Whitelisted RC mount clones (DL / SATCOM) with hardpoint-compatible injection and encyclopedia entries.
- Fullscreen remote control: world-space mouse aim, throttle, afterburner, FS missile picker (`L`).
- Datalink quality for DL clones; SATCOM always full link; host→client afterburner VFX sync.
- MP server presence handshake (fail-closed for clients when the server lacks this addon).
- AI loadout swap to RC clones (`AiEquipRcClones`, default on). Bots do not remote-pilot.
- Auto-exit MissileCamera Fullscreen when the tracked local aircraft is destroyed.
- Hot-path performance pass (no render Hz / quality changes): Seek early-out, FS frame cache, edge-only VFX/throttle/proxy writes, Steering-only seeker suppress, allocation-free retarget input.

### Changed

- Default binds: `T` take/release, `L` list, Left Shift afterburner, Right Shift / Right Ctrl throttle, mouse aim.
- RC munitions cost **+10%** vs stock (persists across `WeaponMount.Initialize`).
- Spectator `K`→RC engage removed — RC only from MissileCamera Fullscreen while flying.

### Fixed

- Afterburner / `Motor.Thrust` Harmony chain (Spawn parameter name + PatchAll isolation).
- Terminal cruise seeker stealing aim under RC; proximity fly-by airburst while controlling.
- Aim/reticle sync after feed `SyncPose`; RC display names on HUD / map / killfeed.

### Pre-release

Internal `0.0.x` builds (2026-08-01) covered scaffolding and the fixes listed above before this public tag.
