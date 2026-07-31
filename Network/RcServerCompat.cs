using System;
using Mirage;
using MissileCameraRemoteControl.Cloning;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;
using NuclearOption.Networking;
using UnityEngine;

namespace MissileCameraRemoteControl.Network
{
    /// <summary>
    /// MP presence gate (fail-closed on pure clients).
    /// Detects online by NetworkMode.Client (not GameState — lobby stays Menu).
    /// Vanilla lobby or no presence reply → Denied until disconnect.
    /// </summary>
    internal static class RcServerCompat
    {
        internal const int PresenceMagic = unchecked((int)0x4D435243); // 'MCRC'

        private const float DefaultTimeoutSec = 3f;
        private const float QueryRetrySec = 0.75f;

        private enum Phase : byte
        {
            Idle = 0,
            Checking = 1,
            Allowed = 2,
            Denied = 3
        }

        private static Phase _phase = Phase.Idle;
        private static float _deadline;
        private static float _nextQuery;
        private static bool _clientHandlers;
        private static bool _serverHandlers;
        private static bool _loggedResult;
        private static int _nmInstanceId;
        private static NetworkManagerMode _lastMode = NetworkManagerMode.None;
        private static bool _strippedOnDeny;

        internal static bool FeaturesAllowed
        {
            get
            {
                if (!RcConfig.Enabled.Value)
                    return false;

                NetworkManagerMode mode = TryGetNetworkMode();

                // Offline menu / SP / encyclopedia — full features.
                if (mode == NetworkManagerMode.None && !IsGameStateOnline())
                    return true;

                // We are the server / listen-host.
                if (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.Server)
                    return true;

                // Pure client — only after presence Allowed.
                if (mode == NetworkManagerMode.Client)
                    return _phase == Phase.Allowed;

                // Fallback: Multiplayer GameState without resolved mode yet.
                if (IsGameStateOnline())
                    return _phase == Phase.Allowed;

                return true;
            }
        }

        internal static bool IsDenied => _phase == Phase.Denied;

        internal static void Reset()
        {
            _phase = Phase.Idle;
            _deadline = 0f;
            _nextQuery = 0f;
            _loggedResult = false;
            _lastMode = NetworkManagerMode.None;
            _strippedOnDeny = false;
        }

        internal static void Tick()
        {
            try
            {
                EnsureHandlers();
                Evaluate();
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC server compat: {ex.Message}");
            }
        }

        private static void Evaluate()
        {
            NetworkManagerMode mode = TryGetNetworkMode();
            if (mode != _lastMode)
            {
                _lastMode = mode;
                RcPlugin.ModLogger?.LogInfo(
                    $"RC presence: NetworkMode={mode}, GameState={SafeGameState()}, phase={_phase}, ModdedServer={SafeModdedFlag()}");
            }

            // Host / dedicated with this plugin — always on.
            if (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.Server)
            {
                if (_phase != Phase.Allowed)
                {
                    _phase = Phase.Allowed;
                    _strippedOnDeny = false;
                }
                return;
            }

            // Pure remote client (lobby Menu OR Multiplayer mission).
            if (mode == NetworkManagerMode.Client && IsClientActive())
            {
                EvaluateAsClient();
                return;
            }

            // Not connected as client — restore if we had been denied/checking.
            if (_phase == Phase.Denied || _phase == Phase.Checking)
            {
                RcPlugin.ModLogger?.LogInfo("RC re-enabled (left multiplayer / disconnected).");
                _phase = Phase.Allowed;
                _loggedResult = false;
                _strippedOnDeny = false;
                try { HardpointInjector.InjectAll(RcPlugin.ModLogger); }
                catch { /* ignore */ }
                return;
            }

            if (!IsGameStateOnline())
            {
                _phase = Phase.Allowed;
                _loggedResult = false;
            }
        }

        private static void EvaluateAsClient()
        {
            // Fast path: Steam lobby tagged vanilla.
            try
            {
                if (NetworkManagerNuclearOption.ModdedServer == false)
                {
                    SetDenied("lobby modded_server=0 (vanilla)");
                    return;
                }
            }
            catch
            {
                // ignore
            }

            if (_phase == Phase.Allowed)
                return;

            if (_phase == Phase.Denied)
            {
                EnsureStripped();
                return;
            }

            if (_phase != Phase.Checking)
                BeginCheck();

            float now = Time.unscaledTime;
            if (now >= _nextQuery)
            {
                _nextQuery = now + QueryRetrySec;
                SendQuery();
            }

            if (now >= _deadline)
                SetDenied("no presence reply (server likely missing this addon)");
        }

        private static void BeginCheck()
        {
            _phase = Phase.Checking;
            _loggedResult = false;
            _strippedOnDeny = false;
            float timeout = DefaultTimeoutSec;
            try
            {
                if (RcConfig.ServerPresenceTimeout.Value > 0.5f)
                    timeout = RcConfig.ServerPresenceTimeout.Value;
            }
            catch
            {
                // ignore
            }

            _deadline = Time.unscaledTime + timeout;
            _nextQuery = 0f;
            RcPlugin.ModLogger?.LogInfo(
                "RC: client session — checking server for MissileCamera Remote Control…");
            SendQuery();
        }

        private static void SetAllowed(bool fromCheck)
        {
            Phase prev = _phase;
            _phase = Phase.Allowed;
            _strippedOnDeny = false;
            if (_loggedResult)
                return;
            if (fromCheck || prev == Phase.Checking || prev == Phase.Denied)
            {
                _loggedResult = true;
                RcPlugin.ModLogger?.LogInfo("RC: server supports Remote Control — features enabled.");
            }
        }

        private static void SetDenied(string reason)
        {
            if (_phase != Phase.Denied)
            {
                _phase = Phase.Denied;
                if (!_loggedResult)
                {
                    _loggedResult = true;
                    RcPlugin.ModLogger?.LogWarning(
                        $"RC DISABLED this session: {reason}");
                }

                try { RemoteControlSession.Clear(); }
                catch { /* ignore */ }
            }

            EnsureStripped();
        }

        private static void EnsureStripped()
        {
            if (_strippedOnDeny)
                return;
            _strippedOnDeny = true;
            try
            {
                int n = HardpointInjector.StripAllRcOptions(RcPlugin.ModLogger);
                RcPlugin.ModLogger?.LogInfo($"RC: stripped {n} RC mount option(s) while disabled.");
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC strip failed: {ex.Message}");
            }
        }

        private static void OnPresenceReply(INetworkPlayer player, RcPresenceReplyMsg msg)
        {
            if (msg.Magic != PresenceMagic)
                return;
            SetAllowed(fromCheck: true);
            try { HardpointInjector.InjectAll(RcPlugin.ModLogger); }
            catch { /* ignore */ }
        }

        private static void OnPresenceQuery(INetworkPlayer player, RcPresenceQueryMsg msg)
        {
            if (msg.Magic != PresenceMagic || player == null)
                return;
            try
            {
                player.Send(new RcPresenceReplyMsg { Magic = PresenceMagic }, Channel.Reliable);
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC presence reply failed: {ex.Message}");
            }
        }

        private static void SendQuery()
        {
            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                if (nm?.Client == null || !nm.Client.Active)
                    return;
                nm.Client.Send(new RcPresenceQueryMsg { Magic = PresenceMagic }, Channel.Reliable);
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC presence query send failed: {ex.Message}");
            }
        }

        private static bool IsGameStateOnline()
        {
            try
            {
                GameState state = GameManager.gameState;
                return state == GameState.Multiplayer || state == GameState.ServerWaiting;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeGameState()
        {
            try { return GameManager.gameState.ToString(); }
            catch { return "?"; }
        }

        private static string SafeModdedFlag()
        {
            try
            {
                bool? m = NetworkManagerNuclearOption.ModdedServer;
                return m == null ? "null" : m.Value.ToString();
            }
            catch
            {
                return "?";
            }
        }

        private static NetworkManagerMode TryGetNetworkMode()
        {
            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                return nm != null ? nm.NetworkMode : NetworkManagerMode.None;
            }
            catch
            {
                return NetworkManagerMode.None;
            }
        }

        private static bool IsClientActive()
        {
            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                return nm?.Client != null && nm.Client.Active;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureHandlers()
        {
            NetworkManagerNuclearOption? nm = null;
            try { nm = NetworkManagerNuclearOption.i; }
            catch { return; }
            if (nm == null)
                return;

            int id = nm.GetInstanceID();
            if (id != _nmInstanceId)
            {
                _nmInstanceId = id;
                _clientHandlers = false;
                _serverHandlers = false;
            }

            try
            {
                if (!_clientHandlers && nm.Client?.MessageHandler != null)
                {
                    nm.Client.MessageHandler.RegisterHandler<RcPresenceReplyMsg>(OnPresenceReply, false);
                    _clientHandlers = true;
                }
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC presence client handler: {ex.Message}");
            }

            try
            {
                if (!_serverHandlers && nm.Server?.MessageHandler != null)
                {
                    nm.Server.MessageHandler.RegisterHandler<RcPresenceQueryMsg>(OnPresenceQuery, false);
                    _serverHandlers = true;
                    RcPlugin.ModLogger?.LogInfo("RC presence server handler registered.");
                }
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC presence server handler: {ex.Message}");
            }
        }
    }
}
