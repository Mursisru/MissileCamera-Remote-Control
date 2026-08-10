using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// Close heavy blast cook-off for RC (same-owner TakeDamage early-outs in vanilla).
    /// Soft / distant TakeShockwave must NOT Force — that was mid-flight premature detonations.
    /// </summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.TakeDamage))]
    internal static class RcBlastCookoffTakeDamagePatch
    {
        /// <summary>Shockwave blastDamage scale — ignore weak / fringe hits.</summary>
        private const float MinLethalBlast = 35f;
        private const float MinAmountAffected = 0.35f;

        private static bool Prefix(
            Missile __instance,
            float pierceDamage,
            float blastDamage,
            float amountAffected,
            float fireDamage,
            float impactDamage,
            PersistentID dealerID)
        {
            try
            {
                if (__instance == null || __instance.disabled)
                    return true;
                if (!MissileAccess.IsRcMissile(__instance))
                    return true;
                if (blastDamage < MinLethalBlast || amountAffected < MinAmountAffected)
                    return true;

                // Formation wingmen must NOT chain-detonate when the lead (or another ally RC) cooks off.
                if (RcFormationFollow.IsFollower(__instance))
                    return true;

                // Same-owner / self / invalid dealer: vanilla returns before Detonate.
                bool blockedDealer = dealerID.NotValid
                    || dealerID == __instance.persistentID
                    || dealerID == __instance.ownerID;
                if (!blockedDealer)
                    return true;

                RcDetonateUtil.Force(__instance);
                return false;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>Hide selected-unit Rearmer capacity text ("100,0t") — keep HUDUnitMarker icons.</summary>
    [HarmonyPatch(typeof(RearmerDisplay), nameof(RearmerDisplay.Initialize))]
    internal static class RcHideRearmerHudTextPatch
    {
        private static void Postfix(RearmerDisplay __instance, HUDUnitMarker marker)
        {
            try
            {
                if (__instance == null || marker == null)
                    return;
                Object.Destroy(__instance.gameObject);
            }
            catch
            {
                // ignore
            }
        }
    }
}
