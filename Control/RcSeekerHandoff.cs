using System.Reflection;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// After RC retarget / release: sync seeker mid-course state so autonomous Seek
    /// flies to the unit chosen under RC (not the launch-time knownPos).
    /// </summary>
    internal static class RcSeekerHandoff
    {
        private static readonly FieldInfo? SeekerTargetField =
            typeof(MissileSeeker).GetField("targetUnit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? MissileTargetField =
            typeof(Missile).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? CruiseKnownPos =
            typeof(OpticalSeekerCruiseMissile).GetField("knownPos", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseAimPos =
            typeof(OpticalSeekerCruiseMissile).GetField("aimPos", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseKnownVel =
            typeof(OpticalSeekerCruiseMissile).GetField("knownVel", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseTerminal =
            typeof(OpticalSeekerCruiseMissile).GetField("terminalMode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseGuidance =
            typeof(OpticalSeekerCruiseMissile).GetField("guidance", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseTargetHq =
            typeof(OpticalSeekerCruiseMissile).GetField("targetHQAtLaunch", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseTargetPart =
            typeof(OpticalSeekerCruiseMissile).GetField("targetPart", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? BallisticKnownPos =
            typeof(BallisticMissileGuidance).GetField("knownPos", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? BallisticKnownVel =
            typeof(BallisticMissileGuidance).GetField("knownVel", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// While still under RC: sync seeker mid-course fields for later release.
        /// Never writes SetAimpoint (mouse owns aim).
        /// </summary>
        internal static void PrepareSeekerState(Missile missile)
        {
            ApplyInternal(missile, writeAimpoint: false, log: false);
        }

        /// <summary>On RC release: sync seeker + SetAimpoint so Seek resumes toward lock.</summary>
        internal static void CommitForAutonomous(Missile missile)
        {
            ApplyInternal(missile, writeAimpoint: true, log: true);
        }

        private static void ApplyInternal(Missile missile, bool writeAimpoint, bool log)
        {
            if (missile == null || missile.disabled)
                return;

            Unit? unit = TryGetMissileTarget(missile);
            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            if (seeker == null)
                return;

            try
            {
                SeekerTargetField?.SetValue(seeker, unit);
            }
            catch
            {
                // ignore
            }

            // Impact-only RC: never force-detonate on release. Dead/cleared lock → resume Seek / SD blocked by gate.
            if (unit == null || unit.disabled)
            {
                if (writeAimpoint)
                {
                    try
                    {
                        if (seeker is OpticalSeekerCruiseMissile cruise)
                            CruiseGuidance?.SetValue(cruise, true);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                return;
            }

            GlobalPosition known = ResolveKnownPosition(missile, unit);
            Vector3 knownVel = Vector3.zero;
            try
            {
                if (unit.rb != null)
                    knownVel = unit.rb.velocity;
            }
            catch
            {
                // ignore
            }

            try
            {
                if (seeker is OpticalSeekerCruiseMissile cruise)
                    ApplyCruise(missile, cruise, unit, known, knownVel, writeAimpoint);
                else if (seeker is BallisticMissileGuidance ballistic)
                    ApplyBallistic(missile, ballistic, known, knownVel, writeAimpoint);
                else if (writeAimpoint)
                    ApplyGeneric(missile, known, knownVel);
            }
            catch
            {
                if (writeAimpoint)
                    ApplyGeneric(missile, known, knownVel);
            }

            // Never arm proxy on RC clones (impact fuse only — SetProxyFuse also Harmony-blocked).
            if (!writeAimpoint)
                Access.MissileAccess.ClearProxyFuse(missile);

            if (log)
            {
                RcPlugin.ModLogger?.LogInfo(
                    $"RC handoff → autonomous toward {unit.unitName ?? unit.name}");
            }
        }

        private static void ApplyCruise(
            Missile missile,
            OpticalSeekerCruiseMissile cruise,
            Unit unit,
            GlobalPosition known,
            Vector3 knownVel,
            bool writeAimpoint)
        {
            CruiseKnownPos?.SetValue(cruise, known);
            CruiseAimPos?.SetValue(cruise, known);
            CruiseKnownVel?.SetValue(cruise, knownVel);
            CruiseTerminal?.SetValue(cruise, false);
            // Under RC (prepare): guidance stays false so leaked Seek cannot PreTerminal/Terminal.
            // On release commit: guidance true so autonomous Seek resumes.
            CruiseGuidance?.SetValue(cruise, writeAimpoint);
            CruiseTargetPart?.SetValue(cruise, null);
            try
            {
                CruiseTargetHq?.SetValue(cruise, unit.NetworkHQ);
            }
            catch
            {
                // ignore
            }

            if (writeAimpoint)
                missile.SetAimpoint(known, knownVel);
        }

        private static void ApplyBallistic(
            Missile missile,
            BallisticMissileGuidance ballistic,
            GlobalPosition known,
            Vector3 knownVel,
            bool writeAimpoint)
        {
            BallisticKnownPos?.SetValue(ballistic, known);
            BallisticKnownVel?.SetValue(ballistic, knownVel);
            if (writeAimpoint)
                missile.SetAimpoint(known, knownVel);
        }

        private static void ApplyGeneric(Missile missile, GlobalPosition known, Vector3 knownVel)
        {
            try
            {
                missile.SetAimpoint(known, knownVel);
            }
            catch
            {
                // ignore
            }
        }

        private static GlobalPosition ResolveKnownPosition(Missile missile, Unit unit)
        {
            try
            {
                if (missile.NetworkHQ != null)
                {
                    if (missile.NetworkHQ.TryGetKnownPosition(unit, out GlobalPosition gp))
                        return gp;
                    GlobalPosition? opt = missile.NetworkHQ.GetKnownPosition(unit);
                    if (opt != null)
                        return opt.Value;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                return unit.GlobalPosition();
            }
            catch
            {
                return missile.GlobalPosition() + missile.transform.forward * 5000f;
            }
        }

        private static Unit? TryGetMissileTarget(Missile missile)
        {
            try
            {
                if (MissileTargetField?.GetValue(missile) is Unit u && u != null && !u.disabled)
                    return u;
            }
            catch
            {
                // ignore
            }

            try
            {
                MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
                if (seeker != null && SeekerTargetField?.GetValue(seeker) is Unit su && su != null && !su.disabled)
                    return su;
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
