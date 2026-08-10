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
    internal static class RcManualDetonate
    {
        private const float NukeSurfaceMaxAltM = 40f;
        private static float _nextNukeHint;

        internal static void Tick()
        {
            if (!RcConfig.Enabled.Value)
                return;
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;
            if (!RemoteControlSession.IsActive)
                return;

            Missile? m = RemoteControlSession.Controlled;
            if (m == null || m.disabled)
                return;
            if (!RemoteControlSession.OwnsMissile(m))
                return;
            if (!AuthorityGate.CanControl(m))
                return;

            KeyCode key = RcConfig.ManualDetonate.Value.MainKey;
            if (key == KeyCode.None)
                key = KeyCode.Space;
            if (!RcSpaceKeyEatPatch.RawKeyDown(key))
                return;

            TryDetonate(m);
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
