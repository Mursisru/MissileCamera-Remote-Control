using System.Collections.Generic;
using System.Reflection;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Formation follow (P): lead = controlled RC missile.
    /// Wingmen share the lead's real aimpoint + lock (not a phantom point 4 km ahead).
    /// Detonation: vanilla DetectCollisions impact / TakeDamage only — no soft prox burst.
    /// </summary>
    internal static class RcFormationFollow
    {
        private const float MinAheadM = 50f;
        private const float MinBehindM = 40f;
        private const float MinAimDistM = 800f;
        private const float CatchUpLagM = 40f;
        private const float CatchUpGain = 0.45f;
        private const float CatchUpMinAheadM = 80f;
        private const float CatchUpMaxAheadM = 400f;
        private const float CatchUpBlendSpanM = 350f;
        private const float MaxCatchFoldRad = 70f * Mathf.Deg2Rad;

        private static readonly FieldInfo? SeekerTargetField =
            typeof(MissileSeeker).GetField("targetUnit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private struct FollowerState
        {
            internal Missile Missile;
            internal float AlongSpacing;
            internal bool IsAhead;
            internal bool FinsDone;
            internal bool TangibleDone;
            internal bool ArmDone;
        }

        private static Missile? _lead;
        private static bool _active;
        private static readonly List<FollowerState> _followers = new List<FollowerState>(16);
        private static readonly HashSet<int> _followerIds = new HashSet<int>(16);
        private static readonly Dictionary<int, int> _idToIndex = new Dictionary<int, int>(16);

        internal static bool IsActive => _active && _lead != null && !_lead.disabled;

        internal static Missile? Lead => IsActive ? _lead : null;

        internal static bool IsFollower(Missile? missile)
        {
            if (!_active || missile == null || missile.disabled)
                return false;
            if (ReferenceEquals(missile, _lead))
                return false;
            return _followerIds.Contains(missile.GetInstanceID());
        }

        internal static void AppendFollowerMissiles(System.Action<Missile> add)
        {
            if (!_active || add == null)
                return;
            for (int i = 0; i < _followers.Count; i++)
            {
                Missile? m = _followers[i].Missile;
                if (m != null && !m.disabled)
                    add(m);
            }
        }

        internal static void Clear()
        {
            _active = false;
            _lead = null;
            _followers.Clear();
            _followerIds.Clear();
            _idToIndex.Clear();
            if (RemoteControlSession.Controlled != null)
                RcSeekSkipSet.Rebuild();
            else
                RcSeekSkipSet.Clear();
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

        internal static void EngageFromTake(Missile lead)
        {
            if (lead == null || lead.disabled)
                return;
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
            Vector3 alongAxis = ResolveLeadAimDir();
            if (alongAxis.sqrMagnitude < 1e-6f)
                alongAxis = leadTf.forward;
            alongAxis.Normalize();

            _lead = lead;
            _active = true;

            int aheadN = 0;
            int behindN = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                Missile m = pool[i];
                if (m == null || m.disabled || ReferenceEquals(m, lead))
                    continue;
                if (!AuthorityGate.CanControl(m) || !AuthorityGate.IsAllied(m))
                    continue;
                if (!MissileAccess.IsRcControllable(m))
                    continue;

                Vector3 delta = m.transform.position - leadPos;
                float along = Vector3.Dot(delta, alongAxis);
                bool ahead = along > 0f;
                float spacing = Mathf.Abs(along);
                if (ahead)
                    spacing = Mathf.Max(spacing, MinAheadM);
                else
                    spacing = Mathf.Max(spacing, MinBehindM);

                int id = m.GetInstanceID();
                _idToIndex[id] = _followers.Count;
                _followerIds.Add(id);
                _followers.Add(new FollowerState
                {
                    Missile = m,
                    AlongSpacing = spacing,
                    IsAhead = ahead,
                    FinsDone = false,
                    TangibleDone = false,
                    ArmDone = false
                });

                if (ahead) aheadN++;
                else behindN++;
            }

            RcSeekSkipSet.Rebuild();
            RcPlugin.ModLogger?.LogInfo(
                $"Formation follow ON — lead={lead.unitName ?? lead.name}, ahead={aheadN}, behind={behindN}");
        }

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

            Unit? leadTarget = MissileAccess.TryGetLockedTarget(_lead);
            float leadThrottle = 1f;
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

                    SyncFollowerLock(m, leadTarget);

                    try { m.SetThrottle(leadThrottle); }
                    catch { /* ignore */ }

                    bool wingBoost = leadBoost && MissileAccess.HasMotorFuel(m);
                    AfterburnerVfxBinder.SetBoost(m, wingBoost);
                    RcSeekerSuppress.Tick(m);
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// Shared impact with lead. If behind on the aim ray, pursue a point on lead→impact
        /// (cut inside / catch up) — same stick alone left wingmen shallow and late.
        /// Never replace impact with me+4km (that peeled them off behind the lead).
        /// </summary>
        internal static void ReinforceAimpoint(Missile missile)
        {
            if (_lead == null || !IsFollower(missile))
                return;

            try
            {
                Vector3 impact = ResolveSharedAimLocal();
                if (impact.sqrMagnitude < 1f)
                    return;

                Vector3 leadPos = _lead.transform.position;
                Vector3 me = missile.transform.position;
                Vector3 leadToImpact = impact - leadPos;
                float leadRange = leadToImpact.magnitude;
                Vector3 dir = leadRange > 1f
                    ? leadToImpact / leadRange
                    : ResolveLeadAimDir();

                float myRange = (impact - me).magnitude;
                // Behind = farther from impact than the lead (along-track lag).
                float lag = myRange - leadRange;

                Vector3 aim = impact;
                if (lag > CatchUpLagM && leadRange > 50f)
                {
                    // Point on lead→impact ahead of lead — steeper dive onto the lead track.
                    float ahead = Mathf.Clamp(lag * CatchUpGain, CatchUpMinAheadM, CatchUpMaxAheadM);
                    ahead = Mathf.Min(ahead, leadRange * 0.85f);
                    Vector3 pursuit = leadPos + dir * ahead;
                    float blend = Mathf.Clamp01((lag - CatchUpLagM) / CatchUpBlendSpanM);
                    blend = Mathf.Min(blend, 0.8f);
                    aim = Vector3.Lerp(impact, pursuit, blend);
                }

                Vector3 toAim = aim - me;
                if (toAim.sqrMagnitude < 1f)
                    return;

                Vector3 nose = missile.transform.forward;
                if (nose.sqrMagnitude > 1e-6f && Vector3.Dot(toAim, nose) < 0.02f)
                {
                    // Fold toward impact inside forward cone — do NOT invent a far phantom aim.
                    Vector3 folded = Vector3.RotateTowards(
                        nose.normalized,
                        toAim.normalized,
                        MaxCatchFoldRad,
                        0f);
                    aim = me + folded * Mathf.Max(toAim.magnitude, 250f);
                }

                Vector3 leadVel = ResolveLeadVel();
                missile.SetAimpoint(aim.ToGlobalPosition(), leadVel);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Prefer lead's last WriteAimpoint (terrain-resolved). Never invent leadPos+4km —
        /// that made wingmen fly past the real impact and never collide.
        /// </summary>
        private static Vector3 ResolveSharedAimLocal()
        {
            if (MouseGuidanceController.TryGetLastAimLocal(out Vector3 cached))
                return cached;

            if (_lead != null && MissileAccess.TryGetAimLocal(_lead, out Vector3 fromLead))
                return fromLead;

            if (_lead == null)
                return Vector3.zero;

            Vector3 dir = ResolveLeadAimDir();
            float dist = Mathf.Max(MinAimDistM, RcConfig.AimDistance.Value);
            return RcBallisticImpactSafety.ResolveAimPoint(_lead.transform.position, dir, dist);
        }

        private static void SyncFollowerLock(Missile follower, Unit? leadTarget)
        {
            try
            {
                follower.SetTarget(leadTarget);
            }
            catch
            {
                // ignore
            }

            if (SeekerTargetField == null)
                return;
            try
            {
                MissileSeeker? seeker = MissileAccess.GetSeeker(follower) ?? follower.GetComponent<MissileSeeker>();
                if (seeker != null)
                    SeekerTargetField.SetValue(seeker, leadTarget);
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

            MissileAccess.ClearProxyFuseOnce(m);

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
            bool changed = false;
            for (int i = _followers.Count - 1; i >= 0; i--)
            {
                Missile? m = _followers[i].Missile;
                if (m == null || m.disabled)
                {
                    _followers.RemoveAt(i);
                    changed = true;
                }
            }

            if (!changed)
                return;

            _followerIds.Clear();
            _idToIndex.Clear();
            for (int i = 0; i < _followers.Count; i++)
            {
                Missile? m = _followers[i].Missile;
                if (m == null)
                    continue;
                int id = m.GetInstanceID();
                _followerIds.Add(id);
                _idToIndex[id] = i;
            }

            RcSeekSkipSet.Rebuild();
        }
    }
}
