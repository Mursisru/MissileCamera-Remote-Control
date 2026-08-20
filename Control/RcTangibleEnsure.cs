using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Vanilla ARH/SARH Seek can return before SetTangible when targetID is cleared (no lock).
    /// Missile stays on IgnoreCollisions — enemies cannot hit it. Same owner-clear rule as launch safety.
    /// </summary>
    internal static class RcTangibleEnsure
    {
        private const float OwnerClearM = 20f;
        private const float OwnerNullMinAgeS = 3f;

        private static readonly FieldInfo? SeekerMissileField =
            AccessTools.Field(typeof(MissileSeeker), "missile");

        internal static Missile? GetMissile(MissileSeeker? seeker)
        {
            if (seeker == null || SeekerMissileField == null)
                return null;
            try
            {
                return SeekerMissileField.GetValue(seeker) as Missile;
            }
            catch
            {
                return null;
            }
        }

        internal static bool TrySet(Missile? missile)
        {
            if (missile == null || missile.disabled)
                return false;

            try
            {
                if (missile.IsTangible())
                    return true;
                if (!IsClearOfOwner(missile))
                    return false;

                missile.SetTangible(true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsClearOfOwner(Missile missile)
        {
            try
            {
                Unit? owner = missile.owner;
                if (owner == null || owner.disabled)
                    return missile.timeSinceSpawn > OwnerNullMinAgeS;

                return !FastMath.InRange(
                    owner.GlobalPosition(),
                    missile.GlobalPosition(),
                    OwnerClearM);
            }
            catch
            {
                return missile.timeSinceSpawn > OwnerNullMinAgeS;
            }
        }
    }
}
