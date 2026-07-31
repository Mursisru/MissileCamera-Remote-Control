using System.Reflection;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// While RC: force cruise seeker into inert mid-course state.
    /// If Seek ever leaks (Harmony miss / IsActive flicker), PreTerminal/Terminal must not run —
    /// that is what steals the stick when lining up inside terminalRange (~2 km).
    /// </summary>
    internal static class RcSeekerSuppress
    {
        private static readonly FieldInfo? CruiseTerminal =
            typeof(OpticalSeekerCruiseMissile).GetField("terminalMode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseGuidance =
            typeof(OpticalSeekerCruiseMissile).GetField("guidance", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            if (seeker is not OpticalSeekerCruiseMissile cruise)
                return;

            try
            {
                CruiseTerminal?.SetValue(cruise, false);
                // guidance=false → Seek early-outs before PreTerminal/Terminal SetAimpoint.
                CruiseGuidance?.SetValue(cruise, false);
            }
            catch
            {
                // ignore
            }
        }
    }
}
