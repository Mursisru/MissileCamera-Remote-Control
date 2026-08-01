using HarmonyLib;
using MissileCameraRemoteControl.Cloning;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Vanilla Initialize sets costPerRound = shared missile definition.value (stock).
    /// Re-apply RC +10% whenever that runs.
    /// </summary>
    [HarmonyPatch(typeof(WeaponMount), nameof(WeaponMount.Initialize))]
    internal static class RcWeaponMountCostPatch
    {
        private static void Postfix(WeaponMount __instance)
        {
            try
            {
                RcCostMarkup.Ensure(__instance);
            }
            catch
            {
                // ignore
            }
        }
    }
}
