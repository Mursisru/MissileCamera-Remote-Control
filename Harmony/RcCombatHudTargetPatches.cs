using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>While RC active, stock Select/Cancel are owned by RcRetargetController.</summary>
    [HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
    internal static class RcCombatHudTargetSelectPatch
    {
        private static bool Prefix()
        {
            return !RcRetargetController.BlockVanillaTargetSelect;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DeselectLast))]
    internal static class RcCombatHudDeselectLastPatch
    {
        private static bool Prefix()
        {
            // Click-Cancel: RC clears missile lock in RcRetargetController; skip vanilla last-deselect.
            return !RemoteControlSession.IsActive;
        }
    }

    [HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DeselectAll))]
    internal static class RcCombatHudDeselectAllPatch
    {
        private static void Prefix(ref bool withAudio)
        {
            // Hold-Cancel: allow vanilla clear-all; also clear RC missile target.
            if (!RemoteControlSession.IsActive)
                return;
            Missile? m = RemoteControlSession.Controlled;
            if (m != null)
                RcRetargetController.ClearMissileTarget(m);
        }
    }
}
