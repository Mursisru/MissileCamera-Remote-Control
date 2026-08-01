using System.Collections.Generic;
using MissileCameraRemoteControl.Access;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Network;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>Active RC session — only while MissileCamera fullscreen is up.</summary>
    internal static class RemoteControlSession
    {
        private static Missile? _controlled;
        private static readonly List<Missile> _pool = new List<Missile>(32);

        internal static Missile? Controlled => _controlled;

        internal static IReadOnlyList<Missile> Pool => _pool;

        internal static bool IsActive =>
            _controlled != null
            && !_controlled.disabled
            && AuthorityGate.CanControl(_controlled)
            && MissileCameraFsAccess.IsFullscreenActive;

        internal static bool IsControlling(Missile? missile)
        {
            return missile != null && ReferenceEquals(missile, _controlled) && IsActive;
        }

        /// <summary>Session ownership — Seek skip / aim reinforce even if FS flickers for a frame.</summary>
        internal static bool OwnsMissile(Missile? missile)
        {
            return missile != null && ReferenceEquals(missile, _controlled) && !_controlled.disabled;
        }

        internal static void Clear()
        {
            Release(silent: true, restoreTargets: true);
            _pool.Clear();
            FsAimReticle.DestroyUi();
            RcMissilePickerUi.DestroyUi();
        }

        internal static void Release(bool silent = false, bool restoreTargets = true)
        {
            if (_controlled != null)
            {
                RcSeekerHandoff.CommitForAutonomous(_controlled);
                AfterburnerVfxBinder.SetBoost(_controlled, false);
                RcBoostStateSync.Publish(_controlled, false);
                RcUprightAssist.OnRelease(_controlled);
                if (!silent)
                    RcPlugin.ModLogger?.LogInfo($"RC released: {_controlled.name}");
            }
            _controlled = null;
            FsAimReticle.SetVisible(false);
            MouseGuidanceController.Reset();
            ThrottleController.Reset();
            RcLinkQuality.Reset();
            RcWarheadSafety.Reset();
            RcUprightAssist.ResetSaved();
            if (restoreTargets)
                RcAircraftTargetSnapshot.Restore();
            else
                RcAircraftTargetSnapshot.Clear();
        }

        internal static void ToggleNearest()
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
            {
                RcPlugin.ModLogger?.LogInfo("RC: enter MissileCamera Fullscreen (default K) before remote control.");
                return;
            }

            if (IsActive)
            {
                Release();
                return;
            }

            RefreshPool();
            Missile? best = PickNearest(_pool);
            if (best == null)
            {
                RcPlugin.ModLogger?.LogInfo("RC: no allied clone missiles available.");
                return;
            }

            Take(best);
        }

        internal static void Take(Missile missile)
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;
            if (missile == null || !AuthorityGate.CanControl(missile) || !AuthorityGate.IsAllied(missile))
                return;
            if (!MissileAccess.IsRcMissile(missile))
                return;

            RcMissilePickerUi.Close();
            Release(silent: true, restoreTargets: false);
            _controlled = missile;
            MouseGuidanceController.Reset();
            RcLinkQuality.Reset();
            FsAimReticle.SetVisible(true);
            RcAircraftTargetSnapshot.Capture();
            ThrottleController.OnTakeControl(missile);
            RcUprightAssist.OnTakeControl(missile);
            RcWarheadSafety.Reset();
            RcSeekerSuppress.Tick(missile);
            RcWarheadSafety.Tick(missile);
            RcLinkQuality.Evaluate(missile);
            RcPlugin.ModLogger?.LogInfo($"RC engaged (FS): {missile.name}");
        }

        internal static void Tick()
        {
            if (!RcConfig.Enabled.Value)
            {
                if (_controlled != null)
                    Release(silent: true);
                return;
            }

            if (_controlled != null && !MissileCameraFsAccess.IsFullscreenActive)
            {
                RcMissilePickerUi.Close();
                Release();
                return;
            }

            if (_controlled != null && (!AuthorityGate.CanControl(_controlled) || _controlled.disabled))
            {
                Release();
                return;
            }

            if (KeybindPoll.IsDown(RcConfig.OpenMissileList.Value))
                RcMissilePickerUi.Toggle();

            RcMissilePickerUi.Tick();

            if (KeybindPoll.IsDown(RcConfig.ToggleControl.Value) && !RcMissilePickerUi.IsOpen)
                ToggleNearest();

            if (!IsActive || _controlled == null)
                return;

            // Lost: keep stick (thr like Degraded). Auto-Release handed aim to Seek near jam/targets.
            RcLinkQuality.Evaluate(_controlled);

            RcSeekerSuppress.Tick(_controlled);
            RcWarheadSafety.Tick(_controlled);
            RcRetargetController.Tick(_controlled);
            MouseGuidanceController.Tick(_controlled);
            ThrottleController.Tick(_controlled);
        }

        internal static void FixedTick()
        {
            if (!IsActive || _controlled == null)
                return;
            ThrottleController.Reinforce(_controlled);
        }

        internal static void RefreshPool()
        {
            _pool.Clear();
            try
            {
                Missile[] all = Object.FindObjectsOfType<Missile>();
                for (int i = 0; i < all.Length; i++)
                {
                    Missile m = all[i];
                    if (m == null || m.disabled)
                        continue;
                    if (!MissileAccess.IsRcMissile(m))
                        continue;
                    if (!AuthorityGate.CanControl(m) || !AuthorityGate.IsAllied(m))
                        continue;
                    _pool.Add(m);
                }
            }
            catch
            {
                _pool.Clear();
            }
        }

        private static Missile? PickNearest(List<Missile> pool)
        {
            if (pool.Count == 0)
                return null;

            Vector3 origin = Vector3.zero;
            bool haveOrigin = false;
            try
            {
                if (GameManager.GetLocalAircraft(out Aircraft ac) && ac != null)
                {
                    origin = ac.transform.position;
                    haveOrigin = true;
                }
                else if (Camera.main != null)
                {
                    origin = Camera.main.transform.position;
                    haveOrigin = true;
                }
            }
            catch
            {
                // ignore
            }

            if (!haveOrigin)
                return pool[0];

            Missile? best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                float sq = (pool[i].transform.position - origin).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = pool[i];
                }
            }

            return best;
        }

        internal static Vector3 GetPoolOrigin()
        {
            try
            {
                if (GameManager.GetLocalAircraft(out Aircraft ac) && ac != null)
                    return ac.transform.position;
            }
            catch
            {
                // ignore
            }

            if (Camera.main != null)
                return Camera.main.transform.position;
            return Vector3.zero;
        }
    }
}
