# MissileCamera: Remote Control

[![Nuclear Option](https://img.shields.io/badge/Game-Nuclear%20Option-blue)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx 5](https://img.shields.io/badge/Loader-BepInEx%205-orange)](https://docs.bepinex.dev/)
[![Version](https://img.shields.io/badge/Version-0.0.2-green)](https://github.com/Mursisru/MissileCamera-Remote-Control)
[![Requires MissileCamera](https://img.shields.io/badge/Requires-MissileCamera-lightgrey)](https://github.com/Mursisru/MissileCamera)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

BepInEx 5 addon for **Nuclear Option** that extends [MissileCamera](https://github.com/Mursisru/MissileCamera) with remote-control clone munitions (AGM-98D, ALM-D500, …), mouse guidance, and throttle / afterburner control.

**Plugin GUID:** `com.at747.missilecamera.remotecontrol`

---

## Critical warnings

> [!IMPORTANT]
> **Requires MissileCamera** (`com.at747.missilecamera.bepinex`) and **BepInEx 5 (x64)**.

> [!WARNING]
> Remote control is **host / single-player only** (`LocalSim`). Pure clients cannot steer missiles.

> [!IMPORTANT]
> In multiplayer, if the **server does not run this addon**, Remote Control features are **temporarily disabled** on your client for that session (auto re-enabled when you leave). Hosts / servers with the addon stay enabled.

> [!NOTE]
> Vanilla mounts are never mutated. Clones are separate loadout options injected only onto hardpoints that already allow the original weapon.

---

## Features

- Selective RC clones with unique designations (guidance shown as DL/SATCOM in UI)
- Hardpoint-compatible loadout injection
- Manual mouse aim via vanilla `SetAimpoint` (stock aero / Over-G retained)
- Jet / solid throttle, afterburner, afterburner VFX reuse
- Encyclopedia missile entries for RC variants
- **P2 datalink:** DL mesh 150 km + LoS (weak without LoS; drop if out of range or jammed &gt;5 s); SATCOM always full link
- **P3 picker:** `L` opens allied RC missile list in MissileCamera Fullscreen
- **P3 boost sync:** host broadcasts afterburner state to clients in mod lobbies
- **MP presence:** clients query the server for this addon; no reply → RC disabled for that session only

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
| Open RC missile list | `L` |
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
