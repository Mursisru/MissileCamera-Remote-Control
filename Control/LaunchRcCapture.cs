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
    /// Pending is keyed by launch owner — avoids interleaved Fire stealing the wrong stamp (~half vanilla).
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
            internal bool Controllable;
            internal PersistentID OwnerId;
            internal float EnqueueTime;
        }

        private const float PendingTtlSec = 3f;

        private static readonly Dictionary<int, Queue<Pending>> _pendingByOwner =
            new Dictionary<int, Queue<Pending>>(8);

        private static readonly Queue<string> _forcedNames = new Queue<string>(4);

        private static readonly FieldInfo? UnitNameField =
            typeof(Unit).GetField("unitName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo? MissileInfoField =
            typeof(Missile).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void EnqueueFromWeapon(Weapon? weapon, Unit? owner = null)
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
            bool controllable = !CloneProfile.IsPassiveShellDisplayName(name);

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
                    controllable = meta.Controllable;
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

            PersistentID ownerId = PersistentID.None;
            try
            {
                if (owner != null)
                    ownerId = owner.persistentID;
            }
            catch
            {
                // ignore
            }

            Pending pending = new Pending
            {
                Info = info,
                Guidance = guidance,
                Engine = engine,
                SourceMountKey = sourceKey,
                BackupSeeker = backup,
                Controllable = controllable,
                OwnerId = ownerId,
                EnqueueTime = Time.unscaledTime
            };

            int key = OwnerKey(ownerId);
            if (!_pendingByOwner.TryGetValue(key, out Queue<Pending>? q) || q == null)
            {
                q = new Queue<Pending>(4);
                _pendingByOwner[key] = q;
            }

            q.Enqueue(pending);
            PruneStale();
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
            if (TryPeekForMissile(missile, out Pending peek)
                && peek.Info != null
                && !string.IsNullOrEmpty(peek.Info.weaponName))
            {
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

            PruneStale();

            if (!TryDequeueForMissile(missile, out Pending pending))
            {
                if (_forcedNames.Count > 0)
                {
                    string forced = _forcedNames.Dequeue();
                    ApplyDisplayName(missile, forced);
                }

                // Backup: RC WeaponInfo already on instance (no queue match).
                TryStampFromExistingInfo(missile);
                return;
            }

            if (_forcedNames.Count > 0)
                _forcedNames.Dequeue();

            ApplyPending(missile, pending);
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
            _pendingByOwner.Clear();
            _forcedNames.Clear();
        }

        private static void ApplyPending(Missile missile, Pending pending)
        {
            try
            {
                MissileInfoField?.SetValue(missile, pending.Info);

                RcMissileTag tag = missile.GetComponent<RcMissileTag>()
                    ?? missile.gameObject.AddComponent<RcMissileTag>();
                tag.Guidance = pending.Guidance;
                tag.GuidanceLabel = GuidanceLabels.For(pending.Guidance);
                tag.Engine = pending.Engine;
                tag.SourceMountKey = pending.SourceMountKey ?? string.Empty;
                tag.BackupSeekerType = pending.BackupSeeker ?? string.Empty;
                tag.Controllable = pending.Controllable;

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
                    $"RC launch stamped: {pending.Info?.weaponName} ({pending.Guidance}/{pending.Engine}, ctrl={pending.Controllable})");
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC launch stamp failed: {ex.Message}");
            }
        }

        private static void TryStampFromExistingInfo(Missile missile)
        {
            if (missile.GetComponent<RcMissileTag>() != null)
                return;

            try
            {
                WeaponInfo? info = MissileAccess.GetMissileInfo(missile);
                string? name = info != null ? info.weaponName : null;
                if (string.IsNullOrEmpty(name))
                    return;
                if (!CloneProfile.IsRcDisplayName(name) && !CloneProfile.TryGetGuidanceFromRcName(name, out _))
                    return;

                CloneProfile.TryGetGuidanceFromRcName(name, out RcGuidanceKind guidance);
                CloneProfile.TryResolveEngineFromWeaponName(CloneProfile.StripLegacyPrefix(name!), out RcEngineKind engine);

                RcMissileTag tag = missile.gameObject.AddComponent<RcMissileTag>();
                tag.Guidance = guidance;
                tag.GuidanceLabel = GuidanceLabels.For(guidance);
                tag.Engine = engine;
                tag.Controllable = !CloneProfile.IsPassiveShellDisplayName(name);
                ApplyDisplayName(missile, name!);
            }
            catch
            {
                // ignore
            }
        }

        private static bool TryPeekForMissile(Missile missile, out Pending pending)
        {
            pending = default;
            int key = OwnerKey(TryOwnerId(missile));
            if (!_pendingByOwner.TryGetValue(key, out Queue<Pending>? q) || q == null || q.Count == 0)
            {
                // Fallback: any single pending owner queue (SP ripple edge).
                if (_pendingByOwner.Count == 1)
                {
                    foreach (Queue<Pending> only in _pendingByOwner.Values)
                    {
                        if (only != null && only.Count > 0)
                        {
                            pending = only.Peek();
                            return true;
                        }
                    }
                }

                return false;
            }

            pending = q.Peek();
            return true;
        }

        private static bool TryDequeueForMissile(Missile missile, out Pending pending)
        {
            pending = default;
            PersistentID ownerId = TryOwnerId(missile);
            int key = OwnerKey(ownerId);

            if (_pendingByOwner.TryGetValue(key, out Queue<Pending>? q) && q != null && q.Count > 0)
            {
                pending = q.Dequeue();
                if (q.Count == 0)
                    _pendingByOwner.Remove(key);
                return true;
            }

            // Owner None / mismatch: do NOT steal another aircraft's pending (vanilla stamp bug).
            if (!ownerId.IsValid)
            {
                // Last resort: exactly one pending total.
                int total = 0;
                Queue<Pending>? sole = null;
                int soleKey = 0;
                foreach (KeyValuePair<int, Queue<Pending>> kv in _pendingByOwner)
                {
                    if (kv.Value == null || kv.Value.Count == 0)
                        continue;
                    total += kv.Value.Count;
                    sole = kv.Value;
                    soleKey = kv.Key;
                }

                if (total == 1 && sole != null)
                {
                    pending = sole.Dequeue();
                    if (sole.Count == 0)
                        _pendingByOwner.Remove(soleKey);
                    return true;
                }
            }

            return false;
        }

        private static PersistentID TryOwnerId(Missile missile)
        {
            try
            {
                return missile.NetworkownerID;
            }
            catch
            {
                return PersistentID.None;
            }
        }

        private static int OwnerKey(PersistentID id)
        {
            try
            {
                if (id.IsValid)
                    return id.GetHashCode();
            }
            catch
            {
                // ignore
            }

            return 0;
        }

        private static void PruneStale()
        {
            float now = Time.unscaledTime;
            List<int>? removeKeys = null;

            foreach (KeyValuePair<int, Queue<Pending>> kv in _pendingByOwner)
            {
                Queue<Pending>? q = kv.Value;
                if (q == null)
                {
                    removeKeys ??= new List<int>(4);
                    removeKeys.Add(kv.Key);
                    continue;
                }

                while (q.Count > 0 && now - q.Peek().EnqueueTime > PendingTtlSec)
                    q.Dequeue();

                if (q.Count == 0)
                {
                    removeKeys ??= new List<int>(4);
                    removeKeys.Add(kv.Key);
                }
            }

            if (removeKeys == null)
                return;
            for (int i = 0; i < removeKeys.Count; i++)
                _pendingByOwner.Remove(removeKeys[i]);
        }
    }
}
