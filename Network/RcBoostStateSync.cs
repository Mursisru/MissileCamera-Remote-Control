using System;
using Mirage;
using MissileCameraRemoteControl.Vfx;
using NuclearOption.Networking;
using UnityEngine;

namespace MissileCameraRemoteControl.Network
{
    /// <summary>
    /// Broadcasts afterburner state to clients via Mirage SendToAll.
    /// Host also applies VFX locally; clients apply via AfterburnerVfxBinder.
    /// </summary>
    internal static class RcBoostStateSync
    {
        private static bool _handlerRegistered;
        private static uint _lastNetId;
        private static bool _lastBoost;
        private static bool _haveLast;

        internal static void EnsureRegistered()
        {
            if (_handlerRegistered)
                return;

            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                if (nm == null || nm.Client == null)
                    return;

                MessageHandler? handler = nm.Client.MessageHandler;
                if (handler == null)
                    return;

                handler.RegisterHandler<RcBoostNetMsg>(OnBoostMessage, false);
                _handlerRegistered = true;
                RcPlugin.ModLogger?.LogInfo("RC boost net handler registered.");
            }
            catch (Exception ex)
            {
                RcPlugin.ModLogger?.LogWarning($"RC boost handler register failed: {ex.Message}");
            }
        }

        internal static void Reset()
        {
            _haveLast = false;
            _lastNetId = 0;
            _lastBoost = false;
        }

        /// <summary>Host/SP: apply VFX and notify clients when boost edge changes.</summary>
        internal static void Publish(Missile? missile, bool boost)
        {
            if (missile == null)
                return;

            AfterburnerVfxBinder.SetBoost(missile, boost);

            uint netId = 0;
            try { netId = missile.NetId; }
            catch { return; }
            if (netId == 0)
                return;

            if (_haveLast && _lastNetId == netId && _lastBoost == boost)
                return;
            _haveLast = true;
            _lastNetId = netId;
            _lastBoost = boost;

            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                if (nm == null || nm.Server == null || !nm.Server.Active)
                    return;

                // Host-only: pure clients never Publish from ThrottleController.
                if (!missile.IsServer && !missile.IsHost)
                    return;

                EnsureRegistered();
                var msg = new RcBoostNetMsg { NetId = netId, Boost = boost };
                nm.Server.SendToAll(msg, authenticatedOnly: true, excludeLocalPlayer: true, channelId: Channel.Reliable);
            }
            catch
            {
                // ignore — SP without net is fine
            }
        }

        private static void OnBoostMessage(INetworkPlayer player, RcBoostNetMsg msg)
        {
            try
            {
                NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                if (nm == null)
                    return;

                // Host already applied locally.
                if (nm.Server != null && nm.Server.Active && (nm.Client == null || nm.Client.IsHost))
                    return;

                NetworkWorld? world = null;
                try { world = nm.Client != null ? nm.Client.World : null; }
                catch { return; }
                if (world == null)
                    return;

                if (!world.TryGetIdentity(msg.NetId, out NetworkIdentity identity) || identity == null)
                    return;

                Missile? missile = identity.GetComponent<Missile>();
                if (missile == null)
                    missile = identity.GetComponentInChildren<Missile>();
                if (missile == null)
                    return;

                AfterburnerVfxBinder.SetBoost(missile, msg.Boost);
            }
            catch
            {
                // ignore
            }
        }
    }
}
