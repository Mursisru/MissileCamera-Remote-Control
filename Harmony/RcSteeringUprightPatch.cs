using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Control;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// ServerFixedUpdate: Seek() then Steering().
    /// Prefix: restore RC aim + zero uprightPreference (stock roll-to-horizon yanks hard from aircraft bank).
    /// Postfix: restore uprightPreference.
    /// </summary>
    internal static class RcSteeringUprightPatch
    {
        private static readonly FieldInfo? UprightField =
            AccessTools.Field(typeof(Missile), "uprightPreference");

        private static float _savedUpright;
        private static bool _suppressedUpright;

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
                log?.LogInfo("Missile.Steering RC aim reinforce + upright suppress patched.");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"Steering patch failed: {ex.Message}");
            }
        }

        private static void Prefix(Missile __instance)
        {
            _suppressedUpright = false;
            if (__instance == null || !RcSeekSkipSet.HasAny)
                return;

            try
            {
                if (RemoteControlSession.OwnsMissile(__instance))
                {
                    RcSeekerSuppress.Tick(__instance);
                    MouseGuidanceController.ReinforceAimpoint(__instance);
                    SuppressUpright(__instance);
                    return;
                }

                if (RcFormationFollow.IsFollower(__instance))
                {
                    RcSeekerSuppress.Tick(__instance);
                    RcFormationFollow.ReinforceAimpoint(__instance);
                    SuppressUpright(__instance);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void Postfix(Missile __instance)
        {
            if (!_suppressedUpright || __instance == null || UprightField == null)
                return;

            try
            {
                UprightField.SetValue(__instance, _savedUpright);
            }
            catch
            {
                // ignore
            }
            finally
            {
                _suppressedUpright = false;
            }
        }

        private static void SuppressUpright(Missile missile)
        {
            if (UprightField == null)
                return;

            try
            {
                _savedUpright = (float)UprightField.GetValue(missile)!;
                if (_savedUpright <= 0f)
                    return;
                UprightField.SetValue(missile, 0f);
                _suppressedUpright = true;
            }
            catch
            {
                _suppressedUpright = false;
            }
        }
    }
}
