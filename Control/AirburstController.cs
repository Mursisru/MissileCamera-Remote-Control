using MissileCameraRemoteControl.Config;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>Manual airburst — Space detonates armed warhead at current position.</summary>
    internal static class AirburstController
    {
        internal static void Tick(Missile missile)
        {
            if (missile == null || !KeybindPoll.IsDown(RcConfig.Airburst.Value))
                return;

            try
            {
                if (!missile.IsArmed())
                    missile.Arm();

                if (!missile.IsArmed())
                    return;

                missile.Detonate(missile.transform.forward, hitArmor: false, hitTerrain: false);
                RemoteControlSession.Release(silent: true);
                RcPlugin.ModLogger?.LogInfo("RC airburst.");
            }
            catch
            {
                // ignore
            }
        }
    }
}
