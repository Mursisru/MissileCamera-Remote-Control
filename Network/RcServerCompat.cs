using System;
using Mirage;
using MissileCameraRemoteControl.Config;
using MissileCameraRemoteControl.Control;
using NuclearOption.Networking;
using UnityEngine;

namespace MissileCameraRemoteControl.Network
{
    /// <summary>
    /// MP presence gate (fail-closed on pure clients):
    /// RC stays on only after the server replies that it also runs this addon.
    /// Vanilla / no reply → Denied for the session. Host / SP always on.
    /// </summary>
    internal static class RcServerCompat
    {
        internal const int PresenceMagic = unchecked((int)0x4D435243); // 'MCRC'

        private const float DefaultTimeoutSec = 4f;
        private const float QueryRetrySec = 1.0f;

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

        /// <summary>
        /// Runtime allow for RC features.
        /// Online clients: only after Allowed. Host/SP/menu: on (unless config off).
        /// </summary>
        internal static bool FeaturesAllowed
        {
            get
            {
                if (!RcConfig.Enabled.Value)
                    return false;

                if (!IsOnlineSession())
                    return true;

                if (IsLocalHostOrDedicated())
                    return true;

                // Pure client in MP — fail closed until presence confirmed.
                return _phase == Phase.Allowed;
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
            if (!IsOnlineSession())
            {
                if (_phase == Phase.Denied || _phase == Phase.Checking)
                    RcPlugin.ModLogger?.LogInfo("RC re-enabled (left multiplayer session).");
                _phase = Phase.Allowed;
                _loggedResult = false;
                return;
            }

            NetworkManagerMode mode = TryGetNetworkMode();
            if (mode != _lastMode)
            {
                _lastMode = mode;
                RcPlugin.ModLogger?.LogInfo($"RC presence: NetworkMode={mode}, phase={_phase}");
            }

            // Listen-host or dedicated process with this mod loaded.
            if (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.Server
                || IsLocalHostOrDedicated())
            {
                if (_phase != Phase.Allowed)
                    SetAllowed(fromCheck: false);
                return;
            }

            // Lobby tagged vanilla — no need to wait for handshake.
            try
            {
                if (NetworkManagerNuclearOption.ModdedServer == false)
                {
                    SetDenied("lobby marked vanilla (modded_server=0)");
                    return;
                }
            }
            catch
            {
                // ignore
            }

            // Still connecting — keep fail-closed (FeaturesAllowed already false).
            if (mode != NetworkManagerMode.Client || !IsClientActive())
            {
                if (_phase != Phase.Denied && _phase != Phase.Checking && _phase != Phase.Allowed)
                    _phase = Phase.Idle;
                return;
            }

            if (_phase == Phase.Allowed || _phase == Phase.Denied)
                return;

            if (_phase != Phase.Checking)
                BeginCheck();

            float now = Time.unscaledTime;
            if (now >= _nextQuery)
            {
                _nextQuery = now + QueryRetrySec;
                SendQuery();
            }

            if (now >= _deadline)
                SetDenied("no presence reply from server");
        }

        private static void BeginCheck()
        {
            _phase = Phase.Checking;
            _loggedResult = false;
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
                "RC: checking whether the server runs MissileCamera Remote Control…");
            SendQuery();
        }

        private static void SetAllowed(bool fromCheck)
        {
            Phase prev = _phase;
            _phase = Phase.Allowed;
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
            if (_phase == Phase.Denied)
                return;
            _phase = Phase.Denied;
            if (!_loggedResult)
            {
                _loggedResult = true;
                RcPlugin.ModLogger?.LogWarning(
                    $"RC: disabled for this session ({reason}). Server lacks MissileCamera Remote Control.");
            }

            try { RemoteControlSession.Clear(); }
            catch { /* ignore */ }
        }

        private static void OnPresenceReply(INetworkPlayer player, RcPresenceReplyMsg msg)
        {
            if (msg.Magic != PresenceMagic)
                return;
            SetAllowed(fromCheck: true);
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

        private static bool IsOnlineSession()
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

        private static bool IsLocalHostOrDedicated()
        {
            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                if (nm == null)
                    return false;

                NetworkManagerMode mode = nm.NetworkMode;
                if (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.Server)
                    return true;

                // Fallback if NetworkMode lags behind Server.Active on listen-host.
                if (nm.Server != null && nm.Server.Active
                    && (nm.Client == null || !nm.Client.Active || nm.Client.IsHost))
                    return true;

                return false;
            }
            catch
            {
                return false;
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
