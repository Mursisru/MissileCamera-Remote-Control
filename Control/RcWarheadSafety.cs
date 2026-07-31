using System.Reflection;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Skipping seeker.Seek() under RC also skips Arm / SetTangible / DeployFins / proxy setup.
    /// Without those, impacts fizzle (no blast) and unit collisions may never register.
    /// </summary>
    internal static class RcWarheadSafety
    {
        private const float FinDelay = 0.5f;
        private const float TangibleDelay = 1.5f;
        private const float ArmDelay = 2f;

        private static readonly FieldInfo? TargetField =
            typeof(Missile).GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;

            float age = 0f;
            try
            {
                age = missile.timeSinceSpawn;
            }
            catch
            {
                return;
            }

            try
            {
                if (age > FinDelay)
                    missile.DeployFins();
            }
            catch
            {
                // ignore
            }

            try
            {
                if (age > TangibleDelay && !missile.IsTangible())
                    missile.SetTangible(true);
            }
            catch
            {
                // ignore
            }

            try
            {
                if (age > ArmDelay && !missile.IsArmed())
                    missile.Arm();
            }
            catch
            {
                // ignore
            }

            // Proximity fuse needs a target transform — normally set inside Seek().
            try
            {
                Unit? target = TargetField?.GetValue(missile) as Unit;
                if (target != null && !target.disabled && target.transform != null)
                {
                    Rigidbody? trb = null;
                    try
                    {
                        trb = target.rb;
                    }
                    catch
                    {
                        trb = target.GetComponent<Rigidbody>();
                    }

                    missile.SetProxyFuse(target.transform, trb);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
