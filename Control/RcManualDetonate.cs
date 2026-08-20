using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.HarmonyPatches;
using MissileCameraRemoteControl.Network;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// FS + RC: ManualDetonate (default Space) → vanilla Detonate via allow gate.
    /// Nuclear warheads: refuse airburst — only near terrain/sea.
    /// </summary>
    internal static partial class RcManualDetonate
    {
        private const float NukeSurfaceMaxAltM = 40f;
        private static float _nextNukeHint;

        internal static void Tick()
        {
            Missile? m = CanDetonate();
            if (m == null)
                return;

            KeyCode key = RcConfig.ManualDetonate.Value.MainKey;
            if (key == KeyCode.None)
                key = KeyCode.Space;
            if (!RcSpaceKeyEatPatch.RawKeyDown(key))
                return;

            TryDetonate(m);
        }

        /// <summary>Guard chain shared by the physical key (Tick above) and the external channel
        /// (Bridge/RcManualDetonate.Bridge.cs TriggerExternal) so the two can't silently drift
        /// apart. Returns the missile that's clear to detonate, or null if any guard rejects.</summary>
        private static Missile? CanDetonate()
        {
            if (!RcConfig.Enabled.Value) return null;
            if (!MissileCameraFsAccess.IsControlAllowed) return null;
            if (!RemoteControlSession.IsActive) return null;

            Missile? m = RemoteControlSession.Controlled;
            if (m == null || m.disabled) return null;
            if (!RemoteControlSession.OwnsMissile(m)) return null;
            if (!AuthorityGate.CanControl(m)) return null;

            return m;
        }

        private static void TryDetonate(Missile missile)
        {
            try
            {
                if (MissileAccess.IsNuclearWarhead(missile)
                    && !MissileAccess.IsNearSurface(missile, NukeSurfaceMaxAltM))
                {
                    if (Time.unscaledTime >= _nextNukeHint)
                    {
                        _nextNukeHint = Time.unscaledTime + 2f;
                        RcPlugin.ModLogger?.LogInfo(
                            "RC: nuclear munition — manual detonate blocked in air (near surface only).");
                    }
                    return;
                }

                if (!missile.IsArmed())
                {
                    try { missile.Arm(); }
                    catch { /* ignore */ }
                }

                bool near = MissileAccess.IsNearSurface(missile, NukeSurfaceMaxAltM);
                Vector3 n = missile.rb != null && missile.rb.velocity.sqrMagnitude > 1f
                    ? -missile.rb.velocity.normalized
                    : Vector3.up;
                RcDetonateUtil.Force(missile, n, hitTerrain: near);
                RcPlugin.ModLogger?.LogInfo(
                    $"RC manual detonate: {missile.unitName ?? missile.name}");
            }
            catch (System.Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC manual detonate failed: {ex.Message}");
            }
        }
    }
}
