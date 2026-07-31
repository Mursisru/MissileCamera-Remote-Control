using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>After vanilla Steering, RC adds roll-to-upright when the missile is inverted.</summary>
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
                    log?.LogWarning("Missile.Steering not found — upright assist disabled.");
                    return;
                }

                harmony.Patch(
                    steering,
                    postfix: new HarmonyMethod(typeof(RcSteeringUprightPatch), nameof(Postfix)));
                log?.LogInfo("Missile.Steering upright assist patched.");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"Steering upright patch failed: {ex.Message}");
            }
        }

        private static void Postfix(Missile __instance)
        {
            RcUprightAssist.AfterSteering(__instance);
        }
    }
}
