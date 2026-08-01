using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Network;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>
    /// When player selects / follows an allied DL/SATCOM missile, K enters MC FS and Takes RC.
    /// Sources: CameraStateManager.followingUnit, CombatHUD selected markers, MC followed.
    /// CAMERA_SAFETY: CSM read-only; FS via MC Enter reflection only.
    /// </summary>
    internal static class RcSpectatorEngage
    {
        private static readonly FieldInfo? MarkersField =
            AccessTools.Field(typeof(CombatHUD), "markers");

        private static bool _engageThisFrame;

        /// <summary>
        /// Poll K ourselves — MC Toggle patch may miss if MC loaded after Harmony bootstrap.
        /// </summary>
        internal static void Tick()
        {
            _engageThisFrame = false;

            if (!Input.GetKeyDown(KeyCode.K))
                return;

            if (MissileCameraFsAccess.IsFullscreenActive)
                return; // MC owns exit on its own K poll

            Missile? candidate = TryResolveSpectatedRcMissile();
            if (candidate == null)
                return;

            if (!TryEngage(candidate))
                RcPlugin.ModLogger?.LogWarning("RC spectator K: engage failed.");
            else
                _engageThisFrame = true;
        }

        /// <summary>Harmony Toggle Prefix: if we already engaged this frame, skip MC Toggle body.</summary>
        internal static bool ShouldSkipMcToggle()
        {
            return _engageThisFrame;
        }

        /// <summary>Harmony Toggle Prefix fallback when our Tick missed the key.</summary>
        internal static bool TryPrepareAndEnterViaTogglePath()
        {
            if (MissileCameraFsAccess.IsFullscreenActive)
                return false;

            Missile? candidate = TryResolveSpectatedRcMissile();
            if (candidate == null)
                return false;

            return TryEngage(candidate);
        }

        internal static void MaintainFollow(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;
            if (MissileCameraFsAccess.IsInOwnedActive(missile))
                return;
            MissileCameraFsAccess.TryForceFollowMissile(missile);
        }

        private static bool TryEngage(Missile candidate)
        {
            if (!MissileCameraFsAccess.IsReady)
            {
                RcPlugin.ModLogger?.LogWarning("RC spectator: MissileCamera reflection not ready.");
                return false;
            }

            if (!MissileCameraFsAccess.TryForceFollowMissile(candidate))
            {
                RcPlugin.ModLogger?.LogWarning("RC spectator: ForceFollow failed.");
                return false;
            }

            if (!MissileCameraFsAccess.TryEnterFullscreen())
            {
                // Fallback Toggle after inject.
                MissileCameraFsAccess.TryToggleFullscreen();
            }

            if (!MissileCameraFsAccess.IsFullscreenActive)
            {
                RcPlugin.ModLogger?.LogWarning("RC spectator: FS Enter failed after inject.");
                return false;
            }

            MissileCameraFsAccess.TryForceFollowMissile(candidate);
            RemoteControlSession.Take(candidate);
            RcPlugin.ModLogger?.LogInfo(
                $"RC spectator engaged: {candidate.unitName ?? candidate.name}");
            return RemoteControlSession.OwnsMissile(candidate);
        }

        internal static Missile? TryResolveSpectatedRcMissile()
        {
            Missile? m;

            m = TryFromFollowingUnit();
            if (IsEligible(m))
                return m;

            m = TryFromHudSelected();
            if (IsEligible(m))
                return m;

            m = MissileCameraFsAccess.TryGetFollowedMissile();
            if (IsEligible(m))
                return m;

            return null;
        }

        private static Missile? TryFromFollowingUnit()
        {
            try
            {
                CameraStateManager? cam = SceneSingleton<CameraStateManager>.i;
                return cam != null ? cam.followingUnit as Missile : null;
            }
            catch
            {
                return null;
            }
        }

        private static Missile? TryFromHudSelected()
        {
            try
            {
                CombatHUD? hud = SceneSingleton<CombatHUD>.i;
                if (hud == null || MarkersField == null)
                    return null;
                if (MarkersField.GetValue(hud) is not List<HUDUnitMarker> markers)
                    return null;

                for (int i = 0; i < markers.Count; i++)
                {
                    HUDUnitMarker mk = markers[i];
                    if (mk == null || !mk.selected || mk.unit == null)
                        continue;
                    if (mk.unit is Missile missile && IsEligible(missile))
                        return missile;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static bool IsEligible(Missile? missile)
        {
            if (missile == null || missile.disabled)
                return false;
            if (!MissileAccess.IsRcMissile(missile))
                return false;
            if (!AuthorityGate.CanControl(missile) || !AuthorityGate.IsAllied(missile))
                return false;
            return true;
        }
    }
}
