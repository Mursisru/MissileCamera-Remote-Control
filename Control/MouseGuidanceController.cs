using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// WT mouse-aim with a stable world-space DIRECTION (not a fixed world point).
    /// Fixed world points teleport the reticle when the missile flies past / aim goes behind the feed cam.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float MouseDegPerUnit = 1.25f;
        private const float MouseDeadzone = 0.02f;
        private const float SoftLeadSeconds = 0.3f;
        private const float MaxCommandAngleDeg = 50f;

        private static Vector3 _worldAimDir = Vector3.forward;
        private static bool _initialized;
        private static Vector2 _lastStableViewport = new Vector2(0.5f, 0.5f);

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
                _worldAimDir = fwd.normalized;
                _initialized = true;
            }

            // Mouse only — deadzone kills stick noise / tiny axis jitter.
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            if (mx * mx + my * my >= MouseDeadzone * MouseDeadzone)
            {
                float sens = Mathf.Max(0.02f, RcConfig.MouseSensitivity.Value) * MouseDegPerUnit;
                Quaternion yaw = Quaternion.AngleAxis(mx * sens, view.up);
                Quaternion pitch = Quaternion.AngleAxis(-my * sens, view.right);
                Vector3 rotated = yaw * pitch * _worldAimDir;
                if (rotated.sqrMagnitude > 1e-6f)
                    _worldAimDir = rotated.normalized;
            }

            // Aim point rides with the missile along the fixed world direction (never "flies past").
            Vector3 worldAimPoint = mt.position + _worldAimDir * dist;

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

            Vector3 desiredDir = _worldAimDir;
            float ang = Vector3.Angle(refDir, desiredDir);
            Vector3 cmdDir = desiredDir;
            if (ang > MaxCommandAngleDeg)
                cmdDir = Vector3.RotateTowards(refDir, desiredDir, MaxCommandAngleDeg * Mathf.Deg2Rad, 0f);

            Vector3 cmdPoint = mt.position + cmdDir * dist;
            try
            {
                if (missile.rb != null)
                {
                    float lead = Mathf.Clamp(SoftLeadSeconds * (1f + missile.rb.velocity.magnitude / 400f), 0.2f, 0.7f);
                    cmdPoint += missile.rb.velocity * lead * 0.12f;
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                missile.SetAimpoint(cmdPoint.ToGlobalPosition(), Vector3.zero);
            }
            catch
            {
                // ignore
            }

            ProjectReticleStable(feed, mt, worldAimPoint);
        }

        /// <summary>
        /// Project aim to viewport. If behind camera, keep last on-screen position (no edge teleport).
        /// </summary>
        private static void ProjectReticleStable(Camera? feed, Transform missile, Vector3 worldAimPoint)
        {
            if (feed != null)
            {
                Vector3 vp = feed.WorldToViewportPoint(worldAimPoint);
                if (vp.z > 0.05f)
                {
                    float vx = Mathf.Clamp01(vp.x);
                    float vy = Mathf.Clamp01(vp.y);
                    // Only accept if roughly on/near screen — avoid wild NaN jumps.
                    if (!float.IsNaN(vx) && !float.IsNaN(vy) && !float.IsInfinity(vx) && !float.IsInfinity(vy))
                    {
                        _lastStableViewport = new Vector2(vx, vy);
                        FsAimReticle.SetFromViewport(vx, vy, inFront: true);
                        return;
                    }
                }

                // Behind / invalid — hold last stable screen spot.
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
