using System.Collections.Generic;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Control;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    /// <summary>
    /// While player owns / formation: block seeker SD / fuel miss / proxy CPA.
    /// Impact via DetectCollisions / TakeDamage opens a sync allow stack.
    /// Delayed pen fuse (Piledriver etc.) arms Detonate after await — use deferred allow ids.
    /// Released RC clones: vanilla Detonate (no gate).
    /// </summary>
    internal static class RcDetonateGate
    {
        private static int _allowDepth;
        private static int _manualMissileId;
        private static readonly HashSet<int> DeferredImpactIds = new HashSet<int>();

        internal static void AllowBegin() => _allowDepth++;

        internal static void AllowEnd()
        {
            if (_allowDepth > 0)
                _allowDepth--;
        }

        internal static bool IsAllowed => _allowDepth > 0;

        /// <summary>Sticky allow for manual / Force detonate (survives nested Finalizers).</summary>
        internal static void AllowManual(Missile? missile)
        {
            _manualMissileId = missile != null ? missile.GetInstanceID() : 0;
        }

        internal static void ClearManual() => _manualMissileId = 0;

        internal static bool IsManual(Missile? missile) =>
            missile != null && _manualMissileId != 0 && missile.GetInstanceID() == _manualMissileId;

        internal static void ArmDeferredImpact(Missile? missile)
        {
            if (missile == null)
                return;
            DeferredImpactIds.Add(missile.GetInstanceID());
        }

        /// <summary>True if this missile had a delayed impact fuse pending; clears the latch.</summary>
        internal static bool ConsumeDeferredImpact(Missile? missile)
        {
            if (missile == null)
                return false;
            return DeferredImpactIds.Remove(missile.GetInstanceID());
        }

        internal static void ClearDeferredImpact(Missile? missile)
        {
            if (missile == null)
                return;
            DeferredImpactIds.Remove(missile.GetInstanceID());
        }
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

    /// <summary>
    /// PenetrateObject returns true when impactFuseDelay &gt; 0 and armor is pierced —
    /// Detonate runs later inside ImpactDelayedFuse (after DetectCollisions allow ends).
    /// </summary>
    [HarmonyPatch(typeof(Missile), "PenetrateObject")]
    internal static class RcPenetrateDeferredDetonatePatch
    {
        private static void Postfix(Missile __instance, bool __result)
        {
            if (!__result || __instance == null)
                return;
            try
            {
                if (MissileAccess.IsRcMissile(__instance) || RcSeekSkipSet.ContainsMissile(__instance))
                    RcDetonateGate.ArmDeferredImpact(__instance);
            }
            catch
            {
                // ignore
            }
        }
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

                bool rc = MissileAccess.IsRcMissile(__instance)
                    || RcSeekSkipSet.ContainsMissile(__instance);
                if (!rc)
                    return true;

                // Gate only under stick / formation — released clones use stock fuse & SD.
                if (!RemoteControlSession.OwnsMissile(__instance)
                    && !RcFormationFollow.IsFollower(__instance))
                    return true;

                if (RcDetonateGate.IsAllowed || RcDetonateGate.IsManual(__instance))
                    return true;

                if (RcDetonateGate.ConsumeDeferredImpact(__instance))
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
