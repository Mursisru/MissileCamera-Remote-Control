using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Skipping seeker.Seek() under RC also skips Arm / SetTangible / DeployFins / proxy setup.
    /// DeployFins / Arm / Tangible are one-shot — spam DeployFins every frame re-fires RpcUnfoldFins
    /// (~1s fold animation) and is a prime suspect for periodic aero jerks.
    /// </summary>
    internal static class RcWarheadSafety
    {
        private const float FinDelay = 0.5f;
        private const float TangibleDelay = 1.5f;
        private const float ArmDelay = 2f;
        private const float ProxyInterval = 0.25f;

        private static readonly FieldInfo? TargetField =
            typeof(Missile).GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static int _missileId;
        private static bool _finsDone;
        private static bool _tangibleDone;
        private static bool _armDone;
        private static float _nextProxyTime;

        internal static void Reset()
        {
            _missileId = 0;
            _finsDone = false;
            _tangibleDone = false;
            _armDone = false;
            _nextProxyTime = 0f;
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
                _nextProxyTime = 0f;
            }

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

            if (!_tangibleDone && age > TangibleDelay)
            {
                try
                {
                    if (!missile.IsTangible())
                        missile.SetTangible(true);
                    _tangibleDone = true;
                }
                catch
                {
                    // retry
                }
            }

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

            if (Time.unscaledTime < _nextProxyTime)
                return;
            _nextProxyTime = Time.unscaledTime + ProxyInterval;

            try
            {
                Unit? target = TargetField?.GetValue(missile) as Unit;
                if (target != null && !target.disabled && target.transform != null)
                {
                    Rigidbody? trb = null;
                    try
                    {
                        trb = target.rb;
                    }
                    catch
                    {
                        trb = target.GetComponent<Rigidbody>();
                    }

                    missile.SetProxyFuse(target.transform, trb);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
