namespace MissileCameraRemoteControl.Config
{
    internal enum RcGuidanceKind : byte
    {
        DataLink = 0,
        Satcom = 1
    }

    internal enum RcEngineKind : byte
    {
        Jet = 0,
        Solid = 1
    }

    /// <summary>Static whitelist: mount jsonKey prefix → guidance + engine. User may override later.</summary>
    internal static class CloneProfile
    {
        internal const string DlSuffix = "_RC_DL";
        internal const string SatSuffix = "_RC_SAT";
        internal const string DlPrefix = "[DL] ";
        internal const string SatPrefix = "[SATCOM] ";

        internal static bool IsRcCloneKey(string? jsonKey)
        {
            if (string.IsNullOrEmpty(jsonKey))
                return false;
            return jsonKey!.EndsWith(DlSuffix) || jsonKey.EndsWith(SatSuffix);
        }

        /// <summary>Returns false for excluded / already-cloned mounts.</summary>
        internal static bool TryResolve(string jsonKey, string? weaponName, out RcGuidanceKind guidance, out RcEngineKind engine)
        {
            guidance = RcGuidanceKind.DataLink;
            engine = RcEngineKind.Jet;

            if (string.IsNullOrEmpty(jsonKey) || IsRcCloneKey(jsonKey))
                return false;

            if (IsExcluded(jsonKey))
                return false;

            string key = jsonKey;
            string name = weaponName ?? string.Empty;

            // SATCOM: nuclear cruise ALND-4 + Piledriver TBM family
            if (key.IndexOf("20kt", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ALND", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.Satcom;
                engine = RcEngineKind.Jet;
                return key.StartsWith("CruiseMissile", System.StringComparison.Ordinal);
            }

            if (key.StartsWith("BallisticMissile", System.StringComparison.Ordinal)
                || name.IndexOf("Piledriver", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.Satcom;
                engine = RcEngineKind.Solid;
                return true;
            }

            // DL cruise / AShM / Tusko
            if (key.StartsWith("CruiseMissile", System.StringComparison.Ordinal)
                || name.IndexOf("ALM-C450", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (key.StartsWith("AShM1", System.StringComparison.Ordinal)
                || name.IndexOf("AShM-300", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (key.StartsWith("AShM2", System.StringComparison.Ordinal)
                || name.IndexOf("AGM-99", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (key.StartsWith("AShM3", System.StringComparison.Ordinal)
                || name.IndexOf("Tusko", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Solid;
                return true;
            }

            return false;
        }

        /// <summary>Resolve engine for a launched clone from bare weaponName (prefix already stripped).</summary>
        internal static bool TryResolveEngineFromWeaponName(string weaponName, out RcEngineKind engine)
        {
            engine = RcEngineKind.Jet;
            if (string.IsNullOrEmpty(weaponName))
                return false;

            if (weaponName.IndexOf("Tusko", System.StringComparison.OrdinalIgnoreCase) >= 0
                || weaponName.IndexOf("Piledriver", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                engine = RcEngineKind.Solid;
                return true;
            }

            engine = RcEngineKind.Jet;
            return true;
        }

        private static bool IsExcluded(string jsonKey)
        {
            return jsonKey.StartsWith("AAM", System.StringComparison.Ordinal)
                || jsonKey.StartsWith("IRMS", System.StringComparison.Ordinal)
                || jsonKey.StartsWith("AGM1", System.StringComparison.Ordinal)
                || jsonKey.StartsWith("AGM_heavy", System.StringComparison.Ordinal)
                || jsonKey.StartsWith("ARM", System.StringComparison.Ordinal)
                || jsonKey.StartsWith("Rocket", System.StringComparison.Ordinal)
                || jsonKey.StartsWith("bomb", System.StringComparison.OrdinalIgnoreCase)
                || jsonKey.StartsWith("nuclearBomb", System.StringComparison.Ordinal)
                || jsonKey.IndexOf("Genie", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string MakeCloneKey(string originalKey, RcGuidanceKind guidance)
        {
            return originalKey + (guidance == RcGuidanceKind.Satcom ? SatSuffix : DlSuffix);
        }

        internal static string MakeDisplayName(string originalName, RcGuidanceKind guidance)
        {
            string prefix = guidance == RcGuidanceKind.Satcom ? SatPrefix : DlPrefix;
            if (string.IsNullOrEmpty(originalName))
                return prefix.Trim();
            if (originalName.StartsWith(DlPrefix, System.StringComparison.Ordinal)
                || originalName.StartsWith(SatPrefix, System.StringComparison.Ordinal))
                return originalName;
            return prefix + originalName;
        }
    }
}
