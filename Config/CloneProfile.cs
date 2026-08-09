using System;

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

    /// <summary>Static whitelist: mount jsonKey → guidance + engine. Display names are remapped (no DL/SATCOM prefixes).</summary>
    internal static class CloneProfile
    {
        internal const string DlSuffix = "_RC_DL";
        internal const string SatSuffix = "_RC_SAT";

        // Legacy prefixes — stripped if present on old saves / leftover cfg.
        internal const string DlPrefix = "[DL] ";
        internal const string SatPrefix = "[SATCOM] ";

        internal const string NameAgm98D = "AGM-98D";
        internal const string NameDlhm300S = "DLhM-300S";
        internal const string NameAlmD500 = "ALM-D500";
        internal const string NameTuskoD = "Tusko-D";
        internal const string NameAlnd4S = "ALND-4S";
        internal const string NamePiledriverTbmS = "Piledriver TBM-S";
        internal const string NameAgm68D = "AGM-68D";
        internal const string NameAam46Longstrong = "AAM-46 Longstrong";
        internal const string Name76mmDlgShell = "76mm DLG Shell";

        private static readonly string[] RcDisplayNames =
        {
            NameAgm98D,
            NameDlhm300S,
            NameAlmD500,
            NameTuskoD,
            NameAlnd4S,
            NamePiledriverTbmS,
            NameAgm68D,
            NameAam46Longstrong,
            Name76mmDlgShell
        };

        internal static bool IsRcCloneKey(string? jsonKey)
        {
            if (string.IsNullOrEmpty(jsonKey))
                return false;
            return jsonKey!.EndsWith(DlSuffix, StringComparison.Ordinal)
                || jsonKey.EndsWith(SatSuffix, StringComparison.Ordinal);
        }

        internal static bool IsRcDisplayName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string bare = StripLegacyPrefix(name!);
            SplitWarheadSuffix(bare, out string core, out _);
            for (int i = 0; i < RcDisplayNames.Length; i++)
            {
                if (string.Equals(core, RcDisplayNames[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal static bool IsPassiveShellDisplayName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            SplitWarheadSuffix(StripLegacyPrefix(name!), out string core, out _);
            return string.Equals(core, Name76mmDlgShell, StringComparison.Ordinal);
        }

        /// <summary>Returns false for excluded / already-cloned mounts.</summary>
        internal static bool TryResolve(
            string jsonKey,
            string? weaponName,
            out RcGuidanceKind guidance,
            out RcEngineKind engine)
        {
            return TryResolve(jsonKey, weaponName, out guidance, out engine, out _);
        }

        internal static bool TryResolve(
            string jsonKey,
            string? weaponName,
            out RcGuidanceKind guidance,
            out RcEngineKind engine,
            out bool controllable)
        {
            guidance = RcGuidanceKind.DataLink;
            engine = RcEngineKind.Jet;
            controllable = true;

            if (string.IsNullOrEmpty(jsonKey) || IsRcCloneKey(jsonKey))
                return false;

            string key = jsonKey;
            string name = weaponName ?? string.Empty;

            // Positive whitelist first (before broad excludes).
            if (Is76mmGuided(key, name))
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                controllable = false;
                return true;
            }

            if (key.StartsWith("AGM1", StringComparison.Ordinal)
                || Contains(name, "AGM-68"))
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            // AAM-46 Longstrong ONLY from AAM-36 — never AAM-29 / blanket AAM2* (duplicate models).
            if (IsAam36Source(name) && !Contains(name, "AAM-29"))
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (IsExcluded(key))
                return false;

            if (key.IndexOf("20kt", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ALND", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.Satcom;
                engine = RcEngineKind.Jet;
                return key.StartsWith("CruiseMissile", StringComparison.Ordinal);
            }

            if (key.StartsWith("BallisticMissile", StringComparison.Ordinal)
                || name.IndexOf("Piledriver", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.Satcom;
                engine = RcEngineKind.Solid;
                return true;
            }

            if (key.StartsWith("CruiseMissile", StringComparison.Ordinal)
                || name.IndexOf("ALM-C450", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (key.StartsWith("AShM1", StringComparison.Ordinal)
                || name.IndexOf("AShM-300", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (key.StartsWith("AShM2", StringComparison.Ordinal)
                || name.IndexOf("AGM-99", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Jet;
                return true;
            }

            if (key.StartsWith("AShM3", StringComparison.Ordinal)
                || name.IndexOf("Tusko", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                guidance = RcGuidanceKind.DataLink;
                engine = RcEngineKind.Solid;
                return true;
            }

            return false;
        }

        internal static bool TryResolveEngineFromWeaponName(string weaponName, out RcEngineKind engine)
        {
            engine = RcEngineKind.Jet;
            if (string.IsNullOrEmpty(weaponName))
                return false;

            string n = StripLegacyPrefix(weaponName);
            if (n.IndexOf("Tusko", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Piledriver", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(n, NameTuskoD, StringComparison.Ordinal)
                || string.Equals(n, NamePiledriverTbmS, StringComparison.Ordinal))
            {
                engine = RcEngineKind.Solid;
                return true;
            }

            engine = RcEngineKind.Jet;
            return true;
        }

        internal static bool TryGetGuidanceFromRcName(string? weaponName, out RcGuidanceKind guidance)
        {
            guidance = RcGuidanceKind.DataLink;
            if (string.IsNullOrEmpty(weaponName))
                return false;

            string n = StripLegacyPrefix(weaponName!);
            SplitWarheadSuffix(n, out string core, out _);

            if (string.Equals(core, NameAlnd4S, StringComparison.Ordinal)
                || string.Equals(core, NamePiledriverTbmS, StringComparison.Ordinal))
            {
                guidance = RcGuidanceKind.Satcom;
                return true;
            }

            if (IsRcDisplayName(n))
            {
                guidance = RcGuidanceKind.DataLink;
                return true;
            }

            // Legacy prefixed names
            if (weaponName!.StartsWith(SatPrefix, StringComparison.Ordinal))
            {
                guidance = RcGuidanceKind.Satcom;
                return true;
            }

            if (weaponName.StartsWith(DlPrefix, StringComparison.Ordinal))
            {
                guidance = RcGuidanceKind.DataLink;
                return true;
            }

            return false;
        }

        private static bool IsExcluded(string jsonKey)
        {
            return jsonKey.StartsWith("AAM1", StringComparison.Ordinal)
                || jsonKey.StartsWith("IRMS", StringComparison.Ordinal)
                || jsonKey.StartsWith("AGM_heavy", StringComparison.Ordinal)
                || jsonKey.StartsWith("ARM", StringComparison.Ordinal)
                || jsonKey.StartsWith("Rocket", StringComparison.Ordinal)
                || jsonKey.StartsWith("bomb", StringComparison.OrdinalIgnoreCase)
                || jsonKey.StartsWith("nuclearBomb", StringComparison.Ordinal)
                || jsonKey.IndexOf("Genie", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Is76mmGuided(string key, string name)
        {
            if (key.StartsWith("76mm", StringComparison.OrdinalIgnoreCase))
                return true;
            return Contains(name, "76mm") && Contains(name, "Guided");
        }

        /// <summary>Strict AAM-36 family — do not use AAM2* key alone (AAM-29 must not become Longstrong).</summary>
        private static bool IsAam36Source(string name) =>
            Contains(name, "AAM-36");

        internal static string MakeCloneKey(string originalKey, RcGuidanceKind guidance)
        {
            return originalKey + (guidance == RcGuidanceKind.Satcom ? SatSuffix : DlSuffix);
        }

        /// <summary>
        /// RC display name = remapped designation + stock warhead tag only where vanilla uses it:
        /// (HE) = Tusko + non-nuclear Piledriver; (20kt) = ALND + nuclear Piledriver.
        /// </summary>
        internal static string MakeDisplayName(
            string? weaponName,
            RcGuidanceKind guidance,
            string? jsonKey = null,
            string? shortName = null)
        {
            string primary = StripLegacyPrefix(weaponName ?? string.Empty);
            string secondary = StripLegacyPrefix(shortName ?? string.Empty);

            string mappedCore;
            if (!TryRemapCore(primary, out mappedCore)
                && !TryRemapCore(secondary, out mappedCore)
                && !TryRemapFromKey(jsonKey, out mappedCore))
            {
                if (!string.IsNullOrEmpty(primary))
                {
                    SplitWarheadSuffix(primary, out string core, out _);
                    mappedCore = string.IsNullOrEmpty(core) ? primary : core;
                }
                else
                {
                    mappedCore = "RC Munition";
                }
            }

            return mappedCore + ResolveWarheadTag(mappedCore, jsonKey, weaponName, shortName);
        }

        /// <summary>
        /// (HE) only Tusko + conventional Piledriver; (20kt) only ALND + nuclear Piledriver; else none.
        /// </summary>
        internal static string ResolveWarheadTag(string mappedCore, string? jsonKey, params string?[] names)
        {
            bool nuke = IsNuclearFamily(jsonKey, names);

            if (string.Equals(mappedCore, NameAlnd4S, StringComparison.Ordinal))
                return " (20kt)";

            if (string.Equals(mappedCore, NamePiledriverTbmS, StringComparison.Ordinal))
                return nuke ? " (20kt)" : " (HE)";

            if (string.Equals(mappedCore, NameTuskoD, StringComparison.Ordinal))
                return " (HE)";

            return string.Empty;
        }

        private static bool IsNuclearFamily(string? jsonKey, string?[] names)
        {
            if (!string.IsNullOrEmpty(jsonKey)
                && (jsonKey!.IndexOf("20kt", StringComparison.OrdinalIgnoreCase) >= 0
                    || jsonKey.IndexOf("tacNuke", StringComparison.OrdinalIgnoreCase) >= 0
                    || jsonKey.IndexOf("Nuke", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            for (int i = 0; i < names.Length; i++)
            {
                string? n = names[i];
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n!.IndexOf("ALND", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (n.IndexOf("20kt", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        internal static string StripLegacyPrefix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            if (name.StartsWith(SatPrefix, StringComparison.Ordinal))
                return name.Substring(SatPrefix.Length);
            if (name.StartsWith(DlPrefix, StringComparison.Ordinal))
                return name.Substring(DlPrefix.Length);
            return name;
        }

        /// <summary>Split "ALND-4 (20kt)" → core="ALND-4", suffix=" (20kt)".</summary>
        internal static void SplitWarheadSuffix(string name, out string core, out string suffix)
        {
            suffix = string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                core = string.Empty;
                return;
            }

            core = name;
            int open = name.LastIndexOf('(');
            int close = name.LastIndexOf(')');
            if (open <= 0 || close <= open)
                return;

            int start = open;
            if (name[open - 1] == ' ')
                start = open - 1;

            suffix = name.Substring(start);
            core = name.Substring(0, start).TrimEnd();
        }

        private static bool TryRemapFromKey(string? jsonKey, out string mappedCore)
        {
            mappedCore = string.Empty;
            if (string.IsNullOrEmpty(jsonKey))
                return false;
            if (jsonKey!.StartsWith("AGM1", StringComparison.Ordinal))
            {
                mappedCore = NameAgm68D;
                return true;
            }

            // Never remap bare AAM2* → Longstrong (would alias AAM-29 racks too).
            if (jsonKey.StartsWith("76mm", StringComparison.OrdinalIgnoreCase))
            {
                mappedCore = Name76mmDlgShell;
                return true;
            }

            return false;
        }

        private static bool TryRemapCore(string name, out string mappedCore)
        {
            mappedCore = string.Empty;
            if (string.IsNullOrEmpty(name))
                return false;

            SplitWarheadSuffix(name, out string core, out _);

            for (int i = 0; i < RcDisplayNames.Length; i++)
            {
                if (string.Equals(core, RcDisplayNames[i], StringComparison.Ordinal))
                {
                    mappedCore = core;
                    return true;
                }
            }

            if (Contains(core, "Piledriver"))
            {
                mappedCore = NamePiledriverTbmS;
                return true;
            }

            if (Contains(core, "ALND"))
            {
                mappedCore = NameAlnd4S;
                return true;
            }

            if (Contains(core, "ALM-C450") || Contains(core, "ALM-C") || Contains(core, "ALM-D500"))
            {
                mappedCore = NameAlmD500;
                return true;
            }

            if (Contains(core, "AGM-99") || Contains(core, "AGM-98"))
            {
                mappedCore = NameAgm98D;
                return true;
            }

            if (Contains(core, "AGM-68"))
            {
                mappedCore = NameAgm68D;
                return true;
            }

            if (Contains(core, "AAM-36") || string.Equals(core, NameAam46Longstrong, StringComparison.Ordinal))
            {
                mappedCore = NameAam46Longstrong;
                return true;
            }

            // Explicitly never remap AAM-29 → Longstrong
            if (Contains(core, "AAM-29"))
                return false;

            if ((Contains(core, "76mm") && Contains(core, "Guided")) || Contains(core, "DLG Shell"))
            {
                mappedCore = Name76mmDlgShell;
                return true;
            }

            if (Contains(core, "AShM-300") || Contains(core, "DLhM-300"))
            {
                mappedCore = NameDlhm300S;
                return true;
            }

            if (Contains(core, "Tusko"))
            {
                mappedCore = NameTuskoD;
                return true;
            }

            return false;
        }

        private static bool Contains(string hay, string needle) =>
            hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
