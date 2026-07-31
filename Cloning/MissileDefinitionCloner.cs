using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>
    /// Registers unique [DL]/[SATCOM] MissileDefinition entries so they appear in Encyclopedia → Missiles.
    /// Reuses original unitPrefab (already Mirage-registered) — only metadata is cloned.
    /// </summary>
    internal static class MissileDefinitionCloner
    {
        private static readonly HashSet<string> _registeredKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly FieldInfo? DisabledField =
            typeof(UnitDefinition).GetField("disabled", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Clear()
        {
            _registeredKeys.Clear();
        }

        internal static void EnsureForMount(
            Encyclopedia enc,
            WeaponMount original,
            WeaponMount clone,
            RcGuidanceKind guidance,
            ManualLogSource? log)
        {
            if (enc == null || original?.info?.weaponPrefab == null || clone?.info == null)
                return;

            Missile? srcMissile = original.info.weaponPrefab.GetComponent<Missile>()
                ?? original.info.weaponPrefab.GetComponentInChildren<Missile>(true);
            if (srcMissile == null || srcMissile.definition == null)
                return;

            UnitDefinition srcDef = srcMissile.definition;
            if (!(srcDef is MissileDefinition srcMissileDef))
                return;

            string defKey = CloneProfile.MakeCloneKey(
                string.IsNullOrEmpty(srcDef.jsonKey) ? srcDef.unitName : srcDef.jsonKey,
                guidance);

            if (_registeredKeys.Contains(defKey))
                return;
            if (Encyclopedia.Lookup != null && Encyclopedia.Lookup.ContainsKey(defKey))
            {
                _registeredKeys.Add(defKey);
                return;
            }

            try
            {
                MissileDefinition defClone = UnityEngine.Object.Instantiate(srcMissileDef);
                defClone.name = srcMissileDef.name + (guidance == RcGuidanceKind.Satcom ? "_RC_SAT" : "_RC_DL");
                defClone.jsonKey = defKey;
                defClone.unitName = CloneProfile.MakeDisplayName(srcDef.unitName, guidance);
                if (!string.IsNullOrEmpty(srcDef.bogeyName))
                    defClone.bogeyName = CloneProfile.MakeDisplayName(srcDef.bogeyName, guidance);

                string channel = guidance == RcGuidanceKind.Satcom
                    ? "SATCOM satellite command"
                    : "Data-Link (DL) mesh relay";
                string baseDesc = srcDef.description ?? string.Empty;
                defClone.description =
                    $"[MissileCamera Remote Control]\nGuidance: {channel}. Manual mouse RC with vanilla aero; signal loss falls back to stock seeker.\n\n{baseDesc}";

                // Same network prefab as vanilla — required for Mirage RegisterPrefab / encyclopedia spawn.
                defClone.unitPrefab = srcDef.unitPrefab;

                // Ensure not disabled.
                try
                {
                    DisabledField?.SetValue(defClone, false);
                }
                catch
                {
                    // ignore
                }

                if (enc.missiles == null)
                    enc.missiles = new List<MissileDefinition>();
                if (!enc.missiles.Contains(defClone))
                    enc.missiles.Add(defClone);

                if (Encyclopedia.Lookup == null)
                    Encyclopedia.Lookup = new Dictionary<string, UnitDefinition>();
                Encyclopedia.Lookup[defKey] = defClone;

                if (enc.IndexLookup == null)
                    enc.IndexLookup = new List<INetworkDefinition>();
                if (!enc.IndexLookup.Contains(defClone))
                {
                    var lookupProp = typeof(INetworkDefinition).GetProperty("LookupIndex");
                    lookupProp?.SetValue(defClone, (int?)enc.IndexLookup.Count);
                    enc.IndexLookup.Add(defClone);
                }

                _registeredKeys.Add(defKey);
                log?.LogInfo($"Encyclopedia missile entry: {defClone.unitName} ({defKey})");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"MissileDefinition clone failed for {defKey}: {ex.Message}");
            }
        }
    }
}
