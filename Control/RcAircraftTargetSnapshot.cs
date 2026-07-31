using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// Snapshot aircraft HUD/WM target list on RC take; restore on release.
    /// </summary>
    internal static class RcAircraftTargetSnapshot
    {
        private static readonly FieldInfo? MarkersField =
            AccessTools.Field(typeof(CombatHUD), "markers");

        private static readonly List<Unit> _saved = new List<Unit>(16);
        private static bool _hasSnapshot;

        internal static void Capture()
        {
            _saved.Clear();
            _hasSnapshot = false;
            try
            {
                CombatHUD? hud = SceneSingleton<CombatHUD>.i;
                if (hud == null)
                    return;

                List<Unit>? list = hud.GetTargetList();
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Unit u = list[i];
                        if (u != null && !u.disabled)
                            _saved.Add(u);
                    }
                }

                _hasSnapshot = true;
            }
            catch
            {
                _saved.Clear();
                _hasSnapshot = false;
            }
        }

        internal static void Restore()
        {
            if (!_hasSnapshot)
                return;

            try
            {
                CombatHUD? hud = SceneSingleton<CombatHUD>.i;
                if (hud == null)
                {
                    Clear();
                    return;
                }

                Aircraft? ac = null;
                try
                {
                    if (GameManager.GetLocalAircraft(out Aircraft a))
                        ac = a;
                }
                catch
                {
                    // ignore
                }

                // Clear current selections visually + list.
                try
                {
                    hud.DeselectAll(false);
                }
                catch
                {
                    // ignore
                }

                // SelectUnit Insert(0) — restore reverse so saved[0] ends as primary.
                for (int i = _saved.Count - 1; i >= 0; i--)
                {
                    Unit u = _saved[i];
                    if (u == null || u.disabled)
                        continue;
                    try
                    {
                        hud.SelectUnit(u);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (ac != null && ac.weaponManager != null)
                {
                    try
                    {
                        ac.weaponManager.TargetListChanged();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                // Re-select markers that match saved units.
                TryReselectMarkers(hud);
            }
            catch
            {
                // ignore
            }

            Clear();
        }

        internal static void Clear()
        {
            _saved.Clear();
            _hasSnapshot = false;
        }

        private static void TryReselectMarkers(CombatHUD hud)
        {
            if (MarkersField == null)
                return;
            try
            {
                if (MarkersField.GetValue(hud) is not List<HUDUnitMarker> markers)
                    return;

                for (int i = 0; i < markers.Count; i++)
                {
                    HUDUnitMarker m = markers[i];
                    if (m == null || m.unit == null)
                        continue;
                    bool want = false;
                    for (int s = 0; s < _saved.Count; s++)
                    {
                        if (ReferenceEquals(_saved[s], m.unit))
                        {
                            want = true;
                            break;
                        }
                    }

                    try
                    {
                        if (want)
                            m.SelectMarker();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
