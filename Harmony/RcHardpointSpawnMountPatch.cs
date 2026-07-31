using HarmonyLib;
using MissileCameraRemoteControl.Cloning;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// RC mount templates are DDOL + SetActive(false). Vanilla Instantiate copies inactive → invisible pylons.
    /// Briefly activate the template only for SpawnMount, then hide the template again (not the spawn).
    /// </summary>
    [HarmonyPatch(typeof(Hardpoint), nameof(Hardpoint.SpawnMount))]
    internal static class RcHardpointSpawnMountPatch
    {
        private static void Prefix(WeaponMount weaponMount, out bool __state)
        {
            __state = false;
            try
            {
                if (weaponMount == null || weaponMount.prefab == null)
                    return;
                if (!IsRcMount(weaponMount))
                    return;
                if (weaponMount.prefab.activeSelf)
                    return;

                weaponMount.prefab.SetActive(true);
                __state = true;
            }
            catch
            {
                __state = false;
            }
        }

        private static void Postfix(WeaponMount weaponMount, bool __state)
        {
            if (!__state || weaponMount == null || weaponMount.prefab == null)
                return;
            try
            {
                // Hide TEMPLATE only — spawned instance stays active and visible.
                weaponMount.prefab.SetActive(false);
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsRcMount(WeaponMount mount)
        {
            if (mount == null)
                return false;

            string key = mount.jsonKey ?? string.Empty;
            if (key.IndexOf("_RC_DL", System.StringComparison.Ordinal) >= 0
                || key.IndexOf("_RC_SAT", System.StringComparison.Ordinal) >= 0)
                return true;

            WeaponInfo? info = mount.info;
            if (info == null || string.IsNullOrEmpty(info.weaponName))
                return false;

            return CloneProfile.IsRcDisplayName(info.weaponName)
                || CloneProfile.TryGetGuidanceFromRcName(info.weaponName, out _);
        }
    }
}
