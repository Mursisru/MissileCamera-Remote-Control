using System;
using System.Reflection;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Access
{
    /// <summary>Safe accessors for Missile private motor / seeker fields (Motor is a private nested type).</summary>
    internal static class MissileAccess
    {
        internal static readonly Type? MotorType =
            typeof(Missile).GetNestedType("Motor", BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? MotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? SeekerField =
            typeof(Missile).GetField("seeker", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? BurnRateField =
            MotorType?.GetField("burnRate", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? TopSpeedField =
            MotorType?.GetField("topSpeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo? ParticleSystemsField =
            MotorType?.GetField("particleSystems", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? WeaponInfoField =
            typeof(Weapon).GetField("info", BindingFlags.Instance | BindingFlags.Public)
            ?? typeof(Weapon).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MissileInfoField =
            typeof(Missile).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? ProxyFuseField =
            typeof(Missile).GetField("proxyFuse", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo? ThrustMethod =
            MotorType?.GetMethod("Thrust", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static MethodInfo? MotorThrustMethod => ThrustMethod;

        /// <summary>
        /// Null private ProxyFuse — while RC, near-miss CPA airburst must not fire
        /// (vanilla Detonate from DetectCollisions when ConditionsMet).
        /// </summary>
        internal static void ClearProxyFuse(Missile? missile)
        {
            if (missile == null || ProxyFuseField == null)
                return;
            try
            {
                ProxyFuseField.SetValue(missile, null);
            }
            catch
            {
                // ignore
            }
        }

        internal static Array? GetMotors(Missile missile)
        {
            if (missile == null || MotorsField == null)
                return null;
            try
            {
                return MotorsField.GetValue(missile) as Array;
            }
            catch
            {
                return null;
            }
        }

        internal static MissileSeeker? GetSeeker(Missile missile)
        {
            if (missile == null || SeekerField == null)
                return null;
            try
            {
                return SeekerField.GetValue(missile) as MissileSeeker;
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryGetBurnRate(object motor, out float burnRate)
        {
            burnRate = 0f;
            if (motor == null || BurnRateField == null)
                return false;
            try
            {
                burnRate = (float)BurnRateField.GetValue(motor)!;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TrySetBurnRate(object motor, float burnRate)
        {
            if (motor == null || BurnRateField == null)
                return false;
            try
            {
                BurnRateField.SetValue(motor, burnRate);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetTopSpeed(object motor, out float topSpeed)
        {
            topSpeed = 0f;
            if (motor == null || TopSpeedField == null)
                return false;
            try
            {
                topSpeed = (float)TopSpeedField.GetValue(motor)!;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TrySetTopSpeed(object motor, float topSpeed)
        {
            if (motor == null || TopSpeedField == null)
                return false;
            try
            {
                TopSpeedField.SetValue(motor, topSpeed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static ParticleSystem[]? GetMotorParticles(object motor)
        {
            if (motor == null || ParticleSystemsField == null)
                return null;
            try
            {
                return ParticleSystemsField.GetValue(motor) as ParticleSystem[];
            }
            catch
            {
                return null;
            }
        }

        internal static void SetWeaponInfo(Weapon weapon, WeaponInfo info)
        {
            if (weapon == null || info == null || WeaponInfoField == null)
                return;
            try
            {
                WeaponInfoField.SetValue(weapon, info);
            }
            catch
            {
                // ignore
            }
        }

        internal static WeaponInfo? GetMissileInfo(Missile missile)
        {
            if (missile == null || MissileInfoField == null)
                return null;
            try
            {
                return MissileInfoField.GetValue(missile) as WeaponInfo;
            }
            catch
            {
                return null;
            }
        }

        internal static bool IsRcMissile(Missile? missile)
        {
            if (missile == null)
                return false;
            if (missile.GetComponent<RcMissileTag>() != null)
                return true;
            try
            {
                WeaponInfo? info = GetMissileInfo(missile);
                if (info == null || string.IsNullOrEmpty(info.weaponName))
                    return false;
                return CloneProfile.IsRcDisplayName(info.weaponName)
                    || CloneProfile.TryGetGuidanceFromRcName(info.weaponName, out _);
            }
            catch
            {
                return false;
            }
        }
    }
}
