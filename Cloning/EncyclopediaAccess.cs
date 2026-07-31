using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using MissileCameraRemoteControl.Config;

namespace MissileCameraRemoteControl.Cloning
{
    internal static class EncyclopediaAccess
    {
        private static readonly PropertyInfo? LookupIndexProp =
            typeof(INetworkDefinition).GetProperty("LookupIndex");

        internal static void RegisterCloneMount(Encyclopedia enc, WeaponMount clone, ManualLogSource? log)
        {
            if (enc == null || clone == null)
                return;

            try
            {
                if (enc.weaponMounts == null)
                    enc.weaponMounts = new List<WeaponMount>();

                if (!enc.weaponMounts.Contains(clone))
                    enc.weaponMounts.Add(clone);

                if (Encyclopedia.WeaponLookup == null)
                    Encyclopedia.WeaponLookup = new Dictionary<string, WeaponMount>();

                if (!string.IsNullOrEmpty(clone.jsonKey))
                    Encyclopedia.WeaponLookup[clone.jsonKey] = clone;

                if (enc.IndexLookup == null)
                    enc.IndexLookup = new List<INetworkDefinition>();

                if (!enc.IndexLookup.Contains(clone))
                {
                    SetLookupIndex(clone, enc.IndexLookup.Count);
                    enc.IndexLookup.Add(clone);
                }

                clone.Initialize();
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Encyclopedia register failed for {clone.jsonKey}: {ex.Message}");
            }
        }

        internal static void SetLookupIndex(INetworkDefinition def, int index)
        {
            if (def == null || LookupIndexProp == null)
                return;
            try
            {
                LookupIndexProp.SetValue(def, (int?)index);
            }
            catch
            {
                // ignore
            }
        }
    }
}
