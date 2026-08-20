using BepInEx.Configuration;

namespace MissileCameraRemoteControl.Config
{
    // Partial-class extension of Config/RcConfig.cs — the external-consumer half lives here.
    // Bind() in the other file calls BindExtras(config) once, right where PhysicalAimEnabled
    // would otherwise have been bound inline.
    internal static partial class RcConfig
    {
        // Gates Tick()'s physical aim polling (PollMouse or PollKeyScheme, whichever AimInputMode
        // selects) — independent of external aim (McRcBridge.InjectAimDelta, e.g. NOXMFD's
        // browser MFD), which stays available regardless of this setting. Doesn't touch throttle/
        // boost/detonate physical keybinds — those aren't what fights the browser's own drag-to-aim,
        // so they're left as-is. Defaults ON (unchanged prior behavior); turn off in the config file
        // if you want the MFD to always work without your own mouse motion also steering whatever
        // you TAKE from it.
        internal static ConfigEntry<bool> PhysicalAimEnabled { get; private set; } = null!;

        internal static void BindExtras(ConfigFile config)
        {
            PhysicalAimEnabled = config.Bind("Control", "PhysicalAimEnabled", true,
                "Whether in-game mouse/key input (per AimInputMode above) steers the controlled missile. " +
                "Turn OFF if you're flying via an external MFD (e.g. NOXMFD's browser page) and don't want " +
                "your own mouse movement also fighting its drag-to-aim — the MFD keeps working either way.");
        }
    }
}
