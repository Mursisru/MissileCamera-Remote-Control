using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// ServerFixedUpdate order: Seek() then Steering().
    /// Prefix on Steering runs after Seek — restore RC / formation aim so GSN cannot keep the stick.
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
                    prefix: new HarmonyMethod(typeof(RcSteeringUprightPatch), nameof(Prefix)));
                log?.LogInfo("Missile.Steering RC aim reinforce + suppress patched.");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"Steering patch failed: {ex.Message}");
            }
        }

        private static void Prefix(Missile __instance)
        {
            // Hot path: no RC / formation → exit; world missiles never hit IsFollower O(n).
            if (__instance == null || !RcSeekSkipSet.HasAny)
                return;

            try
            {
                if (RemoteControlSession.OwnsMissile(__instance))
                {
                    RcSeekerSuppress.Tick(__instance);
                    MouseGuidanceController.ReinforceAimpoint(__instance);
                    return;
                }

                if (RcFormationFollow.IsFollower(__instance))
                {
                    RcSeekerSuppress.Tick(__instance);
                    RcFormationFollow.ReinforceAimpoint(__instance);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
