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

        // Frame-cached IsActive / CanControl (invalidated in BeginFrame via RcFrameCache).
        private static int _gateFrame = -1;
        private static bool _cachedIsActive;
        private static bool _cachedCanControl;
        private static Missile? _cachedCanControlMissile;

        internal static Missile? Controlled => _controlled;

        internal static IReadOnlyList<Missile> Pool => _pool;

        internal static bool IsActive
        {
            get
            {
                EnsureGateFrame();
                return _cachedIsActive;
            }
        }

        internal static bool IsControlling(Missile? missile)
        {
            return missile != null && ReferenceEquals(missile, _controlled) && IsActive;
        }

        /// <summary>Session ownership — Seek skip / aim reinforce even if FS flickers for a frame.</summary>
        internal static bool OwnsMissile(Missile? missile)
        {
            return missile != null && ReferenceEquals(missile, _controlled) && !_controlled.disabled;
        }

        private static void EnsureGateFrame()
        {
            int f = Time.frameCount;
            if (f == _gateFrame)
                return;
            _gateFrame = f;
            _cachedCanControlMissile = null;

            if (_controlled == null || _controlled.disabled)
            {
                _cachedIsActive = false;
                _cachedCanControl = false;
                return;
            }

            _cachedCanControl = AuthorityGate.CanControl(_controlled);
            _cachedCanControlMissile = _controlled;
            _cachedIsActive = _cachedCanControl && MissileCameraFsAccess.IsFullscreenActive;
        }

        internal static bool CachedCanControl(Missile missile)
        {
            EnsureGateFrame();
            if (ReferenceEquals(missile, _cachedCanControlMissile))
                return _cachedCanControl;
            return AuthorityGate.CanControl(missile);
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
            _gateFrame = -1;
            FsAimReticle.SetVisible(false);
            MouseGuidanceController.Reset();
            ThrottleController.Reset();
            RcLinkQuality.Reset();
            RcWarheadSafety.Reset();
            RcSeekerSuppress.Reset();
            RcBallisticImpactSafety.Reset();
            RcUprightAssist.ResetSaved();
            MissileAccess.ClearProxyLatch();
            AfterburnerVfxBinder.ClearCache();
            RcFormationFollow.Clear();
            RcSeekSkipSet.Clear();
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
            Missile? best = PickForTake(_pool);
            if (best == null)
                return;

            Take(best);
        }

        /// <summary>Prefer the missile on the FS feed; fallback nearest to ownship.</summary>
        private static Missile? PickForTake(List<Missile> pool)
        {
            if (pool.Count == 0)
                return null;

            try
            {
                Missile? followed = MissileCameraFsAccess.TryGetFollowedMissile();
                if (followed != null && !followed.disabled)
                {
                    for (int i = 0; i < pool.Count; i++)
                    {
                        if (ReferenceEquals(pool[i], followed))
                            return followed;
                    }

                    if (MissileAccess.IsRcControllable(followed)
                        && AuthorityGate.CanControl(followed)
                        && AuthorityGate.IsAllied(followed))
                        return followed;

                    // Do not silently Take a different missile while FS shows another — that felt like "T does nothing".
                    RcPlugin.ModLogger?.LogInfo(
                        $"RC: FS missile '{followed.unitName ?? followed.name}' is not an RC clone — equip a DL/SATCOM mount.");
                    return null;
                }
            }
            catch
            {
                // fall through
            }

            if (pool.Count == 0)
            {
                RcPlugin.ModLogger?.LogInfo("RC: no allied clone missiles available.");
                return null;
            }

            return PickNearest(pool);
        }

        internal static void Take(Missile missile)
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;
            if (missile == null || !AuthorityGate.CanControl(missile) || !AuthorityGate.IsAllied(missile))
                return;
            if (!MissileAccess.IsRcControllable(missile))
                return;

            RcMissilePickerUi.Close();
            Release(silent: true, restoreTargets: false);
            _controlled = missile;
            _gateFrame = -1;
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
            RcSeekSkipSet.Rebuild();
            RcLivingRcRegistry.Notify(missile);
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

            if (KeybindPoll.IsDown(RcConfig.FormationFollow.Value) && !RcMissilePickerUi.IsOpen)
                RcFormationFollow.ToggleFromControlled();

            if (!IsActive || _controlled == null)
                return;

            // Lost: keep stick (thr like Degraded). Auto-Release handed aim to Seek near jam/targets.
            RcLinkQuality.Evaluate(_controlled);

            // Seeker suppress runs on Missile.Steering prefix (Fixed) — not every Update.
            RcWarheadSafety.Tick(_controlled);
            RcRetargetController.Tick(_controlled);
            MouseGuidanceController.Tick(_controlled);
            ThrottleController.Tick(_controlled);
        }

        internal static void FixedTick()
        {
            Missile? m = _controlled;
            if (m == null || m.disabled)
                return;

            if (!IsActive)
                return;
            ThrottleController.Reinforce(m);
            RcFormationFollow.Tick();
        }

        internal static void RefreshPool()
        {
            _pool.Clear();
            try
            {
                if (RcLivingRcRegistry.TryCopyAlive(_pool))
                {
                    FilterPoolInPlace();
                    if (_pool.Count > 0)
                        return;
                    _pool.Clear();
                }

                // Fallback when registry empty (join mid-mission / missed stamp).
                Missile[] all = Object.FindObjectsOfType<Missile>();
                for (int i = 0; i < all.Length; i++)
                {
                    Missile m = all[i];
                    if (m == null || m.disabled)
                        continue;
                    if (!MissileAccess.IsRcControllable(m))
                        continue;
                    if (!AuthorityGate.CanControl(m) || !AuthorityGate.IsAllied(m))
                        continue;
                    _pool.Add(m);
                    RcLivingRcRegistry.Notify(m);
                }
            }
            catch
            {
                _pool.Clear();
            }
        }

        private static void FilterPoolInPlace()
        {
            for (int i = _pool.Count - 1; i >= 0; i--)
            {
                Missile m = _pool[i];
                if (m == null || m.disabled
                    || !MissileAccess.IsRcControllable(m)
                    || !AuthorityGate.CanControl(m)
                    || !AuthorityGate.IsAllied(m))
                {
                    _pool.RemoveAt(i);
                }
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
