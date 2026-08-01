using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Cloning;
using NuclearOption.SavedMission;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// AI aircraft: spawn RC clones on hardpoints that would have carried the vanilla whitelist mount.
    /// Equal CombatAI priority to stock (no opportunity bias / hangar prefer filter).
    /// Does not mutate shared StandardLoadout ScriptableObjects — only the mount arg / new Loadout lists.
    /// </summary>
    [HarmonyPatch(typeof(WeaponManager), "LoadHardpointSet")]
    internal static class RcAiLoadHardpointSetPatch
    {
        private static readonly FieldInfo? AircraftField =
            typeof(WeaponManager).GetField("aircraft", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Prefix(WeaponManager __instance, ref WeaponMount weaponMount)
        {
            try
            {
                if (__instance == null || weaponMount == null)
                    return;
                if (!RcAiLoadout.ShouldSwap(ResolveAircraft(__instance)))
                    return;
                if (RcAiLoadout.TryRemapMount(weaponMount, out WeaponMount remapped))
                    weaponMount = remapped;
            }
            catch
            {
                // keep vanilla mount
            }
        }

        internal static Aircraft? ResolveAircraft(WeaponManager wm)
        {
            if (wm == null)
                return null;
            try
            {
                if (AircraftField != null)
                {
                    var ac = AircraftField.GetValue(wm) as Aircraft;
                    if (ac != null)
                        return ac;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                return wm.GetComponentInParent<Aircraft>() ?? wm.GetComponent<Aircraft>();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Hangar AI random loadout — remap after pick so Networkloadout stores RC keys.</summary>
    [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.SelectAIAircraftWeapons))]
    internal static class RcAiSelectWeaponsPatch
    {
        private static void Postfix(WeaponManager __instance, ref Loadout __result)
        {
            try
            {
                if (__instance == null || __result == null)
                    return;
                if (!RcAiLoadout.ShouldSwap(RcAiLoadHardpointSetPatch.ResolveAircraft(__instance)))
                    return;
                RcAiLoadout.RemapLoadout(__result);
            }
            catch
            {
                // ignore
            }
        }
    }
}
