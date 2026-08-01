using System;
using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>
    /// Reflects MissileCamera FS / feed APIs without referencing MC internals.
    /// CAMERA_SAFETY: never writes CameraStateManager.
    /// </summary>
    internal static class MissileCameraFsAccess
    {
        private static bool _resolvedOk;
        private static float _nextResolveAttempt;
        private static PropertyInfo? _fsIsActive;
        private static MethodInfo? _tryGetFeedCamera;

        internal static bool IsFullscreenActive
        {
            get
            {
                EnsureResolved();
                if (_fsIsActive == null)
                    return false;
                try
                {
                    return (bool)_fsIsActive.GetValue(null)!;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static Camera? TryGetFeedCamera()
        {
            EnsureResolved();
            if (_tryGetFeedCamera == null)
                return null;
            try
            {
                return _tryGetFeedCamera.Invoke(null, null) as Camera;
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolvedOk)
                return;
            if (Time.unscaledTime < _nextResolveAttempt)
                return;
            _nextResolveAttempt = Time.unscaledTime + 1f;

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
                    return;

                Type? fs = mc.GetType("MissileCamera.MissileCameraFullscreenController");
                _fsIsActive = fs?.GetProperty("IsActive", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                Type? feed = mc.GetType("MissileCamera.MissileCameraFeedController");
                _tryGetFeedCamera = feed?.GetMethod(
                    "TryGetFeedCamera",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);

                _resolvedOk = _fsIsActive != null;
                if (_resolvedOk)
                    RcPlugin.ModLogger?.LogInfo("MissileCamera FS reflection ready.");
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"MC reflect: {ex.Message}");
                _resolvedOk = false;
            }
        }
    }
}
