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
        private static MethodInfo? _tryGetPanelRt;
        private static PropertyInfo? _fsViewRt;
        private static PropertyInfo? _fsHud;

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

        /// <summary>FS feed view rect (same space as FLIR attitude center). Null if not FS / not ready.</summary>
        internal static RectTransform? TryGetFeedViewRect()
        {
            EnsureResolved();
            try
            {
                if (IsFullscreenActive && _fsViewRt != null)
                {
                    if (_fsViewRt.GetValue(null) is RectTransform view && view != null)
                        return view;
                }

                if (_tryGetPanelRt != null
                    && _tryGetPanelRt.Invoke(null, null) is RectTransform panel
                    && panel != null)
                    return panel;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>MC HUD overlay root under the feed view — preferred parent for RC reticle.</summary>
        internal static RectTransform? TryGetHudOverlayRoot()
        {
            EnsureResolved();
            try
            {
                RectTransform? view = TryGetFeedViewRect();
                if (view == null)
                    return null;

                Transform? hud = view.Find("MissileCameraHudOverlay");
                if (hud != null && hud is RectTransform hudRt)
                    return hudRt;

                // FS host Hud.Root via reflection if Find missed.
                if (_fsHud != null && _fsHud.GetValue(null) is object hudObj)
                {
                    PropertyInfo? rootProp = hudObj.GetType().GetProperty(
                        "Root", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (rootProp?.GetValue(hudObj) is RectTransform root && root != null)
                        return root;
                }
            }
            catch
            {
                // ignore
            }

            return null;
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
                _tryGetPanelRt = feed?.GetMethod(
                    "TryGetPanelRt",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);

                Type? host = mc.GetType("MissileCamera.MissileCameraFullscreenFeedHost");
                _fsViewRt = host?.GetProperty("ViewRt", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                _fsHud = host?.GetProperty("Hud", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

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
