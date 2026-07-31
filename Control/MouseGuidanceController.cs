using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>Mouse → SetAimpoint. Vanilla Steering/ApplyAero untouched (Over-G intact).</summary>
    internal static class MouseGuidanceController
    {
        private static float _pitch;
        private static float _yaw;

        internal static void Reset()
        {
            _pitch = 0f;
            _yaw = 0f;
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            float sens = RcConfig.MouseSensitivity.Value;
            _yaw += Input.GetAxisRaw("Mouse X") * sens * 10f;
            _pitch -= Input.GetAxisRaw("Mouse Y") * sens * 10f;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);

            Quaternion offset = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 dir = missile.transform.rotation * offset * Vector3.forward;
            float dist = Mathf.Max(100f, RcConfig.AimDistance.Value);
            GlobalPosition aim = (missile.transform.position + dir * dist).ToGlobalPosition();

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
