namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Upright assist intentionally inert.
    /// GitHub boosted uprightPreference + roll inject → corkscrew around aim / FS twitch.
    /// Stock Steering handles roll; we do not rewrite uprightPreference under RC.
    /// </summary>
    internal static class RcUprightAssist
    {
        internal static void OnTakeControl(Missile missile)
        {
        }

        internal static void OnRelease(Missile? missile)
        {
        }

        internal static void ResetSaved()
        {
        }

        internal static void AfterSteering(Missile missile)
        {
        }
    }
}
