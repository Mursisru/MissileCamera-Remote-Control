using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>
    /// Reflects MissileCamera FS / follow APIs without referencing MC internals.
    /// CAMERA_SAFETY: never writes CameraStateManager — only MC overlay Enter/Toggle.
    /// </summary>
    internal static class MissileCameraFsAccess
    {
        private static bool _resolvedOk;
        private static float _nextResolveAttempt;
        private static PropertyInfo? _fsIsActive;
        private static MethodInfo? _tryGetFeedCamera;
        private static MethodInfo? _tryGetFollowedMissile;
        private static MethodInfo? _fsToggle;
        private static MethodInfo? _fsEnter;
        private static MethodInfo? _applyManualSelection;
        private static FieldInfo? _ownedActiveField;
        private static FieldInfo? _followedMissileField;
        private static FieldInfo? _manualFollowField;

        internal static bool IsReady
        {
            get
            {
                EnsureResolved();
                return _resolvedOk;
            }
        }

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

        internal static bool TryToggleFullscreen()
        {
            EnsureResolved();
            if (_fsToggle == null)
                return false;
            try
            {
                _fsToggle.Invoke(null, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Direct Enter after inject — skips Toggle debounce / early CanToggle race.</summary>
        internal static bool TryEnterFullscreen()
        {
            EnsureResolved();
            if (_fsEnter == null)
                return false;
            try
            {
                _fsEnter.Invoke(null, null);
                return IsFullscreenActive;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryForceFollowMissile(Missile missile)
        {
            if (missile == null || missile.disabled)
                return false;

            EnsureResolved();
            if (_ownedActiveField == null)
                return false;

            try
            {
                if (_ownedActiveField.GetValue(null) is not IList list)
                    return false;

                bool found = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], missile))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    list.Add(missile);

                // Prefer ApplyManualSelection; fall back to field writes.
                if (_applyManualSelection != null)
                {
                    _applyManualSelection.Invoke(null, new object[] { missile });
                }
                else
                {
                    _followedMissileField?.SetValue(null, missile);
                    _manualFollowField?.SetValue(null, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"MC ForceFollow failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>True if missile already in OwnedActive (no ApplyManualSelection side effects).</summary>
        internal static bool IsInOwnedActive(Missile missile)
        {
            if (missile == null)
                return false;
            EnsureResolved();
            if (_ownedActiveField == null)
                return false;
            try
            {
                if (_ownedActiveField.GetValue(null) is not IList list)
                    return false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list[i], missile))
                        return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        internal static MethodInfo? ResolveFullscreenToggleMethod()
        {
            EnsureResolved();
            return _fsToggle;
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
                _fsToggle = fs?.GetMethod("Toggle", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                _fsEnter = fs?.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                Type? feed = mc.GetType("MissileCamera.MissileCameraFeedController");
                _tryGetFeedCamera = feed?.GetMethod(
                    "TryGetFeedCamera",
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
                _applyManualSelection = feed?.GetMethod(
                    "ApplyManualSelection",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new[] { typeof(Missile) },
                    null);
                _ownedActiveField = feed?.GetField("OwnedActive", BindingFlags.Static | BindingFlags.NonPublic);
                _followedMissileField = feed?.GetField("_followedMissile", BindingFlags.Static | BindingFlags.NonPublic);
                _manualFollowField = feed?.GetField("_manualFollowActive", BindingFlags.Static | BindingFlags.NonPublic);

                _resolvedOk = _fsToggle != null && _ownedActiveField != null;
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
