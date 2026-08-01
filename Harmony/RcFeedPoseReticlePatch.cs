using System;
using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// MC feed SyncPose runs at end-of-frame; Update/LateUpdate reticle was one pose behind
    /// the FLIR image → circle drifted off the true aim direction vs center marker.
    /// </summary>
    internal static class RcFeedPoseReticlePatch
    {
        internal static void TryPatch(Harmony harmony, BepInEx.Logging.ManualLogSource? log)
        {
            try
            {
                Assembly? mc = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "MissileCamera")
                    {
                        mc = asm;
                        break;
                    }
                }

                if (mc == null)
                {
                    log?.LogWarning("MissileCamera assembly missing — reticle pose sync skipped.");
                    return;
                }

                Type? rig = mc.GetType("MissileCamera.MissileCameraRig");
                MethodInfo? sync = rig?.GetMethod(
                    "SyncPose",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sync == null)
                {
                    log?.LogWarning("MissileCameraRig.SyncPose not found — reticle pose sync skipped.");
                    return;
                }

                harmony.Patch(
                    sync,
                    postfix: new HarmonyMethod(typeof(RcFeedPoseReticlePatch), nameof(Postfix)));
                log?.LogInfo("MissileCameraRig.SyncPose → RC reticle project patched.");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"RC reticle pose patch failed: {ex.Message}");
            }
        }

        private static void Postfix()
        {
            try
            {
                MouseGuidanceController.LateProject();
            }
            catch
            {
                // ignore
            }
        }
    }
}
