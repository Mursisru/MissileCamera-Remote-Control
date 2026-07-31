using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// War Thunder-style: missile steers toward the FS aim circle.
    /// Prefer MissileCamera feed camera ray; fallback to nose-relative angles.
    /// </summary>
    internal static class MouseGuidanceController
    {
        private const float FallbackMaxAngleDeg = 45f;

        internal static void Reset()
        {
            FsAimReticle.ResetToCenter();
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            FsAimReticle.TickMove();
            Vector2 sp = FsAimReticle.ScreenPosition;
            float dist = Mathf.Max(100f, RcConfig.AimDistance.Value);

            GlobalPosition aim;
            Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
            if (feed != null)
            {
                // Feed RT is shown fullscreen — viewport matches screen.
                float vx = sp.x / Mathf.Max(1f, Screen.width);
                float vy = sp.y / Mathf.Max(1f, Screen.height);
                Ray ray = feed.ViewportPointToRay(new Vector3(vx, vy, 0f));
                Vector3 world = ray.origin + ray.direction.normalized * dist;
                aim = world.ToGlobalPosition();
            }
            else
            {
                float nx = (sp.x / Mathf.Max(1f, Screen.width) - 0.5f) * 2f;
                float ny = (sp.y / Mathf.Max(1f, Screen.height) - 0.5f) * 2f;
                float yaw = nx * FallbackMaxAngleDeg;
                float pitch = -ny * FallbackMaxAngleDeg;
                Vector3 dir = missile.transform.rotation * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                aim = (missile.transform.position + dir * dist).ToGlobalPosition();
            }

            try
            {
                missile.SetAimpoint(aim, Vector3.zero);
            }
            catch
            {
                // ignore
            }
        }
    }
}
