using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Before Steering: only suppress cruise terminal/guidance.
    /// Aim is written solely in MouseGuidance Update (no Fixed reinforce — dual SetAimpoint caused frequent jerks).
    /// </summary>
    internal static class RcSteeringUprightPatch
    {
        internal static void TryPatch(Harmony harmony, BepInEx.Logging.ManualLogSource? log)
        {
            try
            {
                MethodInfo? steering = typeof(Missile).GetMethod(
                    "Steering",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (steering == null)
                {
                    log?.LogWarning("Missile.Steering not found — seeker suppress hook skipped.");
                    return;
                }

                harmony.Patch(
                    steering,
                    prefix: new HarmonyMethod(typeof(RcSteeringUprightPatch), nameof(Prefix)),
                    postfix: new HarmonyMethod(typeof(RcSteeringUprightPatch), nameof(Postfix)));
                log?.LogInfo("Missile.Steering RC suppress patched.");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"Steering patch failed: {ex.Message}");
            }
        }

        private static void Prefix(Missile __instance)
        {
            if (__instance == null)
                return;
            if (!RemoteControlSession.OwnsMissile(__instance))
                return;
            RcSeekerSuppress.Tick(__instance);
        }

        private static void Postfix(Missile __instance)
        {
            RcUprightAssist.AfterSteering(__instance);
        }
    }
}
