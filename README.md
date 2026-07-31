# MissileCamera: Remote Control

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx 5](https://img.shields.io/badge/Loader-BepInEx%205-orange)](https://docs.bepinex.dev/)
[![Version](https://img.shields.io/badge/Version-0.0.0-green)](https://github.com/Mursisru/MissileCamera-Remote-Control)
[![Requires MissileCamera](https://img.shields.io/badge/Requires-MissileCamera-lightgrey)](https://github.com/Mursisru/MissileCamera)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

BepInEx 5 addon for **Nuclear Option** that extends [MissileCamera](https://github.com/Mursisru/MissileCamera) with remote-control clone munitions (`[DL]` / `[SATCOM]`), mouse guidance, and throttle / afterburner control.

**Plugin GUID:** `com.at747.missilecamera.remotecontrol`

---

## Critical warnings

> [!IMPORTANT]
> **Requires MissileCamera** (`com.at747.missilecamera.bepinex`) and **BepInEx 5 (x64)**.

> [!WARNING]
> Remote control is **host / single-player only** (`LocalSim`). Pure clients cannot steer missiles.

> [!NOTE]
> Vanilla mounts are never mutated. Clones are separate loadout options injected only onto hardpoints that already allow the original weapon.

---

## Features (P0–P1)

- Selective clones of cruise / heavy munitions with **[DL]** or **[SATCOM]** labels
- Hardpoint-compatible loadout injection
- Manual mouse aim via vanilla `SetAimpoint` (stock aero / Over-G retained)
- Jet / solid throttle, afterburner, afterburner VFX reuse
- Encyclopedia missile entries for RC variants

---

## Requirements

- Nuclear Option
- BepInEx 5
- [MissileCamera](https://github.com/Mursisru/MissileCamera) installed

---

## Player installation

1. Install BepInEx 5 and MissileCamera.
2. Copy `MissileCameraRemoteControl.dll` into `BepInEx/plugins/`.
3. Launch the game once to generate config.

---

## Default keybinds

| Action | Default |
|--------|---------|
| Take / release RC | `T` |
| Afterburner (hold) | Left Shift |
| Throttle up | Right Shift |
| Throttle down | Right Ctrl |
| Aim | Mouse |

Configure under `com.at747.missilecamera.remotecontrol.cfg`.

---

## Build

```bash
dotnet build MissileCameraRemoteControl.csproj -c Release
```

Set `NuclearOptionRoot` via `Directory.Build.props` (not committed) to your game install path.

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

---

## Licence

MIT — see [LICENSE](LICENSE).
