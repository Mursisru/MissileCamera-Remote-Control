using HarmonyLib;
using MissileCameraRemoteControl.Access;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// RC clones: block seeker self-destruct (low speed / fuel coast / missed target / null target).
    /// Allow Detonate only from DetectCollisions (impact/terrain/sea) or TakeDamage (being shot).
    /// </summary>
    internal static class RcDetonateGate
    {
        private static int _allowDepth;

        internal static void AllowBegin() => _allowDepth++;

        internal static void AllowEnd()
        {
            if (_allowDepth > 0)
                _allowDepth--;
        }

        internal static bool IsAllowed => _allowDepth > 0;
    }

    [HarmonyPatch(typeof(Missile), "DetectCollisions")]
    internal static class RcDetectCollisionsDetonateAllowPatch
    {
        private static void Prefix() => RcDetonateGate.AllowBegin();

        // Finalizer always runs (success or exception) — do not also use Postfix (would double-end).
        private static void Finalizer() => RcDetonateGate.AllowEnd();
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.TakeDamage))]
    internal static class RcTakeDamageDetonateAllowPatch
    {
        private static void Prefix() => RcDetonateGate.AllowBegin();

        private static void Finalizer() => RcDetonateGate.AllowEnd();
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class RcDetonateSelfDestructBlockPatch
    {
        private static bool Prefix(Missile __instance)
        {
            try
            {
                if (__instance == null)
                    return true;
                if (!MissileAccess.IsRcMissile(__instance))
                    return true;
                if (RcDetonateGate.IsAllowed)
                    return true;

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
