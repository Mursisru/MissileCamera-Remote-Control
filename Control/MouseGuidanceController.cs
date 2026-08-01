using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// WT mouse-aim: SetAimpoint in Update + reinforce in Steering prefix (after Seek).
    /// Direct cmd (no soft Slerp) so terminal/leaked Seek cannot hold the nose.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float MaxPitchSin = 0.9998f;

        private static Vector3 _worldAimDir = Vector3.forward;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);

        internal static Vector2 GetReticleViewport() => _lastStableViewport;

        internal static Vector2 GetReticleScreenPosition() =>
            new Vector2(_lastStableViewport.x * Screen.width, _lastStableViewport.y * Screen.height);

        internal static void Reset()
        {
            _initialized = false;
            _worldAimDir = Vector3.forward;
            _lastStableViewport = new Vector2(0.5f, 0.5f);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            float dist = Mathf.Max(200f, RcConfig.AimDistance.Value);
            Transform mt = missile.transform;
            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            Transform view = feed != null ? feed.transform : mt;

            if (!_initialized)
            {
                Vector3 fwd = view.forward;
                if (fwd.sqrMagnitude < 1e-6f)
                    fwd = mt.forward;
                _worldAimDir = ClampPitch(fwd.normalized);
                _initialized = true;
            }

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            if (mx * mx + my * my >= MouseDeadzone * MouseDeadzone)
            {
                float sens = Mathf.Max(0.02f, RcConfig.MouseSensitivity.Value) * MouseDegPerUnit;
                ApplyMouseCameraRelative(view, mx * sens, -my * sens);
            }

            if (_worldAimDir.sqrMagnitude > 1e-8f)
                _worldAimDir = _worldAimDir.normalized;
            else
                _worldAimDir = mt.forward.normalized;

            Vector3 worldAimPoint = WriteAimpoint(missile, dist, mt);
            ProjectReticleStable(feed, mt, worldAimPoint);
        }

        // Aim only in Update — Fixed reinforce removed (dual SetAimpoint → frequent jerks).

        private static Vector3 WriteAimpoint(Missile missile, float dist, Transform mt)
        {
            try
            {
                GlobalPosition gp = missile.GlobalPosition();
                GlobalPosition aimGp = gp + _worldAimDir * dist;
                missile.SetAimpoint(aimGp, Vector3.zero);
                return aimGp.ToLocalPosition();
            }
            catch
            {
                Vector3 local = mt.position + _worldAimDir * dist;
                try
                {
                    missile.SetAimpoint(local.ToGlobalPosition(), Vector3.zero);
                }
                catch
                {
                    // ignore
                }

                return local;
            }
        }

        private static void ApplyMouseCameraRelative(Transform view, float yawDeltaDeg, float pitchDeltaDeg)
        {
            Vector3 flatF = view.forward;
            flatF.y = 0f;
            if (flatF.sqrMagnitude < 1e-4f)
            {
                flatF = new Vector3(_worldAimDir.x, 0f, _worldAimDir.z);
                if (flatF.sqrMagnitude < 1e-4f)
                    flatF = Vector3.forward;
            }

            flatF.Normalize();
            Vector3 flatR = Vector3.Cross(Vector3.up, flatF);
            if (flatR.sqrMagnitude < 1e-6f)
                flatR = Vector3.right;
            else
                flatR.Normalize();

            Vector3 dir = _worldAimDir;
            dir = Quaternion.AngleAxis(yawDeltaDeg, Vector3.up) * dir;
            dir = Quaternion.AngleAxis(pitchDeltaDeg, flatR) * dir;
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

        private static void ProjectReticleStable(Camera? feed, Transform missile, Vector3 worldAimPoint)
        {
            if (feed != null)
            {
                Vector3 vp = feed.WorldToViewportPoint(worldAimPoint);
                if (vp.z > 0.05f)
                {
                    float vx = Mathf.Clamp01(vp.x);
                    float vy = Mathf.Clamp01(vp.y);
                    if (!float.IsNaN(vx) && !float.IsNaN(vy) && !float.IsInfinity(vx) && !float.IsInfinity(vy))
                    {
                        _lastStableViewport = new Vector2(vx, vy);
                        FsAimReticle.SetFromViewport(vx, vy, inFront: true);
                        return;
                    }
                }

                FsAimReticle.SetFromViewport(_lastStableViewport.x, _lastStableViewport.y, inFront: true);
                return;
            }

            Vector3 local = missile.InverseTransformPoint(worldAimPoint);
            if (local.z > 0.05f)
            {
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(local.y, Mathf.Max(0.01f, local.z)) * Mathf.Rad2Deg;
                float vx = Mathf.Clamp01(0.5f + Mathf.Clamp(yaw / 60f, -0.48f, 0.48f));
                float vy = Mathf.Clamp01(0.5f + Mathf.Clamp(pitch / 60f, -0.48f, 0.48f));
                _lastStableViewport = new Vector2(vx, vy);
                FsAimReticle.SetFromViewport(vx, vy, inFront: true);
                return;
            }

            FsAimReticle.SetFromViewport(_lastStableViewport.x, _lastStableViewport.y, inFront: true);
        }
    }
}
