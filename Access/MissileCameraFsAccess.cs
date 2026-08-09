using System;
using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>
    /// Reflects MissileCamera FS / feed APIs without referencing MC internals.
    /// CAMERA_SAFETY: never writes CameraStateManager.
    /// Hot path: use RcFrameCache; Raw* methods are uncached getters for the cache.
    /// </summary>
    internal static class MissileCameraFsAccess
    {
        private static bool _resolvedOk;
        private static float _nextResolveAttempt;
        private static PropertyInfo? _fsIsActive;
        private static MethodInfo? _tryGetFeedCamera;
        private static MethodInfo? _tryGetPanelRt;
        private static MethodInfo? _tryGetFollowedMissile;
        private static MethodInfo? _exitIfActive;
        private static PropertyInfo? _fsViewRt;
        private static PropertyInfo? _fsHud;
        private static PropertyInfo? _hudRootProp;

        internal static bool IsFullscreenActive => RcFrameCache.IsFullscreenActive;

        internal static Camera? TryGetFeedCamera() => RcFrameCache.FeedCamera;

        internal static RectTransform? TryGetFeedViewRect() => RcFrameCache.FeedViewRect;

        internal static RectTransform? TryGetHudOverlayRoot() => RcFrameCache.HudOverlayRoot;

        /// <summary>Missile currently shown in MissileCamera FS feed (not nearest-to-aircraft).</summary>
        internal static Missile? TryGetFollowedMissile()
        {
            EnsureResolved();
            if (_tryGetFollowedMissile == null)
                return null;
            try
            {
                return _tryGetFollowedMissile.Invoke(null, null) as Missile;
            }
            catch
            {
                return null;
            }
        }

        internal static bool QueryFullscreenActiveRaw()
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

        internal static Camera? QueryFeedCameraRaw()
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

        internal static void TryExitFullscreen()
        {
            EnsureResolved();
            if (_exitIfActive == null)
                return;
            try
            {
                _exitIfActive.Invoke(null, null);
            }
            catch
            {
                // ignore
            }
        }

        internal static RectTransform? QueryFeedViewRectRaw()
        {
            EnsureResolved();
            try
            {
                if (QueryFullscreenActiveRaw() && _fsViewRt != null)
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

        internal static RectTransform? QueryHudOverlayRootRaw()
        {
            EnsureResolved();
            try
            {
                RectTransform? view = QueryFeedViewRectRaw();
                if (view == null)
                    return null;

                Transform? hud = view.Find("MissileCameraHudOverlay");
                if (hud is RectTransform hudRt)
                    return hudRt;

                if (_fsHud != null && _fsHud.GetValue(null) is object hudObj)
                {
                    if (_hudRootProp == null)
                    {
                        _hudRootProp = hudObj.GetType().GetProperty(
                            "Root", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    if (_hudRootProp?.GetValue(hudObj) is RectTransform root && root != null)
                        return root;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>Call once from host bootstrap — avoid GetAssemblies from hot getters.</summary>
        internal static void TryResolveNow()
        {
            _nextResolveAttempt = 0f;
            EnsureResolved();
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
                _exitIfActive = fs?.GetMethod(
                    "ExitIfActive",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);

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
                _tryGetFollowedMissile = feed?.GetMethod(
                    "TryGetFollowedMissile",
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
