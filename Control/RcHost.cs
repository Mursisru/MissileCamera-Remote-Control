using System;
using BepInEx.Logging;
using HarmonyLib;
using MissileCameraRemoteControl.Cloning;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Vfx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>DDOL host: Harmony + poll Encyclopedia for clone bootstrap + RC tick.</summary>
    internal sealed class RcHost : MonoBehaviour
    {
        private static RcHost? _instance;
        private ManualLogSource? _log;
        private Harmony? _harmony;
        private float _nextBootstrapAttempt;
        private int _bootstrapAttempts;
        private bool _loggedBootstrapOk;

        internal static void Ensure(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            var go = new GameObject("MissileCameraRemoteControl.Host");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<RcHost>();
            _instance._log = logger;
            _instance.Bootstrap();
        }

        private void Bootstrap()
        {
            _harmony = new Harmony(RcPlugin.PluginGuid);

            // PatchAll must not abort Motor/Seek — one bad attribute used to kill the whole chain.
            try
            {
                _harmony.PatchAll(typeof(RcPlugin).Assembly);
            }
            catch (Exception ex)
            {
                _log?.LogError($"Harmony PatchAll failed (continuing critical patches): {ex.Message}");
            }

            try
            {
                PatchAllSeekerOverrides(_harmony);
                PatchMotorThrust(_harmony);
                HarmonyPatches.RcMissileCameraThrSnap.TryPatch(_harmony, _log);
                HarmonyPatches.RcSteeringUprightPatch.TryPatch(_harmony, _log);
                HarmonyPatches.RcGLimitEnforce.TryPatch(_harmony, _log);
                HarmonyPatches.RcFeedPoseReticlePatch.TryPatch(_harmony, _log);
                _log?.LogInfo("Harmony patched.");
            }
            catch (Exception ex)
            {
                _log?.LogError($"Harmony critical patches failed: {ex}");
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _nextBootstrapAttempt = 0f;
            Access.MissileCameraFsAccess.TryResolveNow();
            TryBootstrapClones("plugin_awake");
            Network.RcBoostStateSync.EnsureRegistered();
        }

        private void PatchMotorThrust(Harmony harmony)
        {
            System.Reflection.MethodInfo? thrust = Access.MissileAccess.MotorThrustMethod;
            if (thrust == null)
            {
                _log?.LogWarning("Motor.Thrust not found — afterburner disabled.");
                return;
            }

            harmony.Patch(
                thrust,
                prefix: new HarmonyMethod(typeof(HarmonyPatches.RcMotorThrustPatch), nameof(HarmonyPatches.RcMotorThrustPatch.Prefix)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches.RcMotorThrustPatch), nameof(HarmonyPatches.RcMotorThrustPatch.Postfix)));
            _log?.LogInfo("Motor.Thrust patched (RC afterburner).");
        }

        private static void PatchAllSeekerOverrides(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(HarmonyPatches.RcSeekPatch), nameof(HarmonyPatches.RcSeekPatch.Prefix));
            Type seekerBase = typeof(MissileSeeker);
            foreach (Type type in seekerBase.Assembly.GetTypes())
            {
                if (type == null || type == seekerBase || !seekerBase.IsAssignableFrom(type))
                    continue;
                System.Reflection.MethodInfo? seek = type.GetMethod(
                    "Seek",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                if (seek == null)
                    continue;
                try
                {
                    harmony.Patch(seek, prefix: prefix);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch
            {
                // ignore
            }
            RemoteControlSession.Clear();
            AfterburnerVfxBinder.ClearCache();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Access.MissileCameraFsAccess.TryResolveNow();
            Network.RcBoostStateSync.EnsureRegistered();
            TryBootstrapClones("scene_loaded:" + scene.name);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            string name = scene.name ?? string.Empty;
            if (name.IndexOf("GameWorld", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                HardReset("scene_unload:" + name);
            }
        }

        private void HardReset(string reason)
        {
            RemoteControlSession.Clear();
            LaunchRcCapture.Clear();
            AfterburnerVfxBinder.ClearCache();
            FsAimReticle.DestroyUi();
            RcMissilePickerUi.DestroyUi();
            RcFsOwnshipGuard.Reset();
            Network.RcBoostStateSync.Reset();
            Network.RcServerCompat.Reset();
            _log?.LogInfo($"RC hard reset ({reason}).");
        }

        private void TryBootstrapClones(string reason)
        {
            if (WeaponCloneBootstrap.IsDone)
            {
                // Re-inject into newly spawned hangar aircraft.
                try
                {
                    HardpointInjector.InjectAll(_log);
                }
                catch
                {
                    // ignore
                }
                return;
            }

            _bootstrapAttempts++;
            bool ok = false;
            try
            {
                ok = WeaponCloneBootstrap.TryRun(_log);
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"Clone bootstrap ({reason}) error: {ex.Message}");
            }

            if (ok && !_loggedBootstrapOk)
            {
                _loggedBootstrapOk = true;
                _log?.LogInfo($"Clone bootstrap OK via {reason} (attempts={_bootstrapAttempts}, clones={WeaponCloneBootstrap.CloneCount}).");
            }
        }

        private void Update()
        {
            // Poll until Encyclopedia is ready — AfterLoad often finishes before plugin load.
            if (!WeaponCloneBootstrap.IsDone && Time.unscaledTime >= _nextBootstrapAttempt)
            {
                _nextBootstrapAttempt = Time.unscaledTime + 1f;
                TryBootstrapClones("poll");
            }

            if (!RcConfig.Enabled.Value)
                return;

            try
            {
                Access.RcFrameCache.BeginFrame();
                Network.RcServerCompat.Tick();

                GameState state = GameManager.gameState;
                if (state != GameState.SinglePlayer && state != GameState.Multiplayer)
                {
                    if (RemoteControlSession.Controlled != null)
                        RemoteControlSession.Clear();
                    return;
                }

                if (!Network.RcServerCompat.FeaturesAllowed)
                {
                    if (RemoteControlSession.Controlled != null)
                        RemoteControlSession.Clear();
                    return;
                }

                RemoteControlSession.Tick();
                RcFsOwnshipGuard.Tick();
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"RC tick error: {ex.Message}");
            }
        }

        private void LateUpdate()
        {
            // Intentionally empty: aim/reticle only after MC SyncPose (EOF).
            // LateUpdate here ran BEFORE SyncPose and baked a boresight offset into aimDir.
        }

        private void FixedUpdate()
        {
            if (!RcConfig.Enabled.Value)
                return;
            if (!Network.RcServerCompat.FeaturesAllowed)
                return;
            try
            {
                RemoteControlSession.FixedTick();
            }
            catch
            {
                // ignore
            }
        }
    }
}
