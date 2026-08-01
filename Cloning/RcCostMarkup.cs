using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>
    /// RC munitions +10% vs stock. WeaponMount.Initialize always resets costPerRound from the
    /// shared vanilla missile definition.value — re-apply after every Initialize.
    /// </summary>
    internal static class RcCostMarkup
    {
        internal const float Factor = 1.1f;

        internal static void Ensure(WeaponMount? mount)
        {
            if (mount == null || mount.info == null)
                return;
            if (!IsRcMount(mount))
                return;

            float baseRound = ResolveStockRoundCost(mount);
            if (baseRound > 0f)
            {
                try
                {
                    mount.info.SetCostPerRound(baseRound * Factor);
                }
                catch
                {
                    mount.info.costPerRound = baseRound * Factor;
                }
            }

            float baseEmpty = ResolveStockEmptyCost(mount, out bool haveEmpty);
            if (haveEmpty)
                mount.emptyCost = baseEmpty * Factor;
        }

        internal static void EnsureDefinition(UnitDefinition? defClone, UnitDefinition? srcDef)
        {
            if (defClone == null || srcDef == null)
                return;
            try
            {
                defClone.value = srcDef.value * Factor;
            }
            catch
            {
                // ignore
            }
        }

        internal static bool IsRcMount(WeaponMount? mount)
        {
            if (mount == null)
                return false;

            string key = mount.jsonKey ?? string.Empty;
            if (CloneProfile.IsRcCloneKey(key))
                return true;

            WeaponInfo? info = mount.info;
            if (info == null || string.IsNullOrEmpty(info.weaponName))
                return false;

            return CloneProfile.IsRcDisplayName(info.weaponName)
                || CloneProfile.TryGetGuidanceFromRcName(info.weaponName, out _);
        }

        private static float ResolveStockRoundCost(WeaponMount mount)
        {
            // Shared flying prefab → stock definition.value (same source Initialize uses).
            try
            {
                WeaponInfo? info = mount.info;
                if (info != null && info.weaponPrefab != null)
                {
                    Missile? missile = info.weaponPrefab.GetComponent<Missile>()
                        ?? info.weaponPrefab.GetComponentInChildren<Missile>(true);
                    if (missile != null && missile.definition != null && missile.definition.value > 0f)
                        return missile.definition.value;
                }
            }
            catch
            {
                // fall through
            }

            WeaponMount? original = TryResolveOriginal(mount);
            try
            {
                if (original?.info != null && original.info.costPerRound > 0f)
                    return original.info.costPerRound;
            }
            catch
            {
                // ignore
            }

            try
            {
                return mount.info != null ? mount.info.costPerRound : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        private static float ResolveStockEmptyCost(WeaponMount mount, out bool haveEmpty)
        {
            haveEmpty = false;
            WeaponMount? original = TryResolveOriginal(mount);
            if (original == null)
                return 0f;

            try
            {
                haveEmpty = true;
                return original.emptyCost;
            }
            catch
            {
                haveEmpty = false;
                return 0f;
            }
        }

        private static WeaponMount? TryResolveOriginal(WeaponMount clone)
        {
            if (CloneRegistry.TryGetOriginal(clone, out WeaponMount? original) && original != null)
                return original;

            string? sourceKey = null;
            try
            {
                if (clone.prefab != null)
                {
                    RcMountMeta? meta = clone.prefab.GetComponent<RcMountMeta>()
                        ?? clone.prefab.GetComponentInChildren<RcMountMeta>(true);
                    if (meta != null && !string.IsNullOrEmpty(meta.SourceMountKey))
                        sourceKey = meta.SourceMountKey;
                }
            }
            catch
            {
                // ignore
            }

            if (string.IsNullOrEmpty(sourceKey))
                return null;

            try
            {
                if (Encyclopedia.WeaponLookup != null
                    && Encyclopedia.WeaponLookup.TryGetValue(sourceKey!, out WeaponMount stock)
                    && stock != null)
                    return stock;
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
