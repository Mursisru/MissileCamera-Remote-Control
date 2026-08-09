using System.Collections.Generic;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Hot-path: Seek Prefix checks seeker instance IDs only (no FieldInfo on world missiles).
    /// Rebuilt on Take / Release / formation Engage / Clear.
    /// </summary>
    internal static class RcSeekSkipSet
    {
        private static readonly HashSet<int> _seekerIds = new HashSet<int>(16);
        private static readonly HashSet<int> _missileIds = new HashSet<int>(16);

        internal static bool HasAny => _missileIds.Count > 0;

        internal static bool ShouldSkipSeeker(MissileSeeker? seeker)
        {
            if (seeker == null || _seekerIds.Count == 0)
                return false;
            return _seekerIds.Contains(seeker.GetInstanceID());
        }

        internal static bool ContainsMissile(Missile? missile)
        {
            if (missile == null || _missileIds.Count == 0)
                return false;
            return _missileIds.Contains(missile.GetInstanceID());
        }

        internal static void Clear()
        {
            _seekerIds.Clear();
            _missileIds.Clear();
        }

        internal static void Rebuild()
        {
            Clear();
            Missile? lead = RemoteControlSession.Controlled;
            if (lead != null && !lead.disabled)
                AddMissile(lead);

            RcFormationFollow.AppendFollowerMissiles(AddMissile);
        }

        private static void AddMissile(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            _missileIds.Add(missile.GetInstanceID());

            try
            {
                MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
                if (seeker != null)
                    _seekerIds.Add(seeker.GetInstanceID());
            }
            catch
            {
                // ignore
            }
        }
    }
}
