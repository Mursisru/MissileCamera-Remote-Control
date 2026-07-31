using System;
using System.Collections.Generic;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// RailLaunch spawns via Spawner using WeaponInfo.weaponPrefab.
    /// We keep the vanilla (network-registered) prefab and stamp RC identity onto the live missile after spawn.
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

        internal static void TryApplyToSpawned(Missile? missile)
        {
            if (missile == null || _pending.Count == 0)
                return;

            Pending pending = _pending.Dequeue();
            try
            {
                // Force clone WeaponInfo onto live missile (prefab still carries vanilla info).
                var infoField = typeof(Missile).GetField(
                    "info",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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

                // Optional: show RC name in unitName for HUD/MC.
                try
                {
                    if (pending.Info != null && !string.IsNullOrEmpty(pending.Info.weaponName))
                        missile.NetworkunitName = pending.Info.weaponName;
                }
                catch
                {
                    // ignore
                }

                RcPlugin.ModLogger?.LogInfo(
                    $"RC launch stamped: {pending.Info?.weaponName} ({pending.Guidance}/{pending.Engine})");
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC launch stamp failed: {ex.Message}");
            }
        }

        internal static void Clear()
        {
            _pending.Clear();
        }
    }
}
