# MissileCamera: Remote Control

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx 5](https://img.shields.io/badge/Loader-BepInEx%205-orange)](https://docs.bepinex.dev/)
[![Version](https://img.shields.io/badge/Version-2.0.2-green)](https://github.com/Mursisru/MissileCamera-Remote-Control/releases/tag/2.0.2)
[![Requires MissileCamera](https://img.shields.io/badge/Requires-MissileCamera-lightgrey)](https://github.com/Mursisru/MissileCamera)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

BepInEx 5 addon for **Nuclear Option**. Extends [MissileCamera](https://github.com/Mursisru/MissileCamera) with remote-control (RC) munition clones, fullscreen mouse guidance, throttle / afterburner, formation follow, and datalink / SATCOM link rules.

**Plugin GUID:** `com.at747.missilecamera.remotecontrol`  
**Soft dependency:** `com.at747.missilecamera.bepinex` (MissileCamera). If missing, RC offers an install prompt and stays off until MC is present.

> [!NOTE]
> Current release is **2.0.0** ([`main`](https://github.com/Mursisru/MissileCamera-Remote-Control/tree/main) / [`v2.0.0`](https://github.com/Mursisru/MissileCamera-Remote-Control/tree/v2.0.0)). Ongoing work continues on [`dev`](https://github.com/Mursisru/MissileCamera-Remote-Control/tree/dev).

---

## Critical requirements

> [!IMPORTANT]
> **Install MissileCamera first.** Without it, Remote Control shows an install prompt and stays disabled until Missile Camera is present. Both plugins go in `BepInEx/plugins/`.

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
- **Fullscreen RC** via MissileCamera: War Thunder–style **world-space** aim, stock aero / G-limits
- Configurable **aim input**: Mouse / WASD / Arrows / NumPad / Custom binds
- Jet / solid **throttle** and **afterburner** (Motor thrust + VFX; gauge stays 0–1)
- **Formation follow** (`P`): other allied RC missiles fly parallel slots on the lead aim ray
- **Datalink quality** for DL (mesh range, LoS degrade, jam break); SATCOM stays full link
- **Missile picker** in fullscreen (`L`)
- **AI loadouts** can equip RC clones (bots do not remote-pilot — Seek runs normally)
- RC munitions cost **+10%** vs stock
- Auto-exit MissileCamera fullscreen when your tracked ownship is destroyed
- By default, **only official RC clones** are remote-controllable (third-party munitions ignored). Optional `AllowAnyMunition` unlocks any allied LocalSim munition

### Whitelist

| Role | RC name | Stock base |
|------|---------|------------|
| DL Jet | ALM-D500 | ALM-C450 |
| DL Jet | AGM-98D | AGM-99 |
| DL Jet | AGM-68D | AGM-68 (`AGM_heavy*`) |
| DL Jet | AAM-46 Longstrong | AAM-36 |
| DL Jet | DLhM-300S | AShM-300 |
| DL Jet | 76mm DLG Shell | 76mm Guided |
| DL Solid | Tusko-D | Tusko-B |
| SATCOM Jet | ALND-4S | ALND-4 |
| SATCOM Solid | Piledriver TBM-S | Piledriver TBM |

> [!NOTE]
> AGM-68D is cloned only from **AGM-68** racks (`AGM_heavy*`), never AGM-48 (`AGM1*`). AAM-46 Longstrong only from **AAM-36**, never AAM-29.

---

## Install

1. Install **BepInEx 5** and **[MissileCamera](https://github.com/Mursisru/MissileCamera)**.
2. Download `MissileCameraRC-2-0-0.zip` from [Releases](https://github.com/Mursisru/MissileCamera-Remote-Control/releases) (**2.0.0**).
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
4. Aim with your configured input mode; manage throttle / afterburner with the binds below.
5. Optional: **`P`** toggles formation follow for other allied RC missiles.

> [!TIP]
> World-space aim: the reticle is a command point in the world. Turning the seeker slides the marker on the FLIR — fly the nose onto the marker, War Thunder style.

> [!NOTE]
> While you hold RC, proximity fly-by airburst is disabled so CPA near-misses do not detonate. Impact fuse still works. On release, seeker handoff resumes autonomous guidance toward the RC-chosen target.

> [!TIP]
> Set `General.AllowAnyMunition = true` only if you want to remote-pilot non-RC / third-party munitions. Default is **off** (official clones only).

---

## Default keybinds

| Action | Default |
|--------|---------|
| Take / release RC | `T` |
| Open RC missile list | `L` |
| Formation follow | `P` |
| Afterburner (hold) | Left Shift |
| Throttle up | Right Shift |
| Throttle down | Right Ctrl |
| Aim | `AimInputMode`: Mouse / WASD / Arrows / NumPadArrows / Custom |

`AimInputMode` in config: `Mouse` (default) / `WASD` / `Arrows` / `NumPadArrows` / `Custom`.  
`Custom` uses the `CustomAim` keybinds (`AimYawLeft/Right`, `AimPitchUp/Down`).

Useful config toggles:

| Key | Default | Meaning |
|-----|---------|---------|
| `General.AllowAnyMunition` | `false` | Remote-control any allied LocalSim munition |
| `General.AiEquipRcClones` | `true` | AI loadouts swap stock → RC clones |
| `Control.AutoFormationFollow` | `false` | Auto-engage formation on Take |
| `Control.PhysicalAimEnabled` | `true` | Whether in-game mouse/key input steers the missile |
| `Updates.CheckForUpdates` | `true` | Compare `AppVersion` to latest full GitHub release |
| `Updates.DontShowAgain` | `false` | Suppress outdated-version prompt (set by in-game checkbox) |

All binds and options are in the BepInEx config file.

---

## Branches

| Branch | Purpose |
|--------|---------|
| `main` | Stable releases |
| `dev` | Ongoing development / pre-releases |

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

## NOXMFD integration

Remote Control works through MissileCamera's seeker feed. Besides cockpit fullscreen (`K`), you can pilot from **[NOXMFD](https://github.com/roke77/NOXMFD)** via the companion extension **[NOXMFD: RC Missile Camera](https://github.com/Mursisru/NOXMFD-Extension-Remote-Control-Missile-Camera)**.

> [!NOTE]
> **Install order:** BepInEx 5 → MissileCamera → **this plugin** → NOXMFD → `NOXMFD.RcMissileCamera.dll`.

When the extension's **MISSILE CAMERA** page is open, `McBridge.RequestCapture(true)` keeps the feed live headlessly. `McRcBridge` treats bridge capture like fullscreen for control eligibility (`IsControlAllowed`), so **TAKE / aim / throttle / formation / detonate** work from the browser without pressing `K`.

| Surface | In cockpit FS (`K`) | NOXMFD extension page |
| :--- | :--- | :--- |
| Take / release | `T` | TAKE / RELEASE buttons |
| Afterburner | Hold Left Shift | **AB** click-toggle |
| Aim | Mouse / WASD / etc. | Drag on feed surface |
| Manual detonate | Hold Space ~600 ms | Hold DETONATE ~600 ms |

Bridge-side performance tuning (render FPS, JPEG size, marker labels, suppress duplicate cockpit MFD) lives in MissileCamera's **`[MissileCameraBridge]`** config — see the [MissileCamera README](https://github.com/Mursisru/MissileCamera#missilecamerabridge).

---

## Credits

<p align="center">
  <a href="https://github.com/Mursisru"><img src="https://github.com/Mursisru.png" width="80" height="80" alt="Mursisru"/><br/><sub><b>Mursisru</b></sub></a>
  &nbsp;&nbsp;&nbsp;
  <a href="https://github.com/roke77"><img src="https://github.com/roke77.png" width="80" height="80" alt="roke77"/><br/><sub><b>roke77</b></sub></a>
  &nbsp;&nbsp;&nbsp;
  <a href="https://github.com/lupfine"><img src="https://github.com/lupfine.png" width="80" height="80" alt="lupfine"/><br/><sub><b>lupfine</b></sub></a>
</p>

- **[Mursisru](https://github.com/Mursisru)** — MissileCamera: Remote Control author, maintenance, and releases
- **[roke77](https://github.com/roke77)** — [NOXMFD](https://github.com/roke77/NOXMFD) (browser MFD host for the RC Missile Camera page)
- **[lupfine](https://github.com/lupfine)** — original remote-camera / Bridge integration concept

---

## Licence

MIT — see [LICENSE](LICENSE).
