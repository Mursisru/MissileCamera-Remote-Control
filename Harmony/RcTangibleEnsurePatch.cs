using HarmonyLib;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>After Seek — catch early returns that skip vanilla SetTangible (no target / NotValid targetID).</summary>
    [HarmonyPatch(typeof(MissileSeeker), nameof(MissileSeeker.Seek))]
    internal static class RcSeekTangibleEnsurePatch
    {
        private static void Postfix(MissileSeeker __instance)
        {
            if (__instance == null)
                return;
            try
            {
                RcTangibleEnsure.TrySet(RcTangibleEnsure.GetMissile(__instance));
            }
            catch
            {
                // ignore
            }
        }
    }
}
