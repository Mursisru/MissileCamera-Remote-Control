using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// WT mouse-aim: world yaw/pitch (no gimbal at zenith/nadir).
    /// Reticle and SetAimpoint share the same direction — no soft-lead overshoot.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float MaxPitchDeg = 89f;
        // Match vanilla Steering Dot&lt;0.71 clamp (~45°) so command ≈ what aero accepts.
        private const float MaxCommandAngleDeg = 45f;

        private static float _aimYawDeg;
        private static float _aimPitchDeg;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);

        internal static void Reset()
        {
            _initialized = false;
            _aimYawDeg = 0f;
            _aimPitchDeg = 0f;
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
                FromDirection(fwd.normalized);
                _initialized = true;
            }

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            if (mx * mx + my * my >= MouseDeadzone * MouseDeadzone)
            {
                float sens = Mathf.Max(0.02f, RcConfig.MouseSensitivity.Value) * MouseDegPerUnit;
                ApplyMouseCameraRelative(view, mx * sens, -my * sens);
            }

            Vector3 worldAimDir = ToDirection();
            Vector3 worldAimPoint = mt.position + worldAimDir * dist;

            Vector3 refDir = mt.forward;
            try
            {
                if (missile.rb != null && missile.rb.velocity.sqrMagnitude > 25f)
                    refDir = Vector3.Slerp(mt.forward, missile.rb.velocity.normalized, 0.35f).normalized;
            }
            catch
            {
                // ignore
            }

            Vector3 cmdDir = worldAimDir;
            float ang = Vector3.Angle(refDir, worldAimDir);
            if (ang > MaxCommandAngleDeg)
                cmdDir = Vector3.RotateTowards(refDir, worldAimDir, MaxCommandAngleDeg * Mathf.Deg2Rad, 0f);

            Vector3 cmdPoint = mt.position + cmdDir * dist;
            try
            {
                missile.SetAimpoint(cmdPoint.ToGlobalPosition(), Vector3.zero);
            }
            catch
            {
                // ignore
            }

            // Reticle follows commanded point (same as steering target) — no lead offset.
            ProjectReticleStable(feed, mt, cmdPoint);
        }

        private static void FromDirection(Vector3 dir)
        {
            dir.Normalize();
            _aimPitchDeg = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg, -MaxPitchDeg, MaxPitchDeg);
            _aimYawDeg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        private static Vector3 ToDirection()
        {
            float pitch = _aimPitchDeg * Mathf.Deg2Rad;
            float yaw = _aimYawDeg * Mathf.Deg2Rad;
            float cp = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), Mathf.Cos(yaw) * cp);
        }

        /// <summary>
        /// Mouse X = yaw about world up; Mouse Y = pitch about horizontal camera-right.
        /// Flat right never collapses at zenith/nadir the way view.up / view.right do.
        /// </summary>
        private static void ApplyMouseCameraRelative(Transform view, float yawDeltaDeg, float pitchDeltaDeg)
        {
            Vector3 flatF = view.forward;
            flatF.y = 0f;
            if (flatF.sqrMagnitude < 1e-4f)
            {
                Vector3 aim = ToDirection();
                flatF = new Vector3(aim.x, 0f, aim.z);
                if (flatF.sqrMagnitude < 1e-4f)
                    flatF = Vector3.forward;
            }

            flatF.Normalize();
            Vector3 flatR = Vector3.Cross(Vector3.up, flatF);
            if (flatR.sqrMagnitude < 1e-6f)
                flatR = Vector3.right;
            else
                flatR.Normalize();

            Vector3 dir = ToDirection();
            dir = Quaternion.AngleAxis(yawDeltaDeg, Vector3.up) * dir;
            dir = Quaternion.AngleAxis(pitchDeltaDeg, flatR) * dir;
            if (dir.sqrMagnitude < 1e-6f)
                return;

            FromDirection(dir.normalized);
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
