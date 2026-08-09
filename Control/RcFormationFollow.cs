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
    /// Ahead wingmen stay in front of the lead and fly toward the shared direction marker.
    /// Behind wingmen stay directly behind the lead and also fly toward that marker.
    /// </summary>
    internal static class RcFormationFollow
    {
        private const float AheadSlotGain = 0.55f;
        private const float BehindSlotGain = 0.85f;
        private const float MinAheadM = 50f;
        private const float MinBehindM = 40f;
        private const float MinAimDistM = 800f;

        private struct FollowerState
        {
            internal Missile Missile;
            /// <summary>Lateral/vertical offset in aim-frame at engage (Z unused).</summary>
            internal Vector3 LateralLocal;
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

            Quaternion aimFrame = SafeLookRotation(alongAxis, leadTf.up);

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

                Vector3 local = Quaternion.Inverse(aimFrame) * delta;
                Vector3 lateral = new Vector3(local.x, local.y, 0f);

                int id = m.GetInstanceID();
                _idToIndex[id] = _followers.Count;
                _followerIds.Add(id);
                _followers.Add(new FollowerState
                {
                    Missile = m,
                    LateralLocal = lateral,
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

        /// <summary>After Seek: shared direction marker + ahead/behind slot hold.</summary>
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

                // Same direction marker the lead flies toward (reticle command).
                Vector3 leadMarker = RcBallisticImpactSafety.ResolveAimPoint(leadPos, dir, aimDist);

                Quaternion aimFrame = SafeLookRotation(dir, _lead.transform.up);
                Vector3 lateral = aimFrame * f.LateralLocal;

                Vector3 slot;
                float slotGain;
                if (f.IsAhead)
                {
                    // Stay in front of the lead along the marker axis.
                    float spacing = Mathf.Max(f.AlongSpacing, MinAheadM);
                    slot = leadPos + dir * spacing + lateral;
                    slotGain = AheadSlotGain;
                }
                else
                {
                    // Stay directly behind the lead.
                    float spacing = Mathf.Max(f.AlongSpacing, MinBehindM);
                    slot = leadPos - dir * spacing + lateral;
                    slotGain = BehindSlotGain;
                }

                // Marker for this wingman = lead marker shifted by formation offset
                // (ahead aims further along the attack line; behind aims short of it).
                Vector3 myMarker = leadMarker + (slot - leadPos);

                // Hold slot while still flying into the marker.
                Vector3 cmd = myMarker + (slot - me) * slotGain;
                Vector3 cmdDelta = cmd - me;
                if (cmdDelta.sqrMagnitude < 1f)
                    cmdDelta = dir * aimDist;

                Vector3 aim = RcBallisticImpactSafety.ResolveAimPoint(
                    me, cmdDelta.normalized, Mathf.Max(cmdDelta.magnitude, MinAimDistM * 0.5f));

                missile.SetAimpoint(aim.ToGlobalPosition(), ResolveLeadVel());
            }
            catch
            {
                // ignore
            }
        }

        private static Quaternion SafeLookRotation(Vector3 forward, Vector3 approxUp)
        {
            if (forward.sqrMagnitude < 1e-8f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 up = approxUp.sqrMagnitude > 1e-8f ? approxUp : Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up.normalized)) > 0.98f)
                up = Vector3.up;
            try
            {
                return Quaternion.LookRotation(forward, up);
            }
            catch
            {
                return Quaternion.identity;
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
