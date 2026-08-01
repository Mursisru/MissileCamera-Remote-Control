using System.Reflection;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Player throttle 0–1 → Missile.throttle (MissileCamera THR gauge).
    /// Afterburner multiplies Motor.Thrust only (does not inflate the gauge).
    /// Lost link: throttle locked at 1, boost blocked.
    /// Degraded: thr locked at 1, AB still allowed (optical LoS to own jet often fails in FS).
    /// </summary>
    internal static class ThrottleController
    {
        private const float RampPerSec = 2.85f;
        private const float TapStep = 0.1f;

        /// <summary>Motor.topSpeed lift while AB held — vanilla skips AddForce at Vmax.</summary>
        internal const float BoostTopSpeedFactor = 1.35f;

        private static readonly FieldInfo? ThrottleField =
            typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.Public);

        private static float _throttle = 1f;
        private static bool _boost;
        private static RcEngineKind _engine = RcEngineKind.Jet;

        internal static float UiThrottle => _throttle;
        internal static bool BoostActive => _boost;

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
            RcBoostStateSync.Publish(missile, false);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null)
                return;

            RcLinkLevel link = RcLinkQuality.Current;
            if (link == RcLinkLevel.Lost)
            {
                _throttle = 1f;
                _boost = false;
                ApplyUiThrottle(missile);
                AfterburnerVfxBinder.SetBoost(missile, false);
                RcBoostStateSync.Publish(missile, false);
                return;
            }

            _boost = KeybindPoll.IsHeld(RcConfig.Boost.Value);

            if (link == RcLinkLevel.Degraded)
            {
                // Weak mesh: hold cruise thr, but keep AB authority.
                _throttle = 1f;
            }
            else
            {
                if (KeybindPoll.IsDown(RcConfig.ThrottleUp.Value))
                    _throttle = Mathf.Clamp01(_throttle + TapStep);
                else if (KeybindPoll.IsHeld(RcConfig.ThrottleUp.Value))
                    _throttle = Mathf.Clamp01(_throttle + RampPerSec * Time.unscaledDeltaTime);

                if (KeybindPoll.IsDown(RcConfig.ThrottleDown.Value))
                    _throttle = Mathf.Clamp01(_throttle - TapStep);
                else if (KeybindPoll.IsHeld(RcConfig.ThrottleDown.Value))
                    _throttle = Mathf.Clamp01(_throttle - RampPerSec * Time.unscaledDeltaTime);
            }

            ApplyUiThrottle(missile);
            AfterburnerVfxBinder.SetBoost(missile, _boost);
            RcBoostStateSync.Publish(missile, _boost);
        }

        internal static void Reinforce(Missile missile)
        {
            if (missile == null)
                return;

            RcLinkLevel link = RcLinkQuality.Current;
            if (link == RcLinkLevel.Lost)
            {
                _boost = false;
                _throttle = 1f;
            }
            else
            {
                // FixedUpdate can run before Update — re-poll AB for Motor.Thrust same frame.
                _boost = KeybindPoll.IsHeld(RcConfig.Boost.Value);
                if (link == RcLinkLevel.Degraded)
                    _throttle = 1f;
            }

            ApplyUiThrottle(missile);
        }

        /// <summary>
        /// Motor.Thrust Prefix — OwnsMissile so FixedUpdate still applies if FS flickers.
        /// </summary>
        internal static bool TryGetMotorOverride(Missile missile, out float effectiveThrottle, out float burnMult)
        {
            effectiveThrottle = 1f;
            burnMult = 1f;

            if (missile == null || !RemoteControlSession.OwnsMissile(missile))
                return false;

            RcLinkLevel link = RcLinkQuality.Current;
            if (link == RcLinkLevel.Lost)
            {
                effectiveThrottle = 1f;
                burnMult = 1f;
                return true;
            }

            float thr = link == RcLinkLevel.Degraded ? 1f : _throttle;

            if (_engine == RcEngineKind.Jet)
            {
                effectiveThrottle = _boost
                    ? Mathf.Max(0.01f, thr) * Mathf.Max(1f, RcConfig.JetBoostThrottle.Value)
                    : thr;
                burnMult = _boost ? Mathf.Max(0.01f, RcConfig.JetBoostBurnMult.Value) : 1f;
                return true;
            }

            if (_boost)
            {
                effectiveThrottle = Mathf.Max(0.01f, thr) * Mathf.Max(1f, RcConfig.SolidBoostThrottle.Value);
                burnMult = Mathf.Max(0.01f, RcConfig.SolidBoostBurnMult.Value);
                return true;
            }

            effectiveThrottle = thr;
            burnMult = 1f;
            return true;
        }

        private static void ApplyUiThrottle(Missile missile)
        {
            try
            {
                missile.SetThrottle(_throttle);
            }
            catch
            {
                // ignore
            }

            if (ThrottleField == null)
                return;
            try
            {
                ThrottleField.SetValue(missile, _throttle);
            }
            catch
            {
                // ignore
            }
        }
    }
}
