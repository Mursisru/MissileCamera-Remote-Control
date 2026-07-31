using MissileCameraRemoteControl.Config;

namespace MissileCameraRemoteControl
{
    /// <summary>Guidance channel labels for encyclopedia / hangar (not part of weapon display names).</summary>
    internal static class GuidanceLabels
    {
        internal const string DataLink = "DL";
        internal const string Satcom = "SATCOM";

        internal static string For(RcGuidanceKind kind) =>
            kind == RcGuidanceKind.Satcom ? Satcom : DataLink;

        internal static bool TryFromWeaponName(string? weaponName, out string label)
        {
            label = string.Empty;
            if (!CloneProfile.TryGetGuidanceFromRcName(weaponName, out RcGuidanceKind kind))
                return false;
            label = For(kind);
            return true;
        }

        internal static bool TryFromUnitName(string? unitName, out string label) =>
            TryFromWeaponName(unitName, out label);
    }
}
