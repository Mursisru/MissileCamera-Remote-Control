namespace MissileCameraRemoteControl.Control
{
    internal static partial class MouseGuidanceController
    {
        /// <summary>
        /// External aim channel — adds to the same pending yaw/pitch buffer as physical input
        /// polling, so external and physical input compose naturally instead of one silently
        /// overwriting the other. Degrees, yaw right positive, pitch up negative.
        /// </summary>
        internal static void InjectExternal(float yawDeltaDeg, float pitchDeltaDeg)
        {
            if (float.IsNaN(yawDeltaDeg) || float.IsInfinity(yawDeltaDeg)) yawDeltaDeg = 0f;
            if (float.IsNaN(pitchDeltaDeg) || float.IsInfinity(pitchDeltaDeg)) pitchDeltaDeg = 0f;
            _pendingYawDeg += yawDeltaDeg;
            _pendingPitchDeg += pitchDeltaDeg;
        }
    }
}
