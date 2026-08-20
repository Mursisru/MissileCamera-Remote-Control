using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Close MissileCamera FS when the local player aircraft that was alive under FS is destroyed.
    /// Does not exit FS if it was opened with no local aircraft (spectator-only).
    /// </summary>
    internal static class RcFsOwnshipGuard
    {
        private static Aircraft? _trackedOwnship;

        internal static void Reset()
        {
            _trackedOwnship = null;
        }

        internal static void Tick()
        {
            if (!MissileCameraFsAccess.IsControlAllowed)
            {
                _trackedOwnship = null;
                return;
            }

            bool hasLocal = false;
            Aircraft? local = null;
            try
            {
                hasLocal = GameManager.GetLocalAircraft(out Aircraft ac) && ac != null;
                local = hasLocal ? ac : null;
            }
            catch
            {
                hasLocal = false;
                local = null;
            }

            bool localAlive = hasLocal && local != null && !local.disabled;
            if (localAlive)
            {
                _trackedOwnship = local;
                return;
            }

            // Had a living ownship while FS was up — now gone/disabled → force exit.
            Aircraft? tracked = _trackedOwnship;
            bool trackedDead = tracked == null || tracked.disabled;
            if (_trackedOwnship != null && trackedDead)
            {
                _trackedOwnship = null;
                try
                {
                    if (RemoteControlSession.Controlled != null)
                        RemoteControlSession.Clear();
                }
                catch
                {
                    // ignore
                }

                MissileCameraFsAccess.TryExitFullscreen();
            }
        }
    }
}
