namespace MissileCameraRemoteControl.Control
{
    // Partial-class extension of Control/RcManualDetonate.cs — holds only the new external entry
    // point. The guard chain it shares with the physical key's Tick() (CanDetonate) stays in the
    // other file: that logic is Mursisru's own pre-existing rule for when detonate is allowed,
    // just extracted into a function both channels call, and shouldn't get lost in here.
    internal static partial class RcManualDetonate
    {
        /// <summary>External detonate channel (Bridge) — same guards + TryDetonate as the physical
        /// key, just without the RawKeyDown edge-detect (the caller — a single POST — is already
        /// one-shot).</summary>
        internal static bool TriggerExternal()
        {
            Missile? m = CanDetonate();
            if (m == null)
                return false;

            TryDetonate(m);
            return true;
        }
    }
}
