using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// RC AAM proximity burst: horizontal + vertical miss vs locked target inside range.
    /// AAM-46 always; other AAM seekers when General.AllowAnyMunition is on.
    /// </summary>
    internal static class RcAamProximityDetonate
    {
        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.OwnsMissile(missile))
                return;
            if (!RcConfig.AamProximityDetonate.Value)
                return;
            if (!MissileAccess.IsAirToAirMunition(missile))
                return;

            Unit? target = MissileAccess.TryGetLockedTarget(missile);
            if (target == null || target.disabled)
                return;

            try
            {
                if (!missile.IsArmed())
                    return;
            }
            catch
            {
                return;
            }

            Vector3 mPos = missile.transform.position;
            Vector3 tPos;
            try
            {
                tPos = target.transform.position;
            }
            catch
            {
                return;
            }

            Vector3 rel = tPos - mPos;
            float range = rel.magnitude;
            float maxRange = Mathf.Max(5f, RcConfig.AamProxMaxRangeM.Value);
            if (range > maxRange || range < 0.5f)
                return;

            float horizMiss = new Vector2(rel.x, rel.z).magnitude;
            float vertMiss = Mathf.Abs(rel.y);
            float horizGate = Mathf.Max(1f, RcConfig.AamProxHorizM.Value);
            float vertGate = Mathf.Max(1f, RcConfig.AamProxVertM.Value);

            if (horizMiss > horizGate || vertMiss > vertGate)
                return;

            // Require closing — avoid burst while target is still ahead but offset.
            try
            {
                Vector3 vel = missile.rb != null ? missile.rb.velocity : missile.transform.forward;
                if (vel.sqrMagnitude > 1f && Vector3.Dot(vel, rel) <= 0f)
                    return;
            }
            catch
            {
                // ignore
            }

            Vector3 n = missile.rb != null && missile.rb.velocity.sqrMagnitude > 1f
                ? missile.rb.velocity
                : missile.transform.forward;
            RcDetonateUtil.Force(missile, n, hitTerrain: false);
            RemoteControlSession.Release(silent: true);
        }
    }
}
