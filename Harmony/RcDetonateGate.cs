using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// While player OwnsMissile: block seeker self-destruct (fuel/speed/null-target SD)
    /// so RC stick is not killed mid-flight. Impact / TakeDamage still allowed via gate.
    /// After Release (and AI clones never owned): vanilla Detonate paths run again
    /// (target-lost SD, ballistic airburst) — fixes loiter when primary target dies.
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
                // Only while player is remote-piloting — never permanently mute RC clones.
                if (!RemoteControlSession.OwnsMissile(__instance))
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
