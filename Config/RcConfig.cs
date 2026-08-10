using BepInEx.Configuration;
using UnityEngine;

namespace MissileCameraRemoteControl.Config
{
    internal static class RcConfig
    {
        internal static bool IsBound { get; private set; }

        internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
        internal static ConfigEntry<bool> AiEquipRcClones { get; private set; } = null!;
        /// <summary>When false (default): only official RC clones. When true: any allied LocalSim munition.</summary>
        internal static ConfigEntry<bool> AllowAnyMunition { get; private set; } = null!;
        internal static ConfigEntry<bool> AamProximityDetonate { get; private set; } = null!;
        internal static ConfigEntry<float> AamProxHorizM { get; private set; } = null!;
        internal static ConfigEntry<float> AamProxVertM { get; private set; } = null!;
        internal static ConfigEntry<float> AamProxMaxRangeM { get; private set; } = null!;
        internal static ConfigEntry<float> MouseSensitivity { get; private set; } = null!;
        internal static ConfigEntry<float> KeyAimSensitivity { get; private set; } = null!;
        internal static ConfigEntry<float> AimDistance { get; private set; } = null!;
        internal static ConfigEntry<float> AimLagSeconds { get; private set; } = null!;
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

        internal static ConfigEntry<bool> CheckForUpdates { get; private set; } = null!;
        internal static ConfigEntry<bool> UpdatePromptDontShowAgain { get; private set; } = null!;

        internal static ConfigEntry<KeyboardShortcut> ToggleControl { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> OpenMissileList { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleUp { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleDown { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Boost { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimYawLeft { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimYawRight { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimPitchUp { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> AimPitchDown { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> FormationFollow { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ManualDetonate { get; private set; } = null!;
        internal static ConfigEntry<bool> AutoFormationFollow { get; private set; } = null!;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Master enable for Remote Control.");
            AiEquipRcClones = config.Bind("General", "AiEquipRcClones", true,
                "AI aircraft equip RC (DL/SATCOM) clones instead of vanilla whitelist mounts. Bots do not remote-pilot — Seek runs normally.");
            AllowAnyMunition = config.Bind("General", "AllowAnyMunition", false,
                "If false (default): only official MissileCamera RC clones can be remote-controlled. If true: any allied LocalSim munition (including other mods).");

            CheckForUpdates = config.Bind("Updates", "CheckForUpdates", true,
                "Check GitHub for a newer full release (not pre-release) on launch. Offline = silent.");
            UpdatePromptDontShowAgain = config.Bind("Updates", "DontShowAgain", false,
                "If true, never show the outdated-version prompt (set by the in-game checkbox).");

            AamProximityDetonate = config.Bind("Control", "AamProximityDetonate", true,
                "Under RC: auto-detonate AAM-46 (and other AAM when AllowAnyMunition) near locked target (horizontal + vertical miss gates).");
            AamProxHorizM = config.Bind("Control", "AamProxHorizM", 14f,
                "Max horizontal-plane miss (meters) from missile to target for AAM proximity burst.");
            AamProxVertM = config.Bind("Control", "AamProxVertM", 14f,
                "Max vertical separation (meters) for AAM proximity burst.");
            AamProxMaxRangeM = config.Bind("Control", "AamProxMaxRangeM", 55f,
                "Max slant range (meters) to evaluate AAM proximity burst.");

            AimInputMode = config.Bind("Control", "AimInputMode", RcAimInputMode.Mouse,
                "Mouse | WASD | Arrows | NumPadArrows | Custom. Custom uses AimYaw/Pitch binds below.");
            MouseSensitivity = config.Bind("Control", "MouseSensitivity", 0.08f,
                "Mouse aim rate for world-space WT reticle (lower = smoother).");
            KeyAimSensitivity = config.Bind("Control", "KeyAimSensitivity", 1f,
                "Keyboard/numpad aim rate multiplier (degrees/sec scale).");
            AimDistance = config.Bind("Control", "AimDistance", 4000f,
                "World aim / command point distance (meters).");
            AimLagSeconds = config.Bind("Control", "AimLagSeconds", 0.3f,
                "Extra ease on nose turn rate (0 = stock gLimit only). Nose always reaches the direction marker.");

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
            FormationFollow = config.Bind("Keybinds", "FormationFollow", new KeyboardShortcut(KeyCode.P),
                "Toggle formation: other allied RC missiles follow the controlled lead.");
            ManualDetonate = config.Bind("Keybinds", "ManualDetonate", new KeyboardShortcut(KeyCode.Space),
                "While FS+RC: instant vanilla Detonate (Space eaten so the game never sees it). Nuclear: near surface only.");
            AutoFormationFollow = config.Bind("Control", "AutoFormationFollow", false,
                "If true, engage formation follow automatically when taking RC (P still toggles).");

            // Used only when AimInputMode = Custom.
            AimYawLeft = config.Bind("CustomAim", "AimYawLeft", new KeyboardShortcut(KeyCode.A),
                "Custom mode: yaw aim left (hold).");
            AimYawRight = config.Bind("CustomAim", "AimYawRight", new KeyboardShortcut(KeyCode.D),
                "Custom mode: yaw aim right (hold).");
            AimPitchUp = config.Bind("CustomAim", "AimPitchUp", new KeyboardShortcut(KeyCode.W),
                "Custom mode: pitch aim up (hold).");
            AimPitchDown = config.Bind("CustomAim", "AimPitchDown", new KeyboardShortcut(KeyCode.S),
                "Custom mode: pitch aim down (hold).");

            IsBound = true;

            RcPlugin.ModLogger?.LogInfo(
                $"RC binds: Take={ToggleControl.Value} List={OpenMissileList.Value} Form={FormationFollow.Value} AimMode={AimInputMode.Value} " +
                $"CustomAim=[{AimYawLeft.Value}/{AimYawRight.Value}/{AimPitchUp.Value}/{AimPitchDown.Value}] " +
                $"Thr+={ThrottleUp.Value} Thr-={ThrottleDown.Value} AB={Boost.Value}");
        }
    }
}
