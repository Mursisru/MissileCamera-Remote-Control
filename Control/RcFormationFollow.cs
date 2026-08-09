using System.Collections.Generic;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Formation follow (P): lead = controlled RC missile.
    /// Wingmen copy lead aim direction (parallel) + soft lateral slot hold.
    /// Never aim at the slot itself (that caused near-self aimpoint → PID thrash).
    /// </summary>
    internal static class RcFormationFollow
    {
        private const float SoftSlotGain = 0.35f;
        private const float MinAimDistM = 800f;

        private struct FollowerState
        {
            internal Missile Missile;
            internal Vector3 LocalOffset;
            internal bool FinsDone;
            internal bool TangibleDone;
            internal bool ArmDone;
        }

        private static Missile? _lead;
        private static bool _active;
        private static readonly List<FollowerState> _followers = new List<FollowerState>(16);

        internal static bool IsActive => _active && _lead != null && !_lead.disabled;

        internal static Missile? Lead => IsActive ? _lead : null;

        internal static bool IsFollower(Missile? missile)
        {
            if (!_active || missile == null || missile.disabled)
                return false;
            if (ReferenceEquals(missile, _lead))
                return false;
            for (int i = 0; i < _followers.Count; i++)
            {
                if (ReferenceEquals(_followers[i].Missile, missile))
                    return true;
            }

            return false;
        }

        internal static void Clear()
        {
            _active = false;
            _lead = null;
            _followers.Clear();
        }

        internal static void ToggleFromControlled()
        {
            if (!RemoteControlSession.IsActive || RemoteControlSession.Controlled == null)
            {
                RcPlugin.ModLogger?.LogInfo("Formation: take RC on a missile first.");
                return;
            }

            Missile lead = RemoteControlSession.Controlled;
            if (_active && ReferenceEquals(_lead, lead))
            {
                Clear();
                RcPlugin.ModLogger?.LogInfo("Formation follow OFF.");
                return;
            }

            Engage(lead);
        }

        private static void Engage(Missile lead)
        {
            Clear();
            if (lead == null || lead.disabled)
                return;

            RemoteControlSession.RefreshPool();
            IReadOnlyList<Missile> pool = RemoteControlSession.Pool;
            Transform leadTf = lead.transform;
            Vector3 leadPos = leadTf.position;
            Quaternion leadRot = leadTf.rotation;

            _lead = lead;
            _active = true;

            for (int i = 0; i < pool.Count; i++)
            {
                Missile m = pool[i];
                if (m == null || m.disabled || ReferenceEquals(m, lead))
                    continue;
                if (!AuthorityGate.CanControl(m) || !AuthorityGate.IsAllied(m))
                    continue;
                if (!MissileAccess.IsRcControllable(m))
                    continue;

                Vector3 local = Quaternion.Inverse(leadRot) * (m.transform.position - leadPos);
                _followers.Add(new FollowerState
                {
                    Missile = m,
                    LocalOffset = local,
                    FinsDone = false,
                    TangibleDone = false,
                    ArmDone = false
                });
            }

            RcPlugin.ModLogger?.LogInfo(
                $"Formation follow ON — lead={lead.unitName ?? lead.name}, wingmen={_followers.Count}");
        }

        /// <summary>FixedUpdate bookkeeping — aim is written from Steering Prefix via ReinforceAimpoint.</summary>
        internal static void Tick()
        {
            if (!_active)
                return;

            if (_lead == null || _lead.disabled || !AuthorityGate.CanControl(_lead)
                || !RemoteControlSession.OwnsMissile(_lead))
            {
                Clear();
                return;
            }

            PruneDeadFollowers();

            float leadThrottle = ThrottleController.UiThrottle;
            bool leadBoost = ThrottleController.BoostActive;

            for (int i = 0; i < _followers.Count; i++)
            {
                FollowerState f = _followers[i];
                Missile m = f.Missile;
                if (m == null || m.disabled)
                    continue;

                try
                {
                    EnsureFollowerWarhead(ref f);
                    _followers[i] = f;

                    try { m.SetThrottle(leadThrottle); }
                    catch { /* ignore */ }

                    AfterburnerVfxBinder.SetBoost(m, leadBoost);
                    RcSeekerSuppress.Tick(m);
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>After Seek, before Steering — parallel lead aim + soft slot.</summary>
        internal static void ReinforceAimpoint(Missile missile)
        {
            if (!IsFollower(missile) || _lead == null)
                return;

            FollowerState? found = null;
            for (int i = 0; i < _followers.Count; i++)
            {
                if (ReferenceEquals(_followers[i].Missile, missile))
                {
                    found = _followers[i];
                    break;
                }
            }

            if (found == null)
                return;

            FollowerState f = found.Value;
            try
            {
                Vector3 dir = ResolveLeadAimDir();
                float dist = Mathf.Max(MinAimDistM, RcConfig.AimDistance.Value);
                Vector3 origin = missile.transform.position;
                Vector3 aim = RcBallisticImpactSafety.ResolveAimPoint(origin, dir, dist);

                // Soft lateral/vertical hold toward formation slot (no along-track yank).
                Vector3 slot = _lead.transform.TransformPoint(f.LocalOffset);
                Vector3 toSlot = slot - origin;
                Vector3 along = Vector3.Project(toSlot, dir);
                Vector3 lateral = toSlot - along;
                aim += lateral * SoftSlotGain;

                // Re-resolve if soft slot pushed aim underground along same dive.
                Vector3 fromOrigin = aim - origin;
                if (fromOrigin.sqrMagnitude > 1f)
                    aim = RcBallisticImpactSafety.ResolveAimPoint(origin, fromOrigin.normalized, fromOrigin.magnitude);

                missile.SetAimpoint(aim.ToGlobalPosition(), ResolveLeadVel());
            }
            catch
            {
                // ignore
            }
        }

        private static Vector3 ResolveLeadAimDir()
        {
            Vector3 dir = MouseGuidanceController.WorldAimDir;
            if (dir.sqrMagnitude > 1e-6f)
                return dir.normalized;

            if (_lead != null)
            {
                Vector3 fwd = _lead.transform.forward;
                if (fwd.sqrMagnitude > 1e-6f)
                    return fwd.normalized;
            }

            return Vector3.forward;
        }

        private static Vector3 ResolveLeadVel()
        {
            try
            {
                if (_lead != null && _lead.rb != null)
                    return _lead.rb.velocity;
            }
            catch
            {
                // ignore
            }

            return Vector3.zero;
        }

        private static void EnsureFollowerWarhead(ref FollowerState f)
        {
            Missile m = f.Missile;
            if (m == null)
                return;

            MissileAccess.ClearProxyFuse(m);

            float age = 0f;
            try { age = m.timeSinceSpawn; }
            catch { return; }

            if (!f.FinsDone && age > 0.5f)
            {
                try
                {
                    m.DeployFins();
                    f.FinsDone = true;
                }
                catch { /* retry */ }
            }

            if (!f.TangibleDone)
            {
                try
                {
                    if (m.IsTangible())
                    {
                        f.TangibleDone = true;
                    }
                    else
                    {
                        Unit? owner = m.owner;
                        bool clear = owner == null || owner.disabled
                            || !FastMath.InRange(owner.GlobalPosition(), m.GlobalPosition(), 20f);
                        if (clear || age > 3f)
                        {
                            m.SetTangible(true);
                            f.TangibleDone = true;
                        }
                    }
                }
                catch { /* retry */ }
            }

            if (!f.ArmDone && age > 2f)
            {
                try
                {
                    if (!m.IsArmed())
                        m.Arm();
                    f.ArmDone = true;
                }
                catch { /* retry */ }
            }
        }

        private static void PruneDeadFollowers()
        {
            for (int i = _followers.Count - 1; i >= 0; i--)
            {
                Missile? m = _followers[i].Missile;
                if (m == null || m.disabled)
                    _followers.RemoveAt(i);
            }
        }
    }
}
