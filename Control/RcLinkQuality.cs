using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// DL mesh quality: ally within MeshRange + LoS = Full;
    /// in range without LoS = Degraded; out of range or jam &gt; JamBreakSeconds = Lost.
    /// SATCOM always Full (terrain / jam ignored).
    /// </summary>
    internal static class RcLinkQuality
    {
        private const float EvalInterval = 0.2f;
        private const float LosHitRadius = 40f;

        private static float _nextEval;
        private static RcLinkLevel _level = RcLinkLevel.Full;
        private static float _jamSeconds;
        private static int _lastMissileId;

        internal static RcLinkLevel Current => _level;

        internal static void Reset()
        {
            _level = RcLinkLevel.Full;
            _jamSeconds = 0f;
            _nextEval = 0f;
            _lastMissileId = 0;
        }

        internal static RcLinkLevel Evaluate(Missile missile)
        {
            if (missile == null || missile.disabled)
            {
                _level = RcLinkLevel.Lost;
                return _level;
            }

            int id = missile.GetInstanceID();
            if (id != _lastMissileId)
            {
                _lastMissileId = id;
                _jamSeconds = 0f;
                _nextEval = 0f;
            }

            if (Time.unscaledTime < _nextEval)
                return _level;
            _nextEval = Time.unscaledTime + EvalInterval;

            RcMissileTag? tag = missile.GetComponent<RcMissileTag>();
            bool satcom = tag != null && tag.Guidance == RcGuidanceKind.Satcom;
            if (satcom)
            {
                _jamSeconds = 0f;
                _level = RcLinkLevel.Full;
                return _level;
            }

            float meshRange = Mathf.Max(1000f, RcConfig.MeshRangeM.Value);
            float jamRange = Mathf.Max(500f, RcConfig.JamRangeM.Value);
            float ecmThreshold = Mathf.Max(0.01f, RcConfig.JamEcmThreshold.Value);
            float jamBreak = Mathf.Max(0.5f, RcConfig.JamBreakSeconds.Value);

            bool anyAllyInRange = false;
            bool anyLos = false;
            TryScanAllies(missile, meshRange, ref anyAllyInRange, ref anyLos);

            bool jammed = IsInEnemyJam(missile, jamRange, ecmThreshold);
            if (jammed)
                _jamSeconds += EvalInterval;
            else
                _jamSeconds = 0f;

            if (!anyAllyInRange || _jamSeconds >= jamBreak)
                _level = RcLinkLevel.Lost;
            else if (anyLos)
                _level = RcLinkLevel.Full;
            else
                _level = RcLinkLevel.Degraded;

            return _level;
        }

        private static void TryScanAllies(
            Missile missile,
            float meshRange,
            ref bool anyAllyInRange,
            ref bool anyLos)
        {
            FactionHQ? hq = null;
            try { hq = missile.NetworkHQ; }
            catch { return; }
            if (hq == null)
                return;

            GlobalPosition missilePos;
            try { missilePos = missile.GlobalPosition(); }
            catch { return; }

            Transform? missileTf = missile.transform;
            if (missileTf == null)
                return;

            try
            {
                var units = UnitRegistry.allUnits;
                if (units == null)
                    return;

                for (int i = 0; i < units.Count; i++)
                {
                    Unit? u = units[i];
                    if (u == null || u.disabled)
                        continue;
                    if (ReferenceEquals(u, missile))
                        continue;

                    FactionHQ? uHq = null;
                    try { uHq = u.NetworkHQ; }
                    catch { continue; }
                    if (uHq == null || uHq != hq)
                        continue;

                    GlobalPosition uPos;
                    try { uPos = u.GlobalPosition(); }
                    catch { continue; }

                    if (!FastMath.InRange(missilePos, uPos, meshRange))
                        continue;

                    anyAllyInRange = true;

                    Transform? uTf = u.transform;
                    if (uTf == null)
                        continue;

                    float radius = LosHitRadius;
                    try
                    {
                        if (u.maxRadius > 1f)
                            radius = Mathf.Max(LosHitRadius, u.maxRadius * 2f);
                    }
                    catch
                    {
                        // keep default
                    }

                    try
                    {
                        if (TargetCalc.LineOfSight(missileTf, uTf, radius))
                        {
                            anyLos = true;
                            return; // Full possible — early out
                        }
                    }
                    catch
                    {
                        // try next ally
                    }
                }
            }
            catch
            {
                // ignore scan errors
            }
        }

        private static bool IsInEnemyJam(Missile missile, float jamRange, float ecmThreshold)
        {
            FactionHQ? hq = null;
            try { hq = missile.NetworkHQ; }
            catch { return false; }
            if (hq == null)
                return false;

            GlobalPosition missilePos;
            try { missilePos = missile.GlobalPosition(); }
            catch { return false; }

            try
            {
                var aircraft = UnitRegistry.allAircraft;
                if (aircraft == null)
                    return false;

                for (int i = 0; i < aircraft.Count; i++)
                {
                    Aircraft? ac = aircraft[i];
                    if (ac == null || ac.disabled)
                        continue;

                    FactionHQ? acHq = null;
                    try { acHq = ac.NetworkHQ; }
                    catch { continue; }
                    if (acHq == null || acHq == hq)
                        continue;

                    float ecm = 0f;
                    try { ecm = ac.GetECMIntensity(); }
                    catch { continue; }
                    if (ecm < ecmThreshold)
                        continue;

                    GlobalPosition acPos;
                    try { acPos = ac.GlobalPosition(); }
                    catch { continue; }

                    if (FastMath.InRange(missilePos, acPos, jamRange))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
