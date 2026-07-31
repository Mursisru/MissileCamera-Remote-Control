using System.Collections.Generic;
using MissileCameraRemoteControl.Config;

namespace MissileCameraRemoteControl.Cloning
{
    /// <summary>Maps original mount → cloned mount for hardpoint inject + lookup.</summary>
    internal static class CloneRegistry
    {
        private static readonly Dictionary<WeaponMount, WeaponMount> OriginalToClone =
            new Dictionary<WeaponMount, WeaponMount>();

        private static readonly Dictionary<string, WeaponMount> CloneByKey =
            new Dictionary<string, WeaponMount>();

        private static readonly Dictionary<WeaponMount, RcGuidanceKind> GuidanceByClone =
            new Dictionary<WeaponMount, RcGuidanceKind>();

        private static readonly Dictionary<WeaponMount, RcEngineKind> EngineByClone =
            new Dictionary<WeaponMount, RcEngineKind>();

        internal static IReadOnlyDictionary<WeaponMount, WeaponMount> Pairs => OriginalToClone;

        internal static void Clear()
        {
            OriginalToClone.Clear();
            CloneByKey.Clear();
            GuidanceByClone.Clear();
            EngineByClone.Clear();
        }

        internal static void Register(
            WeaponMount original,
            WeaponMount clone,
            RcGuidanceKind guidance,
            RcEngineKind engine)
        {
            if (original == null || clone == null)
                return;
            OriginalToClone[original] = clone;
            if (!string.IsNullOrEmpty(clone.jsonKey))
                CloneByKey[clone.jsonKey] = clone;
            GuidanceByClone[clone] = guidance;
            EngineByClone[clone] = engine;
        }

        internal static bool TryGetClone(WeaponMount original, out WeaponMount? clone)
        {
            return OriginalToClone.TryGetValue(original, out clone);
        }

        internal static bool TryGetProfile(WeaponMount clone, out RcGuidanceKind guidance, out RcEngineKind engine)
        {
            guidance = RcGuidanceKind.DataLink;
            engine = RcEngineKind.Jet;
            if (clone == null)
                return false;
            bool ok = GuidanceByClone.TryGetValue(clone, out guidance);
            EngineByClone.TryGetValue(clone, out engine);
            return ok;
        }
    }
}
