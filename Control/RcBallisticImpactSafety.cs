using System.Reflection;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.HarmonyPatches;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Ballistic / solid RC under player control: Seek is skipped so stock Fusing airburst
    /// never runs, and aim along look*AimDistance can sit under terrain → CCD tunnel.
    /// Clamp aim to surface; detonate if buried; run airburst check while Owned.
    /// </summary>
    internal static class RcBallisticImpactSafety
    {
        private const float SurfaceClearanceM = 2f;
        private const float BuriedDepthM = 8f;
        private const float ExtraLookAheadSec = 0.12f;
        private const float MinLookAheadM = 40f;

        private static readonly FieldInfo? AirburstHeightField =
            typeof(BallisticMissileGuidance).GetField(
                "airburstHeight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? KnownPosField =
            typeof(BallisticMissileGuidance).GetField(
                "knownPos", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly LayerMask TerrainMask =
            PhysicsLayers.StaticsMask | PhysicsLayers.WaterMask;

        private static int _missileId;
        private static bool _isBallistic;
        private static BallisticMissileGuidance? _ballistic;

        internal static void Reset()
        {
            _missileId = 0;
            _isBallistic = false;
            _ballistic = null;
        }

        internal static bool IsBallisticRc(Missile missile)
        {
            EnsureCached(missile);
            return _isBallistic;
        }

        /// <summary>Keep aimpoint on / above terrain for ballistic RC stick.</summary>
        internal static Vector3 ClampAimToSurface(Missile missile, Vector3 origin, Vector3 aimLocal, float maxDist)
        {
            if (!IsBallisticRc(missile))
                return aimLocal;

            Vector3 delta = aimLocal - origin;
            float dist = delta.magnitude;
            if (dist < 1f)
                return aimLocal;

            Vector3 dir = delta / dist;
            float cast = Mathf.Min(dist, Mathf.Max(200f, maxDist));
            if (Physics.Raycast(origin, dir, out RaycastHit hit, cast, TerrainMask, QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * SurfaceClearanceM;

            // Aim below sea: lift to surface.
            try
            {
                float sea = Datum.LocalSeaY;
                if (aimLocal.y < sea + SurfaceClearanceM)
                {
                    aimLocal.y = sea + SurfaceClearanceM;
                    return aimLocal;
                }
            }
            catch
            {
                // ignore
            }

            return aimLocal;
        }

        internal static void FixedTick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;
            if (!RemoteControlSession.OwnsMissile(missile))
                return;
            if (!IsBallisticRc(missile))
                return;

            try
            {
                TryAirburst(missile);
                TryExtraImpactRay(missile);
                TryBuriedDetonate(missile);
            }
            catch
            {
                // ignore
            }
        }

        private static void EnsureCached(Missile missile)
        {
            int id = missile.GetInstanceID();
            if (id == _missileId)
                return;

            _missileId = id;
            _ballistic = null;
            _isBallistic = false;

            RcMissileTag? tag = missile.GetComponent<RcMissileTag>();
            if (tag != null && tag.Engine == RcEngineKind.Solid)
                _isBallistic = true;

            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            if (seeker is BallisticMissileGuidance bal)
            {
                _ballistic = bal;
                _isBallistic = true;
            }
        }

        private static void TryAirburst(Missile missile)
        {
            BallisticMissileGuidance? bal = _ballistic;
            if (bal == null || AirburstHeightField == null || KnownPosField == null)
                return;

            float airburst;
            GlobalPosition known;
            try
            {
                airburst = (float)AirburstHeightField.GetValue(bal)!;
                known = (GlobalPosition)KnownPosField.GetValue(bal)!;
            }
            catch
            {
                return;
            }

            if (airburst <= 0f)
                return;
            if (missile.timeSinceSpawn <= 30f)
                return;
            if (!missile.IsTangible())
                return;

            float rel = missile.GlobalPosition().y - known.y;
            if (rel >= airburst)
                return;

            ForceDetonate(missile, missile.rb != null ? missile.rb.velocity : Vector3.up, hitTerrain: false);
        }

        private static void TryExtraImpactRay(Missile missile)
        {
            if (missile.rb == null)
                return;
            if (!missile.IsArmed() && missile.timeSinceSpawn < 2f)
                return;

            Vector3 vel = missile.rb.velocity;
            float speed = vel.magnitude;
            if (speed < 30f)
                return;

            Vector3 pos = missile.transform.position;
            float look = Mathf.Max(MinLookAheadM, speed * ExtraLookAheadSec);
            Vector3 end = pos + vel * (look / speed);

            int mask = missile.IsTangible()
                ? ~PhysicsLayers.ExclusionZonesMask.value
                : PhysicsLayers.StaticsMask.value;

            if (!Physics.Linecast(pos, end, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
                return;

            // Match vanilla: skip soft penetrate-only if we can; prefer hard statics/ships.
            ForceDetonate(missile, hit.normal, hitTerrain: true);
        }

        private static void TryBuriedDetonate(Missile missile)
        {
            Vector3 pos = missile.transform.position;
            try
            {
                if (pos.y < Datum.LocalSeaY - 2f)
                {
                    ForceDetonate(missile, Vector3.up, hitTerrain: false);
                    return;
                }
            }
            catch
            {
                // ignore
            }

            Vector3 high = pos + Vector3.up * 8000f;
            Vector3 low = pos + Vector3.down * 200f;
            if (!Physics.Linecast(high, low, out RaycastHit hit, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
                return;

            if (pos.y >= hit.point.y - BuriedDepthM)
                return;

            ForceDetonate(missile, hit.normal, hitTerrain: true);
        }

        private static void ForceDetonate(Missile missile, Vector3 normal, bool hitTerrain)
        {
            if (missile == null || missile.disabled)
                return;

            RcDetonateGate.AllowBegin();
            try
            {
                try
                {
                    if (!missile.IsArmed())
                        missile.Arm();
                }
                catch
                {
                    // ignore
                }

                missile.Detonate(normal, hitArmor: false, hitTerrain: hitTerrain);
            }
            catch
            {
                // ignore
            }
            finally
            {
                RcDetonateGate.AllowEnd();
            }
        }
    }
}
