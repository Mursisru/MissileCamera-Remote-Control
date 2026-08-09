# Changelog

All notable changes to **MissileCamera: Remote Control** are documented in this file.

## [1.9.9] - 2026-08-09

Pre-release on `dev` since **1.0.0**.

### Added

- Formation follow (`P`): allied RC missiles hold ahead/behind slots parallel to the lead aim ray; optional `Control.AutoFormationFollow`.
- Aim input modes: Mouse / WASD / Arrows / NumPadArrows / Custom (`CustomAim` binds).
- Whitelist expansions: **AGM-68D** (AGM-68 / `AGM_heavy*` only), **AAM-46 Longstrong** (AAM-36 only), **76mm DLG Shell** (player-controllable).
- Shared `WeaponInfo` so RC mounts stack correctly in loadouts.
- `General.AllowAnyMunition` (default **false**): when off, only official RC clones are remote-controllable; when on, any allied LocalSim munition.
- FOLLOW / AFTERBURNER status HUD while controlling.
- Hot-path Seek skip-set, proxy/suppress latches, living RC registry, aim write dedupe.

### Fixed

- Target-lost cruise loiter (DetonateGate only while player owns the missile).
- Ballistic / long-flight terrain tunneling and soft-land sink.
- Dive flattened by height clamp → ray-resolve along look / sea plane.
- Random sharp reverse turns (zenith snap, mouse spikes, aim behind nose, formation lateral).
- Formation chasing a marker behind the nose (braking / weird turns).
- FOLLOW forcing afterburner; AB gated on motor fuel.
- Mid-flight blast cook-off false detonations; raised lethal cook-off thresholds.
- AGM-68D never cloned from AGM-48 (`AGM1*`); AAM-46 never from AAM-29.

### Changed

- Official RC identity no longer treats bare `[DL]` / `[SATCOM]` name prefixes as controllable (blocks third-party naming hijacks).
- Afterburner / FOLLOW UX split; degraded DL no longer kills AB solely from optical LoS.

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
