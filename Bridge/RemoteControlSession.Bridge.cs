using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Network;

namespace MissileCameraRemoteControl.Control
{
    // Partial-class extension of Control/RemoteControlSession.cs — the external-consumer half
    // lives here: the two Bridge take-channels, and the eligibility check they share with
    // PickForTake in the other file (so the two entry points can't disagree about what
    // "controllable" means). Shares _pool, PickForTake, Take, RefreshPool, and
    // MissileCameraFsAccess/AuthorityGate/MissileAccess usage via the partial class.
    internal static partial class RemoteControlSession
    {
        /// <summary>Take-eligibility check shared by PickForTake (other file) and TryTakeAt below,
        /// so the two entry points can't disagree about what "controllable" means.</summary>
        private static bool IsEligible(Missile? candidate) =>
            candidate != null && !candidate.disabled
            && AuthorityGate.CanControl(candidate) && AuthorityGate.IsAllied(candidate)
            && MissileAccess.IsRcControllable(candidate);

        /// <summary>External take channel (Bridge) — same picking logic as ToggleNearest's Take
        /// branch, but explicit (never releases an existing session) so a browser "take" button
        /// has predictable behavior regardless of current state.</summary>
        internal static bool TryTakeNearest()
        {
            if (!MissileCameraFsAccess.IsControlAllowed)
                return false;

            RefreshPool();
            Missile? best = PickForTake(_pool);
            if (best == null)
                return false;

            Take(best);
            return true;
        }

        /// <summary>External take-by-pool-index channel (Bridge) — for a browser missile picker
        /// mirroring RcMissilePickerUi. Call RefreshPool() (via the Pool getter usage below) is
        /// implicit: callers should read Pool right before to build a matching index list.</summary>
        internal static bool TryTakeAt(int index)
        {
            if (!MissileCameraFsAccess.IsControlAllowed)
                return false;
            if (index < 0 || index >= _pool.Count)
                return false;

            Missile candidate = _pool[index];
            if (!IsEligible(candidate))
                return false;

            Take(candidate);
            return true;
        }
    }
}
