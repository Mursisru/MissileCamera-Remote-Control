using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// While RC is active: force strong uprightPreference and assist roll when inverted
    /// so LookRotation(forward, transform.up) does not go unstable.
    /// </summary>
    internal static class RcUprightAssist
    {
        private const float ForcedUprightPreference = 2.5f;
        private const float InvertDotThreshold = 0.15f;
        private const float RollAssistStrength = 1.35f;

        private static readonly FieldInfo? UprightField =
            typeof(Missile).GetField("uprightPreference", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? InputsField =
            typeof(Missile).GetField("inputs", BindingFlags.Instance | BindingFlags.NonPublic);

        private static float _savedUpright = -1f;
        private static bool _boosted;

        internal static void OnTakeControl(Missile missile)
        {
            ResetSaved();
            if (missile == null || UprightField == null)
                return;

            try
            {
                if (UprightField.GetValue(missile) is float cur)
                {
                    _savedUpright = cur;
                    float forced = Mathf.Max(cur, ForcedUprightPreference);
                    UprightField.SetValue(missile, forced);
                    _boosted = true;
                }
            }
            catch
            {
                ResetSaved();
            }
        }

        internal static void OnRelease(Missile? missile)
        {
            if (missile != null && _boosted && UprightField != null && _savedUpright >= 0f)
            {
                try
                {
                    UprightField.SetValue(missile, _savedUpright);
                }
                catch
                {
                    // ignore
                }
            }

            ResetSaved();
        }

        internal static void ResetSaved()
        {
            _savedUpright = -1f;
            _boosted = false;
        }

        /// <summary>Called from Steering postfix — extra roll toward world-up when inverted / rolled.</summary>
        internal static void AfterSteering(Missile missile)
        {
            if (missile == null || InputsField == null)
                return;
            if (!RemoteControlSession.IsControlling(missile))
                return;

            try
            {
                Transform t = missile.transform;
                Vector3 fwd = t.forward;
                Vector3 desiredUp = Vector3.ProjectOnPlane(Vector3.up, fwd);
                if (desiredUp.sqrMagnitude < 1e-4f)
                    return;

                desiredUp.Normalize();
                float upDot = Vector3.Dot(t.up, desiredUp);

                // Signed roll error: how far current up is from desired around forward.
                float rollErrDeg = Vector3.SignedAngle(t.up, desiredUp, fwd);
                float rollCmd = Mathf.Clamp(rollErrDeg / 45f, -1f, 1f);

                // When inverted, punch harder so it unrolls instead of fighting weirdly.
                if (upDot < InvertDotThreshold)
                    rollCmd = Mathf.Clamp(rollCmd * RollAssistStrength, -1f, 1f);

                if (Mathf.Abs(rollCmd) < 0.02f)
                    return;

                if (InputsField.GetValue(missile) is not Vector3 inputs)
                    return;

                inputs.z = Mathf.Clamp(inputs.z + rollCmd * 0.85f, -1f, 1f);
                InputsField.SetValue(missile, inputs);
            }
            catch
            {
                // ignore
            }
        }
    }
}
