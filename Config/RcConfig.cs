using BepInEx.Configuration;
using UnityEngine;

namespace MissileCameraRemoteControl.Config
{
    internal static class RcConfig
    {
        internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
        internal static ConfigEntry<float> MouseSensitivity { get; private set; } = null!;
        internal static ConfigEntry<float> AimDistance { get; private set; } = null!;
        internal static ConfigEntry<float> JetBoostThrottle { get; private set; } = null!;
        internal static ConfigEntry<float> JetBoostBurnMult { get; private set; } = null!;
        internal static ConfigEntry<float> SolidBoostThrottle { get; private set; } = null!;
        internal static ConfigEntry<float> SolidBoostBurnMult { get; private set; } = null!;
        internal static ConfigEntry<float> ThrottleStep { get; private set; } = null!;

        // P2 datalink mesh / jam
        internal static ConfigEntry<float> MeshRangeM { get; private set; } = null!;
        internal static ConfigEntry<float> JamRangeM { get; private set; } = null!;
        internal static ConfigEntry<float> JamBreakSeconds { get; private set; } = null!;
        internal static ConfigEntry<float> JamEcmThreshold { get; private set; } = null!;
        internal static ConfigEntry<float> ServerPresenceTimeout { get; private set; } = null!;

        // Fresh entry NAMES so stale Equals/Minus/R from old cfg cannot stick.
        internal static ConfigEntry<KeyboardShortcut> ToggleControl { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> OpenMissileList { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleUp { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleDown { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Boost { get; private set; } = null!;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Master enable for Remote Control.");
            MouseSensitivity = config.Bind("Control", "MouseSensitivity", 0.08f,
                "Mouse aim rate for world-space WT reticle (lower = smoother).");
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

            RcPlugin.ModLogger?.LogInfo(
                $"RC binds: Take={ToggleControl.Value} List={OpenMissileList.Value} Thr+={ThrottleUp.Value} Thr-={ThrottleDown.Value} AB={Boost.Value}");
        }
    }
}
