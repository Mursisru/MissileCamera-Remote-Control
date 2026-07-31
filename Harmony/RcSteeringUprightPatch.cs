using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Before Steering: reinforce RC aimpoint (after Seek in ServerFixedUpdate).
    /// Upright assist stays inert.
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
                    log?.LogWarning("Missile.Steering not found — aim reinforce disabled.");
                    return;
                }

                harmony.Patch(
                    steering,
                    prefix: new HarmonyMethod(typeof(RcSteeringUprightPatch), nameof(Prefix)),
                    postfix: new HarmonyMethod(typeof(RcSteeringUprightPatch), nameof(Postfix)));
                log?.LogInfo("Missile.Steering RC aim reinforce patched.");
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
            // Same FixedUpdate as Seek: kill terminal/guidance then rewrite aim after any leak.
            RcSeekerSuppress.Tick(__instance);
            MouseGuidanceController.ReinforceAimpoint(__instance);
        }

        private static void Postfix(Missile __instance)
        {
            RcUprightAssist.AfterSteering(__instance);
        }
    }
}
