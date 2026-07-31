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

                bool isDl = name!.StartsWith(CloneProfile.DlPrefix, StringComparison.Ordinal);
                bool isSat = name.StartsWith(CloneProfile.SatPrefix, StringComparison.Ordinal);
                if (!isDl && !isSat)
                    return;

                EnsureRcTag(missile, isSat ? RcGuidanceKind.Satcom : RcGuidanceKind.DataLink, name);
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
            string bare = guidance == RcGuidanceKind.Satcom
                ? name.Substring(CloneProfile.SatPrefix.Length)
                : name.Substring(CloneProfile.DlPrefix.Length);
            if (!CloneProfile.TryResolveEngineFromWeaponName(bare, out tag.Engine))
                tag.Engine = RcEngineKind.Jet;

            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            tag.BackupSeekerType = seeker != null ? seeker.GetSeekerType() : string.Empty;
            if (string.IsNullOrEmpty(tag.BackupSeekerType) && seeker != null)
                tag.BackupSeekerType = seeker.GetType().Name;
        }
    }

    [HarmonyPatch(typeof(MissileSeeker), nameof(MissileSeeker.Seek))]
    internal static class RcSeekPatch
    {
        public static bool Prefix(MissileSeeker __instance)
        {
            try
            {
                if (__instance == null)
                    return true;
                Missile? missile = __instance.GetComponent<Missile>();
                if (missile == null)
                    return true;
                if (RemoteControlSession.IsControlling(missile))
                    return false;
            }
            catch
            {
                return true;
            }

            return true;
        }
    }

    internal static class RcMotorThrustPatch
    {
        public static void Prefix(Missile missile, ref float throttle, object __instance, out float __state)
        {
            __state = -1f;
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
                    __state = baseRate;
                    MissileAccess.TrySetBurnRate(__instance, baseRate * burnMult);
                }
            }
            catch
            {
                // keep vanilla
            }
        }

        public static void Postfix(object __instance, float __state)
        {
            if (__state < 0f || __instance == null)
                return;
            try
            {
                MissileAccess.TrySetBurnRate(__instance, __state);
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
                    __result = GuidanceLabels.For(tag.Guidance);
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
