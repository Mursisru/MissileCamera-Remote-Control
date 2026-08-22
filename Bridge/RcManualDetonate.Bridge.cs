namespace MissileCameraRemoteControl.Control
{
    // Continued from Control/RcManualDetonate.cs
    internal static partial class RcManualDetonate
    {
        /// <summary>External detonate channel — same guards + TryDetonate as the physical key,
        /// just without the RawKeyDown edge-detect (a single external call is already
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
