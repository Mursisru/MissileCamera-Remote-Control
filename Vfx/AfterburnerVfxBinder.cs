using System;
using MissileCameraRemoteControl.Access;
using UnityEngine;

namespace MissileCameraRemoteControl.Vfx
{
    /// <summary>Reuse vanilla motor ParticleSystems — scale emission while boost held.</summary>
    internal static class AfterburnerVfxBinder
    {
        private static readonly System.Collections.Generic.Dictionary<int, float> _baseRates =
            new System.Collections.Generic.Dictionary<int, float>(8);

        internal static void SetBoost(Missile? missile, bool boost)
        {
            if (missile == null)
                return;

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
                    if (!_baseRates.ContainsKey(id))
                        _baseRates[id] = emission.rateOverTime.constant;

                    float baseRate = _baseRates[id];
                    emission.rateOverTime = boost ? baseRate * 2.5f : baseRate;

                    if (boost && !ps.isPlaying)
                        ps.Play(true);
                }
            }
        }

        internal static void ClearCache()
        {
            _baseRates.Clear();
        }
    }
}
