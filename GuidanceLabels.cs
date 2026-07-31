using MissileCameraRemoteControl.Config;

namespace MissileCameraRemoteControl
{
    /// <summary>Display strings for RC guidance channels (encyclopedia + loadout).</summary>
    internal static class GuidanceLabels
    {
        internal const string DataLink = "DL";
        internal const string Satcom = "SATCOM";

        internal static string For(RcGuidanceKind kind) =>
            kind == RcGuidanceKind.Satcom ? Satcom : DataLink;

        internal static bool TryFromWeaponName(string? weaponName, out string label)
        {
            label = string.Empty;
            if (string.IsNullOrEmpty(weaponName))
                return false;
            if (weaponName!.StartsWith(CloneProfile.SatPrefix, System.StringComparison.Ordinal))
            {
                label = Satcom;
                return true;
            }
            if (weaponName.StartsWith(CloneProfile.DlPrefix, System.StringComparison.Ordinal))
            {
                label = DataLink;
                return true;
            }
            return false;
        }

        internal static bool TryFromUnitName(string? unitName, out string label) =>
            TryFromWeaponName(unitName, out label);
    }
}
