using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>
    /// WeaponManager stacks stations by WeaponInfo reference equality.
    /// Reuse one WeaponInfo SO per RC display name so AGM-98D on different rack keys stacks as one station.
    /// </summary>
    internal static class RcSharedWeaponInfo
    {
        private static readonly Dictionary<string, WeaponInfo> ByStackKey =
            new Dictionary<string, WeaponInfo>(StringComparer.Ordinal);

        internal static void Clear() => ByStackKey.Clear();

        internal static WeaponInfo GetOrCreate(
            WeaponInfo originalInfo,
            string displayName,
            string description,
            string instanceNameSuffix)
        {
            if (originalInfo == null)
                throw new ArgumentNullException(nameof(originalInfo));
            if (string.IsNullOrEmpty(displayName))
                displayName = "RC Munition";

            if (ByStackKey.TryGetValue(displayName, out WeaponInfo? shared) && shared != null)
                return shared;

            WeaponInfo infoClone = UnityEngine.Object.Instantiate(originalInfo);
            infoClone.name = originalInfo.name + instanceNameSuffix;
            infoClone.weaponName = displayName;
            infoClone.shortName = displayName;
            infoClone.description = description;
            // CRITICAL: keep vanilla flying prefab — Instantiated NetworkIdentity despawns on Spawn().
            infoClone.weaponPrefab = originalInfo.weaponPrefab;

            ByStackKey[displayName] = infoClone;
            return infoClone;
        }
    }
}
