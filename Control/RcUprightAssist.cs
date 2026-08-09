namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Upright assist intentionally inert (corkscrew if enabled).
    /// Stock Steering handles roll; hooks kept for Take/Release call sites.
    /// </summary>
    internal static class RcUprightAssist
    {
        internal static void OnTakeControl(Missile missile) { }

        internal static void OnRelease(Missile? missile) { }

        internal static void ResetSaved() { }
    }
}
