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
        private static void Prefix(MountedMissile __instance)
        {
            try
            {
                if (!Network.RcServerCompat.FeaturesAllowed)
                    return;
                LaunchRcCapture.EnqueueFromWeapon(__instance);
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

            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            tag.BackupSeekerType = seeker != null ? seeker.GetSeekerType() : string.Empty;
            if (string.IsNullOrEmpty(tag.BackupSeekerType) && seeker != null)
                tag.BackupSeekerType = seeker.GetType().Name;

            LaunchRcCapture.TryApplyNameFromMissileInfo(missile);
            if (!string.IsNullOrEmpty(name)
                && (CloneProfile.IsRcDisplayName(name) || CloneProfile.TryGetGuidanceFromRcName(name, out _)))
                LaunchRcCapture.ApplyDisplayName(missile, name);
        }
    }

    [HarmonyPatch(typeof(MissileSeeker), nameof(MissileSeeker.Seek))]
    internal static class RcSeekPatch
    {
        private static readonly System.Reflection.FieldInfo? SeekerMissileField =
            typeof(MissileSeeker).GetField("missile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        public static bool Prefix(MissileSeeker __instance)
        {
            // Fast path: no RC session → never touch seeker fields (all missiles).
            if (RemoteControlSession.Controlled == null)
                return true;

            try
            {
                if (__instance == null)
                    return true;

                Missile? missile = null;
                try
                {
                    if (SeekerMissileField != null)
                        missile = SeekerMissileField.GetValue(__instance) as Missile;
                }
                catch
                {
                    missile = null;
                }

                if (missile == null)
                    missile = __instance.GetComponent<Missile>();
                if (missile == null)
                    return true;

                // Skip Seek for RC ownership (not only IsControlling/FS).
                // Inside terminalRange cruise Seek steals SetAimpoint every FixedUpdate.
                if (RemoteControlSession.OwnsMissile(missile))
                    return false;
            }
            catch
            {
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// RC afterburner: raise Motor.Thrust throttle + burnRate, and lift Motor.topSpeed
    /// (vanilla skips AddForce when speed &gt;= topSpeed — AB felt dead at cruise Vmax).
    /// </summary>
    internal static class RcMotorThrustPatch
    {
        private static float _savedBurn = -1f;
        private static float _savedTop = -1f;

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

                if (burnMult > 1.001f
                    && MissileAccess.TryGetBurnRate(__instance, out float baseRate)
                    && baseRate > 0f)
                {
                    _savedBurn = baseRate;
                    MissileAccess.TrySetBurnRate(__instance, baseRate * burnMult);
                }

                // Lift Vmax while boosting so AddForce still runs past cruise topSpeed.
                if (ThrottleController.BoostActive
                    && MissileAccess.TryGetTopSpeed(__instance, out float top)
                    && top > 1f
                    && top < 1e8f)
                {
                    _savedTop = top;
                    MissileAccess.TrySetTopSpeed(__instance, top * ThrottleController.BoostTopSpeedFactor);
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
