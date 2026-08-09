using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// RC aim terrain safety. Snap along the commanded look ray — never raise-Y at far XZ
    /// (that flattened dive commands into shallow glides).
    /// </summary>
    internal static class RcBallisticImpactSafety
    {
        private const float SurfaceClearanceM = 4f;
        private const float MinRayM = 50f;

        private static readonly LayerMask GroundMask =
            PhysicsLayers.StaticsMask | PhysicsLayers.WaterMask;

        private static int _missileId;
        private static bool _isBallistic;

        internal static void Reset()
        {
            _missileId = 0;
            _isBallistic = false;
        }

        internal static bool IsBallisticRc(Missile missile)
        {
            EnsureCached(missile);
            return _isBallistic;
        }

        /// <summary>Legacy: prefer <see cref="ResolveAimPoint"/>.</summary>
        internal static Vector3 ClampAimToSurface(Missile missile, Vector3 origin, Vector3 aimLocal, float maxDist)
        {
            Vector3 delta = aimLocal - origin;
            float dist = delta.magnitude;
            if (dist < 1f)
                return aimLocal;
            return ResolveAimPoint(origin, delta / dist, Mathf.Max(dist, MinRayM));
        }

        /// <summary>Legacy height-only — redirects to ray resolve when origin known via missile.</summary>
        internal static Vector3 ClampAimHeight(Missile missile, Vector3 aimLocal)
        {
            if (missile == null)
                return aimLocal;
            Vector3 origin = missile.transform.position;
            Vector3 delta = aimLocal - origin;
            float dist = delta.magnitude;
            if (dist < 1f)
                return aimLocal;
            return ResolveAimPoint(origin, delta / dist, dist);
        }

        /// <summary>
        /// Aim along <paramref name="dir"/> up to <paramref name="maxDist"/>.
        /// If the ray hits ground/water/sea plane first, stop there (keeps dive angle).
        /// </summary>
        internal static Vector3 ResolveAimPoint(Vector3 origin, Vector3 dir, float maxDist)
        {
            if (dir.sqrMagnitude < 1e-8f)
                return origin + Vector3.forward * Mathf.Max(maxDist, MinRayM);

            dir.Normalize();
            float dist = Mathf.Max(maxDist, MinRayM);
            float hitDist = dist;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, GroundMask, QueryTriggerInteraction.Ignore))
                hitDist = Mathf.Max(MinRayM * 0.5f, hit.distance - SurfaceClearanceM);

            // Sea plane intersection (dir pointing down through sea).
            try
            {
                float sea = Datum.LocalSeaY + SurfaceClearanceM;
                if (dir.y < -1e-4f && origin.y > sea)
                {
                    float tSea = (sea - origin.y) / dir.y;
                    if (tSea > 0f && tSea < hitDist)
                        hitDist = Mathf.Max(MinRayM * 0.5f, tSea);
                }
            }
            catch
            {
                // ignore
            }

            return origin + dir * hitDist;
        }

        internal static void FixedTick(Missile missile)
        {
        }

        private static void EnsureCached(Missile missile)
        {
            int id = missile.GetInstanceID();
            if (id == _missileId)
                return;

            _missileId = id;
            _isBallistic = false;

            RcMissileTag? tag = missile.GetComponent<RcMissileTag>();
            if (tag != null && tag.Engine == RcEngineKind.Solid)
                _isBallistic = true;

            MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
            if (seeker is BallisticMissileGuidance)
                _isBallistic = true;
        }
    }
}
