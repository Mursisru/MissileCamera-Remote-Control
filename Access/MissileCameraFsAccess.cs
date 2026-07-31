using System;
using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>
    /// Reflects MissileCamera fullscreen / feed camera without referencing internal MC types
    /// (MissileCamera.dll is not modified; InternalsVisibleTo is unavailable).
    /// </summary>
    internal static class MissileCameraFsAccess
    {
        private static bool _resolved;
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
            if (_resolved)
                return;
            _resolved = true;

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
            }
            catch
            {
                _fsIsActive = null;
                _tryGetFeedCamera = null;
            }
        }
    }
}
