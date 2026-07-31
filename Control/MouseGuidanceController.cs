using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// War Thunder mouse-aim: world-fixed aim point.
    /// Mouse rotates the aim in camera space; reticle is a projection of that point
    /// (so missile/camera turn moves the circle toward center — mouse does not fight the camera).
    /// Soft lead + nose-angle clamp keep commands inside vanilla gLimit / Steering.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float DefaultMouseDeg = 1.6f;
        private const float SoftLeadSeconds = 0.35f;
        private const float MaxCommandAngleDeg = 55f;

        private static Vector3 _worldAimLocal; // Datum-local / Unity world (same as transform.position space)
        private static bool _initialized;

        internal static void Reset()
        {
            _initialized = false;
            _worldAimLocal = Vector3.zero;
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            float dist = Mathf.Max(200f, RcConfig.AimDistance.Value);
            Transform mt = missile.transform;
            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();

            if (!_initialized)
            {
                Vector3 fwd = feed != null ? feed.transform.forward : mt.forward;
                Vector3 origin = feed != null ? feed.transform.position : mt.position;
                _worldAimLocal = origin + fwd.normalized * dist;
                _initialized = true;
            }

            // Mouse rotates world aim around the view (not a sticky screen offset).
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            float sens = Mathf.Max(0.02f, RcConfig.MouseSensitivity.Value) * DefaultMouseDeg;
            if (Mathf.Abs(mx) > 0.0001f || Mathf.Abs(my) > 0.0001f)
            {
                Transform view = feed != null ? feed.transform : mt;
                Vector3 from = view.position;
                Vector3 toAim = _worldAimLocal - from;
                float range = Mathf.Max(dist * 0.25f, toAim.magnitude);
                Vector3 dir = toAim.sqrMagnitude > 1e-4f ? toAim.normalized : view.forward;

                Quaternion yaw = Quaternion.AngleAxis(mx * sens, view.up);
                Quaternion pitch = Quaternion.AngleAxis(-my * sens, view.right);
                dir = (yaw * pitch * dir).normalized;
                _worldAimLocal = from + dir * range;
            }

            // Command aimpoint: clamp off-nose angle so Steering/gLimit are not permanently saturated.
            Vector3 desiredDir = (_worldAimLocal - mt.position);
            if (desiredDir.sqrMagnitude < 1e-4f)
                desiredDir = mt.forward;
            else
                desiredDir.Normalize();

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

            float ang = Vector3.Angle(refDir, desiredDir);
            Vector3 cmdDir = desiredDir;
            if (ang > MaxCommandAngleDeg)
                cmdDir = Vector3.RotateTowards(refDir, desiredDir, MaxCommandAngleDeg * Mathf.Deg2Rad, 0f);

            // Soft lead: place aim ahead along command dir (vanilla-friendly intercept feel).
            float lead = SoftLeadSeconds;
            try
            {
                if (missile.rb != null)
                    lead = Mathf.Clamp(SoftLeadSeconds * (1f + missile.rb.velocity.magnitude / 400f), 0.2f, 0.8f);
            }
            catch
            {
                // ignore
            }

            Vector3 cmdPoint = mt.position + cmdDir * dist;
            try
            {
                if (missile.rb != null)
                    cmdPoint += missile.rb.velocity * lead * 0.15f;
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

            // Reticle = projection of the *player* world aim (not the softened command).
            ProjectReticle(feed, mt, _worldAimLocal);
        }

        private static void ProjectReticle(Camera? feed, Transform missile, Vector3 worldAim)
        {
            if (feed != null)
            {
                Vector3 vp = feed.WorldToViewportPoint(worldAim);
                FsAimReticle.SetFromViewport(vp.x, vp.y, vp.z > 0f);
                return;
            }

            // Fallback: body-relative angles → fake viewport
            Vector3 local = missile.InverseTransformPoint(worldAim);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            float vx = 0.5f + Mathf.Clamp(yaw / 60f, -0.48f, 0.48f);
            float vy = 0.5f + Mathf.Clamp(pitch / 60f, -0.48f, 0.48f);
            FsAimReticle.SetFromViewport(vx, vy, local.z > 0f);
        }
    }
}
