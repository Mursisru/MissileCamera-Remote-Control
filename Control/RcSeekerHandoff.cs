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

        /// <summary>Call on RC release (and after Assign) so Seek resumes toward current lock.</summary>
        internal static void CommitForAutonomous(Missile missile)
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

            if (unit == null || unit.disabled)
            {
                // Keep current aimpoint as last RC stick direction — already set by MouseGuidance.
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
                    ApplyCruise(missile, cruise, unit, known, knownVel);
                else if (seeker is BallisticMissileGuidance ballistic)
                    ApplyBallistic(missile, ballistic, known, knownVel);
                else
                    ApplyGeneric(missile, known, knownVel);
            }
            catch
            {
                ApplyGeneric(missile, known, knownVel);
            }

            try
            {
                Transform? tf = unit.transform;
                Rigidbody? rb = null;
                try { rb = unit.rb; }
                catch { rb = unit.GetComponent<Rigidbody>(); }
                if (tf != null)
                    missile.SetProxyFuse(tf, rb);
            }
            catch
            {
                // ignore
            }

            RcPlugin.ModLogger?.LogInfo(
                $"RC handoff → autonomous toward {unit.unitName ?? unit.name}");
        }

        private static void ApplyCruise(
            Missile missile,
            OpticalSeekerCruiseMissile cruise,
            Unit unit,
            GlobalPosition known,
            Vector3 knownVel)
        {
            CruiseKnownPos?.SetValue(cruise, known);
            CruiseAimPos?.SetValue(cruise, known);
            CruiseKnownVel?.SetValue(cruise, knownVel);
            CruiseTerminal?.SetValue(cruise, false);
            CruiseGuidance?.SetValue(cruise, true);
            CruiseTargetPart?.SetValue(cruise, null);
            try
            {
                CruiseTargetHq?.SetValue(cruise, unit.NetworkHQ);
            }
            catch
            {
                // ignore
            }

            missile.SetAimpoint(known, knownVel);
        }

        private static void ApplyBallistic(
            Missile missile,
            BallisticMissileGuidance ballistic,
            GlobalPosition known,
            Vector3 knownVel)
        {
            BallisticKnownPos?.SetValue(ballistic, known);
            BallisticKnownVel?.SetValue(ballistic, knownVel);
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
