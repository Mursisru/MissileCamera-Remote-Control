using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using NuclearOption.SavedMission;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>
    /// AI path A: equip RC clones instead of vanilla whitelist mounts.
    /// Bots fire/seek normally — no remote-pilot (Seek skip only while player OwnsMissile).
    /// </summary>
    internal static class RcAiLoadout
    {
        internal static bool Enabled =>
            RcConfig.Enabled.Value
            && RcConfig.AiEquipRcClones.Value
            && RcServerCompat.FeaturesAllowed
            && CloneRegistry.Pairs.Count > 0;

        /// <summary>True for host-sim AI aircraft (no human Player).</summary>
        internal static bool IsAiAircraft(Aircraft? aircraft)
        {
            if (aircraft == null || aircraft.disabled)
                return false;
            try
            {
                return aircraft.Player == null;
            }
            catch
            {
                return false;
            }
        }

        internal static bool ShouldSwap(Aircraft? aircraft) =>
            Enabled && IsAiAircraft(aircraft);

        /// <summary>Swap whitelisted original → RC clone. Leaves already-RC mounts alone.</summary>
        internal static bool TryRemapMount(WeaponMount? mount, out WeaponMount remapped)
        {
            remapped = mount!;
            if (mount == null)
                return false;

            if (!CloneRegistry.TryResolveClone(mount, out WeaponMount? clone) || clone == null)
                return false;

            remapped = clone;
            return !ReferenceEquals(mount, clone);
        }

        /// <summary>Mutates a disposable Loadout list (never shared StandardLoadout assets).</summary>
        internal static int RemapLoadout(Loadout? loadout)
        {
            if (loadout == null || loadout.weapons == null || loadout.weapons.Count == 0)
                return 0;

            int swapped = 0;
            for (int i = 0; i < loadout.weapons.Count; i++)
            {
                WeaponMount? mount = loadout.weapons[i];
                if (!TryRemapMount(mount, out WeaponMount remapped))
                    continue;
                loadout.weapons[i] = remapped;
                swapped++;
            }

            return swapped;
        }
    }
}
