using System.Reflection;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// While RC: force cruise seeker into inert mid-course state.
    /// Write fields only when values differ (Steering prefix path).
    /// </summary>
    internal static class RcSeekerSuppress
    {
        private static readonly FieldInfo? CruiseTerminal =
            typeof(OpticalSeekerCruiseMissile).GetField("terminalMode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseGuidance =
            typeof(OpticalSeekerCruiseMissile).GetField("guidance", BindingFlags.Instance | BindingFlags.NonPublic);

        private static int _cachedMissileId;
        private static OpticalSeekerCruiseMissile? _cachedCruise;

        internal static void Reset()
        {
            _cachedMissileId = 0;
            _cachedCruise = null;
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            OpticalSeekerCruiseMissile? cruise = ResolveCruise(missile);
            if (cruise == null)
                return;

            try
            {
                if (CruiseTerminal != null && CruiseTerminal.GetValue(cruise) is true)
                    CruiseTerminal.SetValue(cruise, false);
                if (CruiseGuidance != null && CruiseGuidance.GetValue(cruise) is true)
                    CruiseGuidance.SetValue(cruise, false);
            }
            catch
            {
                // ignore
            }
        }

        private static OpticalSeekerCruiseMissile? ResolveCruise(Missile missile)
        {
            int id = missile.GetInstanceID();
            if (id == _cachedMissileId && _cachedCruise != null)
                return _cachedCruise;

            _cachedMissileId = id;
            _cachedCruise = null;
            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            if (seeker is OpticalSeekerCruiseMissile cruise)
                _cachedCruise = cruise;
            return _cachedCruise;
        }
    }
}
