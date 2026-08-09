using MissileCameraRemoteControl.HarmonyPatches;
using UnityEngine;

namespace MissileCameraRemoteControl.Control
{
    /// <summary>Force Detonate through RcDetonateGate (impact-equivalent allow).</summary>
    internal static class RcDetonateUtil
    {
        internal static void Force(Missile? missile, Vector3? normal = null, bool hitTerrain = false)
        {
            if (missile == null || missile.disabled)
                return;

            Vector3 n = normal ?? (missile.rb != null ? missile.rb.velocity : Vector3.up);
            RcDetonateGate.AllowBegin();
            try
            {
                try
                {
                    if (!missile.IsArmed())
                        missile.Arm();
                }
                catch
                {
                    // ignore
                }

                missile.Detonate(n, hitArmor: false, hitTerrain: hitTerrain);
            }
            catch
            {
                // ignore
            }
            finally
            {
                RcDetonateGate.AllowEnd();
            }
        }
    }
}
