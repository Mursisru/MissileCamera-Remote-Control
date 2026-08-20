using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;

namespace MissileCameraRemoteControl.Control
{
    // Partial-class extension of Control/RcManualDetonate.cs — the external-consumer half lives
    // here: the Bridge trigger, and the guard chain it shares with the physical key's Tick() in
    // the other file (so the two channels can't silently drift apart). Shares TryDetonate via the
    // partial class.
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

        /// <summary>Guard chain shared by the physical key (Tick, other file) and TriggerExternal
        /// above so the two can't silently drift apart. Returns the missile that's clear to
        /// detonate, or null if any guard rejects.</summary>
        private static Missile? CanDetonate()
        {
            if (!RcConfig.Enabled.Value) return null;
            if (!MissileCameraFsAccess.IsControlAllowed) return null;
            if (!RemoteControlSession.IsActive) return null;

            Missile? m = RemoteControlSession.Controlled;
            if (m == null || m.disabled) return null;
            if (!RemoteControlSession.OwnsMissile(m)) return null;
            if (!AuthorityGate.CanControl(m)) return null;

            return m;
        }
    }
}
