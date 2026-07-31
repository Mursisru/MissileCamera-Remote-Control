using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>Adds clone mounts only where the original already exists (prefabs + live instances).</summary>
    internal static class HardpointInjector
    {
        internal static void InjectAll(ManualLogSource? log)
        {
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

            // Live hangar / mission aircraft — prefab inject alone misses already-spawned copies.
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

        private static int InjectManager(WeaponManager? wm)
        {
            if (wm == null || wm.hardpointSets == null)
                return 0;

            int count = 0;
            foreach (HardpointSet set in wm.hardpointSets)
                count += InjectSet(set);
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
    }
}
