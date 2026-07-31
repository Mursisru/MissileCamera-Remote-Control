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
        internal static ConfigEntry<float> SolidChokeThrottle { get; private set; } = null!;
        internal static ConfigEntry<float> ThrottleStep { get; private set; } = null!;

        internal static ConfigEntry<KeyboardShortcut> ToggleControl { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> CycleNext { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> CyclePrev { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleUp { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> ThrottleDown { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Boost { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Choke { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Retarget { get; private set; } = null!;
        internal static ConfigEntry<KeyboardShortcut> Airburst { get; private set; } = null!;

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
            SolidChokeThrottle = config.Bind("Throttle", "SolidChokeThrottle", 0.35f, "Solid choke thrust while choke held.");
            ThrottleStep = config.Bind("Throttle", "ThrottleStep", 0.05f, "Jet throttle step per key press.");

            ToggleControl = config.Bind("Keybinds", "ToggleControl", new KeyboardShortcut(KeyCode.R),
                "Take / release RC (requires MissileCamera Fullscreen).");
            CycleNext = config.Bind("Keybinds", "CycleNext", new KeyboardShortcut(KeyCode.RightBracket), "Cycle next controllable missile.");
            CyclePrev = config.Bind("Keybinds", "CyclePrev", new KeyboardShortcut(KeyCode.LeftBracket), "Cycle previous controllable missile.");
            ThrottleUp = config.Bind("Keybinds", "ThrottleUp", new KeyboardShortcut(KeyCode.Equals), "Increase jet throttle.");
            ThrottleDown = config.Bind("Keybinds", "ThrottleDown", new KeyboardShortcut(KeyCode.Minus), "Decrease jet throttle.");
            Boost = config.Bind("Keybinds", "Boost", new KeyboardShortcut(KeyCode.LeftShift), "Hold turbo-boost / afterburner.");
            Choke = config.Bind("Keybinds", "Choke", new KeyboardShortcut(KeyCode.LeftControl), "Hold solid-motor choke.");
            Retarget = config.Bind("Keybinds", "Retarget", new KeyboardShortcut(KeyCode.T), "Lock new target under reticle.");
            Airburst = config.Bind("Keybinds", "Airburst", new KeyboardShortcut(KeyCode.Space), "Manual warhead detonation.");
        }
    }
}
