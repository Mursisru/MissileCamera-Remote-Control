using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Shared 0–100% throttle (RShift / RCtrl) + LShift afterburner.
    /// Jet: boost &gt;100% thrust, fuel ×2.5. Solid: boost ×1.5 thrust, burn ×2.
    /// </summary>
    internal static class ThrottleController
    {
        private static float _throttle = 1f;
        private static bool _boost;
        private static RcEngineKind _engine = RcEngineKind.Jet;

        internal static void Reset()
        {
            _throttle = 1f;
            _boost = false;
            _engine = RcEngineKind.Jet;
        }

        internal static void OnTakeControl(Missile missile)
        {
            Reset();
            if (missile == null)
                return;

            RcMissileTag? tag = missile.GetComponent<RcMissileTag>();
            _engine = tag != null ? tag.Engine : RcEngineKind.Jet;
            _throttle = 1f;
            SafeSetThrottle(missile, 1f);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null)
                return;

            _boost = KeybindPoll.IsHeld(RcConfig.Boost.Value);

            if (KeybindPoll.IsDown(RcConfig.ThrottleUp.Value))
                _throttle = Mathf.Clamp01(_throttle + RcConfig.ThrottleStep.Value);
            if (KeybindPoll.IsDown(RcConfig.ThrottleDown.Value))
                _throttle = Mathf.Clamp01(_throttle - RcConfig.ThrottleStep.Value);

            float t;
            if (_engine == RcEngineKind.Jet)
                t = _boost ? Mathf.Max(_throttle, RcConfig.JetBoostThrottle.Value) : _throttle;
            else
                t = _boost ? RcConfig.SolidBoostThrottle.Value : _throttle;

            SafeSetThrottle(missile, t);
            AfterburnerVfxBinder.SetBoost(missile, _boost);
        }

        internal static bool TryGetMotorOverride(Missile missile, out float effectiveThrottle, out float burnMult)
        {
            effectiveThrottle = 1f;
            burnMult = 1f;

            if (!RemoteControlSession.IsControlling(missile))
                return false;

            if (_engine == RcEngineKind.Jet)
            {
                effectiveThrottle = _boost
                    ? Mathf.Max(_throttle, RcConfig.JetBoostThrottle.Value)
                    : _throttle;
                burnMult = _boost ? RcConfig.JetBoostBurnMult.Value : 1f;
                return true;
            }

            if (_boost)
            {
                effectiveThrottle = RcConfig.SolidBoostThrottle.Value;
                burnMult = RcConfig.SolidBoostBurnMult.Value;
                return true;
            }

            effectiveThrottle = _throttle;
            burnMult = 1f;
            return true;
        }

        private static void SafeSetThrottle(Missile missile, float throttle)
        {
            try
            {
                missile.SetThrottle(throttle);
            }
            catch
            {
                // ignore
            }
        }
    }
}
