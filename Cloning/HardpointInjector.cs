using System;
using System.Collections.Generic;
using BepInEx.Logging;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using UnityEngine;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>Adds clone mounts only where the original already exists (prefabs + live instances).</summary>
    internal static class HardpointInjector
    {
        internal static void InjectAll(ManualLogSource? log)
        {
            if (!RcServerCompat.FeaturesAllowed)
            {
                log?.LogInfo("Hardpoint inject: skipped (RC disabled for this server session).");
                return;
            }

            if (CloneRegistry.Pairs.Count == 0)
            {
                log?.LogInfo("Hardpoint inject: skipped (no clones in registry).");
                return;
            }

            int injected = 0;

            Encyclopedia? enc = null;
            try
            {
                enc = Encyclopedia.i;
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Hardpoint inject: Encyclopedia unavailable ({ex.Message})");
            }

            if (enc != null)
            {
                try
                {
                    if (enc.aircraft != null)
                    {
                        foreach (AircraftDefinition def in enc.aircraft)
                            injected += InjectPrefab(def != null ? def.unitPrefab : null, log);
                    }

                    if (enc.vehicles != null)
                    {
                        foreach (VehicleDefinition def in enc.vehicles)
                            injected += InjectPrefab(def != null ? def.unitPrefab : null, log);
                    }

                    if (enc.ships != null)
                    {
                        foreach (ShipDefinition def in enc.ships)
                            injected += InjectPrefab(def != null ? def.unitPrefab : null, log);
                    }
                }
                catch (Exception ex)
                {
                    log?.LogWarning($"Hardpoint inject encyclopedia sweep failed: {ex.Message}");
                }
            }

            try
            {
                WeaponManager[] live = UnityEngine.Object.FindObjectsOfType<WeaponManager>();
                foreach (WeaponManager wm in live)
                    injected += InjectManager(wm);
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Hardpoint inject live sweep failed: {ex.Message}");
            }

            log?.LogInfo($"Hardpoint inject: added {injected} clone option(s).");
        }

        /// <summary>Remove RC clone options from live + encyclopedia prefab hardpoints (vanilla MP session).</summary>
        internal static int StripAllRcOptions(ManualLogSource? log)
        {
            int removed = 0;

            Encyclopedia? enc = null;
            try { enc = Encyclopedia.i; }
            catch { /* ignore */ }

            if (enc != null)
            {
                try
                {
                    if (enc.aircraft != null)
                    {
                        foreach (AircraftDefinition def in enc.aircraft)
                            removed += StripPrefab(def != null ? def.unitPrefab : null);
                    }
                    if (enc.vehicles != null)
                    {
                        foreach (VehicleDefinition def in enc.vehicles)
                            removed += StripPrefab(def != null ? def.unitPrefab : null);
                    }
                    if (enc.ships != null)
                    {
                        foreach (ShipDefinition def in enc.ships)
                            removed += StripPrefab(def != null ? def.unitPrefab : null);
                    }
                }
                catch (Exception ex)
                {
                    log?.LogWarning($"Hardpoint strip encyclopedia failed: {ex.Message}");
                }
            }

            try
            {
                WeaponManager[] live = UnityEngine.Object.FindObjectsOfType<WeaponManager>();
                foreach (WeaponManager wm in live)
                    removed += StripManager(wm);
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Hardpoint strip live failed: {ex.Message}");
            }

            return removed;
        }

        private static int InjectPrefab(GameObject? prefab, ManualLogSource? log)
        {
            if (prefab == null)
                return 0;

            int count = 0;
            try
            {
                WeaponManager[] managers = prefab.GetComponentsInChildren<WeaponManager>(true);
                foreach (WeaponManager wm in managers)
                    count += InjectManager(wm);
            }
            catch (Exception ex)
            {
                log?.LogDebug($"InjectPrefab {prefab.name}: {ex.Message}");
            }

            return count;
        }

        private static int StripPrefab(GameObject? prefab)
        {
            if (prefab == null)
                return 0;
            int count = 0;
            try
            {
                WeaponManager[] managers = prefab.GetComponentsInChildren<WeaponManager>(true);
                foreach (WeaponManager wm in managers)
                    count += StripManager(wm);
            }
            catch
            {
                // ignore
            }
            return count;
        }

        private static int InjectManager(WeaponManager? wm)
        {
            if (wm == null || wm.hardpointSets == null)
                return 0;

            int count = 0;
            foreach (HardpointSet set in wm.hardpointSets)
                count += InjectSet(set);
            return count;
        }

        private static int StripManager(WeaponManager? wm)
        {
            if (wm == null || wm.hardpointSets == null)
                return 0;

            int count = 0;
            foreach (HardpointSet set in wm.hardpointSets)
                count += StripSet(set);
            return count;
        }

        private static int InjectSet(HardpointSet? set)
        {
            if (set == null || set.weaponOptions == null || set.weaponOptions.Count == 0)
                return 0;

            int added = 0;
            List<WeaponMount> snapshot = new List<WeaponMount>(set.weaponOptions);
            foreach (WeaponMount original in snapshot)
            {
                if (original == null)
                    continue;
                if (!CloneRegistry.TryGetClone(original, out WeaponMount? clone) || clone == null)
                    continue;
                if (set.weaponOptions.Contains(clone))
                    continue;
                set.weaponOptions.Add(clone);
                added++;
            }

            return added;
        }

        private static int StripSet(HardpointSet? set)
        {
            if (set == null || set.weaponOptions == null || set.weaponOptions.Count == 0)
                return 0;

            int removed = 0;
            for (int i = set.weaponOptions.Count - 1; i >= 0; i--)
            {
                WeaponMount? mount = set.weaponOptions[i];
                if (mount == null)
                    continue;

                string? key = null;
                string? name = null;
                try { key = mount.jsonKey; }
                catch { /* ignore */ }
                try
                {
                    if (mount.info != null)
                        name = mount.info.weaponName;
                }
                catch
                {
                    // ignore
                }

                if (CloneProfile.IsRcCloneKey(key) || CloneProfile.IsRcDisplayName(name))
                {
                    set.weaponOptions.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }
    }
}
