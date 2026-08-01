using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// World-space WT aim: stable world direction; mouse rotates it; camera/missile turn
    /// slides the reticle on FLIR (toward center when you turn into the aim).
    /// Not camera-relative stick — that froze the marker on screen during rotation.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float MaxPitchSin = 0.9998f;
        private const float ProjectDistance = 2000f;

        private static Vector3 _worldAimDir = Vector3.forward;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);
        private static Missile? _lastMissile;
        private static float _pendingYawDeg;
        private static float _pendingPitchDeg;
        private static readonly Vector3[] _viewCorners = new Vector3[4];

        internal static Vector2 GetReticleViewport() => _lastStableViewport;

        internal static Vector2 GetReticleScreenPosition()
        {
            RectTransform? view = MissileCameraFsAccess.TryGetFeedViewRect();
            if (view != null)
            {
                view.GetWorldCorners(_viewCorners);
                float x = Mathf.Lerp(_viewCorners[0].x, _viewCorners[2].x, _lastStableViewport.x);
                float y = Mathf.Lerp(_viewCorners[0].y, _viewCorners[2].y, _lastStableViewport.y);
                return new Vector2(x, y);
            }

            return new Vector2(_lastStableViewport.x * Screen.width, _lastStableViewport.y * Screen.height);
        }

        internal static void Reset()
        {
            _initialized = false;
            _worldAimDir = Vector3.forward;
            _lastStableViewport = new Vector2(0.5f, 0.5f);
            _lastMissile = null;
            _pendingYawDeg = 0f;
            _pendingPitchDeg = 0f;
            FsAimReticle.SetVisible(false);
        }

        /// <summary>Update: queue mouse; keep own world-projected marker visible.</summary>
        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            _lastMissile = missile;
            FsAimReticle.SetVisible(true);

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            if (mx * mx + my * my >= MouseDeadzone * MouseDeadzone)
            {
                float sens = Mathf.Max(0.02f, RcConfig.MouseSensitivity.Value) * MouseDegPerUnit;
                _pendingYawDeg += mx * sens;
                _pendingPitchDeg += -my * sens;
            }
        }

        /// <summary>After MC SyncPose — apply mouse to world aim, write aimpoint, project marker.</summary>
        internal static void LateProject()
        {
            Missile? missile = _lastMissile;
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.IsControlling(missile))
                return;

            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            Transform mt = missile.transform;
            Transform view = feed != null ? feed.transform : mt;

            if (!_initialized)
            {
                Vector3 fwd = view.forward;
                if (fwd.sqrMagnitude < 1e-6f)
                    fwd = mt.forward;
                _worldAimDir = ClampPitch(fwd.normalized);
                _initialized = true;
            }

            float yaw = _pendingYawDeg;
            float pitch = _pendingPitchDeg;
            _pendingYawDeg = 0f;
            _pendingPitchDeg = 0f;
            if (yaw * yaw + pitch * pitch > 1e-10f)
                ApplyMouseToWorldAim(view, yaw, pitch);

            if (_worldAimDir.sqrMagnitude > 1e-8f)
                _worldAimDir = _worldAimDir.normalized;
            else
                _worldAimDir = view.forward.normalized;

            float dist = Mathf.Max(200f, RcConfig.AimDistance.Value);

            // Aimpoint along world aim from feed camera — projects cleanly on FLIR.
            Vector3 aimLocal = view.position + _worldAimDir * dist;
            try
            {
                missile.SetAimpoint(aimLocal.ToGlobalPosition(), Vector3.zero);
            }
            catch
            {
                try
                {
                    missile.SetAimpoint(missile.GlobalPosition() + _worldAimDir * dist, Vector3.zero);
                }
                catch
                {
                    // ignore
                }
            }

            // Project same world point → reticle slides when view rotates (world-space).
            Vector3 projectPoint = view.position + _worldAimDir * ProjectDistance;
            if (feed != null)
            {
                Vector3 vp = feed.WorldToViewportPoint(projectPoint);
                if (vp.z > 0.05f
                    && !float.IsNaN(vp.x) && !float.IsNaN(vp.y)
                    && !float.IsInfinity(vp.x) && !float.IsInfinity(vp.y))
                {
                    _lastStableViewport = new Vector2(vp.x, vp.y);
                    FsAimReticle.SetFromViewport(vp.x, vp.y, inFront: true);
                    return;
                }

                // Behind camera: keep last on-screen edge hint.
                FsAimReticle.SetFromViewport(_lastStableViewport.x, _lastStableViewport.y, inFront: true);
                return;
            }

            FsAimReticle.SetFromViewport(0.5f, 0.5f, inFront: true);
        }

        private static void ApplyMouseToWorldAim(Transform view, float yawDeltaDeg, float pitchDeltaDeg)
        {
            // Rotate world aim using current view axes (mouse feel), result stays world-stable.
            Vector3 up = view.up;
            Vector3 right = view.right;
            if (up.sqrMagnitude < 1e-8f || right.sqrMagnitude < 1e-8f)
                return;

            Vector3 dir = _worldAimDir;
            dir = Quaternion.AngleAxis(yawDeltaDeg, up.normalized) * dir;
            dir = Quaternion.AngleAxis(pitchDeltaDeg, right.normalized) * dir;
            if (dir.sqrMagnitude < 1e-6f)
                return;

            _worldAimDir = ClampPitch(dir.normalized);
        }

        private static Vector3 ClampPitch(Vector3 dir)
        {
            dir.Normalize();
            if (dir.y > MaxPitchSin || dir.y < -MaxPitchSin)
            {
                Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                if (flat.sqrMagnitude < 1e-8f)
                    flat = Vector3.forward;
                flat.Normalize();
                float y = Mathf.Sign(dir.y) * MaxPitchSin;
                float horiz = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                dir = flat * horiz + Vector3.up * y;
            }

            return dir.normalized;
        }
    }
}
