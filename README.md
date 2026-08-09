# MissileCamera: Remote Control

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx 5](https://img.shields.io/badge/Loader-BepInEx%205-orange)](https://docs.bepinex.dev/)
[![Version](https://img.shields.io/badge/Version-1.0.0-green)](https://github.com/Mursisru/MissileCamera-Remote-Control/releases/tag/1.0.0)
[![Requires MissileCamera](https://img.shields.io/badge/Requires-MissileCamera-lightgrey)](https://github.com/Mursisru/MissileCamera)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

BepInEx 5 addon for **Nuclear Option**. Extends [MissileCamera](https://github.com/Mursisru/MissileCamera) with remote-control (RC) munition clones, fullscreen mouse guidance, throttle / afterburner, and datalink / SATCOM link rules.

**Plugin GUID:** `com.at747.missilecamera.remotecontrol`  
**Hard dependency:** `com.at747.missilecamera.bepinex` (MissileCamera)

---

## Critical requirements

> [!IMPORTANT]
> **Install MissileCamera first.** This addon will not load without it. Both plugins go in `BepInEx/plugins/`.

> [!IMPORTANT]
> **BepInEx 5 (x64)** is required. Use the same loader setup as MissileCamera.

> [!WARNING]
> **Steering authority is host / single-player only** (`Server.Active` && `missile.LocalSim`). Pure multiplayer clients cannot pilot missiles.

> [!IMPORTANT]
> **Multiplayer presence:** if you join a server that does **not** run this addon, RC mounts and controls are **disabled for that session** (fail-closed). They return when you leave that server. Hosts / servers that include the addon stay enabled.

> [!CAUTION]
> Do not replace vanilla weapon assets. RC options are separate loadout entries. Removing the DLL mid-campaign may leave orphaned mount keys in saved loadouts.

---

## What it does

- Whitelisted **DL** and **SATCOM** mount clones (unique display names, encyclopedia entries)
- Injection only on hardpoints that already carry the stock weapon
- **Fullscreen RC** via MissileCamera: War Thunder–style **world-space** mouse aim, stock aero / G-limits
- Jet / solid **throttle** and **afterburner** (Motor thrust + VFX; gauge stays 0–1)
- **Datalink quality** for DL (mesh range, LoS degrade, jam break); SATCOM stays full link
- **Missile picker** in fullscreen (`L`)
- **AI loadouts** can equip RC clones (bots do not remote-pilot — Seek runs normally)
- RC munitions cost **+10%** vs stock
- Auto-exit MissileCamera fullscreen when your tracked ownship is destroyed

### Whitelist (v1)

| Role | RC name | Stock base |
|------|---------|------------|
| DL Jet | ALM-D500 | ALM-C450 |
| DL Jet | AGM-98D | AGM-99 |
| DL Jet | DLhM-300S | AShM-300 |
| DL Solid | Tusko-D | Tusko-B |
| SATCOM Jet | ALND-4S | ALND-4 |
| SATCOM Solid | Piledriver TBM-S | Piledriver TBM |

---

## Install

1. Install **BepInEx 5** and **[MissileCamera](https://github.com/Mursisru/MissileCamera)**.
2. Download `RemoteControl-1-0-0.zip` from [Releases](https://github.com/Mursisru/MissileCamera-Remote-Control/releases).
3. Extract `MissileCameraRemoteControl.dll` into:

```text
…/Nuclear Option/BepInEx/plugins/
```

4. Launch once to generate `BepInEx/config/com.at747.missilecamera.remotecontrol.cfg`.

---

## How to use

1. Equip an RC clone on a compatible hardpoint.
2. Launch the missile, open **MissileCamera Fullscreen** (default `K` in MissileCamera).
3. Press **`T`** to take / release remote control (or **`L`** to pick from the list).
4. Aim with the **mouse**; manage throttle / afterburner with the binds below.

> [!TIP]
> World-space aim: the reticle is a command point in the world. Turning the seeker slides the marker on the FLIR — fly the nose onto the marker, War Thunder style.

> [!NOTE]
> While you hold RC, proximity fly-by airburst is disabled so CPA near-misses do not detonate. Impact fuse still works. On release, seeker handoff resumes autonomous guidance toward the RC-chosen target.

---

## Default keybinds

| Action | Default |
|--------|---------|
| Take / release RC | `T` |
| Open RC missile list | `L` |
| Afterburner (hold) | Left Shift |
| Throttle up | Right Shift |
| Throttle down | Right Ctrl |
| Aim (mouse) | Mouse |
| Aim yaw / pitch (keys) | Arrow keys |

`AimInputMode` in config: `Mouse` (default) / `Keys` / `Both`. Rebind aim keys to WASD, numpad, or any custom `KeyboardShortcut` in `com.at747.missilecamera.remotecontrol.cfg`.

All binds are configurable in the BepInEx config file.

---

## Branches

| Branch | Purpose |
|--------|---------|
| `main` | Stable releases |
| `dev` | Ongoing development (starts from `main` at 1.0.0) |

---

## Build from source

```bash
dotnet build MissileCameraRemoteControl.csproj -c Release
```

Point game / reference paths via a local `Directory.Build.props` (not committed).

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

---

## Licence

MIT — see [LICENSE](LICENSE).
