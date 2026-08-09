using System;
using System.Collections.Generic;
using BepInEx.Logging;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>
    /// Clone whitelisted WeaponMounts into independent [DL]/[SATCOM] variants.
    /// Flying weaponPrefab stays the vanilla Mirage-registered asset — RC identity is stamped at launch.
    /// </summary>
    internal static class WeaponCloneBootstrap
    {
        private static bool _done;
        private static int _cloneCount;
        private static int _failStreak;

        internal static bool IsDone => _done;
        internal static int CloneCount => _cloneCount;

        internal static void ResetFlag()
        {
            _done = false;
            _cloneCount = 0;
            _failStreak = 0;
            CloneRegistry.Clear();
            MissileDefinitionCloner.Clear();
        }

        internal static bool TryRun(ManualLogSource? log)
        {
            if (_done)
                return true;

            Encyclopedia? enc;
            try
            {
                enc = Encyclopedia.i;
            }
            catch (Exception ex)
            {
                if (_failStreak++ < 3)
                    log?.LogWarning($"Clone bootstrap: Encyclopedia throw ({ex.Message})");
                return false;
            }

            if (enc == null || enc.weaponMounts == null || enc.weaponMounts.Count == 0)
            {
                if (_failStreak++ == 0)
                    log?.LogInfo("Clone bootstrap: waiting for Encyclopedia.weaponMounts…");
                return false;
            }

            if (Encyclopedia.WeaponLookup == null || Encyclopedia.WeaponLookup.Count == 0)
            {
                if (_failStreak++ == 0)
                    log?.LogInfo("Clone bootstrap: waiting for WeaponLookup…");
                return false;
            }

            int cloned = 0;
            int skipped = 0;
            List<WeaponMount> snapshot = new List<WeaponMount>(enc.weaponMounts);

            foreach (WeaponMount original in snapshot)
            {
                if (original == null || string.IsNullOrEmpty(original.jsonKey))
                    continue;

                string? weaponName = original.info != null ? original.info.weaponName : null;
                if (!CloneProfile.TryResolve(
                        original.jsonKey,
                        weaponName,
                        out RcGuidanceKind guidance,
                        out RcEngineKind engine,
                        out bool controllable))
                    continue;

                try
                {
                    WeaponMount? clone = CloneMount(original, guidance, engine, controllable, log);
                    if (clone == null)
                    {
                        skipped++;
                        continue;
                    }

                    CloneRegistry.Register(original, clone, guidance, engine);
                    EncyclopediaAccess.RegisterCloneMount(enc, clone, log);
                    // RegisterCloneMount → Initialize() resets costPerRound from shared definition.value.
                    RcCostMarkup.Ensure(clone);
                    MissileDefinitionCloner.EnsureForMount(enc, original, clone, guidance, log);
                    cloned++;
                }
                catch (Exception ex)
                {
                    log?.LogWarning($"Clone failed for {original.jsonKey}: {ex}");
                    skipped++;
                }
            }

            HardpointInjector.InjectAll(log);
            _cloneCount = cloned;
            _done = true;
            log?.LogInfo($"Weapon clone bootstrap: created {cloned} RC mount(s), skipped {skipped}, registry={CloneRegistry.Pairs.Count}.");
            return true;
        }

        private static WeaponMount? CloneMount(
            WeaponMount original,
            RcGuidanceKind guidance,
            RcEngineKind engine,
            bool controllable,
            ManualLogSource? log)
        {
            if (original.info == null || original.prefab == null)
            {
                log?.LogWarning($"Skip {original.jsonKey}: missing info/prefab.");
                return null;
            }

            string cloneKey = CloneProfile.MakeCloneKey(original.jsonKey, guidance);
            if (Encyclopedia.WeaponLookup != null && Encyclopedia.WeaponLookup.TryGetValue(cloneKey, out WeaponMount existing) && existing != null)
            {
                CloneRegistry.Register(original, existing, guidance, engine);
                RcCostMarkup.Ensure(existing);
                return existing;
            }

            WeaponInfo infoClone = UnityEngine.Object.Instantiate(original.info);
            infoClone.name = original.info.name + (guidance == RcGuidanceKind.Satcom ? "_RC_SAT" : "_RC_DL");
            string displayName = CloneProfile.MakeDisplayName(
                original.info.weaponName,
                guidance,
                original.jsonKey,
                original.info.shortName);
            infoClone.weaponName = displayName;
            infoClone.shortName = displayName;
            infoClone.description = RcWeaponDescriptions.Resolve(original.info.weaponName, guidance);

            // CRITICAL: keep vanilla flying prefab — runtime Instantiates are NOT Mirage-registered and despawn on Spawn().
            infoClone.weaponPrefab = original.info.weaponPrefab;

            GameObject mountPrefab;
            try
            {
                mountPrefab = UnityEngine.Object.Instantiate(original.prefab);
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Skip {original.jsonKey}: mount prefab Instantiate failed ({ex.Message})");
                UnityEngine.Object.Destroy(infoClone);
                return null;
            }

            mountPrefab.name = original.prefab.name + (guidance == RcGuidanceKind.Satcom ? "_RC_SAT" : "_RC_DL");
            mountPrefab.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(mountPrefab);
            mountPrefab.hideFlags = HideFlags.HideAndDontSave;

            foreach (Weapon w in mountPrefab.GetComponentsInChildren<Weapon>(true))
                MissileAccess.SetWeaponInfo(w, infoClone);

            string backupHint = string.Empty;
            try
            {
                if (original.info.weaponPrefab != null)
                {
                    MissileSeeker? seeker = original.info.weaponPrefab.GetComponent<MissileSeeker>()
                        ?? original.info.weaponPrefab.GetComponentInChildren<MissileSeeker>(true);
                    if (seeker != null)
                    {
                        backupHint = seeker.GetSeekerType();
                        if (string.IsNullOrEmpty(backupHint))
                            backupHint = seeker.GetType().Name;
                    }
                }
            }
            catch
            {
                // ignore
            }

            RcMountMeta meta = mountPrefab.GetComponent<RcMountMeta>() ?? mountPrefab.AddComponent<RcMountMeta>();
            meta.Guidance = guidance;
            meta.Engine = engine;
            meta.SourceMountKey = original.jsonKey;
            meta.BackupSeekerHint = backupHint;
            meta.Controllable = controllable;

            WeaponMount mountClone = UnityEngine.Object.Instantiate(original);
            mountClone.name = original.name + (guidance == RcGuidanceKind.Satcom ? "_RC_SAT" : "_RC_DL");
            mountClone.jsonKey = cloneKey;
            mountClone.info = infoClone;
            mountClone.prefab = mountPrefab;

            try
            {
                mountClone.Initialize();
            }
            catch (Exception ex)
            {
                log?.LogDebug($"Initialize {cloneKey}: {ex.Message}");
            }

            mountClone.info = infoClone;
            foreach (Weapon w in mountPrefab.GetComponentsInChildren<Weapon>(true))
                MissileAccess.SetWeaponInfo(w, infoClone);

            // Cost markup applied after Register + any later Initialize (see RcCostMarkup / Harmony).
            RcCostMarkup.Ensure(mountClone);

            string baseName = !string.IsNullOrEmpty(infoClone.weaponName)
                ? infoClone.weaponName
                : original.info.weaponName;
            int ammo = mountClone.ammo > 0 ? mountClone.ammo : original.ammo;
            mountClone.mountName = ammo > 1
                ? $"{baseName} x{ammo}"
                : baseName;

            return mountClone;
        }
    }
}
