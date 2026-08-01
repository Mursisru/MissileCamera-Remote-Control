using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// MC Fullscreen.Toggle: if spectator already engaged via RC K poll, skip Toggle body
    /// (prevents Enter→immediate Exit). Else try engage on Toggle path.
    /// </summary>
    internal static class RcMcFullscreenTogglePatch
    {
        private static bool _patched;

        internal static void TryPatch(Harmony harmony, BepInEx.Logging.ManualLogSource? log)
        {
            if (_patched)
                return;

            try
            {
                // Force reflect attempt (MC may load after plugin Awake).
                if (!MissileCameraFsAccess.IsReady)
                {
                    log?.LogInfo("MC Toggle patch deferred — MissileCamera not ready yet.");
                    return;
                }

                MethodInfo? toggle = MissileCameraFsAccess.ResolveFullscreenToggleMethod();
                if (toggle == null)
                {
                    log?.LogWarning("MC Fullscreen Toggle not found.");
                    return;
                }

                harmony.Patch(
                    toggle,
                    prefix: new HarmonyMethod(typeof(RcMcFullscreenTogglePatch), nameof(Prefix)));
                _patched = true;
                log?.LogInfo("MC Fullscreen Toggle patched for spectator RC.");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"MC Toggle patch failed: {ex.Message}");
            }
        }

        /// <summary>Return false = skip original Toggle.</summary>
        private static bool Prefix()
        {
            try
            {
                if (RcSpectatorEngage.ShouldSkipMcToggle())
                    return false;

                // Toggle pressed via MC input path without our Tick seeing K first.
                if (!MissileCameraFsAccess.IsFullscreenActive
                    && RcSpectatorEngage.TryPrepareAndEnterViaTogglePath())
                    return false;
            }
            catch
            {
                // ignore
            }

            return true;
        }
    }
}
