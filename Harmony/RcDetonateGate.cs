using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// RC clones: impact / TakeDamage only. Block seeker SD, fuel/speed miss, proximity CPA,
    /// target-lost airburst — no timed or near-miss detonations.
    /// DetectCollisions / TakeDamage (and explicit impact Force) open the allow gate.
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
                if (!MissileAccess.IsRcMissile(__instance)
                    && !RcSeekSkipSet.ContainsMissile(__instance))
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

    /// <summary>RC impact fuse only — never arm vanilla ProxyFuse CPA airburst.</summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.SetProxyFuse))]
    internal static class RcSetProxyFuseBlockPatch
    {
        private static bool Prefix(Missile __instance)
        {
            try
            {
                return __instance == null
                    || (!MissileAccess.IsRcMissile(__instance)
                        && !RcSeekSkipSet.ContainsMissile(__instance));
            }
            catch
            {
                return true;
            }
        }
    }
}
