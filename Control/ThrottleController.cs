using System.Reflection;
using MissileCameraRemoteControl.Access;
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
    internal static partial class ThrottleController
    {
        private const float RampPerSec = 2.85f;
        private const float TapStep = 0.1f;
        private const float ThrEps = 0.0005f;

        /// <summary>Motor.topSpeed lift while AB held — vanilla skips AddForce at Vmax.</summary>
        internal const float BoostTopSpeedFactor = 1.35f;

        private static readonly FieldInfo? ThrottleField =
            typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.Public);

        private static float _throttle = 1f;
        private static float _appliedThrottle = float.NaN;
        private static bool _boost;
        private static RcEngineKind _engine = RcEngineKind.Jet;

        internal static float UiThrottle => _throttle;
        internal static bool BoostActive => _boost;

        internal static void Reset()
        {
            _throttle = 1f;
            _appliedThrottle = float.NaN;
            _boost = false;
            _externalBoostHeld = false;
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
            ApplyUiThrottle(missile, force: true);
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
                ApplyUiThrottle(missile, force: false);
                RcBoostStateSync.Publish(missile, false);
                return;
            }

            ResolveBoost(missile);

            if (link == RcLinkLevel.Degraded)
            {
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

            ApplyUiThrottle(missile, force: false);
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
                ResolveBoost(missile);
                if (link == RcLinkLevel.Degraded)
                    _throttle = 1f;
            }

            ApplyUiThrottle(missile, force: false);
        }

        private static void ResolveBoost(Missile missile)
        {
            // AB only from afterburner bind (physical or external) + fuel — NEVER from formation FOLLOW.
            _boost = (KeybindPoll.IsHeld(RcConfig.Boost.Value) || _externalBoostHeld)
                && MissileAccess.HasMotorFuel(missile);
        }

        internal static bool TryGetMotorOverride(Missile missile, out float effectiveThrottle, out float burnMult)
        {
            effectiveThrottle = 1f;
            burnMult = 1f;

            if (missile == null)
                return false;
            if (!RemoteControlSession.OwnsMissile(missile) && !RcFormationFollow.IsFollower(missile))
                return false;

            RcLinkLevel link = RcLinkQuality.Current;
            if (link == RcLinkLevel.Lost)
            {
                effectiveThrottle = 1f;
                burnMult = 1f;
                return true;
            }

            float thr = link == RcLinkLevel.Degraded ? 1f : _throttle;
            if (RcFormationFollow.IsFollower(missile))
                thr = 1f;

            bool boost = _boost && MissileAccess.HasMotorFuel(missile);

            if (_engine == RcEngineKind.Jet)
            {
                effectiveThrottle = boost
                    ? Mathf.Max(0.01f, thr) * Mathf.Max(1f, RcConfig.JetBoostThrottle.Value)
                    : thr;
                burnMult = boost ? Mathf.Max(0.01f, RcConfig.JetBoostBurnMult.Value) : 1f;
                return true;
            }

            if (boost)
            {
                effectiveThrottle = Mathf.Max(0.01f, thr) * Mathf.Max(1f, RcConfig.SolidBoostThrottle.Value);
                burnMult = Mathf.Max(0.01f, RcConfig.SolidBoostBurnMult.Value);
                return true;
            }

            effectiveThrottle = thr;
            burnMult = 1f;
            return true;
        }

        private static void ApplyUiThrottle(Missile missile, bool force)
        {
            if (!force && !float.IsNaN(_appliedThrottle) && Mathf.Abs(_appliedThrottle - _throttle) < ThrEps)
                return;
            _appliedThrottle = _throttle;

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
