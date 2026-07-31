using System;
using System.Collections.Generic;
using System.Reflection;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// RailLaunch uses vanilla Mirage-registered weaponPrefab.
    /// Stamp RC WeaponInfo/tag after spawn, and rename unitName BEFORE network Spawn
    /// so SyncVar(initialOnly) + PersistentUnit capture the remapped display name.
    /// </summary>
    internal static class LaunchRcCapture
    {
        private struct Pending
        {
            internal WeaponInfo Info;
            internal RcGuidanceKind Guidance;
            internal RcEngineKind Engine;
            internal string SourceMountKey;
            internal string BackupSeeker;
        }

        private static readonly Queue<Pending> _pending = new Queue<Pending>(8);
        private static readonly Queue<string> _forcedNames = new Queue<string>(4);

        private static readonly FieldInfo? UnitNameField =
            typeof(Unit).GetField("unitName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static void EnqueueFromWeapon(Weapon? weapon)
        {
            if (weapon == null || weapon.info == null)
                return;

            WeaponInfo info = weapon.info;
            string? name = info.weaponName;
            if (string.IsNullOrEmpty(name))
                return;

            bool isRc = CloneProfile.IsRcDisplayName(name)
                || CloneProfile.TryGetGuidanceFromRcName(name, out _);
            if (!isRc)
                return;

            RcGuidanceKind guidance = RcGuidanceKind.DataLink;
            CloneProfile.TryGetGuidanceFromRcName(name, out guidance);
            RcEngineKind engine = RcEngineKind.Jet;
            string sourceKey = string.Empty;
            string backup = string.Empty;

            try
            {
                RcMountMeta? meta = weapon.GetComponentInParent<RcMountMeta>()
                    ?? weapon.GetComponent<RcMountMeta>();
                if (meta != null)
                {
                    guidance = meta.Guidance;
                    engine = meta.Engine;
                    sourceKey = meta.SourceMountKey ?? string.Empty;
                    backup = meta.BackupSeekerHint ?? string.Empty;
                }
                else
                {
                    CloneProfile.TryResolveEngineFromWeaponName(CloneProfile.StripLegacyPrefix(name!), out engine);
                }
            }
            catch
            {
                // ignore
            }

            _pending.Enqueue(new Pending
            {
                Info = info,
                Guidance = guidance,
                Engine = engine,
                SourceMountKey = sourceKey,
                BackupSeeker = backup
            });
        }

        /// <summary>Encyclopedia / definition spawn: push remapped MissileDefinition.unitName.</summary>
        internal static void PushForcedDisplayName(string? displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return;
            if (!CloneProfile.IsRcDisplayName(displayName)
                && !CloneProfile.TryGetGuidanceFromRcName(displayName, out _))
                return;
            _forcedNames.Enqueue(displayName!);
        }

        /// <summary>Called immediately before Mirage network Spawn — overrides stock definition.unitName.</summary>
        internal static void TryRenameBeforeNetworkSpawn(GameObject? instance)
        {
            if (instance == null)
                return;

            Missile? missile = instance.GetComponent<Missile>();
            if (missile == null)
                return;

            string? name = null;
            if (_pending.Count > 0)
            {
                Pending peek = _pending.Peek();
                if (peek.Info != null && !string.IsNullOrEmpty(peek.Info.weaponName))
                    name = peek.Info.weaponName;
            }

            if (string.IsNullOrEmpty(name) && _forcedNames.Count > 0)
                name = _forcedNames.Peek();

            if (string.IsNullOrEmpty(name))
                return;

            ApplyDisplayName(missile, name!);
        }

        internal static void TryApplyToSpawned(Missile? missile)
        {
            if (missile == null)
                return;

            // Definition-spawn path: consume forced name and apply (network already spawned).
            if (_pending.Count == 0)
            {
                if (_forcedNames.Count > 0)
                {
                    string forced = _forcedNames.Dequeue();
                    ApplyDisplayName(missile, forced);
                }
                return;
            }

            Pending pending = _pending.Dequeue();
            if (_forcedNames.Count > 0)
                _forcedNames.Dequeue();

            try
            {
                var infoField = typeof(Missile).GetField(
                    "info",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                infoField?.SetValue(missile, pending.Info);

                RcMissileTag tag = missile.GetComponent<RcMissileTag>()
                    ?? missile.gameObject.AddComponent<RcMissileTag>();
                tag.Guidance = pending.Guidance;
                tag.Engine = pending.Engine;
                tag.SourceMountKey = pending.SourceMountKey ?? string.Empty;
                tag.BackupSeekerType = pending.BackupSeeker ?? string.Empty;

                if (string.IsNullOrEmpty(tag.BackupSeekerType))
                {
                    MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
                    if (seeker != null)
                    {
                        tag.BackupSeekerType = seeker.GetSeekerType();
                        if (string.IsNullOrEmpty(tag.BackupSeekerType))
                            tag.BackupSeekerType = seeker.GetType().Name;
                    }
                }

                if (pending.Info != null && !string.IsNullOrEmpty(pending.Info.weaponName))
                    ApplyDisplayName(missile, pending.Info.weaponName);

                RcPlugin.ModLogger?.LogInfo(
                    $"RC launch stamped: {pending.Info?.weaponName} ({pending.Guidance}/{pending.Engine})");
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC launch stamp failed: {ex.Message}");
            }
        }

        /// <summary>Backup path when Fire→queue missed — rename from WeaponInfo / RC display rules.</summary>
        internal static void TryApplyNameFromMissileInfo(Missile missile)
        {
            if (missile == null)
                return;
            try
            {
                WeaponInfo? info = MissileAccess.GetMissileInfo(missile);
                string? name = info != null ? info.weaponName : null;
                if (string.IsNullOrEmpty(name))
                    return;
                if (!CloneProfile.IsRcDisplayName(name) && !CloneProfile.TryGetGuidanceFromRcName(name, out _))
                    return;
                ApplyDisplayName(missile, name!);
            }
            catch
            {
                // ignore
            }
        }

        internal static void ApplyDisplayName(Missile missile, string displayName)
        {
            if (missile == null || string.IsNullOrEmpty(displayName))
                return;

            try
            {
                missile.NetworkunitName = displayName;
            }
            catch
            {
                // ignore
            }

            try
            {
                UnitNameField?.SetValue(missile, displayName);
            }
            catch
            {
                // ignore
            }

            try
            {
                if (UnitRegistry.TryGetPersistentUnit(missile.persistentID, out PersistentUnit pu) && pu != null)
                    pu.unitName = displayName;
            }
            catch
            {
                // ignore
            }
        }

        internal static void Clear()
        {
            _pending.Clear();
            _forcedNames.Clear();
        }
    }
}
