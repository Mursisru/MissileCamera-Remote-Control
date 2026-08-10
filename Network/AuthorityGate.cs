using MissileCameraRemoteControl.Config;
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
            if (!RcServerCompat.FeaturesAllowed)
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
                FactionHQ? mHq = null;
                try { mHq = missile.NetworkHQ; }
                catch { /* ignore */ }
                if (mHq == null)
                {
                    try { mHq = missile.MapHQ; }
                    catch { /* ignore */ }
                }

                if (mHq != null && mHq == hq)
                    return true;

                // AllowAnyMunition: some mod missiles leave NetworkHQ unset — fall back to owner / local aircraft.
                if (!RcConfig.AllowAnyMunition.Value)
                    return false;

                Unit? owner = null;
                try { owner = missile.owner; }
                catch { /* ignore */ }

                if (owner != null)
                {
                    FactionHQ? oHq = null;
                    try { oHq = owner.NetworkHQ; }
                    catch { /* ignore */ }
                    if (oHq == null)
                    {
                        try { oHq = owner.MapHQ; }
                        catch { /* ignore */ }
                    }

                    if (oHq != null && oHq == hq)
                        return true;
                }

                if (GameManager.GetLocalAircraft(out Aircraft local)
                    && local != null
                    && !local.disabled
                    && owner != null
                    && ReferenceEquals(owner, local))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
