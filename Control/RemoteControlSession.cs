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

        internal static bool IsActive =>
            _controlled != null
            && !_controlled.disabled
            && AuthorityGate.CanControl(_controlled)
            && MissileCameraFsAccess.IsFullscreenActive;

        internal static bool IsControlling(Missile? missile)
        {
            return missile != null && ReferenceEquals(missile, _controlled) && IsActive;
        }

        internal static void Clear()
        {
            Release(silent: true);
            _pool.Clear();
            FsAimReticle.DestroyUi();
        }

        internal static void Release(bool silent = false)
        {
            if (_controlled != null)
            {
                AfterburnerVfxBinder.SetBoost(_controlled, false);
                if (!silent)
                    RcPlugin.ModLogger?.LogInfo($"RC released: {_controlled.name}");
            }
            _controlled = null;
            FsAimReticle.SetVisible(false);
            MouseGuidanceController.Reset();
            ThrottleController.Reset();
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

        internal static void Cycle(int direction)
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;

            RefreshPool();
            if (_pool.Count == 0)
                return;

            int idx = 0;
            if (_controlled != null)
            {
                idx = _pool.IndexOf(_controlled);
                if (idx < 0)
                    idx = 0;
            }

            idx = (idx + direction) % _pool.Count;
            if (idx < 0)
                idx += _pool.Count;
            Take(_pool[idx]);
        }

        internal static void Take(Missile missile)
        {
            if (!MissileCameraFsAccess.IsFullscreenActive)
                return;
            if (missile == null || !AuthorityGate.CanControl(missile) || !AuthorityGate.IsAllied(missile))
                return;
            if (!MissileAccess.IsRcMissile(missile))
                return;

            Release(silent: true);
            _controlled = missile;
            MouseGuidanceController.Reset();
            FsAimReticle.SetVisible(true);
            ThrottleController.OnTakeControl(missile);
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

            // Drop RC when leaving MissileCamera fullscreen.
            if (_controlled != null && !MissileCameraFsAccess.IsFullscreenActive)
            {
                Release();
                return;
            }

            if (_controlled != null && (!AuthorityGate.CanControl(_controlled) || _controlled.disabled))
            {
                Release();
                return;
            }

            if (KeybindPoll.IsDown(RcConfig.ToggleControl.Value))
                ToggleNearest();
            else if (KeybindPoll.IsDown(RcConfig.CycleNext.Value))
                Cycle(1);
            else if (KeybindPoll.IsDown(RcConfig.CyclePrev.Value))
                Cycle(-1);

            if (!IsActive || _controlled == null)
                return;

            MouseGuidanceController.Tick(_controlled);
            ThrottleController.Tick(_controlled);
            RetargetController.Tick(_controlled);
            AirburstController.Tick(_controlled);
        }

        private static void RefreshPool()
        {
            _pool.Clear();
            try
            {
                Missile[] all = UnityEngine.Object.FindObjectsOfType<Missile>();
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
    }
}
