using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// While MissileCamera FS + RC stick: eat Space (and ManualDetonate bind) so the game
    /// never sees the press. Detection uses <see cref="RawKeyDown"/> passthrough.
    /// </summary>
    internal static class RcSpaceKeyEatPatch
    {
        private static bool _passthrough;

        internal static bool RawKeyDown(KeyCode key)
        {
            _passthrough = true;
            try
            {
                return Input.GetKeyDown(key);
            }
            finally
            {
                _passthrough = false;
            }
        }

        internal static bool RawKey(KeyCode key)
        {
            _passthrough = true;
            try
            {
                return Input.GetKey(key);
            }
            finally
            {
                _passthrough = false;
            }
        }

        private static bool ShouldEat(KeyCode key)
        {
            if (_passthrough || key == KeyCode.None)
                return false;
            if (!MissileCameraFsAccess.IsControlAllowed)
                return false;
            if (!RemoteControlSession.IsActive)
                return false;

            KeyCode manual = RcConfig.ManualDetonate.Value.MainKey;
            if (manual == KeyCode.None)
                manual = KeyCode.Space;
            return key == manual || key == KeyCode.Space;
        }

        internal static void TryPatch(Harmony harmony, BepInEx.Logging.ManualLogSource? log)
        {
            try
            {
                MethodInfo? down = typeof(Input).GetMethod(
                    nameof(Input.GetKeyDown),
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(KeyCode) },
                    null);
                MethodInfo? held = typeof(Input).GetMethod(
                    nameof(Input.GetKey),
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(KeyCode) },
                    null);
                MethodInfo? up = typeof(Input).GetMethod(
                    nameof(Input.GetKeyUp),
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(KeyCode) },
                    null);

                if (down != null)
                    harmony.Patch(down, prefix: new HarmonyMethod(typeof(RcSpaceKeyEatPatch), nameof(PrefixDown)));
                if (held != null)
                    harmony.Patch(held, prefix: new HarmonyMethod(typeof(RcSpaceKeyEatPatch), nameof(PrefixHeld)));
                if (up != null)
                    harmony.Patch(up, prefix: new HarmonyMethod(typeof(RcSpaceKeyEatPatch), nameof(PrefixUp)));

                log?.LogInfo("Input Space/manual-detonate eat patched (FS+RC only).");
            }
            catch (System.Exception ex)
            {
                log?.LogWarning($"Space eat patch failed: {ex.Message}");
            }
        }

        private static bool PrefixDown(KeyCode key, ref bool __result)
        {
            if (!ShouldEat(key))
                return true;
            __result = false;
            return false;
        }

        private static bool PrefixHeld(KeyCode key, ref bool __result)
        {
            if (!ShouldEat(key))
                return true;
            __result = false;
            return false;
        }

        private static bool PrefixUp(KeyCode key, ref bool __result)
        {
            if (!ShouldEat(key))
                return true;
            __result = false;
            return false;
        }
    }
}
