using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Jet: 0–100% throttle + boost (&gt;100%, fuel ×2.5).
    /// Solid: choke reduces thrust (burn timer normal); boost ×1.5 thrust / ×2 burn.
    /// </summary>
    internal static class ThrottleController
    {
        private static float _jetThrottle = 1f;
        private static bool _boost;
        private static bool _choke;
        private static RcEngineKind _engine = RcEngineKind.Jet;

        internal static void Reset()
        {
            _jetThrottle = 1f;
            _boost = false;
            _choke = false;
            _engine = RcEngineKind.Jet;
        }

        internal static void OnTakeControl(Missile missile)
        {
            Reset();
            if (missile == null)
                return;

            RcMissileTag? tag = missile.GetComponent<RcMissileTag>();
            _engine = tag != null ? tag.Engine : RcEngineKind.Jet;
            _jetThrottle = 1f;
            try
            {
                missile.SetThrottle(1f);
            }
            catch
            {
                // ignore
            }
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null)
                return;

            _boost = KeybindPoll.IsHeld(RcConfig.Boost.Value);
            _choke = KeybindPoll.IsHeld(RcConfig.Choke.Value);

            if (_engine == RcEngineKind.Jet)
            {
                if (KeybindPoll.IsDown(RcConfig.ThrottleUp.Value))
                    _jetThrottle = Mathf.Clamp01(_jetThrottle + RcConfig.ThrottleStep.Value);
                if (KeybindPoll.IsDown(RcConfig.ThrottleDown.Value))
                    _jetThrottle = Mathf.Clamp01(_jetThrottle - RcConfig.ThrottleStep.Value);

                float t = _boost ? Mathf.Max(_jetThrottle, RcConfig.JetBoostThrottle.Value) : _jetThrottle;
                SafeSetThrottle(missile, t);
            }
            else
            {
                float t = 1f;
                if (_choke && !_boost)
                    t = Mathf.Clamp01(RcConfig.SolidChokeThrottle.Value);
                if (_boost)
                    t = RcConfig.SolidBoostThrottle.Value;
                SafeSetThrottle(missile, t);
            }

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
                    ? Mathf.Max(_jetThrottle, RcConfig.JetBoostThrottle.Value)
                    : _jetThrottle;
                burnMult = _boost ? RcConfig.JetBoostBurnMult.Value : 1f;
                return true;
            }

            // Solid
            if (_boost)
            {
                effectiveThrottle = RcConfig.SolidBoostThrottle.Value;
                burnMult = RcConfig.SolidBoostBurnMult.Value;
                return true;
            }

            if (_choke)
            {
                effectiveThrottle = Mathf.Clamp01(RcConfig.SolidChokeThrottle.Value);
                burnMult = 1f; // burn timer continues at normal rate
                return true;
            }

            effectiveThrottle = 1f;
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
