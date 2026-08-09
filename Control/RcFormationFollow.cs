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
    /// Ahead + behind: fly parallel to reticle dir (on-axis); soft lateral only — never chase a
    /// marker behind the nose (that caused braking / weird turns).
    /// </summary>
    internal static class RcFormationFollow
    {
        private const float LateralGain = 0.65f;
        private const float MinAheadM = 50f;
        private const float MinBehindM = 40f;
        private const float MinAimDistM = 800f;

        private struct FollowerState
        {
            internal Missile Missile;
            /// <summary>Along-track spacing from lead (always &gt; 0).</summary>
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

        /// <summary>Used by RcSeekSkipSet rebuild.</summary>
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
            // Don't Rebuild here during Release (Controlled already null) — Session rebuilds on Take.
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

        /// <summary>Auto-engage from Take when Config.AutoFormationFollow is on.</summary>
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

                    // Full throttle — avoid coasting/lag relative to lead.
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
        /// Parallel to lead aim dir. Ahead/behind slots on the reticle ray; lateral soft only.
        /// </summary>
        internal static void ReinforceAimpoint(Missile missile)
        {
            if (_lead == null || !IsFollower(missile))
                return;

            if (!_idToIndex.TryGetValue(missile.GetInstanceID(), out int idx)
                || idx < 0 || idx >= _followers.Count)
                return;

            FollowerState f = _followers[idx];
            try
            {
                Vector3 dir = ResolveLeadAimDir();
                float aimDist = Mathf.Max(MinAimDistM, RcConfig.AimDistance.Value);
                Vector3 leadPos = _lead.transform.position;
                Vector3 me = missile.transform.position;

                float spacing = f.IsAhead
                    ? Mathf.Max(f.AlongSpacing, MinAheadM)
                    : Mathf.Max(f.AlongSpacing, MinBehindM);

                // Desired point on the aim ray (ahead of lead / behind lead).
                Vector3 slot = f.IsAhead
                    ? leadPos + dir * spacing
                    : leadPos - dir * spacing;

                // Soft lateral only — keep parallel heading so nose never yaws into a chase reverse.
                Vector3 toSlot = slot - me;
                Vector3 along = Vector3.Project(toSlot, dir);
                Vector3 lateral = toSlot - along;

                Vector3 lat = lateral * LateralGain;
                float maxLat = aimDist * 0.4f;
                float latSq = lat.sqrMagnitude;
                if (latSq > maxLat * maxLat && latSq > 1e-6f)
                    lat *= maxLat / Mathf.Sqrt(latSq);

                Vector3 look = dir * aimDist + lat;
                if (look.sqrMagnitude < 1f)
                    look = dir * aimDist;

                Vector3 nose = missile.transform.forward;
                if (nose.sqrMagnitude > 1e-6f && Vector3.Dot(look, nose) < 0f)
                    look = dir * aimDist;

                Vector3 lookDir = look.normalized;
                Vector3 aim = RcBallisticImpactSafety.ResolveAimPoint(
                    me, lookDir, Mathf.Max(look.magnitude, MinAimDistM * 0.5f));
                Vector3 toAim = aim - me;
                if (toAim.sqrMagnitude < 1f || Vector3.Dot(toAim, lookDir) < 0f)
                    aim = me + lookDir * Mathf.Max(aimDist, MinAimDistM);

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
