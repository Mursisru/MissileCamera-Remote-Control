using System.Collections.Generic;
using System.Reflection;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// While RC: force cruise seeker into inert mid-course state.
    /// Latch per missile after first successful write (Seek is skipped — fields stay false).
    /// </summary>
    internal static class RcSeekerSuppress
    {
        private static readonly FieldInfo? CruiseTerminal =
            typeof(OpticalSeekerCruiseMissile).GetField("terminalMode", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CruiseGuidance =
            typeof(OpticalSeekerCruiseMissile).GetField("guidance", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<int, OpticalSeekerCruiseMissile?> _cruiseById =
            new Dictionary<int, OpticalSeekerCruiseMissile?>(8);

        private static readonly HashSet<int> _inertIds = new HashSet<int>(8);

        internal static void Reset()
        {
            _cruiseById.Clear();
            _inertIds.Clear();
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            int id = missile.GetInstanceID();
            if (_inertIds.Contains(id))
                return;

            OpticalSeekerCruiseMissile? cruise = ResolveCruise(missile, id);
            if (cruise == null)
            {
                _inertIds.Add(id);
                return;
            }

            try
            {
                // Direct SetValue — Seek skip means they stay false; avoid GetValue boxing.
                CruiseTerminal?.SetValue(cruise, false);
                CruiseGuidance?.SetValue(cruise, false);
                _inertIds.Add(id);
            }
            catch
            {
                // ignore
            }
        }

        private static OpticalSeekerCruiseMissile? ResolveCruise(Missile missile, int id)
        {
            if (_cruiseById.TryGetValue(id, out OpticalSeekerCruiseMissile? cached))
                return cached;

            OpticalSeekerCruiseMissile? cruise = null;
            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            if (seeker is OpticalSeekerCruiseMissile c)
                cruise = c;
            _cruiseById[id] = cruise;
            return cruise;
        }
    }
}
