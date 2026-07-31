using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// MissileCamera double-lerps THR (~2.5 Hz) — looks like throttle keeps drifting after key release.
    /// While RC is active, snap FLIR throttle display to our UiThrottle every frame.
    /// </summary>
    internal static class RcMissileCameraThrSnap
    {
        private static FieldInfo? _displayThrottleField;
        private static FieldInfo? _displayReadyField;
        private static FieldInfo? _lastThrottleField;

        internal static void TryPatch(Harmony harmony, ManualLogSource? log)
        {
            try
            {
                Assembly? mc = FindMissileCameraAssembly();
                if (mc == null)
                {
                    log?.LogWarning("MissileCamera assembly not found — THR snap skipped.");
                    return;
                }

                Type? bars = mc.GetType("MissileCamera.MissileCameraFlirGaugeBars", throwOnError: false);
                Type? snap = mc.GetType("MissileCamera.MissileCameraHudSnapshot", throwOnError: false);
                if (bars == null)
                {
                    log?.LogWarning("FlirGaugeBars type missing — THR snap skipped.");
                    return;
                }

                _displayThrottleField = bars.GetField("_displayThrottle", BindingFlags.Instance | BindingFlags.NonPublic);
                _displayReadyField = bars.GetField("_displayReady", BindingFlags.Instance | BindingFlags.NonPublic);
                _lastThrottleField = bars.GetField("_lastThrottle", BindingFlags.Instance | BindingFlags.NonPublic);

                MethodInfo? update = bars.GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    snap != null ? new[] { snap, mc.GetType("MissileCamera.MissileCameraPanelMetrics")! } : Type.EmptyTypes,
                    null);

                // Fallback: any Update with 2 params
                if (update == null)
                {
                    foreach (MethodInfo m in bars.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (m.Name != "Update")
                            continue;
                        ParameterInfo[] p = m.GetParameters();
                        if (p.Length == 2)
                        {
                            update = m;
                            break;
                        }
                    }
                }

                if (update == null || _displayThrottleField == null)
                {
                    log?.LogWarning("FlirGaugeBars.Update not patchable — THR snap skipped.");
                    return;
                }

                harmony.Patch(
                    update,
                    prefix: new HarmonyMethod(typeof(RcMissileCameraThrSnap), nameof(FlirUpdatePrefix)));

                if (snap != null)
                {
                    MethodInfo? smooth = snap.GetMethod(
                        "Smooth",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    if (smooth != null)
                    {
                        harmony.Patch(
                            smooth,
                            postfix: new HarmonyMethod(typeof(RcMissileCameraThrSnap), nameof(SmoothPostfix)));
                    }
                }

                log?.LogInfo("MissileCamera THR snap patched (RC session).");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"THR snap patch failed: {ex.Message}");
            }
        }

        private static Assembly? FindMissileCameraAssembly()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (a.GetName().Name == "MissileCamera")
                        return a;
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }

        private static void FlirUpdatePrefix(object __instance)
        {
            try
            {
                if (!RemoteControlSession.IsActive || _displayThrottleField == null)
                    return;

                float t = ThrottleController.UiThrottle;
                _displayThrottleField.SetValue(__instance, t);
                _displayReadyField?.SetValue(__instance, true);
                // Force SetFill to refresh next compare
                _lastThrottleField?.SetValue(__instance, -1f);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>After MC smooths throttle, restore raw RC command so target == display.</summary>
        private static void SmoothPostfix(ref float throttle)
        {
            try
            {
                if (!RemoteControlSession.IsActive)
                    return;
                throttle = ThrottleController.UiThrottle;
            }
            catch
            {
                // ignore
            }
        }
    }
}
