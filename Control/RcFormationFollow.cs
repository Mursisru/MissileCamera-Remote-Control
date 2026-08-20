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
    /// Formation FOLLOW (P):
    /// Cruise = copy smoothed lead heading only (no trail lateral hunt → no weave).
    /// Terminal = latch onto shared impact; SetAimpoint writes the marker itself.
    /// Trail ring is distance-only (solo if left far behind the path).
    /// </summary>
    internal static class RcFormationFollow
    {
        private const float MinSpacingM = 45f;
        private const float MinAimDistM = 800f;
        private const float AimWriteDistM = 2800f;

        /// <summary>Start easing onto impact; latch hard below this.</summary>
        private const float TerminalStartM = 4500f;
        private const float TerminalLatchM = 2800f;
        private const float TerminalFullM = 1200f;

        private const float CatchUpExtraLagM = 400f;
        private const float MaxTrailLeaveM = 1800f;
        private const float FollowerOmegaScale = 0.55f;
        private const float TerminalOmegaScale = 1f;
        private const float LeadDirSmoothTau = 0.35f;
        private const float CatchUpBiasDeg = 6f;

        /// <summary>Within this range of last lead impact → hold that point; farther → own seeker.</summary>
        private const float TerminalHandoffM = 2800f;

        private static readonly FieldInfo? SeekerTargetField =
            typeof(MissileSeeker).GetField("targetUnit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private struct FollowerState
        {
            internal Missile Missile;
            internal float TrailBackM;
            internal bool WasAheadAtEngage;
            internal bool FinsDone;
            internal bool TangibleDone;
            internal bool ArmDone;
            internal Vector3 CmdDir;
            internal bool CmdInit;
            /// <summary>Once true — never return to trail/heading copy; fly the marker.</summary>
            internal bool ImpactLocked;
        }

        private static Missile? _lead;
        private static bool _active;
        private static Unit? _sharedTarget;
        private static Vector3 _lastImpactLocal;
        private static bool _hasLastImpact;
        private static bool _leadInTerminal;
        private static Vector3 _smoothLeadDir = Vector3.forward;
        private static bool _smoothLeadDirInit;
        private static readonly RcFormationTrail _trail = new RcFormationTrail();
        private static readonly List<FollowerState> _followers = new List<FollowerState>(16);
        private static readonly HashSet<int> _followerIds = new HashSet<int>(16);

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
            _sharedTarget = null;
            _hasLastImpact = false;
            _lastImpactLocal = Vector3.zero;
            _leadInTerminal = false;
            _smoothLeadDirInit = false;
            _trail.Clear();
            _followers.Clear();
            _followerIds.Clear();
            if (RemoteControlSession.Controlled != null)
                RcSeekSkipSet.Rebuild();
            else
                RcSeekSkipSet.Clear();
        }

        /// <summary>
        /// Lead lost / FOLLOW off:
        /// near last impact (terminal) → keep flying to that point;
        /// far → own GSN toward last shared lock.
        /// </summary>
        internal static void HandoffAndClear()
        {
            if (!_active)
            {
                Clear();
                return;
            }

            Unit? target = _sharedTarget;
            if (target != null && target.disabled)
                target = null;

            Vector3 impact = _hasLastImpact ? _lastImpactLocal : ResolveSharedImpact();
            bool haveImpact = impact.sqrMagnitude > 1f;
            bool leadTerminal = _leadInTerminal;

            int toImpact = 0;
            int toSeek = 0;

            for (int i = 0; i < _followers.Count; i++)
            {
                Missile? m = _followers[i].Missile;
                if (m == null || m.disabled)
                    continue;
                try
                {
                    float dist = haveImpact
                        ? Vector3.Distance(m.transform.position, impact)
                        : float.MaxValue;

                    // Lead already terminal → whole salvo holds impact; else per-missile range.
                    bool terminal = haveImpact && (leadTerminal || dist <= TerminalHandoffM);
                    if (terminal)
                    {
                        RcSeekerHandoff.CommitToImpactPoint(m, impact, target);
                        toImpact++;
                    }
                    else
                    {
                        SyncLock(m, target);
                        RcSeekerHandoff.CommitForAutonomous(m);
                        toSeek++;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            RcPlugin.ModLogger?.LogInfo(
                $"Formation handoff — impactHold={toImpact}, seeker={toSeek}, leadTerminal={leadTerminal}");
            Clear();
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
                HandoffAndClear();
                RcPlugin.ModLogger?.LogInfo("Formation follow OFF (wingmen keep last lock).");
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
            Vector3 leadPos = lead.transform.position;
            Vector3 axis = ResolveLeadDir(lead);

            _lead = lead;
            _active = true;
            _sharedTarget = MissileAccess.TryGetLockedTarget(lead);
            _trail.Clear();
            _trail.PushLead(leadPos);

            int n = 0;
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
                float along = Vector3.Dot(delta, axis);
                bool ahead = along > 0f;
                float back = Mathf.Max(MinSpacingM, Mathf.Abs(along));

                _followerIds.Add(m.GetInstanceID());
                Vector3 initDir = m.transform.forward.sqrMagnitude > 1e-6f
                    ? m.transform.forward.normalized
                    : axis;
                _followers.Add(new FollowerState
                {
                    Missile = m,
                    TrailBackM = back,
                    WasAheadAtEngage = ahead,
                    FinsDone = false,
                    TangibleDone = false,
                    ArmDone = false,
                    CmdDir = initDir,
                    CmdInit = true,
                    ImpactLocked = false
                });
                n++;
            }

            RcSeekSkipSet.Rebuild();
            RcPlugin.ModLogger?.LogInfo(
                $"Formation follow ON — lead={lead.unitName ?? lead.name}, wingmen={n} (heading copy + impact latch).");
        }

        internal static void Tick()
        {
            if (!_active)
                return;

            if (_lead == null || _lead.disabled || !AuthorityGate.CanControl(_lead)
                || !RemoteControlSession.OwnsMissile(_lead))
            {
                HandoffAndClear();
                return;
            }

            _trail.PushLead(_lead.transform.position);
            CacheLastImpact();
            PruneDead();

            Unit? leadTarget = MissileAccess.TryGetLockedTarget(_lead);
            if (leadTarget != null && !leadTarget.disabled)
                _sharedTarget = leadTarget;
            else if (_sharedTarget != null && _sharedTarget.disabled)
                _sharedTarget = null;

            bool leadBoost = ThrottleController.BoostActive;

            for (int i = _followers.Count - 1; i >= 0; i--)
            {
                FollowerState f = _followers[i];
                Missile m = f.Missile;
                if (m == null || m.disabled)
                    continue;

                try
                {
                    if (ShouldSolo(m))
                    {
                        ReleaseSolo(m, i);
                        continue;
                    }

                    EnsureWarhead(ref f);
                    _followers[i] = f;

                    SyncLock(m, _sharedTarget);
                    RcSeekerHandoff.PrepareSeekerState(m);

                    try { m.SetThrottle(1f); }
                    catch { /* ignore */ }

                    AfterburnerVfxBinder.SetBoost(m, leadBoost && MissileAccess.HasMotorFuel(m));
                    RcSeekerSuppress.Tick(m);
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// Cruise: copy lead heading (optionally tiny catch-up toward lead).
        /// Terminal latch: LOS to shared impact; aimpoint = marker (not far phantom / trail).
        /// </summary>
        internal static void ReinforceAimpoint(Missile missile)
        {
            if (_lead == null || !IsFollower(missile))
                return;

            try
            {
                int idx = -1;
                for (int i = 0; i < _followers.Count; i++)
                {
                    if (!ReferenceEquals(_followers[i].Missile, missile))
                        continue;
                    idx = i;
                    break;
                }

                if (idx < 0)
                    return;

                FollowerState f = _followers[idx];
                Vector3 me = missile.transform.position;
                Vector3 leadPos = _lead.transform.position;
                Vector3 impact = ResolveSharedImpact();
                Vector3 leadDir = SmoothLeadDir(ResolveLeadDir(_lead));

                float distImp = impact.sqrMagnitude > 1f ? Vector3.Distance(me, impact) : float.MaxValue;
                float leadDistImp = impact.sqrMagnitude > 1f
                    ? Vector3.Distance(leadPos, impact)
                    : float.MaxValue;

                // Latch early and stay latched — never resume trail/heading after commit.
                if (!f.ImpactLocked && impact.sqrMagnitude > 1f
                    && (distImp <= TerminalLatchM || leadDistImp <= TerminalLatchM || _leadInTerminal))
                {
                    f.ImpactLocked = true;
                }

                float terminalT = f.ImpactLocked ? 1f : TerminalBlend(distImp);
                // Lagging wingmen start committing sooner so they don't miss the marker on the trail.
                if (!f.ImpactLocked && distImp > leadDistImp + CatchUpExtraLagM)
                    terminalT = Mathf.Max(terminalT, 0.45f);

                float dt = Mathf.Max(Time.fixedDeltaTime, 1f / 120f);
                float omegaScale = Mathf.Lerp(FollowerOmegaScale, TerminalOmegaScale, terminalT);
                float omega = MissileAccess.GetMaxTurnRateRad(missile) * omegaScale;
                float maxRad = Mathf.Max(omega * dt, 1e-5f);

                Vector3 desiredDir;
                if (f.ImpactLocked || terminalT >= 0.85f)
                {
                    desiredDir = (impact - me).normalized;
                    f.ImpactLocked = true;
                }
                else if (terminalT > 0.01f && impact.sqrMagnitude > 1f)
                {
                    // Blend cruise heading → impact LOS (no trail).
                    Vector3 cruise = ResolveCruiseDesiredDir(me, leadPos, leadDir, leadDistImp, distImp);
                    Vector3 impDir = (impact - me).normalized;
                    desiredDir = Vector3.Slerp(cruise, impDir, terminalT).normalized;
                }
                else
                {
                    desiredDir = ResolveCruiseDesiredDir(me, leadPos, leadDir, leadDistImp, distImp);
                }

                Vector3 cmd = f.CmdInit && f.CmdDir.sqrMagnitude > 1e-8f
                    ? f.CmdDir.normalized
                    : (missile.transform.forward.sqrMagnitude > 1e-6f
                        ? missile.transform.forward.normalized
                        : leadDir);

                Vector3 next = Vector3.RotateTowards(cmd, desiredDir, maxRad, 0f).normalized;

                Vector3 nose = missile.transform.forward;
                if (nose.sqrMagnitude > 1e-6f && Vector3.Dot(next, nose) < 0.05f)
                    next = nose.normalized;

                f.CmdDir = next;
                f.CmdInit = true;
                _followers[idx] = f;

                Vector3 vel = Vector3.zero;
                try
                {
                    if (_lead.rb != null)
                        vel = _lead.rb.velocity;
                }
                catch { /* ignore */ }

                // Locked / deep terminal: write the marker. Far phantom along cmd was why they rode the trail past it.
                Vector3 aim;
                if (f.ImpactLocked || terminalT >= 0.5f)
                {
                    aim = impact;
                }
                else if (terminalT > 0.01f)
                {
                    // Stay on the ray to impact — never beyond the marker.
                    float along = Mathf.Min(AimWriteDistM, Mathf.Max(distImp, MinAimDistM));
                    aim = Vector3.Lerp(me + next * AimWriteDistM, me + (impact - me).normalized * along, terminalT);
                }
                else
                {
                    aim = me + next * AimWriteDistM;
                }

                missile.SetAimpoint(aim.ToGlobalPosition(), vel);
            }
            catch
            {
                // ignore
            }
        }

        private static Vector3 SmoothLeadDir(Vector3 raw)
        {
            if (raw.sqrMagnitude < 1e-8f)
                return _smoothLeadDirInit ? _smoothLeadDir : Vector3.forward;
            raw.Normalize();
            if (!_smoothLeadDirInit)
            {
                _smoothLeadDir = raw;
                _smoothLeadDirInit = true;
                return raw;
            }

            float dt = Mathf.Max(Time.fixedDeltaTime, 1f / 120f);
            float a = 1f - Mathf.Exp(-dt / LeadDirSmoothTau);
            _smoothLeadDir = Vector3.Slerp(_smoothLeadDir, raw, a).normalized;
            return _smoothLeadDir;
        }

        /// <summary>Heading copy only — no nearest-trail lateral pull (that was the weave).</summary>
        private static Vector3 ResolveCruiseDesiredDir(
            Vector3 me,
            Vector3 leadPos,
            Vector3 leadDir,
            float leadDistImp,
            float distImp)
        {
            Vector3 dir = leadDir;
            if (distImp <= leadDistImp + CatchUpExtraLagM)
                return dir;

            // Soft catch-up toward lead when far behind on the approach — capped, no trail hunt.
            Vector3 toLead = leadPos - me;
            if (toLead.sqrMagnitude < 1f)
                return dir;
            return Vector3.RotateTowards(dir, toLead.normalized, CatchUpBiasDeg * Mathf.Deg2Rad, 0f);
        }

        private static float TerminalBlend(float distToImpact)
        {
            if (distToImpact <= TerminalFullM)
                return 1f;
            if (distToImpact >= TerminalStartM)
                return 0f;
            float u = 1f - Mathf.InverseLerp(TerminalFullM, TerminalStartM, distToImpact);
            return u * u * (3f - 2f * u);
        }

        private static void CacheLastImpact()
        {
            Vector3 impact = ResolveSharedImpact();
            if (impact.sqrMagnitude < 1f)
                return;
            _lastImpactLocal = impact;
            _hasLastImpact = true;

            if (_lead == null)
                return;
            try
            {
                if (!_lead.disabled)
                    _leadInTerminal = Vector3.Distance(_lead.transform.position, impact) <= TerminalHandoffM;
            }
            catch
            {
                // keep previous _leadInTerminal
            }
        }

        private static Vector3 ResolveSharedImpact()
        {
            if (MouseGuidanceController.TryGetLastAimLocal(out Vector3 cached) && cached.sqrMagnitude > 1f)
                return cached;

            if (_lead != null && MissileAccess.TryGetAimLocal(_lead, out Vector3 fromLead) && fromLead.sqrMagnitude > 1f)
                return fromLead;

            if (_lead == null)
                return Vector3.zero;

            Vector3 dir = ResolveLeadDir(_lead);
            float dist = Mathf.Max(MinAimDistM, RcConfig.AimDistance.Value);
            return RcBallisticImpactSafety.ResolveAimPoint(_lead.transform.position, dir, dist);
        }

        private static Vector3 ResolveLeadDir(Missile lead)
        {
            Vector3 cmd = MouseGuidanceController.WorldAimDir;
            if (cmd.sqrMagnitude > 1e-6f)
                return cmd.normalized;

            try
            {
                if (lead.rb != null && lead.rb.velocity.sqrMagnitude > 25f)
                    return lead.rb.velocity.normalized;
            }
            catch { /* ignore */ }

            Vector3 fwd = lead.transform.forward;
            return fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
        }

        private static bool ShouldSolo(Missile m)
        {
            if (_trail.Count < 2)
                return false;
            return _trail.NearestDistance(m.transform.position) > MaxTrailLeaveM;
        }

        private static void ReleaseSolo(Missile m, int index)
        {
            Unit? target = _sharedTarget;
            if (target != null && target.disabled)
                target = null;

            try
            {
                SyncLock(m, target);
                RcSeekerHandoff.CommitForAutonomous(m);
                AfterburnerVfxBinder.SetBoost(m, false);
            }
            catch { /* ignore */ }

            if (index >= 0 && index < _followers.Count)
                _followers.RemoveAt(index);
            RebuildIds();
            RcSeekSkipSet.Rebuild();
            RcPlugin.ModLogger?.LogInfo(
                $"Formation solo → {m.unitName ?? m.name} (left lead trail, seeking shared target).");
        }

        private static void SyncLock(Missile follower, Unit? target)
        {
            try { follower.SetTarget(target); }
            catch { /* ignore */ }

            if (SeekerTargetField == null)
                return;
            try
            {
                MissileSeeker? seeker = MissileAccess.GetSeeker(follower) ?? follower.GetComponent<MissileSeeker>();
                if (seeker != null)
                    SeekerTargetField.SetValue(seeker, target);
            }
            catch { /* ignore */ }
        }

        private static void EnsureWarhead(ref FollowerState f)
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
                try { m.DeployFins(); f.FinsDone = true; }
                catch { /* retry */ }
            }

            if (!f.TangibleDone && RcTangibleEnsure.TrySet(m))
                f.TangibleDone = true;

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

        private static void PruneDead()
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
            RebuildIds();
            RcSeekSkipSet.Rebuild();
        }

        private static void RebuildIds()
        {
            _followerIds.Clear();
            for (int i = 0; i < _followers.Count; i++)
            {
                Missile? m = _followers[i].Missile;
                if (m != null)
                    _followerIds.Add(m.GetInstanceID());
            }
        }
    }
}
