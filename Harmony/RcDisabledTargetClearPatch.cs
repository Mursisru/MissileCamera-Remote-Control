using System.Reflection;
using HarmonyLib;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Vanilla SlowChecks detonates on targetUnit == null, not disabled.
    /// Destroyed units leave a disabled ref → cruise/AAM loiter until fuel.
    /// Clear disabled lock so SD paths run (DetonateGate still blocks while OwnsMissile).
    /// </summary>
    internal static class RcDisabledTargetClear
    {
        private static readonly FieldInfo? SeekerTargetField =
            typeof(MissileSeeker).GetField("targetUnit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        internal static void Prefix(MissileSeeker __instance)
        {
            if (__instance == null || SeekerTargetField == null)
                return;
            try
            {
                if (SeekerTargetField.GetValue(__instance) is Unit u && u != null && u.disabled)
                    SeekerTargetField.SetValue(__instance, null);
            }
            catch
            {
                // ignore
            }
        }
    }

    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "SlowChecks")]
    internal static class RcCruiseSlowChecksDisabledTargetPatch
    {
        private static void Prefix(OpticalSeekerCruiseMissile __instance) =>
            RcDisabledTargetClear.Prefix(__instance);
    }

    [HarmonyPatch(typeof(OpticalSeeker), "SlowChecks")]
    internal static class RcOpticalSlowChecksDisabledTargetPatch
    {
        private static void Prefix(OpticalSeeker __instance) =>
            RcDisabledTargetClear.Prefix(__instance);
    }

    [HarmonyPatch(typeof(ARHSeeker), "SlowChecks")]
    internal static class RcArhSlowChecksDisabledTargetPatch
    {
        private static void Prefix(ARHSeeker __instance) =>
            RcDisabledTargetClear.Prefix(__instance);
    }

    [HarmonyPatch(typeof(SARHSeeker), "SlowChecks")]
    internal static class RcSarhSlowChecksDisabledTargetPatch
    {
        private static void Prefix(SARHSeeker __instance) =>
            RcDisabledTargetClear.Prefix(__instance);
    }

    [HarmonyPatch(typeof(IRSeeker), "SlowChecks")]
    internal static class RcIrSlowChecksDisabledTargetPatch
    {
        private static void Prefix(IRSeeker __instance) =>
            RcDisabledTargetClear.Prefix(__instance);
    }
}
