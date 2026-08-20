using BepInEx.Configuration;

namespace MissileCameraRemoteControl.Config
{
    internal static partial class RcConfig
    {
        // Gates only the physical aim polling — external aim (McRcBridge.InjectAimDelta) stays
        // available regardless of this setting.
        internal static ConfigEntry<bool> PhysicalAimEnabled { get; private set; } = null!;

        internal static void BindExtras(ConfigFile config)
        {
            PhysicalAimEnabled = config.Bind("Control", "PhysicalAimEnabled", true,
                "Whether in-game mouse/key input (per AimInputMode above) steers the controlled missile. " +
                "Turn OFF if you're flying via an external control surface and don't want your own mouse " +
                "movement fighting its own aim input — the external channel keeps working either way.");
        }
    }
}
