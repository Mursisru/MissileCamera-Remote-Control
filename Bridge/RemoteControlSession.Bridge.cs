using MissileCameraRemoteControl.Access;

namespace MissileCameraRemoteControl.Control
{
    // Partial-class extension of Control/RemoteControlSession.cs — holds only the two new
    // external entry points. The eligibility check they share with PickForTake (IsEligible) stays
    // in the other file: that logic is Mursisru's own pre-existing "what counts as controllable"
    // rule, just extracted into a function both entry points call, and shouldn't get lost in here.
    internal static partial class RemoteControlSession
    {
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
