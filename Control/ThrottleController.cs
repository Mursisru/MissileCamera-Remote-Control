using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Player throttle 0–1 is written to Missile.throttle every tick (MissileCamera THR gauge reads it).
    /// Afterburner multiplies thrust only via Motor.Thrust Harmony — never inflates the gauge past 100%.
    /// </summary>
    internal static class ThrottleController
    {
        // Hold-ramp: ThrottleStep applied this many times per second while key held.
        private const float HoldStepsPerSec = 8f;

        private static float _throttle = 1f;
        private static bool _boost;
        private static RcEngineKind _engine = RcEngineKind.Jet;

        /// <summary>0–1 commanded throttle shown on MissileCamera THR bar.</summary>
        internal static float UiThrottle => _throttle;

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
            ApplyUiThrottle(missile);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null)
                return;

            _boost = KeybindPoll.IsHeld(RcConfig.Boost.Value);

            float step = Mathf.Max(0.01f, RcConfig.ThrottleStep.Value);
            float delta = step * HoldStepsPerSec * Time.unscaledDeltaTime;
            if (KeybindPoll.IsHeld(RcConfig.ThrottleUp.Value))
                _throttle = Mathf.Clamp01(_throttle + delta);
            if (KeybindPoll.IsHeld(RcConfig.ThrottleDown.Value))
                _throttle = Mathf.Clamp01(_throttle - delta);

            ApplyUiThrottle(missile);
            AfterburnerVfxBinder.SetBoost(missile, _boost);
        }

        /// <summary>Re-assert field after FixedUpdate seekers / physics (keeps THR gauge honest).</summary>
        internal static void Reinforce(Missile missile)
        {
            if (missile == null)
                return;
            ApplyUiThrottle(missile);
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

        private static void ApplyUiThrottle(Missile missile)
        {
            try
            {
                // Always 0–1 so MotorAccess.Clamp01 matches player command on the FLIR THR bar.
                missile.SetThrottle(_throttle);
            }
            catch
            {
                // ignore
            }
        }
    }
}
