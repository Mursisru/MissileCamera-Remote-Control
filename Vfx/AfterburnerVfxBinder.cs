using System;
using System.Collections.Generic;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Vfx
{
    /// <summary>Reuse vanilla motor ParticleSystems — scale emission on boost edge only.</summary>
    internal static class AfterburnerVfxBinder
    {
        private static readonly Dictionary<int, float> _baseRates = new Dictionary<int, float>(8);
        private static int _lastMissileId;
        private static bool _lastBoost;
        private static bool _haveLast;

        internal static void SetBoost(Missile? missile, bool boost)
        {
            if (missile == null)
                return;

            int mid = missile.GetInstanceID();
            if (_haveLast && _lastMissileId == mid && _lastBoost == boost)
                return;
            _haveLast = true;
            _lastMissileId = mid;
            _lastBoost = boost;

            Array? motors = MissileAccess.GetMotors(missile);
            if (motors == null)
                return;

            for (int i = 0; i < motors.Length; i++)
            {
                object? motor = motors.GetValue(i);
                if (motor == null)
                    continue;

                ParticleSystem[]? systems = MissileAccess.GetMotorParticles(motor);
                if (systems == null)
                    continue;

                for (int p = 0; p < systems.Length; p++)
                {
                    ParticleSystem ps = systems[p];
                    if (ps == null)
                        continue;

                    int id = ps.GetInstanceID();
                    var emission = ps.emission;
                    if (!_baseRates.TryGetValue(id, out float baseRate))
                    {
                        baseRate = emission.rateOverTime.constant;
                        _baseRates[id] = baseRate;
                    }

                    emission.rateOverTime = boost ? baseRate * 2.5f : baseRate;

                    if (boost && !ps.isPlaying)
                        ps.Play(true);
                }
            }
        }

        internal static void ClearCache()
        {
            _baseRates.Clear();
            _haveLast = false;
            _lastMissileId = 0;
            _lastBoost = false;
        }
    }
}
