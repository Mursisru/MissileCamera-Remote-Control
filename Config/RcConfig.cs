using BepInEx.Configuration;
using UnityEngine;

namespace MissileCameraRemoteControl.Config
{
    internal static class RcConfig
    {
        internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
        internal static ConfigEntry<bool> AiEquipRcClones { get; private set; } = null!;
        internal static ConfigEntry<float> MouseSensitivity { get; private set; } = null!;
        internal static ConfigEntry<float> KeyAimSensitivity { get; private set; } = null!;
        internal static ConfigEntry<float> AimDistance { get; private set; } = null!;
        internal static ConfigEntry<RcAimInputMode> AimInputMode { get; private set; } = null!;
        internal static ConfigEntry<float> JetBoostThrottle { get; private set; } = null!;
        internal static ConfigEntry<float> JetBoostBurnMult { get; private set; } = null!;
        internal static ConfigEntry<float> SolidBoostThrottle { get; private set; } = null!;
        internal static ConfigEntry<float> SolidBoostBurnMult { get; private set; } = null!;
        internal static ConfigEntry<float> ThrottleStep { get; private set; } = null!;

        internal static ConfigEntry<float> MeshRangeM { get; private set; } = null!;
        internal static ConfigEntry<float> JamRangeM { get; private set; } = null!;
        internal static ConfigEntry<float> JamBreakSeconds { get; private set; } = null!;
        internal static ConfigEntry<float> JamEcmThreshold { get; private set; } = null!;
        internal static ConfigEntry<float> ServerPresenceTimeout { get; private set; } = null!;

        internal static ConfigEntry<KeyboardShortcut> ToggleControl { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> OpenMissileList { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleUp { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleDown { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Boost { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimYawLeft { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimYawRight { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimPitchUp { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimPitchDown { get; private set; } = null!;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Master enable for Remote Control.");
            AiEquipRcClones = config.Bind("General", "AiEquipRcClones", true,
                "AI aircraft equip RC (DL/SATCOM) clones instead of vanilla whitelist mounts. Bots do not remote-pilot — Seek runs normally.");

            AimInputMode = config.Bind("Control", "AimInputMode", RcAimInputMode.Both,
                "Mouse = mouse only; Keys = remappable keys only; Both = mouse + keys (WASD/arrows/numpad via binds below).");
            MouseSensitivity = config.Bind("Control", "MouseSensitivity", 0.08f,
                "Mouse aim rate for world-space WT reticle (lower = smoother).");
            KeyAimSensitivity = config.Bind("Control", "KeyAimSensitivity", 1f,
                "Keyboard/numpad aim rate multiplier (degrees/sec scale).");
            AimDistance = config.Bind("Control", "AimDistance", 4000f,
                "World aim / command point distance (meters).");

            JetBoostThrottle = config.Bind("Throttle", "JetBoostThrottle", 1.5f, "Jet afterburner throttle multiplier (>1).");
            JetBoostBurnMult = config.Bind("Throttle", "JetBoostBurnMult", 2.5f, "Jet fuel burn multiplier during boost.");
            SolidBoostThrottle = config.Bind("Throttle", "SolidBoostThrottle", 1.5f, "Solid boost thrust multiplier.");
            SolidBoostBurnMult = config.Bind("Throttle", "SolidBoostBurnMult", 2f, "Solid burn rate multiplier during boost.");
            ThrottleStep = config.Bind("Throttle", "ThrottleStep", 0.05f, "Throttle step on tap; hold ramps faster.");

            MeshRangeM = config.Bind("Datalink", "MeshRangeM", 150000f,
                "DL mesh relay range (meters). Ally within range required for RC.");
            JamRangeM = config.Bind("Datalink", "JamRangeM", 25000f,
                "Enemy ECM aircraft radius that counts as jamming the DL (meters).");
            JamBreakSeconds = config.Bind("Datalink", "JamBreakSeconds", 5f,
                "Continuous jam time before DL link is lost (seconds).");
            JamEcmThreshold = config.Bind("Datalink", "JamEcmThreshold", 0.5f,
                "Minimum enemy GetECMIntensity to count as jamming.");

            ServerPresenceTimeout = config.Bind("Network", "ServerPresenceTimeout", 4f,
                "Seconds to wait for server Remote Control presence reply before disabling RC on this client.");

            ToggleControl = config.Bind("Keybinds", "TakeControl", new KeyboardShortcut(KeyCode.T),
                "Take / release RC (requires MissileCamera Fullscreen).");
            OpenMissileList = config.Bind("Keybinds", "OpenMissileList", new KeyboardShortcut(KeyCode.L),
                "Open allied RC missile picker (Fullscreen).");
            ThrottleUp = config.Bind("Keybinds", "ThrottleIncrease", new KeyboardShortcut(KeyCode.RightShift),
                "Increase throttle (hold to ramp).");
            ThrottleDown = config.Bind("Keybinds", "ThrottleDecrease", new KeyboardShortcut(KeyCode.RightControl),
                "Decrease throttle (hold to ramp).");
            Boost = config.Bind("Keybinds", "Afterburner", new KeyboardShortcut(KeyCode.LeftShift),
                "Hold afterburner / turbo-boost.");

            // Defaults: arrows. Rebind to WASD / numpad / anything in the cfg (BepInEx KeyboardShortcut).
            AimYawLeft = config.Bind("Keybinds", "AimYawLeft", new KeyboardShortcut(KeyCode.LeftArrow),
                "Yaw aim left (hold). Example WASD: A; numpad: Keypad4.");
            AimYawRight = config.Bind("Keybinds", "AimYawRight", new KeyboardShortcut(KeyCode.RightArrow),
                "Yaw aim right (hold). Example WASD: D; numpad: Keypad6.");
            AimPitchUp = config.Bind("Keybinds", "AimPitchUp", new KeyboardShortcut(KeyCode.UpArrow),
                "Pitch aim up (hold). Example WASD: W; numpad: Keypad8.");
            AimPitchDown = config.Bind("Keybinds", "AimPitchDown", new KeyboardShortcut(KeyCode.DownArrow),
                "Pitch aim down (hold). Example WASD: S; numpad: Keypad5 or Keypad2.");

            RcPlugin.ModLogger?.LogInfo(
                $"RC binds: Take={ToggleControl.Value} List={OpenMissileList.Value} AimMode={AimInputMode.Value} " +
                $"Aim=[{AimYawLeft.Value}/{AimYawRight.Value}/{AimPitchUp.Value}/{AimPitchDown.Value}] " +
                $"Thr+={ThrottleUp.Value} Thr-={ThrottleDown.Value} AB={Boost.Value}");
        }
    }
}
