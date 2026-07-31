using NuclearOption.Networking;

namespace MissileCameraRemoteControl.Network
{
    /// <summary>RC only on host/SP LocalSim — never on pure clients.</summary>
    internal static class AuthorityGate
    {
        internal static bool ServerActive
        {
            get
            {
                try
                {
                    NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
                    return nm != null && nm.Server != null && nm.Server.Active;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static bool CanControl(Missile? missile)
        {
            if (missile == null || missile.disabled)
                return false;
            if (!ServerActive)
                return false;
            try
            {
                return missile.LocalSim;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetLocalHq(out FactionHQ? hq)
        {
            hq = null;
            try
            {
                if (GameManager.GetLocalHQ(out FactionHQ local) && local != null)
                {
                    hq = local;
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        internal static bool IsAllied(Missile missile)
        {
            if (!TryGetLocalHq(out FactionHQ? hq) || hq == null)
                return false;
            try
            {
                return missile.NetworkHQ != null && missile.NetworkHQ == hq;
            }
            catch
            {
                return false;
            }
        }
    }
}
