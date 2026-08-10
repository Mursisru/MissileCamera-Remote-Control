using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Hard gLimit: after ApplyAero, clamp pitch/yaw angular rate so G never exceeds stock gLimit.
    /// Vanilla torque clamp can still overshoot for one tick when aimPoint jumps.
    /// </summary>
    internal static class RcGLimitEnforce
    {
        internal static void TryPatch(Harmony harmony, BepInEx.Logging.ManualLogSource? log)
        {
            try
            {
                MethodInfo? aero = typeof(Missile).GetMethod(
                    "ApplyAero",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (aero == null)
                {
                    log?.LogWarning("Missile.ApplyAero not found — hard G clamp skipped.");
                    return;
                }

                harmony.Patch(
                    aero,
                    postfix: new HarmonyMethod(typeof(RcGLimitEnforce), nameof(Postfix)));
                log?.LogInfo("Missile.ApplyAero hard gLimit clamp patched.");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"gLimit patch failed: {ex.Message}");
            }
        }

        private static void Postfix(Missile __instance)
        {
            if (__instance == null || __instance.disabled || __instance.rb == null)
                return;
            if (!RcSeekSkipSet.HasAny)
                return;
            if (!RemoteControlSession.OwnsMissile(__instance)
                && !RcFormationFollow.IsFollower(__instance))
                return;

            try
            {
                float omegaMax = MissileAccess.GetMaxTurnRateRad(__instance);
                if (omegaMax <= 1e-5f)
                    return;

                Transform t = __instance.transform;
                Vector3 local = t.InverseTransformVector(__instance.rb.angularVelocity);
                float xy = Mathf.Sqrt(local.x * local.x + local.y * local.y);
                if (xy <= omegaMax)
                    return;

                float s = omegaMax / xy;
                local.x *= s;
                local.y *= s;
                __instance.rb.angularVelocity = t.TransformVector(local);
            }
            catch
            {
                // ignore
            }
        }
    }
}
