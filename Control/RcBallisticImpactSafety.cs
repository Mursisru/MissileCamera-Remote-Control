using System.Reflection;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.HarmonyPatches;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// While player OwnsMissile: Seek (and its SD/airburst) is skipped / gated.
    /// Long flights can drive aim under terrain → CCD tunnel, or soft-land with no blast.
    /// Clamp aim; extra impact ray; burial + slow near-ground detonate; ballistic airburst.
    /// </summary>
    internal static class RcBallisticImpactSafety
    {
        private const float SurfaceClearanceM = 2f;
        private const float BuriedDepthM = 5f;
        private const float ExtraLookAheadSec = 0.18f;
        private const float MinLookAheadM = 50f;
        private const float SoftLandSpeed = 90f;
        private const float SoftLandAltM = 25f;
        private const float SoftLandMinAge = 8f;

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

        /// <summary>Keep aimpoint on / above terrain for any owned RC stick (cruise + ballistic).</summary>
        internal static Vector3 ClampAimToSurface(Missile missile, Vector3 origin, Vector3 aimLocal, float maxDist)
        {
            if (missile == null)
                return aimLocal;

            Vector3 delta = aimLocal - origin;
            float dist = delta.magnitude;
            if (dist < 1f)
                return aimLocal;

            Vector3 dir = delta / dist;
            float cast = Mathf.Min(dist, Mathf.Max(200f, maxDist));
            if (Physics.Raycast(origin, dir, out RaycastHit hit, cast, TerrainMask, QueryTriggerInteraction.Ignore))
                return hit.point + hit.normal * SurfaceClearanceM;

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

            EnsureCached(missile);

            try
            {
                if (_isBallistic)
                    TryAirburst(missile);
                TryExtraImpactRay(missile);
                TryBuriedDetonate(missile);
                TrySoftLandDetonate(missile);
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
            if (speed < 25f)
                return;

            Vector3 pos = missile.transform.position;
            float look = Mathf.Max(MinLookAheadM, speed * ExtraLookAheadSec);
            Vector3 end = pos + vel * (look / speed);

            int mask = missile.IsTangible()
                ? ~PhysicsLayers.ExclusionZonesMask.value
                : PhysicsLayers.StaticsMask.value;

            if (!Physics.Linecast(pos, end, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore))
                return;

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

        /// <summary>Slow near-ground under RC — vanilla Seek SD is gated, so soft-land would fizzle.</summary>
        private static void TrySoftLandDetonate(Missile missile)
        {
            if (missile.timeSinceSpawn < SoftLandMinAge)
                return;
            if (missile.rb == null)
                return;

            float speed = missile.rb.velocity.magnitude;
            if (speed > SoftLandSpeed)
                return;

            Vector3 pos = missile.transform.position;
            float alt = SoftLandAltM + 1f;
            try
            {
                alt = missile.radarAlt;
            }
            catch
            {
                if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, SoftLandAltM + 50f,
                        PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
                    alt = hit.distance;
            }

            try
            {
                float seaClear = pos.y - Datum.LocalSeaY;
                if (seaClear < SoftLandAltM && seaClear < alt)
                    alt = seaClear;
            }
            catch
            {
                // ignore
            }

            if (alt > SoftLandAltM)
                return;

            ForceDetonate(missile, Vector3.up, hitTerrain: true);
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
