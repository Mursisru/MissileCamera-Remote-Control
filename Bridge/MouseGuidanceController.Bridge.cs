namespace MissileCameraRemoteControl.Control
{
    // Partial-class extension of Control/MouseGuidanceController.cs — the external-consumer half
    // lives here, sharing _pendingYawDeg/_pendingPitchDeg with the other half via the partial
    // class.
    internal static partial class MouseGuidanceController
    {
        /// <summary>
        /// External aim channel (Bridge) — adds to the same pending yaw/pitch buffer as
        /// PollMouse/PollKeyScheme, so a browser/HOTAS-app drag and physical mouse input compose
        /// naturally instead of one silently overwriting the other. Degrees, same convention as
        /// PollMouse (yaw right positive, pitch up negative — matches -my below).
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
