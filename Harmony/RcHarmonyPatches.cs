using System;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Cloning;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;
using UnityEngine;

namespace MissileCameraRemoteControl.HarmonyPatches
{
    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[] { })]
    internal static class EncyclopediaAfterLoadPatch
    {
        private static void Postfix(Encyclopedia __instance)
        {
            try
            {
                WeaponCloneBootstrap.TryRun(RcPlugin.ModLogger);
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogError($"Encyclopedia AfterLoad RC clone failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[] { typeof(Encyclopedia) })]
    internal static class EncyclopediaAfterLoadStaticPatch
    {
        private static void Postfix(Encyclopedia instance)
        {
            try
            {
                WeaponCloneBootstrap.TryRun(RcPlugin.ModLogger);
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogError($"Encyclopedia static AfterLoad RC clone failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(MountedMissile), nameof(MountedMissile.Fire))]
    internal static class MountedMissileFireRcPatch
    {
        private static void Prefix(MountedMissile __instance, Unit owner)
        {
            try
            {
                if (!Network.RcServerCompat.FeaturesAllowed)
                    return;
                LaunchRcCapture.EnqueueFromWeapon(__instance, owner);
            }
            catch
            {
                // ignore
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new Type[]
    {
        typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit)
    })]
    internal static class SpawnerSpawnMissileRcPatch
    {
        private static void Postfix(Missile __result)
        {
            try
            {
                LaunchRcCapture.TryApplyToSpawned(__result);
            }
            catch
            {
                // ignore
            }
        }
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.RegisterMissile))]
    internal static class RegisterMissileRcTagPatch
    {
        private static void Postfix(Missile missile)
        {
            try
            {
                // Backup path if Fire→Spawn ordering missed the queue.
                if (missile == null || missile.GetComponent<RcMissileTag>() != null)
                    return;

                WeaponInfo? info = MissileAccess.GetMissileInfo(missile);
                string? name = info != null ? info.weaponName : null;
                if (string.IsNullOrEmpty(name))
                    return;

                bool isRc = CloneProfile.IsRcDisplayName(name)
                    || CloneProfile.TryGetGuidanceFromRcName(name, out _);
                if (!isRc)
                    return;

                CloneProfile.TryGetGuidanceFromRcName(name, out RcGuidanceKind guidance);
                EnsureRcTag(missile, guidance, name!);
            }
            catch
            {
                // ignore
            }
        }

        private static void EnsureRcTag(Missile missile, RcGuidanceKind guidance, string name)
        {
            RcMissileTag tag = missile.gameObject.AddComponent<RcMissileTag>();
            tag.Guidance = guidance;
            tag.GuidanceLabel = GuidanceLabels.For(guidance);
            string bare = CloneProfile.StripLegacyPrefix(name);
            if (!CloneProfile.TryResolveEngineFromWeaponName(bare, out tag.Engine))
                tag.Engine = RcEngineKind.Jet;
            tag.Controllable = !CloneProfile.IsPassiveShellDisplayName(name);

            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            tag.BackupSeekerType = seeker != null ? seeker.GetSeekerType() : string.Empty;
            if (string.IsNullOrEmpty(tag.BackupSeekerType) && seeker != null)
                tag.BackupSeekerType = seeker.GetType().Name;

            LaunchRcCapture.TryApplyNameFromMissileInfo(missile);
            if (!string.IsNullOrEmpty(name)
                && (CloneProfile.IsRcDisplayName(name) || CloneProfile.TryGetGuidanceFromRcName(name, out _)))
                LaunchRcCapture.ApplyDisplayName(missile, name);

            MissileAccess.InvalidateRcMissileCache(missile);
            if (tag.Controllable)
                RcLivingRcRegistry.Notify(missile);
        }
    }

    [HarmonyPatch(typeof(MissileSeeker), nameof(MissileSeeker.Seek))]
    internal static class RcSeekPatch
    {
        // Hot path: skip Seek only for seeker IDs in RcSeekSkipSet (no FieldInfo on world missiles).
        public static bool Prefix(MissileSeeker __instance)
        {
            if (!RcSeekSkipSet.HasAny)
                return true;
            try
            {
                return !RcSeekSkipSet.ShouldSkipSeeker(__instance);
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>
    /// RC afterburner: raise Motor.Thrust throttle + burnRate, and lift Motor.topSpeed
    /// (vanilla skips AddForce when speed &gt;= topSpeed — AB felt dead at cruise Vmax).
    /// Snapshot base burn/top once per boost edge — avoid GetValue boxing every Thrust.
    /// </summary>
    internal static class RcMotorThrustPatch
    {
        private static float _savedBurn = -1f;
        private static float _savedTop = -1f;
        private static float _cachedBaseBurn = -1f;
        private static float _cachedBaseTop = -1f;
        private static bool _burnCached;
        private static bool _topCached;
        private static bool _wasBoost;

        public static void Prefix(
            object __instance,
            Missile missile,
            bool localSim,
            Vector3 inputs,
            ref float throttle)
        {
            _savedBurn = -1f;
            _savedTop = -1f;
            try
            {
                if (missile == null || __instance == null)
                    return;
                if (!ThrottleController.TryGetMotorOverride(missile, out float effectiveThrottle, out float burnMult))
                    return;

                throttle = effectiveThrottle;

                bool boost = ThrottleController.BoostActive;
                if (boost != _wasBoost)
                {
                    _burnCached = false;
                    _topCached = false;
                    _wasBoost = boost;
                }

                if (burnMult > 1.001f)
                {
                    if (!_burnCached)
                    {
                        if (MissileAccess.TryGetBurnRate(__instance, out float baseRate) && baseRate > 0f)
                        {
                            _cachedBaseBurn = baseRate;
                            _burnCached = true;
                        }
                    }

                    if (_burnCached)
                    {
                        _savedBurn = _cachedBaseBurn;
                        MissileAccess.TrySetBurnRate(__instance, _cachedBaseBurn * burnMult);
                    }
                }

                if (boost)
                {
                    if (!_topCached)
                    {
                        if (MissileAccess.TryGetTopSpeed(__instance, out float top)
                            && top > 1f && top < 1e8f)
                        {
                            _cachedBaseTop = top;
                            _topCached = true;
                        }
                    }

                    if (_topCached)
                    {
                        _savedTop = _cachedBaseTop;
                        MissileAccess.TrySetTopSpeed(
                            __instance, _cachedBaseTop * ThrottleController.BoostTopSpeedFactor);
                    }
                }
            }
            catch
            {
                // keep vanilla
            }
        }

        public static void Postfix(object __instance)
        {
            if (__instance == null)
                return;
            try
            {
                if (_savedBurn >= 0f)
                    MissileAccess.TrySetBurnRate(__instance, _savedBurn);
                if (_savedTop >= 0f)
                    MissileAccess.TrySetTopSpeed(__instance, _savedTop);
            }
            catch
            {
                // ignore
            }
            finally
            {
                _savedBurn = -1f;
                _savedTop = -1f;
            }
        }
    }

    /// <summary>
    /// Cruise seekers call SetThrottle(0.8–1) from TerrainWaypoint.
    /// While RC is active, force the player UI throttle so MissileCamera THR stays in sync.
    /// </summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.SetThrottle))]
    internal static class RcSetThrottleGuardPatch
    {
        private static void Prefix(Missile __instance, ref float throttle)
        {
            try
            {
                if (__instance == null || !RemoteControlSession.IsControlling(__instance))
                    return;
                throttle = ThrottleController.UiThrottle;
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>Hangar weapon inspect: show DL / SATCOM instead of stock seeker type.</summary>
    [HarmonyPatch(typeof(AircraftSelectionMenu), nameof(AircraftSelectionMenu.DisplayInfo), new Type[] { typeof(WeaponInfo) })]
    internal static class AircraftSelectionGuidancePatch
    {
        private static readonly System.Reflection.FieldInfo? SeekerField =
            typeof(AircraftSelectionMenu).GetField("weaponSeeker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static void Postfix(AircraftSelectionMenu __instance, WeaponInfo weaponInfo)
        {
            try
            {
                if (weaponInfo == null || SeekerField == null)
                    return;
                if (!GuidanceLabels.TryFromWeaponName(weaponInfo.weaponName, out string label))
                    return;
                object? tmp = SeekerField.GetValue(__instance);
                if (tmp is TMPro.TMP_Text text)
                    text.text = label;
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>Encyclopedia Missiles tab: Guidance field = DL / SATCOM for RC definitions.</summary>
    [HarmonyPatch(typeof(EncyclopediaBrowser), "DisplayUnitInfo")]
    internal static class EncyclopediaGuidancePatch
    {
        private static readonly System.Reflection.FieldInfo? GuidanceField =
            typeof(EncyclopediaBrowser).GetField("guidance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static void Postfix(EncyclopediaBrowser __instance, UnitDefinition definition)
        {
            try
            {
                if (definition == null || GuidanceField == null)
                    return;
                if (!GuidanceLabels.TryFromUnitName(definition.unitName, out string label))
                    return;
                object? tmp = GuidanceField.GetValue(__instance);
                if (tmp is TMPro.TMP_Text text)
                    text.text = label;
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>Live missile / threat UI: RC missiles report DL or SATCOM.</summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.GetSeekerType))]
    internal static class MissileGetSeekerTypePatch
    {
        private static void Postfix(Missile __instance, ref string __result)
        {
            try
            {
                if (__instance == null)
                    return;
                RcMissileTag? tag = __instance.GetComponent<RcMissileTag>();
                if (tag != null)
                {
                    if (string.IsNullOrEmpty(tag.GuidanceLabel))
                        tag.GuidanceLabel = GuidanceLabels.For(tag.Guidance);
                    __result = tag.GuidanceLabel;
                    return;
                }

                WeaponInfo? info = MissileAccess.GetMissileInfo(__instance);
                if (info != null && GuidanceLabels.TryFromWeaponName(info.weaponName, out string label))
                    __result = label;
            }
            catch
            {
                // ignore
            }
        }
    }
}
