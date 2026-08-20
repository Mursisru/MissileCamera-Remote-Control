using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Skipping seeker.Seek() under RC also skips Arm / SetTangible / DeployFins.
    /// DeployFins / Arm / Tangible are one-shot — spam DeployFins every frame re-fires RpcUnfoldFins.
    /// Tangible only when clear of owner (vanilla-style) — fixed 1.5s SetTangible caused launch-pad self-hits.
    /// Proxy fuse cleared every tick; SetProxyFuse also blocked by Harmony for all RC clones.
    /// </summary>
    internal static class RcWarheadSafety
    {
        private const float FinDelay = 0.5f;
        private const float ArmDelay = 2f;

        private static int _missileId;
        private static bool _finsDone;
        private static bool _tangibleDone;
        private static bool _armDone;

        internal static void Reset()
        {
            _missileId = 0;
            _finsDone = false;
            _tangibleDone = false;
            _armDone = false;
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            int id = missile.GetInstanceID();
            if (id != _missileId)
            {
                _missileId = id;
                _finsDone = false;
                _tangibleDone = false;
                _armDone = false;
            }

            Access.MissileAccess.ClearProxyFuseOnce(missile);

            float age = 0f;
            try
            {
                age = missile.timeSinceSpawn;
            }
            catch
            {
                return;
            }

            if (!_finsDone && age > FinDelay)
            {
                try
                {
                    missile.DeployFins();
                    _finsDone = true;
                }
                catch
                {
                    // retry next tick
                }
            }

            if (!_tangibleDone && RcTangibleEnsure.TrySet(missile))
                _tangibleDone = true;

            if (!_armDone && age > ArmDelay)
            {
                try
                {
                    if (!missile.IsArmed())
                        missile.Arm();
                    _armDone = true;
                }
                catch
                {
                    // retry
                }
            }
        }

    }
}
