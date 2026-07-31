using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// While RC: stock Select locks unit under FS reticle onto the controlled missile;
    /// Cancel clears missile lock; Cancel-hold clears all aircraft targets too.
    /// </summary>
    internal static class RcRetargetController
    {
        private const float PickRadiusPx = 120f;

        private static readonly FieldInfo? MarkersField =
            AccessTools.Field(typeof(CombatHUD), "markers");

        private static readonly FieldInfo? SeekerTargetField =
            typeof(MissileSeeker).GetField("targetUnit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static MethodInfo? _timedPressUp3;
        private static MethodInfo? _timedPressDown2;
        private static bool _inputMethodsResolved;

        /// <summary>Harmony: swallow vanilla HUD TargetSelect while RC owns Select.</summary>
        internal static bool BlockVanillaTargetSelect => RemoteControlSession.IsActive;

        internal static void Tick(Missile missile)
        {
            if (missile == null || !RemoteControlSession.IsControlling(missile))
                return;

            try
            {
                object? input = TryGetPlayerInput();
                if (input == null)
                    return;

                EnsureInputMethods(input);
                float clickDelay = PlayerSettings.clickDelay;
                float pressDelay = PlayerSettings.pressDelay;

                if (InvokeTimedPressUp(input, "Select", 0f, clickDelay))
                {
                    TryLockUnderReticle(missile);
                    return;
                }

                if (InvokeTimedPressDown(input, "Select", pressDelay))
                {
                    TryLockUnderReticle(missile);
                    return;
                }

                if (InvokeTimedPressDown(input, "Cancel", pressDelay))
                {
                    ClearMissileTarget(missile);
                    ClearAllAircraftTargets();
                    return;
                }

                if (InvokeTimedPressUp(input, "Cancel", 0f, clickDelay))
                    ClearMissileTarget(missile);
            }
            catch
            {
                // ignore
            }
        }

        private static object? TryGetPlayerInput()
        {
            try
            {
                PropertyInfo? prop = typeof(GameManager).GetProperty(
                    "playerInput",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                    return prop.GetValue(null);

                FieldInfo? field = typeof(GameManager).GetField(
                    "playerInput",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return field?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureInputMethods(object input)
        {
            if (_inputMethodsResolved)
                return;
            _inputMethodsResolved = true;
            System.Type t = input.GetType();
            _timedPressUp3 = t.GetMethod(
                "GetButtonTimedPressUp",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(float), typeof(float) },
                null);
            _timedPressDown2 = t.GetMethod(
                "GetButtonTimedPressDown",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(float) },
                null);
        }

        private static bool InvokeTimedPressUp(object input, string action, float min, float max)
        {
            if (_timedPressUp3 == null)
                return false;
            try
            {
                return (bool)_timedPressUp3.Invoke(input, new object[] { action, min, max })!;
            }
            catch
            {
                return false;
            }
        }

        private static bool InvokeTimedPressDown(object input, string action, float time)
        {
            if (_timedPressDown2 == null)
                return false;
            try
            {
                return (bool)_timedPressDown2.Invoke(input, new object[] { action, time })!;
            }
            catch
            {
                return false;
            }
        }

        internal static void ClearMissileTarget(Missile missile)
        {
            if (missile == null)
                return;
            try
            {
                missile.SetTarget(null);
            }
            catch
            {
                // ignore
            }

            TrySetSeekerTarget(missile, null);
        }

        private static void ClearAllAircraftTargets()
        {
            try
            {
                CombatHUD? hud = SceneSingleton<CombatHUD>.i;
                hud?.DeselectAll(true);
            }
            catch
            {
                // ignore
            }
        }

        private static void TryLockUnderReticle(Missile missile)
        {
            Unit? best = PickUnitNearReticle(missile);
            if (best == null)
                return;

            AssignMissileTarget(missile, best);
            RcSeekerHandoff.CommitForAutonomous(missile);
            RcPlugin.ModLogger?.LogInfo($"RC retarget → {best.unitName ?? best.name}");
        }

        private static void AssignMissileTarget(Missile missile, Unit unit)
        {
            try
            {
                missile.SetTarget(unit);
            }
            catch
            {
                // ignore
            }

            TrySetSeekerTarget(missile, unit);

            try
            {
                Transform? tf = unit.transform;
                Rigidbody? rb = null;
                try { rb = unit.rb; }
                catch { rb = unit.GetComponent<Rigidbody>(); }
                if (tf != null)
                    missile.SetProxyFuse(tf, rb);
            }
            catch
            {
                // ignore
            }

            // Reflect on HUD markers for feedback (aircraft list restored on RC exit).
            try
            {
                CombatHUD? hud = SceneSingleton<CombatHUD>.i;
                if (hud != null && hud.TryGetMarker(unit, out HUDUnitMarker marker) && marker != null)
                    marker.SelectMarker();
            }
            catch
            {
                // ignore
            }
        }

        private static void TrySetSeekerTarget(Missile missile, Unit? unit)
        {
            if (SeekerTargetField == null)
                return;
            try
            {
                MissileSeeker? seeker = MissileAccess.GetSeeker(missile) ?? missile.GetComponent<MissileSeeker>();
                if (seeker == null)
                    return;
                SeekerTargetField.SetValue(seeker, unit);
            }
            catch
            {
                // ignore
            }
        }

        private static Unit? PickUnitNearReticle(Missile missile)
        {
            CombatHUD? hud = null;
            try { hud = SceneSingleton<CombatHUD>.i; }
            catch { return null; }
            if (hud == null || MarkersField == null)
                return null;

            if (MarkersField.GetValue(hud) is not List<HUDUnitMarker> markers || markers.Count == 0)
                return FallbackRaycast(missile);

            Vector2 reticleScreen = MouseGuidanceController.GetReticleScreenPosition();
            Aircraft? own = null;
            try
            {
                if (GameManager.GetLocalAircraft(out Aircraft ac))
                    own = ac;
            }
            catch
            {
                // ignore
            }

            Unit? best = null;
            float bestDist = PickRadiusPx;

            for (int i = 0; i < markers.Count; i++)
            {
                HUDUnitMarker m = markers[i];
                if (m == null || m.unit == null || m.unit.disabled)
                    continue;
                if (m.unit is Missile)
                    continue;
                if (m.image == null || !m.image.enabled)
                    continue;

                try
                {
                    if (SceneSingleton<TargetListSelector>.i != null
                        && SceneSingleton<TargetListSelector>.i.CheckExclusions(m.unit))
                        continue;
                }
                catch
                {
                    // ignore exclusions failure
                }

                if (own != null && m.unit.NetworkHQ != null && own.NetworkHQ != null
                    && m.unit.NetworkHQ == own.NetworkHQ)
                    continue;

                Vector2 mp = m.image.rectTransform != null
                    ? (Vector2)m.image.rectTransform.position
                    : (Vector2)m.image.transform.position;
                float d = Vector2.Distance(reticleScreen, mp);
                if (d > PickRadiusPx)
                    continue;

                if (d < bestDist)
                {
                    bestDist = d;
                    best = m.unit;
                }
            }

            if (best != null)
                return best;

            return FallbackRaycast(missile);
        }

        private static Unit? FallbackRaycast(Missile missile)
        {
            try
            {
                Camera? feed = MissileCameraFsAccess.TryGetFeedCamera();
                if (feed == null)
                    return null;

                Vector2 vp = MouseGuidanceController.GetReticleViewport();
                Ray ray = feed.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
                if (!Physics.Raycast(ray, out RaycastHit hit, 200000f, ~0, QueryTriggerInteraction.Ignore))
                    return null;

                Unit? u = hit.collider.GetComponentInParent<Unit>();
                if (u == null || u.disabled || u is Missile)
                    return null;
                if (ReferenceEquals(u, missile))
                    return null;
                return u;
            }
            catch
            {
                return null;
            }
        }
    }
}
